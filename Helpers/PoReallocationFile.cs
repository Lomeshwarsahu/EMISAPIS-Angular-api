using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EMISAPIS.Helpers
{
    public class PoReallocationFile
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("extensionId")]
        public int ExtensionId { get; set; }

        [BsonElement("EXTFileName")]
        public string ExtFileName { get; set; } = string.Empty;

        [BsonElement("ExtFile")]
        public byte[] ExtFile { get; set; } = Array.Empty<byte>();

        [BsonElement("ext")]
        public string Ext { get; set; } = string.Empty;
    }

    public class PoAmendmentFile
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("BGID")]
        public int PoAmmdId { get; set; }

        [BsonElement("FILENAME")]
        public string FileName { get; set; } = string.Empty;

        [BsonElement("FILEPATH")]
        public byte[] FilePath { get; set; } = Array.Empty<byte>();

        [BsonElement("ext")]
        public string Ext { get; set; } = string.Empty;
    }
}
