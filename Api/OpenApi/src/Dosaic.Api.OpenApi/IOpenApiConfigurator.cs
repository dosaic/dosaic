using Dosaic.Hosting.Abstractions.Plugins;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Dosaic.Api.OpenApi
{
    public interface IOpenApiConfigurator : IPluginConfigurator
    {
        void UseSwaggerUI(SwaggerUIOptions options);
        void UseSwagger(SwaggerOptions options);
        void AddSwaggerGen(SwaggerGenOptions options);
    }
}
