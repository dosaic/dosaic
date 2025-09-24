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
                sd.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService) &&
                sd.ImplementationType == typeof(NpgsqlDbMigratorService<TestEfCoreDb>));

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
