namespace EMISAPIS.DTOS
{
    public class FileMovementRequestDTO
    {
        public int UserId { get; set; }
        public int ToUserId { get; set; }
        public int PonoId { get; set; }
        public string FileId { get; set; }
        public string Remarks { get; set; }
        public DateTime ForwardDate { get; set; }
        public string Flag { get; set; }
    }
}
