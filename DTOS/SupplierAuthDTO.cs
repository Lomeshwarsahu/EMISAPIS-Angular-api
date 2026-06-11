namespace EMISAPIS.DTOS
{
    public class SupplierProfileDto
    {
        public int SupplierId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string MaskedMobile { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
    }

    public class SupplierOtpRequestDto
    {
        public int SupplierId { get; set; }
    }

    public class SupplierPasswordRequestDto
    {
        public int SupplierId { get; set; }
        public string Otp { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string RepeatPassword { get; set; } = string.Empty;
        /// <summary>new = create user, reset = forgot password</summary>
        public string Mode { get; set; } = "reset";
        public string DesiredUserId { get; set; } = string.Empty;
    }

    /// <summary>ParticularSupplierAdd.aspx supplier profile form.</summary>
    public class ParticularSupplierDetailsDto
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string ContactPersonName { get; set; } = string.Empty;
        public string MobileNo { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string GstNo { get; set; } = string.Empty;
        public string GstNo2 { get; set; } = string.Empty;
        public string GstNo3 { get; set; } = string.Empty;
        public string PhoneNo { get; set; } = string.Empty;
        public string TinNo { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    public class ParticularSupplierUpdateDto
    {
        public int SupplierId { get; set; }
        public string MobileNo { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string GstNo { get; set; } = string.Empty;
        public string GstNo2 { get; set; } = string.Empty;
        public string GstNo3 { get; set; } = string.Empty;
        public string PhoneNo { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    /// <summary>SupplierGSTentry1.aspx GST grid row.</summary>
    public class SupplierGstEntryDto
    {
        public int GstId { get; set; }
        public string GstNo { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string Flag { get; set; } = "Y";
    }

    public class SupplierGstPageDto
    {
        public int SupplierId { get; set; }
        public string SupplierCode { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public List<SupplierGstEntryDto> Entries { get; set; } = new();
    }

    public class SupplierGstSaveDto
    {
        public int UserId { get; set; }
        public int SupplierId { get; set; }
        public string GstNo { get; set; } = string.Empty;
    }
}
