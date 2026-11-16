using Dosaic.Hosting.Abstractions.Extensions;
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
    public class ZitadelPlugin(ZitadelConfiguration config, ILogger<ZitadelPlugin> logger, IZitadelConfigurator[] configurators)
        : IPluginServiceConfiguration, IPluginApplicationConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            // built here, so a broken cookie configuration stops the start instead of the first request
            var tokenRetriever = ZitadelTokenRetrieval.Create(config.CookieToken);
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
            // post-configure runs after AddZitadelIntrospection has set its own retriever and events
            serviceCollection.PostConfigure<OAuth2IntrospectionOptions>(ZitadelDefaults.AuthenticationScheme, opts =>
            {
                opts.TokenRetriever = tokenRetriever;
                if (!config.MapProjectRoles) return;
                var onTokenValidated = opts.Events.OnTokenValidated;
                opts.Events.OnTokenValidated = async context =>
                {
                    if (onTokenValidated is not null) await onTokenValidated(context);
                    ZitadelProjectRoles.MapToRoleClaims(context.Principal, config.ProjectId);
                };
            });

            serviceCollection.AddTransient<IManagementService, ManagementService>();
        }

        public void ConfigureApplication(IApplicationBuilder applicationBuilder)
        {
            configurators.ForEach(x => x.ConfigureApplicationBeforeAuthentication(applicationBuilder));
            applicationBuilder.UseAuthentication();
            configurators.ForEach(x => x.ConfigureApplicationAfterAuthentication(applicationBuilder));
            configurators.ForEach(x => x.ConfigureApplicationBeforeAuthorization(applicationBuilder));
            applicationBuilder.UseAuthorization();
            configurators.ForEach(x => x.ConfigureApplicationAfterAuthorization(applicationBuilder));
        }

        internal Task OnAuthenticationFailed(AuthenticationFailedContext context)
        {
            logger.LogError(context.Error);
            return Task.CompletedTask;
        }
    }

    public interface IZitadelConfigurator : IPluginConfigurator
    {
        void ConfigureApplicationBeforeAuthentication(IApplicationBuilder applicationBuilder) { }
        void ConfigureApplicationAfterAuthentication(IApplicationBuilder applicationBuilder) { }
        void ConfigureApplicationBeforeAuthorization(IApplicationBuilder applicationBuilder) { }
        void ConfigureApplicationAfterAuthorization(IApplicationBuilder applicationBuilder) { }
    }
}
