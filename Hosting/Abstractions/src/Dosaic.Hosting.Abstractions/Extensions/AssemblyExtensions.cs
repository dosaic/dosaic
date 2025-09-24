using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Dosaic.Hosting.Abstractions.Extensions
{
    public static class AssemblyExtensions
    {
        public static bool HasType(this Assembly assembly, Predicate<Type> typePredicate)
        {
            return GetAllTypesSafely(assembly).Any(t => typePredicate(t));
        }

        public static IList<Type> GetTypesSafely(this IEnumerable<Assembly> assemblies,
            Predicate<Type> typePredicate = null)
        {
            return assemblies.SelectMany(x => x.GetTypesSafely(typePredicate)).ToList();
        }

        public static IList<Type> GetTypesSafely(this Assembly assembly, Predicate<Type> typePredicate = null)
        {
            return GetAllTypesSafely(assembly).Where(x => typePredicate == null || typePredicate(x)).ToList();
        }

        [ExcludeFromCodeCoverage]
        private static Type[] GetAllTypesSafely(Assembly assembly)
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
    }
}
