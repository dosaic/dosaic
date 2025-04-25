using Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit
{
    public interface IAuditableModel : IModel
    {
        [ExcludeFromHistory]
        NanoId CreatedBy { get; set; }
        [ExcludeFromHistory]
        DateTime CreatedUtc { get; set; }
        [ExcludeFromHistory]
        NanoId ModifiedBy { get; set; }
        [ExcludeFromHistory]
        DateTime? ModifiedUtc { get; set; }
    }
}
