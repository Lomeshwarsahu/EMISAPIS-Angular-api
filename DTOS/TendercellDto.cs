namespace EMISAPIS.DTOS
{
 
    //public class SupplierDto
    //{
    //    public int SupplierId { get; set; }
    //    public string IsContractor { get; set; }
    //    public string Name { get; set; }
    //    public string EmailId { get; set; }
    //    public string PhNo { get; set; }
    //    public string Address { get; set; }
    //    public string SupplierCode { get; set; }
    //    public string ModuleCode { get; set; }
    //    public string MobileNo { get; set; }
    //    public string GstNo { get; set; }
    //    public string Type { get; set; }
    //    public string Class { get; set; }
    //    public string IsRegister { get; set; }
    //    public string ServiceEngineerName { get; set; }
    //    public string ServiceEngineerNumber { get; set; }
    //    public string TinNo { get; set; }
    //}

    public class DirectorateUserDto
    {
        public int UserId { get; set; }
        public string EMailId { get; set; }
        public string UserName { get; set; }
        public string Designation { get; set; }
    }

    public class IndentGridDto
    {
        public string Description { get; set; }
        public int IndentId { get; set; }
        public int LocationId { get; set; }
        public int DirectorateId { get; set; }
        public int FinancialYearId { get; set; }
        public string ConsolidatedDate { get; set; }
        public decimal FinalQty { get; set; }
        public int NosIndentQty { get; set; }
        public string EStatus { get; set; }
        public string UploadStatus { get; set; }
        public string UserName { get; set; }
        public int UserId { get; set; }
        public string DirApproved { get; set; }
        public string DispatchNo { get; set; }
        public string DispatchDt { get; set; }
        public string CgmscApp { get; set; }
    }
    public class TenderStatusDtos
    {
        public int TenderId { get; set; }
        public string Name { get; set; }
    }

    public class TenderDetailDto
    {
        public int TenderId { get; set; }
        public string TenderNo { get; set; }
        public int FinancialYearId { get; set; }
        public string Status { get; set; }
        public string TenderDate { get; set; }
        public string CStatus { get; set; }
        public string CovAdt { get; set; }
        public string ObjCStartDt { get; set; }
        public string ObjCEndDt { get; set; }
    }

    public class SchemedetailsDto
    {
        public int Slno { get; set; }
        public int SchStatusDid { get; set; }
        public int TenderId { get; set; }
        public string Name { get; set; }
        public string Emd { get; set; }
        public decimal ReqEmdAmt { get; set; }
        public decimal SubmittedEmdAmt { get; set; }
        public decimal TpAmount { get; set; }
        public int EmdDocType { get; set; }
        public string EmdPath { get; set; }
        public string EmdFileName { get; set; }
        public string TpFileName { get; set; }
        public string TpPath { get; set; }
        public string EmdDocNo { get; set; }
        public int SupplierId { get; set; }
        public string Remark { get; set; }
        public int PItems { get; set; }

        // ✅ इन्हें int से string किया गया है (क्योंकि इनमें 'Y' या टेक्स्ट आ सकता है)
        public string IsCovTechEli { get; set; }
        public string IsCOVFinEli { get; set; }

        public string CovATechRemarksBeforeObclm { get; set; }
        public string CovAFinRemarksBeforeObclm { get; set; }
        public string DtypeName { get; set; }
        public string TechEli { get; set; }
        public string FinElig { get; set; }

        // ✅ इसे भी int से string किया गया है
        public string IsObjUploadElig { get; set; }

        public string ObjUploadRemrks { get; set; }
        public string ObjElig { get; set; }
        public string FinalCovrARemarks { get; set; }
        public string IsEligibleB { get; set; }
    }
    public class ParticipatedItemDto
    {
        public int ChildId { get; set; }
        public string ItemCodeAsPerTender { get; set; }
        public string ItemName { get; set; }
        public int ItemId { get; set; }
        public string FlagCob { get; set; }
        public string IsEligibleB { get; set; }
        public int SchStatusDid { get; set; }
        public int SchemeId { get; set; }
        public int SupplierId { get; set; }
        public string Name { get; set; }
        public string RemarksA { get; set; }
        public int DemoRemarksId { get; set; }
    }
    public class UpdateItemModel
    {
        public int SchStatusDid { get; set; }
        public int ChildId { get; set; }
        public int ItemId { get; set; }
        public bool IsChecked { get; set; }
        public string RemarksA { get; set; }
    }

    public class UpdateParticipatedItemsRequestDto
    {
        public List<UpdateItemModel> Items { get; set; }
    }

}
