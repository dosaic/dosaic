using Dosaic.Hosting.Abstractions.Attributes;

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
}
