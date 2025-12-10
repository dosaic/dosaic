using AwesomeAssertions;
using Chronos;
using Chronos.Abstractions;
using Dosaic.Hosting.Abstractions;
using Dosaic.Testing.NUnit.Assertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.MongoDb
{
    public class MongoDbPluginTests
    {
        private readonly MongoDbConfiguration _mongoDbConfiguration = new()
        {
            Database = "testDatabase",
            Host = "testHost",
            Port = 9999,
            Password = "testPassword",
            User = "testUser",
            AuthDatabase = "testAuthDatabase"
        };

        [Test]
        public void ConfigureServicesWorks()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<IDateTimeProvider, DateTimeProvider>();

            // register dbContext and map as interface
            sc.AddSingleton<IMongoDbInstance, MongoDbInstance>();
            var plugin = new MongoDbPlugin(_mongoDbConfiguration);
            plugin.ConfigureServices(sc);
            var sp = sc.BuildServiceProvider();

            sp.Should().RegisterSources("MongoDB.Driver.Core.Extensions.DiagnosticSources");

            var mongoDbConfig = sp.GetRequiredService<MongoDbConfiguration>();
            mongoDbConfig.Should().NotBeNull();
            mongoDbConfig.Database.Should().Be(_mongoDbConfiguration.Database);
            mongoDbConfig.Host.Should().Be(_mongoDbConfiguration.Host);
            mongoDbConfig.AuthDatabase.Should().Be(_mongoDbConfiguration.AuthDatabase);
            mongoDbConfig.Port.Should().Be(_mongoDbConfiguration.Port);
            mongoDbConfig.User.Should().Be(_mongoDbConfiguration.User);
            mongoDbConfig.Password.Should().Be(_mongoDbConfiguration.Password);

            var mongoDbInstance = sp.GetRequiredService<IMongoDbInstance>();
            mongoDbInstance.Should().BeOfType<MongoDbInstance>();

        }

        [Test]
        public void ConfigureHealthChecksWorks()
        {
            var config = new MongoDbConfiguration
            {
                Host = "localhost",
                Port = 27017,
                Database = "test",
                User = "user",
                Password = "pw"
            };
            var plugin = new MongoDbPlugin(config);
            var hcBuilder = Substitute.For<IHealthChecksBuilder>();
            plugin.ConfigureHealthChecks(hcBuilder);
            hcBuilder.Received()
                .Add(Arg.Is<HealthCheckRegistration>(h =>
                    h.Name == "mongo" && h.Tags.Contains(HealthCheckTag.Readiness.Value)));
        }
    }
}
