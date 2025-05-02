using Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests;
using Dosaic.Testing.NUnit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.NpgSql.Tests
{
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
                IncludeErrorDetail = true
            };
        }

        [Test]
        public void ConfigureNpgSqlDatabaseWithDefaultParameters()
        {
            ServiceCollectionExtensions.ConfigureNpgSqlDatabase<TestEfCoreDb>(_serviceProvider, _builder, _config);

            _builder.Options.Should().NotBeNull();
        }

        [Test]
        public void ConfigureNpgSqlDatabaseWithCompiledModel()
        {
            var model = Substitute.For<Microsoft.EntityFrameworkCore.Metadata.IModel>();

            ServiceCollectionExtensions.ConfigureNpgSqlDatabase<TestEfCoreDb>(_serviceProvider, _builder, _config,
                model);

            _builder.Options.Should().NotBeNull();
        }

        [Test]
        public void ConfigureNpgSqlDatabaseWithNullConnectionPropertiesThrowsException()
        {
            var invalidConfig = new EfCoreNpgSqlConfiguration { Host = null, Database = null };

            Action act = () =>
                ServiceCollectionExtensions.ConfigureNpgSqlDatabase<TestEfCoreDb>(_serviceProvider, _builder,
                    invalidConfig);

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
    }
}
