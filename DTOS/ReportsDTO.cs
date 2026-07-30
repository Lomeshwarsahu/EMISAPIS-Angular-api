
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

    public class IndentPoTenderStatusDTO
    {
        public int? user_id { get; set; }
        public string? user_name { get; set; }
        public string? year { get; set; }
        public string? consolidated_date { get; set; }
        public string? indent_con_no { get; set; }
        public string? description { get; set; }
        public string? item_code_as_per_tender { get; set; }
        public string? item_name { get; set; }
        public string? facility_aut_code { get; set; }
        public string? location_name { get; set; }
        public decimal? indentQTY { get; set; }
        public string? POYear { get; set; }
        public string? po_date { get; set; }
        public string? po_no { get; set; }
        public decimal? POQTY { get; set; }
        public long? POValueWithTax { get; set; }
        public decimal? BalancePO { get; set; }
        public int? facility_id { get; set; }
        public string? supplier_name { get; set; }
        public string? tender_no { get; set; }
        public decimal? basic_rate { get; set; }
        public decimal? percentage { get; set; }
        public int? facility_aut_id { get; set; }
        public int? item_id { get; set; }
        public decimal? dispatchQTY { get; set; }
        public decimal? installedQTY { get; set; }
    }

    public class IndentPoTenderStatusSummaryDTO
    {
        public string? Indent_Year { get; set; }
        public int? indent_consolidation_id { get; set; }
        public int? user_id { get; set; }
        public int? facility_aut_id { get; set; }
        public string? user_name { get; set; }
        public string? year { get; set; }
        public string? description { get; set; }
        public string? consolidated_date { get; set; }
        public int? totalIndentitems { get; set; }
        public decimal? indentqty { get; set; }
        public decimal? poqty { get; set; }
        public decimal? BalancePO { get; set; }
    }

    public class IndentPoTenderStatusDrillDownDTO
    {
        public int? user_id { get; set; }
        public string? user_name { get; set; }
        public string? year { get; set; }
        public string? consolidated_date { get; set; }
        public string? indent_con_no { get; set; }
        public string? description { get; set; }
        public string? item_code_as_per_tender { get; set; }
        public string? item_name { get; set; }
        public string? facility_aut_code { get; set; }
        public string? location_name { get; set; }
        public decimal? indentQTY { get; set; }
        public string? POYear { get; set; }
        public string? po_date { get; set; }
        public string? po_no { get; set; }
        public decimal? POQTY { get; set; }
        public long? POValueWithTax { get; set; }
        public decimal? BalancePO { get; set; }
        public int? facility_id { get; set; }
        public string? supplier_name { get; set; }
        public decimal? basic_rate { get; set; }
        public decimal? percentage { get; set; }
        public int? facility_aut_id { get; set; }
        public int? item_id { get; set; }
        public decimal? dispatchQTY { get; set; }
        public decimal? installedQTY { get; set; }
        public string? remarks { get; set; }
        public string? contract_end_date { get; set; }
        public string? tenderDT { get; set; }
        public string? tender_no_drill { get; set; }
        public string? finalstatus { get; set; }
    }

    public class IndentPoTenderSummaryDTO
    {
        public string? user_name { get; set; }
        public int? user_id { get; set; }
        public int? noscountItem { get; set; }
        public decimal? NoofEqIndent { get; set; }
        public decimal? NoofEqPO { get; set; }
        public decimal? NoofEqBal { get; set; }
        public decimal? netvalue { get; set; }
        public decimal? grossvalue { get; set; }
        public int? NoEQLivInTender { get; set; }
    }

    public class IndentPoTenderSummaryDrillDownDTO
    {
        public int? item_id { get; set; }
        public int? user_id { get; set; }
        public string? user_name { get; set; }
        public string? year { get; set; }
        public string? item_code_as_per_tender { get; set; }
        public string? item_name { get; set; }
        public int? nosdistinctnoscount { get; set; }
        public decimal? indentQTY { get; set; }
        public decimal? POQTY { get; set; }
        public decimal? BalancePO { get; set; }
        public decimal? basic_rate { get; set; }
        public decimal? netvalue { get; set; }
        public decimal? grossvalue { get; set; }
        public string? tenderstatus { get; set; }
    }

    public class POSummaryDTO
    {
        public string? Code { get; set; }
        public string? ItemName { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? BasicRate { get; set; }
        public decimal? Percentage { get; set; }
        public decimal? SingleUnitPrice { get; set; }
        public decimal? TotalPOValue { get; set; }
    }

    public class POSummaryDetailDTO
    {
        public int? LocationId { get; set; }
        public string? LocationName { get; set; }
        public string? ItemCode { get; set; }
        public string? ItemName { get; set; }
        public string? OutwardNo { get; set; }
        public string? PoDate { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? BasicRate { get; set; }
        public decimal? Percentage { get; set; }
        public decimal? SingleUnitPrice { get; set; }
        public decimal? TotalPOValue { get; set; }
        public string? SupplierName { get; set; }
        public string? MobileNo { get; set; }
        public string? TenderNo { get; set; }
        public string? TenderDate { get; set; }
        public string? Status { get; set; }
        public string? Remarks { get; set; }
        public string? DistrictName { get; set; }
    }

    public class POSummaryConsigneeHODTO
    {
        public string? OutwardNo { get; set; }
        public string? PoType { get; set; }
        public string? PoNo { get; set; }
        public string? PoDate { get; set; }
        public string? SupplierName { get; set; }
        public string? ItemCode { get; set; }
        public string? ItemName { get; set; }
        public string? DistrictName { get; set; }
        public string? LocationName { get; set; }
        public decimal? PoQty { get; set; }
        public decimal? SupplyQty { get; set; }
        public decimal? ReceivedQty { get; set; }
        public decimal? InstallQty { get; set; }
        public string? EqpType { get; set; }
        public decimal? TotalPrice { get; set; }
        public decimal? BasicRate { get; set; }
        public decimal? Percentage { get; set; }
        public decimal? SingleUnitPrice { get; set; }
    }

    public class BalanceDTO : ItemWiseDetailDTO
    {
        public decimal? BalanceQty { get; set; }
    }

    public class OpeningStockDTO
    {
        public string? UserName { get; set; }
        public int? UserId { get; set; }
        public int? Nos { get; set; }
    }

    public class OpeningStockDetailDTO
    {
        public int? ItemId { get; set; }
        public string? ItemName { get; set; }
        public string? ItemCode { get; set; }
        public string? MakeNo { get; set; }
        public string? Model { get; set; }
        public string? Make { get; set; }
        public string? LocationName { get; set; }
        public int? LocationId { get; set; }
    }

    public class TenderLiveStatusDTO
    {
        public int? NosTender { get; set; }
        public string? CStatus { get; set; }
        public int? CSID { get; set; }
        public int? Items { get; set; }
    }

    public class TenderLiveStatusDrillDownDTO
    {
        public string? TenderNo { get; set; }
        public string? TenderDate { get; set; }
        public string? TenderDescription { get; set; }
        public string? CStatus { get; set; }
        public string? ItemCode { get; set; }
        public string? ItemName { get; set; }
    }

    public class TenderWisePODetailDTO
    {
        public string? DirectorateName { get; set; }
        public string? SupplierName { get; set; }
        public int? TenderId { get; set; }
        public string? TenderNo { get; set; }
        public string? TenderDate { get; set; }
        public int? PoId { get; set; }
        public string? PoNo { get; set; }
        public string? PoDate { get; set; }
        public string? ContractDate { get; set; }
        public string? ContractEndDate { get; set; }
        public decimal? PoQty { get; set; }
        public decimal? SupplyQty { get; set; }
        public decimal? ReceiptQty { get; set; }
        public decimal? InstallQty { get; set; }
        public decimal? BasicRate { get; set; }
        public decimal? Percentage { get; set; }
        public decimal? SingleUnitPrice { get; set; }
        public string? ItemCode { get; set; }
        public string? ItemName { get; set; }
    }

    public class EMDDepositeDTO
    {
        public int? Id { get; set; }
        public int? SupId { get; set; }
        public string? Name { get; set; }
        public string? TenderNo { get; set; }
        public decimal? EMDAmt { get; set; }
        public string? EMDType { get; set; }
        public string? EMDDocumentNo { get; set; }
        public string? EMDDepositeDt { get; set; }
        public string? EntryDate { get; set; }
    }

    public class PaymentsCPReportIGMDTO
    {
        public string? Name { get; set; }
        public int? SupplierId { get; set; }
        public int? CountNOs { get; set; }
        public decimal? ChequeAmt { get; set; }
        public decimal? Adminc { get; set; }
        public string? AIDNO { get; set; }
        public string? ChequeDT { get; set; }
        public string? PAIDON { get; set; }
        public int? PaymentId { get; set; }
        public string? BudgetName { get; set; }
        public int? BudgetId { get; set; }
        public decimal? TotalCheque { get; set; }
        public string? MobileNo { get; set; }
        public string? EmailId { get; set; }
    }

    public class POPaidIGMDTO
    {
        public string? PoNo { get; set; }
        public string? PoDate { get; set; }
        public string? OutwardNo { get; set; }
        public string? SupplierName { get; set; }
        public string? SanctionDate { get; set; }
        public decimal? GrossAmt { get; set; }
        public decimal? TotalDed { get; set; }
        public decimal? TotalAddition { get; set; }
        public decimal? ChequeAmt { get; set; }
        public string? AIDNO { get; set; }
        public string? ChequeDate { get; set; }
        public string? BudgetName { get; set; }
        public int? BudgetId { get; set; }
        public int? SanctionId { get; set; }
        public string? PStatus { get; set; }
        public int? SupplierId { get; set; }
        public string? PoType { get; set; }
        public int? PoId { get; set; }
        public int? PaymentId { get; set; }
    }

    public class ReceiptPendingDTO
    {
        public string? DistrictName { get; set; }
        public string? LocationName { get; set; }
        public string? ItemCode { get; set; }
        public string? ItemName { get; set; }
        public string? SupplierName { get; set; }
        public string? PoNo { get; set; }
        public string? PoDate { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? DispatchedQty { get; set; }
        public decimal? ReceiptQty { get; set; }
        public decimal? InstallQty { get; set; }
        public int? PoId { get; set; }
        public int? ItemId { get; set; }
        public int? LocationId { get; set; }
        public string? Remarks { get; set; }
    }

    public class DispatchDetailDTO
    {
        public int? PoId { get; set; }
        public int? PoItemId { get; set; }
        public int? ItemId { get; set; }
        public decimal? Quantity { get; set; }
        public int? ConsigneeId { get; set; }
        public int? FinancialYearId { get; set; }
        public string? ItemName { get; set; }
        public string? ItemCode { get; set; }
        public decimal? SingleUnitPrice { get; set; }
        public string? LocationName { get; set; }
        public decimal? TotalPrice { get; set; }
        public string? PoDate { get; set; }
        public string? PoNo { get; set; }
        public int? UserId { get; set; }
    }

    public class ReportIndentPODetailDTO
    {
        public string? UserName { get; set; }
        public string? LocationName { get; set; }
        public string? Code { get; set; }
        public string? ItemName { get; set; }
        public string? IndentDT { get; set; }
        public string? IndentYear { get; set; }
        public string? TenderNo { get; set; }
        public string? TenderDate { get; set; }
        public string? SupplierName { get; set; }
        public string? RateContractDT { get; set; }
        public string? PoNo { get; set; }
        public string? PoDate { get; set; }
        public string? POYear { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? BasicRate { get; set; }
        public decimal? Percentage { get; set; }
        public decimal? SUPGST { get; set; }
        public decimal? TotalPOValueGST { get; set; }
    }

    public class FacilityAuthPOValueDTO
    {
        public int? FacilityAutId { get; set; }
        public string? FacilityAutName { get; set; }
        public string? PoType { get; set; }
        public int? NosPo { get; set; }
        public int? NosItem { get; set; }
        public decimal? TotalPOValueCr { get; set; }
        public decimal? PValue { get; set; }
    }

    public class EquipmentTagDTO
    {
        public string? DistrictName { get; set; }
        public string? LocationName { get; set; }
        public string? ItemName { get; set; }
        public string? ItemCode { get; set; }
        public string? Make { get; set; }
        public string? ModelNo { get; set; }
        public string? ReceiptDate { get; set; }
        public string? InstallationDate { get; set; }
        public string? WarrantyUpto { get; set; }
        public string? MakeSerialNo { get; set; }
        public string? EntryType { get; set; }
    }
}
