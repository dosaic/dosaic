using Dosaic.Extensions.NanoIds;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit
{
    public abstract class AuditableModel : IAuditableModel
    {
        public required NanoId Id { get; set; }
        [ExcludeFromHistory]
        public NanoId CreatedBy { get; set; }
        [ExcludeFromHistory]
        public DateTime CreatedUtc { get; set; }
        [ExcludeFromHistory]
        public NanoId ModifiedBy { get; set; }
        [ExcludeFromHistory]
        public DateTime? ModifiedUtc { get; set; }
    }
}
