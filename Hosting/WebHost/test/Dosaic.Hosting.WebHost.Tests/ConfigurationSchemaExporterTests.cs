using System.Text.Json.Nodes;
using AwesomeAssertions;
using Dosaic.Hosting.Abstractions.Attributes;
using Dosaic.Hosting.Abstractions.Configuration;
using Dosaic.Hosting.WebHost.Services;
using NUnit.Framework;

namespace Dosaic.Hosting.WebHost.Tests
{
    public class ConfigurationSchemaExporterTests
    {
        public enum SampleMode
        {
            Fast,
            Safe
        }

        [Flags]
        public enum SampleFlags
        {
            None = 0,
            Read = 1,
            Write = 2
        }

        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
        public sealed class ValueObjectAttribute : Attribute;

        [ValueObject]
        public readonly struct SampleId(int value)
        {
            public int Value { get; } = value;
        }

        public class SpecialTypes
        {
            public DateOnly Day { get; set; }
            public TimeOnly Time { get; set; }
            public SampleFlags Flags { get; set; }
            public Dictionary<SampleMode, int> ByMode { get; set; }
            public SampleId Id { get; set; }
            public SampleId? OptionalId { get; set; }
            public Uri Endpoint { get; set; }
        }

        public class SampleNested
        {
            public int Retries { get; set; } = 3;
            public SampleNested Parent { get; set; }
        }

        public class SampleConfiguration
        {
            [System.ComponentModel.Description("Where the sample lives.")]
            public string Host { get; set; }

            public int Port { get; set; } = 5432;
            public long? Limit { get; set; }
            public bool Enabled { get; set; } = true;
            public double Ratio { get; set; } = 0.5;
            public SampleMode Mode { get; set; } = SampleMode.Safe;
            public SampleMode? OptionalMode { get; set; }
            public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
            public Uri Endpoint { get; set; }
            public Guid Id { get; set; }
            public string[] Queues { get; set; } = ["default", "bulk"];
            public List<SampleNested> Items { get; set; } = [];
            public Dictionary<string, int> Workers { get; set; } = [];
            public Dictionary<string, SampleNested> Named { get; set; }
            public SampleNested Nested { get; set; } = new() { Retries = 7 };
            public string ReadOnly => "computed";
            public IList<string> ReadOnlyList { get; } = [];
            public Action Callback { get; set; }
            public IDisposable Service { get; set; }
            public object Anything { get; set; }
            public Type Kind { get; set; }
            internal string Hidden { get; set; }
        }

        public class NestedSectionConfiguration
        {
            public string Name { get; set; }
        }

        public class ParentSectionConfiguration
        {
            public int PoolSize { get; set; } = 10;
        }

        public class SecondSampleConfiguration
        {
            public string Extra { get; set; }
            public string Host { get; set; } = "ignored, the first class already describes it";
        }

        public class WithoutDefaultConstructor(string value)
        {
            public string Value { get; set; } = value;
        }

        [Configuration("abstract")]
        public abstract class AbstractConfiguration
        {
            public string Value { get; set; }
        }

        public class NotAConfiguration
        {
            public string Value { get; set; }
        }

        // the samples carry no [Configuration] attribute: the host tests of this assembly would try to bind them
        private static readonly (string Section, Type Type) _sample = ("sample", typeof(SampleConfiguration));

        private static JsonObject Export(params (string Section, Type Type)[] configurations) =>
            ConfigurationSchemaExporter.Export(configurations);

        private static JsonObject Section(JsonObject schema, string name) =>
            schema["properties"]![name]!.AsObject();

        private static JsonObject Property(JsonObject section, string name) =>
            section["properties"]![name]!.AsObject();

        private static string[] Types(JsonNode schema) => schema["type"] is JsonArray types
            ? types.Select(x => x!.GetValue<string>()).ToArray()
            : [schema["type"]!.GetValue<string>()];

        [Test]
        public void DescribesTheDocumentAsADraft7ObjectSchemaThatForbidsNothing()
        {
            var schema = Export(_sample);

            schema["$schema"]!.GetValue<string>().Should().Be("http://json-schema.org/draft-07/schema#");
            schema["type"]!.GetValue<string>().Should().Be("object");
            schema.ToJsonString().Should().NotContain("\"additionalProperties\":false").And.NotContain("required");
        }

