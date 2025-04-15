using Dosaic.Plugins.Persistence.VaultSharp.Secret;
using Microsoft.Extensions.DependencyInjection;

namespace Dosaic.Plugins.Persistence.VaultSharp
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddVaultSharpPlugin(this IServiceCollection serviceCollection,
            VaultConfiguration configuration)
        {
            new VaultSharpPlugin(configuration).ConfigureServices(serviceCollection);
            return serviceCollection;
        }

        public static IServiceCollection AddSecretStorage<T>(this IServiceCollection serviceCollection)
            where T : struct, Enum
        {
            return serviceCollection.AddSingleton<ISecretStorage<T>, SecretStorage<T>>();
        }
    }
}
