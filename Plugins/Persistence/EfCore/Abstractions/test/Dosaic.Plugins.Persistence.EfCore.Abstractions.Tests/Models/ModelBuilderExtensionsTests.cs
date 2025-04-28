using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Eventsourcing;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Models
{
    public class ModelBuilderExtensionsTests
    {
        private ModelBuilder _modelBuilder;

        [SetUp]
        public void Setup()
        {
            _modelBuilder = new ModelBuilder(new Microsoft.EntityFrameworkCore.Metadata.Conventions.ConventionSet());
            _modelBuilder.ApplyConfigurationsFromAssembly(typeof(TestModel).Assembly);

            _modelBuilder.Entity<TestHistoryModel>();
            _modelBuilder.Entity<TestAggregate>();
            _modelBuilder.Entity<TestAuditModel>();
            _modelBuilder.Owned<TestOwnedModel>();
        }

        [Test]
        public void ApplyEnumFieldsConvertsEnumPropertiesToDbEnumType()
        {
            _modelBuilder.ApplyEnumFields();

            var entity = _modelBuilder.Model.FindEntityType(typeof(TestModel));
            var property = entity!.FindProperty(nameof(TestModel.EnumProperty));

            property!.GetColumnType().Should().Be("custom.test_enum");
        }

        [Test]
        public void ApplyEnumFieldsHandlesNullableEnumProperties()
        {
            _modelBuilder.ApplyEnumFields();

            var entity = _modelBuilder.Model.FindEntityType(typeof(TestModel));
            var property = entity!.FindProperty(nameof(TestModel.NullableEnumProperty));

            property!.GetColumnType().Should().Be("custom.test_enum");
        }

        [Test]
        public void ApplyEnumFieldsThrowsExceptionWhenEnumDoesNotHaveDbEnumAttribute()
        {
            _modelBuilder.Entity<TestModelWithMissingDbEnumAttribute>().Property(x => x.EnumWithoutAttribute);

            var act = () => _modelBuilder.ApplyEnumFields();

            act.Should().Throw<ArgumentException>()
                .WithMessage($"*{nameof(DbEnumAttribute)}*");
        }

        [Test]
        public void ApplySnakeCaseNamingConventionsConvertsTableNamesToSnakeCase()
        {
            _modelBuilder.ApplySnakeCaseNamingConventions();

            var entity = _modelBuilder.Model.FindEntityType(typeof(TestModel));

            entity!.GetTableName().Should().Be("test_model");
        }

        [Test]
        public void ApplySnakeCaseNamingConventionsConvertsColumnNamesToSnakeCase()
        {
            _modelBuilder.ApplySnakeCaseNamingConventions();


            var entity = _modelBuilder.Model.FindEntityType(typeof(TestModel));
            var property = entity!.FindProperty(nameof(TestModel.PropertyName));

            property!.GetColumnName().Should().Be("property_name");
        }

        [Test]
        public void ApplySnakeCaseNamingConventionsPreservesInitialUnderscores()
        {
            _modelBuilder.Entity<TestModel>().Property(x => x._PropertyWithUnderscore);
            _modelBuilder.ApplySnakeCaseNamingConventions();

            var entity = _modelBuilder.Model.FindEntityType(typeof(TestModel));
            var property = entity!.FindProperty(nameof(TestModel._PropertyWithUnderscore));

            property!.GetColumnName().Should().Be("__property_with_underscore");
        }

        [Test]
        public void ApplySnakeCaseNamingConventionsAddsOwnedTypePrefix()
        {
            _modelBuilder.Entity<TestModel>();
            _modelBuilder.ApplySnakeCaseNamingConventions();

            var ownedEntity = _modelBuilder.Model.FindEntityType(typeof(TestOwnedModel));
            var property = ownedEntity!.FindProperty(nameof(TestOwnedModel.OwnedProperty));

            property!.GetColumnName().Should().Be("owned_model_owned_property");
        }

        [Test]
        public void ApplyHistoriesCreatesHistoryTableWithCorrectName()
        {
            _modelBuilder.ApplyHistories();

            var historyEntity = _modelBuilder.Model.FindEntityType(typeof(History<TestHistoryModel>));

            historyEntity!.GetTableName().Should().Be("TestHistoryModel_history");
            historyEntity.GetSchema().Should().Be("test");
        }

        [Test]
        public void ApplyEventSourcingConfiguresAggregateEventProperties()
        {
            _modelBuilder.ApplyEventSourcing();

            var entity = _modelBuilder.Model.FindEntityType(typeof(TestAggregate));
            var eventDataProperty = entity!.FindProperty(nameof(AggregateEvent.EventData));

            eventDataProperty!.GetColumnType().Should().Be("jsonb");
        }

        [Test]
        public void ApplyKeysConfiguresModelKeys()
        {
            _modelBuilder.ApplyKeys();

            var entity = _modelBuilder.Model.FindEntityType(typeof(TestModel));
            var property = entity!.FindProperty(nameof(TestModel.Id));

            property!.ClrType.Should().BeAssignableTo<NanoId>();
            property.IsPrimaryKey().Should().BeTrue();
        }

        [Test]
        public void ApplyAuditFieldsConfiguresModelCorrectly()
        {
            var dbOpts = new DbContextOptionsBuilder<EfCoreDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"));
            var model = new TestEfCoreDb(dbOpts.Options).Model;

            var entity = model.FindEntityType(typeof(TestAuditModel));

            entity!.FindProperty(nameof(IAuditableModel.CreatedUtc))!.IsNullable.Should().BeFalse();
            entity.FindProperty(nameof(IAuditableModel.CreatedBy))!.IsNullable.Should().BeFalse();
            entity.FindProperty(nameof(IAuditableModel.ModifiedBy))!.IsNullable.Should().BeTrue();
            entity.FindProperty(nameof(IAuditableModel.ModifiedUtc))!.IsNullable.Should().BeTrue();
            entity.FindProperty(nameof(IAuditableModel.CreatedBy))!.IsForeignKey().Should().BeTrue();
            entity.FindProperty(nameof(IAuditableModel.ModifiedBy))!.IsForeignKey().Should().BeTrue();
        }


        public class TestModelWithMissingDbEnumAttribute : Model
        {
            public MissingAttributeEnum EnumWithoutAttribute { get; set; }
        }

        public enum MissingAttributeEnum
        {
            Value1,
            Value2
        }
    }
}
