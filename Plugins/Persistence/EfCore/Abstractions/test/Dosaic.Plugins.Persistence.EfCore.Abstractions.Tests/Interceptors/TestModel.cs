using Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Interceptors
{
    [DbNanoIdPrimaryKey(NanoIds.Lengths.NoLookAlikeDigitsAndLetters.L2)]
    public class TestModel : IModel
    {
        public required string Name { get; set; }
        public NanoId Id { get; set; }

        public static TestModel GetModel() =>
            new() { Id = NanoId.NewId<TestModel>(), Name = "Group 1", };
    }

    public class GroupModelConfiguration : IEntityTypeConfiguration<TestModel>
    {
        public void Configure(EntityTypeBuilder<TestModel> builder)
        {
            builder.ToTable("groups", "test");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).HasMaxLength(64);
        }
    }
}
