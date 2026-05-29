using System.Globalization;
using System.Text.Json.Serialization.Metadata;
#pragma warning disable IDE0005
using Dosaic.Extensions.Localization;
#pragma warning restore IDE0005
using Dosaic.Hosting.Abstractions;
using Dosaic.Hosting.Abstractions.Extensions;
using Dosaic.Hosting.Abstractions.Plugins;
using Dosaic.Hosting.Abstractions.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dosaic.Example.Service
{
    public class ExampleWebHost : IPluginEndpointsConfiguration, IPluginServiceConfiguration
    {
        private readonly IImplementationResolver _implementationResolver;
        private readonly ILogger _logger;

        public ExampleWebHost(IImplementationResolver implementationResolver, ILogger logger)
        {
            _implementationResolver = implementationResolver;
            _logger = logger;
        }

        public void ConfigureEndpoints(IEndpointRouteBuilder endpointRouteBuilder, IServiceProvider serviceProvider)
        {

            endpointRouteBuilder.MapGet("/hello", () =>
            {
                using (var activity = Tracing.StartActivity("SayHello"))
                {
                    activity?.SetTag("foo", 1);
                    activity?.SetTag("bar", "Hello, World!");
                    activity?.SetTag("baz", new int[] { 1, 2, 3 });
                }

                return "Hello, World!";
            });

            endpointRouteBuilder.MapGet("/secure", () =>
            {
                using (var activity = Tracing.StartActivity("SayHelloSecure"))
                {
                    activity?.SetTag("foo", 1);
                    activity?.SetTag("bar", "Hello, secure World!");
                    activity?.SetTag("baz", new int[] { 1, 2, 3 });
                }

                return "Hello, secure World!";
            }).RequireAuthorization();

            endpointRouteBuilder.MapGet("/delay", async () =>
            {
                using (var activity = Tracing.StartActivity("delay"))
                {
                    activity?.SetTag("foo", 1);
                    activity?.SetTag("bar", "delay");
                    activity?.SetTag("baz", new int[] { 1, 2, 3 });
                }

                await Task.Delay(5000, CancellationToken.None);

                using (var activity = Tracing.StartActivity("delay-end"))
                {
                    activity?.SetTag("foo", 2);
                    activity?.SetTag("bar", "delay");
                }

                return "done!";
            });
        }

        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            var x = _implementationResolver.FindAndResolve<IPluginActivateable>();
            _logger.LogDebug("Found {ItemCount} plugins", x.Count);

            serviceCollection.PostConfigure<JsonOptions>(options =>
       {
           var chain = options.SerializerOptions.TypeInfoResolverChain;
           if (chain.Count == 0)
           {
               chain.Add(new DefaultJsonTypeInfoResolver());
           }
       });
            serviceCollection.PostConfigure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
            {
                var chain = options.JsonSerializerOptions.TypeInfoResolverChain;
                if (chain.Count == 0)
                {
                    chain.Add(new DefaultJsonTypeInfoResolver());
                }
            });
            EntityLabels.DefaultCulture = Locale.De;
            _logger.LogDebug(EntityLabels.Get<Entry>());
            _logger.LogDebug(EntityLabels.Get<Entry>(x => x.Source));
            _logger.LogDebug(EntityLabels.Get<Entry>(x => x.Source, Locale.En));
            _logger.LogDebug(EntityLabels.Get<Entry>(x => x.Source, Locale.De));
            _logger.LogDebug(EntityLabels.Get("Entry.Source"));
            _logger.LogDebug(EntityLabels.Get("Entry.Source", Locale.En));
            _logger.LogDebug(EntityLabels.Get("Entry.Source", Locale.De));
            _logger.LogDebug(EntityLabels.Get<MyEnum>());
            _logger.LogDebug(EntityLabels.Get(MyEnum.FirstValue));
        }
    }
}
