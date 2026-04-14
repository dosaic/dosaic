using AwesomeAssertions;
using Dosaic.Hosting.Abstractions;
using Dosaic.Hosting.Abstractions.Services;
using Dosaic.Plugins.Messaging.Abstractions;
using Dosaic.Testing.NUnit;
using Dosaic.Testing.NUnit.Assertions;
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
        sp.Should().RegisterSources("MassTransit");
        sp.GetRequiredService<IMessageBus>().Should().BeOfType<MessageSender>();
        sp.GetRequiredService<IMessageValidator>().Should().BeOfType<MessageValidator>();
        sp.GetRequiredService<IMessageConsumer<TestMessage>>().Should().BeOfType<TestConsumer>();
        sp.GetRequiredService<IMessageDeduplicateKeyProvider>().Should().BeOfType<MessageDeduplicateKeyProvider>();
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
        _configurator.Received().ConfigureReceiveEndpoint(Arg.Any<IBusRegistrationContext>(), Arg.Any<Uri>(), Arg.Any<Type[]>(), Arg.Any<IRabbitMqReceiveEndpointConfigurator>());
        _configurator.Received().ConfigureRabbitMq(Arg.Any<IBusRegistrationContext>(), Arg.Any<IRabbitMqBusFactoryConfigurator>());
    }

    [Test]
    public void ShouldRegisterServicesWithInMemoryTransport()
    {
        var inMemoryConfig = new MessageBusConfiguration { UseInMemory = true };
        var plugin = new MessageBusPlugin(_implementationResolver, inMemoryConfig, [_configurator]);
        var sc = TestingDefaults.ServiceCollection();
        plugin.ConfigureServices(sc);
        var sp = sc.BuildServiceProvider();
        sp.Should().RegisterSources("MassTransit");
        sp.GetRequiredService<IMessageBus>().Should().BeOfType<MessageSender>();
        sp.GetRequiredService<IMessageValidator>().Should().BeOfType<MessageValidator>();
        sp.GetRequiredService<IMessageConsumer<TestMessage>>().Should().BeOfType<TestConsumer>();

        var healthOptions = sp.GetService<IOptions<MassTransitHealthCheckOptions<IBus>>>()?.Value;
        healthOptions.Should().NotBeNull();
        healthOptions!.Tags.Should().Contain(HealthCheckTag.Readiness.Value);
        healthOptions.Name.Should().Be("message-bus");
        healthOptions.MinimalFailureStatus.Should().Be(HealthStatus.Unhealthy);

        _configurator.Received().ConfigureMassTransit(Arg.Any<IBusRegistrationConfigurator>());
        _configurator.DidNotReceive().ConfigureRabbitMq(Arg.Any<IBusRegistrationContext>(), Arg.Any<IRabbitMqBusFactoryConfigurator>());
        _configurator.DidNotReceive().ConfigureReceiveEndpoint(Arg.Any<IBusRegistrationContext>(), Arg.Any<Uri>(), Arg.Any<Type[]>(), Arg.Any<IRabbitMqReceiveEndpointConfigurator>());
    }

    internal record TestMessage : IMessage;

    internal class TestConsumer : IMessageConsumer<TestMessage>
    {
        public Task ProcessAsync(TestMessage message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    internal record QuorumTestMessage : IMessage;

    [QuorumQueue(5)]
    internal class QuorumTestConsumer : IMessageConsumer<QuorumTestMessage>
    {
        public Task ProcessAsync(QuorumTestMessage message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    internal record QuorumDefaultTestMessage : IMessage;

    [QuorumQueue]
    internal class QuorumDefaultTestConsumer : IMessageConsumer<QuorumDefaultTestMessage>
    {
        public Task ProcessAsync(QuorumDefaultTestMessage message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private static Dictionary<string, IDictionary<string, object>> GetEndpointQueueArguments(IServiceProvider sp)
    {
        var bus = sp.GetRequiredService<IBus>();
        var host = bus.GetInaccessibleValue<RabbitMqHost>("_host");
        var endpoints = host.GetInaccessibleValue<ReceiveEndpointCollection>("ReceiveEndpoints")
            .GetInaccessibleValue<SingleThreadedDictionary<string, ReceiveEndpoint>>("_endpoints");
        var result = new Dictionary<string, IDictionary<string, object>>();
        foreach (var kvp in endpoints)
        {
            var queueArgs = kvp.Value
                .GetInaccessibleValue<object>("_context")
                .GetInaccessibleValue<object>("_configuration")
                .GetInaccessibleValue<object>("_settings")
                .GetInaccessibleValue<IDictionary<string, object>>("QueueArguments");
            result[kvp.Key] = queueArgs;
        }
        return result;
    }

    [Test]
    public void ShouldConfigureQuorumQueuesFromGlobalConfig()
    {
        var config = new MessageBusConfiguration
        {
            Host = "localhost",
            Username = "guest",
            Password = "guest",
            UseQuorumQueues = true,
            QuorumQueueReplicationFactor = 3
        };
        var plugin = new MessageBusPlugin(_implementationResolver, config, [_configurator]);
        var sc = TestingDefaults.ServiceCollection();
        plugin.ConfigureServices(sc);
        var sp = sc.BuildServiceProvider();

        var allArgs = GetEndpointQueueArguments(sp);
        allArgs.Should().ContainKey(nameof(TestMessage));
        allArgs[nameof(TestMessage)].Should().Contain("x-queue-type", "quorum");
    }

    [Test]
    public void ShouldConfigureQuorumQueuesFromAttribute()
    {
        var config = new MessageBusConfiguration
        {
            Host = "localhost",
            Username = "guest",
            Password = "guest"
        };
        var plugin = new MessageBusPlugin(_implementationResolver, config, [_configurator]);
        var sc = TestingDefaults.ServiceCollection();
        plugin.ConfigureServices(sc);
        var sp = sc.BuildServiceProvider();

        var allArgs = GetEndpointQueueArguments(sp);
        allArgs.Should().ContainKey(nameof(QuorumTestMessage));
        var args = allArgs[nameof(QuorumTestMessage)];
        args.Should().Contain("x-queue-type", "quorum");
        args.Should().Contain("x-quorum-initial-group-size", 5);
    }

    [Test]
    public void ShouldPreferAttributeOverGlobalConfig()
    {
        var config = new MessageBusConfiguration
        {
            Host = "localhost",
            Username = "guest",
            Password = "guest",
            UseQuorumQueues = true,
            QuorumQueueReplicationFactor = 3
        };
        var plugin = new MessageBusPlugin(_implementationResolver, config, [_configurator]);
        var sc = TestingDefaults.ServiceCollection();
        plugin.ConfigureServices(sc);
        var sp = sc.BuildServiceProvider();

        var allArgs = GetEndpointQueueArguments(sp);
        var args = allArgs[nameof(QuorumTestMessage)];
        args.Should().Contain("x-quorum-initial-group-size", 5);
    }

    [Test]
    public void ShouldNotUseQuorumQueuesWhenDisabled()
    {
        var config = new MessageBusConfiguration
        {
            Host = "localhost",
            Username = "guest",
            Password = "guest"
        };
        var plugin = new MessageBusPlugin(_implementationResolver, config, [_configurator]);
        var sc = TestingDefaults.ServiceCollection();
        plugin.ConfigureServices(sc);
        var sp = sc.BuildServiceProvider();

        var allArgs = GetEndpointQueueArguments(sp);
        allArgs[nameof(TestMessage)].Should().NotContainKey("x-queue-type");
    }

    [Test]
    public void ShouldSetDeliveryLimitOnQuorumQueues()
    {
        var config = new MessageBusConfiguration
        {
            Host = "localhost",
            Username = "guest",
            Password = "guest",
            UseQuorumQueues = true,
            DeliveryLimit = 5
        };
        var plugin = new MessageBusPlugin(_implementationResolver, config, [_configurator]);
        var sc = TestingDefaults.ServiceCollection();
        plugin.ConfigureServices(sc);
        var sp = sc.BuildServiceProvider();

        var allArgs = GetEndpointQueueArguments(sp);
        var args = allArgs[nameof(TestMessage)];
        args.Should().Contain("x-queue-type", "quorum");
        args.Should().Contain("x-delivery-limit", 5);
    }

    [Test]
    public void ShouldNotSetDeliveryLimitOnClassicQueues()
    {
        var config = new MessageBusConfiguration
        {
            Host = "localhost",
            Username = "guest",
            Password = "guest",
            DeliveryLimit = 5
        };
        var plugin = new MessageBusPlugin(_implementationResolver, config, [_configurator]);
        var sc = TestingDefaults.ServiceCollection();
        plugin.ConfigureServices(sc);
        var sp = sc.BuildServiceProvider();

        var allArgs = GetEndpointQueueArguments(sp);
        allArgs[nameof(TestMessage)].Should().NotContainKey("x-queue-type");
        allArgs[nameof(TestMessage)].Should().NotContainKey("x-delivery-limit");
    }

    [Test]
    public void ShouldSetDeliveryLimitOnAttributeQuorumQueues()
    {
        var config = new MessageBusConfiguration
        {
            Host = "localhost",
            Username = "guest",
            Password = "guest",
            DeliveryLimit = 10
        };
        var plugin = new MessageBusPlugin(_implementationResolver, config, [_configurator]);
        var sc = TestingDefaults.ServiceCollection();
        plugin.ConfigureServices(sc);
        var sp = sc.BuildServiceProvider();

        var allArgs = GetEndpointQueueArguments(sp);
        var args = allArgs[nameof(QuorumTestMessage)];
        args.Should().Contain("x-queue-type", "quorum");
        args.Should().Contain("x-delivery-limit", 10);
    }

    [Test]
    public void ShouldConfigureQuorumQueueFromAttributeWithDefaultReplicationFactor()
    {
        var config = new MessageBusConfiguration
        {
            Host = "localhost",
            Username = "guest",
            Password = "guest"
        };
        var plugin = new MessageBusPlugin(_implementationResolver, config, [_configurator]);
        var sc = TestingDefaults.ServiceCollection();
        plugin.ConfigureServices(sc);
        var sp = sc.BuildServiceProvider();

        var allArgs = GetEndpointQueueArguments(sp);
        var args = allArgs[nameof(QuorumDefaultTestMessage)];
        args.Should().Contain("x-queue-type", "quorum");
        args.Should().NotContainKey("x-quorum-initial-group-size");
    }
}
