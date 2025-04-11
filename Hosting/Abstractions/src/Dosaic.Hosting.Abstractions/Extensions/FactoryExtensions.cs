using Dosaic.Hosting.Abstractions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Dosaic.Hosting.Abstractions.Extensions
{
    public static class FactoryExtensions
    {
        public static IServiceCollection AddFactory<TService>(this IServiceCollection serviceCollection,
            ServiceLifetime lifetime = ServiceLifetime.Transient)
            where TService : class
        {
            serviceCollection.Add(ServiceDescriptor.Describe(typeof(IFactory<TService>),
                sp => new Factory<TService>(sp),
                lifetime));

            return serviceCollection;
        }

        public static IServiceCollection AddFactory(this IServiceCollection serviceCollection, Type type,
            ServiceLifetime lifetime = ServiceLifetime.Transient)

        {
            serviceCollection.Add(ServiceDescriptor.Describe(typeof(IFactory<>).MakeGenericType(type),
                sp => Activator.CreateInstance(typeof(Factory<>).MakeGenericType(type), sp),
                lifetime));

            return serviceCollection;
        }
    }
}
