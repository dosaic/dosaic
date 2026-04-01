using Dosaic.Hosting.Abstractions.Plugins;
using IdentityModel.AspNetCore.OAuth2Introspection;
using IdentityModel.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.Configuration;
using Zitadel.Authentication;
using Zitadel.Credentials;
using Zitadel.Extensions;

namespace Dosaic.Plugins.Authorization.Zitadel
{
    public class ZitadelPlugin(ZitadelConfiguration config, ILogger<ZitadelPlugin> logger)
        : IPluginServiceConfiguration, IPluginApplicationConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection
                .AddAuthorization()
                .AddAuthentication(ZitadelDefaults.AuthenticationScheme)
                .AddZitadelIntrospection(ZitadelDefaults.AuthenticationScheme, opts =>
                {
                    if (config.Host.StartsWith("http://") && config.UseHttps)
                    {
                        throw new InvalidConfigurationException("You cant use a HTTP Zitadel Host and require HTTPs.");
                    }

                    var host = config.Host.Replace("https://", "").Replace("http://", "").TrimEnd('/');
                    var scheme = config.UseHttps ? "https" : "http";
                    var authority = $"{scheme}://{host}";
                    opts.Authority = authority;
                    opts.JwtProfile = Application.LoadFromJsonString(config.JwtProfile);
                    opts.DiscoveryPolicy = new DiscoveryPolicy
                    {
                        RequireHttps = config.UseHttps,
                        ValidateEndpoints = config.ValidateEndpoints,
                        ValidateIssuerName = config.ValidateIssuer
                    };
                    opts.EnableCaching = config.EnableCaching;
                    opts.CacheDuration = TimeSpan.FromMinutes(config.CacheDurationInMinutes);
                    opts.CacheKeyPrefix = config.CacheKeyPrefix;
                    opts.Events.OnAuthenticationFailed += OnAuthenticationFailed;
                });

            serviceCollection.AddTransient<IManagementService, ManagementService>();
        }

        public void ConfigureApplication(IApplicationBuilder applicationBuilder)
        {
            applicationBuilder.UseAuthentication();
            applicationBuilder.UseAuthorization();
        }

        internal Task OnAuthenticationFailed(AuthenticationFailedContext context)
        {
            logger.LogError(context.Error);
            return Task.CompletedTask;
        }
    }
}
