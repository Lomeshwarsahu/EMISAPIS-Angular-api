namespace EMISAPIS.DTOS
{
    public class CmcItemOptionDto
    {
        public string ItemCodeAsPerTender { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
    }

    public class CmcTenderOptionDto
    {
        public int TenderId { get; set; }
        public string TenderNo { get; set; } = string.Empty;
    }

    public class CmcDetailRowDto
    {
        public int ItemId { get; set; }
        public int TenderId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemCodeAsPerTender { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string TenderNo { get; set; } = string.Empty;
        public decimal Cmc1 { get; set; }
        public decimal Cmc2 { get; set; }
        public decimal Cmc3 { get; set; }
        public decimal Cmc4 { get; set; }
        public decimal Cmc5 { get; set; }
    }

    public class EligibleDetailRowDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Year { get; set; } = string.Empty;
        public string ConsolidatedDate { get; set; } = string.Empty;
        public string IndentConNo { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string FacilityAutCode { get; set; } = string.Empty;
        public decimal IndentQty { get; set; }
        public decimal PoQty { get; set; }
        public decimal BalancePo { get; set; }
        public decimal BasicRate { get; set; }
        public string TenderNo { get; set; } = string.Empty;
    }
}
