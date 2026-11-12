using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.NpgSql.Tests
{
    public class EfCoreNpgSqlPluginTests
    {
        private static Action ConfigureServices(EfCoreNpgSqlConfiguration configuration) =>
            () => new EfCoreNpgSqlPlugin(configuration).ConfigureServices(new ServiceCollection());

        [Test]
        public void DefaultConfigurationStartsAndRegistersNothing()
        {
            var serviceCollection = new ServiceCollection();

            new EfCoreNpgSqlPlugin(new EfCoreNpgSqlConfiguration()).ConfigureServices(serviceCollection);

            serviceCollection.Should().BeEmpty();
        }

        [Test]
        public void ValidConnectionSettingsStart()
        {
            var configuration = new EfCoreNpgSqlConfiguration
            {
                SslMode = "verify-full",
                RootCertificate = "/does/not/exist/ca.crt",
                ConnectionParameters = "Timeout=5;Application Name=api"
            };

            ConfigureServices(configuration).Should().NotThrow();
        }

        [Test]
        public void InvalidSslModeStopsTheStartAndNamesTheKeyAndTheValidValues()
        {
            var configuration = new EfCoreNpgSqlConfiguration { SslMode = "sometimes" };

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
