namespace EMISAPIS.DTOS
{
    public class ComplainItemOptionDto
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
    }

    public class ComplainTroubleOptionDto
    {
        public int TroubleId { get; set; }
        public string TroubleText { get; set; } = string.Empty;
    }

    public class ComplainDepartmentOptionDto
    {
        public int ItemDetailId { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public class ComplainEquipmentDetailDto
    {
        public int LocationId { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string SupplierMobile { get; set; } = string.Empty;
        public string SupplierEmail { get; set; } = string.Empty;
        public string WarrantyValidDate { get; set; } = string.Empty;
    }

    public class CreateFacilityComplainRequest
    {
        public int UserId { get; set; }
        public int ItemId { get; set; }
        public int ItemDetailId { get; set; }
        public int TroubleId { get; set; }
        public int LocationId { get; set; }
        public int SupplierId { get; set; }
        public string ComplainDate { get; set; } = string.Empty;
        public string NotFunctionDate { get; set; } = string.Empty;
        public string ComplainDetails { get; set; } = string.Empty;
        public string SupplierEmail { get; set; } = string.Empty;
        public string SupplierMobile { get; set; } = string.Empty;
    }
}
