using AwesomeAssertions;
using Chronos.Abstractions;
using Dosaic.Hosting.Abstractions;
using Dosaic.Hosting.Abstractions.Attributes;
using Dosaic.Hosting.Abstractions.Plugins;
using Dosaic.Hosting.Abstractions.Services;
using Dosaic.Hosting.WebHost.Configurators;
using Dosaic.Testing.NUnit.Assertions;
using ExternalNamespace;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;

namespace ExternalNamespace
{
    internal class ExternalNamespaceTestPlugin : IPluginApplicationConfiguration, IPluginEndpointsConfiguration, IPluginServiceConfiguration
    {
        public void ConfigureApplication(IApplicationBuilder applicationBuilder)
        {
            // do nothing
            applicationBuilder.UseRouting();
        }

        public void ConfigureEndpoints(IEndpointRouteBuilder endpointRouteBuilder, IServiceProvider serviceProvider)
        {
            // do nothing
        }

        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddHealthChecks();
        }
    }

    internal class XLastNamespaceTestPlugin : IPluginApplicationConfiguration, IPluginEndpointsConfiguration, IPluginServiceConfiguration
    {
        public void ConfigureApplication(IApplicationBuilder applicationBuilder)
        {
            // do nothing
            applicationBuilder.UseRouting();
        }

        public void ConfigureEndpoints(IEndpointRouteBuilder endpointRouteBuilder, IServiceProvider serviceProvider)
        {
            // do nothing
        }

        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddHealthChecks();
        }
    }
}

namespace Dosaic.Hosting.WebHost.Tests
{

    public class AppConfiguratorTests
    {

        [Test]
        public void ConfigurePluginsAndEndpointsShouldApplyThemInCorrectOrder()
        {
            var implementationResolver = Substitute.For<IImplementationResolver>();
            var fakeLogger = new FakeLogger<AppConfiguratorTests>();
            var webApplication = WebApplication.CreateBuilder(new WebApplicationOptions());
            webApplication.Services.AddHealthChecks();
            webApplication.Services.AddControllers();
            var appConfigurator = new AppConfigurator(fakeLogger, webApplication.Build(), implementationResolver);
            implementationResolver.FindTypes().Returns(
            [
                typeof(ExternalNamespaceTestPlugin), typeof(DosaicNamespaceTestPlugin), typeof(XLastNamespaceTestPlugin)
            ]);
            implementationResolver.ResolveInstance(Arg.Is<Type>(t => t == typeof(ExternalNamespaceTestPlugin))).Returns(new ExternalNamespaceTestPlugin());
            implementationResolver.ResolveInstance(Arg.Is<Type>(t => t == typeof(DosaicNamespaceTestPlugin))).Returns(new DosaicNamespaceTestPlugin());
            implementationResolver.ResolveInstance(Arg.Is<Type>(t => t == typeof(XLastNamespaceTestPlugin))).Returns(new XLastNamespaceTestPlugin());
            appConfigurator.ConfigurePlugins();
            appConfigurator.ConfigureEndpoints();
            fakeLogger.Entries[0].Message.Should().Be("Configured application DosaicNamespaceTestPlugin order 0");
            fakeLogger.Entries[1].Message.Should().Be("Configured application ExternalNamespaceTestPlugin order 1");
            fakeLogger.Entries[2].Message.Should().Be("Configured application XLastNamespaceTestPlugin order 2");
            fakeLogger.Entries[3].Message.Should().Be("Configured endpoints DosaicNamespaceTestPlugin order 0");
            fakeLogger.Entries[4].Message.Should().Be("Configured endpoints ExternalNamespaceTestPlugin order 1");
            fakeLogger.Entries[5].Message.Should().Be("Configured endpoints XLastNamespaceTestPlugin order 2");
        }

        private class DosaicNamespaceTestPlugin : IPluginApplicationConfiguration, IPluginEndpointsConfiguration, IPluginServiceConfiguration
        {
            public void ConfigureApplication(IApplicationBuilder applicationBuilder)
            {
                // do nothing
                applicationBuilder.UseRouting();
            }

            public void ConfigureEndpoints(IEndpointRouteBuilder endpointRouteBuilder, IServiceProvider serviceProvider)
            {
                // nothing
            }
            public void ConfigureServices(IServiceCollection serviceCollection)
            {
                serviceCollection.AddHealthChecks();
            }
        }

        [Test]
        public void ConfigureMiddlewaresShouldApplyMiddlwaresInCorrectOrder()
        {
            var implementationResolver = Substitute.For<IImplementationResolver>();
            var fakeLogger = new FakeLogger<AppConfiguratorTests>();
            var appConfigurator = new AppConfigurator(fakeLogger, WebApplication.Create(), implementationResolver);
            implementationResolver.FindTypes().Returns(
                new List<Type>() { typeof(TestMiddlwareWithOrder10), typeof(TestMiddlwareWithOrder3), typeof(TestMiddlwareWithOrder1), });
            appConfigurator.ConfigureMiddlewares(MiddlewareMode.BeforePlugins);
            fakeLogger.Entries[0].Message.Should().Be("Configured middleware TestMiddlwareWithOrder1 order 0");
            fakeLogger.Entries[1].Message.Should().Be("Configured middleware TestMiddlwareWithOrder3 order 1");
            fakeLogger.Entries[2].Message.Should().Be("Configured middleware TestMiddlwareWithOrder10 order 2");
        }

