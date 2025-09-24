using System.Collections;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using AwesomeAssertions.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dosaic.Testing.NUnit.Assertions
{
    public static class EntityTypeAssertionsExtensions
    {
        public static EntityTypeAssertions Should(this IEntityType entityType) => new(entityType);

        public static DbContextAssertions Should(this DbContext dbContext) => new(dbContext);
    }

    public class DbContextAssertions(DbContext dbContext)
        : ReferenceTypeAssertions<DbContext, DbContextAssertions>(dbContext, AssertionChain.GetOrCreate())
    {
        protected override string Identifier => nameof(DbContextAssertions);

        public AndConstraint<DbContextAssertions> MatchMigrations<T>(string because = "", params object[] becauseArgs)
        {
            var modelDiffer = Subject.GetService<IMigrationsModelDiffer>();
            var migrationsAssembly = Subject.GetService<IMigrationsAssembly>();
            var modelInitializer = Subject.GetService<IModelRuntimeInitializer>();
            var snapshotModel = migrationsAssembly.ModelSnapshot?.Model;
            if (snapshotModel is IMutableModel mutableModel)
            {
                snapshotModel = mutableModel.FinalizeModel();
            }
            if (snapshotModel is not null)
            {
                snapshotModel = modelInitializer.Initialize(snapshotModel);
            }

            var designTimeModel = Subject.GetService<IDesignTimeModel>();

            var modelDifferences = modelDiffer.GetDifferences(snapshotModel?.GetRelationalModel(),
                designTimeModel.Model.GetRelationalModel());
            CurrentAssertionChain.BecauseOf(because, becauseArgs)
                .ForCondition(modelDifferences.Count == 0)
                .FailWith("DatabaseModel should not differ with the Migrations (Run 'add migration'..)");
            return new AndConstraint<DbContextAssertions>(this);
        }

        public AndConstraint<DbContextAssertions> MatchCompiledModel(IModel compiledModel, string because = "",
            params object[] becauseArgs)
        {
            CurrentAssertionChain.BecauseOf(because, becauseArgs)
                .ForCondition(Subject.Model.ToDebugString() == compiledModel.ToDebugString())
                .FailWith("Compiled model is not in sync with the DbContext model.");
            return new AndConstraint<DbContextAssertions>(this);
        }
    }

    public class EntityTypeAssertions(IEntityType entityType)
        : ReferenceTypeAssertions<IEntityType, EntityTypeAssertions>(entityType, AssertionChain.GetOrCreate())
    {
        protected override string Identifier => nameof(EntityTypeAssertions);

        public AndConstraint<EntityTypeAssertions> BeOnTable(string schema, string name, string because = "",
            params object[] becauseArgs)
        {
            CurrentAssertionChain.BecauseOf(because, becauseArgs)
                .ForCondition(!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(schema))
                .FailWith("You need to specify a schema and name!")
                .Then
                .Given(() => new { Table = Subject.GetTableName(), Schema = Subject.GetSchema() })
                .ForCondition(p => p.Table == name && p.Schema == schema)
                .FailWith("Table name or schema does not match the expected ones.");

            return new AndConstraint<EntityTypeAssertions>(this);
        }

        public AndConstraint<EntityTypeAssertions> HavePrimaryKey<TPk>(string name)
        {
            CurrentAssertionChain.Given(Subject.FindPrimaryKey)
                .ForCondition(pk =>
                    pk is { Properties.Count: 1 } &&
                    pk.Properties.SingleOrDefault(prop => prop.Name == name) != null)
                .FailWith("Primary key is not present or have not the id field as single key.");

            CurrentAssertionChain.Given(() => Subject.FindProperty(name))
                .ForCondition(p => p != null && p.ClrType == typeof(TPk))
                .FailWith($"Id field is not of type '{typeof(TPk).Name}'");
            return new AndConstraint<EntityTypeAssertions>(this);
        }

        public AndConstraint<EntityTypeAssertions> HaveProperty<T>(string name, bool isNullable = false,
            int? maxLength = null, object defaultValue = null, string defaultValueSql = null, string because = "",
            params object[] becauseArgs)
        {
            CurrentAssertionChain.BecauseOf(because, becauseArgs)
                .ForCondition(!string.IsNullOrEmpty(name))
                .FailWith("You need to specify a property name!")
                .Then
                .Given(() => Subject.GetProperty(name!))
                .ForCondition(p => p != null
                                   && p.ClrType == typeof(T)
                                   && p.GetMaxLength() == maxLength
                                   && p.IsNullable == isNullable
                                   && (defaultValue is null ||
                                       p.GetDefaultValue()?.ToString() == defaultValue.ToString())
                                   && (defaultValueSql is null || p.GetDefaultValueSql() == defaultValueSql))
                .FailWith($"Property could not be found with name '{name}' or it has the wrong type.");

            return new AndConstraint<EntityTypeAssertions>(this);
        }

        public AndConstraint<EntityTypeAssertions> HaveProperty(string name, string databaseType,
            bool isNullable = false,
            int? maxLength = null, object defaultValue = null, string defaultValueSql = null, string because = "",
            params object[] becauseArgs)
        {
            CurrentAssertionChain.BecauseOf(because, becauseArgs)
                .ForCondition(!string.IsNullOrEmpty(name))
                .FailWith("You need to specify a property name!")
                .Then
                .Given(() => Subject.GetProperty(name!))
                .ForCondition(p => p != null
                                   && p.GetColumnType() == databaseType
                                   && p.GetMaxLength() == maxLength
                                   && p.IsNullable == isNullable
                                   && (defaultValue is null ||
                                       p.GetDefaultValue()?.ToString() == defaultValue.ToString())
                                   && (defaultValueSql is null || p.GetDefaultValueSql() == defaultValueSql))
                .FailWith($"Property could not be found with name '{name}' or it has the wrong type.");

            return new AndConstraint<EntityTypeAssertions>(this);
        }

        public AndConstraint<EntityTypeAssertions> HaveIndex(string property, bool isUnique = false,
            string because = "",
            params object[] becauseArgs)
        {
            Subject.Should().HaveIndex([property], isUnique, because, becauseArgs);
            return new AndConstraint<EntityTypeAssertions>(this);
        }

        public AndConstraint<EntityTypeAssertions> HasCheckConstraint(string name, string sql, string because = "",
            params object[] becauseArgs)
        {
            CurrentAssertionChain.BecauseOf(because, becauseArgs)
                .ForCondition(!string.IsNullOrEmpty(name))
                .FailWith("You need to specify a constraint name!")
                .Then
                .ForCondition(!string.IsNullOrEmpty(sql))
                .FailWith("You need to specify the SQL expression for the check constraint!")
                .Then
                .Given(() => Subject.GetCheckConstraints().FirstOrDefault(c => c.Name == name))
                .ForCondition(c => c != null && c.Sql == sql)
                .FailWith($"Expected check constraint '{name}' with SQL '{sql}' to be present, but it was not found.");

            return new AndConstraint<EntityTypeAssertions>(this);
        }

        public AndConstraint<EntityTypeAssertions> HaveIndex(string[] properties, bool isUnique = false,
            string because = "", params object[] becauseArgs)
        {
            CurrentAssertionChain.BecauseOf(because, becauseArgs)
                .ForCondition(properties.Length > 0 && properties.All(p => !string.IsNullOrEmpty(p)))
                .FailWith("You need to specify a property name!")
                .Then
                .Given(() => Subject.GetIndexes().FirstOrDefault(p =>
                    p.Properties.Select(x => x.Name).OrderBy(x => x).ToArray()
                        .SequenceEqual(properties.OrderBy(x => x))))
                .ForCondition(i => i != null && i.IsUnique == isUnique)
                .FailWith(
                    $"Index with properties '{string.Join(",", properties)}' is not present or does not match the specification.");

            return new AndConstraint<EntityTypeAssertions>(this);
        }

        public AndConstraint<EntityTypeAssertions> HaveForeignKey(string property, Type target, string because = "",
            params object[] becauseArgs)
        {
            CurrentAssertionChain.BecauseOf(because, becauseArgs)
                .ForCondition(!string.IsNullOrEmpty(property))
                .FailWith("You need to specify a property name and a target type which implements IEntity")
                .Then
                .Given(() => Subject.GetForeignKeys().FirstOrDefault(p =>
                    p.Properties.Count == 1 && p.Properties.Select(x => x.Name).Contains(property)))
                .ForCondition(fk => fk != null && fk.PrincipalEntityType.ClrType == target)
                .FailWith("Foreign key could not be found or locates the wrong target model");
            return new AndConstraint<EntityTypeAssertions>(this);
        }

        public AndConstraint<EntityTypeAssertions> HaveData<T>(T[] data, string because = "",
            params object[] becauseArgs)
        {
            var subjectType = Subject.ClrType;
            if (typeof(T) != subjectType) throw new ArgumentException($"Data is not of type '{subjectType.Name}'");
            var convertedData = new List<IDictionary<string, object>>();
            var subjectProperties = Subject.ClrType.GetProperties().Select(x => new { x.Name, x.GetMethod }).ToList();
            foreach (var entry in data)
            {
                var dic = new Dictionary<string, object>();
                foreach (var prop in subjectProperties)
                {
                    var value = prop.GetMethod?.Invoke(entry, []);
#pragma warning disable EF1001
                    if (value == null || value.GetType().IsDefaultValue(value))
#pragma warning restore EF1001
                        continue;
                    dic.Add(prop.Name, value);
                }

                convertedData.Add(dic);
            }

            CurrentAssertionChain.BecauseOf(because, becauseArgs)
                .ForCondition(convertedData.Count != 0)
                .FailWith(
                    "You need to specify data with at least one row and all rows needs to be the same type (model type)")
                .Then
                .Given(() =>
                {
                    var x = Subject.GetSeedData();
                    var convertedSeedData = new List<IDictionary<string, object>>();
                    foreach (var entry in x)
                    {
                        var dic = new Dictionary<string, object>();
                        foreach (var (key, value) in entry)
                        {

#pragma warning disable EF1001
                            if (value == null || value.GetType().IsDefaultValue(value))
#pragma warning restore EF1001
                                continue;
                            dic.Add(key, value);
                        }

                        convertedSeedData.Add(dic);
                    }
                    return convertedSeedData;
                })
                .ForCondition(d => d != null
                                   && CompareListOfDictionaries(d.ToList(), convertedData)
                )
                .FailWith("The specified data does not match.");
            return new AndConstraint<EntityTypeAssertions>(this);
        }

        private static bool CompareListOfDictionaries(IList<IDictionary<string, object>> dic1,
            IList<IDictionary<string, object>> dic2)
        {
            if (dic1.Count != dic2.Count) return false;
            return dic1.Select(entry => dic2.Any(d => CompareDictionaries(entry, d))).All(exists => exists)
                   && dic2.Select(entry => dic1.Any(d => CompareDictionaries(entry, d))).All(exists => exists);
        }

        private static bool CompareDictionaries(IDictionary<string, object> dic1, IDictionary<string, object> dic2)
        {
            return dic1.All(x => dic2.ContainsKey(x.Key) && CompareObjects(dic2[x.Key]!, x.Value))
                   && dic2.All(x => dic1.ContainsKey(x.Key) && CompareObjects(dic1[x.Key]!, x.Value));
        }

        private static bool CompareObjects(object obj1, object obj2)
        {
            if (obj1 is null && obj2 is null) return true;
            if (obj1 is null || obj2 is null) return false;
            if (obj1.GetType() != obj2.GetType()) return false;
            if (obj1.GetType().GetInterfaces().All(i => i != typeof(IEnumerable)))
                return obj1.Equals(obj2);
            var obj1List = ((IEnumerable)obj1).OfType<object>().ToList();
            var obj2List = ((IEnumerable)obj2).OfType<object>().ToList();
            if (obj1List.Count != obj2List.Count) return false;
            return obj1List.All(x => obj2List.Any(i => CompareObjects(i, x)))
                   && obj2List.All(x => obj1List.Any(i => CompareObjects(i, x)));
        }
    }
}
