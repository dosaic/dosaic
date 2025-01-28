using System.Diagnostics.Metrics;
using Dosaic.Hosting.Abstractions;
using Dosaic.Hosting.Abstractions.Metrics;
using Dosaic.Hosting.Abstractions.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;
using Unleash;
using Unleash.Events;
using Unleash.Internal;

namespace Dosaic.Plugins.Management.Unleash
{
    public class UnleashPlugin(UnleashConfiguration unleashConfiguration, ILogger<UnleashPlugin> logger)
        : IPluginServiceConfiguration, IPluginHealthChecksConfiguration
    {
        internal const string UnleashPluginImpressionsTotal = "dosaic_unleash_plugin_impressions_total";
        internal const string UnleashPluginErrorsTotal = "dosaic_unleash_plugin_errors_total";
        internal const string UnleashPluginToggleUpdatesTotal = "dosaic_unleash_plugin_toggleUpdates_total";

        private readonly Counter<long> _impressionCounter =
            Metrics.CreateCounter<long>(UnleashPluginImpressionsTotal, "calls", "Total number of impression events");

        private readonly Counter<long> _errorCounter =
            Metrics.CreateCounter<long>(UnleashPluginErrorsTotal, "calls", "Total number of error events");

        private readonly Counter<long> _toggleUpdateCounter =
            Metrics.CreateCounter<long>(UnleashPluginToggleUpdatesTotal, "calls", "Total number of toggle update events");

        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            var settings = new UnleashSettings
            {
                AppName = unleashConfiguration.AppName,
                UnleashApi = new Uri(unleashConfiguration.ApiUri),
                InstanceTag = unleashConfiguration.InstanceTag,
                CustomHttpHeaders =
                    new Dictionary<string, string>() { { "Authorization", unleashConfiguration.ApiToken } }
            };

            var unleash = new DefaultUnleash(settings);

            // Set up handling of impression and error events
            unleash.ConfigureEvents(cfg =>
            {
                cfg.ImpressionEvent = HandleImpressionEvent;
                cfg.ErrorEvent = HandleErrorEvent;
                cfg.TogglesUpdatedEvent = HandleTogglesUpdatedEvent;
            });

            serviceCollection.AddSingleton<IUnleash>(unleash);

            serviceCollection.AddSingleton<IFeatureDefinitionProvider, UnleashFeatureDefinitionProvider>()
                .AddFeatureManagement()
                .AddFeatureFilter<UnleashFilter>()
                .UseDisabledFeaturesHandler(new FeatureNotEnabledDisabledHandler());
        }

        internal void HandleTogglesUpdatedEvent(TogglesUpdatedEvent evt)
        {
            _toggleUpdateCounter.Add(1);
            logger.LogInformation("Feature toggles updated on: {evt.UpdatedOn}", evt.UpdatedOn);
        }

        internal void HandleErrorEvent(ErrorEvent evt)
        {
            if (evt.Error != null)
            {
                _errorCounter.Add(1);
                logger.LogError("Unleash {UnleashError}  of type {UnleashErrorType} occured.", evt.Error, evt.ErrorType);
            }
            else
            {
                // cant find reason why this is happening and no proper error is returned
                // also seems not to degrade or interrupt the service in any kind of way
                logger.LogDebug("ignore or find out why this is null: {UnleashErrorType} {UnleashError}.",
                    evt.ErrorType, evt.Error);
            }
        }

        internal void HandleImpressionEvent(ImpressionEvent evt)
        {
            _impressionCounter.Add(1, new KeyValuePair<string, object>("featureName", evt.FeatureName),
                new KeyValuePair<string, object>("enabled", evt.Enabled));
            logger.LogDebug("ImpressionEvent: {UnleashFeatureName}: {UnleashEnabled}", evt.FeatureName,
                evt.Enabled);
        }

        public void ConfigureHealthChecks(IHealthChecksBuilder healthChecksBuilder)
        {
            var url = $"https://{unleashConfiguration.ApiUri}/health";
            healthChecksBuilder.AddUrlGroup(new Uri(url), "unleash", HealthStatus.Degraded,
                tags: [HealthCheckTag.Readiness.Value]);
        }
    }
}
