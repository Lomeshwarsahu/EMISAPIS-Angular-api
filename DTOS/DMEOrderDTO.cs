namespace EMISAPIS.DTOS
{
    public class FinancialYearOptionDto
    {
        public int FinancialYearId { get; set; }
        public string Year { get; set; } = string.Empty;
    }

    public class PoEquipmentOptionDto
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemCodeAsPerTender { get; set; } = string.Empty;
    }

    public class PoReceiptDeskRowDto
    {
        public int PoItemId { get; set; }
        public int PoId { get; set; }
        public int ConsigneeId { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string PoNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal? TotalPrice { get; set; }
        public List<PoReceiptBatchDto> Batches { get; set; } = new();
    }

    public class PoReceiptBatchDto
    {
        public string IssueId { get; set; } = string.Empty;
        public string DispatchNo { get; set; } = string.Empty;
        public string DispatchDate { get; set; } = string.Empty;
        public string TentativeSupplyDate { get; set; } = string.Empty;
        public string ReceiptNo { get; set; } = string.Empty;
        public string ReceiptDate { get; set; } = string.Empty;
        public decimal SuppliedQty { get; set; }
        public string SupplyStatus { get; set; } = string.Empty;
        public int? ReceiptId { get; set; }
        public int PoId { get; set; }
        public int LocationId { get; set; }
    }

    public class PoDashboardRowDto
    {
        public int PoId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string IndentDt { get; set; } = string.Empty;
        public string IndentYear { get; set; } = string.Empty;
        public decimal IndentQuantity { get; set; }
        public string PoNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public string PoYear { get; set; } = string.Empty;
        public decimal PoQty { get; set; }
        public string TenderNo { get; set; } = string.Empty;
        public string TenderDate { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string? FilePathReagent { get; set; }
        public string? FilePathAccessories { get; set; }
    }
}
