namespace EMISAPIS.DTOS
{
    public class EmdSupplierOptionDto
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
    }

    public class EmdTenderOptionDto
    {
        public int TenderId { get; set; }
        public string TenderNo { get; set; } = string.Empty;
    }

    public class EmdRefundPendingDto
    {
        public int Id { get; set; }
        public int SupId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string TenderNo { get; set; } = string.Empty;
        public decimal EmdAmt { get; set; }
        public string EmdType { get; set; } = string.Empty;
        public string EmdDocumentNo { get; set; } = string.Empty;
        public string EmdDocument { get; set; } = string.Empty;
        public string EmdDepositDate { get; set; } = string.Empty;
        public string EntryDate { get; set; } = string.Empty;
        public bool HasFile { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class EmdApproveItemDto
    {
        public int Id { get; set; }
        public int SupId { get; set; }
    }

    public class EmdApproveRequestDto
    {
        public List<EmdApproveItemDto> Items { get; set; } = new();
    }

    public class SdReleasePendingDto
    {
        public int PoId { get; set; }
        public string TenderNo { get; set; } = string.Empty;
        public string PoNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public decimal SdAmount { get; set; }
        public string SdType { get; set; } = string.Empty;
        public string SdIssueDate { get; set; } = string.Empty;
        public string SdMaturityDate { get; set; } = string.Empty;
        public string SdEntryDate { get; set; } = string.Empty;
        public int SdDetailsId { get; set; }
    }

    public class SdReleaseSaveRequestDto
    {
        public List<int> PoIds { get; set; } = new();
        public decimal ReleaseAmount { get; set; }
        public decimal RecoveredAmount { get; set; }
        public string ReleaseType { get; set; } = string.Empty;
        public string RefundDate { get; set; } = string.Empty;
        public string ChequeNo { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
    }
}
