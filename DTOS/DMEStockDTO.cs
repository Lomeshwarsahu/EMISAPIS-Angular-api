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

    public class ProgressMonthYearOptionDto
    {
        public int Id { get; set; }
        public string MonthYear { get; set; } = string.Empty;
    }

    public class ProgressCategoryRowDto
    {
        public string PItemName { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public int ExistingItemId { get; set; }
        public int Location { get; set; }
        public int ItemId { get; set; }
        public string Make { get; set; } = string.Empty;
        public string ModelNo { get; set; } = string.Empty;
        public string? InstallationDate { get; set; }
        public string? WarrantyUpto { get; set; }
        public string MakeSerialNo { get; set; } = string.Empty;
        public string Supplied { get; set; } = string.Empty;
        public string InstallLocation { get; set; } = string.Empty;
        public string SuppliedFrom { get; set; } = string.Empty;
        public int SupId { get; set; }
        public string? ReceiptDate { get; set; }
        public string Week1 { get; set; } = string.Empty;
        public string Week2 { get; set; } = string.Empty;
        public string Week3 { get; set; } = string.Empty;
        public string Week4 { get; set; } = string.Empty;
    }

    public class FacilityReceiptRowDto
    {
        public int PoId { get; set; }
        public string PoNo { get; set; } = string.Empty;
        public string? PoDate { get; set; }
        public int ItemDetailId { get; set; }
        public int ItemId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ModelNo { get; set; } = string.Empty;
        public string MakeNo { get; set; } = string.Empty;
        public string? InstallationDate { get; set; }
        public string InstallLocation { get; set; } = string.Empty;
        public string? WarrantyFrom { get; set; }
        public string? WarrantyTo { get; set; }
        public int ConsigneeId { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal ReceiptQty { get; set; }
        public decimal ReceivedQty { get; set; }
    }

    public class FacilityReceiptBatchDto
    {
        public int IssueId { get; set; }
        public string? TentativeStartDate { get; set; }
        public string ReceiptNo { get; set; } = string.Empty;
        public string SupplyStatus { get; set; } = string.Empty;
        public string? DispatchDate { get; set; }
        public string DispatchNo { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public int PoId { get; set; }
        public int LocationId { get; set; }
        public string ReceivedDate { get; set; } = string.Empty;
        public int ReceiptId { get; set; }
    }

    public class FacilityReceiptTagRequest
    {
        public int ItemDetailId { get; set; }
        public string Tagged { get; set; } = string.Empty;
    }

    public class NodalInfoRowDto
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

    public class NodalInfoSaveRequest
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;
        public string EmailId { get; set; } = string.Empty;
        public string MobileNo { get; set; } = string.Empty;
    }

    public class NodalProgressRowDto
    {
        public string ItemName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public int ExistingItemId { get; set; }
        public int Location { get; set; }
        public int ItemId { get; set; }
        public string Make { get; set; } = string.Empty;
        public string ModelNo { get; set; } = string.Empty;
        public string? InstallationDate { get; set; }
        public string? WarrantyUpto { get; set; }
        public string MakeSerialNo { get; set; } = string.Empty;
        public string Supplied { get; set; } = string.Empty;
        public string InstallLocation { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public string WStatus { get; set; } = string.Empty;
        public string SuppliedFrom { get; set; } = string.Empty;
        public int SupId { get; set; }
        public string? ReceiptDate { get; set; }
    }

    public class NodalProgressSaveRequest
    {
        public int UserId { get; set; }
        public bool IsDme { get; set; }
        public int Week { get; set; }
        public int Month { get; set; }
        public List<NodalProgressItemDto> Items { get; set; } = new();
    }

    public class NodalProgressItemDto
    {
        public int ExistingItemId { get; set; }
        public string Status { get; set; } = "N";
        public string Remark { get; set; } = string.Empty;
    }

    public class NodalOtpRequest
    {
        public int UserId { get; set; }
    }

    public class NodalOtpResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
