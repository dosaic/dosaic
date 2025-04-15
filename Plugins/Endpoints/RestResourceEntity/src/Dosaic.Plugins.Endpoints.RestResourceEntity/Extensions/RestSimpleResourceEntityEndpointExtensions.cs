using Dosaic.Plugins.Endpoints.RestResourceEntity.Endpoints;
using Dosaic.Plugins.Persistence.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Dosaic.Plugins.Endpoints.RestResourceEntity.Extensions
{
    public static class RestSimpleResourceEntityEndpointExtensions
    {
        public static RestSimpleResourceEndpointBuilder<T> AddSimpleRestResource<T>(
            this IEndpointRouteBuilder endpointRouteBuilder, IServiceProvider serviceProvider, string resource)
            where T : class, IIdentifier<Guid>
        {
            var defaultResponses = serviceProvider.GetRequiredService<GlobalResponseOptions>();
            return new RestSimpleResourceEndpointBuilder<T>(endpointRouteBuilder, resource, defaultResponses);
        }
    }
}
