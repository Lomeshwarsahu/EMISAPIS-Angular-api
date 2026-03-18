namespace EMISAPIS.DTOS
{
    public class PurchaseOrderGridDTO
    {
        public int item_id { get; set; }
        public int PO_ID { get; set; }
        public string CODE { get; set; }
        public string ITEM_NAME { get; set; }
        public string OUTWARD_NO { get; set; }
        public string po_date { get; set; }
        public string PO_NO { get; set; }
        public int quantity { get; set; }
        public int no_of_consignee { get; set; }
        public decimal basic_rate { get; set; }
        public decimal percentage { get; set; }
        public decimal single_unit_price { get; set; }
        public decimal totalPOvalue { get; set; }
        public string tender_no { get; set; }
        public string status { get; set; }
        public string SD { get; set; }
        public string SubmissionStatus { get; set; }
        public string name { get; set; }
    }
}
