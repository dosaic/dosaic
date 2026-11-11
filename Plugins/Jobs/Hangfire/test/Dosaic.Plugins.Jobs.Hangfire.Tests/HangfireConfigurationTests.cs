using AwesomeAssertions;
using Dosaic.Hosting.Abstractions.Extensions;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NUnit.Framework;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests
{
    public class HangfireConfigurationTests
    {
        private static HangfireConfiguration Bind(Dictionary<string, string> values) =>
            (HangfireConfiguration)new ConfigurationBuilder().AddInMemoryCollection(values).Build()
                .GetSection("hangfire", typeof(HangfireConfiguration));

        [Test]
        public void Config()
        {
            var config = HangfirePluginTests.GetConfiguration();

            config.ConnectionString.Should()
                .Be("Host=testHost;Port=9999;Database=testDatabase;Username=testUser;Password=testPassword;");
            config.Host.Should().Be("testHost");
            config.Port.Should().Be(9999);
            config.Database.Should().Be("testDatabase");
            config.InMemory.Should().Be(true);
            config.EnableJobsByFeatureManagementConfig.Should().Be(true);
            config.WorkerCount.Should().Be(200);
            config.AllowedDashboardHost.Should().Be("localhost");
            config.PollingIntervalInMs.Should().Be(1000);
            config.User.Should().Be("testUser");
            config.Password.Should().Be("testPassword");
            config.MaxJobArgumentsSizeToRenderInBytes.Should().Be(4096);
            config.InvisibilityTimeoutInMinutes.Should().Be(30);
        }

        [Test]
        public void UnchangedConfigurationGivesTheByteIdenticalConnectionStringAsBefore()
        {
            var config = HangfirePluginTests.GetConfiguration();

            config.ConnectionString.Should().Be(
                $"Host={config.Host};Port={config.Port};Database={config.Database};Username={config.User};Password={config.Password};");
        }

        [Test]
        public void ConfigurationWithoutAnyConnectionValueGivesAnEmptyConnectionString()
        {
            new HangfireConfiguration().ConnectionString.Should().BeEmpty();
        }

        [Test]
        public void DefaultsGiveTheSameEffectiveParametersAsTheInterpolatedString()
        {
            var config = HangfirePluginTests.GetConfiguration();
            var legacy = new NpgsqlConnectionStringBuilder(
                $"Host={config.Host};Port={config.Port};Database={config.Database};Username={config.User};Password={config.Password};");

            var built = new NpgsqlConnectionStringBuilder(config.ConnectionString);

            built.EquivalentTo(legacy).Should().BeTrue();
            built.Keys.Should().BeEquivalentTo(legacy.Keys);
        }

        [TestCase("with;semicolon")]
        [TestCase("with=equals")]
        [TestCase("with'single'quotes")]
        [TestCase("with\"double\"quotes")]
        [TestCase("p;Host=evil;SSL Mode=Disable")]
        [TestCase(" leading and trailing ")]
        public void PasswordWithSpecialCharactersRoundTrips(string password)
        {
            var config = HangfirePluginTests.GetConfiguration();
            config.Password = password;

            var built = new NpgsqlConnectionStringBuilder(config.ConnectionString);

            built.Password.Should().Be(password);
            built.Host.Should().Be("testHost");
            built.Keys.Should().HaveCount(5);
        }

        [Test]
        public void EveryNewKeyReachesTheConnectionString()
        {
            var config = HangfirePluginTests.GetConfiguration();
            config.SslMode = "verifyfull";
            config.RootCertificate = "/etc/ssl/ca.crt";
            config.MaxPoolSize = 7;
            config.NoResetOnClose = true;

            var built = new NpgsqlConnectionStringBuilder(config.ConnectionString);

            built.SslMode.Should().Be(SslMode.VerifyFull);
            built.RootCertificate.Should().Be("/etc/ssl/ca.crt");
            built.MaxPoolSize.Should().Be(7);
            built.NoResetOnClose.Should().BeTrue();
        }

        [Test]
        public void ConnectionParametersAreAppliedFirstAndDiscreteKeysOverrideThem()
        {
            var config = HangfirePluginTests.GetConfiguration();
            config.ConnectionParameters =
                "Host=rawHost;Port=1;Database=rawDatabase;Username=rawUser;Password=rawPassword;SSL Mode=Disable;" +
                "Maximum Pool Size=3;Timeout=5;Application Name=jobs";
            config.SslMode = "Require";
            config.MaxPoolSize = 9;

            var built = new NpgsqlConnectionStringBuilder(config.ConnectionString);

            built.Host.Should().Be("testHost");
            built.Port.Should().Be(9999);
            built.Database.Should().Be("testDatabase");
            built.Username.Should().Be("testUser");
            built.Password.Should().Be("testPassword");
            built.SslMode.Should().Be(SslMode.Require);
            built.MaxPoolSize.Should().Be(9);
            built.Timeout.Should().Be(5);
            built.ApplicationName.Should().Be("jobs");
        }

        [Test]
        public void ConnectionParametersFillInWhateverTheDiscreteKeysLeaveUnset()
        {
            var config = new HangfireConfiguration
            {
                ConnectionParameters = "Host=rawHost;Port=6432;Database=rawDatabase;No Reset On Close=true"
            };

            var built = new NpgsqlConnectionStringBuilder(config.ConnectionString);

            built.Host.Should().Be("rawHost");
            built.Port.Should().Be(6432);
            built.Database.Should().Be("rawDatabase");
            built.NoResetOnClose.Should().BeTrue();
        }

        [Test]
        public void PortIsOnlyAppliedWhenItIsPositive()
        {
            var config = HangfirePluginTests.GetConfiguration();
            config.Port = 0;

            new NpgsqlConnectionStringBuilder(config.ConnectionString).Port.Should().Be(NpgsqlConnection.DefaultPort);
            config.ConnectionString.Should().NotContain("Port");
        }

        [TestCase("sometimes")]
        [TestCase("3")]
        [TestCase("Require,Prefer")]
        [TestCase("verify-full")]
        public void InvalidSslModeThrowsAndNamesTheKey(string sslMode)
        {
            var config = HangfirePluginTests.GetConfiguration();
            config.SslMode = sslMode;

            var act = () => config.ConnectionString;

            act.Should().Throw<InvalidOperationException>()
                .WithMessage($"*hangfire:sslMode '{sslMode}'*VerifyFull*");
        }

        [Test]
        public void InvalidConnectionParametersThrowWithoutRepeatingTheValue()
        {
            var config = HangfirePluginTests.GetConfiguration();
            config.ConnectionParameters = "NotAKeyword=secret";

            var act = () => config.ConnectionString;

            act.Should().Throw<InvalidOperationException>()
                .Where(x => x.Message.Contains("hangfire:connectionParameters") && !x.Message.Contains("secret"));
        }

        [Test]
        public void EmptyStringsLeaveTheOptionalConnectionKeysUnset()
        {
            var config = HangfirePluginTests.GetConfiguration();
            config.SslMode = "";
            config.RootCertificate = "";
            config.ConnectionParameters = "";

            new NpgsqlConnectionStringBuilder(config.ConnectionString).Keys.Should().HaveCount(5);
        }

        [Test]
        public void EnvironmentStyleKeysBindToTheNewSettings()
        {
            var config = Bind(new Dictionary<string, string>
            {
                ["HANGFIRE:HOST"] = "db",
                ["HANGFIRE:SERVER:ENABLED"] = "false",
                ["HANGFIRE:SERVER:QUEUES:IMPORT"] = "40",
                ["HANGFIRE:SERVER:SHUTDOWNTIMEOUTINSECONDS"] = "120",
                ["HANGFIRE:PREPARESCHEMAIFNECESSARY"] = "false",
                ["HANGFIRE:REGISTERRECURRINGJOBS"] = "false",
                ["HANGFIRE:HEALTHCHECK:ENABLED"] = "false",
                ["HANGFIRE:HEALTHCHECK:MINIMUMAVAILABLESERVERS"] = "2",
                ["HANGFIRE:STATISTICSMETRICS:ENABLED"] = "false",
                ["HANGFIRE:SSLMODE"] = "VerifyFull",
                ["HANGFIRE:ROOTCERTIFICATE"] = "/ca.crt",
                ["HANGFIRE:MAXPOOLSIZE"] = "5",
                ["HANGFIRE:NORESETONCLOSE"] = "true",
                ["HANGFIRE:CONNECTIONPARAMETERS"] = "Timeout=5"
            });

            config.Server.Enabled.Should().BeFalse();
            config.Server.Queues.Should().BeEquivalentTo(new Dictionary<string, int> { ["IMPORT"] = 40 });
            config.Server.ShutdownTimeoutInSeconds.Should().Be(120);
            config.PrepareSchemaIfNecessary.Should().BeFalse();
            config.RegisterRecurringJobs.Should().BeFalse();
            config.HealthCheck.Enabled.Should().BeFalse();
            config.HealthCheck.MinimumAvailableServers.Should().Be(2);
            config.StatisticsMetrics.Enabled.Should().BeFalse();
            config.SslMode.Should().Be("VerifyFull");
            config.RootCertificate.Should().Be("/ca.crt");
            config.MaxPoolSize.Should().Be(5);
            config.NoResetOnClose.Should().BeTrue();
            config.ConnectionParameters.Should().Be("Timeout=5");
        }

        [Test]
        public void LaterLayerNarrowsTheQueueMapOfAnEarlierLayer()
        {
            var config = (HangfireConfiguration)new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["hangfire:host"] = "db",
                    ["hangfire:server:queues:import"] = "3",
                    ["hangfire:server:queues:mail"] = "2"
                })
                .AddInMemoryCollection(new Dictionary<string, string> { ["HANGFIRE:SERVER:QUEUES:MAIL"] = "0" })
                .Build()
                .GetSection("hangfire", typeof(HangfireConfiguration));

            config.Server.Queues.Should().HaveCount(2);
            config.Server.Queues.Should().Contain(x => x.Key.ToLowerInvariant() == "mail" && x.Value == 0);
            config.Server.Queues.Should().Contain(x => x.Key.ToLowerInvariant() == "import" && x.Value == 3);
        }

        [Test]
        public void TheConfigurationSchemaDescribesTheHangfireSection()
        {
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.schema.json");
            try
            {
                Hosting.WebHost.PluginWebHostBuilder.WriteConfigurationSchema([typeof(HangfireConfiguration)], path);

                var hangfire = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!["properties"]!["hangfire"]!
                    ["properties"]!;
                var server = hangfire["server"]!["properties"]!;
                server["enabled"]!["default"]!.GetValue<bool>().Should().BeTrue();
                server["queues"]!["additionalProperties"]!["type"]!.GetValue<string>().Should().Be("integer");
                hangfire["prepareSchemaIfNecessary"]!["default"]!.GetValue<bool>().Should().BeTrue();
                hangfire["queueConfigurations"]!["items"]!["properties"]!["prefetchCount"]!["default"]!
                    .GetValue<long>().Should().Be(1);
                hangfire["connectionString"].Should().BeNull("it is computed, not configured");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void UnrelatedKeysLeaveTheNewSettingsOnTheirDefaults()
        {
            var config = Bind(new Dictionary<string, string> { ["hangfire:host"] = "db" });

            config.Server.Enabled.Should().BeTrue();
            config.Server.Queues.Should().BeEmpty();
            config.PrepareSchemaIfNecessary.Should().BeTrue();
            config.RegisterRecurringJobs.Should().BeTrue();
            config.HealthCheck.Enabled.Should().BeTrue();
            config.HealthCheck.MinimumAvailableServers.Should().BeNull();
            config.StatisticsMetrics.Enabled.Should().BeTrue();
            config.MaxPoolSize.Should().BeNull();
            config.NoResetOnClose.Should().BeFalse();
        }
    }
}
