using MongoDB.Driver;

namespace Dosaic.Plugins.Persistence.MongoDb
{
    public interface IMongoDbInstance
    {
        MongoClient Client { get; }
        IMongoCollection<T> GetCollectionFor<T>();
    }
}
