using Dosaic.Api.OpenApi.Filters.Document;
using Dosaic.Api.OpenApi.Filters.Operation;
using Dosaic.Api.OpenApi.Filters.Schema;
using Dosaic.Hosting.Abstractions.Attributes;
using Dosaic.Hosting.Abstractions.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Dosaic.Api.OpenApi
{
    [Configuration("openapi")]
    public class OpenApiConfiguration
    {
        public OpenApiAuthConfiguration Auth { get; set; }

        public class OpenApiAuthConfiguration
        {
            public bool Enabled { get; set; }
            public string TokenUrl { get; set; }
            public string AuthUrl { get; set; }
        }
    }
    public class OpenApiPlugin : IPluginApplicationConfiguration, IPluginServiceConfiguration, IPluginEndpointsConfiguration
    {
        private readonly OpenApiConfiguration _configuration;
        private readonly IHostEnvironment _environment;

        public OpenApiPlugin(OpenApiConfiguration configuration, IHostEnvironment environment)
        {
            _environment = environment;
            _configuration = configuration;
        }

        public void ConfigureApplication(IApplicationBuilder applicationBuilder)
        {
            applicationBuilder.UseSwagger();
            applicationBuilder.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("swagger/v1/swagger.json", _environment.ApplicationName);
                options.RoutePrefix = string.Empty;
                options.DisplayRequestDuration();
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
                options.SchemaFilter<ValueObjectSchemaFilter>(new object[] { documentationFiles });
                options.DocumentFilter<ValueObjectDocumentFilter>();
                options.OperationFilter<FormFileFilter>();

                options.EnableAnnotations();
                var authEnabled = _configuration.Auth?.Enabled ?? false;
                if (!authEnabled) return;
                var tokenUrl = new Uri(_configuration.Auth!.TokenUrl!);
                var authUrl = new Uri(_configuration.Auth!.AuthUrl!);
                options.AddSecurityDefinition(@"Bearer", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    In = ParameterLocation.Header,
                    Flows = new OpenApiOAuthFlows
                    {
                        AuthorizationCode = new OpenApiOAuthFlow { TokenUrl = tokenUrl, AuthorizationUrl = authUrl }
                        ,
                        ClientCredentials = new OpenApiOAuthFlow { TokenUrl = tokenUrl }
                        ,
                        Password = new OpenApiOAuthFlow { TokenUrl = tokenUrl }
                        ,
                        Implicit = new OpenApiOAuthFlow { AuthorizationUrl = authUrl }
                    }
                });
                options.AddSecurityRequirement(new OpenApiSecurityRequirement {
                    {
                        new OpenApiSecurityScheme {
                            Reference = new OpenApiReference {
                                Type = ReferenceType.SecurityScheme, Id = "Bearer"
                            }
                            , Scheme = "oauth2", Name = "Bearer", In = ParameterLocation.Header
                        }
                        , new List<string>()
                    }
                });
            });
        }

        public void ConfigureEndpoints(IEndpointRouteBuilder endpointRouteBuilder, IServiceProvider serviceProvider)
        {
            endpointRouteBuilder.MapSwagger();
        }
    }
}
