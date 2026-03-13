namespace EMISAPIS.DTOS
{
    public class FitPaymentRequestDTO
    {
        //    public string PoType { get; set; }   // NP / CP / All / SP
        //    public string FitUnfit { get; set; } // FP / NFP/ALL
      
        //public bool MyDeskFile { get; set; }
        //    public int UserId { get; set; }

        public string Potype { get; set; }      // NP / CP / SP / All
        public bool MyDeskFile { get; set; }
        public string FitUnfit { get; set; }    // FP / NFP / All
        public int UserId { get; set; }         // DistId

    }
}
