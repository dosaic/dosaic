using System.Reflection;

namespace Dosaic.Hosting.Abstractions.Extensions
{
    public static class AssemblyExtensions
    {
        public static bool HasType(this Assembly assembly, Predicate<Type> typePredicate)
        {
            return assembly.GetTypes().Any(t => typePredicate(t));
        }

        public static IList<Type> GetTypes(this IEnumerable<Assembly> assemblies, Predicate<Type> typePredicate = null)
        {
            return assemblies.SelectMany(x => x.GetTypes(typePredicate)).ToList();
        }

        public static IList<Type> GetTypes(this Assembly assembly, Predicate<Type> typePredicate = null)
        {
            return assembly.GetTypes().Where(x => typePredicate == null || typePredicate(x)).ToList();
        }
    }
}
