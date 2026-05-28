using System.Net;
using Microsoft.Extensions.Http.Resilience;

namespace Dosaic.Extensions.RestEase.Resilience
{
    public sealed class ResilienceConfig
    {
        public bool Enabled { get; set; }
        public int? MaxRetryAttempts { get; set; }
        public TimeSpan? BaseDelay { get; set; }
        public TimeSpan? AttemptTimeout { get; set; }
        public TimeSpan? TotalRequestTimeout { get; set; }
        public HashSet<HttpStatusCode> AdditionalRetryStatusCodes { get; set; } = new();
        public Action<HttpRetryStrategyOptions> ConfigureRetry { get; set; }
    }
}
