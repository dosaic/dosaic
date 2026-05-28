using Dosaic.Api.OpenApi.Filters.Document;
using Dosaic.Api.OpenApi.Filters.Schema;
using Dosaic.Api.OpenApi.Schema;
using Dosaic.Hosting.Abstractions.Extensions;
using Dosaic.Hosting.Abstractions.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Dosaic.Api.OpenApi
{

    public class OpenApiPlugin : IPluginApplicationConfiguration, IPluginServiceConfiguration,
        IPluginEndpointsConfiguration
    {
        private readonly OpenApiConfiguration _configuration;
        private readonly IHostEnvironment _environment;
        private readonly IOpenApiConfigurator[] _configurators;

        public OpenApiPlugin(OpenApiConfiguration configuration, IHostEnvironment environment, IOpenApiConfigurator[] configurators)
        {
            _environment = environment;
            _configuration = configuration;
            _configurators = configurators;
        }

        public void ConfigureApplication(IApplicationBuilder applicationBuilder)
        {
            applicationBuilder.UseSwagger(
                options => _configurators.ForEach(x => x.UseSwagger(options))
            );
            applicationBuilder.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("swagger/v1/swagger.json", _environment.ApplicationName);
                options.RoutePrefix = string.Empty;
                options.DisplayRequestDuration();
                _configurators.ForEach(x => x.UseSwaggerUI(options));
            });
        }

        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddEndpointsApiExplorer();
            serviceCollection.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = _environment.ApplicationName, Version = "v1" });
                var documentationFiles = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.xml");

                foreach (var file in documentationFiles)
                    options.IncludeXmlComments(file);

                options.SchemaFilter<ReadOnlyPropertySchemaFilter>();
                options.SchemaFilter<OpenApiIgnoreSchemaFilter>();
                options.SchemaFilter<EnumSummarySchemaFilter>(new object[] { documentationFiles });
                options.SchemaFilter<ValueObjectSchemaFilter>(new object[] { documentationFiles });
                options.DocumentFilter<ValueObjectDocumentFilter>();

                options.EnableAnnotations();
                var authEnabled = _configuration.Auth?.Enabled ?? false;
                if (authEnabled)
                {
                    var tokenUrl = new Uri(_configuration.Auth!.TokenUrl!);
                    var authUrl = new Uri(_configuration.Auth!.AuthUrl!);
                    options.AddSecurityDefinition("bearer",
                        new OpenApiSecurityScheme
                        {
                            Scheme = "bearer",
                            BearerFormat = "JWT",
                            Description = "JWT Authorization header using the Bearer scheme.",
                            Type = SecuritySchemeType.OAuth2,
                            In = ParameterLocation.Header,
                            Flows = new OpenApiOAuthFlows
                            {
                                AuthorizationCode =
                                    new OpenApiOAuthFlow { TokenUrl = tokenUrl, AuthorizationUrl = authUrl },
                                ClientCredentials = new OpenApiOAuthFlow { TokenUrl = tokenUrl },
                                Password = new OpenApiOAuthFlow { TokenUrl = tokenUrl },
                                Implicit = new OpenApiOAuthFlow { AuthorizationUrl = authUrl },
                            }
                        });

                    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("bearer", document)] = []
                    });
                }
                _configurators.ForEach(x => x.AddSwaggerGen(options));
            });
        }

        public void ConfigureEndpoints(IEndpointRouteBuilder endpointRouteBuilder, IServiceProvider serviceProvider)
        {
            endpointRouteBuilder.MapSwagger();
        }
    }
}
