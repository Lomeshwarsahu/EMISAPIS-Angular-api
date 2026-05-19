namespace EMISAPIS.DTOS
{
    public class MainEquipmentTypeDto
    {
        public int Pid { get; set; }
        public string PItemName { get; set; } = string.Empty;
    }

    public class CovidStockRowDto
    {
        public int ExistingItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string MakeSerialNo { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public string ModelNo { get; set; } = string.Empty;
        public string InstallLocation { get; set; } = string.Empty;
        public string? ReceiptDate { get; set; }
        public string? InstallationDate { get; set; }
        public string? WarrantyUpto { get; set; }
        public string SuppliedFrom { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
    }

    public class OpeningStockRowDto
    {
        public int ExistingItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string MakeSerialNo { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public string ModelNo { get; set; } = string.Empty;
        public string InstallLocation { get; set; } = string.Empty;
        public string? ReceiptDate { get; set; }
        public string? InstallationDate { get; set; }
        public string? WarrantyUpto { get; set; }
        public string SuppliedFrom { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
    }
}
