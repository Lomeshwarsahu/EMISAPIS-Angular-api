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
}
