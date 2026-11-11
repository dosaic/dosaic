using Dosaic.Hosting.Abstractions.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Dosaic.Plugins.Persistence.EfCore.NpgSql
{
    /// <summary>
    ///     Builds the connection string from the <c>npgsql</c> section once at startup, so an invalid
    ///     <c>sslMode</c> or <c>connectionParameters</c> stops the host instead of failing the first DbContext.
    /// </summary>
    public class EfCoreNpgSqlPlugin(EfCoreNpgSqlConfiguration configuration) : IPluginServiceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            ServiceCollectionExtensions.CreateConnectionStringBuilder(configuration);
        }
    }
}
