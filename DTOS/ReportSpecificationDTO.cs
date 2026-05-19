namespace EMISAPIS.DTOS
{
    public class EquipmentCategoryDto
    {
        public int EqpCatId { get; set; }
        public string EqpCatName { get; set; } = string.Empty;
    }

    public class ReportSpecificationItemDto
    {
        public int ItemId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string EqpCatName { get; set; } = string.Empty;
        public bool HasSpecification { get; set; }
    }

    public class EquipmentSearchOptionDto
    {
        public int ItemId { get; set; }
        public string DisplayText { get; set; } = string.Empty;
    }

    public class ReportSpecificationSummaryDto
    {
        public List<CategoryUploadSummaryDto> Categories { get; set; } = new();
        public int TotalUploaded { get; set; }
        public int TotalItems { get; set; }
    }

    public class CategoryUploadSummaryDto
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int Uploaded { get; set; }
        public int Total { get; set; }
    }
}
