using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using Assembly = System.Reflection.Assembly;

namespace Dosaic.Testing.NUnit.Extensions
{
    public static class ArchitectureExtensions
    {
        public static Architecture FromCurrentAssembly(this ArchLoader archLoader)
        {
            return archLoader.LoadAssembly(Assembly.GetCallingAssembly()).Build();
        }

        public static Architecture FromAssemblies(this ArchLoader archLoader, params Assembly[] assemblies)
        {
            return archLoader.LoadAssemblies(assemblies).Build();
        }
    }
}
