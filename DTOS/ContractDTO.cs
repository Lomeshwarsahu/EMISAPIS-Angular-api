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

    public class UsersDTO
    {
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string Designation { get; set; }
    }

    public class IndentConsolidationDTO
    {
        public int IndentConsolidationId { get; set; }
        public string IndentConNo { get; set; }
        public string IndentDate { get; set; }
        public int ItemCount { get; set; }
        public string Status { get; set; }
        public string FacilityAutName { get; set; }
        public string Description { get; set; }
        public string Path { get; set; }
        public string UserType { get; set; }
        public string Designation { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
    }
    public class IndentFilterRequestDTO
    {
        public string FinancialYearId { get; set; }
        public string ItemId { get; set; }
        public string AuthorityId { get; set; }
        public string UserId { get; set; }
    }

    public class ComplaintDTO
    {
        public int ComplaintId { get; set; }
        public string ComplaintNo { get; set; }
        public string ComplaintDate { get; set; }
        public int ItemId { get; set; }
        public string ComplaintDetails { get; set; }
        public int LocationId { get; set; }
        public int SupplierId { get; set; }
        public int ComplaintTroubleId { get; set; }
        public string NotFunctionDate { get; set; }
        public string ItemName { get; set; }
        public string LocationName { get; set; }
        public string ItemCode { get; set; }
        public int UserId { get; set; }
        public string SerialNo { get; set; }
        public string SupplierName { get; set; }
        public string Email { get; set; }
        public string MobileNo { get; set; }
        public string Path { get; set; }
        public string Ext { get; set; }
        public int ExtensionId { get; set; }
    }
    public class ComplaintRequestDTO
    {
        public string Did { get; set; }   // authority
        public string Status { get; set; } // Booked / Closed
    }

    public class FacilityReportDTO
    {
        public int FacilityAutId { get; set; }
        public string FacilityAutName { get; set; }
        public string POtype { get; set; }
        public int NosPO { get; set; }
        public int NosItem { get; set; }
        public decimal TotalPOValueCr { get; set; }
        public decimal PValue { get; set; }
    }
    public class POReportDTO
    {
        public int FacilityAutId { get; set; }
        public string FacilityAutName { get; set; }
        public string PONO { get; set; }
        public string POtype { get; set; }
        public string CODE { get; set; }
        public string ITEM_NAME { get; set; }
        public string PODate { get; set; }
        public string SupplierName { get; set; }
        public string TenderNo { get; set; }
        public decimal Quantity { get; set; }
        public decimal PValue { get; set; }
        public DateTime PDate { get; set; }
        public decimal Percentage { get; set; }
        public decimal BasicRate { get; set; }
    }

    //public class PaymentReportDTO
    //{
    //    public string PoNo { get; set; }
    //    public string PoDate { get; set; }
    //    public string OutwardNo { get; set; }
    //    public string SupplierName { get; set; }
    //    public string SanctionDate { get; set; }
    //    public decimal GrossAmt { get; set; }
    //    public decimal TotalDed { get; set; }
    //    public decimal TotalAddition { get; set; }
    //    public decimal ChequeAmt { get; set; }
    //    public string AidNo { get; set; }
    //    public string ChequeDate { get; set; }
    //    public string BudgetName { get; set; }
    //    public int BudgetId { get; set; }
    //    public int SanctionId { get; set; }
    //    public string PStatus { get; set; }
    //    public int SupplierId { get; set; }
    //    public string Potype { get; set; }
    //    public int PoId { get; set; }
    //    public int PaymentId { get; set; }
    //    public string TypeP { get; set; }
    //    public string AccountNo { get; set; }
    //    public DateTime AidDate { get; set; }
    //    public decimal AdminCharges { get; set; }
    //    public decimal ActChequeAmt { get; set; }
    //}
    public class PaymentUnionDTO
    {
        public string PoNo { get; set; }
        public string PoDate { get; set; }
        public string OutwardNo { get; set; }
        public string SupplierName { get; set; }
        public string SanctionDate { get; set; }
        public decimal GrossAmt { get; set; }
        public decimal TotalDed { get; set; }
        public decimal TotalAddition { get; set; }
        public decimal ChequeAmt { get; set; }
        public string AidNo { get; set; }
        public string ChequeDate { get; set; }
        public string BudgetName { get; set; }
        public int BudgetId { get; set; }
        public int SanctionId { get; set; }
        public string PStatus { get; set; }
        public int SupplierId { get; set; }
        public string Potype { get; set; }
        public int PoId { get; set; }
        public int PaymentId { get; set; }
        public string TypeP { get; set; }
        public DateTime AidDate { get; set; }
        public decimal AdminCharges { get; set; }
        public decimal ActChequeAmt { get; set; }
        public string AccountNo { get; set; }
    }
    public class SupplierPaymentSummaryDTO
    {
        public string Name { get; set; }
        public int SupplierId { get; set; }
        public int CountNOs { get; set; }
        public decimal ChequeAmt { get; set; }
        public decimal AdminC { get; set; }
        public string AidNo { get; set; }
        public string ChequeDT { get; set; }
        public string PaidOn { get; set; }
        public int PaymentId { get; set; }
        public string BudgetName { get; set; }
        public int BudgetId { get; set; }
        public decimal TotalCheque { get; set; }
        public string MobileNo { get; set; }
        public int LenMob { get; set; }
        public string EmailId { get; set; }
        public string Potype { get; set; }

    }
    public class ItemListsDTO
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
    }

    public class IndentItemSummaryDTO
    {
        public string Code { get; set; }
        public string ItemName { get; set; }
        public decimal Quantity { get; set; }
        public decimal BasicRate { get; set; }
        public decimal Percentage { get; set; }
        public decimal SingleUnitPrice { get; set; }
        public decimal TotalPOValue { get; set; }
    }

    public class IndentDetailsDTO
    {
        public int LocationId { get; set; }
        public string LocationName { get; set; }
        public int DP_DistrictID { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string UserType { get; set; }
        public string Designation { get; set; }

        public string Code { get; set; }
        public string ItemName { get; set; }

        public string OutwardNo { get; set; }
        public string PoDate { get; set; }

        public decimal Quantity { get; set; }
        public decimal BasicRate { get; set; }
        public decimal Percentage { get; set; }
        public decimal SingleUnitPrice { get; set; }
        public decimal TotalPOValue { get; set; }

        public string SupplierName { get; set; }
        public string MobileNo { get; set; }

        public string TenderNo { get; set; }
        public string TenderDate { get; set; }

        public string Status { get; set; }
        public string Remarks { get; set; }

        public int ItemId { get; set; }
        public int FinancialYearId { get; set; }
        public string Year { get; set; }

        public int TenderId { get; set; }
        public string PoNo { get; set; }
        public int SupplierId { get; set; }

        public int DirectorateId { get; set; }
        public int IndentFundId { get; set; }
        public int PoId { get; set; }
    }
    //public class TenderDto
    //{
    //    public int TenderId { get; set; }
    //    public string TenderNo { get; set; }
    //    public string TenderDate { get; set; }
    //    public string TenderDescription { get; set; }
    //    public int TotalItems { get; set; }
    //    public int Found { get; set; }
    //    public int NotFound { get; set; }
    //    public int PriceEntry { get; set; }
    //    public int Accept { get; set; }
    //    public int Reject { get; set; }
    //    public string Status { get; set; }
    //}

    public class TenderDto
    {
        // आपके पुराने फ़ील्ड्स
        public int TenderId { get; set; }
        public string TenderNo { get; set; }
        public string TenderDate { get; set; }
        public string TenderDescription { get; set; }
        public int TotalItems { get; set; }
        public int Found { get; set; }
        public int NotFound { get; set; }
        public int PriceEntry { get; set; }
        public int Accept { get; set; }
        public int Reject { get; set; }
        public string Status { get; set; }

        // SQL Query से जोड़े गए नए फ़ील्ड्स
        public string Flag { get; set; }
        public int FinancialYearId { get; set; }
        public int WarrantyYear { get; set; }
        public int ImportDays { get; set; }
        public int DomesticDays { get; set; }
        public string CoverA { get; set; }
        public string CoverB { get; set; }
        public string CoverDemo { get; set; }
        public string CoverC { get; set; }
        public int CsId { get; set; }
    }
}
