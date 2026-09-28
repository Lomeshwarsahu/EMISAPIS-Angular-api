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

    public class ComplainStatusRowDto
    {
        public int ComplaintId { get; set; }
        public string ComplaintNo { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ComplaintDate { get; set; } = string.Empty;
        public string NotFunctionDate { get; set; } = string.Empty;
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string MakeNo { get; set; } = string.Empty;
        public string SerialNo { get; set; } = string.Empty;
        public string ComplainTroubleshoot { get; set; } = string.Empty;
        public string LocationName { get; set; } = string.Empty;
        public string StoreName { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string SupplierMobile { get; set; } = string.Empty;
        public string SupplierEmail { get; set; } = string.Empty;
        public string ComplaintDetails { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Ext { get; set; } = string.Empty;
        public int ExtensionId { get; set; }
        public int LocationId { get; set; }
        public int SupplierId { get; set; }
    }

    public class ComplainDetailDto
    {
        public int ComplaintId { get; set; }
        public string ComplaintNo { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ComplaintDate { get; set; } = string.Empty;
        public string NotFunctionDate { get; set; } = string.Empty;
        public string ComplainTroubleshoot { get; set; } = string.Empty;
        public string LocationName { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string SupplierMobile { get; set; } = string.Empty;
        public string SupplierEmail { get; set; } = string.Empty;
        public string WarrantyValidDate { get; set; } = string.Empty;
        public string MakeNo { get; set; } = string.Empty;
        public string SerialNo { get; set; } = string.Empty;
        public int LocationId { get; set; }
        public int SupplierId { get; set; }
        public string ComplaintDetails { get; set; } = string.Empty;
        public string CompClosedOn { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string SupplierServiceDate { get; set; } = string.Empty;
        public string CorrectiveActionTaken { get; set; } = string.Empty;
        public string PreventiveAction { get; set; } = string.Empty;
        public string ChangedParts { get; set; } = string.Empty;
        public string PartsReplaced { get; set; } = string.Empty;
    }

    public class CloseComplainRequest
    {
        public int ComplaintId { get; set; }
        public string CompClosedOn { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string SupplierServiceDate { get; set; } = string.Empty;
        public string CorrectiveActionTaken { get; set; } = string.Empty;
        public string PreventiveAction { get; set; } = string.Empty;
        public string ChangedParts { get; set; } = string.Empty;
        public int PartsReplaced { get; set; }
        public int UserId { get; set; }
    }
}
