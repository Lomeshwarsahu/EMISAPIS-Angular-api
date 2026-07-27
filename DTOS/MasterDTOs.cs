namespace EMISAPIS.DTOS
{
    #region DHS Facility Users Locations

    public class DistrictDto
    {
        public int DP_DistrictID { get; set; }
        public string DBStart_Name_En { get; set; } = string.Empty;
    }

    public class FacilityTypeDto
    {
        public int FacilityTypeId { get; set; }
        public string FacilityTypeName { get; set; } = string.Empty;
    }

    public class DHSFacilityGridDto
    {
        public int FacilityTypeId { get; set; }
        public int Authority { get; set; }
        public int LocationId { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string EmailId { get; set; } = string.Empty;
        public string StoreNo { get; set; } = string.Empty;
        public string StoreName { get; set; } = string.Empty;
    }

    public class DHSFacilityGridRequest
    {
        public int FacilityTypeId { get; set; }
        public int DistrictId { get; set; }
    }

    public class AddFacilityUserRequest
    {
        public int LocationId { get; set; }
        public string EmailId { get; set; } = string.Empty;
    }

    #endregion

    #region Health Facility Details (NodleMaster)

    public class NodleMasterGridDto
    {
        public string DistrictName { get; set; } = string.Empty;
        public int LocationId { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string FacilityTypeName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string StoreOfficerMob { get; set; } = string.Empty;
        public string EmailID { get; set; } = string.Empty;
        public int UserId { get; set; }
        public int NodleId { get; set; }
        public string NodleName { get; set; } = string.Empty;
        public string NodleDesignation { get; set; } = string.Empty;
        public string NodleMobile { get; set; } = string.Empty;
        public string NodleEmail { get; set; } = string.Empty;
    }

    public class NodleMasterSaveRequest
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;
        public string EmailId { get; set; } = string.Empty;
        public string MobileNo { get; set; } = string.Empty;
    }

    public class NodleMasterDeleteRequest
    {
        public int Id { get; set; }
    }

    #endregion

    #region Item Specification

    public class EqpCategoryDto
    {
        public int Eqpcatid { get; set; }
        public string Eqpcatname { get; set; } = string.Empty;
    }

    public class ItemSpecGridDto
    {
        public int ItemId { get; set; }
        public string ItemCodeAsPerTender { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Eqpcatname { get; set; } = string.Empty;
        public bool HasFile { get; set; }
        public string FileName { get; set; } = string.Empty;
    }

    public class ItemSpecDownloadDto
    {
        public string UploadFolderName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }

    #endregion

    #region Medical Facility Users Locations (DME authority=12)

    public class MedicalCollegeUserDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
    }

    public class MedFacilityGridDto
    {
        public int FacilityTypeId { get; set; }
        public int Authority { get; set; }
        public int LocationId { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string EmailId { get; set; } = string.Empty;
        public string StoreNo { get; set; } = string.Empty;
        public string StoreName { get; set; } = string.Empty;
    }

    public class MedFacilityGridRequest
    {
        public int FacilityTypeId { get; set; }
        public int AuthorityId { get; set; }
        public int DistrictId { get; set; }
        public int UserId { get; set; }
    }

    public class AddMedFacilityRequest
    {
        public string LocationName { get; set; } = string.Empty;
        public int DistrictId { get; set; }
        public int FacilityTypeId { get; set; }
        public int Authority { get; set; }
        public int UserId { get; set; }
        public string Address1 { get; set; } = string.Empty;
        public string Address2 { get; set; } = string.Empty;
        public string Address3 { get; set; } = string.Empty;
        public string MobileNo { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        public string EmailId { get; set; } = string.Empty;
    }

    #endregion

    #region Master Supplier Add

    public class SupplierAddRequest
    {
        public string Name { get; set; } = string.Empty;
        public string ContactPersonName { get; set; } = string.Empty;
        public string ContactPersonNumber { get; set; } = string.Empty;
        public string MobileNo { get; set; } = string.Empty;
        public string EmailId { get; set; } = string.Empty;
        public string GSTNo { get; set; } = string.Empty;
        public string PhNo { get; set; } = string.Empty;
        public string TinNo { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    public class SupplierEditRequest : SupplierAddRequest
    {
        public int SupplierId { get; set; }
    }

    public class SupplierDetailDto
    {
        public int SupplierId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ServiceEngineerName { get; set; } = string.Empty;
        public string ServiceEngineerNumber { get; set; } = string.Empty;
        public string MobileNo { get; set; } = string.Empty;
        public string EmailId { get; set; } = string.Empty;
        public string GSTNo { get; set; } = string.Empty;
        public string PhNo { get; set; } = string.Empty;
        public string TinNo { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    #endregion

    #region Store Home

    public class StoreHomeDto
    {
        public string UserName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Address2 { get; set; } = string.Empty;
        public string HODName { get; set; } = string.Empty;
        public string HODNo { get; set; } = string.Empty;
        public string EmailID { get; set; } = string.Empty;
        public string LoginEmail { get; set; } = string.Empty;
        public string StoreOfficer { get; set; } = string.Empty;
        public string StoreOfficerMob { get; set; } = string.Empty;
        public string StoreLandline { get; set; } = string.Empty;
    }

    public class StoreHomeUpdateRequest
    {
        public string UserName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Address2 { get; set; } = string.Empty;
        public string HODName { get; set; } = string.Empty;
        public string HODNo { get; set; } = string.Empty;
        public string EmailID { get; set; } = string.Empty;
        public string StoreOfficer { get; set; } = string.Empty;
        public string StoreOfficerMob { get; set; } = string.Empty;
        public string StoreLandline { get; set; } = string.Empty;
    }

    #endregion
}
