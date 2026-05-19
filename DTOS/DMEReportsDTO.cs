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
}
