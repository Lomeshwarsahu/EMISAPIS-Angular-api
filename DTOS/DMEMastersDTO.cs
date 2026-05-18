namespace EMISAPIS.DTOS
{
    public class EelSuggestionRowDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string MobileNo { get; set; } = string.Empty;
        public string EmailId { get; set; } = string.Empty;
        public string Supplier { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string SuggestionCondition { get; set; } = string.Empty;
        public string EntryDt { get; set; } = string.Empty;
        public string? UploadLetter { get; set; }
        public string? UploadRelevantDoc { get; set; }
        public string? Ext { get; set; }
    }
}
