//using System.Text.Json.Serialization;
namespace EMISAPIS.DTOS
{
    public class PoHeaderDTO
    {
        //[JsonPropertyName("code")]
        public int PoId { get; set; }
        public string PoDate { get; set; }
        public string YearOnly { get; set; }
        public int TenderId { get; set; }
        public string PoNo { get; set; }
        public int SupplierId { get; set; }

        public string TenderNo { get; set; }
        public string TenderDate { get; set; }

        public string Status { get; set; }
        public string Remarks { get; set; }

        public string ItemName { get; set; }
        //public string Code { get; set; }
        public string itemcode { get; set; }

        

        public int FinancialYearId { get; set; }
        public string Year { get; set; }

        public string ApprovedBy { get; set; }
        public string OutwardNo { get; set; }

        public int Poq { get; set; }
        public int Dispatched { get; set; }
        public int ReceiptQty { get; set; }
        public int InsQty { get; set; }

        public decimal Percentage { get; set; }
        public decimal BasicRate { get; set; }

        public int ContractItemId { get; set; }

        public int WarrantyYear { get; set; }

        public string Make { get; set; }
        public string Model { get; set; }

        public int TrancheDays { get; set; }

        public int Nosco { get; set; }
    }
}