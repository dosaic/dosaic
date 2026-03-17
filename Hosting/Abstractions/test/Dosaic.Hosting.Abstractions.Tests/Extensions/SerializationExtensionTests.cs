using AwesomeAssertions;
using Dosaic.Hosting.Abstractions.Extensions;
using NUnit.Framework;

namespace Dosaic.Hosting.Abstractions.Tests.Extensions
{
    public class SerializationExtensionTests
    {
        private class Sample
        {
            // vogen type...
            public HealthCheckTag HealthCheckTag { get; set; }
            public string Name { get; set; }
        }

        [Test]
        public void CanSerializeAndDeserializeJson()
        {
            var sample = new Sample { HealthCheckTag = HealthCheckTag.Liveness, Name = "test" };
            var serialized = sample.Serialize();
            var deserialized = serialized.Deserialize<Sample>();

            deserialized.Should().BeEquivalentTo(sample);

            var obj = serialized.Deserialize(typeof(Sample));
            obj.Should().BeOfType<Sample>();
            (obj as Sample).Should().BeEquivalentTo(sample);
        }

        [Test]
        public void CanSerializeAndDeserializeYaml()
        {
            var sample = new Sample { HealthCheckTag = HealthCheckTag.Liveness, Name = "test" };
            var serialized = sample.Serialize(SerializationMethod.Yaml);
            var withSchema = "$schema: bla.json" + Environment.NewLine + serialized;
            var deserialized = serialized.Deserialize<Sample>(SerializationMethod.Yaml);
            var deserializedWithSchema = withSchema.Deserialize<Sample>(SerializationMethod.Yaml);

            deserialized.Should().BeEquivalentTo(sample);
            deserializedWithSchema.Should().BeEquivalentTo(sample);

            var obj = serialized.Deserialize(typeof(Sample), SerializationMethod.Yaml);
            obj.Should().BeOfType<Sample>();
            (obj as Sample).Should().BeEquivalentTo(sample);
        }

        [Test]
        public void CanSerializeAndDeserializeObjectsWithKindSpecifiers()
        {
            var sample = new SampleWithKindSpecifiers
            {
                Samples = [new SampleConf { Name = "one" }, new Sample2Conf { Name = "two" }]
            };
            var serialized = sample.Serialize();
            var deserialized = serialized.Deserialize<SampleWithKindSpecifiers>();
            deserialized.Samples.Should().HaveCount(2);
            deserialized.Samples[0].Name.Should().Be("one");
            deserialized.Samples[0].Kind.Should().Be("one");
            deserialized.Samples[1].Name.Should().Be("two");
            deserialized.Samples[1].Kind.Should().Be("two");

            serialized = sample.Serialize(SerializationMethod.Yaml);
            deserialized = serialized.Deserialize<SampleWithKindSpecifiers>(SerializationMethod.Yaml);
            deserialized.Samples.Should().HaveCount(2);
            deserialized.Samples[0].Name.Should().Be("one");
            deserialized.Samples[0].Kind.Should().Be("one");
            deserialized.Samples[1].Name.Should().Be("two");
            deserialized.Samples[1].Kind.Should().Be("two");
        }

        private interface ISample : IKindSpecifier
        {
            string Name { get; }
        }

        private class SampleConf : ISample
        {
            public string Kind => "one";
            public string Name { get; set; }
        }

        private class Sample2Conf : ISample
        {
            public string Kind => "two";
            public string Name { get; set; }
        }

        private class SampleWithKindSpecifiers
        {
            public IList<ISample> Samples { get; set; }
        }

        private interface IMixedCaseKind : IKindSpecifier
        {
            string Value { get; }
        }

        private class MixedCaseConf : IMixedCaseKind
        {
            public string Kind => "Expression";
            public string Value { get; set; }
        }

        private class MixedCaseContainer
        {
            public IList<IMixedCaseKind> Items { get; set; }
        }

        [Test]
        public void KindSpecifierWithMixedCaseKindWorksForJson()
        {
            var sample = new MixedCaseContainer
            {
                Items = [new MixedCaseConf { Value = "hello" }]
            };
            var serialized = sample.Serialize();
            var deserialized = serialized.Deserialize<MixedCaseContainer>();
            deserialized.Items.Should().HaveCount(1);
            deserialized.Items[0].Value.Should().Be("hello");
            deserialized.Items[0].Kind.Should().Be("Expression");
        }

        [Test]
        public void KindSpecifierWithMixedCaseKindWorksForYaml()
        {
            var sample = new MixedCaseContainer
            {
                Items = [new MixedCaseConf { Value = "hello" }]
            };
            var serialized = sample.Serialize(SerializationMethod.Yaml);
            var deserialized = serialized.Deserialize<MixedCaseContainer>(SerializationMethod.Yaml);
            deserialized.Items.Should().HaveCount(1);
            deserialized.Items[0].Value.Should().Be("hello");
            deserialized.Items[0].Kind.Should().Be("Expression");
        }

        // #2 - JSON CanConvert only matches interfaces; YAML Accepts matches all IKindSpecifier types
        private class ConcreteKindContainer
        {
            public SampleConf Item { get; set; }
        }

