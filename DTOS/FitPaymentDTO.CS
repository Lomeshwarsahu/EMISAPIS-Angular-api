namespace EMISAPIS.DTOS
{
    public class FitPaymentDTO
    {
        //public int PoId { get; set; }
        //public string TenderNo { get; set; }
        //public string PoNo { get; set; }
        //public string Supplier { get; set; }
        //public string PoDate { get; set; }
        //public string ItemName { get; set; }
        //public int POQty { get; set; }
        //public int POValue { get; set; }
        //public int SupplyQty { get; set; }
        //public int ReceiptQty { get; set; }
        //public string FitUnfit { get; set; }
        //public string PresentFile { get; set; }
        //public string FileNo { get; set; }
        //public string LastRDate { get; set; }
        //public string ReasonName { get; set; }

        public string FitUnfit { get; set; }
        public int PoId { get; set; }
        public string TenderNo { get; set; }
        public string PoNo { get; set; }
        public string Supplier { get; set; }
        public string PoDate { get; set; }
        public string FacilityAutName { get; set; }
        public string ItemCode { get; set; }
        public string ItemName { get; set; }

        public int POQty { get; set; }
        public long POValue { get; set; }
        public int SupplyQty { get; set; }
        public int InstallationQty { get; set; }
        public int ReceiptQty { get; set; }

        public string LastRDate { get; set; }
        public string PoType { get; set; }

        public string FileNo { get; set; }
        public string FileDt { get; set; }

        public string PresentFile { get; set; }
        public int PresentUserId { get; set; }
        public int ToUserId { get; set; }

        public decimal PenaltyPercent { get; set; }

        public int ReasonId { get; set; }
        public string ReasonName { get; set; }
        public string IsSolved { get; set; }

        public string SiteStatus { get; set; }

        public int RowNo { get; set; }

        public string ToDate { get; set; }
        public string EntDT { get; set; }

        public string Remarks { get; set; }

        public string ExtStatus { get; set; }
    }
}
