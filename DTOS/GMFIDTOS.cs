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

}