        [Test]
        public void ConcreteKindSpecifierPropertyWorksForJson()
        {
            var sample = new ConcreteKindContainer { Item = new SampleConf { Name = "test" } };
            var serialized = sample.Serialize();
            var deserialized = serialized.Deserialize<ConcreteKindContainer>();
            deserialized.Item.Name.Should().Be("test");
            deserialized.Item.Kind.Should().Be("one");
        }

        [Test]
        public void ConcreteKindSpecifierPropertyWorksForYaml()
        {
            var sample = new ConcreteKindContainer { Item = new SampleConf { Name = "test" } };
            var serialized = sample.Serialize(SerializationMethod.Yaml);
            var deserialized = serialized.Deserialize<ConcreteKindContainer>(SerializationMethod.Yaml);
            deserialized.Item.Name.Should().Be("test");
            deserialized.Item.Kind.Should().Be("one");
        }

        // #3 - JSON scopes type discovery per interface; YAML uses one global dictionary
        private interface IAlpha : IKindSpecifier
        {
            string AlphaValue { get; }
        }

        private interface IBeta : IKindSpecifier
        {
            string BetaValue { get; }
        }

        private class AlphaImpl : IAlpha
        {
            public string Kind => "shared";
            public string AlphaValue { get; set; }
        }

        private class BetaImpl : IBeta
        {
            public string Kind => "shared";
            public string BetaValue { get; set; }
        }

        private class OverlapContainer
        {
            public IList<IAlpha> Alphas { get; set; }
            public IList<IBeta> Betas { get; set; }
        }

        [Test]
        public void OverlappingKindNamesAcrossInterfacesWorksForJson()
        {
            var sample = new OverlapContainer
            {
                Alphas = [new AlphaImpl { AlphaValue = "a" }],
                Betas = [new BetaImpl { BetaValue = "b" }]
            };
            var serialized = sample.Serialize();
            var deserialized = serialized.Deserialize<OverlapContainer>();
            deserialized.Alphas.Should().HaveCount(1);
            deserialized.Alphas[0].Should().BeOfType<AlphaImpl>();
            deserialized.Alphas[0].AlphaValue.Should().Be("a");
            deserialized.Betas.Should().HaveCount(1);
            deserialized.Betas[0].Should().BeOfType<BetaImpl>();
            deserialized.Betas[0].BetaValue.Should().Be("b");
        }

        [Test]
        public void OverlappingKindNamesAcrossInterfacesWorksForYaml()
        {
            var sample = new OverlapContainer
            {
                Alphas = [new AlphaImpl { AlphaValue = "a" }],
                Betas = [new BetaImpl { BetaValue = "b" }]
            };
            var serialized = sample.Serialize(SerializationMethod.Yaml);
            var deserialized = serialized.Deserialize<OverlapContainer>(SerializationMethod.Yaml);
            deserialized.Alphas.Should().HaveCount(1);
            deserialized.Alphas[0].Should().BeOfType<AlphaImpl>();
            deserialized.Alphas[0].AlphaValue.Should().Be("a");
            deserialized.Betas.Should().HaveCount(1);
            deserialized.Betas[0].Should().BeOfType<BetaImpl>();
            deserialized.Betas[0].BetaValue.Should().Be("b");
        }

        // #4 - JSON writes proper objects; YAML writes KindSpecifiers as scalar strings
        [Test]
        public void KindSpecifierJsonOutputIsStructuredObject()
        {
            var sample = new SampleWithKindSpecifiers
            {
                Samples = [new SampleConf { Name = "one" }]
            };
            var serialized = sample.Serialize();
            var generic = serialized.Deserialize<Dictionary<string, List<Dictionary<string, object>>>>();
            generic["samples"][0].Should().ContainKey("kind");
            generic["samples"][0].Should().ContainKey("name");
        }

        [Test]
        public void KindSpecifierYamlOutputIsStructuredMapping()
        {
            var sample = new SampleWithKindSpecifiers
            {
                Samples = [new SampleConf { Name = "one" }]
            };
            var serialized = sample.Serialize(SerializationMethod.Yaml);
            var generic = serialized.Deserialize<Dictionary<string, List<Dictionary<string, string>>>>(SerializationMethod.Yaml);
            generic["samples"][0].Should().ContainKey("kind");
            generic["samples"][0].Should().ContainKey("name");
        }

        // Mimics the ConfigurationExtensions.GetSection path: dict → YAML → typed object
        [Test]
        public void KindSpecifierFromDictionaryViaYamlWorks()
        {
            var dict = new Dictionary<string, object>
            {
                {
                    "items", new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            { "kind", "Expression" },
                            { "value", "hello" }
                        }
                    }
                }
            };
            var yaml = dict.Serialize(SerializationMethod.Yaml);
            var result = yaml.Deserialize<MixedCaseContainer>(SerializationMethod.Yaml);
            result.Items.Should().HaveCount(1);
            result.Items[0].Value.Should().Be("hello");
            result.Items[0].Kind.Should().Be("Expression");
        }
    }
}
