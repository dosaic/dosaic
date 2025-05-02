using Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Models
{
    public interface IModel
    {
        NanoId Id { get; set; }
    }
}
