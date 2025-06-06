using System.Reflection;
using Dosaic.Hosting.Abstractions.Services;

namespace Dosaic.Hosting.Abstractions.Extensions
{
    public static class ImplementationResolverExtensions
    {
        public static List<Type> FindTypes(this IImplementationResolver implementationResolver, Predicate<Type> filter)
            => implementationResolver.FindTypes().Where(f => filter == null || filter(f)).ToList();
        public static List<object> FindAndResolve(this IImplementationResolver implementationResolver, Predicate<Type> filter)
            => implementationResolver.FindTypes(t => (filter == null || filter(t)) && t.CanBeInstantiated())
                .Select(implementationResolver.ResolveInstance)
                .Where(x => x is not null)
                .ToList();
        public static List<T> FindAndResolve<T>(this IImplementationResolver implementationResolver)
            => implementationResolver.FindAndResolve(f => f.Implements<T>())
                .Where(x => x is not null)
                .OfType<T>().ToList();

        public static List<Assembly> FindAssemblies(this IImplementationResolver implementationResolver,
            Predicate<Assembly> filter) => implementationResolver.FindAssemblies().Where(f => filter(f)).ToList();
    }
}
