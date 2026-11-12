using AwesomeAssertions;
using Dosaic.Hosting.Abstractions.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.NpgSql.Tests
{
    public class EfCoreNpgSqlPluginTests
    {
        private static Action ConfigureServices(EfCoreNpgSqlConfiguration configuration) =>
            () => new EfCoreNpgSqlPlugin(configuration).ConfigureServices(new ServiceCollection());

        private static EfCoreNpgSqlConfiguration ValidConfiguration() =>
            new() { Host = "db", Database = "app", Username = "app", Password = "secret" };

        [Test]
        public void CompleteConfigurationStartsAndRegistersNothing()
        {
            var serviceCollection = new ServiceCollection();

            new EfCoreNpgSqlPlugin(ValidConfiguration()).ConfigureServices(serviceCollection);

            serviceCollection.Should().BeEmpty();
        }

        [Test]
        public void ValidConnectionSettingsStart()
        {
            var configuration = ValidConfiguration();
            configuration.SslMode = "verify-full";
            configuration.RootCertificate = "/does/not/exist/ca.crt";
            configuration.ConnectionParameters = "Timeout=5;Application Name=api";

            ConfigureServices(configuration).Should().NotThrow();
        }

        [Test]
        public void ConnectionParametersCanProvideTheRequiredKeys()
        {
            var configuration = new EfCoreNpgSqlConfiguration
            {
                ConnectionParameters = "Host=db;Database=app;Username=app;Password=secret"
            };

            ConfigureServices(configuration).Should().NotThrow();
        }

        [Test]
        public void MissingSectionStopsTheStartAndNamesTheSectionAndTheKeys()
        {
            var bind = () => new ConfigurationBuilder().Build()
                .GetSection("npgsql", typeof(EfCoreNpgSqlConfiguration));

            bind.Should().Throw<InvalidOperationException>()
                .WithMessage("Configuration section 'npgsql' is missing. EfCoreNpgSqlConfiguration is bound from it. " +
                             "Set at least npgsql:host, npgsql:database, npgsql:username " +
                             "(environment: NPGSQL_HOST, NPGSQL_DATABASE, NPGSQL_USERNAME).");
        }

        [Test]
        public void EmptyConfigurationStopsTheStartAndNamesTheSectionAndTheKeys()
        {
            ConfigureServices(new EfCoreNpgSqlConfiguration()).Should().Throw<InvalidOperationException>()
                .WithMessage("Configuration section 'npgsql' is missing or empty. Set npgsql:host, npgsql:database, " +
                             "npgsql:username (environment: NPGSQL_HOST, NPGSQL_DATABASE, NPGSQL_USERNAME) or Host, " +
                             "Database, Username in npgsql:connectionParameters.");
        }

        [Test]
        public void NullConfigurationIsTreatedAsAMissingSection()
        {
            ConfigureServices(null).Should().Throw<InvalidOperationException>()
                .WithMessage("Configuration section 'npgsql' is missing or empty.*");
        }

        [Test]
        public void IncompleteSectionStopsTheStartAndNamesOnlyTheMissingKeys()
        {
            var configuration = new EfCoreNpgSqlConfiguration { Host = "db", Password = "secret" };

            ConfigureServices(configuration).Should().Throw<InvalidOperationException>()
                .WithMessage("Configuration section 'npgsql' is incomplete. Set npgsql:database, npgsql:username " +
                             "(environment: NPGSQL_DATABASE, NPGSQL_USERNAME) or Database, Username in " +
                             "npgsql:connectionParameters.")
                .Where(x => !x.Message.Contains("secret"));
        }

        [Test]
        public void MigrationRetriesUntilTheHostStopsByDefault()
        {
            var migration = new EfCoreNpgSqlConfiguration().Migration;

            migration.MaxAttempts.Should().Be(0);
            migration.StopHostOnFailure.Should().BeFalse();
        }

        [Test]
        public void MigrationRetriesAreReadFromTheSection()
        {
            var configuration = (EfCoreNpgSqlConfiguration)new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["npgsql:host"] = "db",
                    ["npgsql:migration:maxAttempts"] = "5",
                    ["npgsql:migration:stopHostOnFailure"] = "true"
                }).Build()
                .GetSection("npgsql", typeof(EfCoreNpgSqlConfiguration));

            configuration.Migration.MaxAttempts.Should().Be(5);
            configuration.Migration.StopHostOnFailure.Should().BeTrue();
        }

        [Test]
        public void InvalidSslModeStopsTheStartAndNamesTheKeyAndTheValidValues()
        {
            var configuration = ValidConfiguration();
            configuration.SslMode = "sometimes";

            ConfigureServices(configuration).Should().Throw<InvalidOperationException>()
                .WithMessage("*npgsql:sslMode 'sometimes'*Disable, Allow, Prefer, Require, VerifyCA, VerifyFull*");
        }

        [Test]
        public void MalformedConnectionParametersStopTheStartWithoutRepeatingTheValue()
        {
            var configuration = new EfCoreNpgSqlConfiguration { ConnectionParameters = "Password=secret;broken" };

            ConfigureServices(configuration).Should().Throw<InvalidOperationException>()
                .Where(x => x.Message.Contains("npgsql:connectionParameters") && !x.Message.Contains("secret"));
        }
    }
}
