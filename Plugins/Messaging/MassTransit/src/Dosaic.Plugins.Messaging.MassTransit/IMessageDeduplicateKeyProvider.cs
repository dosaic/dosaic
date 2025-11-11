namespace Dosaic.Plugins.Messaging.MassTransit
{
    public interface IMessageDeduplicateKeyProvider
    {
        string TryGetKey(object message);
        void Register<T>(Func<T, string> keyFactory);
    }
}
