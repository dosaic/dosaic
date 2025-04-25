using Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests
{
    [DbNanoIdPrimaryKey(NanoIds.Lengths.NoLookAlikeDigitsAndLetters.L2)]
    public class TestModel : IModel
    {
        public required string Name { get; set; }
        public NanoId Id { get; set; }

        public static TestModel GetModel(string name = "Group 1") =>
            new() { Id = NanoId.NewId<TestModel>(), Name = name };
    }

    [DbNanoIdPrimaryKey(2, "prefix_")]
    public class PrefixedTestModel : IModel
    {
        public required NanoId Id { get; set; }
    }

    public class TestModelConfiguration : IEntityTypeConfiguration<TestModel>
    {
        public void Configure(EntityTypeBuilder<TestModel> builder)
        {
            builder.ToTable("testmodel", "test");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).HasMaxLength(64);
        }
    }
}
