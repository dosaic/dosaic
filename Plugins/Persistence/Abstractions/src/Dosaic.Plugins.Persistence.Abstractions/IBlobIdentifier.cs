namespace Dosaic.Plugins.Persistence.Abstractions
{
    public interface IBlobIdentifier
    {
        string Bucket { get; }
        string Key { get; }
    }
}