        [Test]
        public void OnlyConcreteConfigurationClassesBecomeSections()
        {
            var schema = ConfigurationSchemaExporter.Export([
                typeof(HostConfiguration), typeof(NotAConfiguration), typeof(AbstractConfiguration),
                typeof(HostConfiguration), typeof(IDisposable)
            ]);

            var sections = schema["properties"]!.AsObject().Select(x => x.Key).ToList();
            sections.Should().Contain("host").And.NotContain("abstract");
            sections.Should().OnlyHaveUniqueItems();
            sections.Should().HaveCount(ConfigurationSchemaExporter.Export(Array.Empty<Type>())["properties"]!
                .AsObject().Count + 1);
        }

        [Test]
        public void ScalarsAreTypedAndCarryTheirDefaults()
        {
            var sample = Section(Export(_sample), "sample");

            Types(Property(sample, "host")).Should().Equal("string", "null");
            Property(sample, "host")["default"].Should().BeNull();
            Types(Property(sample, "port")).Should().Equal("integer");
            Property(sample, "port")["default"]!.GetValue<long>().Should().Be(5432);
            Types(Property(sample, "limit")).Should().Equal("integer", "null");
            Types(Property(sample, "enabled")).Should().Equal("boolean");
            Property(sample, "enabled")["default"]!.GetValue<bool>().Should().BeTrue();
            Types(Property(sample, "ratio")).Should().Equal("number");
            Property(sample, "ratio")["default"]!.GetValue<decimal>().Should().Be(0.5m);
            Types(Property(sample, "timeout")).Should().Equal("string");
            Property(sample, "timeout")["default"]!.GetValue<string>().Should().Be("00:00:30");
            Property(sample, "endpoint")["format"]!.GetValue<string>().Should().Be("uri");
            Property(sample, "id")["format"]!.GetValue<string>().Should().Be("uuid");
        }

        [Test]
        public void EnumsListTheirNamesAndNullableEnumsAllowNull()
        {
            var sample = Section(Export(_sample), "sample");

            var mode = Property(sample, "mode");
            mode["enum"]!.AsArray().Select(x => x!.GetValue<string>()).Should().Equal("Fast", "Safe");
            mode["default"]!.GetValue<string>().Should().Be("Safe");
            var optional = Property(sample, "optionalMode");
            Types(optional).Should().Equal("string", "null");
            optional["enum"]!.AsArray().Should().HaveCount(3).And.Contain(x => x == null);
        }

        [Test]
        public void CollectionsAndMapsDescribeTheirElements()
        {
            var sample = Section(Export(_sample), "sample");

            var queues = Property(sample, "queues");
            Types(queues).Should().Equal("array");
            Types(queues["items"]).Should().Equal("string", "null");
            queues["default"]!.AsArray().Select(x => x!.GetValue<string>()).Should().Equal("default", "bulk");
            var items = Property(sample, "items");
            Property(items["items"]!.AsObject(), "retries")["default"]!.GetValue<long>().Should().Be(3);
            items["default"].Should().BeNull();
            var workers = Property(sample, "workers");
            Types(workers).Should().Equal("object");
            Types(workers["additionalProperties"]).Should().Equal("integer");
            Property(Property(sample, "named")["additionalProperties"]!.AsObject(), "retries").Should().NotBeNull();
        }

        [Test]
        public void SpecialTypesAreDescribedTheWayTheyAreWritten()
        {
            var special = Section(Export(("special", typeof(SpecialTypes))), "special");

            Property(special, "day")["format"]!.GetValue<string>().Should().Be("date");
            Property(special, "time")["format"]!.GetValue<string>().Should().Be("time");
            Types(Property(special, "flags")).Should().Equal("string");
            Property(special, "flags")["enum"].Should().BeNull("a combination like 'Read, Write' has to stay valid");
            Types(Property(special, "byMode")).Should().Equal("object");
            Types(Property(special, "byMode")["additionalProperties"]).Should().Equal("integer");
            Types(Property(special, "id")).Should().Equal("integer");
            Property(special, "id")["properties"].Should().BeNull("a value object is written as its bare value");
            Types(Property(special, "optionalId")).Should().Equal("integer");
            Types(Property(special, "endpoint")).Should().Equal("string", "null");
        }

        [Test]
        public void NestedObjectsTakeTheirDefaultsFromTheParentInstanceAndStopAtCycles()
        {
            var nested = Property(Section(Export(_sample), "sample"), "nested");

            Property(nested, "retries")["default"]!.GetValue<long>().Should().Be(7);
            var parent = Property(nested, "parent");
            Types(parent).Should().Equal("object");
            parent["properties"].Should().BeNull("the type is already being described further up");
        }

