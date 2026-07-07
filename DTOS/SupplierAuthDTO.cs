namespace EMISAPIS.DTOS
{
    public class SupplierProfileDto
    {
        public int SupplierId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string MaskedMobile { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
    }

    public class SupplierOtpRequestDto
    {
        public int SupplierId { get; set; }
    }

    public class SupplierPasswordRequestDto
    {
        public int SupplierId { get; set; }
        public string Otp { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string RepeatPassword { get; set; } = string.Empty;
        /// <summary>new = create user, reset = forgot password</summary>
        public string Mode { get; set; } = "reset";
        public string DesiredUserId { get; set; } = string.Empty;
    }

    /// <summary>ParticularSupplierAdd.aspx supplier profile form.</summary>
    public class ParticularSupplierDetailsDto
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string ContactPersonName { get; set; } = string.Empty;
        public string MobileNo { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string GstNo { get; set; } = string.Empty;
        public string GstNo2 { get; set; } = string.Empty;
        public string GstNo3 { get; set; } = string.Empty;
        public string PhoneNo { get; set; } = string.Empty;
        public string TinNo { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    public class ParticularSupplierUpdateDto
    {
        public int SupplierId { get; set; }
        public string MobileNo { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string GstNo { get; set; } = string.Empty;
        public string GstNo2 { get; set; } = string.Empty;
        public string GstNo3 { get; set; } = string.Empty;
        public string PhoneNo { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    /// <summary>SupplierGSTentry1.aspx GST grid row.</summary>
    public class SupplierGstEntryDto
    {
        public int GstId { get; set; }
        public string GstNo { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string Flag { get; set; } = "Y";
    }

    public class SupplierGstPageDto
    {
        public int SupplierId { get; set; }
        public string SupplierCode { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public List<SupplierGstEntryDto> Entries { get; set; } = new();
    }

    public class SupplierGstSaveDto
    {
        public int UserId { get; set; }
        public int SupplierId { get; set; }
        public string GstNo { get; set; } = string.Empty;
    }

    /// <summary>po_supply.aspx — purchase order desk row.</summary>
    public class SupplierPoSupplyRowDto
    {
        public int PoId { get; set; }
        public int ItemId { get; set; }
        public string OutwardNo { get; set; } = string.Empty;
        public string PoNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public decimal BasicRate { get; set; }
        public decimal Percentage { get; set; }
        public decimal Quantity { get; set; }
        public decimal TotalPoValue { get; set; }
        public string TenderNo { get; set; } = string.Empty;
        public int NoOfConsignee { get; set; }
        public string Status { get; set; } = string.Empty;
        public string SdName { get; set; } = string.Empty;
        public string SubmissionStatus { get; set; } = string.Empty;
    }

    public class SupplierTenderOptionDto
    {
        public int TenderId { get; set; }
        public string TenderNo { get; set; } = string.Empty;
    }

    public class SupplierPoSupplyFiltersDto
    {
        public int SupplierId { get; set; }
        public int CurrentFinancialYearId { get; set; }
        public List<FinancialYearOptionDto> FinancialYears { get; set; } = new();
        public List<SupplierTenderOptionDto> Tenders { get; set; } = new();
    }

    /// <summary>po_supplyDispatch.aspx — dispatch desk row.</summary>
    public class SupplierPoDispatchRowDto
    {
        public int PoId { get; set; }
        public string OutwardNo { get; set; } = string.Empty;
        public string PoNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public decimal BasicRate { get; set; }
        public decimal Percentage { get; set; }
        public int NoOfConsignee { get; set; }
        public decimal Quantity { get; set; }
        public decimal TotalPoValue { get; set; }
        public decimal DispatchedQty { get; set; }
        public string SupplyStatus { get; set; } = string.Empty;
    }

    /// <summary>po_supply_edit.aspx — dispatch equipment desk header + consignee rows.</summary>
    public class SupplierPoDispatchEditDto
    {
        public int PoId { get; set; }
        public string PoNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public List<SupplierPoDispatchEditRowDto> Rows { get; set; } = new();
    }

    public class SupplierPoDispatchEditRowDto
    {
        public int PoItemId { get; set; }
        public int PoId { get; set; }
        public int ItemId { get; set; }
        public int ConsigneeId { get; set; }
        public int CategoryId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string LocationName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal Quantity { get; set; }
        public decimal TotalPrice { get; set; }
        public bool CanAddDispatch { get; set; }
        public List<SupplierPoDispatchEditBatchDto> Batches { get; set; } = new();
    }

    public class SupplierPoDispatchEditBatchDto
    {
        public int IssueId { get; set; }
        public int PoId { get; set; }
        public int LocationId { get; set; }
        public int CategoryId { get; set; }
        public string DispatchNo { get; set; } = string.Empty;
        public string DispatchDate { get; set; } = string.Empty;
        public string TentativeSupplyDate { get; set; } = string.Empty;
        public string ReceivedDate { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string SupplyStatus { get; set; } = string.Empty;
    }

    /// <summary>rptDispatchDetails.aspx — printable dispatch report.</summary>
    public class SupplierDispatchReportDto
    {
        public int PoId { get; set; }
        public int LocationId { get; set; }
        public int IssueId { get; set; }
        public string DispatchNo { get; set; } = string.Empty;
        public string DispatchDate { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string PoNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public string TenderNo { get; set; } = string.Empty;
        public string ConsigneeName { get; set; } = string.Empty;
        public string ModelNo { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public decimal BasicRate { get; set; }
        public decimal TotalNetPoValue { get; set; }
        public decimal TotalGrossPoValue { get; set; }
        public decimal PoQtyForConsignee { get; set; }
        public string SupplyDays { get; set; } = string.Empty;
        public decimal TaxPercent { get; set; }
        public string ChallanNo { get; set; } = string.Empty;
        public string ChallanDate { get; set; } = string.Empty;
        public string InvoiceNo { get; set; } = string.Empty;
        public string InvoiceDate { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
    }

    /// <summary>po_supply_details.aspx — equipment dispatch entry page.</summary>
    public class SupplierDispatchEntryPageDto
    {
        public int PoId { get; set; }
        public int LocationId { get; set; }
        public int ItemId { get; set; }
        public int IssueId { get; set; }
        public int SupplierId { get; set; }
        public int CategoryId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string TaxPercent { get; set; } = string.Empty;
        public string PoNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public string TenderNo { get; set; } = string.Empty;
        public string ConsigneeName { get; set; } = string.Empty;
        public string ModelNo { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public decimal BasicRate { get; set; }
        public decimal TotalNetPoValue { get; set; }
        public decimal TotalGrossPoValue { get; set; }
        public decimal PoQtyAllConsignees { get; set; }
        public decimal PoQtyConsignee { get; set; }
        public decimal DispatchedQty { get; set; }
        public decimal BalanceQty { get; set; }
        public string SupplyDays { get; set; } = string.Empty;
        public bool HasInvoice { get; set; }
        public bool HasInvoiceFile { get; set; }
        public string ChallanNo { get; set; } = string.Empty;
        public string ChallanDate { get; set; } = string.Empty;
        public string InvoiceNo { get; set; } = string.Empty;
        public string InvoiceDate { get; set; } = string.Empty;
        public string EwayBillNo { get; set; } = string.Empty;
        public string EwayBillDate { get; set; } = string.Empty;
        public string HsnCode { get; set; } = string.Empty;
        public string TcsValue { get; set; } = string.Empty;
        public string InvoiceGst { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public string BulkVsSerial { get; set; } = string.Empty;
        public string DispatchNo { get; set; } = string.Empty;
        public string DispatchDate { get; set; } = string.Empty;
        public string TentativeSupplyDate { get; set; } = string.Empty;
        public string CgmscLogoPrinted { get; set; } = "N";
        public string WarrantyValidity { get; set; } = "N";
        public string ServiceManual { get; set; } = "N";
        public string OperatingManual { get; set; } = "N";
        public string CalibrationCertificate { get; set; } = "N";
        public string WarrantyCard { get; set; } = "N";
        public string OtherStatutory { get; set; } = "N";
        public string PoDocuments { get; set; } = "N";
        public List<SupplierGstOptionDto> GstOptions { get; set; } = new();
        public List<SupplierDispatchEquipmentLineDto> EquipmentLines { get; set; } = new();
    }

    public class SupplierGstOptionDto
    {
        public int GstId { get; set; }
        public string GstNo { get; set; } = string.Empty;
    }

    public class SupplierDispatchEquipmentLineDto
    {
        public int IssueDetailId { get; set; }
        public string SerialNo { get; set; } = string.Empty;
        public string WarrantyCardNo { get; set; } = string.Empty;
        public decimal SupplyQty { get; set; }
    }

    public class SupplierDispatchEquipmentLineRequestDto
    {
        public int IssueId { get; set; }
        public int IssueDetailId { get; set; }
        public string SerialNo { get; set; } = string.Empty;
        public string WarrantyCardNo { get; set; } = string.Empty;
        public decimal SupplyQty { get; set; }
    }

    public class SupplierDispatchCompleteRequestDto
    {
        public int PoId { get; set; }
        public int LocationId { get; set; }
        public int IssueId { get; set; }
        public string DispatchNo { get; set; } = string.Empty;
        public string DispatchDate { get; set; } = string.Empty;
        public string TentativeSupplyDate { get; set; } = string.Empty;
        public string CgmscLogoPrinted { get; set; } = "N";
        public string WarrantyValidity { get; set; } = "N";
        public string ServiceManual { get; set; } = "N";
        public string OperatingManual { get; set; } = "N";
        public string CalibrationCertificate { get; set; } = "N";
        public string WarrantyCard { get; set; } = "N";
        public string OtherStatutory { get; set; } = "N";
        public string PoDocuments { get; set; } = "N";
    }

    /// <summary>Facilitypo_supply_ReceiptSUP.aspx — PO dropdown option.</summary>
    public class SupplierPoReceiptOptionDto
    {
        public int PoId { get; set; }
        public string DisplayText { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class SupplierPoReceiptFiltersDto
    {
        public int SupplierId { get; set; }
        public List<FinancialYearOptionDto> FinancialYears { get; set; } = new();
    }

    public class SupplierPoReceiptBatchDto
    {
        public int IssueId { get; set; }
        public int PoId { get; set; }
        public int LocationId { get; set; }
        public int? ReceiptId { get; set; }
        public string DispatchDate { get; set; } = string.Empty;
        public string ReceivedDate { get; set; } = string.Empty;
        public string SupplyStatus { get; set; } = string.Empty;
    }

    public class SupplierPoReceiptRowDto
    {
        public int PoItemId { get; set; }
        public int PoId { get; set; }
        public string PoNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public string RowStatus { get; set; } = string.Empty;
        public int ConsigneeId { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal SupplyQty { get; set; }
        public decimal ReceiptQty { get; set; }
        public decimal InstQty { get; set; }
        public decimal DeniedQty { get; set; }
        public string DeniedStatus { get; set; } = string.Empty;
        public List<SupplierPoReceiptBatchDto> Batches { get; set; } = new();
    }

    /// <summary>FacilityPO_Receipt1_SUP.aspx — equipment receipt entry page.</summary>
    public class SupplierReceiptEntryPageDto
    {
        public int PoId { get; set; }
        public int LocationId { get; set; }
        public int IssueId { get; set; }
        public int ReceiptId { get; set; }
        public int CategoryId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string TaxPercent { get; set; } = string.Empty;
        public string PoNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public string TenderNo { get; set; } = string.Empty;
        public string ConsigneeName { get; set; } = string.Empty;
        public string ModelNo { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public decimal BasicRate { get; set; }
        public decimal TotalNetPoValue { get; set; }
        public decimal TotalGrossPoValue { get; set; }
        public decimal PoQtyAllConsignees { get; set; }
        public decimal PoQtyConsignee { get; set; }
        public decimal DispatchedQty { get; set; }
        public decimal BalanceQty { get; set; }
        public string SupplyDays { get; set; } = string.Empty;
        public int WarrantyYears { get; set; }
        public string LastReceiptDate { get; set; } = string.Empty;
        public string ChallanNo { get; set; } = string.Empty;
        public string ChallanDate { get; set; } = string.Empty;
        public string InvoiceNo { get; set; } = string.Empty;
        public string InvoiceDate { get; set; } = string.Empty;
        public string DispatchNo { get; set; } = string.Empty;
        public string DispatchDate { get; set; } = string.Empty;
        public string SupplierRemarks { get; set; } = string.Empty;
        public string ReceivedDate { get; set; } = string.Empty;
        public string ReceiptNo { get; set; } = string.Empty;
        public string ReceiptQty { get; set; } = string.Empty;
        public string ReceiptRemarks { get; set; } = string.Empty;
        public List<SupplierReceiptIssueDetailOptionDto> IssueDetailOptions { get; set; } = new();
        public List<SupplierReceiptInstallationLineDto> InstallationLines { get; set; } = new();
    }

    public class SupplierReceiptIssueDetailOptionDto
    {
        public int IssueDetailId { get; set; }
        public string SerialNo { get; set; } = string.Empty;
        public string WarrantyCertificateNo { get; set; } = string.Empty;
        public decimal DispatchedQty { get; set; }
    }

    public class SupplierReceiptInstallationLineDto
    {
        public int ItemDetailId { get; set; }
        public int IssueDetailId { get; set; }
        public string SerialNo { get; set; } = string.Empty;
        public string WarrantyCertificateNo { get; set; } = string.Empty;
        public string WarrantyCardNo { get; set; } = string.Empty;
        public decimal ReceivedQty { get; set; }
        public string InstallationDate { get; set; } = string.Empty;
        public string WarrantyFromDate { get; set; } = string.Empty;
        public string WarrantyToDate { get; set; } = string.Empty;
        public string InstallationBy { get; set; } = string.Empty;
        public string InstallationLocation { get; set; } = string.Empty;
        public string CgmscLogoPrinted { get; set; } = "N";
        public string WarrantyValidity { get; set; } = "N";
        public string ServiceManual { get; set; } = "N";
        public string OperatingManual { get; set; } = "N";
        public string CalibrationCertificate { get; set; } = "N";
        public string WarrantyCard { get; set; } = "N";
        public string OtherStatutory { get; set; } = "N";
        public string PoDocuments { get; set; } = "N";
    }

    public class SupplierReceiptSaveRequestDto
    {
        public int PoId { get; set; }
        public int LocationId { get; set; }
        public int IssueId { get; set; }
        public string ReceivedDate { get; set; } = string.Empty;
        public string ReceiptNo { get; set; } = string.Empty;
        public string ReceiptQty { get; set; } = string.Empty;
        public string ReceiptRemarks { get; set; } = string.Empty;
    }

    public class SupplierReceiptInstallationSaveRequestDto
    {
        public int ReceiptId { get; set; }
        public int IssueDetailId { get; set; }
        public string WarrantyCardNo { get; set; } = string.Empty;
        public decimal ReceivedQty { get; set; }
        public string InstallationDate { get; set; } = string.Empty;
        public string InstallationBy { get; set; } = string.Empty;
        public string InstallationLocation { get; set; } = string.Empty;
        public string CgmscLogoPrinted { get; set; } = "N";
        public string WarrantyValidity { get; set; } = "N";
        public string ServiceManual { get; set; } = "N";
        public string OperatingManual { get; set; } = "N";
        public string CalibrationCertificate { get; set; } = "N";
        public string WarrantyCard { get; set; } = "N";
        public string OtherStatutory { get; set; } = "N";
        public string PoDocuments { get; set; } = "N";
    }

    public class SupplierReceiptCompleteRequestDto
    {
        public int PoId { get; set; }
        public int LocationId { get; set; }
        public int IssueId { get; set; }
        public int ReceiptId { get; set; }
    }

    /// <summary>RCDetailReportForSupplier.aspx — active RC row.</summary>
    public class SupplierRcDetailRowDto
    {
        public int ContractItemId { get; set; }
        public int ItemId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string TenderNo { get; set; } = string.Empty;
        public int TenderId { get; set; }
        public string ContractDate { get; set; } = string.Empty;
        public string ContractEndDate { get; set; } = string.Empty;
        public decimal BasicRate { get; set; }
        public decimal Percentage { get; set; }
        public decimal SingleUnitPrice { get; set; }
        public bool HasSpecification { get; set; }
    }

    /// <summary>AcceptedReoprtSupplier.aspx — accepted tender price row.</summary>
    public class SupplierAcceptedReportRowDto
    {
        public int ItemId { get; set; }
        public int TenderId { get; set; }
        public int SupplierId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string TenderNo { get; set; } = string.Empty;
        public string TenderDate { get; set; } = string.Empty;
        public int TenderQuantity { get; set; }
        public decimal BasicRate { get; set; }
        public decimal Gst { get; set; }
        public decimal AcceptedBasicRate { get; set; }
    }

    public class SupplierAcceptedSupplierOptionDto
    {
        public int SupplierId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>ReceiptComplainSupplier.aspx — complaint row.</summary>
    public class SupplierReceiptComplainRowDto
    {
        public int ComplaintId { get; set; }
        public string ComplaintNo { get; set; } = string.Empty;
        public string PoNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string SerialNo { get; set; } = string.Empty;
        public string ComplaintDate { get; set; } = string.Empty;
        public string NotFunctionDate { get; set; } = string.Empty;
        public string LocationName { get; set; } = string.Empty;
        public string FacilityContactNo { get; set; } = string.Empty;
        public string ComplaintDetails { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileExt { get; set; } = string.Empty;
        public bool HasFile { get; set; }
    }

    public class SupplierEmdDocumentTypeDto
    {
        public int DtypeId { get; set; }
        public string DtypeName { get; set; } = string.Empty;
    }

    /// <summary>EMDdeposite.aspx — submitted deposit row.</summary>
    public class SupplierEmdDepositRowDto
    {
        public int Id { get; set; }
        public int SupId { get; set; }
        public string TenderNo { get; set; } = string.Empty;
        public decimal EmdAmt { get; set; }
        public string EmdType { get; set; } = string.Empty;
        public string EmdDocumentNo { get; set; } = string.Empty;
        public string EmdDepositeDt { get; set; } = string.Empty;
        public string EmdDocument { get; set; } = string.Empty;
        public bool HasFile { get; set; }
    }

    /// <summary>PaymentReport.aspx — paid PO payment row.</summary>
    public class SupplierPaymentReportRowDto
    {
        public int PoId { get; set; }
        public int SanctionId { get; set; }
        public int SupplierId { get; set; }
        public int BudgetId { get; set; }
        public int PaymentId { get; set; }
        public string PoNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public decimal GrossAmt { get; set; }
        public decimal TotalDed { get; set; }
        public decimal TotalAddition { get; set; }
        public decimal ChequeAmt { get; set; }
        public string ChequeDate { get; set; } = string.Empty;
        public string AidNo { get; set; } = string.Empty;
        public string PaymentType { get; set; } = string.Empty;
    }

    /// <summary>BalanceStatussupplier.aspx — pending receipt/installation row.</summary>
    public class SupplierBalanceStatusRowDto
    {
        public int PoId { get; set; }
        public int DirectorateId { get; set; }
        public string TenderNo { get; set; } = string.Empty;
        public string Year { get; set; } = string.Empty;
        public string PoNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public string FacilityAutName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Supplier { get; set; } = string.Empty;
        public decimal PoQty { get; set; }
        public decimal SupplyQty { get; set; }
        public decimal ReceiptQty { get; set; }
        public decimal InstQty { get; set; }
        public string PoType { get; set; } = string.Empty;
        public decimal BalanceQty { get; set; }
    }

    /// <summary>SDdetailSupplier.aspx — MasSD payment mode option.</summary>
    public class SupplierSdPaymentModeDto
    {
        public string SdMode { get; set; } = string.Empty;
        public string SdName { get; set; } = string.Empty;
        public bool MaturityOptional { get; set; }
    }

    /// <summary>SDdetailSupplier.aspx — load/save SD security deposit detail.</summary>
    public class SupplierPoSdDetailDto
    {
        public int PoId { get; set; }
        public int SupplierId { get; set; }
        public int ItemId { get; set; }
        public string EquipmentName { get; set; } = string.Empty;
        public decimal GrossValue { get; set; }
        public decimal SdAmount { get; set; }
        public bool HasExisting { get; set; }
        public bool HasFile { get; set; }
        public bool IsSubmitted { get; set; }
        public string? PaymentMode { get; set; }
        public string? IssueDate { get; set; }
        public string? MaturityDate { get; set; }
        public string? DocumentNo { get; set; }
        public List<SupplierSdPaymentModeDto> PaymentModes { get; set; } = new();
    }

    /// <summary>ApplyForExtension.aspx — extension history row.</summary>
    public class SupplierPoExtensionRowDto
    {
        public int ExtensionId { get; set; }
        public int PoId { get; set; }
        public string Remark { get; set; } = string.Empty;
        public int Days { get; set; }
        public string ExtendedDate { get; set; } = string.Empty;
        public string PoEndDate { get; set; } = string.Empty;
        public bool HasFile { get; set; }
        public string LetterDate { get; set; } = string.Empty;
        public string LetterNo { get; set; } = string.Empty;
        public string ApplyDate { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>ApplyForExtension.aspx — header + extension list.</summary>
    public class SupplierPoExtensionPageDto
    {
        public int PoId { get; set; }
        public int SupplierId { get; set; }
        public string EquipmentName { get; set; } = string.Empty;
        public string PoNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public int SupplyDays { get; set; }
        public string PoEndDate { get; set; } = string.Empty;
        public string BaseEndDate { get; set; } = string.Empty;
        public bool HasSdRecord { get; set; }
        public bool CanApply { get; set; }
        public bool HasPendingExtension { get; set; }
        public List<SupplierPoExtensionRowDto> Extensions { get; set; } = new();
    }

    /// <summary>Facility_InstallationReportSUP.aspx — receipt item row.</summary>
    public class SupplierInstallationReportRowDto
    {
        public int SlNo { get; set; }
        public int ItemDetailId { get; set; }
        public string SerialNo { get; set; } = string.Empty;
        public string InstallationDate { get; set; } = string.Empty;
        public string WarrantyFrom { get; set; } = string.Empty;
        public string WarrantyTo { get; set; } = string.Empty;
        public decimal ReceivedQty { get; set; }
        public string WarrantyCardNo { get; set; } = string.Empty;
        public string InstallationLocation { get; set; } = string.Empty;
        public bool IsMongo { get; set; }
        public bool HasInstallationReport { get; set; }
        public bool HasInstallationPhoto { get; set; }
        public bool HasWarrantyCard { get; set; }
        public bool HasChallan { get; set; }
    }

    /// <summary>Facility_InstallationReportSUP.aspx — page payload.</summary>
    public class SupplierInstallationReportPageDto
    {
        public int ReceiptId { get; set; }
        public string ReceivedDate { get; set; } = string.Empty;
        public bool BulkInst { get; set; }
        public bool HasBulkInstallationReport { get; set; }
        public bool HasBulkInstallationPhoto { get; set; }
        public bool HasBulkWarrantyCard { get; set; }
        public bool HasBulkChallan { get; set; }
        public List<SupplierInstallationReportRowDto> Rows { get; set; } = new();
    }

    /// <summary>InstalationReport.aspx — printable installation certificate.</summary>
    public class SupplierInstallationPrintDto
    {
        public int ItemDetailId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string SupplyQty { get; set; } = string.Empty;
        public string PoNo { get; set; } = string.Empty;
        public string SerialNo { get; set; } = string.Empty;
        public string ConsigneeAddress { get; set; } = string.Empty;
        public string ReceiptDate { get; set; } = string.Empty;
        public string InstallationLocation { get; set; } = string.Empty;
        public string TrainingItemName { get; set; } = string.Empty;
        public string WarrantyValidity { get; set; } = string.Empty;
        public string CgmscLogoPrinted { get; set; } = string.Empty;
        public string ServiceManualProvided { get; set; } = string.Empty;
        public string OperatingManualProvided { get; set; } = string.Empty;
        public string CalibrationCertificateProvided { get; set; } = string.Empty;
        public string OriginalWarrantyCardReceived { get; set; } = string.Empty;
        public string OtherStatutoryDocuments { get; set; } = string.Empty;
        public string AllAccessoriesReceived { get; set; } = string.Empty;
        public string DispatchNo { get; set; } = string.Empty;
        public string DispatchDate { get; set; } = string.Empty;
    }
}
