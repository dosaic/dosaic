namespace Dosaic.Plugins.Messaging.MassTransit;

internal static class MessageBusConstants
{
    /// <summary>Transport header carrying the message deduplication key.</summary>
    public const string DedupeHeader = "x-deduplication-header";

    /// <summary>
    ///     Transport header carrying the send span's W3C traceparent. The consumer starts its own
    ///     root trace and attaches this as an <c>ActivityLink</c> (linking mode).
    /// </summary>
    public const string TraceLinkHeader = "x-trace-link";

    /// <summary>
    ///     Transport header carrying the send span's W3C traceparent. The consumer continues the
    ///     trace as a child of it (parent-child mode, when trace linking is disabled).
    /// </summary>
    public const string TraceParentHeader = "x-trace-parent";

    /// <summary>Prefix applied to every messaging span name.</summary>
    public const string SpanPrefix = "MSG";
}
