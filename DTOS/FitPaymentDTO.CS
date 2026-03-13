namespace EMISAPIS.DTOS
{
    public class FitPaymentDTO
    {
        public int PoId { get; set; }
        public string TenderNo { get; set; }
        public string PoNo { get; set; }
        public string Supplier { get; set; }
        public string PoDate { get; set; }
        public string ItemName { get; set; }
        public int POQty { get; set; }
        public int SupplyQty { get; set; }
        public int ReceiptQty { get; set; }
        public string FitUnfit { get; set; }
        public string PresentFile { get; set; }
        public string FileNo { get; set; }
        public string LastRDate { get; set; }


    }
}
