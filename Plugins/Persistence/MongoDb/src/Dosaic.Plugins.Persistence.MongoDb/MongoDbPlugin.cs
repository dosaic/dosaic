using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Dosaic.Hosting.Abstractions;
using Dosaic.Hosting.Abstractions.Plugins;
using Dosaic.Plugins.Persistence.Abstractions;

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
            serviceCollection.AddTransient(typeof(IRepository<>), typeof(MongoRepository<>));
            serviceCollection.AddTransient(typeof(IReadRepository<>), typeof(MongoRepository<>));
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
                settings.Credential = MongoCredential.CreateCredential(_configuration.AuthDatabase ?? _configuration.Database, _configuration.User, _configuration.Password);
            }
            healthChecksBuilder.AddMongoDb(settings, _configuration.Database, "mongo", tags: new[] { HealthCheckTag.Readiness.Value });
        }
    }
}
