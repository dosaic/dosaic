using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

using NUnit.Framework;
using System.Reflection;

namespace Dosaic.Plugins.Persistence.EfCore.NpgSql.Tests
{
    [TestFixture]
    public class PostgresEnumExtensionsTests
    {
        private ModelBuilder _modelBuilder;

        [SetUp]
        public void Setup()
        {
            _modelBuilder = new ModelBuilder();
        }

        [Test]
        public void MapDbEnums_WithEnumsInDbContext_DoesNotThrowException()
        {
            Action action = () => _modelBuilder.MapDbEnums<TestDbContextWithEnum>();

            action.Should().NotThrow();
        }

        [Test]
        public void MapDbEnums_WithDataSourceBuilder_ReturnsNonNullBuilder()
        {
            var connectionString = "Host=localhost;Database=test;Username=test;Password=test";
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);

            var result = dataSourceBuilder.MapDbEnums<TestDbContextWithEnum>();

            result.Should().NotBeNull();
            result.Should().BeOfType<NpgsqlDataSourceBuilder>();
        }

        [Test]
        public void UseDbEnums_WithDbContextOptionsBuilder_ReturnsNonNullBuilder()
        {
            var optionsBuilder = new DbContextOptionsBuilder<TestDbContextWithEnum>();
            var npgsqlOptionsBuilder = new NpgsqlDbContextOptionsBuilder(optionsBuilder);

            var result = npgsqlOptionsBuilder.UseDbEnums<TestDbContextWithEnum>();

            result.Should().NotBeNull();
            result.Should().BeOfType<NpgsqlDbContextOptionsBuilder>();
        }

        [Test]
        public void GetEnumTypes_WithNoEnums_ReturnsEmptyCollection()
        {
            var types = typeof(PostgresEnumExtensionsTests).Assembly.GetTypes()
                .Where(t => t.IsEnum && t.GetCustomAttribute<DbEnumAttribute>() != null)
                .ToHashSet();

            types.Should().HaveCount(1);
            types.First().Should().Be(typeof(TestEnum));
        }

        [Test]
        public void GetEnumTypes_WithDbEnumAttribute_ReturnsCorrectTypes()
        {
            var method = typeof(PostgresEnumExtensions).GetMethod("GetEnumTypes",
                BindingFlags.NonPublic | BindingFlags.Static);
            var enumTypes = method?.Invoke(null, new object[] { typeof(TestDbContextWithEnum) }) as HashSet<Type>;

            enumTypes.Should().NotBeNull();
            enumTypes.Should().HaveCount(1);
            enumTypes.Should().Contain(typeof(TestEnum));
        }

        private class TestDbContext : DbContext { }

        private class TestDbContextWithEnum : DbContext { }

        [DbEnum("test_enum", "public")]
        private enum TestEnum
        {
            Value1,
            Value2
        }
    }
}
