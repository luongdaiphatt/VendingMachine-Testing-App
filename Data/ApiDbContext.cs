using MongoDB.Driver;

namespace VendingMachineTest.Data
{
    public class ApiDbContext
    {
        private readonly IMongoDatabase _database;
        public ApiDbContext(string connectionString = "mongodb://localhost:27017", string dbName = "chatDB")
        {
            var client = new MongoClient(connectionString);
            _database = client.GetDatabase(dbName);
        }
        public IMongoCollection<T> GetCollection<T>(string collectionName)
        {
            return _database.GetCollection<T>(collectionName);
        }
    }
}
