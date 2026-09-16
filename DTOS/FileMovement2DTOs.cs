namespace EMISAPIS.DTOS
{
    // PODetailsRDLC
    public class PoDetailItemDto
    {
        public string SoissueDate { get; set; } = string.Empty;
        public int ItemId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public decimal PercentValue { get; set; }
        public decimal BasicRate { get; set; }
        public string SchemeName { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public string AccYear { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string PoNo { get; set; } = string.Empty;
        public decimal FinalRate { get; set; }
        public decimal PoQty { get; set; }
        public decimal PoValue { get; set; }
        public int PoId { get; set; }
        public string TrancheDays { get; set; } = string.Empty;
        public string PenaltyType { get; set; } = string.Empty;
        public decimal PenaltyPercent { get; set; }
        public string PoType { get; set; } = string.Empty;
        public string OutwardNo { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public decimal ReceivedQty { get; set; }
        public decimal InstalledQty { get; set; }
        public decimal BalanceQty { get; set; }
        public decimal PendingInstall { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }

    public class PoDetailReceiptDto
    {
        public string MachineSrno { get; set; } = string.Empty;
        public string LocationName { get; set; } = string.Empty;
        public string InvoiceNo { get; set; } = string.Empty;
        public string InvoiceDate { get; set; } = string.Empty;
        public decimal OrderedQty { get; set; }
        public decimal InvoiceAbsQty { get; set; }
        public decimal Gst { get; set; }
        public decimal BasicRate { get; set; }
        public decimal Sup { get; set; }
        public string RecievedDate { get; set; } = string.Empty;
        public int Daystaken { get; set; }
        public string InstallationDate { get; set; } = string.Empty;
        public string Logo { get; set; } = string.Empty;
    }

    // PendingInstallDrillDown
    public class PendingInstallDto
    {
        public string DistrictName { get; set; } = string.Empty;
        public string LocationName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Supplier { get; set; } = string.Empty;
        public string PoNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal DispatchedQty { get; set; }
        public decimal ReceiptQty { get; set; }
        public decimal InstalledQty { get; set; }
        public string Remarks { get; set; } = string.Empty;
    }
}
