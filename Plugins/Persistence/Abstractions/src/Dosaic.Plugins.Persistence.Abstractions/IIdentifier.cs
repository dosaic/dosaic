namespace Dosaic.Plugins.Persistence.Abstractions
{
    public interface IIdentifier<TId>
    {
        TId Id { get; set; }

        TId NewId();
    }
}
