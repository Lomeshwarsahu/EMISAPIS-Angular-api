using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography.Pkcs;
namespace EMISAPIS.DTOS
{
    public class BMEDDTO
    {
        public int SupplierId { get; set; }
        public string IsContractor { get; set; } // या bool, DB के आधार पर
        public string Name { get; set; }
        public string EmailId { get; set; }
        public string PhNo { get; set; }
        public string Address { get; set; }
        public string SupplierCode { get; set; }
        public string ModuleCode { get; set; }
        public string MobileNo { get; set; }
        public string GSTNo { get; set; }
        public string Type { get; set; }
        public string Class { get; set; }
        public string IsRegister { get; set; } // या bool, DB के आधार पर
        public string ServiceEngineerName { get; set; }
        public string ServiceEngineerNumber { get; set; }
        public string TinNo { get; set; }

    }
    public class SupplierCreateDTO
    {
        [Required(ErrorMessage = "Please insert Supplier Name.")]
        [MaxLength(100, ErrorMessage = "The limit of Supplier name is 100 characters.")]
        public string SupplierName { get; set; }

        [Required(ErrorMessage = "Please insert Contact Person Name.")]
        [MaxLength(100, ErrorMessage = "The limit of Contact person name is 100 characters.")]
        public string ContactPersonName { get; set; }

        [Required(ErrorMessage = "Please insert Contact Person Number.")]
        public string ContactPersonNumber { get; set; }

        [Required(ErrorMessage = "Please insert Mobile Number.")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "The limit of Mobile Number is 10 digits.")]
        public string MobileNo { get; set; }

        [Required(ErrorMessage = "Please insert Email Id.")]
        [MaxLength(50, ErrorMessage = "The limit of Email Id is 50 characters.")]
        [EmailAddress(ErrorMessage = "Invalid Email Format.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Please insert GST No.")]
        [MaxLength(15, ErrorMessage = "The limit of GST No is 15 characters.")]
        public string GSTNo { get; set; }

        // Optional fields
        [MaxLength(11, ErrorMessage = "The limit of Phone No is 11 digits.")]
        public string PhnNo { get; set; }

        [MaxLength(15, ErrorMessage = "The limit of Tin No is 15 characters.")]
        public string TinNo { get; set; }

