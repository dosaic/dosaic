using Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions
{
    public class Batch<T> where T : class
    {
        public IList<T> Add { get; init; } = [];
        public IList<T> Update { get; init; } = [];
        public IList<NanoId> Delete { get; init; } = [];
    }

    public class Batch<TAdd, TUpdate>
        where TAdd : class
        where TUpdate : class
    {
        public IList<TAdd> Add { get; init; } = [];
        public IList<TUpdate> Update { get; init; } = [];
        public IList<NanoId> Delete { get; init; } = [];
    }

}
