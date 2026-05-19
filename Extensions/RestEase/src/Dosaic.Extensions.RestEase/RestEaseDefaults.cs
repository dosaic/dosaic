using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Polly;
using Polly.Retry;

namespace Dosaic.Extensions.RestEase
{
    public static class RestEaseDefaults
    {
        public const string ResiliencePipelineName = "Dosaic.RestEase";

        public static JsonSerializerOptions CreateDefaultJsonOptions() => new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        public static ResiliencePipeline<HttpResponseMessage> CreateDefaultPipeline() =>
            new ResiliencePipelineBuilder<HttpResponseMessage>()
                .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromMilliseconds(250),
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .HandleResult(IsTransient),
                    DelayGenerator = RetryAfterDelay
                })
                .AddTimeout(TimeSpan.FromSeconds(100))
                .Build();

        [ExcludeFromCodeCoverage(Justification = "Thin pass-through to ComputeRetryDelay; helper is unit tested directly.")]
        private static ValueTask<TimeSpan?> RetryAfterDelay(RetryDelayGeneratorArguments<HttpResponseMessage> args)
            => ValueTask.FromResult(ComputeRetryDelay(args.Outcome.Result?.Headers.RetryAfter));

        internal static TimeSpan? ComputeRetryDelay(RetryConditionHeaderValue retryAfter)
        {
            if (retryAfter is null) return null;
            if (retryAfter.Delta.HasValue) return retryAfter.Delta.Value;
            return retryAfter.Date!.Value - DateTimeOffset.UtcNow;
        }

        private static bool IsTransient(HttpResponseMessage response)
        {
            var status = (int)response.StatusCode;
            return status >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout || response.StatusCode == HttpStatusCode.TooManyRequests;
        }
    }
}
