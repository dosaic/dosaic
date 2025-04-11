using Dosaic.Hosting.Abstractions.DependencyInjection;
using Dosaic.Hosting.Abstractions.Extensions;
using Dosaic.Testing.NUnit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Dosaic.Hosting.Abstractions.Tests.Extensions
{
    public class FactoryExtensionTests
    {
        private IServiceCollection _serviceCollection;

        private class X;

        [SetUp]
        public void Setup()
        {
            _serviceCollection = TestingDefaults.ServiceCollection();
        }

        [TestCase(ServiceLifetime.Scoped)]
        [TestCase(ServiceLifetime.Transient)]
        [TestCase(ServiceLifetime.Singleton)]
        public void AddFactoryShouldRegisterFactoryWithSpecifiedServiceLifetime(ServiceLifetime lifetime)
        {
            _serviceCollection.AddFactory<X>(lifetime);

            _serviceCollection.Should().Contain(x =>
                x.ServiceType == typeof(IFactory<X>) &&
                x.Lifetime == lifetime &&
                x.ImplementationFactory != null);
            var sp = _serviceCollection.BuildServiceProvider();

            sp.GetRequiredService<IFactory<X>>().Should().BeAssignableTo<Factory<X>>();
        }

        [Test]
        public void AddFactoryShouldRegisterFactoryWithDefaultServiceLifetimeTransient()
        {
            _serviceCollection.AddFactory<X>();

            _serviceCollection.Should().Contain(x =>
                x.ServiceType == typeof(IFactory<X>) &&
                x.Lifetime == ServiceLifetime.Transient &&
                x.ImplementationFactory != null);
            var sp = _serviceCollection.BuildServiceProvider();

            sp.GetRequiredService<IFactory<X>>().Should().BeAssignableTo<Factory<X>>();
        }
    }
}
