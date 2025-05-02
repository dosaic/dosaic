using Dosaic.Extensions.NanoIds;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit
{
    public abstract class History : IModel
    {
        public required NanoId Id { get; set; }
        public required NanoId ForeignId { get; set; }
        public ChangeState State { get; set; }
        public required string ChangeSet { get; set; }
        public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
        public NanoId ModifiedBy { get; set; }

        public ObjectChanges GetChanges() => ObjectChanges.FromJson(ChangeSet);
    }

    [DbNanoIdPrimaryKey(NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters.L12)]
    public class History<TModel> : History where TModel : class, IModel, IHistory
    {
        public virtual TModel Model { get; set; }
    }

}
