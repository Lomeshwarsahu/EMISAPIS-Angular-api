
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
        public string po_date { get; set; }
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

    public class BalanceStatusDTO
    {
        public int po_id { get; set; }
        public string? tender_no { get; set; }
        public string? year { get; set; }
        public string? outward_no { get; set; }
        public string? po_no { get; set; }
        public string? po_date { get; set; }
        public int directorate_id { get; set; }
        public string? directorate { get; set; }
        public string? authority { get; set; }
        public string? item_code { get; set; }
        public string? item_name { get; set; }
        public string? supplier { get; set; }
        public decimal? po_qty { get; set; }
        public decimal? supply_qty { get; set; }
        public decimal? receipt_qty { get; set; }
        public string? LastRDate { get; set; }
        public decimal? install_qty { get; set; }
        public string? po_type { get; set; }
        public decimal? balance_qty { get; set; }
    }

    public class PendingInstallDrillDownDHSRowDto
    {
        public string? district { get; set; }
        public string? DBStart_Name_En { get; set; }
        public string? location_name { get; set; }
        public string? item_code { get; set; }
        public string? item_code_as_per_tender { get; set; }
        public string? item_name { get; set; }
        public string? supplier_name { get; set; }
        public string? supplier { get; set; }
        public string? po_no { get; set; }
        public string? PoNo { get; set; }
        public string? po_date { get; set; }
        public string? po_dat { get; set; }
        public decimal? po_qty { get; set; }
        public decimal? DispatchedQTY { get; set; }
        public decimal? receipt_qty { get; set; }
        public decimal? receiptQTY { get; set; }
        public decimal? install_qty { get; set; }
        public decimal? insqty { get; set; }
        public decimal? pending_receipt { get; set; }
        public decimal? pending_install { get; set; }
        public int po_id { get; set; }
        public int item_id { get; set; }
        public int location_id { get; set; }
        public int? DP_DistrictID { get; set; }
        public string? remarks { get; set; }
    }

    public class RdlcDHSPendingRowDto
    {
        public string? district { get; set; }
        public string? DBStart_Name_En { get; set; }
        public string? location_name { get; set; }
        public string? item_code { get; set; }
        public string? item_code_as_per_tender { get; set; }
        public string? item_name { get; set; }
        public string? supplier_name { get; set; }
        public string? supplier { get; set; }
        public string? po_no { get; set; }
        public string? PoNo { get; set; }
        public string? po_date { get; set; }
        public string? po_dat { get; set; }
        public decimal? po_qty { get; set; }
        public decimal? DispatchedQTY { get; set; }
        public decimal? receipt_qty { get; set; }
        public decimal? receiptQTY { get; set; }
        public decimal? install_qty { get; set; }
        public decimal? insqty { get; set; }
        public decimal? pending_receipt { get; set; }
        public decimal? pending_install { get; set; }
        public string? directorate_name { get; set; }
        public string? directorate { get; set; }
        public int po_id { get; set; }
        public int item_id { get; set; }
        public int location_id { get; set; }
        public int? DP_DistrictID { get; set; }
        public string? remarks { get; set; }
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
        public int? NoofEqIndent { get; set; }
        public int? NoofEqPO { get; set; }
        public int? NoofEqBal { get; set; }
        public decimal? netvalue { get; set; }
        public decimal? grossvalue { get; set; }
        public int? NoEQLivInTender { get; set; }
    }

    public class IndentPoTenderSummaryDrillDownDTO
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
        public string? tender_no { get; set; }
        public string? finalstatus { get; set; }
    }

    public class PendingPoSupWiseDto
    {
        public string? item_code_as_per_tender { get; set; }
        public string? item_name { get; set; }
        public string? name { get; set; }
        public string? PONO { get; set; }
        public string? PODate { get; set; }
        public decimal? POQTY { get; set; }
        public decimal? Supplyqty { get; set; }
        public decimal? receiptQTY { get; set; }
        public decimal? insqty { get; set; }
        public string? status { get; set; }
        public string? PID { get; set; }
        public int? supplier_id { get; set; }
        public string? year { get; set; }
        public int? po_id { get; set; }
    }

    public class BalanceSupplierWiseDto
    {
        public int? supplier_id { get; set; }
        public string? supplier_name { get; set; }
        public int? total_pos { get; set; }
        public int? total_items { get; set; }
        public decimal? po_qty { get; set; }
        public decimal? dispatched_qty { get; set; }
        public decimal? receipt_qty { get; set; }
        public decimal? installed_qty { get; set; }
        public decimal? balance_dispatch { get; set; }
        public decimal? balance_receipt { get; set; }
        public decimal? balance_install { get; set; }
    }

    public class PaymentReportDto
    {
        public int? po_id { get; set; }
        public string? po_no { get; set; }
        public string? po_date { get; set; }
        public string? tender_no { get; set; }
        public string? supplier_name { get; set; }
        public string? item_name { get; set; }
        public decimal? totalprice { get; set; }
        public decimal? paid_amount { get; set; }
        public decimal? balance_amount { get; set; }
        public string? payment_status { get; set; }
    }

    public class OpeningStockSummaryDto
    {
        public string? user_name { get; set; }
        public int? user_id { get; set; }
        public int? nos { get; set; }
    }

    public class OpeningStockDrilldownDto
    {
        public int? existing_item_id { get; set; }
        public string? item_name { get; set; }
        public string? serial_no { get; set; }
        public string? location_name { get; set; }
        public string? user_name { get; set; }
        public string? entry_date { get; set; }
        public string? working_status { get; set; }
    }

    public class POSummaryDirectorateDto
    {
        public string? CODE { get; set; }
        public string? ITEM_NAME { get; set; }
        public decimal? quantity { get; set; }
        public decimal? basic_rate { get; set; }
        public decimal? percentage { get; set; }
        public decimal? single_unit_price { get; set; }
        public decimal? totalPOvalue { get; set; }
    }

    public class POSummaryDirectorateDrillDownDto
    {
        public int? location_id { get; set; }
        public string? location_name { get; set; }
        public int? DP_DistrictID { get; set; }
        public int? user_id { get; set; }
        public string? user_name { get; set; }
        public string? user_type { get; set; }
        public string? designation { get; set; }
        public string? CODE { get; set; }
        public string? ITEM_NAME { get; set; }
        public string? OUTWARD_NO { get; set; }
        public string? po_date { get; set; }
        public decimal? quantity { get; set; }
        public decimal? basic_rate { get; set; }
        public decimal? percentage { get; set; }
        public decimal? single_unit_price { get; set; }
        public decimal? totalPOvalue { get; set; }
        public string? SUPPLIER_NAME { get; set; }
        public string? mobile_no { get; set; }
        public string? TENDER_NO { get; set; }
        public string? TENDER_DATE { get; set; }
        public string? STATUS { get; set; }
        public string? REMARKS { get; set; }
        public int? item_id { get; set; }
        public int? FINANCIAL_YEAR_ID { get; set; }
        public string? YEAR { get; set; }
        public int? TENDER_ID { get; set; }
        public string? PO_NO { get; set; }
        public int? SUPPLIER_ID { get; set; }
        public int? directorate_id { get; set; }
        public int? indent_fund_id { get; set; }
        public int? PO_ID { get; set; }
        public string? DBStart_Name_En { get; set; }
    }

    public class POSummaryConsigneeHoDto
    {
        public string? outward_no { get; set; }
        public string? potype { get; set; }
        public string? po_no { get; set; }
        public string? podate { get; set; }
        public string? name { get; set; }
        public string? item_code_as_per_tender { get; set; }
        public string? item_name { get; set; }
        public string? DBStart_Name_En { get; set; }
        public int? consignee_id { get; set; }
        public string? location_name { get; set; }
        public decimal? po_qty { get; set; }
        public decimal? supply_qty { get; set; }
        public decimal? received_qty { get; set; }
        public decimal? Install_qty { get; set; }
        public int? po_id { get; set; }
        public string? Eqp_Type { get; set; }
        public int? categoryId { get; set; }
        public string? po_date { get; set; }
        public decimal? totalprice { get; set; }
        public decimal? basic_rate { get; set; }
        public decimal? percentage { get; set; }
        public decimal? single_unit_price { get; set; }
    }

    public class POReceiptSummaryDto
    {
        public int? po_id { get; set; }
        public string? tender_no { get; set; }
        public string? year { get; set; }
        public string? outward_no { get; set; }
        public string? po_no { get; set; }
        public string? pono { get; set; }
        public string? po_date { get; set; }
        public string? facility_aut_name { get; set; }
        public string? EQPTyp { get; set; }
        public string? item_code_as_per_tender { get; set; }
        public string? item_name { get; set; }
        public string? Supplier { get; set; }
        public decimal? POQTY { get; set; }
        public decimal? Supplyqty { get; set; }
        public decimal? receiptQTY { get; set; }
        public string? LastRDate { get; set; }
        public decimal? insqty { get; set; }
        public string? potype { get; set; }
        public int? cancellationdays { get; set; }
        public int? DaystakentoSupply { get; set; }
        public string? lstsupplydt { get; set; }
        public int? todays { get; set; }
    }

    public class FacStockCovidDto
    {
        public int? facility_id { get; set; }
        public string? facility_name { get; set; }
        public string? district_name { get; set; }
        public string? item_name { get; set; }
        public decimal? in_stock { get; set; }
        public decimal? installed { get; set; }
        public decimal? working { get; set; }
        public decimal? not_working { get; set; }
    }

    public class DispatchDetailDto
    {
        public int? dispatch_id { get; set; }
        public string? po_no { get; set; }
        public string? po_date { get; set; }
        public string? supplier_name { get; set; }
        public string? item_name { get; set; }
        public decimal? dispatched_qty { get; set; }
        public string? dispatch_date { get; set; }
        public string? chalan_no { get; set; }
        public string? location_name { get; set; }
    }

    public class ReportIndentPoDetailDto
    {
        public int? indent_id { get; set; }
        public string? indent_no { get; set; }
        public string? indent_date { get; set; }
        public string? item_name { get; set; }
        public decimal? indent_qty { get; set; }
        public string? po_no { get; set; }
        public decimal? po_qty { get; set; }
        public string? directorate_name { get; set; }
        public string? district_name { get; set; }
    }

    public class TenderLiveStatusDto
    {
        public int? nostender { get; set; }
        public string? CStatus { get; set; }
        public int? CSID { get; set; }
        public int? items { get; set; }
    }

    public class TenderLiveStatusDrilldownDto
    {
        public int? tender_id { get; set; }
        public string? tender_no { get; set; }
        public string? tender_date { get; set; }
        public string? CStatus { get; set; }
        public int? CSID { get; set; }
        public string? item_name { get; set; }
        public string? item_code_as_per_tender { get; set; }
    }

    public class TenderStatusReportDto
    {
        public int? tender_id { get; set; }
        public string? tender_no { get; set; }
        public string? tender_date { get; set; }
        public string? finalstatus { get; set; }
        public string? year { get; set; }
        public int? total_items { get; set; }
    }

    public class TenderStatusItemWiseDto
    {
        public string? tender_no { get; set; }
        public string? tender_date { get; set; }
        public string? item_code { get; set; }
        public string? item_name { get; set; }
        public string? supplier_name { get; set; }
        public decimal? basic_rate { get; set; }
        public decimal? percentage { get; set; }
        public string? contract_end_date { get; set; }
    }

    public class CoverAItemsReportDto
    {
        public int? tender_id { get; set; }
        public string? tender_no { get; set; }
        public string? item_code { get; set; }
        public string? item_name { get; set; }
        public string? supplier_name { get; set; }
        public string? status { get; set; }
    }

    public class EmdRefundReportDto
    {
        public string? tender_no { get; set; }
        public string? supplier_name { get; set; }
        public decimal? emd_amount { get; set; }
        public string? refund_status { get; set; }
        public string? refund_date { get; set; }
    }

    public class EelSuggestionReportDto
    {
        public int? specification_id { get; set; }
        public string? item_name { get; set; }
        public string? suggested_by { get; set; }
        public string? suggestion { get; set; }
        public string? created_date { get; set; }
    }

    public class IndentReportPocellDto
    {
        public int? indent_consolidation_id { get; set; }
        public string? item_code_as_per_tender { get; set; }
        public string? item_name { get; set; }
        public decimal? indent_qty { get; set; }
        public decimal? po_qty { get; set; }
        public decimal? balance_qty { get; set; }
    }

    public class FinancialYearDto
    {
        public int financial_year_id { get; set; }
        public string? year { get; set; }
    }

    public class UserLookupDto
    {
        public int user_id { get; set; }
        public string? user_name { get; set; }
        public string? designation { get; set; }
    }

    public class FacilityLookupDto
    {
        public int facility_id { get; set; }
        public string? facility_name { get; set; }
    }

    public class SupplierLookupDto
    {
        public int supplier_id { get; set; }
        public string? supplier_name { get; set; }
    }

    public class TenderLookupDto
    {
        public int tender_id { get; set; }
        public string? tender_no { get; set; }
    }

    public class ComplainReportDto
    {
        public int? complaint_id { get; set; }
        public string? complaint_no { get; set; }
        public string? complaint_date { get; set; }
        public int? item_id { get; set; }
        public string? complaint_details { get; set; }
        public int? location_id { get; set; }
        public int? supplier_id { get; set; }
        public int? complaints_trouble_id { get; set; }
        public string? not_function_date { get; set; }
        public string? item_name { get; set; }
        public string? location_name { get; set; }
        public string? item_code_as_per_tender { get; set; }
        public int? user_id { get; set; }
        public string? serial_no { get; set; }
        public string? supplier { get; set; }
        public string? email_id { get; set; }
        public string? mobile_no { get; set; }
    }

    public class PopaidReportIgmDto
    {
        public string? poNo { get; set; }
        public string? poDate { get; set; }
        public string? outwardNo { get; set; }
        public string? supplierName { get; set; }
        public string? sanctionDate { get; set; }
        public decimal? grossAmount { get; set; }
        public decimal? totalDeduction { get; set; }
        public decimal? totalAddition { get; set; }
        public decimal? chequeAmount { get; set; }
        public string? aidNo { get; set; }
        public string? chequeDate { get; set; }
        public string? budgetName { get; set; }
        public string? poType { get; set; }
    }

    public class PoPaidReportDto
    {
        public string? poNo { get; set; }
        public string? poDate { get; set; }
        public string? outwardNo { get; set; }
        public string? supplierName { get; set; }
        public string? sanctionDate { get; set; }
        public decimal? grossAmount { get; set; }
        public decimal? totalDeduction { get; set; }
        public decimal? totalAddition { get; set; }
        public decimal? chequeAmount { get; set; }
        public string? aidNo { get; set; }
        public string? chequeDate { get; set; }
        public string? budgetName { get; set; }
        public string? poType { get; set; }
    }

    public class EmdDepositeReportDto
    {
        public int? id { get; set; }
        public int? SupId { get; set; }
        public string? name { get; set; }
        public string? TenderNo { get; set; }
        public decimal? EMDAmt { get; set; }
        public string? EMDType { get; set; }
        public string? EMDDocumentNo { get; set; }
        public string? EMDDocument { get; set; }
        public string? EMDDepositeDt { get; set; }
        public string? EntryDate { get; set; }
    }

    public class POSummaryPOWiseDetailDto
    {
        public int? facility_aut_id { get; set; }
        public string? facility_aut_name { get; set; }
        public string? PONO { get; set; }
        public string? POtype { get; set; }
        public string? CODE { get; set; }
        public string? ITEM_NAME { get; set; }
        public string? po_date { get; set; }
        public string? SUPPLIER_NAME { get; set; }
        public string? TENDER_NO { get; set; }
        public decimal? quantity { get; set; }
        public decimal? PValue { get; set; }
        public string? PDate { get; set; }
        public decimal? percentage { get; set; }
        public decimal? basic_rate { get; set; }
    }

    public class SupplierPaymentSummaryReportDto
    {
        public string? name { get; set; }
        public int? supplier_id { get; set; }
        public int? countNOs { get; set; }
        public decimal? ChequeAmt { get; set; }
        public decimal? adminc { get; set; }
        public string? AIDNO { get; set; }
        public string? chequeDT { get; set; }
        public string? PAIDON { get; set; }
        public int? PAYMENTID { get; set; }
        public string? BUDGETNAME { get; set; }
        public int? BUDGETID { get; set; }
        public decimal? TotalCheque { get; set; }
        public string? mobile_no { get; set; }
        public string? email_id { get; set; }
    }

    public class TenderWisePoDetailDto
    {
        public string? facility_aut_name { get; set; }
        public string? supplier_name { get; set; }
        public int? tender_id { get; set; }
        public string? tender_no { get; set; }
        public string? tender_date { get; set; }
        public int? po_id { get; set; }
        public string? pono { get; set; }
        public string? POdate { get; set; }
        public string? contract_date { get; set; }
        public string? contract_end_date { get; set; }
        public decimal? poqty { get; set; }
        public decimal? Supplyqty { get; set; }
        public decimal? receiptQTY { get; set; }
        public decimal? insqty { get; set; }
        public decimal? basic_rate { get; set; }
        public decimal? percentage { get; set; }
        public decimal? single_unit_price { get; set; }
        public string? item_code_as_per_tender { get; set; }
        public string? item_name { get; set; }
    }

    public class EquipmentTagReportDto
    {
        public string? district { get; set; }
        public string? location { get; set; }
        public string? itemName { get; set; }
        public string? itemCode { get; set; }
        public string? make { get; set; }
        public string? modelNo { get; set; }
        public string? receiptDate { get; set; }
        public string? installationDate { get; set; }
        public string? warrantyUpto { get; set; }
        public string? serialNo { get; set; }
        public string? isTagged { get; set; }
    }
}
