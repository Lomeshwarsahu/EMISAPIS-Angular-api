namespace EMISAPIS.DTOS
{
    public class DescrepencyDTO
    {
        public int SlNo { get; set; }
        public int DeniedQty { get; set; }
        public int PoNoId { get; set; }
        public int ConsigneeId { get; set; }
        public string LocationName { get; set; }
        public string DeniedLetter { get; set; }
        public string ReceiptCopy { get; set; }
        public string Ext { get; set; }
        public string ExtRecCopy { get; set; }
        public int DecrepencyId { get; set; }
    }
}