        [Test]
        public void ConfigureMiddlewaresFiltersOnMode()
        {
            var implementationResolver = Substitute.For<IImplementationResolver>();
            var fakeLogger = new FakeLogger<AppConfiguratorTests>();
            var appConfigurator = new AppConfigurator(fakeLogger, WebApplication.Create(), implementationResolver);
            implementationResolver.FindTypes().Returns(
                new List<Type>() { typeof(TestMiddlwareWithOrder10), typeof(TestMiddlwareWithOrder3), typeof(TestMiddlwareWithOrder1), typeof(TestMiddlwareAfterPlugins), typeof(TestMiddlwareNoRegistration), });
            appConfigurator.ConfigureMiddlewares(MiddlewareMode.BeforePlugins);
            fakeLogger.Entries.Should().HaveCount(3);
            fakeLogger.Entries[0].Message.Should().Be("Configured middleware TestMiddlwareWithOrder1 order 0");
            fakeLogger.Entries[1].Message.Should().Be("Configured middleware TestMiddlwareWithOrder3 order 1");
            fakeLogger.Entries[2].Message.Should().Be("Configured middleware TestMiddlwareWithOrder10 order 2");
        }

        [Test]
        public void ConfigureMiddlewaresAfterPlugins()
        {
            var implementationResolver = Substitute.For<IImplementationResolver>();
            var fakeLogger = new FakeLogger<AppConfiguratorTests>();
            var appConfigurator = new AppConfigurator(fakeLogger, WebApplication.Create(), implementationResolver);
            implementationResolver.FindTypes().Returns(
                new List<Type>() { typeof(TestMiddlwareWithOrder10), typeof(TestMiddlwareAfterPlugins), typeof(TestMiddlwareNoRegistration), });
            appConfigurator.ConfigureMiddlewares(MiddlewareMode.AfterPlugins);
            fakeLogger.Entries.Should().HaveCount(1);
            fakeLogger.Entries[0].Message.Should().Be("Configured middleware TestMiddlwareAfterPlugins order 0");
        }

        [Test]
        public void ConfigureMiddlewaresExcludesNoRegistration()
        {
            var implementationResolver = Substitute.For<IImplementationResolver>();
            var fakeLogger = new FakeLogger<AppConfiguratorTests>();
            var appConfigurator = new AppConfigurator(fakeLogger, WebApplication.Create(), implementationResolver);
            implementationResolver.FindTypes().Returns(
                new List<Type>() { typeof(TestMiddlwareNoRegistration), });
            appConfigurator.ConfigureMiddlewares(MiddlewareMode.BeforePlugins);
            fakeLogger.Entries.Should().HaveCount(0);
        }

        [Middleware(1)]
        private class TestMiddlwareWithOrder1 : ApiMiddleware
        {
            public TestMiddlwareWithOrder1(RequestDelegate next, IDateTimeProvider dateTimeProvider) : base(next, dateTimeProvider)
            {
            }

            public override async Task Invoke(HttpContext context)
            {
                await Next.Invoke(context);
            }
        }

        [Middleware(3)]
        private class TestMiddlwareWithOrder3 : ApiMiddleware
        {
            public TestMiddlwareWithOrder3(RequestDelegate next, IDateTimeProvider dateTimeProvider) : base(next, dateTimeProvider)
            {
            }

            public override async Task Invoke(HttpContext context)
            {
                await Next.Invoke(context);
            }
        }

        [Middleware(10)]
        private class TestMiddlwareWithOrder10 : ApiMiddleware
        {
            public TestMiddlwareWithOrder10(RequestDelegate next, IDateTimeProvider dateTimeProvider) : base(next, dateTimeProvider)
            {
            }

            public override async Task Invoke(HttpContext context)
            {
                await Next.Invoke(context);
            }
        }

        [Middleware(1, MiddlewareMode.AfterPlugins)]
        private class TestMiddlwareAfterPlugins : ApiMiddleware
        {
            public TestMiddlwareAfterPlugins(RequestDelegate next, IDateTimeProvider dateTimeProvider) : base(next, dateTimeProvider)
            {
            }

            public override async Task Invoke(HttpContext context)
            {
                await Next.Invoke(context);
            }
        }

        [Middleware(1, MiddlewareMode.NoRegistration)]
        private class TestMiddlwareNoRegistration : ApiMiddleware
        {
            public TestMiddlwareNoRegistration(RequestDelegate next, IDateTimeProvider dateTimeProvider) : base(next, dateTimeProvider)
            {
            }

            public override async Task Invoke(HttpContext context)
            {
                await Next.Invoke(context);
            }
        }
    }
}
