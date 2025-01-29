using System.Collections.Concurrent;
using Dosaic.Hosting.Abstractions.Extensions;
using Dosaic.Plugins.Messaging.Abstractions;

namespace Dosaic.Plugins.Messaging.MassTransit;

internal static class QueueResolver
{
    private static readonly IDictionary<Type, string> _queueNames = new ConcurrentDictionary<Type, string>();

    private static string GetQueueNameFromType(Type t)
    {
        var queueNameAttribute = t.GetAttribute<QueueNameAttribute>();
        if (queueNameAttribute is not null)
            return queueNameAttribute.Name;
        if (!t.IsGenericType) return t.Name;
        return t.Name.Split('`')[0] + "-" + string.Join("-", t.GetGenericArguments().Select(GetQueueNameFromType));
    }
    public static string Resolve(Type t)
    {
        if (_queueNames.TryGetValue(t, out var value))
            return value;
        var queueName = GetQueueNameFromType(t);
        _queueNames.Add(t, queueName);
        return queueName;
    }
    public static Uri Resolve<TMessage>() where TMessage : IMessage => new($"queue:{Resolve(typeof(TMessage))}");
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class QueueNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
