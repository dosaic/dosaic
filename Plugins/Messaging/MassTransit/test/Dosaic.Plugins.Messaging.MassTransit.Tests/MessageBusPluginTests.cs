using AwesomeAssertions;
using Dosaic.Hosting.Abstractions;
using Dosaic.Hosting.Abstractions.Services;
using Dosaic.Plugins.Messaging.Abstractions;
using Dosaic.Testing.NUnit;
using Dosaic.Testing.NUnit.Extensions;
using MassTransit;
using MassTransit.Configuration;
using MassTransit.RabbitMqTransport;
using MassTransit.RabbitMqTransport.Configuration;
using MassTransit.Transports;
using MassTransit.Util;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Messaging.MassTransit.Tests;

public class MessageBusPluginTests
{
    private readonly MessageBusConfiguration _configuration = new()
    {
        Host = "localhost",
        Password = "password",
        Port = 5500,
        Username = "username",
        VHost = "/",
        UseRetry = true,
        MaxRedeliveryCount = 3,
        MaxRetryCount = 3,
        RetryDelaySeconds = 30,
        RedeliveryDelaySeconds = 30
    };
    private IImplementationResolver _implementationResolver;
    private MessageBusPlugin _plugin;
    private IMessageBusConfigurator _configurator;

    [SetUp]
    public void Setup()
    {
        _configurator = Substitute.For<IMessageBusConfigurator>();
        _implementationResolver = Substitute.For<IImplementationResolver>();
        _plugin = new MessageBusPlugin(_implementationResolver, _configuration, [_configurator]);
        _implementationResolver.FindAssemblies().Returns([typeof(TestConsumer).Assembly]);
    }

    [Test]
    public void ShouldRegisterServices()
    {
        var sc = TestingDefaults.ServiceCollection();
        _plugin.ConfigureServices(sc);
        var sp = sc.BuildServiceProvider();
        sp.GetRequiredService<IMessageBus>().Should().BeOfType<MessageSender>();
        sp.GetRequiredService<IMessageValidator>().Should().BeOfType<MessageValidator>();
        sp.GetRequiredService<IMessageConsumer<TestMessage>>().Should().BeOfType<TestConsumer>();
        var bus = sp.GetRequiredService<IBus>();
        var host = bus.GetInaccessibleValue<RabbitMqHost>("_host");
        var hostConfig = host.GetInaccessibleValue<RabbitMqHostConfiguration>("_hostConfiguration");
        hostConfig.Settings.Host.Should().Be(_configuration.Host);
        hostConfig.Settings.Port.Should().Be(_configuration.Port);
        hostConfig.Settings.VirtualHost.Should().Be(_configuration.VHost);
        hostConfig.Settings.Username.Should().Be(_configuration.Username);
        hostConfig.Settings.Password.Should().Be(_configuration.Password);
        hostConfig.Settings.Heartbeat.Should().Be(TimeSpan.FromSeconds(30));
        hostConfig.Settings.PublisherConfirmation.Should().Be(true);
        var ep = host.GetInaccessibleValue<ReceiveEndpointCollection>("ReceiveEndpoints")
            .GetInaccessibleValue<SingleThreadedDictionary<string, ReceiveEndpoint>>("_endpoints");
        ep.Should().ContainKey(nameof(TestMessage));
        ep[nameof(TestMessage)].InputAddress.LocalPath.Should().Be($"/{nameof(TestMessage)}");

        var healthOptions = sp.GetService<IOptions<MassTransitHealthCheckOptions<IBus>>>()?.Value;
        healthOptions.Should().NotBeNull();
        healthOptions!.Tags.Should().Contain(HealthCheckTag.Readiness.Value);
        healthOptions.Name.Should().Be("message-bus");
        healthOptions.MinimalFailureStatus.Should().Be(HealthStatus.Unhealthy);

        _configurator.Received().ConfigureMassTransit(Arg.Any<IBusRegistrationConfigurator>());
        _configurator.Received().ConfigureReceiveEndpoint(Arg.Any<IBusRegistrationContext>(), Arg.Any<Uri>(), Arg.Any<IRabbitMqReceiveEndpointConfigurator>());
        _configurator.Received().ConfigureRabbitMq(Arg.Any<IBusRegistrationContext>(), Arg.Any<IRabbitMqBusFactoryConfigurator>());
    }

    internal record TestMessage : IMessage;

    internal class TestConsumer : IMessageConsumer<TestMessage>
    {
        public Task ProcessAsync(TestMessage message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
