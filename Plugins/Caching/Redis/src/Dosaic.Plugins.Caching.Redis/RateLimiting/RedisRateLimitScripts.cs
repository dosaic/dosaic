namespace Dosaic.Plugins.Caching.Redis.RateLimiting
{
    /// <summary>
    ///     The limiter state lives in Redis and every decision is taken inside one script, so it is atomic
    ///     across all processes. Time always comes from the Redis server (<c>TIME</c>) — the clocks of the
    ///     calling processes never enter the calculation.
    ///     Every script takes exactly one key and returns <c>{acquired (0|1), retryAfterInMs}</c>.
    ///     A request for 0 permits only probes: it succeeds when at least one permit is free and takes none.
    /// </summary>
    internal static class RedisRateLimitScripts
    {
        private const string Now = """
            local t = redis.call('TIME')
            local now = tonumber(t[1]) * 1000 + math.floor(tonumber(t[2]) / 1000)

            """;

        /// <summary>ARGV: permitLimit, windowInMs, permits. State: hash { start, count }.</summary>
        public const string FixedWindow = Now + """
            local limit = tonumber(ARGV[1])
            local window = tonumber(ARGV[2])
            local permits = tonumber(ARGV[3])
            local state = redis.call('HMGET', KEYS[1], 'start', 'count')
            local start = tonumber(state[1])
            local count = tonumber(state[2])
            if start == nil or count == nil or now < start or now - start >= window then
              start = now
              count = 0
            end
            local need = permits
            if need == 0 then need = 1 end
            if count + need <= limit then
              if permits > 0 then
                redis.call('HSET', KEYS[1], 'start', string.format('%.0f', start), 'count', count + permits)
                redis.call('PEXPIRE', KEYS[1], window - (now - start))
              end
              return {1, 0}
            end
            return {0, math.max(1, start + window - now)}
            """;

        /// <summary>
        ///     ARGV: permitLimit, windowInMs, segmentsPerWindow, permits.
        ///     State: hash { segmentIndex -> permits taken in that segment }. Permits come back when the
        ///     segment they were taken in slides out of the window, like the BCL sliding window limiter.
        /// </summary>
        public const string SlidingWindow = Now + """
            local limit = tonumber(ARGV[1])
            local window = tonumber(ARGV[2])
            local segments = tonumber(ARGV[3])
            local permits = tonumber(ARGV[4])
            local segment = math.max(1, math.floor(window / segments))
            local current = math.floor(now / segment)
            local oldest = current - segments + 1
            local entries = redis.call('HGETALL', KEYS[1])
            local used = 0
            local live = {}
            for i = 1, #entries, 2 do
              local index = tonumber(entries[i])
              if index < oldest then
                redis.call('HDEL', KEYS[1], entries[i])
              else
                local taken = tonumber(entries[i + 1])
                used = used + taken
                live[#live + 1] = {index, taken}
              end
            end
            local need = permits
            if need == 0 then need = 1 end
            if used + need <= limit then
              if permits > 0 then
                redis.call('HINCRBY', KEYS[1], string.format('%.0f', current), permits)
                redis.call('PEXPIRE', KEYS[1], (segments + 1) * segment)
              end
              return {1, 0}
            end
            table.sort(live, function(a, b) return a[1] < b[1] end)
            local retry = segment
            local freed = 0
            for _, entry in ipairs(live) do
              freed = freed + entry[2]
              if used - freed + need <= limit then
                retry = (entry[1] + segments) * segment - now
                break
              end
            end
            return {0, math.max(1, retry)}
            """;

        /// <summary>
        ///     ARGV: tokenLimit, tokensPerPeriod, replenishmentPeriodInMs, permits.
        ///     State: hash { tokens, ts } where ts is the start of the current replenishment period.
        /// </summary>
        public const string TokenBucket = Now + """
            local limit = tonumber(ARGV[1])
            local perPeriod = tonumber(ARGV[2])
            local period = tonumber(ARGV[3])
            local permits = tonumber(ARGV[4])
            local state = redis.call('HMGET', KEYS[1], 'tokens', 'ts')
            local tokens = tonumber(state[1])
            local ts = tonumber(state[2])
            if tokens == nil or ts == nil then
              tokens = limit
              ts = now
            elseif now < ts then
              ts = now
            end
            local periods = math.floor((now - ts) / period)
            if periods > 0 then
              tokens = tokens + periods * perPeriod
              ts = ts + periods * period
            end
            if tokens > limit then tokens = limit end
            local need = permits
            if need == 0 then need = 1 end
            local acquired = 0
            local retry = 0
            if tokens >= need then
              acquired = 1
              tokens = tokens - permits
            else
              retry = math.max(1, ts + math.ceil((need - tokens) / perPeriod) * period - now)
            end
            redis.call('HSET', KEYS[1], 'tokens', tokens, 'ts', string.format('%.0f', ts))
            redis.call('PEXPIRE', KEYS[1], (math.ceil(limit / perPeriod) + 1) * period)
            return {acquired, retry}
            """;

        /// <summary>
        ///     ARGV: permitLimit, leaseTtlInMs, permits, leaseId.
        ///     State: sorted set { leaseId:n -> expiry }. A lease that is never released (crashed process)
        ///     stops counting once its expiry has passed.
        /// </summary>
        public const string Concurrency = Now + """
            local limit = tonumber(ARGV[1])
            local ttl = tonumber(ARGV[2])
            local permits = tonumber(ARGV[3])
            redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', now)
            local used = redis.call('ZCARD', KEYS[1])
            local need = permits
            if need == 0 then need = 1 end
            if used + need <= limit then
              if permits > 0 then
                local expiry = string.format('%.0f', now + ttl)
                for i = 1, permits do
                  redis.call('ZADD', KEYS[1], expiry, ARGV[4] .. ':' .. i)
                end
                redis.call('PEXPIRE', KEYS[1], ttl)
              end
              return {1, 0}
            end
            local retry = ttl
            local first = redis.call('ZRANGE', KEYS[1], 0, 0, 'WITHSCORES')
            if first[2] then retry = tonumber(first[2]) - now end
            return {0, math.max(1, retry)}
            """;

        /// <summary>
        ///     ARGV: leaseTtlInMs, leaseMember... Pushes the expiry of leases that are still held further out,
        ///     so a call may run longer than the lease TTL without losing its permit. <c>XX</c> only touches
        ///     members that still exist: a lease that was released, or that expired and whose permit was
        ///     handed to somebody else, is never brought back. Returns the number of renewed members.
        /// </summary>
        public const string RenewConcurrency = Now + """
            local ttl = tonumber(ARGV[1])
            local expiry = string.format('%.0f', now + ttl)
            local renewed = 0
            for i = 2, #ARGV do
              renewed = renewed + redis.call('ZADD', KEYS[1], 'XX', 'CH', expiry, ARGV[i])
            end
            if renewed > 0 then redis.call('PEXPIRE', KEYS[1], ttl) end
            return renewed
            """;
    }
}
