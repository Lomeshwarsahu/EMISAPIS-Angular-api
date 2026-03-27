namespace EMISAPIS.DTOS
{
    public class ContractDTO
    {
    }

    public class ConTenterlistDTO
    {
        public string tender_no { get; set; }
        public int tender_id { get; set; }
    }
    public class AccSupplierlistDTO
    {
        public string name { get; set; }
        public int supplier_id { get; set; }
    }
    public class RcDetailReportDTO
    {
        public int ContractItemId { get; set; }
        public int ItemId { get; set; }

        public string ItemCode { get; set; }
        public string ItemName { get; set; }

        public string Make { get; set; }
        public string Model { get; set; }

        public string SupplierName { get; set; }
        public string TenderNo { get; set; }

        public string ContractDate { get; set; }
        public string ContractEndDate { get; set; }

        public decimal BasicRate { get; set; }
        public decimal GST { get; set; }
        public decimal SingleUnitPrice { get; set; }

        public decimal CMC1 { get; set; }
        public decimal CMC2 { get; set; }
        public decimal CMC3 { get; set; }
        public decimal CMC4 { get; set; }
        public decimal CMC5 { get; set; }

        public int TenderId { get; set; }
    }

    public class RcDetailReportRequestDTO
    {
        public int? TenderId { get; set; }
        public int CategoryId { get; set; }   // 1=Equipment, 2=Reagent
        public string RcType { get; set; }    // R or D

    }

    public class TenderSupplierRequestDTO
    {
        public string FilterType { get; set; } // "tender" or "supplier"
        public int? TenderId { get; set; }
        public int? SupplierId { get; set; }
    }

    public class TenderSupplierDTO
    {
        public int ItemId { get; set; }
        public string ItemCode { get; set; }
        public string ItemName { get; set; }

        public string SupplierName { get; set; }
        public string TenderNo { get; set; }
        public string TenderDate { get; set; }

        public int TenderQuantity { get; set; }

        public decimal BasicRate { get; set; }
        public decimal GST { get; set; }
        public decimal AcceptedBasicRate { get; set; }

        public string AcceptedDate { get; set; }

        public int TenderId { get; set; }
        public int SupplierId { get; set; }
    }
}
