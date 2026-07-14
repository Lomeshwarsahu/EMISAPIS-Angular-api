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

    public class FacilityIndentHeaderDto
    {
        public int IndentId { get; set; }
        public int UserId { get; set; }
        public string McName { get; set; } = string.Empty;
        public string BudgetName { get; set; } = string.Empty;
        public string IndentDate { get; set; } = string.Empty;
        public int FinancialYearId { get; set; }
        public string Year { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public string AsLetterNo { get; set; } = string.Empty;
        public string AsDate { get; set; } = string.Empty;
        public string DispatchNo { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
    }

    public class FacilityIndentEquipmentDto
    {
        public int ItemId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ItemNameDisplay { get; set; } = string.Empty;
        public decimal ApproxRate { get; set; }
    }

    public class FacilityIndentDeptRowDto
    {
        public int LocationId { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public decimal CurrentStock { get; set; }
        public decimal Pipeline { get; set; }
        public decimal ExistingIndentQty { get; set; }
        public decimal ApproxRate { get; set; }
    }

    public class FacilityIndentItemRowDto
    {
        public int IndentItemId { get; set; }
        public int LocationId { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public int ItemId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public decimal EstimatedCost { get; set; }
        public decimal IndentQuantity { get; set; }
        public decimal Value { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal Pipeline { get; set; }
        public string Remarks { get; set; } = string.Empty;
        public string RcStatus { get; set; } = string.Empty;
    }

    public class AddFacilityIndentItemRequest
    {
        public int UserId { get; set; }
        public int ItemId { get; set; }
        public int LocationId { get; set; }
        public decimal FacilityIndQty { get; set; }
        public decimal ApproxRate { get; set; }
        public string Remarks { get; set; } = string.Empty;
    }

    public class DeleteFacilityIndentItemsRequest
    {
        public int UserId { get; set; }
        public List<int> IndentItemIds { get; set; } = new();
    }

    public class FacilityIndentReportDto
    {
        public FacilityIndentHeaderDto Header { get; set; } = new();
        public List<FacilityIndentReportLineDto> Lines { get; set; } = new();
    }

    public class FacilityIndentReportLineDto
    {
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string LocationName { get; set; } = string.Empty;
        public decimal IndentQuantity { get; set; }
        public decimal EstimatedCost { get; set; }
        public decimal Value { get; set; }
        public string RcStatus { get; set; } = string.Empty;
        public string Supplier { get; set; } = string.Empty;
        public string PriceIncGst { get; set; } = string.Empty;
        public string TenderNo { get; set; } = string.Empty;
        public string TenderStatus { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
    }
}
