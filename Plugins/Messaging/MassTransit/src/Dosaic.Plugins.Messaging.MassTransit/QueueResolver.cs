using System.Collections.Concurrent;
using Dosaic.Hosting.Abstractions.Extensions;

namespace Dosaic.Plugins.Messaging.MassTransit;

public interface IQueueResolver
{
    Uri ResolveListenAddress(Type messageType);
    Uri ResolveSendAddress(Type messageType);
}

public class QueueResolver : IQueueResolver
{
    private readonly ConcurrentDictionary<Type, Uri> _listenAddresses = new();
    private readonly ConcurrentDictionary<Type, Uri> _sendAddresses = new();
    private readonly IReadOnlySet<Uri> _quorumQueues;

    public QueueResolver(MessageBusConfiguration configuration, IList<(Uri Queue, Type[] ConsumerTypes)> queueGroups)
    {
        var quorumQueues = new HashSet<Uri>();
        foreach (var (queue, consumerTypes) in queueGroups)
        {
            if (configuration.UseQuorumQueues ||
                GetQuorumQueueReplicationFactorFromConsumers(consumerTypes).HasValue)
                quorumQueues.Add(queue);
        }
        _quorumQueues = quorumQueues;
    }

    public static string GetQueueNameFromType(Type t)
    {
        var queueNameAttribute = t.GetAttribute<QueueNameAttribute>();
        if (queueNameAttribute is not null)
            return queueNameAttribute.Name;
        if (!t.IsGenericType) return t.Name;
        return t.Name.Split('`')[0] + "-" + string.Join("-", t.GetGenericArguments().Select(GetQueueNameFromType));
    }

    public static Uri BuildListenAddress(Type t)
    {
        return new Uri($"queue:{GetQueueNameFromType(t)}");
    }

    public Uri ResolveListenAddress(Type messageType)
    {
        return _listenAddresses.GetOrAdd(messageType, BuildListenAddress);
    }

    public Uri ResolveSendAddress(Type messageType)
    {
        return _sendAddresses.GetOrAdd(messageType, t =>
        {
            var listenAddress = ResolveListenAddress(t);
            return _quorumQueues.Contains(listenAddress)
                ? new Uri($"exchange:{listenAddress.PathAndQuery}")
                : listenAddress;
        });
    }

    internal static int? GetQuorumQueueReplicationFactorFromConsumers(Type[] consumerTypes)
    {
        var attributes = consumerTypes
            .Select(t => t.GetAttribute<QuorumQueueAttribute>())
            .Where(a => a is not null)
            .ToArray();
        if (attributes.Length == 0)
            return null;
        var factors = attributes.Select(a => a!.ReplicationFactor).Where(f => f > 0).ToArray();
        return factors.Length > 0 ? factors.Min() : 0;
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class QueueNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
