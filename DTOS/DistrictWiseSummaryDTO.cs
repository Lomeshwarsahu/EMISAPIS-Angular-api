namespace EMISAPIS.DTOS
{
    public class DistrictWiseDetailDTO
    {
        public string potype { get; set; }
        public string tender_no { get; set; }
        public string po_no { get; set; }
        public string po_date { get; set; }
        public string supplier_name { get; set; }
        public string item_code_as_per_tender { get; set; }
        public string item_name { get; set; }
        public string DBStart_Name_En { get; set; }
        public int consignee_id { get; set; }
        public string location_name { get; set; }
        public decimal po_qty { get; set; }
        public decimal basicrate { get; set; }
        public decimal percentage { get; set; }
        public decimal totalprice { get; set; }
        public string Eqptype { get; set; }
        public int po_id { get; set; }
        public decimal supply_qty { get; set; }
        public decimal receiptQTY { get; set; }
        public decimal insqty { get; set; }

    }

    public class IndentPOSummaryDirwiseDTO
    {
        public string IndentDT { get; set; }
        public string Indent_Letter_no { get; set; }
        public string po_no { get; set; }
        public string podate { get; set; }
        public string item_code_as_per_tender { get; set; }
        public string item_name { get; set; }
        public string eqtype { get; set; }
        public decimal Indent_Qty { get; set; }
        public decimal poqty { get; set; }
        public int no_of_consignee { get; set; }
        public decimal povalue { get; set; }
        public int indent_consolidation_id { get; set; }
        public string indent_year { get; set; }
        public string po_year { get; set; }
    }
}
