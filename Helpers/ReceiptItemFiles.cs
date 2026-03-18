using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EMISAPIS.Helpers
{
public class ReceiptItemFiles
    {
        [BsonId]
        public ObjectId Id { get; set; }

        public int item_detail_id { get; set; }

        public byte[] FileChalan { get; set; }

        public byte[] FileWarrantyCard { get; set; }

        public byte[] FilePhoto { get; set; }

        public byte[] File { get; set; }
    }
}
