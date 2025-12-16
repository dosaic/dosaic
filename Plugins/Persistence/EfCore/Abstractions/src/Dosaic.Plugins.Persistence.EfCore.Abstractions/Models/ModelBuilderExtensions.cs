using Dosaic.Extensions.NanoIds;
using Dosaic.Hosting.Abstractions.Extensions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Eventsourcing;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers;
using Microsoft.EntityFrameworkCore;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Models
{
    public static partial class ModelBuilderExtensions
    {
        public static void ApplyKeys(this ModelBuilder builder)
        {
            // https://zelark.github.io/nano-id-cc/
            // Variant                                                  Length          IDs needed, in order to have a 1% probability of at least one collision.
            // NoLookAlikeSafeDigits+NoLookAlikeSafeLetters             (36 chars)       6K (lenght 6)     8M (lenght 10)
            // -> 6789BCDFGHJKLMNPQRTWbcdfghjkmnpqrtwz
            // NoLookAlikeDigits+NoLookAlikeLetters                     (49 chars)      16K (lenght 6)    40M (lenght 10)
            // -> 346789ABCDEFGHJKLMNPQRTUVWXYabcdefghijkmnpqrtwxyz
            // SqidAlphabet                                             (62 chars)      33K (lenght 6)   129M (lenght 10)
            // -> kKsW7PVdXUYnHgQ6rujl0GepfNzB2qZ9bC83IyDmOAtJ4hcSvM1Roaw5LxEiTF
            foreach (var entity in builder.Model.GetEntityTypes().Where(x => !x.IsOwned()))
            {
                var dbNanoId = entity.ClrType.GetAttribute<DbNanoIdPrimaryKeyAttribute>();
                builder.Entity(entity.ClrType).HasKey(nameof(Model.Id));
                builder.Entity(entity.ClrType).Property(nameof(Model.Id))
                    .HasMaxLength(dbNanoId.LengthWithPrefix);

                foreach (var foreignKey in entity.GetForeignKeys())
                {
                    var fkProp = foreignKey.Properties.Single().Name;
                    var attr = foreignKey.PrincipalEntityType.ClrType.GetAttribute<NanoIdAttribute>();
                    builder.Entity(entity.ClrType).Property(fkProp).HasMaxLength(attr.LengthWithPrefix);
                }
            }

            foreach (var entity in builder.Model.GetEntityTypes().Where(x => x.IsOwned()))
            {
                foreach (var foreignKey in entity.GetForeignKeys())
                {
                    if (foreignKey.IsOwnership)
                        foreignKey.Properties[0].SetColumnName(nameof(Model.Id));
                    else
                    {
                        var fkProp = foreignKey.Properties.Single().Name;
                        var attr = foreignKey.PrincipalEntityType.ClrType.GetAttribute<NanoIdAttribute>();
                        entity.GetProperty(fkProp).SetMaxLength(attr.LengthWithPrefix);
                    }
                }
            }
        }

        public static void ApplyEnumFields(this ModelBuilder builder)
        {
            foreach (var entity in builder.Model.GetEntityTypes())
            {
                foreach (var column in entity.GetProperties())
                {
                    var clrType =
                        column.ClrType.IsGenericType && column.ClrType.GetGenericTypeDefinition() == typeof(Nullable<>)
                            ? column.ClrType.GetGenericArguments()[0]
                            : column.ClrType;

                    if (!clrType.IsEnum)
                        continue;

                    var dbEnum = clrType.GetAttribute<DbEnumAttribute>();
                    if (dbEnum is null)
                        throw new ArgumentException(
                            $"Type '{clrType.FullName}' does not have the {nameof(DbEnumAttribute)}");
                    column.SetColumnType(dbEnum.DbName);
                }
            }
        }

        public static void ApplyHistories(this ModelBuilder builder, Type modifiedByForeignKeyModel)
        {
            var historicModels = builder.Model.GetEntityTypes()
                .Where(x => x.ClrType.GetInterfaces().Contains(typeof(IHistory)))
                .ToArray();
            var historyType = typeof(History<>);
            foreach (var historyModel in historicModels)
            {
                var entityTypeBuilder = builder.Entity(historyType.MakeGenericType(historyModel.ClrType));
                entityTypeBuilder.ToTable($"{historyModel.GetTableName()}_history", historyModel.GetSchema());
                entityTypeBuilder.Property<NanoId>(nameof(History.ForeignId)).IsRequired();
                entityTypeBuilder.Property<ChangeState>(nameof(History.State)).IsRequired();
                entityTypeBuilder.Property<string>(nameof(History.ChangeSet))
                    .HasColumnType("jsonb").IsRequired();
                entityTypeBuilder.Property<DateTime>(nameof(History.ModifiedUtc)).IsRequired();
                entityTypeBuilder.Property<NanoId>(nameof(History.ModifiedBy));

                entityTypeBuilder.HasOne("Model")
                    .WithMany()
                    .HasForeignKey(nameof(History.ForeignId));

                entityTypeBuilder.HasOne(modifiedByForeignKeyModel)
                    .WithMany()
                    .HasForeignKey(nameof(History.ModifiedBy));
            }
        }

        public static void ApplyEventSourcing(this ModelBuilder builder, Type modifiedByForeignKeyModel)
        {
            var models = builder.Model.GetEntityTypes()
                .Where(x => x.ClrType.Implements(typeof(AggregateEvent<>)) &&
                            x.ClrType is { IsAbstract: false, IsClass: true })
                .ToArray();

            foreach (var model in models)
            {
                var entityTypeBuilder = builder.Entity(model.ClrType);
                entityTypeBuilder.Property<string>(nameof(AggregateEvent.EventData))
                    .HasColumnType("jsonb").IsRequired();
                entityTypeBuilder.Property<bool>(nameof(AggregateEvent.IsDeleted)).IsRequired();
                entityTypeBuilder.Property<DateTime>(nameof(AggregateEvent.ValidFrom)).IsRequired();
                entityTypeBuilder.Property<DateTime>(nameof(AggregateEvent.ModifiedUtc)).IsRequired();
                entityTypeBuilder.Property<NanoId>(nameof(AggregateEvent.ModifiedBy));

                entityTypeBuilder.HasOne(modifiedByForeignKeyModel)
                    .WithMany()
                    .HasForeignKey(nameof(AggregateEvent.ModifiedBy));
            }
        }

        public static void ApplyAuditFields(this ModelBuilder builder, Type createdByForeignKeyModel,
            Type modifiedByForeignKeyModel, string defaultValueCreatedBy = "System")
        {
            foreach (var entity in builder.Model.GetEntityTypes()
                         .ToList() // prevent modifying collection while iterating
                         .Where(x => x.ClrType.IsAssignableTo(typeof(IAuditableModel))))
            {
                builder.Entity(entity.ClrType).Property(nameof(IAuditableModel.CreatedBy))
                    .IsRequired()
                    .HasDefaultValueSql(defaultValueCreatedBy);

                builder.Entity(entity.ClrType).Property(nameof(IAuditableModel.CreatedUtc))
                    .IsRequired()
                    .HasDefaultValueSql("timezone('utc', now())");

                builder.Entity(entity.ClrType).HasOne(createdByForeignKeyModel)
                    .WithMany()
                    .HasForeignKey(nameof(IAuditableModel.CreatedBy));

                builder.Entity(entity.ClrType).HasOne(modifiedByForeignKeyModel)
                    .WithMany()
                    .HasForeignKey(nameof(IAuditableModel.ModifiedBy));
            }
        }

        public static void ApplySnakeCaseNamingConventions(this ModelBuilder builder)
        {
            foreach (var entity in builder.Model.GetEntityTypes())
            {
                entity.SetTableName(entity.GetTableName()!.ToSnakeCase());
                var prefix = !entity.IsOwned()
                    ? ""
                    : entity.GetForeignKeys().Single(x => x.IsOwnership).PrincipalToDependent!.Name.ToSnakeCase() + "_";

                foreach (var prop in entity.GetProperties())
                {
                    var columnName = prop.GetColumnName();
                    if (columnName != nameof(Model.Id))
                        columnName = prefix + columnName;
                    prop.SetColumnName(columnName.ToSnakeCase());
                }

                foreach (var key in entity.GetKeys())
                    key.SetName(key.GetName()!.ToSnakeCase());

                foreach (var key in entity.GetForeignKeys())
                    key.SetConstraintName(key.GetConstraintName()!.ToSnakeCase());

                foreach (var index in entity.GetIndexes())
                    index.SetDatabaseName(index.GetDatabaseName()!.ToSnakeCase());
            }
        }
    }
}
