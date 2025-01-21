using System.Reflection;

namespace Dosaic.Hosting.Abstractions.Services
{
    public interface IImplementationResolver
    {
        void ClearInstances();
        object ResolveInstance(Type type);
        List<Type> FindTypes();
        List<Assembly> FindAssemblies();
    }
}