        [Test]
        public void PropertiesAConfigurationFileCannotSetAreLeftOut()
        {
            var names = Section(Export(_sample), "sample")["properties"]!.AsObject()
                .Select(x => x.Key).ToList();

            names.Should().NotContain(["readOnly", "readOnlyList", "callback", "service", "anything", "kind", "hidden"]);
            names.Should().Contain(["host", "port", "nested"]);
        }

        [Test]
        public void DescriptionsComeFromTheDescriptionAttribute()
        {
            var sample = Section(Export(_sample), "sample");

            Property(sample, "host")["description"]!.GetValue<string>().Should().Be("Where the sample lives.");
            Property(sample, "port")["description"].Should().BeNull();
        }

        [Test]
        public void NestedSectionNamesBecomeNestedObjectsAndMergeWithTheirParentSection()
        {
            var schema = Export(("database:primary", typeof(NestedSectionConfiguration)),
                ("database", typeof(ParentSectionConfiguration)));

            var database = Section(schema, "database");
            Property(database, "poolSize")["default"]!.GetValue<long>().Should().Be(10);
            Property(Property(database, "primary"), "name").Should().NotBeNull();
        }

        [Test]
        public void ClassesBoundToTheSameSectionInAnotherCasingAreMergedIntoOneSection()
        {
            var schema = Export(_sample, ("SAMPLE", typeof(SecondSampleConfiguration)));

            schema["properties"]!.AsObject().Select(x => x.Key).Should()
                .ContainSingle(x => x.Equals("sample", StringComparison.OrdinalIgnoreCase));
            var sample = schema["properties"]!.AsObject()
                .Single(x => x.Key.Equals("sample", StringComparison.OrdinalIgnoreCase)).Value!.AsObject();
            Property(sample, "extra").Should().NotBeNull();
            Property(sample, "port").Should().NotBeNull();
            Property(sample, "host")["default"].Should().BeNull("the first description of a key wins");
        }

        [Test]
        public void ClassesThatCannotBeInstantiatedAreDescribedWithoutDefaults()
        {
            var section = Section(Export(("noDefaultCtor", typeof(WithoutDefaultConstructor))), "noDefaultCtor");

            Types(Property(section, "value")).Should().Equal("string", "null");
            Property(section, "value")["default"].Should().BeNull();
        }

        [Test]
        public void TheHostSectionIsDescribedWithItsDefaults()
        {
            var host = Section(ConfigurationSchemaExporter.Export([typeof(HostConfiguration)]), "host");

            Property(host, "urls").Should().NotBeNull();
            Property(host, "maxRequestSize")["default"]!.GetValue<long>().Should().Be(8388608);
            Property(host, "additionalConfigPaths").Should().NotBeNull();
            var memory = Property(Property(host, "healthChecks"), "memory");
            Property(memory, "maximumMegabytes")["default"]!.GetValue<long>().Should().Be(2048);
        }

        [Test]
        public void TheSectionsTheWebHostReadsItselfAreAlwaysDescribed()
        {
            var schema = ConfigurationSchemaExporter.Export(Array.Empty<Type>());

            var minimumLevel = Property(Section(schema, "serilog"), "minimumLevel");
            minimumLevel["enum"]!.AsArray().Select(x => x!.GetValue<string>()).Should().Contain(["Debug", "Warning"]);
            minimumLevel["default"]!.GetValue<string>().Should().Be("Information");
            var telemetry = Section(schema, "telemetry");
            Property(telemetry, "endpoint")["format"]!.GetValue<string>().Should().Be("uri");
            Property(telemetry, "protocol")["default"]!.GetValue<string>().Should().Be("Grpc");
            Property(Property(telemetry, "headers")["items"]!.AsObject(), "name").Should().NotBeNull();
            var cors = Section(schema, DosaicWebHostDefaults.CorsConfigSectionName);
            Types(Property(cors, "origins")).Should().Equal("array");
            Types(Property(cors, "supportsCredentials")).Should().Equal("boolean");
        }

