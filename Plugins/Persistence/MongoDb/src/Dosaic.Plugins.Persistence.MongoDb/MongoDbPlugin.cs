using Dosaic.Hosting.Abstractions;
using Dosaic.Hosting.Abstractions.Plugins;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Dosaic.Plugins.Persistence.MongoDb
{
    public class MongoDbPlugin : IPluginServiceConfiguration, IPluginHealthChecksConfiguration
    {
        private readonly MongoDbConfiguration _configuration;

        public MongoDbPlugin(MongoDbConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton(_configuration);
            serviceCollection.AddSingleton<IMongoDbInstance, MongoDbInstance>();
        }

        public void ConfigureHealthChecks(IHealthChecksBuilder healthChecksBuilder)
        {
            var settings = new MongoClientSettings
            {
                Server = new MongoServerAddress(_configuration.Host, _configuration.Port),
                ConnectTimeout = TimeSpan.FromSeconds(5),
                HeartbeatInterval = TimeSpan.FromSeconds(5)
            };
            if (!string.IsNullOrEmpty(_configuration.User) && !string.IsNullOrEmpty(_configuration.Password))
            {
                settings.Credential = MongoCredential.CreateCredential(
                    _configuration.AuthDatabase ?? _configuration.Database, _configuration.User,
                    _configuration.Password);
            }

            healthChecksBuilder.AddMongoDb(sp =>
                    sp.GetRequiredService<IMongoDbInstance>().Client, _ => _configuration.Database, "mongo",
                tags: [HealthCheckTag.Readiness.Value]);
        }
    }
}
