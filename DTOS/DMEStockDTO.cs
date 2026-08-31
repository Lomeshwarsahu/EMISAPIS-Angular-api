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
        public int Pid { get; set; }
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

    public class EquipmentItemOptionDto
    {
        public int ItemId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public int Pid { get; set; }
    }

    public class SupplySourceOptionDto
    {
        public int SupId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class WardOptionDto
    {
        public int WardId { get; set; }
        public string WardName { get; set; } = string.Empty;
    }

    public class OpeningStockDetailDto
    {
        public int ExistingItemId { get; set; }
        public int Pid { get; set; }
        public int ItemId { get; set; }
        public int SupId { get; set; }
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string SerialNo { get; set; } = string.Empty;
        public int? Qty { get; set; }
        public string? ReceiptDate { get; set; }
        public string? InstallationDate { get; set; }
        public int? WarrantyYear { get; set; }
        public string? WarrantyUpto { get; set; }
        public int WardId { get; set; }
        public string InstallLocationOther { get; set; } = string.Empty;
        public string AmcFlag { get; set; } = "N";
        public string? AmcValidDate { get; set; }
        public string AmcFirm { get; set; } = string.Empty;
        public string WorkingStatus { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public bool IsBulkEntry { get; set; }
    }

    public class OpeningStockSaveDto
    {
        public int UserId { get; set; }
        public int? ExistingItemId { get; set; }
        public int Pid { get; set; }
        public int ItemId { get; set; }
        public int SupId { get; set; }
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string SerialNo { get; set; } = string.Empty;
        public int? Qty { get; set; }
        public string? ReceiptDate { get; set; }
        public string? InstallationDate { get; set; }
        public int? WarrantyYear { get; set; }
        public string? WarrantyUpto { get; set; }
        public int WardId { get; set; }
        public string InstallLocationOther { get; set; } = string.Empty;
        public string AmcFlag { get; set; } = "N";
        public string? AmcValidDate { get; set; }
        public string AmcFirm { get; set; } = string.Empty;
        public string WorkingStatus { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
    }

    public class NodalInformationDto
    {
        public int LocationId { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string FacilityTypeName { get; set; } = string.Empty;
        public string DistrictName { get; set; } = string.Empty;
        public int DistrictId { get; set; }
        public int FacilityTypeId { get; set; }
        public int UserId { get; set; }
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;
        public string MobileNo { get; set; } = string.Empty;
        public string EmailId { get; set; } = string.Empty;
    }

    public class NodalInformationSaveDto
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;
        public string EmailId { get; set; } = string.Empty;
        public string MobileNo { get; set; } = string.Empty;
    }
}
