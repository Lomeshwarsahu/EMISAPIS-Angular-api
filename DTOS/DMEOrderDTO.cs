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
