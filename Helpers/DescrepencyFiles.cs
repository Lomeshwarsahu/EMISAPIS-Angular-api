using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EMISAPIS.Helpers
{
    public class DescrepencyFiles
    {
        [BsonId]
        public ObjectId Id { get; set; }

        public int decrepencyid { get; set; }

        public string? DeniedLetter { get; set; }

        public byte[]? DeniedFile { get; set; }

        public string? ReceivedCopy { get; set; }

        public byte[]? ReceivedCopyFile { get; set; }

        public string? ext { get; set; }
    }
}
