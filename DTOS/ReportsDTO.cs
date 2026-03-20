
namespace EMISAPIS.DTOS
{
    public class ItemWiseDetailDTO
    {
        public int financial_year_id { get; set; }
        public string item_code_as_per_tender { get; set; }
        public int po_id { get; set; }
        public string tender_no { get; set; }
        public string year { get; set; }
        public string outward_no { get; set; }
        public string po_no { get; set; }
        public DateTime? po_date { get; set; }
        public int directorate_id { get; set; }
        public string facility_aut_name { get; set; }
        public string item_name { get; set; }
        public string Supplier { get; set; }
        public decimal? POQTY { get; set; }
        public decimal? Supplyqty { get; set; }
        public decimal? receiptQTY { get; set; }
        public DateTime? LastRDate { get; set; }
        public decimal? insqty { get; set; }
        public string potype { get; set; }
        public decimal? balanceToDispatch { get; set; }
        public decimal? BalToReceipt { get; set; }
        public decimal? BalToInstall { get; set; }
    }


    public class ItemDTO
    {
        public int item_id { get; set; }
        public string item_name { get; set; }
    }

    public class ItemWiseFullDTO
    {
        public int po_id { get; set; }
        public string tender_no { get; set; }
        public string year { get; set; }
        public string outward_no { get; set; }
        public string po_no { get; set; }
        public string po_date { get; set; } // already converted to varchar in query
        public int directorate_id { get; set; }
        public string facility_aut_name { get; set; }
        public string item_code_as_per_tender { get; set; }
        public string item_name { get; set; }
        public string Supplier { get; set; }
        public string DBStart_Name_En { get; set; }
        public string location_name { get; set; }
        public decimal? POQTY { get; set; }
        public decimal? Supplyqty { get; set; }
        public decimal? receiptQTY { get; set; }
        public decimal? insqty { get; set; }
        public string potype { get; set; }
        public decimal? balanceToDispatch { get; set; }
        public decimal? BalToReceipt { get; set; }
        public decimal? BalToInstall { get; set; }
    }
    public class DistrictsDTO
    {
        public int DP_DistrictID { get; set; }
        public string? DBStart_Name_En { get; set; }
    }
    public class DiectorateDTO
    {
        public int facility_aut_id { get; set; }
        public string? facility_aut_name { get; set; }
    }
}
