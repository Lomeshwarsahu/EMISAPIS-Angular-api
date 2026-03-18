using EMISAPIS.Helpers;
using MongoDB.Driver;

namespace EMISAPIS.Helpers
{
    //public class MongoService
    //{
    //}

    //}


    public class MongoService
    {
        private readonly IMongoCollection<ReceiptItemFiles> _collection;

        public MongoService()
        {
            var client = new MongoClient("mongodb://localhost:27017");
            var database = client.GetDatabase("EMS");

            _collection = database.GetCollection<ReceiptItemFiles>("ReceiptItemFiles");
        }

        public async Task<ReceiptItemFiles> GetFile(int itemId)
        {
            return await _collection
                .Find(x => x.item_detail_id == itemId)
                .FirstOrDefaultAsync();
        }
    }
}