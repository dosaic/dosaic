using AwesomeAssertions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Eventsourcing;
using Dosaic.Testing.NUnit;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Eventsourcing
{
    public class EventTypeMapperFactoryTests
    {
        private IServiceProvider _serviceProvider;
        private EventTypeMapperFactory<TestEventMapper> _factory;

        [SetUp]
        public void Setup()
        {
            var serviceCollection = TestingDefaults.ServiceCollection();
            serviceCollection.AddTransient<FirstMapper>();
            serviceCollection.AddTransient<SecondMapper>();
            serviceCollection.AddTransient(typeof(IEventTypeMapperFactory<>), typeof(EventTypeMapperFactory<>));
            _serviceProvider = serviceCollection.BuildServiceProvider();
            _factory = new EventTypeMapperFactory<TestEventMapper>(_serviceProvider);
        }

        [Test]
        public void ResolveReturnsMapperFromDI()
        {
            var key = TestEnum.First;
            var mapperType = typeof(FirstMapper);
            TestEventMapper.Mappers[key] = mapperType;

            var result = _factory.Resolve(key);

            result.Should().BeAssignableTo<FirstMapper>();
            _serviceProvider.GetRequiredService(mapperType).Should().NotBeNull();
        }

        [Test]
        public void ResolveWithDiffKeyReturnsCorrectMapper()
        {
            var key1 = TestEnum.First;
            var key2 = TestEnum.Second;
            var mapperType1 = typeof(FirstMapper);
            var mapperType2 = typeof(SecondMapper);
            TestEventMapper.Mappers[key1] = mapperType1;
            TestEventMapper.Mappers[key2] = mapperType2;

            var result1 = _factory.Resolve(key1);
            var result2 = _factory.Resolve(key2);

            result1.Should().BeAssignableTo<FirstMapper>();
            result2.Should().BeAssignableTo<SecondMapper>();

            _serviceProvider.GetRequiredService(mapperType1).Should().NotBeNull();
            _serviceProvider.GetRequiredService(mapperType2).Should().NotBeNull();
        }

        [Test]
        public void ResolveWithNonExistKeyThrowsException()
        {
            var key = TestEnum.NonExist;

            if (TestEventMapper.Mappers.ContainsKey(key))
                TestEventMapper.Mappers.Remove(key);

            FluentActions.Invoking(() => _factory.Resolve(key))
                .Should().Throw<KeyNotFoundException>();
        }

        // Helper classes for testing
        public enum TestEnum
        {
            First,
            Second,
            NonExist
        }

        public class TestEventMapper : IEventMapper
        {
            public static IDictionary<Enum, Type> Mappers { get; } = new Dictionary<Enum, Type>();
        }

        public class FirstMapper : TestEventMapper;

        public class SecondMapper : TestEventMapper;
    }
}
