namespace Dosaic.Extensions.RestEase.Resilience
{
    public sealed class ResilienceConfig
    {
        public bool Enabled { get; set; }
        public int? MaxRetryAttempts { get; set; }
        public TimeSpan? BaseDelay { get; set; }
        public TimeSpan? AttemptTimeout { get; set; }
        public TimeSpan? TotalRequestTimeout { get; set; }
    }
}
