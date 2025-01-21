using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Dosaic.Hosting.Abstractions.Plugins;
using Dosaic.Hosting.WebHost.Extensions;

namespace Dosaic.Hosting.WebHost.Tests.Extensions
{
    public class PluginExtensionsTests
    {
        [Test]
        public void PluginWithAppConfigOnlyShouldReturnCorrectName()
        {
            var pluginName = typeof(PluginWithAppConfigOnly).GetPluginName();
            pluginName.Should().Be("PluginWithAppConfigOnly(Application)");
        }

        [Test]
        public void PluginWithAllPluginActivateablesShouldReturnCorrectName()
        {
            var pluginName = typeof(PluginWithAllPluginActivateables).GetPluginName();
            pluginName.Should().Be("PluginWithAllPluginActivateables(Application, Service, Controller, Endpoints, HealthChecks)");
        }

        private class PluginWithAppConfigOnly : IPluginApplicationConfiguration
        {
            public void ConfigureApplication(IApplicationBuilder applicationBuilder)
            {
                //nothing
            }
        }

        private class PluginWithAllPluginActivateables : IPluginApplicationConfiguration, IPluginServiceConfiguration, IPluginControllerConfiguration,
            IPluginEndpointsConfiguration, IPluginHealthChecksConfiguration
        {
            public void ConfigureApplication(IApplicationBuilder applicationBuilder)
            {
                //nothing
            }

            public void ConfigureServices(IServiceCollection serviceCollection)
            {
                //nothing
            }

            public void ConfigureControllers(IMvcBuilder controllerBuilder)
            {
                //nothing
            }

            public void ConfigureEndpoints(IEndpointRouteBuilder endpointRouteBuilder, IServiceProvider serviceProvider)
            {
                //nothing
            }

            public void ConfigureHealthChecks(IHealthChecksBuilder healthChecksBuilder)
            {
                //nothing
            }
        }
    }
}
