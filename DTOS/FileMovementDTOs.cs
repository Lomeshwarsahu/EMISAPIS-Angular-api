namespace EMISAPIS.DTOS
{
    // LogoVerifiedHO
    public class LogoVerifiedBatchDto
    {
        public int SlNo { get; set; }
        public int ItemDetailId { get; set; }
        public string ModelNo { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public string InstallationDate { get; set; } = string.Empty;
        public string LocationName { get; set; } = string.Empty;
        public string Div100Per { get; set; } = string.Empty;
        public string IsUserRecom { get; set; } = string.Empty;
        public string CgmscLogPrinted { get; set; } = string.Empty;
        public string InstalationReportFile { get; set; } = string.Empty;
        public string ChallanFile { get; set; } = string.Empty;
        public string WarrantyCardFile { get; set; } = string.Empty;
        public int ReceiptId { get; set; }
    }

    public class LogoVerifiedSaveDto
    {
        public int ItemDetailId { get; set; }
        public string UserRecom { get; set; } = string.Empty;
        public string LogoVerified { get; set; } = string.Empty;
    }

    // SiteNotReady
    public class SiteNotReadyReceiptDto
    {
        public int ReceiptId { get; set; }
        public string ReceiptNo { get; set; } = string.Empty;
        public string RecievedDate { get; set; } = string.Empty;
        public decimal ReceiptQty { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string SiteNotReadyFile { get; set; } = string.Empty;
        public string SiteNotFlag { get; set; } = string.Empty;
        public int PoId { get; set; }
    }

    // InvoicesBySO
    public class InvoiceSoHeaderDto
    {
        public string PoNo { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public int PoNoId { get; set; }
    }

    public class InvoiceSoLineDto
    {
        public int InvoiceId { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public string InvoiceDate { get; set; } = string.Empty;
        public decimal InvoiceValue { get; set; }
        public decimal Cgst { get; set; }
        public decimal Sgst { get; set; }
    }

    public class InvoiceSoSaveRequestDto
    {
        public int InvoiceId { get; set; }
        public int PoNoId { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public string InvoiceDate { get; set; } = string.Empty;
        public decimal InvoiceValue { get; set; }
        public decimal Cgst { get; set; }
        public decimal Sgst { get; set; }
    }

    // PoReportNew
    public class PoReportItemOptionDto
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
    }

    public class PoReportDistrictOptionDto
    {
        public int DpDistrictId { get; set; }
        public string DBStart_Name_En { get; set; } = string.Empty;
    }

    public class PoReportFacilityOptionDto
    {
        public int FacilityAutId { get; set; }
        public string FacilityAutName { get; set; } = string.Empty;
    }

    public class PoReportRowDto
    {
        public int PoId { get; set; }
        public string TenderNo { get; set; } = string.Empty;
        public string PoNo { get; set; } = string.Empty;
        public string Supplier { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public decimal Poqty { get; set; }
        public decimal PoValue { get; set; }
        public decimal ReceiptQTY { get; set; }
        public decimal InstalationQty { get; set; }
        public string LastRDate1 { get; set; } = string.Empty;
        public string FacilityAutName { get; set; } = string.Empty;
    }
}
