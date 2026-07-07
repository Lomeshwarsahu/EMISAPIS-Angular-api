using MongoDB.Driver;

namespace EMISAPIS.Helpers
{
    public class MongoService
    {
        private readonly IMongoCollection<ReceiptItemFiles> _collection;

        public MongoService()
        {
            var client = new MongoClient("mongodb://localhost:27017");
            var database = client.GetDatabase("EMS");
            _collection = database.GetCollection<ReceiptItemFiles>("ReceiptItemFiles");
        }

        public async Task<ReceiptItemFiles?> GetFile(int itemId)
        {
            return await _collection
                .Find(x => x.item_detail_id == itemId)
                .FirstOrDefaultAsync();
        }

        public async Task UpsertInstallationFile(int itemDetailId, string fileType, byte[] fileBytes)
        {
            ReceiptItemFiles? existing = await GetFile(itemDetailId);
            if (existing == null)
            {
                existing = new ReceiptItemFiles { item_detail_id = itemDetailId };
            }

            switch (fileType.ToLowerInvariant())
            {
                case "insreport":
                    existing.File = fileBytes;
                    break;
                case "insphoto":
                    existing.FilePhoto = fileBytes;
                    break;
                case "waranty":
                    existing.FileWarrantyCard = fileBytes;
                    break;
                case "chalan":
                    existing.FileChalan = fileBytes;
                    break;
                default:
                    throw new InvalidOperationException("Invalid file type.");
            }

            await _collection.ReplaceOneAsync(
                x => x.item_detail_id == itemDetailId,
                existing,
                new ReplaceOptions { IsUpsert = true });
        }
    }
}
