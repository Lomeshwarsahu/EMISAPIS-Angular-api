using System.ComponentModel.DataAnnotations;
namespace EMISAPIS.DTOS
{
    public class POCellDTOS
    {
    }
   
    public class FacilityUserListDto
    {
        public int FacilityTypeId { get; set; } // f.facility_type_id
        public int NoOfConsignee { get; set; } // count(*) as no_of_consignee
        public string FacilityTypeName { get; set; } = string.Empty; // fm.facility_type_name
        public int NoOfUser { get; set; } // isnull(U.userid,0) No_of_user
        public int Authority { get; set; } // f.authority
    }
    public class ProgramFacilityReportDto
    {
        public string ProgramName { get; set; } = string.Empty; // m.ProgramName
        public string FacilityAutCode { get; set; } = string.Empty; // f.facility_aut_code
        public string CreatedOn { get; set; } = string.Empty; // formatted date string (DD/MM/YYYY)
    }



public class SaveProgramRequestDto
    {
        [Required(ErrorMessage = "Please fill Program Name")]
        public string ProgramName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please Select Directorate Name")]
        // Agar dropdown se value string me aa rahi hai toh string rakhein, agar ID number hai toh int use karein
        public string DirectorateId { get; set; } = string.Empty;
    }
    public class TenderDropdownDto
    {
        public string TenderNo { get; set; } = string.Empty;
        public int TenderId { get; set; }
        public DateTime? TenderDate { get; set; }
    }
    public class PoDashboardRequestDto
    {
        public string YearId { get; set; } = "0";      // ddlYear.SelectedValue
        public string TenderId { get; set; } = "0";    // ddlTender.SelectedValue
        public string SupplierId { get; set; } = "";   // ddlSupplier.SelectedValue
        public string StatusId { get; set; } = "0";    // ddlStatus.SelectedValue
    }
    public class PoDashboardResponseDto
    {
        public string Year { get; set; } = string.Empty;
        public string OutwardNo { get; set; } = string.Empty;
        public string PoNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty; // CONVERT 103 String (DD/MM/YYYY)
        public decimal PoValue { get; set; }
        public long TotalPoValue { get; set; }
        public long PoQty { get; set; }
        public int PoItemsKey { get; set; } // Represent nosConsignee
        public string Status { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string MobileNo { get; set; } = string.Empty;
        public string EmailId { get; set; } = string.Empty;
        public string PoType { get; set; } = string.Empty; // Normal PO / Covid Po
        public string FilePathAccessories { get; set; } = string.Empty;
        public string FilePathReagent { get; set; } = string.Empty;
        public string IsPayment { get; set; } = string.Empty; // Paid / Not Paid
        public string ItemName { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty; // ITEM_CODE_AS_PER_TENDER
        public string TenderNo { get; set; } = string.Empty;
        public int TenderId { get; set; }
        public int SupplierId { get; set; }
        public int PoId { get; set; }
        public int DirectorateId { get; set; }
        public int FinancialYearId { get; set; }
        public int ItemId { get; set; }
    }

public class ContractItemReportDto
    {
        public int ItemId { get; set; } // ci.item_id
        public string ItemName { get; set; } = string.Empty; // Concatenated custom name pattern
        public decimal SingleUnitPrice { get; set; } // single_unit_price / ci.single_unit_price
        public string TenderNo { get; set; } = string.Empty; // t.tender_no
        public DateTime? TenderDate { get; set; } // t.tender_date
        public int TenderId { get; set; } // t.tender_id
        public decimal BasicRate { get; set; } // ci.basic_rate
        public decimal Percentage { get; set; } // ci.percentage
        public string ItemCodeAsPerTender { get; set; } = string.Empty; // m.item_code_as_per_tender
        public string CoreItemName { get; set; } = string.Empty; // m.item_name
        public string SupplierName { get; set; } = string.Empty; // s.name
        public string EmailId { get; set; } = string.Empty; // s.email_id
        public string MobileNo { get; set; } = string.Empty; // s.mobile_no
        public string IsExtended { get; set; } = string.Empty; // ci.is_extended
        public DateTime? NextDayAfterExpiry { get; set; } // Calculated Expiry Date token
    }
  

public class GeneratePoRequestDto
    {
        public int PoId { get; set; } = 0; // Request.QueryString["PO_ID"]
        public string AuthorityValue { get; set; } = string.Empty; // ddlAuthorities.SelectedValue
        public int FundSourceValue { get; set; } = 0; // ddlfundSource.SelectedValue
        public int FundSourceSelectedIndex { get; set; } = 0; // Validation wrapper
        public int MedicalHospitalValue { get; set; } = 0; // ddlMediHospit.SelectedValue
        public int MedicalHospitalSelectedIndex { get; set; } = 0; // Validation wrapper
        public int CovidPoValue { get; set; } = 0; // ddlCovidPO.SelectedValue
        public int CovidPoSelectedIndex { get; set; } = 0; // Validation wrapper
        public int SupplyDaysSelectedIndex { get; set; } = 0; // ddlSupplyDays.SelectedIndex
        public int AuthoritySelectedIndex { get; set; } = 0; // ddlAuthorities.SelectedIndex

        // Core Transaction Database Insertion Fields
        public int TenderId { get; set; } // lblTenderID.Text
        public string PoDateStr { get; set; } = string.Empty; // txtPODate.Text (Format: YYYY-MM-DD)
        public int SupplierId { get; set; } // lblSupplierID.Text
        public int FinancialYearId { get; set; } // ddlyear.SelectedValue
        public string FinancialYearText { get; set; } = string.Empty; // ddlyear.SelectedItem.Text
        public int DirectorateId { get; set; } // lblDirectorateID.Text
        public int ProgramId { get; set; } // ddlprogram.SelectedValue
        public string RcItemsSelectedValue { get; set; } = string.Empty; // ddlRCItems.SelectedValue
        public string GemPoText { get; set; } = string.Empty; // txtgempo.Text
    }
    public class ProgramDropdownDto
    {
        public int ProgramId { get; set; } // ProgramID
        public string ProgramName { get; set; } = string.Empty; // ProgramName
        public string FacilityAutCode { get; set; } = string.Empty; // f.facility_aut_code
    }

public class SupplyDaysReportDto
    {
        public int DaysTaken { get; set; } // t.domestic_days / t.import_days AS daystaken
        public string IsExtended { get; set; } = string.Empty; // ci.is_extended
        public DateTime? CalculatedEndDate { get; set; } // Calculated DATEADD column
    }
    public class MappedFundsRequestDto
    {
        public string FacilityAutId { get; set; } = string.Empty; // facilityautid
        public string DmeUserId { get; set; } = "0"; // dmeuserid
    }
    public class MappedFundsResponseDto
    {
        public string FacilityAutName { get; set; } = string.Empty; // f.facility_aut_name
        public string BudgetName { get; set; } = string.Empty; // b.BUDGETNAME
        public int MapId { get; set; } // m.MapId
        public int BudgetId { get; set; } // b.BUDGETID
        public int FacilityAutId { get; set; } // f.facility_aut_id
        public int? DmeUserId { get; set; } // m.DMEUserid
        public string DmeUserName { get; set; } = string.Empty; // DMEUserName (with ISNULL fallback)
    }
    public class AwardContractDropdownDto
    {
        public int AwardOfContractId { get; set; } // ac.award_of_contract_id
        public string ContractNumber { get; set; } = string.Empty; // Supplier Name + (Contract Number) Concatenated
    }

    // 1. Grid Data List Response DTO
    public class ContractGridDetailDto
    {
        public int AwardOfContractId { get; set; }
        public int ContractItemId { get; set; }
        public int ItemId { get; set; }
        public string ItemCodeE { get; set; } = string.Empty;
        public string ItemNameE { get; set; } = string.Empty;
        public decimal BasicRate { get; set; }
        public decimal Percentage { get; set; }
        public decimal SingleUnitPrice { get; set; }
        public string Model { get; set; } = string.Empty;
        public string ContractDate { get; set; } = string.Empty; // Formatted 103 String
        public string ContractDuration { get; set; } = string.Empty;
        public string ContractEndDate { get; set; } = string.Empty; // Formatted 103 String
        public string SupplierName { get; set; } = string.Empty;
        public string TenderNo { get; set; } = string.Empty;
        public int TenderId { get; set; }
        public string ContractNewEndDate { get; set; } = string.Empty; // Formatted 105 String
        public string Remark { get; set; } = string.Empty;
    }

    // 2. Populate Panel Form Request Payload DTO
    public class ContractPanelInfoDto
    {
        public string ContractEndDate { get; set; } = string.Empty; // Formatted 105 String
        public string Remark { get; set; } = string.Empty;
    }

public class ContractExtendRequestDto
    {
        public int AwardOfContractId { get; set; } // ddlContract.SelectedValue
        public string ContractNewEndDate { get; set; } = string.Empty; // txtContNewEndDate.Text (DD-MM-YYYY)
        public string Remark { get; set; } = string.Empty; // txtRmrk.Text
    }

public class PoExtensionReportDto
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public int ItemId { get; set; }
        public int PoId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string OutwardNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty; // 103 Format (DD/MM/YYYY)
        public string PoNo { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public int NoOfConsignee { get; set; }
        public decimal BasicRate { get; set; }
        public decimal Percentage { get; set; }
        public decimal SingleUnitPrice { get; set; }
        public decimal TotalPoValue { get; set; }
        public string TenderNo { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Sd { get; set; } = string.Empty;
        public string SubmissionStatus { get; set; } = string.Empty;
        public int TrancheDays { get; set; }
        public string PoEndDate { get; set; } = string.Empty; // Calculated 103 String

        // Extension Letter Details Sub-fields
        //public int LetterId { get; set; }
        public string LetterId { get; set; } = string.Empty;
        public int ExtensionId { get; set; } 
        public string Remark { get; set; } = string.Empty;
        public int Days { get; set; }
        public string ExtendedDate { get; set; } = string.Empty; // 105 Format (DD-MM-YYYY)
        public string LastPoEndDate { get; set; } = string.Empty; // 105 Format (DD-MM-YYYY)
        public string Path { get; set; } = string.Empty;
        public string LetterDate { get; set; } = string.Empty;
        public string LetterNo { get; set; } = string.Empty;
        public string SysGenApplyDate { get; set; } = string.Empty; // 105 Format (DD-MM-YYYY)
        public string LetterStatus { get; set; } = string.Empty; // ltr.status
    }
 

public class IndentConsolidationReportDto
    {
        public int EquipmentCount { get; set; } // count(B.item_id)
        public int IndentConsolidationId { get; set; } // A.INDENT_CONSOLIDATION_ID
        public string Description { get; set; } = string.Empty; // A.description
        public int UserId { get; set; } // A.USER_ID
        public int DirectorateId { get; set; } // A.DIRECTORATE_ID
        public int FinancialYearId { get; set; } // A.FINANCIAL_YEAR_ID
        public decimal ProposedQty { get; set; } // SUM(B.PROPOSED_QTY)
        public string IndentConNo { get; set; } = string.Empty; // A.indent_con_no
        public string ConsolidatedDate { get; set; } = string.Empty; // Formatted CONSOLIDATED_DATE (103)
        public decimal FinalQty { get; set; } // SUM(B.FINAL_QTY)
        public string EStatus { get; set; } = string.Empty; // Case statement status
        public string UploadStatus { get; set; } = string.Empty; // Nested select file status
        public DateTime? CreatedOn { get; set; } // A.CreatedOn
    }

public class SaveIndentConsolidationRequestDto
    {
        public int FundId { get; set; } // ddlFund.SelectedValue
        public string IndentDescription { get; set; } = string.Empty; // txtIndentDescription.Text (Letter No)
        public string IndentDateStr { get; set; } = string.Empty; // txtDate.Text (Format: YYYY-MM-DD)
        public int UserId { get; set; } // sDet.DistId
        public int DirectorateId { get; set; } // ddlFacilityAuth.SelectedValue
        public int FinancialYearId { get; set; } // ddlYear.SelectedValue
    }
    public class BudgetDropdownDto
    {
        public int BudgetId { get; set; } // BUDGETID
        public string BudgetName { get; set; } = string.Empty; // BUDGETNAME
    }
    public class IndentSingleRecordDto
    {
        public int UserId { get; set; } // user_id
        public int DirectorateId { get; set; } // directorate_id
        public string Year { get; set; } = string.Empty; // f.year
        public int FinancialYearId { get; set; } // i.financial_year_id
        public string IndentConNo { get; set; } = string.Empty; // indent_con_no
        public string ConsolidatedDate { get; set; } = string.Empty; // Formatted consolidated_date (103)
    }
    public class SearchDropdownDto
    {

        public int ItemId { get; set; } // item_id
        public string ItemName { get; set; } = string.Empty; // item_name
        public string itemcode_as_per_tender { get; set; } = string.Empty;
        public string CMEEEL { get; set; } = string.Empty;
    }
 

public class IndentConsolidationItemDetailDto
    {
        public int IndentConsolidationId { get; set; } // ic.indent_consolidation_id
        public string IndentConNo { get; set; } = string.Empty; // ic.indent_con_no
        public string ConsolidatedDate { get; set; } = string.Empty; // ic.consolidated_date
        public string Description { get; set; } = string.Empty; // ic.description
        public int ItemId { get; set; } // m.item_id
        public string ItemCodeAsPerTender { get; set; } = string.Empty; // m.item_code_as_per_tender
        public string ItemName { get; set; } = string.Empty; // m.item_name
        public string RcEndDate { get; set; } = string.Empty; // g.RC_END_DATE (Formatted)
        public decimal EstimatedCost { get; set; } // calculated ESTIMATED_COST
        public decimal FinalQty { get; set; } // isnull(ind.indent,0)
        public string Status { get; set; } = string.Empty; // ici.status
    }


public class IndentConsolidationDetailDto
    {
        public int ItemId { get; set; } // A.ITEM_ID
        public string ItemCodeAsPerTender { get; set; } = string.Empty; // A.ITEM_CODE_AS_PER_TENDER
        public string RcEndDate { get; set; } = string.Empty; // g.RC_END_DATE (Formatted)
        public string ItemDesc { get; set; } = string.Empty; // A.ITEM_DESC
        public string ItemName { get; set; } = string.Empty; // A.ITEM_NAME
        public string FileName { get; set; } = string.Empty; // B.FILE_NAME
        public int? UploadDocId { get; set; } // B.UPLOAD_DOC_ID
        public string UploadFolderName { get; set; } = string.Empty; // B.UPLOAD_FOLDER_NAME
        public decimal SingleUnitPrice { get; set; } // isnull(G.SINGLE_UNIT_PRICE,0)
        public decimal EstimatedCost { get; set; } // calculated ESTIMATED_COST
        public int ContractItemId { get; set; } // COALESCE(G.CONTRACT_ITEM_ID,0)
        public int? IndentConsItemsId { get; set; } // s.indent_cons_items_id
        public int? IndentConsolidationId { get; set; } // q.indent_consolidation_id
        public decimal ProposedQty { get; set; } // s.proposed_qty
        public string OtherFundName { get; set; } = string.Empty; // s.other_fund_name
        public decimal FinalQtyI { get; set; } // s.final_qty
        public decimal FinalQty { get; set; } // isnull(ind.indent,0)
        public int? IndentFundId { get; set; } // s.indent_fund_id
        public int? IndentMonthId { get; set; } // s.indent_month_id
        public string IndentFundName { get; set; } = string.Empty; // r.indent_fund_name
        public string IndentMonth { get; set; } = string.Empty; // r1.indent_month
        public string Status { get; set; } = string.Empty; // s.status
    }
    public class ConsigneeIndentDetailDto
    {
        public int LocationId { get; set; } // l.location_id
        public string LocationName { get; set; } = string.Empty; // l.location_name
        public int DpDistrictId { get; set; } // l.DP_DistrictID
        public string IndentQuantity { get; set; } = "0"; // calculated indent_quantity
        public int? IndentItemId { get; set; } // fac.indent_item_id
        public int? FacilityTypeId { get; set; } // facility_type_id
    }




public class BulkConsigneeSaveRequestDto
    {
        public int ItemId { get; set; } // Request.QueryString["itemId"]
        public int FinancialYearId { get; set; } // Request.QueryString["finyrId"]
        public int IndConId { get; set; } // Request.QueryString["indConId"]
        public string IndentNumber { get; set; } = string.Empty; // lblIndent_Number.Text
        public List<ConsigneeRowItemDto> ConsigneeRows { get; set; } = new List<ConsigneeRowItemDto>();
    }

    public class ConsigneeRowItemDto
    {
        public int LocationId { get; set; } // lblFacilityId
        public int IndentItemId { get; set; } // lblIndentItemId
        public decimal IndentQuantity { get; set; } // txtIndeQty
    }
    public class CompleteIndentRequestDto
    {
        public int ItemId { get; set; } // Request.QueryString["itemId"]
        public int IndConId { get; set; } // Request.QueryString["indentId"]
    }
   

public class SavedDataGridDto
    {
        public int IndentConsolidationId { get; set; } // id.indent_consolidation_id
        public string Description { get; set; } = string.Empty; // id.description
        public string IndentDate { get; set; } = string.Empty; // Indent_date
        public int LocationId { get; set; } // l.location_id
        public string LocationName { get; set; } = string.Empty; // l.location_name
        public int CurrentStock { get; set; } // CurrentStock
        public int Pipeline { get; set; } // Pipeline
        public int ItemId { get; set; } // im.item_id
        public string ItemName { get; set; } = string.Empty; // itemname
        public string EqpCode { get; set; } = string.Empty; // eqpcode
        public int FacilityId { get; set; } // i.facility_id
        public decimal IndentQuantity { get; set; } // im.indent_quantity
        public decimal EstimatedCost { get; set; } // m.estimated_cost
        public long Value { get; set; } // Value
        public string Remarks { get; set; } = string.Empty; // i.remarks
        public int? HeadId { get; set; } // d.headID
        public string HeadName { get; set; } = string.Empty; // d.headName
        public string RCEndDate { get; set; } = string.Empty; // RCEndDate
        public string Supplier { get; set; } = string.Empty; // Supplier
        public string Make { get; set; } = string.Empty; // make
        public string Model { get; set; } = string.Empty; // model
        public decimal? PriceIncGST { get; set; } // PriceIncGST
        public string TenderDT { get; set; } = string.Empty; // tenderDT
        public string TenderNo { get; set; } = string.Empty; // tender_no
        public string FinalStatus { get; set; } = string.Empty; // finalstatus
        public string POYear { get; set; } = string.Empty; // POYear
        public string PODate { get; set; } = string.Empty; // po_date
        public string PONo { get; set; } = string.Empty; // po_no
        public decimal POQTY { get; set; } // POQTY
        public long POValueWithTax { get; set; } // POValueWithTax
        public string ItemRemarks { get; set; } = string.Empty; // itemremarks
        public int IndentConsItemsId { get; set; } // iim.indent_cons_items_id
    }
    public class TermConditionGridDto
    {
        public int TermConditionId { get; set; } // tc.term_condition_id
        public string TermCondition { get; set; } = string.Empty; // tc.term_condition
        public int TenderId { get; set; } // tc.tender_id
        public string TenderNo { get; set; } = string.Empty; // t.tender_no
    }
    public class TenderSummaryRdlcDto
    {
        public string TenderNo { get; set; } = string.Empty;
        public string LiveDT { get; set; } = string.Empty;
        public string TLast { get; set; } = string.Empty;
        public int NoSitems { get; set; }
        public string FinalStatus { get; set; } = string.Empty;
        public string NosItemsA { get; set; } = string.Empty;
        public string AItemsSupplier { get; set; } = string.Empty;
        public string CoverA { get; set; } = string.Empty;
        public int? COVALastDays { get; set; }
        public string NositemsDA { get; set; } = string.Empty;
        public string AdaItemsSupplier { get; set; } = string.Empty;
        public string ObjClaimLastDate { get; set; } = string.Empty;
        public string ObjCStartDT { get; set; } = string.Empty;
        public string COVBDT { get; set; } = string.Empty;
        public string COVCDT { get; set; } = string.Empty;
        public int NoofPriceFound { get; set; }
        public int NosAccepted { get; set; }
        public int NosRejected { get; set; }
        public int DaysTakenFromLiveDT { get; set; }
        public int? COVAToObjStartDays { get; set; }
        public int? OBJDays { get; set; }
        public int? ClaimEndToBDays { get; set; }
        public int? CovBToCovCDays { get; set; }
        public int Csid { get; set; }
        public int TenderId { get; set; }
        public int DaysClosed { get; set; }
        public int Show { get; set; }
        public int NosRC { get; set; }
    }

}

