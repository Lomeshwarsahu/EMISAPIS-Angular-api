using MongoDB.Driver;

namespace EMISAPIS.Helpers
{
    public class MongoService
    {
        private readonly IMongoCollection<ReceiptItemFiles> _collection;
        private readonly IMongoCollection<DescrepencyFiles> _descrepencyCollection;
        private readonly IMongoCollection<EelSubmissionFiles> _eelSubmissionCollection;

        public MongoService()
        {
            var client = new MongoClient("mongodb://localhost:27017");
            var database = client.GetDatabase("EMS");
            _collection = database.GetCollection<ReceiptItemFiles>("ReceiptItemFiles");
            _descrepencyCollection = database.GetCollection<DescrepencyFiles>("DescrepencyFiles");
            _eelSubmissionCollection = database.GetCollection<EelSubmissionFiles>("masEELSubmission");
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

        public async Task<DescrepencyFiles?> GetDescrepencyFile(int descrepencyId)
        {
            return await _descrepencyCollection
                .Find(x => x.decrepencyid == descrepencyId)
                .FirstOrDefaultAsync();
        }

        public async Task UpsertDescrepencyFile(
            int descrepencyId,
            string fileKind,
            string fileToken,
            byte[] fileBytes,
            string ext = ".pdf")
        {
            DescrepencyFiles? existing = await GetDescrepencyFile(descrepencyId);
            if (existing == null)
            {
                existing = new DescrepencyFiles
                {
                    decrepencyid = descrepencyId,
                    ext = ext,
                };
            }

            string kind = fileKind.Trim().ToLowerInvariant();
            if (kind is "denied" or "deniedletter")
            {
                existing.DeniedLetter = fileToken;
                existing.DeniedFile = fileBytes;
            }
            else if (kind is "received" or "receivedcopy")
            {
                existing.ReceivedCopy = fileToken;
                existing.ReceivedCopyFile = fileBytes;
            }
            else
            {
                throw new InvalidOperationException("Invalid discrepancy file kind.");
            }

            existing.ext = string.IsNullOrWhiteSpace(ext) ? ".pdf" : ext;
            await _descrepencyCollection.ReplaceOneAsync(
                x => x.decrepencyid == descrepencyId,
                existing,
                new ReplaceOptions { IsUpsert = true });
        }

        public async Task<EelSubmissionFiles?> GetEelSubmission(int eelId)
        {
            return await _eelSubmissionCollection
                .Find(x => x.EELID == eelId)
                .FirstOrDefaultAsync();
        }
    }
}
