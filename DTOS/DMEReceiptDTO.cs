namespace EMISAPIS.DTOS
{
    /// <summary>FacilityPO_ReceiptDME — receipt / installation entry page (same JSON shape as supplier).</summary>
    public class DmeReceiptEntryPageDto
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
        public string CancellationDays { get; set; } = string.Empty;
        public string LastReceiptDate { get; set; } = string.Empty;
        public bool BulkInst { get; set; }
        public bool HasBulkInstallationReport { get; set; }
        public bool HasBulkInstallationPhoto { get; set; }
        public bool HasBulkWarrantyCard { get; set; }
        public bool HasBulkChallan { get; set; }
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
        public string InstallDispatchNo { get; set; } = string.Empty;
        public string InstallDispatchDate { get; set; } = string.Empty;
        public List<DmeReceiptIssueDetailOptionDto> IssueDetailOptions { get; set; } = new();
        public List<DmeReceiptInstallationLineDto> InstallationLines { get; set; } = new();
    }

    public class DmeReceiptIssueDetailOptionDto
    {
        public int IssueDetailId { get; set; }
        public string SerialNo { get; set; } = string.Empty;
        public string WarrantyCertificateNo { get; set; } = string.Empty;
        public decimal DispatchedQty { get; set; }
    }

    public class DmeReceiptInstallationLineDto
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
        public bool HasInstallationReport { get; set; }
        public bool HasInstallationPhoto { get; set; }
        public bool HasWarrantyCard { get; set; }
        public bool HasChallan { get; set; }
    }

    public class DmeReceiptSaveRequestDto
    {
        public int UserId { get; set; }
        public int PoId { get; set; }
        public int LocationId { get; set; }
        public int IssueId { get; set; }
        public string ReceivedDate { get; set; } = string.Empty;
        public string ReceiptNo { get; set; } = string.Empty;
        public string ReceiptQty { get; set; } = string.Empty;
        public string ReceiptRemarks { get; set; } = string.Empty;
    }

    public class DmeReceiptInstallationSaveRequestDto
    {
        public int UserId { get; set; }
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
        public bool BulkInst { get; set; }
    }

    public class DmeReceiptCompleteRequestDto
    {
        public int UserId { get; set; }
        public int PoId { get; set; }
        public int LocationId { get; set; }
        public int IssueId { get; set; }
        public int ReceiptId { get; set; }
        public string InstallDispatchNo { get; set; } = string.Empty;
        public string InstallDispatchDate { get; set; } = string.Empty;
    }

    public class DmeInstallationReportRowDto
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

    public class DmeInstallationReportPageDto
    {
        public int ReceiptId { get; set; }
        public string ReceivedDate { get; set; } = string.Empty;
        public bool BulkInst { get; set; }
        public bool HasBulkInstallationReport { get; set; }
        public bool HasBulkInstallationPhoto { get; set; }
        public bool HasBulkWarrantyCard { get; set; }
        public bool HasBulkChallan { get; set; }
        public List<DmeInstallationReportRowDto> Rows { get; set; } = new();
    }
}
