using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Dosaic.Hosting.Abstractions.Extensions
{
    public static class AssemblyExtensions
    {
        [ExcludeFromCodeCoverage]
        private static Type[] GetTypesSafe(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch
            {
                return [];
            }
        }

        public static bool HasType(this Assembly assembly, Predicate<Type> typePredicate)
        {
            return GetTypesSafe(assembly).Any(t => typePredicate(t));
        }

        public static IList<Type> GetTypes(this IEnumerable<Assembly> assemblies, Predicate<Type> typePredicate = null)
        {
            return assemblies.SelectMany(x => x.GetTypes(typePredicate)).ToList();
        }

        public static IList<Type> GetTypes(this Assembly assembly, Predicate<Type> typePredicate = null)
        {
            return GetTypesSafe(assembly).Where(x => typePredicate == null || typePredicate(x)).ToList();
        }
    }
}