        [Test]
        public void SectionsAreSortedSoTheFileIsStableAcrossRuns()
        {
            (string, Type) other = ("another", typeof(ParentSectionConfiguration));
            var first = Export(_sample, other).ToJsonString();
            var second = Export(other, _sample).ToJsonString();

            second.Should().Be(first);
            JsonNode.Parse(first)!["properties"]!.AsObject().Select(x => x.Key).Should()
                .BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
        }

        [Test]
        public void SummariesAreReadFromAnXmlDocumentationFileAsPlainText()
        {
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xml");
            File.WriteAllText(path, """
                <?xml version="1.0"?>
                <doc>
                  <members>
                    <member name="P:Sample.Config.Host">
                      <summary>
                        The host of <see cref="T:Sample.Server" />, see <c>docs</c> and
                        <paramref name="port" />.
                      </summary>
                    </member>
                    <member name="P:Sample.Config.Empty"><summary>   </summary></member>
                    <member name="P:Sample.Config.NoSummary"><remarks>only remarks</remarks></member>
                  </members>
                </doc>
                """);
            try
            {
                var summaries = ConfigurationSchemaExporter.XmlDocumentation.Load(path);

                summaries.Should().ContainSingle().Which.Should().Be(
                    new KeyValuePair<string, string>("P:Sample.Config.Host", "The host of Server, see docs and port."));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void AMissingOrBrokenDocumentationFileOnlyCostsTheDescriptions()
        {
            var broken = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xml");
            File.WriteAllText(broken, "<doc><members>");
            try
            {
                ConfigurationSchemaExporter.XmlDocumentation.Load(broken).Should().BeEmpty();
                ConfigurationSchemaExporter.XmlDocumentation.Load(broken + ".missing").Should().BeEmpty();
                ConfigurationSchemaExporter.XmlDocumentation.Load(null).Should().BeEmpty();
            }
            finally
            {
                File.Delete(broken);
            }
        }

        [TestCase(new[] { "--write-config-schema" }, "appsettings.schema.json")]
        [TestCase(new[] { "--WRITE-CONFIG-SCHEMA", "out/schema.json" }, "out/schema.json")]
        [TestCase(new[] { "--write-config-schema=custom.json" }, "custom.json")]
        [TestCase(new[] { "--write-config-schema=" }, "appsettings.schema.json")]
        [TestCase(new[] { "--write-config-schema", "--other" }, "appsettings.schema.json")]
        [TestCase(new[] { "schema", "export", "--write-config-schema", "a.json", "--x", "b" }, "a.json")]
        public void TheArgumentIsRecognisedWithAndWithoutAPath(string[] args, string expectedPath)
        {
            ConfigurationSchemaCommand.TryGetPath(args, out var path).Should().BeTrue();

            path.Should().Be(expectedPath);
        }

        [Test]
        public void OtherArgumentsDoNotAskForTheSchema()
        {
            ConfigurationSchemaCommand.TryGetPath(["schema", "export", "--output", "x"], out _).Should().BeFalse();
            ConfigurationSchemaCommand.TryGetPath([], out _).Should().BeFalse();
            ConfigurationSchemaCommand.TryGetPath(null, out _).Should().BeFalse();
            ConfigurationSchemaCommand.TryGetPath([null, "--write-config-schema-x"], out _).Should().BeFalse();
            ConfigurationSchemaCommand.TryRun([typeof(HostConfiguration)], null, TextWriter.Null).Should().BeFalse();
        }

        [Test]
        public void TheCommandWritesTheSchemaAndReportsWhereItWent()
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "nested", "appsettings.schema.json");
            using var output = new StringWriter();
            try
            {
                var handled = ConfigurationSchemaCommand.TryRun([typeof(HostConfiguration)],
                    ["--write-config-schema", path], output);

                handled.Should().BeTrue();
                output.ToString().Should().Contain(path);
                var written = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
                Section(written, "host").Should().NotBeNull();
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void TheCommandDoesNothingWithoutItsArgument()
        {
            using var output = new StringWriter();

            ConfigurationSchemaCommand.TryRun([typeof(HostConfiguration)], ["--urls", "http://+:1"], output)
                .Should().BeFalse();

            output.ToString().Should().BeEmpty();
        }

        [Test]
        public void TheSchemaCanBeWrittenThroughThePublicApi()
        {
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.schema.json");
            try
            {
                PluginWebHostBuilder.WriteConfigurationSchema([typeof(HostConfiguration)], path);

                Section(JsonNode.Parse(File.ReadAllText(path))!.AsObject(), "host").Should().NotBeNull();
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
