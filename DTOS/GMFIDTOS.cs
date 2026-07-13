namespace EMISAPIS.DTOS
{
    public class GMFIDTOS
    {
    }
    public class PurchaseOrderDropdownDto
    {
        public int PoId { get; set; } // po_id
        public string PoNo { get; set; } = string.Empty; // po_no
    }
    public class ConsigneeDropdownDto
    {
        public int ConsigneeId { get; set; } // i.consignee_id
        public string LocationName { get; set; } = string.Empty; // l.location_name
    }
    public class ReceiptItemTrackingDto
    {
        public int ReceiptId { get; set; } // r.receipt_id
        public string RecievedDate { get; set; } = string.Empty; // recieved_date (DD-MM-YYYY)
        public string InstallationDate { get; set; } = string.Empty; // installation_date
        public string WarentyFrom { get; set; } = string.Empty; // warenty_from
        public string WarentyTo { get; set; } = string.Empty; // warenty_to
        public int ItemDetailId { get; set; } // ri.item_detail_id
        public int LocationId { get; set; } // r.location_id
        public int PoId { get; set; } // r.po_id
        public int FinancialYearId { get; set; } // po.financial_year_id
        public string DispatchDate { get; set; } = string.Empty; // dispatch_date
        public string ChallanDate { get; set; } = string.Empty; // challan_date
        public string InvoiceDate { get; set; } = string.Empty; // invoice_date
    }

  

public class UpdateReceivedDateRequestDto
    {
        public int ReceiptId { get; set; } // txtreceiptid.Text
        public string ReceivedDate { get; set; } = string.Empty; // txtReceivedDate.Text (Format: DD-MM-YYYY)
        public string DispatchDate { get; set; } = string.Empty; // txtdispdt.Text (Format: DD-MM-YYYY)
        public string ChallanDate { get; set; } = string.Empty; // txtchallandt.Text (Format: DD-MM-YYYY)
        public string InvoiceDate { get; set; } = string.Empty; // txtinvdt.Text (Format: DD-MM-YYYY)
    }
    public class UpdateInstallationDateRequestDto
    {
        public int ReceiptId { get; set; } // txtreceiptid.Text
        public string InstallationDate { get; set; } = string.Empty; // txtInstallationDate.Text (Format: YYYY-MM-DD)
        public string ReceivedDate { get; set; } = string.Empty; // txtReceivedDate.Text (Format: YYYY-MM-DD)
        public string WarrantyFrom { get; set; } = string.Empty; // txtWarrantyFrom.Text (Format: YYYY-MM-DD)
        public string WarrantyTo { get; set; } = string.Empty; // txtWarrantyTo.Text
    }
    public class SupplierGridDto
    {
        public int SupplierId { get; set; } // SupplierID
        public string SupplierCode { get; set; } = string.Empty; // SupplierCode
        public string SupplierName { get; set; } = string.Empty; // SupplierName
        public string CountryName { get; set; } = "India"; // CountryName
        public string IsActive { get; set; } = string.Empty; // IsActive (a.is_register)
        public string Deletable { get; set; } = "True"; // Calculated Subquery (True/False)
        public string Address1 { get; set; } = string.Empty; // Address1
        public string Address2 { get; set; } = string.Empty; // Address2
        public string Address3 { get; set; } = string.Empty; // Address3
        public string City { get; set; } = string.Empty;
        public string Zip { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty; // a.mobile_no
        public string Phone { get; set; } = string.Empty; // Phone
        public string Fax { get; set; } = string.Empty; // Fax
        public string Email { get; set; } = string.Empty; // Email
    }

    public class SupplierBankAccDto
    {
        public int BankAccountId { get; set; } // BankAccountID
        public int SupplierId { get; set; } // SupplierID
        public string AccountNo { get; set; } = string.Empty; // AccountNo
        public string AccountName { get; set; } = string.Empty; // AccountName
        public string BankName { get; set; } = string.Empty; // BankName
        public string Branch { get; set; } = string.Empty; // Branch
        public string IfscCode { get; set; } = string.Empty; // IFSCCode
        public string MicrCode { get; set; } = string.Empty; // MICRCode
        public int DefaultAcc { get; set; } // DefaultAcc (1 or 0)
        public string DefaultAccText { get; set; } = "No";
        public string Remarks { get; set; } = string.Empty; // Remarks
    }
    public class SupplierGstDto
    {
        public int Gstid { get; set; } // gstid
        public int Supplierid { get; set; } // supplierid
        public string Gstno { get; set; } = string.Empty; // gstno
        public string Flag { get; set; } = "Y"; // flag
    }
    public class CgmscBankAccDto
    {
        public int Bankid { get; set; } // bankid
        public string Accountno { get; set; } = string.Empty; // ACCOUNTNO
        public string Accountname { get; set; } = string.Empty; // ACCOUNTNAME
        public string Bankname { get; set; } = string.Empty; // BANKNAME
        public string Branch { get; set; } = string.Empty; // BRANCH
        public string Ifsccode { get; set; } = string.Empty; // IFSCCODE
        public string Remarks { get; set; } = string.Empty; // REMARKS
        public string Isactive { get; set; } = "Y"; // ISACTIVE
    }
    public class FundMasterDto
    {
        public int Budgetid { get; set; } 
        public string Budgetname { get; set; } = string.Empty; 
        public int Orderid { get; set; } 
    }
    public class DirectorateDropdownDto
    {
        public int FacilityAutId { get; set; } // facility_aut_id
        public string FacilityAutName { get; set; } = string.Empty; // facility_aut_name
    }

    public class DmeInstituteDropdownDto
    {
        public int UserId { get; set; } // user_id
        public string Username { get; set; } = string.Empty; // username
    }

    public class FundMappingExecutionDto
    {
        public int Budgetid { get; set; } // budgetid
        public string Budgetname { get; set; } = string.Empty; // BUDGETNAME
        public int Cnt { get; set; } // Flag checker (1 = Mapped, 0 = Unmapped)
        public int MapId { get; set; } // MapId for downstream lists
        public string FacilityAutName { get; set; } = string.Empty; // facility_aut_name
        public string DmeUserName { get; set; } = string.Empty; // DMEUserName
    }

    public class BulkFundMapSubmissionDto
    {
        public int DirectorateId { get; set; } // facility_aut_id
        public int InstituteId { get; set; } // DMEUserid (Optional context for id 12)
        public List<int> SelectedBudgetIds { get; set; } = new List<int>(); // List of checked boxes items
    }
    public class FundReceiptGridDto
    {
        public int Bgid { get; set; } // bgid
        public int Budgetid { get; set; } // BUDGETID
        public string UserName { get; set; } = string.Empty; // user_name (Directorate)
        public string FacName { get; set; } = string.Empty; // facName (College/Hospital)
        public string BudgetName { get; set; } = string.Empty; // BUDGETNAME
        public string RecDate { get; set; } = string.Empty; // recdate
        public long Amount { get; set; } // amount
        public string FileName { get; set; } = string.Empty; // filename
        public string Remarks { get; set; } = string.Empty; // remarks
        public string RecType { get; set; } = string.Empty; // RecType
        public string Pentry { get; set; } = string.Empty; // Provisional / Actual
        public string PentryShow { get; set; } = string.Empty; // Anticipatory / Actual
        public long ActualAmountReceived { get; set; } // ActualAmountReceived
    }

    public class SubmitFundReceiptDto
    {
        public int BudgetId { get; set; }
        public long Amount { get; set; }
        public string ReceivedDate { get; set; } = string.Empty; // YYYY-MM-DD
        public int DirectorateId { get; set; }
        public int FacilityId { get; set; } // DME College
        public int BankId { get; set; } // Account No
        public string Remarks { get; set; } = string.Empty;
        public string IsOp { get; set; } = "N"; // Opening Balance Flag
        public string IsProvisional { get; set; } = "N"; // Y = Anticipatory, N = Actual
        public string FileBase64 { get; set; } = string.Empty; // PDF File binary upload string stream
    }
    public class FundDetailsResponseDto
    {
        public int Budgetid { get; set; }
        public int Bgid { get; set; }
        public string Budgetname { get; set; } = string.Empty;
        public long Amount { get; set; }
        public string Receiveddate { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
        public string Acname { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public int ExtensionId { get; set; }
        public string Ext { get; set; } = ".pdf";
        public string RecType { get; set; } = string.Empty;
        public string Pentry { get; set; } = string.Empty;
        public string PentryShow { get; set; } = string.Empty;
        public long ActualAmountReceived { get; set; }
        public long Bal { get; set; } // Remaining pending balance format string
        public long BalValue { get; set; } // Actual calculation numeric remaining flag
        public int Bankid { get; set; }
    }
    public class ActualCentricFundDto
    {
        public int Bgid { get; set; } // bd.bgid
        public int Abgid { get; set; } // bd.abgid
        public string Budgetname { get; set; } = string.Empty; // mb.budgetname
        public long Amount { get; set; } // bd.amount
        public int Budgetid { get; set; } // mb.budgetid
        public string Receiveddate { get; set; } = string.Empty; // bd.RECEIVEDDATE formatted
        public string Path { get; set; } = string.Empty; // bd.filepath
        public string Filename { get; set; } = string.Empty; // bd.filename
        public string Acname { get; set; } = string.Empty; // accountname-bankname-accountno
        public string Remarks { get; set; } = string.Empty; // bd.Remarks
        public int ExtensionId { get; set; } // bd.BGID
        public string Ext { get; set; } = ".pdf"; // default extension
    }
    public class SubmitActualEntryDto
    {
        public int Bgid { get; set; } // mbd.bgid (Parent Provisional ID)
        public int Budgetid { get; set; } // mb.budgetid
        public long Amount { get; set; } // txtAmunt entered value
        public long CurrentBalance { get; set; } // lblBalance0 validation baseline value
        public string ReceivedDate { get; set; } = string.Empty; // txtRecvDt standard YYYY-MM-DD
        public string AnticipatoryDate { get; set; } = string.Empty; // lblBudgRecevDT standard YYYY-MM-DD
        public int BankId { get; set; } // ddlAcno selected value
        public string Remarks { get; set; } = string.Empty; // txtRemarks text value
        public string FileBase64 { get; set; } = string.Empty; // Base64 PDF content data string stream
        public string FileExtension { get; set; } = ".pdf"; // default extension
    }

    public class GridFilterDto
    {
        public int FinancialYearId { get; set; }
        public int? DirectorateId { get; set; } // Nullable because it is optional
    }

    public class PoFilterModel
    {
        public int FinancialYearId { get; set; }
        public string FromDate { get; set; } // Expected format: 'dd/MM/yyyy'
        public string ToDate { get; set; }   // Expected format: 'dd/MM/yyyy'
    }
    public class SupplierDispatchDto
    {
        public string Hsncode { get; set; }
        public int PoQty { get; set; }
        public string ItemName { get; set; }
        public string LocationName { get; set; }
        public string Pono { get; set; }
        public string Name { get; set; } // Supplier name mapping

        // CRITICAL FIX: Changed from decimal to string to support alphanumeric GSTIN numbers safely
        public string InvoiceGst { get; set; }

        public string InvoiceNo { get; set; }
        public string PoDate { get; set; }
        public string ChallanNo { get; set; }
        public string Challandate { get; set; }
        public string DispatchNo { get; set; }
        public string DispatchDate { get; set; }
        public string InvoiceDate { get; set; }
        public int Supplyqty { get; set; }
        public string EwayBillno { get; set; }
        public string EwayBilldt { get; set; }
        public decimal Tcsvalue { get; set; }
        public decimal InvoiceAmount { get; set; }
        public decimal Basicrate { get; set; }
        public decimal Percentage { get; set; }
        public decimal TaxableValue { get; set; }
    }

    public class PaymentStatusDto
    {
        public int Sno { get; set; }
        public int PaidMonth { get; set; }
        public string OutwardNo { get; set; }
        public string PoNo { get; set; }
        public DateTime? PoDate { get; set; }
        public string SupplierName { get; set; }
        public string SupGst { get; set; }
        public string ChequeNo { get; set; }
        public string ChequeDt { get; set; }
        public decimal PoValue { get; set; }
        public decimal ChequeAmt { get; set; }
        public decimal AdminCharges { get; set; }
        public decimal GrossWithAdmiChareges { get; set; }
        public decimal TotalStDeductionIGST { get; set; }
        public decimal TotalStDeductionCGST { get; set; }
        public decimal TotalStDeductionSGST { get; set; }
        public decimal TotalStDeduction194Q { get; set; }
        public decimal TotalStDeduction { get; set; }
        public decimal Penalties { get; set; }
        public decimal TotalAddition { get; set; }
        public decimal WitheldAmt { get; set; }
        public string BudgetName { get; set; }
        public int PaymentId { get; set; }
        public DateTime? TranDt { get; set; }
        public int PoId { get; set; }
    }
    public class TaxReleaseDto
    {
        public int Sno { get; set; }
        public string Name { get; set; }           // Supplier Name
        public string AidNo { get; set; }
        public string PoNo { get; set; }           // outward_no / po_no
        public string AidDate { get; set; }
        public int BudgetId { get; set; }
        public string BudgetName { get; set; }
        public int SupplierId { get; set; }
        public decimal ReleaseAmt { get; set; }
        public decimal RecoveredAmt { get; set; }
        public string PaidOn { get; set; }
        public int PaymentId { get; set; }
    }

    public class ForwardFileRequestDto
    {
        public int PoNoId { get; set; }           // ViewState["gvIndex"]
        public string FileNo { get; set; } = string.Empty; // ViewState["FileId"]
        public int SendToUserId { get; set; }     // ddlSendTo
        public int FromUserId { get; set; }       // sDet.DistId
        public string ForwardDate { get; set; } = string.Empty; // txtFDate.Text (YYYY-MM-DD)
        public string Remarks { get; set; } = string.Empty;     // txtremarks.Text
        public string Flag { get; set; } = string.Empty;        // rdbsenflag (S/B)
    }

    // Reason Save karne ke liye DTO (btnReasonYes_click ka replacement)
    public class SaveReasonRequestDto
    {
        public int PoNoId { get; set; }           // ViewState["gvIndex"]
        public int ReasonId { get; set; }         // ddlReason
        public string Remarks { get; set; } = string.Empty; // txtReson.Text
        public int UserId { get; set; }           // sDet.DistId
    }

    // Document links fetch karne ke liye DTO
    public class ReceiptDocumentsDto
    {
        public string InstalationReportFile { get; set; } = string.Empty;
        public string InstalationPhoto { get; set; } = string.Empty;
        public string Challanfile { get; set; } = string.Empty;
        public string WarrantyCardFile { get; set; } = string.Empty;
    }


    public class DashboardGridResponseDto
    {
        public long PoId { get; set; }
        public string TenderNo { get; set; } = string.Empty;
        public string PoNo { get; set; } = string.Empty;
        public string Supplier { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public string FacilityAutName { get; set; } = string.Empty;
        public string ItemCodeAsPerTender { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public long PoQty { get; set; }
        public long PoValue { get; set; }
        public long SupplyQty { get; set; }
        public long InsQty { get; set; }
        public long ReceiptQty { get; set; }
        public string LastRDate { get; set; } = string.Empty;
        public string PoType { get; set; } = string.Empty;
        public string FileNo { get; set; } = string.Empty;
        public string FileDt { get; set; } = string.Empty;
        public string PresentFile { get; set; } = string.Empty;
        public int PresentUserId { get; set; }
        public int ToUserId { get; set; }
        public decimal PenaltyPercent { get; set; }
        public string ReasonName { get; set; } = string.Empty;
        public string IsSolved { get; set; } = string.Empty;
        public int ReasonId { get; set; }
        public string SiteStatus { get; set; } = string.Empty;
        public long RowNo { get; set; }
        public long ToBePaid { get; set; }
        public string ToDate { get; set; } = string.Empty;
        public string EntDt { get; set; } = string.Empty;
        public string Conditond { get; set; } = string.Empty;
        public decimal FinalRate { get; set; }
        public decimal ToPaidValue { get; set; }
        public string FinRemarks { get; set; } = string.Empty;
        public int FacilityAutId { get; set; }
    }


        
    namespace EMISAPIS.DTOS
    {
        public class PoItemDetailsDto
        {
            public int ItemId { get; set; }
            public string ItemCode { get; set; } = string.Empty;
            public string ItemName { get; set; } = string.Empty;
            public decimal PercentValue { get; set; }
            public decimal BasicRate { get; set; }
            public string SchemeName { get; set; } = string.Empty;
            public string PoDate { get; set; } = string.Empty;
            public string AccYear { get; set; } = string.Empty;
            public string SupplierName { get; set; } = string.Empty;
            public string PoNo { get; set; } = string.Empty;
            public decimal FinalRate { get; set; }
            public long PoQty { get; set; }
            public decimal PoValue { get; set; }
            public int SupplierId { get; set; }
            public int TenderId { get; set; }
            public int PoNoId { get; set; }
            public int AccYrSetId { get; set; }
            public string InvoiceGst { get; set; } = string.Empty;
            public string HsnCode { get; set; } = string.Empty;
            public string Ext { get; set; } = ".pdf";
            public string FacilityAutName { get; set; } = string.Empty;
            public int FacilityAutId { get; set; }
            public int ReasonId { get; set; }
            public long UnexQty { get; set; }
        }
    }

    public class PoPenaltyAndTrancheDto
    {
        public int PoId { get; set; }
        public string PoDate { get; set; } = string.Empty;
        public int TrancheDays { get; set; }
        public string IsLdPenalty { get; set; } = string.Empty;
        public string ExtendedDate { get; set; } = string.Empty;
        public string PenaltyType { get; set; } = string.Empty;
        public int CancellationDays { get; set; }
        public decimal CancellationPercentage { get; set; }
        public decimal PenaltyPercent { get; set; }
        public int ImportDays { get; set; }
        public int DomesticDays { get; set; }
        public decimal LogoCharges { get; set; }
        public decimal LogoChargesUpper { get; set; }
    }

    public class SanctionDetailsDto
    {
        public int SanctionId { get; set; }
        public string SanctionNo { get; set; } = string.Empty;
        public decimal BudgetAmt { get; set; }
        public string SanctionDate { get; set; } = string.Empty;
        public int BudgetId { get; set; }
        public string Remarks { get; set; } = string.Empty;
        public string SupGst { get; set; } = string.Empty;
    }
    public class SupplierGstDropdownDto
    {
        public int GstId { get; set; }
        public string GstNo { get; set; } = string.Empty;
    }
    public class SaveSanctionHeaderDto
    {
        public int PoId { get; set; }           // lblPOID.Text
        public int TenderId { get; set; }       // lblTenderid.Text
        public int BudgetId { get; set; }       // ddlBudgetId.SelectedValue
        public string GstNo { get; set; } = string.Empty; // ddlGSTNO.SelectedValue
        public string DispatchGstNo { get; set; } = string.Empty; // lblGSTNoDis.Text
        public string SanctionNo { get; set; } = string.Empty; // txtSanctionNo.Text
        public string HsnCode { get; set; } = string.Empty; // txtHSNCode.Text
        public string SanctionDate { get; set; } = string.Empty; // txtSanctionDate.Text (YYYY-MM-DD format from frontend)
        public string Remarks { get; set; } = string.Empty; // txtRemarks.Text
        public decimal BudgetAmt { get; set; }  // lblBudgetAmt.Text
        public string AutoCode { get; set; } = string.Empty; // lblAutoCode.Text
        public bool IsGstOverrideChecked { get; set; } // CheckBox3.Checked
    }
    public class InvoiceSummaryDto
    {
        public int InvoiceId { get; set; }
        public int ReceiptId { get; set; }
        public int WarehouseId { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string InvoiceNo { get; set; } = string.Empty;
        public string InvoiceDate { get; set; } = string.Empty;
        public long OrderedQty { get; set; }
        public long InvoiceAbsQty { get; set; }
        public decimal Gst { get; set; }
        public decimal BasicRate { get; set; }
        public decimal OldInvoiceValue { get; set; }
        public int PoItemId { get; set; }
        public int ItemId { get; set; }
        public decimal InValueOnBill { get; set; }
        public decimal GrossAmount50 { get; set; }
        public string PStatus { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public string PDateFormatted { get; set; } = string.Empty;
        public string RDate { get; set; } = string.Empty;
        public string RecievedDate { get; set; } = string.Empty;
        public int DaysTaken { get; set; }
        public string Logo { get; set; } = string.Empty;
    }



}
