using Dosaic.Extensions.NanoIds;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests
{
    [DbNanoIdPrimaryKey(NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters.L2)]
    public class TestUserModel : Model;

    public class TestOwnedModel
    {
        // ReSharper disable once EntityFramework.ModelValidation.UnlimitedStringLength
        public string OwnedProperty { get; set; }
    }

    [DbNanoIdPrimaryKey(NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters.L2)]
    public class TestModel : IModel
    {
        public required string Name { get; set; }
        public NanoId Id { get; set; }

        public TestOwnedModel OwnedModel { get; set; }

        public string _PropertyWithUnderscore { get; set; }
        public string PropertyName { get; set; }

        public TestEnumType EnumProperty { get; set; }
        public TestEnumType? NullableEnumProperty { get; set; }

        public static TestModel GetModel(string name = "Group 1") =>
            new() { Id = NanoId.NewId<TestModel>(), Name = name };
    }

    [DbEnum("test_enum", "custom")]
    public enum TestEnumType
    {
        Value1,
        Value2
    }

    [DbNanoIdPrimaryKey(2, "prefix_")]
    public class PrefixedTestModel : IModel
    {
        public required NanoId Id { get; set; }
    }

    [DbNanoIdPrimaryKey(NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters.L2)]
    public class TestAuditModel : AuditableModel
    {
        public required string Name { get; set; }
        public virtual ICollection<SubTestModel> Subs { get; set; } = null!;
    }

    public class SubTestOwnedInfo
    {
        public string InfoKey { get; set; }
        public string InfoValue { get; set; }
    }

    [DbNanoIdPrimaryKey(NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters.L2)]
    public class SubTestModel : Model
    {
        public required string DeepName { get; set; }
        public SubTestOwnedInfo OwnedInfo { get; set; }
    }

    [DbNanoIdPrimaryKey(NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters.L2)]
    public class TestHistoryModel : Model, IHistory
    {
        public string HistoryProperty { get; set; }

        [ExcludeFromHistory] public string Ignored { get; set; }

        public virtual ICollection<TestHistoryChildModel> Children { get; set; }

        public static TestHistoryModel GetModel(string historyProperty = "Group 1") =>
            new() { Id = NanoId.NewId<TestHistoryModel>(), HistoryProperty = historyProperty };
    }

    [DbNanoIdPrimaryKey(NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters.L2)]
    [HistoryParent(typeof(TestHistoryModel), nameof(ParentId), Collection = nameof(TestHistoryModel.Children))]
    public class TestHistoryChildModel : Model
    {
        public NanoId ParentId { get; set; }
        public string ChildName { get; set; }
        public decimal Price { get; set; }
        public virtual ICollection<TestHistoryGrandchildModel> Parts { get; set; }
    }

    [DbNanoIdPrimaryKey(NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters.L2)]
    [HistoryParent(typeof(TestHistoryChildModel), nameof(ChildId), Collection = nameof(TestHistoryChildModel.Parts))]
    public class TestHistoryGrandchildModel : Model
    {
        public NanoId ChildId { get; set; }
        public string PartName { get; set; }
        public int Qty { get; set; }
    }

    public class TestModelConfiguration : IEntityTypeConfiguration<TestModel>
    {
        public void Configure(EntityTypeBuilder<TestModel> builder)
        {
            builder.ToTable(nameof(TestModel), "test");
            builder.Property(x => x.Name).HasMaxLength(64);
            builder.Property(x => x._PropertyWithUnderscore).HasMaxLength(64);
            builder.Property(x => x.PropertyName).HasMaxLength(64);
            builder.Property(x => x.NullableEnumProperty);
            builder.Property(x => x.EnumProperty);

            builder.OwnsOne(x => x.OwnedModel).Property(x => x.OwnedProperty);
        }
    }

    public class TestAuditModelConfiguration : IEntityTypeConfiguration<TestAuditModel>
    {
        public void Configure(EntityTypeBuilder<TestAuditModel> builder)
        {
            builder.ToTable(nameof(TestAuditModel), "test");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name);
        }
    }

    public class SubTestModelModelConfiguration : IEntityTypeConfiguration<SubTestModel>
    {
        public void Configure(EntityTypeBuilder<SubTestModel> builder)
        {
            builder.ToTable(nameof(SubTestModel), "test");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.DeepName);
            builder.OwnsOne(x => x.OwnedInfo, owned =>
            {
                owned.Property(x => x.InfoKey);
                owned.Property(x => x.InfoValue);
            });
        }
    }

    public class TestHistoryModelConfiguration : IEntityTypeConfiguration<TestHistoryModel>
    {
        public void Configure(EntityTypeBuilder<TestHistoryModel> builder)
        {
            builder.ToTable(nameof(TestHistoryModel), "test");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.HistoryProperty).HasMaxLength(64);
            builder.Property(x => x.Ignored).HasMaxLength(64);
            builder.HasMany(x => x.Children).WithOne().HasForeignKey(x => x.ParentId);
        }
    }

    public class TestHistoryChildModelConfiguration : IEntityTypeConfiguration<TestHistoryChildModel>
    {
        public void Configure(EntityTypeBuilder<TestHistoryChildModel> builder)
        {
            builder.ToTable(nameof(TestHistoryChildModel), "test");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.ChildName).HasMaxLength(64);
            builder.HasMany(x => x.Parts).WithOne().HasForeignKey(x => x.ChildId);
        }
    }

    public class TestHistoryGrandchildModelConfiguration : IEntityTypeConfiguration<TestHistoryGrandchildModel>
    {
        public void Configure(EntityTypeBuilder<TestHistoryGrandchildModel> builder)
        {
            builder.ToTable(nameof(TestHistoryGrandchildModel), "test");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.PartName).HasMaxLength(64);
        }
    }
}