        [Required(ErrorMessage = "Please insert Address.")]
        [MaxLength(500, ErrorMessage = "The limit of Address is 500 characters.")]
        public string Address { get; set; }
    }
    public class SupplierResponseDTO
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; }
        public string ContactPersonName { get; set; }
        public string ContactPersonNumber { get; set; }
        public string MobileNo { get; set; }
        public string Email { get; set; }
        public string GSTNo { get; set; }
        public string PhnNo { get; set; }
        public string TinNo { get; set; }
        public string Address { get; set; }
    }
    public class SupplierUpdateDTO
    {
        [Required(ErrorMessage = "Supplier ID is required.")]
        public int SupplierId { get; set; } // Update के लिए ID ज़रूरी है

        [Required(ErrorMessage = "Please insert Supplier Name.")]
        [MaxLength(100, ErrorMessage = "The limit of Supplier name is 100 characters.")]
        public string SupplierName { get; set; }

        [Required(ErrorMessage = "Please insert Contact Person Name.")]
        [MaxLength(100, ErrorMessage = "The limit of Contact person name is 100 characters.")]
        public string ContactPersonName { get; set; }

        [Required(ErrorMessage = "Please insert Contact Person Number.")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "The limit of Contact Person Number is 10 digits.")]
        public string ContactPersonNumber { get; set; }

        [Required(ErrorMessage = "Please insert Mobile Number.")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "The limit of Mobile Number is 10 digits.")]
        public string MobileNo { get; set; }

        [Required(ErrorMessage = "Please insert Email Id.")]
        [MaxLength(50, ErrorMessage = "The limit of Email Id is 50 characters.")]
        [EmailAddress(ErrorMessage = "Invalid Email Format.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Please insert GST No.")]
        [MaxLength(15, ErrorMessage = "The limit of GST No is 15 characters.")]
        public string GSTNo { get; set; }

        // Optional fields (कोई [Required] नहीं)
        [MaxLength(11, ErrorMessage = "The limit of Phone No is 11 digits.")]
        public string PhnNo { get; set; }

        [MaxLength(15, ErrorMessage = "The limit of Tin No is 15 characters.")]
        public string TinNo { get; set; }

        [Required(ErrorMessage = "Please insert Address.")]
        [MaxLength(500, ErrorMessage = "The limit of Address is 500 characters.")]
        public string Address { get; set; }
    }
    public class CategoryDTO
    {
        public int categoryId { get; set; }
        public string categoryName { get; set; }
    }

    public class EquipmentItemDTO
    {
        public int? ContractItemId { get; set; }
        public string ItemName { get; set; }
        public int ItemId { get; set; }
        public string ItemCodeAsPerTender { get; set; }
        public decimal? EstimatedCost { get; set; }
        public string AMC { get; set; }
        public string PM { get; set; }
        public string PmMonth { get; set; }
        public string RCValid { get; set; }
        public decimal? BasicRate { get; set; }
        public decimal? Percentage { get; set; }
        public string TenderNo { get; set; }
        public string Category { get; set; }
        public int? CategoryId { get; set; }
    }

    public class EquipmentCreateDTO
    {
        [Required(ErrorMessage = "Equipment Code is required.")]
        public string ItemCode { get; set; }

        [Required(ErrorMessage = "Equipment Name is required.")]
        public string ItemName { get; set; }

        [Required(ErrorMessage = "Please Select Category.")]
        public int CategoryId { get; set; }

        public decimal? Price { get; set; }

        public int? PreventivePeriod { get; set; }

        // ये 't' (true) या 'f' (false) की वैल्यूज़ लेंगी
        public string Warranty { get; set; } = "f";
        public string AMC { get; set; } = "f";
        public string PrevMaint { get; set; } = "f";
        public string Installation { get; set; } = "f";
    }

    public class ItemMappingDTO
    {
        [Required(ErrorMessage = "Main Item Type is required.")]
        public string MainItemType { get; set; }

        public string AmcRequired { get; set; } = "Y";
        public string EntryType { get; set; } = "S";
        public string ProgressRequired { get; set; } = "Y";
        public string IsElectrical { get; set; } = "Y";
        public List<int> SelectedItemIds { get; set; } = new List<int>();
    }

    public class UnmappedItemDTO
    {
        public string ItemCodeAsPerTender { get; set; }
        public string ItemName { get; set; }
        public int ItemId { get; set; }

        
        public int? Pid { get; set; }

        public string PItemName { get; set; }
    }
    public class mappedItemDTO
    {
        public int? PID { get; set; }

        public string PItemName { get; set; }
    }

    public class MapExistingItemDTO
    {
        [Required(ErrorMessage = "Please Select Main Item Type")]
        public int MainItemTypeId { get; set; }

        public List<int> SelectedItemIds { get; set; } = new List<int>();
    }
    public class mappedItemsReportDTO
    {
        public string PItemName { get; set; }
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public string IsElectrical { get; set; }
        public string ProgReq { get; set; }
        public string SRorBulkEntry { get; set; }
        public string AmcReq { get; set; }
        public int ItemId { get; set; }
    }
    public class tenderlistDTO
    {
        public int Tenderid { get; set; }
        public string Tenderno { get; set; }
    }
    public class TenderSupplierrDTO
    {
        public int sId { get; set; }
        public string sName { get; set; } = string.Empty;
    }
    public class ContractFilterDTO
    {
        public int FinancialYearId { get; set; } // 0 मतलब "All"
        public int TenderId { get; set; }        // 0 मतलब "All"
        public int SupplierId { get; set; }      // 0 मतलब "All"
        public string Status { get; set; } = string.Empty; // "" मतलब "All"
    }
    public class AwardOfContractDTO
    {
        public string ContractNumber { get; set; } = string.Empty;
        public string ContractDate { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string ContractType { get; set; } = string.Empty;
        public string ContractDescription { get; set; } = string.Empty;
        public string TenderNo { get; set; } = string.Empty;
        public string TenderDate { get; set; } = string.Empty;
        public int TenderId { get; set; }
        public string ContractDuration { get; set; } = string.Empty;
        public string ContractSignDate { get; set; } = string.Empty;
        public string ContractEndDate { get; set; } = string.Empty;
        public int DocumentType { get; set; }
        public string DocumentNumber { get; set; } = string.Empty;
        public string DocumentDate { get; set; } = string.Empty;
        public string DocumentExpiryDate { get; set; } = string.Empty;
        public int FinancialYearId { get; set; }
        public string Year { get; set; } = string.Empty;
        public decimal DocumentValue { get; set; }
        public int AwardOfContractId { get; set; }
        public string Status { get; set; } = string.Empty;
    }


    // 1. Contract Header बनाने के लिए (Angular से डेटा लेने के लिए)
    public class GenerateContractDTO
    {
        public int TenderId { get; set; }
        public int SupplierId { get; set; }
        public int FinancialYearId { get; set; }
        public string ContractDate { get; set; } = string.Empty; // "yyyy-MM-dd" format
    }

    public class GetTaxDTO
    {
        public int TaxId { get; set; }
        public string Taxname { get; set; } = string.Empty;
    }
    public class ItemRateDetailDTO
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;

        // पैसों (Rates/Tax) के लिए decimal का इस्तेमाल करना सबसे अच्छा है
        public decimal BasicRate { get; set; }
        public decimal Gst { get; set; }

        public int SupplierId { get; set; }
        public int TenderId { get; set; }
    }
    public class TenderItemDTO
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
    }
    // 2. नया आइटम जोड़ने के लिए (Add Equipments)
    public class AddContractItemDTO
    {
        public int AwardOfContractId { get; set; }
        public int ItemId { get; set; }
        public int NoOfDaysForSupply { get; set; }
        public decimal BasicRate { get; set; }
        public int TaxTypeId { get; set; }
        public decimal Percentage { get; set; }
        public decimal SingleUnitPrice { get; set; }
        public string LicenceNumber { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string DomesticImported { get; set; } = string.Empty; // 0 or 1
    }

    public class ContractItemDetailsDTO
    {
        public int AwardOfContractId { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public int ContractItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int NoOfDaysForSupply { get; set; }
        public decimal BasicRate { get; set; }
        public string TaxTypeName { get; set; } = string.Empty;
        public decimal Percentage { get; set; }
        public decimal SingleUnitPrice { get; set; }
        public string LicenceNumber { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int ItemId { get; set; }
        public int TaxTypeId { get; set; }
        public string SupplyCategory { get; set; } = string.Empty;
        public string ContractDate { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public int FinancialYearId { get; set; }
        public int TenderId { get; set; }
    }

    public class UpdateContractItemsDTO
    {
        public int ContractItemId { get; set; }
        public int NoOfDaysForSupply { get; set; }
        public decimal BasicRate { get; set; }
        public int TaxTypeId { get; set; }
        public decimal Percentage { get; set; }
        public decimal SingleUnitPrice { get; set; }
        public string LicenceNumber { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
    }

    public class FinalizeContractDTO
    {
        public int AwardOfContractId { get; set; }
        public int ContractDuration { get; set; } // महीनों (Months) में
        public string ContractSignDate { get; set; } = string.Empty; // फॉर्मेट: "yyyy-MM-dd"
    }
    // 3. Grid/Table में डेटा भेजने के लिए (Response DTO)
    public class ContractItemGridResponseDTO
    {
        public int ContractItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int NoOfDaysForSupply { get; set; }
        public decimal BasicRate { get; set; }
        public string TaxTypeName { get; set; } = string.Empty;
        public decimal Percentage { get; set; }
        public decimal SingleUnitPrice { get; set; }
        public string LicenceNumber { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string SupplyCategory { get; set; } = string.Empty; // Domestic/Imported
    }

    //public class FinYearListDTO
    //{
    //    public int finyear_id { get; set; }
    //    public string year { get; set; } = string.Empty;
    //}
    public class TenderDashboardReportDTO
    {
        public int TenderId { get; set; }
        public string TenderNo { get; set; } = string.Empty;
        public string TenderDate { get; set; } = string.Empty;
        public string TenderDescription { get; set; } = string.Empty;
        public string Flag { get; set; } = string.Empty;
        public int FinancialYearId { get; set; }
        public int WarrantyYear { get; set; }
        public int ImportDays { get; set; }
        public int DomesticDays { get; set; }
        public string CoverA { get; set; } = string.Empty;
        public string CoverB { get; set; } = string.Empty;
        public string CoverDemo { get; set; } = string.Empty;
        public string CoverC { get; set; } = string.Empty;
        public string CStatus { get; set; } = string.Empty;
        public int CsId { get; set; }

        // काउंट्स और कैलकुलेशंस
        public int TotalItems { get; set; }
        public int FoundItems { get; set; }
        public int NosNotFound { get; set; }
        public int PriceEntry { get; set; }
        public int Accept { get; set; }
        public int Reject { get; set; }
        public int NosBidder { get; set; }
        public int NosItemsBid { get; set; }
        public decimal TotalValue { get; set; }
        public string TenderType { get; set; } = string.Empty;
    }
}
