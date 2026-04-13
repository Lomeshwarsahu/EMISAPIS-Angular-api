using System.ComponentModel.DataAnnotations;
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
}
