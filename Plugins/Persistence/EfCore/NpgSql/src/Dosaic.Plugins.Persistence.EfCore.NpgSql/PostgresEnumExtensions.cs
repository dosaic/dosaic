using Dosaic.Hosting.Abstractions.Extensions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Npgsql.NameTranslation;

namespace Dosaic.Plugins.Persistence.EfCore.NpgSql
{
    internal static class PostgresEnumExtensions
    {
        private static readonly NpgsqlSnakeCaseNameTranslator _translator = new();

        private static HashSet<Type> GetEnumTypes(Type modelType) =>
            modelType.GetAssemblyTypes(x => x.IsEnum && x.HasAttribute<DbEnumAttribute>()).ToHashSet();

        public static void MapDbEnums<TDbContext>(this ModelBuilder builder)
        {
            var register = typeof(NpgsqlModelBuilderExtensions).GetMethods()
                .Single(x =>
                {
                    if (x is not
                        {
                            Name: nameof(NpgsqlModelBuilderExtensions.HasPostgresEnum), ContainsGenericParameters: false
                        })
                        return false;
                    var parameters = x.GetParameters();
                    return parameters.Length == 4
                           && parameters[0].ParameterType == typeof(ModelBuilder)
                           && parameters[1].ParameterType == typeof(string)
                           && parameters[2].ParameterType == typeof(string)
                           && parameters[3].ParameterType == typeof(string[]);
                });
            foreach (var e in GetEnumTypes(typeof(TDbContext)))
            {
                var dbEnum = e.GetAttribute<DbEnumAttribute>();
                var labels = Enum.GetNames(e).Select(_translator.TranslateMemberName).Order().ToArray();
                register.Invoke(null, [builder, dbEnum.Schema, dbEnum.Name, labels]);
            }
        }

        public static NpgsqlDataSourceBuilder MapDbEnums<TDbContext>(this NpgsqlDataSourceBuilder dataSourceBuilder)
        {
            dataSourceBuilder.EnableUnmappedTypes();
            foreach (var e in GetEnumTypes(typeof(TDbContext)))
            {
                var dbName = e.GetAttribute<DbEnumAttribute>()!.DbName;
                dataSourceBuilder.MapEnum(e, dbName);
            }

            return dataSourceBuilder;
        }

        public static NpgsqlDbContextOptionsBuilder UseDbEnums<TDbContext>(this NpgsqlDbContextOptionsBuilder builder)
        {
            foreach (var e in GetEnumTypes(typeof(TDbContext)))
            {
                var dbEnum = e.GetAttribute<DbEnumAttribute>();
                builder.MapEnum(e, dbEnum.Name, dbEnum.Schema, _translator);
            }

            return builder;
        }
    }
}
