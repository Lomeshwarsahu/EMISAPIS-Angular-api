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

    public class MasFileNoSearchResultDto
    {
        public int PoId { get; set; }
        public string PoNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public string SchemeCode { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string FileNo { get; set; } = string.Empty;
        public string FileDt { get; set; } = string.Empty;
    }

    public class MasFileNoRowDto
    {
        public int PoId { get; set; }
        public string PoNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public string SchemeCode { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string FileNo { get; set; } = string.Empty;
        public string FileDt { get; set; } = string.Empty;
    }

    public class MasFileNoSaveRequest
    {
        public int PoId { get; set; }
        public string FileNo { get; set; } = string.Empty;
        public string FileDt { get; set; } = string.Empty;
    }

    public class PoReallocationHeaderDto
    {
        public int PoId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string PoNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public decimal TotalPoQty { get; set; }
        public string DirectorateName { get; set; } = string.Empty;
        public int DirectorateId { get; set; }
    }

    public class PoReallocationRowDto
    {
        public int PoId { get; set; }
        public int PoItemId { get; set; }
        public int ConsigneeId { get; set; }
        public int IndentItemId { get; set; }
        public int IndentId { get; set; }
        public int IndentConsolidationId { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public int IssueId { get; set; }
        public string DLocation { get; set; } = string.Empty;
        public int ReceiptId { get; set; }
        public string RLocation { get; set; } = string.Empty;
        public bool CanReallocate { get; set; }
    }

    public class DistrictOptionDto
    {
        public int DistrictId { get; set; }
        public string DistrictName { get; set; } = string.Empty;
    }

    public class LocationOptionDto
    {
        public int LocationId { get; set; }
        public string LocationName { get; set; } = string.Empty;
    }

    public class PoReallocationHistoryRowDto
    {
        public string PoNo { get; set; } = string.Empty;
        public string OldLocation { get; set; } = string.Empty;
        public string NewLocation { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
        public string EntryDate { get; set; } = string.Empty;
        public int ExtensionId { get; set; }
        public string Path { get; set; } = string.Empty;
        public string Ext { get; set; } = string.Empty;
    }

    public class PoReallocationSaveItemDto
    {
        public int PoId { get; set; }
        public int PoItemId { get; set; }
        public int ConsigneeId { get; set; }
        public int IndentItemId { get; set; }
        public int IndentId { get; set; }
        public int IndentConsolidationId { get; set; }
        public int IssueId { get; set; }
        public int NewLocationId { get; set; }
    }

    public class PoReallocationSaveRequest
    {
        public int PoId { get; set; }
        public string Remark { get; set; } = string.Empty;
        public List<PoReallocationSaveItemDto> Items { get; set; } = new();
    }

    public class PoAmendmentHeaderDto
    {
        public int PoId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string SchemeName { get; set; } = string.Empty;
        public string AccYear { get; set; } = string.Empty;
        public string PoNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public string SoIssueDt { get; set; } = string.Empty;
        public string OutwardNo { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public decimal TotalPoQty { get; set; }
        public decimal PoValue { get; set; }
        public decimal BasicRate { get; set; }
        public decimal GstPercent { get; set; }
        public decimal FinalRate { get; set; }
    }

    public class PoAmendmentTypeDto
    {
        public int AmendId { get; set; }
        public string AmenmentType { get; set; } = string.Empty;
    }

    public class PoAmendmentHistoryRowDto
    {
        public int PoAmmdId { get; set; }
        public string PoNo { get; set; } = string.Empty;
        public string AmendDate { get; set; } = string.Empty;
        public string NastiLetterNo { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Ext { get; set; } = string.Empty;
    }

    public class PoAmendmentSaveRequest
    {
        public int PoId { get; set; }
        public string DispatchNo { get; set; } = string.Empty;
        public string AmendDate { get; set; } = string.Empty;
        public string PrevSoIssueDt { get; set; } = string.Empty;
        public string PrevSoIssueNo { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public int AmendTypeId { get; set; }
        public string IsReprintReq { get; set; } = "N";
    }

    public class WithheldReleaseRowDto
    {
        public string SupplierName { get; set; } = string.Empty;
        public string SanctionDate { get; set; } = string.Empty;
        public decimal GrossAmt { get; set; }
        public decimal ChequeAmt { get; set; }
        public string AidNo { get; set; } = string.Empty;
        public string ChequeDate { get; set; } = string.Empty;
        public decimal Witheld20 { get; set; }
        public int WithledQty { get; set; }
        public string PoType { get; set; } = string.Empty;
        public int BudgetId { get; set; }
        public string BudgetName { get; set; } = string.Empty;
        public int SanctionId { get; set; }
        public int PoId { get; set; }
        public int SupplierId { get; set; }
        public int PaymentId { get; set; }
        public string AidDate { get; set; } = string.Empty;
        public decimal ReleaseAmt { get; set; }
        public string PoNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
    }

    public class WithheldBankOptionDto
    {
        public int BankAccountId { get; set; }
        public string AccountNo { get; set; } = string.Empty;
    }

    public class WithheldBankDetailDto
    {
        public string AccountName { get; set; } = string.Empty;
        public string IfscCode { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
    }

    public class WithheldSelectionResultDto
    {
        public bool Valid { get; set; }
        public string Message { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public decimal PaidAmount { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public string IfscCode { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public int PaymentId { get; set; }
    }

    public class WithheldReleaseSaveRequest
    {
        public int PayMode { get; set; }
        public string PayDocumentNo { get; set; } = string.Empty;
        public string PayDocumentDate { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public int SupplierBankAccountId { get; set; }
        public int CgmscBankAccountId { get; set; }
        public int SupplierId { get; set; }
        public decimal AmountPaid { get; set; }
        public List<int> SanctionIds { get; set; } = new();
    }

    public class WithheldReleaseCompleteRequest
    {
        public int PayMode { get; set; }
        public string PayDocumentNo { get; set; } = string.Empty;
        public string PayDocumentDate { get; set; } = string.Empty;
        public decimal AmountPaid { get; set; }
        public string PaidOn { get; set; } = string.Empty;
    }

    public class PaymentLetterBankDto
    {
        public string Bankname { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string Accountno { get; set; } = string.Empty;
        public string Accountname { get; set; } = string.Empty;
        public string Ifsccode { get; set; } = string.Empty;
        public string Aidno { get; set; } = string.Empty;
        public string Aiddate { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
    }

    public class PaymentLetterRowDto
    {
        public int Bankaccountid { get; set; }
        public string Accountno { get; set; } = string.Empty;
        public string Accountname { get; set; } = string.Empty;
        public string Bankname { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string Ifsccode { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public decimal AmountPaid { get; set; }
        public int PaymentId { get; set; }
    }

    public class PaymentLetterDataDto
    {
        public List<PaymentLetterRowDto> BankLetter { get; set; } = new();
        public List<PaymentLetterBankDto> BankInfo { get; set; } = new();
        public string Words { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string AidNo { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
    }

    public class MainEquipmentMappedReportRowDto
    {
        public string PItemName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string IsElectrical { get; set; } = string.Empty;
        public string ProgReq { get; set; } = string.Empty;
        public string SrOrBulkEntry { get; set; } = string.Empty;
        public string AmcReq { get; set; } = string.Empty;
        public int ItemId { get; set; }
    }
}
