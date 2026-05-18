namespace EMISAPIS.DTOS
{
    public class DmeFacHeadDto
    {
        public int HeadId { get; set; }
        public string HeadNo { get; set; } = string.Empty;
        public string HeadName { get; set; } = string.Empty;
    }

    public class CreateDmeFacHeadRequest
    {
        public string HeadNo { get; set; } = string.Empty;
        public string HeadName { get; set; } = string.Empty;
        public int UserId { get; set; }
    }

    public class FacilityIndentRowDto
    {
        public int IndentId { get; set; }
        public string McName { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string ConsolidatedDate { get; set; } = string.Empty;
        public string AsLetterNo { get; set; } = string.Empty;
        public string AsDate { get; set; } = string.Empty;
        public string DispatchNo { get; set; } = string.Empty;
        public string DispatchDate { get; set; } = string.Empty;
        public int NosIndentQty { get; set; }
        public string EStatus { get; set; } = string.Empty;
        public string UploadStatus { get; set; } = string.Empty;
        public int FinancialYearId { get; set; }
    }

    public class CreateFacilityIndentRequest
    {
        public int UserId { get; set; }
        public int BudgetId { get; set; }
        public int FinancialYearId { get; set; }
        public string IndentDate { get; set; } = string.Empty;
        public string AsLetterNo { get; set; } = string.Empty;
        public string AsDate { get; set; } = string.Empty;
    }
}
