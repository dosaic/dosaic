using AwesomeAssertions;
using Dosaic.Hosting.Abstractions.Attributes;
using Dosaic.Hosting.Abstractions.Extensions;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace Dosaic.Hosting.Abstractions.Tests.Extensions
{
    public class ConfigurationExtensionTests
    {
        [Test]
        public void CanGetConfigurationSectionAsTypedObject()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "conf:key1", "value1" },
                    { "conf:key2:name", "name" },
                    { "conf:key2:active", "true" },
                    { "conf:key2:age", "13" },
                    { "conf:key2:price", "123.321" },
                    { "conf:key3:0:kind", "test" },
                    { "conf:key3:0:name", "some-name" },
                    { "conf:key3:1:kind", "test" },
                    { "conf:key3:1:name", "some-name2" },
                    { "conf:key4:0", "1" },
                    { "conf:key4:1", "2" },
                    { "conf:key4:2", "3" },
                })
                .Build();
            var result = configuration.GetSection("conf", typeof(TestConfiguration));
            var config = result.Should().NotBeNull().And.BeOfType<TestConfiguration>().Which;
            config.Key1.Should().Be("value1");
            config.Key2.Name.Should().Be("name");
            config.Key2.Active.Should().BeTrue();
            config.Key2.Age.Should().Be(13);
            config.Key2.Price.Should().Be(123.321m);
            config.Key3.Should().HaveCount(2);
            var conf1 = config.Key3[0].Should().BeOfType<TestKindConfiguration>().Which;
            var conf2 = config.Key3[1].Should().BeOfType<TestKindConfiguration>().Which;
            conf1.Kind.Should().Be("test");
            conf1.Name.Should().Be("some-name");
            conf2.Kind.Should().Be("test");
            conf2.Name.Should().Be("some-name2");

            config.Key4.Should().HaveCount(3)
                .And.BeEquivalentTo([1, 2, 3]);
            config.Key5.Should().BeNull();

            configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "conf:key1", "value1" },
                    { "conf:key2:name", "name" },
                    { "conf:key2:active", "true" },
                    { "conf:key2:age", "13" },
                    { "conf:key2:price", "123.321" },
                    // { "conf:key3:0:kind", "test" },
                    { "conf:key3:0:name", "some-name" },
                    { "conf:key3:1:kind", "test" },
                    { "conf:key3:1:name", "some-name2" },
                    { "conf:key4:0", "1" },
                    { "conf:key4:1", "2" },
                    { "conf:key4:2", "3" },
                })
                .Build();
            configuration.Invoking(x => x.GetSection("conf", typeof(TestConfiguration)))
                .Should().Throw<YamlDotNet.Core.YamlException>().And.Message.Should()
                .Contain("Unknown type for kind: not set");

            configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "conf:key1", "value1" },
                    { "conf:key2:name", "name" },
                    { "conf:key2:active", "true" },
                    { "conf:key2:age", "13" },
                    { "conf:key2:price", "123.321" },
                    { "conf:key3:0:kind", "failure" },
                    { "conf:key3:0:name", "some-name" },
                    { "conf:key3:1:kind", "test" },
                    { "conf:key3:1:name", "some-name2" },
                    { "conf:key4:0", "1" },
                    { "conf:key4:1", "2" },
                    { "conf:key4:2", "3" },
                })
                .Build();
            configuration.Invoking(x => x.GetSection("conf", typeof(TestConfiguration)))
                .Should().Throw<YamlDotNet.Core.YamlException>().And.Message.Should()
                .Contain("Unknown type for kind: failure");
        }

        [Test]
        public void GetSectionWrapsExceptionInAggregateException()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "conf:key3:0:kind", "failure" },
                    { "conf:key3:0:name", "some-name" },
                })
                .Build();

            var act = () => configuration.GetSection("conf", typeof(TestConfiguration));

            var exception = act.Should().Throw<AggregateException>().Which;
            exception.Message.Should().Contain("conf").And.Contain(typeof(TestConfiguration).FullName);
            exception.InnerException.Should().NotBeNull();
        }

        [Test]
        public void MissingSectionNamesTheSectionAndTheType()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string> { { "other:key", "value" } })
                .Build();

            var act = () => configuration.GetSection("conf", typeof(TestConfiguration));

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("Configuration section 'conf' is missing. TestConfiguration is bound from it.");
        }

        [Test]
        public void MissingSectionNamesTheRequiredKeys()
        {
            var configuration = new ConfigurationBuilder().Build();

            var act = () => configuration.GetSection("my:conf", typeof(RequiredKeysConfiguration));

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("Configuration section 'my:conf' is missing. RequiredKeysConfiguration is bound from it. " +
                             "Set at least my:conf:host, my:conf:user (environment: MY_CONF_HOST, MY_CONF_USER).");
        }

        [Test]
        public void ASectionWithAnyKeyIsBoundEvenWhenRequiredKeysAreMissing()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string> { { "conf:port", "1" } })
                .Build();

            var result = (RequiredKeysConfiguration)configuration.GetSection("conf", typeof(RequiredKeysConfiguration));

            result.Port.Should().Be(1);
            result.Host.Should().BeNull();
        }

        [Configuration("conf", RequiredKeys = ["host", "user"])]
        public class RequiredKeysConfiguration
        {
            public string Host { get; set; }
            public string User { get; set; }
            public int Port { get; set; }
        }

        [Test]
        public void CanGetSectionWhenSerializationProviderCannotBeResolved()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string> { { "other:key", "value" } })
                .Build();

            var result = configuration.GetSection("conf", typeof(List<int>));

            result.Should().NotBeNull()
                .And.BeOfType<List<int>>()
                .Which.Should().BeEmpty();
        }

        [Test]
        public void DictionaryKeysStayNamesEvenWhenTheyAreAllNumbers()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "conf:nested:counts:1", "4" },
                    { "conf:nested:counts:2", "2" },
                    { "conf:byName:10:age", "3" },
                    { "conf:list:0", "7" },
                })
                .Build();

            var config = (MapConfiguration)configuration.GetSection("conf", typeof(MapConfiguration));

            config.Nested.Counts.Should().BeEquivalentTo(new Dictionary<string, int> { ["1"] = 4, ["2"] = 2 });
            config.ByName.Should().ContainKey("10").WhoseValue.Age.Should().Be(3);
            config.List.Should().Equal(7);
        }

        [Test]
        public void UnderscoresThatEnvironmentVariablesTurnedIntoSectionsAreJoinedBackForPlainValueMaps()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    // HANGFIRE_..._MY_QUEUE=3 reaches the configuration as my:queue
                    { "conf:nested:counts:my:queue", "3" },
                    { "conf:nested:counts:my:other:queue", "5" },
                    { "conf:nested:counts:plain", "1" },
                    { "conf:byName:first:age", "4" },
                })
                .Build();

            var config = (MapConfiguration)configuration.GetSection("conf", typeof(MapConfiguration));

            config.Nested.Counts.Should().BeEquivalentTo(new Dictionary<string, int>
            {
                ["my_queue"] = 3,
                ["my_other_queue"] = 5,
                ["plain"] = 1
            });
            config.ByName.Should().ContainKey("first").WhoseValue.Age.Should().Be(4);
        }

        private class MapConfiguration
        {
            public MapNestConfiguration Nested { get; set; }
            public Dictionary<string, TestNestConfiguration> ByName { get; set; }
            public int[] List { get; set; }
        }

        private class MapNestConfiguration
        {
            public IDictionary<string, int> Counts { get; set; }
        }

        private class TestConfiguration
        {
            public string Key1 { get; set; }
            public TestNestConfiguration Key2 { get; set; }
            public IList<IInterfaceConfiguration> Key3 { get; set; }
            public IList<int> Key4 { get; set; }
            public string Key5 { get; set; }
        }

        private class TestNestConfiguration
        {
            public string Name { get; set; }
            public int Age { get; set; }
            public bool Active { get; set; }
            public decimal Price { get; set; }
        }

        private interface IInterfaceConfiguration : IKindSpecifier;

        private class TestKindConfiguration : IInterfaceConfiguration
        {
            public string Kind => "test";
            public string Name { get; set; }
        }
    }
}
