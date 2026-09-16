namespace EMISAPIS.DTOS
{
    public class PerformanceGridDto
    {
        public int PoId { get; set; }
        public string Pono { get; set; } = "";
        public string PoDate { get; set; } = "";
        public decimal? POQTY { get; set; }
        public decimal? InstQTY { get; set; }
        public string LastInstDT1 { get; set; } = "";
        public string Name { get; set; } = "";
        public string TenderNo { get; set; } = "";
        public string PRequired { get; set; } = "";
        public string PStatus { get; set; } = "";
        public int SupplierId { get; set; }
        public string Potype { get; set; } = "";
        public string ReleaseType { get; set; } = "";
        public int? ReleaseValue { get; set; }
        public string ItemCode { get; set; } = "";
        public string ItemName { get; set; } = "";
        public string CStatus { get; set; } = "";
        public string LimitDT { get; set; } = "";
        public string PresentFile { get; set; } = "";
        public string FileNo { get; set; } = "";
        public string Filedt { get; set; } = "";
        public string DownloadPerf { get; set; } = "";
        public int TenderId { get; set; }
        public string RowColor { get; set; } = "";
        public bool CanRelease { get; set; }
    }

    public class TenderLeavyDto
    {
        public int TenderId { get; set; }
        public string TenderNo { get; set; } = "";
        public string TenderDate { get; set; } = "";
        public string ReleaseType { get; set; } = "M";
        public string Performacereq { get; set; } = "Y";
        public int? ReleaseValue { get; set; }
        public string PerformanceEntryDt { get; set; } = "";
    }

    public class UpdateTenderLeavyDto
    {
        public int TenderId { get; set; }
        public string ReleaseType { get; set; } = "";
        public string Performacereq { get; set; } = "";
        public int? ReleaseValue { get; set; }
        public string Otp { get; set; } = "";
        public int UserId { get; set; }
    }

    public class PerformanceHeaderDto
    {
        public int PoId { get; set; }
        public string PoDate { get; set; } = "";
        public int TenderId { get; set; }
        public string PoNo { get; set; } = "";
        public int NoOfConsignee { get; set; }
        public decimal? POQTY { get; set; }
        public decimal? DispatchQty { get; set; }
        public decimal? ReceiptQty { get; set; }
        public decimal? InsQTY { get; set; }
        public string ItemCode { get; set; } = "";
        public string ItemName { get; set; } = "";
        public string Make { get; set; } = "";
        public string Model { get; set; } = "";
        public decimal? Percentage { get; set; }
        public decimal? BasicRate { get; set; }
        public string ReleaseType { get; set; } = "";
        public int? ReleaseValue { get; set; }
    }

    public class ConsigneeInstallationDto
    {
        public int SlNo { get; set; }
        public string LocationName { get; set; } = "";
        public int ConsigneeId { get; set; }
        public decimal? POQTY { get; set; }
        public decimal? Dis { get; set; }
        public decimal? ReceiptQTY { get; set; }
        public decimal? Insqty { get; set; }
        public string InstallationDate { get; set; } = "";
    }

    public class SavePerformanceReleaseDto
    {
        public int PoId { get; set; }
        public int UserId { get; set; }
    }

    public class ForwardFilePerformanceDto
    {
        public int UserId { get; set; }
        public int ToUserId { get; set; }
        public int PonoId { get; set; }
        public string FileId { get; set; } = "";
        public string Remarks { get; set; } = "";
        public string ForwardDate { get; set; } = "";
        public string Flag { get; set; } = "S";
    }

    public class SendToUserDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = "";
    }

    public class ConsigneeDetailHeaderDto
    {
        public int PoId { get; set; }
        public string PoDate { get; set; } = "";
        public string PoNo { get; set; } = "";
        public string ItemCode { get; set; } = "";
        public string ItemName { get; set; } = "";
        public decimal? Percentage { get; set; }
        public decimal? BasicRate { get; set; }
        public int NoOfConsignee { get; set; }
        public string Model { get; set; } = "";
        public string Make { get; set; } = "";
        public decimal? POQTY { get; set; }
        public decimal? DispatchQty { get; set; }
        public decimal? ReceiptQty { get; set; }
        public decimal? InsQTY { get; set; }
        public string ReleaseDur { get; set; } = "";
        public string ReleaseType { get; set; } = "";
    }

    public class ConsigneeDetailGridDto
    {
        public int Sno { get; set; }
        public string LocationName { get; set; } = "";
        public decimal? PoQty { get; set; }
        public decimal? DispatchedQty { get; set; }
        public decimal? ReceivedQty { get; set; }
        public decimal? InstalledQty { get; set; }
        public string InstallationDate { get; set; } = "";
    }

    public class ConsigneeDetailResponseDto
    {
        public ConsigneeDetailHeaderDto Header { get; set; } = new();
        public List<ConsigneeDetailGridDto> Grid { get; set; } = new();
    }

    public class UploadConsigneePerformanceDto
    {
        public int PoId { get; set; }
        public string PerfCertType { get; set; } = "";
        public int UserId { get; set; }
    }

    public class FinReleaseGridDto
    {
        public int PoId { get; set; }
        public string Fund { get; set; } = "";
        public string SupplierName { get; set; } = "";
        public string NastiNo { get; set; } = "";
        public string PoNo { get; set; } = "";
        public decimal? InstalledQty { get; set; }
        public string LastInstalledDate { get; set; } = "";
        public string ChequeDt { get; set; } = "";
        public decimal? WithheldAmt { get; set; }
        public decimal? RecoveredAmount { get; set; }
        public decimal? ToBeReleasedAmt { get; set; }
        public string Remarks { get; set; } = "";
        public string PaidFrom { get; set; } = "";
        public string PaidTo { get; set; } = "";
        public string PerformanceRequired { get; set; } = "";
        public string TenderNo { get; set; } = "";
        public string ComplaintStatus { get; set; } = "";
        public bool IsEligible { get; set; }
    }

    public class UpdateReleaseDataDto
    {
        public int PoId { get; set; }
        public decimal? RecoveredAmount { get; set; }
        public string Remarks { get; set; } = "";
        public int UserId { get; set; }
    }

    public class GoForChequePreparationDto
    {
        public List<int> PoIds { get; set; } = new();
        public int UserId { get; set; }
    }

    public class ReleaseYearDto
    {
        public int ReleaseYearId { get; set; }
        public string ReleaseYearName { get; set; } = "";
    }

    public class FundDto
    {
        public int FundId { get; set; }
        public string FundName { get; set; } = "";
    }

    public class ForwardUserDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = "";
    }

    public class ChequePrepGridDto
    {
        public int PaymentId { get; set; }
        public string PaymentNo { get; set; } = "";
        public string PoNo { get; set; } = "";
        public string Fund { get; set; } = "";
        public int NoOfSupplier { get; set; }
        public int NoOfPos { get; set; }
        public decimal ToBeReleasedAmt { get; set; }
        public decimal WithheldRecoveredAmt { get; set; }
        public string Status { get; set; } = "";
        public string CgmscAccountNo { get; set; } = "";
        public string ChequeNo { get; set; } = "";
        public string ChequeDt { get; set; } = "";
        public string PaidOn { get; set; } = "";
    }

    public class UpdateChequeInfoDto
    {
        public int PaymentId { get; set; }
        public string ChequeNo { get; set; } = "";
        public string ChequeDt { get; set; } = "";
        public string PaidOn { get; set; } = "";
    }
}
