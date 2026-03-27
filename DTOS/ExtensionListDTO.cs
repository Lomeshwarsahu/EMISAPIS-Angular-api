namespace EMISAPIS.DTOS
{
    public class ExtensionListDTO
    {
      

        public int ExtensionId { get; set; }
        public int PoId { get; set; }
        public string Remark { get; set; }
        public int Days { get; set; }
        public string ExtendedDate { get; set; }
        public string PoEndDate { get; set; }
        public string Path { get; set; }
        public string LetterDate { get; set; }
        public string LetterNo { get; set; }
        public string SysGenApplyDate { get; set; }
        public string Status { get; set; }
        public string Penalty { get; set; }
    }
    public class PoHeaderforextDTO
    {
        public string SupplierName { get; set; }
        public string ItemName { get; set; }
        public string PoNo { get; set; }
        public string PoDate { get; set; }
        public string SupplyDays { get; set; }
        public string PoEndDate { get; set; }
    }
    public class CreateExtensionDTO
    {
        public int PoId { get; set; }
        public string Remark { get; set; }
        public int Days { get; set; }
        public DateTime ExtendedDate { get; set; }
        public DateTime PoEndDate { get; set; }
        public DateTime LetterDate { get; set; }
        public string IsPenalty { get; set; } // Y / N

        public IFormFile File { get; set; }
    }
    public class FileUploadDto
    {
        public int ExtensionId { get; set; }
        public IFormFile File { get; set; }
    }
  
}
