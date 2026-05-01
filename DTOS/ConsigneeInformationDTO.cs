namespace EMISAPIS.DTOS
{
    /// <summary>
    /// Medical college / facility contact details for DME consignee screen (maps to users + maslocations).
    /// </summary>
    public class ConsigneeInformationDTO
    {
        public int UserId { get; set; }
        public string LoginEmail { get; set; } = string.Empty;
        public string DeanName { get; set; } = string.Empty;
        public string DeanMobile { get; set; } = string.Empty;
        public string StoreOfficerName { get; set; } = string.Empty;
        public string StoreOfficerMobile { get; set; } = string.Empty;
        public string OfficeEmail { get; set; } = string.Empty;
        public string OfficeContactNo { get; set; } = string.Empty;
        public string AddressLine1 { get; set; } = string.Empty;
        public string AddressLine2 { get; set; } = string.Empty;
        public string AddressLine3 { get; set; } = string.Empty;
        public string LocationName { get; set; } = string.Empty;
        public int LocationId { get; set; }
    }

    public class ConsigneeInformationUpdateDTO
    {
        public int UserId { get; set; }
        public string DeanName { get; set; } = string.Empty;
        public string DeanMobile { get; set; } = string.Empty;
        public string StoreOfficerName { get; set; } = string.Empty;
        public string StoreOfficerMobile { get; set; } = string.Empty;
        public string OfficeEmail { get; set; } = string.Empty;
        public string OfficeContactNo { get; set; } = string.Empty;
        public string AddressLine1 { get; set; } = string.Empty;
        public string AddressLine2 { get; set; } = string.Empty;
        public string AddressLine3 { get; set; } = string.Empty;
    }
}
