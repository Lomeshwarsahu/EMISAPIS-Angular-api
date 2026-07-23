using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EMISAPIS.Helpers
{
    public class EelSubmissionFiles
    {
        [BsonId]
        public ObjectId Id { get; set; }

        public int EELID { get; set; }

        public string? FILENAMEEEL1 { get; set; }

        public byte[]? FileDateEEL1 { get; set; }

        public string? FILENAMEEEL2 { get; set; }

        public byte[]? FileDateEEL2 { get; set; }

        public string? ext { get; set; }
    }
}
