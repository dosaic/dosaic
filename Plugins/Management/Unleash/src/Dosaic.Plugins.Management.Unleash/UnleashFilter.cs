using System.Diagnostics.Metrics;
using Dosaic.Hosting.Abstractions.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;
using Unleash;

namespace Dosaic.Plugins.Management.Unleash
{
    [FilterAlias(FilterAlias)]
    public class UnleashFilter : IFeatureFilter
    {
        private readonly ILogger<UnleashFilter> _logger;
        private readonly IUnleash _unleash;
        private readonly IHttpContextAccessor _httpContextAccessor;
        internal const string FilterAlias = "Unleash";
        internal const string UnleashPluginUnleashFilterCallsTotal = "dosaic_unleash_plugin_unleash_filter_calls_total";

        private readonly Counter<long> _filterCounter =
            Metrics.CreateCounter<long>(UnleashPluginUnleashFilterCallsTotal, "calls",
                "Total number of impression events");

        public UnleashFilter(ILogger<UnleashFilter> logger, IUnleash unleash, IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _unleash = unleash;
            _httpContextAccessor = httpContextAccessor;
        }

        public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context)
        {
            bool isEnabled;
            try
            {
                var unleashContext =
                    _httpContextAccessor?.HttpContext?.Items[UnleashMiddlware.Unleashcontext] as
                        UnleashContext;
                isEnabled = _unleash.IsEnabled(context.FeatureName, unleashContext);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"{nameof(EvaluateAsync)} has thrown an exception.");
                isEnabled = false;
            }

            _filterCounter.Add(1, new KeyValuePair<string, object>("featureName", context.FeatureName),
                new KeyValuePair<string, object>("isEnabled", isEnabled));
            return Task.FromResult(isEnabled);
        }
    }
}
