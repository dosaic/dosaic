using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using AwesomeAssertions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests;
using Dosaic.Testing.NUnit;
using Dosaic.Testing.NUnit.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.NpgSql.Tests
{
    [SuppressMessage("Usage", "EF1001:Internal EF Core API usage.")]
    public class ServiceCollectionExtensionsTests
    {
        private IServiceProvider _serviceProvider;
        private ILoggerFactory _loggerFactory;
        private DbContextOptionsBuilder _builder;
        private EfCoreNpgSqlConfiguration _config;
        private ILogger<TestEfCoreDb> _logger;

        [SetUp]
        public void Setup()
        {
            _loggerFactory = Substitute.For<ILoggerFactory>();
            _logger = Substitute.For<ILogger<TestEfCoreDb>>();
            _loggerFactory.CreateLogger<TestEfCoreDb>().Returns(_logger);

            _serviceProvider = TestingDefaults.ServiceProvider();

            _builder = new DbContextOptionsBuilder<TestEfCoreDb>();

            _config = new EfCoreNpgSqlConfiguration
            {
                Host = "localhost",
                Port = 5432,
                Username = "user",
                Password = "pass",
                Database = "testdb",
                ConnectionLifetime = 30,
                KeepAlive = 60,
                MaxPoolSize = 100,
                IncludeErrorDetail = true,
                EnableDetailedErrors = true,
                EnableSensitiveDataLogging = true,
                SplitQuery = true
            };
        }

        [Test]
        public void ConfigureNpgSqlDatabaseWithDefaultParameters()
        {
            _builder.ConfigureNpgSqlContext<TestEfCoreDb>(_serviceProvider, _config);

            _builder.Options.Should().NotBeNull();
            _builder.Options.Should().NotBeNull();

            var extensions = _builder.Options.Extensions.ToList();
            extensions[0].Should()
                .BeOfType<Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal.NpgsqlOptionsExtension>();
            extensions[1].Should().BeOfType<Microsoft.EntityFrameworkCore.Infrastructure.CoreOptionsExtension>();
            extensions[2].GetType().FullName.Should().Be("NeinLinq.RewriteDbContextOptionsExtension");
            var npgsqlOptions = extensions[0]
                .As<Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal.NpgsqlOptionsExtension>();

            npgsqlOptions.EnumDefinitions.Should().HaveCount(2);
            npgsqlOptions.EnumDefinitions[0].ClrType.Should().Be(typeof(TestEnumType));
            npgsqlOptions.EnumDefinitions[1].ClrType.Should().Be(typeof(ChangeState));
            npgsqlOptions.QuerySplittingBehavior.Should().Be(QuerySplittingBehavior.SplitQuery);
            npgsqlOptions.DataSource!.ConnectionString.Should().Be(
                "Host=localhost;Port=5432;Username=user;Database=testdb;Connection Lifetime=30;Keepalive=60;Maximum Pool Size=100;Array Nullability Mode=PerInstance;Include Error Detail=True");

            var coreOptions = extensions[1].As<Microsoft.EntityFrameworkCore.Infrastructure.CoreOptionsExtension>();
            coreOptions.DetailedErrorsEnabled.Should().BeTrue();
            coreOptions.IsSensitiveDataLoggingEnabled.Should().BeTrue();
            coreOptions.LoggingCacheTime.Should().Be(TimeSpan.FromMinutes(5));
            coreOptions.WarningsConfiguration.DefaultBehavior.Should().Be(WarningBehavior.Log);
        }

        [Test]
        public void ConfigureNpgSqlDatabaseWithCustomParameters()
        {
            var customConfig = new EfCoreNpgSqlConfiguration
            {
                Host = "external",
                Port = 1337,
                Username = "anon",
                Password = "notsafe",
                Database = "none",
                ConnectionLifetime = 3,
                KeepAlive = 6,
                MaxPoolSize = 1,
                IncludeErrorDetail = false,
                EnableDetailedErrors = false,
                EnableSensitiveDataLogging = false,
                SplitQuery = false,
                ConfigureLoggingCacheTimeInSeconds = 1
            };
            _builder.ConfigureNpgSqlContext<TestEfCoreDb>(_serviceProvider, customConfig, c =>
            {
                c.WithWarnings(x => x.Log((CoreEventId.RowLimitingOperationWithoutOrderByWarning, LogLevel.Debug)));
            });

            _builder.Options.Should().NotBeNull();
            _builder.Options.Should().NotBeNull();

            var extensions = _builder.Options.Extensions.ToList();
            extensions[0].Should()
                .BeOfType<Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal.NpgsqlOptionsExtension>();
            extensions[1].Should().BeOfType<Microsoft.EntityFrameworkCore.Infrastructure.CoreOptionsExtension>();
            extensions[2].GetType().FullName.Should().Be("NeinLinq.RewriteDbContextOptionsExtension");
            var npgsqlOptions = extensions[0]
                .As<Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal.NpgsqlOptionsExtension>();

            npgsqlOptions.EnumDefinitions.Should().HaveCount(2);
            npgsqlOptions.EnumDefinitions[0].ClrType.Should().Be(typeof(TestEnumType));
            npgsqlOptions.EnumDefinitions[1].ClrType.Should().Be(typeof(ChangeState));
            npgsqlOptions.QuerySplittingBehavior.Should().Be(QuerySplittingBehavior.SingleQuery);
            npgsqlOptions.DataSource!.ConnectionString.Should().Be(
                "Host=external;Port=1337;Username=anon;Database=none;Connection Lifetime=3;Keepalive=6;Maximum Pool Size=1;Array Nullability Mode=PerInstance;Include Error Detail=False");

            var coreOptions = extensions[1].As<Microsoft.EntityFrameworkCore.Infrastructure.CoreOptionsExtension>();
            coreOptions.DetailedErrorsEnabled.Should().BeFalse();
            coreOptions.IsSensitiveDataLoggingEnabled.Should().BeFalse();
            coreOptions.LoggingCacheTime.Should().Be(TimeSpan.FromSeconds(1));
            coreOptions.WarningsConfiguration.DefaultBehavior.Should().Be(WarningBehavior.Log);
            var warnings =
                (ImmutableSortedDictionary<int, (WarningBehavior?, LogLevel?)>)coreOptions.WarningsConfiguration
                    .GetInaccessibleValue(
                        "_explicitBehaviors");
            warnings.Should().NotBeNull();
            warnings.Should().Contain(x =>
                x.Key == CoreEventId.RowLimitingOperationWithoutOrderByWarning &&
                x.Value.Item1 == WarningBehavior.Log && x.Value.Item2 == LogLevel.Debug);
        }

        private Npgsql.NpgsqlConnectionStringBuilder GetDataSourceConnectionString(
            Action<NpgSqlConfiguration> configure = null)
        {
            _builder.ConfigureNpgSqlContext<TestEfCoreDb>(_serviceProvider, _config, configure);
            var npgsqlOptions = _builder.Options.Extensions
                .OfType<Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal.NpgsqlOptionsExtension>().Single();
            return new Npgsql.NpgsqlConnectionStringBuilder(npgsqlOptions.DataSource!.ConnectionString);
        }

        [Test]
        public void DefaultsDoNotAddAnyOfTheNewConnectionParameters()
        {
            var connectionString = ServiceCollectionExtensions.CreateConnectionStringBuilder(_config).ConnectionString;

            connectionString.Should().Be(
                "Host=localhost;Port=5432;Username=user;Password=pass;Database=testdb;Connection Lifetime=30;Keepalive=60;Maximum Pool Size=100;Array Nullability Mode=PerInstance;Include Error Detail=True");
        }

        [Test]
        public void EmptyStringsLeaveTheNewConnectionParametersUnset()
        {
            var expected = ServiceCollectionExtensions.CreateConnectionStringBuilder(_config).ConnectionString;
            _config.SslMode = "";
            _config.RootCertificate = "";
            _config.ConnectionParameters = "";

            ServiceCollectionExtensions.CreateConnectionStringBuilder(_config).ConnectionString.Should().Be(expected);
        }

        [Test]
        public void TlsAndPoolResetSettingsReachTheDataSource()
        {
            _config.SslMode = "verifyfull";
            _config.RootCertificate = "/etc/ssl/ca.crt";
            _config.NoResetOnClose = true;

            var connectionString = GetDataSourceConnectionString();

            connectionString.SslMode.Should().Be(Npgsql.SslMode.VerifyFull);
            connectionString.RootCertificate.Should().Be("/etc/ssl/ca.crt");
            connectionString.NoResetOnClose.Should().BeTrue();
        }

        [Test]
        public void ConnectionParametersAreAppliedFirstAndDiscreteSettingsOverrideThem()
        {
            _config.ConnectionParameters =
                "Host=rawHost;Maximum Pool Size=3;SSL Mode=Disable;Timeout=5;Application Name=api";
            _config.SslMode = "Require";

            var connectionString = GetDataSourceConnectionString();

            connectionString.Host.Should().Be("localhost");
            connectionString.MaxPoolSize.Should().Be(100);
            connectionString.SslMode.Should().Be(Npgsql.SslMode.Require);
            connectionString.Timeout.Should().Be(5);
            connectionString.ApplicationName.Should().Be("api");
        }

        [Test]
        public void DefaultsOnlyFillInWhatTheConnectionParametersLeaveUnset()
        {
            // pgBouncer: nothing but host and credentials configured discretely
            var config = new EfCoreNpgSqlConfiguration
            {
                Host = "pgbouncer",
                Username = "user",
                ConnectionParameters = "Port=6432;Maximum Pool Size=20;Keepalive=0;Include Error Detail=true"
            };

            var connectionString = ServiceCollectionExtensions.CreateConnectionStringBuilder(config);

            connectionString.Port.Should().Be(6432);
            connectionString.MaxPoolSize.Should().Be(20);
            connectionString.KeepAlive.Should().Be(0);
            connectionString.IncludeErrorDetail.Should().BeTrue();
            connectionString.ConnectionLifetime.Should().Be(60, "the parameters leave it unset");
            connectionString.Host.Should().Be("pgbouncer");
        }

        [Test]
        public void WithoutConnectionParametersTheDefaultsApply()
        {
            var connectionString =
                ServiceCollectionExtensions.CreateConnectionStringBuilder(new EfCoreNpgSqlConfiguration());

            connectionString.Port.Should().Be(5432);
            connectionString.MaxPoolSize.Should().Be(100);
            connectionString.KeepAlive.Should().Be(15);
            connectionString.ConnectionLifetime.Should().Be(60);
            connectionString.IncludeErrorDetail.Should().BeFalse();
        }

        [Test]
        public void ConfiguredValuesOverrideTheConnectionParametersEvenWhenTheyEqualTheDefault()
        {
            var config = new EfCoreNpgSqlConfiguration
            {
                Port = 5432,
                MaxPoolSize = 100,
                ConnectionParameters = "Port=6432;Maximum Pool Size=20;Array Nullability Mode=Always"
            };

            var connectionString = ServiceCollectionExtensions.CreateConnectionStringBuilder(config);

            connectionString.Port.Should().Be(5432);
            connectionString.MaxPoolSize.Should().Be(100);
            connectionString.ArrayNullabilityMode.Should().Be(Npgsql.ArrayNullabilityMode.PerInstance);
        }

        [Test]
        public void DataSourceHookOfTheConsumerRunsAfterTheConnectionSettings()
        {
            _config.SslMode = "Require";
            Npgsql.SslMode? seenByHook = null;

            GetDataSourceConnectionString(c => c.WithDataSource(dataSource =>
            {
                seenByHook = dataSource.ConnectionStringBuilder.SslMode;
                dataSource.ConnectionStringBuilder.SslMode = Npgsql.SslMode.VerifyCA;
            })).SslMode.Should().Be(Npgsql.SslMode.VerifyCA);

            seenByHook.Should().Be(Npgsql.SslMode.Require);
        }

        [TestCase("sometimes")]
        [TestCase("3")]
        [TestCase("verify-full")]
        public void InvalidSslModeThrowsAndNamesTheKey(string sslMode)
        {
            _config.SslMode = sslMode;

            var act = () => _builder.ConfigureNpgSqlContext<TestEfCoreDb>(_serviceProvider, _config);

            act.Should().Throw<InvalidOperationException>().WithMessage($"*npgsql:sslMode '{sslMode}'*");
        }

        [Test]
        public void InvalidConnectionParametersThrowWithoutRepeatingTheValue()
        {
            _config.ConnectionParameters = "NotAKeyword=secret";

            var act = () => _builder.ConfigureNpgSqlContext<TestEfCoreDb>(_serviceProvider, _config);

            act.Should().Throw<InvalidOperationException>()
                .Where(x => x.Message.Contains("npgsql:connectionParameters") && !x.Message.Contains("secret"));
        }

        [Test]
        public void ConfigureNpgSqlDatabaseWithCompiledModel()
        {
            var model = Substitute.For<Microsoft.EntityFrameworkCore.Metadata.IModel>();

            _builder.ConfigureNpgSqlContext<TestEfCoreDb>(_serviceProvider, _config, c => c.WithModel(model));

            _builder.Options.Should().NotBeNull();
            var extensions = _builder.Options.Extensions.ToList();
            extensions[0].Should()
                .BeOfType<Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal.NpgsqlOptionsExtension>();
            extensions[1].Should().BeOfType<Microsoft.EntityFrameworkCore.Infrastructure.CoreOptionsExtension>();

            extensions[1].As<Microsoft.EntityFrameworkCore.Infrastructure.CoreOptionsExtension>().Model.Should()
                .Be(model);
        }

        [Test]
        public void ConfigureNpgSqlDatabaseWithNullConnectionPropertiesThrowsException()
        {
            var invalidConfig = new EfCoreNpgSqlConfiguration { Host = null, Database = null };

            var act = () =>
                _builder.ConfigureNpgSqlContext<TestEfCoreDb>(_serviceProvider, invalidConfig);

            act.Should().Throw<Exception>();
        }

        [Test]
        public void AddNpgsqlDbMigratorService_ShouldRegisterNpgsqlDbMigratorService()
        {
            var services = TestingDefaults.ServiceCollection();

            services.AddNpgsqlDbMigratorService<TestEfCoreDb>();

            var serviceDescriptor = services.FirstOrDefault(sd =>
                sd.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                && sd.ImplementationType == typeof(NpgsqlDbMigratorService<TestEfCoreDb>));

            serviceDescriptor.Should().NotBeNull();
            serviceDescriptor?.Lifetime.Should().Be(ServiceLifetime.Singleton);
        }

        [Test]
        public void CanConfigureDeep()
        {
            var model = Substitute.For<Microsoft.EntityFrameworkCore.Metadata.IModel>();

            _builder.ConfigureNpgSqlContext<TestEfCoreDb>(_serviceProvider, _config, c =>
            {
                c.WithModel(model)
                    .WithWarnings(x => x.Log((CoreEventId.RowLimitingOperationWithoutOrderByWarning, LogLevel.Debug)))
                    .WithDataSource(x => x.ConfigureTracing(b => b.ConfigureCommandFilter(y => y.IsPrepared)))
                    .WithNpgSql(x => x.CommandTimeout(5000))
                    ;
            });
            _builder.Options.Should().NotBeNull();
            var extensions = _builder.Options.Extensions.ToList();
            var npgsqlOptions = extensions[0]
                .As<Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal.NpgsqlOptionsExtension>();
            npgsqlOptions.CommandTimeout.Should().Be(5000);
            var coreOptions = extensions[1].As<Microsoft.EntityFrameworkCore.Infrastructure.CoreOptionsExtension>();
            coreOptions.Model.Should().BeSameAs(model);
            var warnings =
                (ImmutableSortedDictionary<int, (WarningBehavior?, LogLevel?)>)coreOptions.WarningsConfiguration
                    .GetInaccessibleValue(
                        "_explicitBehaviors");
            warnings.Should().NotBeNull();
            warnings.Should().Contain(x =>
                x.Key == CoreEventId.RowLimitingOperationWithoutOrderByWarning &&
                x.Value.Item1 == WarningBehavior.Log && x.Value.Item2 == LogLevel.Debug);
        }
    }
}
