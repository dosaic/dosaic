using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using Dosaic.Hosting.Abstractions.Extensions;
using Assembly = System.Reflection.Assembly;
using Type = System.Type;

namespace Dosaic.Testing.NUnit.Extensions
{
    public static class ArchitectureExtensions
    {
        private static readonly Type[] _types = AppDomain.CurrentDomain.GetAssemblies().GetTypes().ToArray();

        public static Type GetRealType(this Class cls)
        {
            return _types.FirstOrDefault(x => x.FullName == cls.FullName);
        }

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
