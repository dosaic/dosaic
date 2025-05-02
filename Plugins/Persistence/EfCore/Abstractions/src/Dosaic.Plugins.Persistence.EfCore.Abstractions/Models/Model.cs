using Dosaic.Extensions.NanoIds;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Models
{
    public abstract class Model : IModel
    {
        public required NanoId Id { get; set; }
    }
}
