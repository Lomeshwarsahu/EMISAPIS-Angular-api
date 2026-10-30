namespace EMISAPIS.DTOS
{
    public class DispatchPendingSupplierDto
    {
        public int SupplierId { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public class DispatchPendingPoDto
    {
        public int PoId { get; set; }
        public string OutwardNo { get; set; } = string.Empty;
        public string PoNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public decimal? BasicRate { get; set; }
        public decimal? Gst { get; set; }
        public int NoOfConsignee { get; set; }
        public decimal Quantity { get; set; }
        public decimal? TotalPoValue { get; set; }
        public decimal DispatchedQty { get; set; }
        public string Status { get; set; } = string.Empty;
        public string SupplyStatus { get; set; } = string.Empty;
    }

    public class DispatchConsigneeBatchDto
    {
        public int IssueId { get; set; }
        public int PoId { get; set; }
        public int LocationId { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string TentativeDate { get; set; } = string.Empty;
        public string DispatchDate { get; set; } = string.Empty;
        public string DispatchNo { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string ReceivedDate { get; set; } = string.Empty;
        /// <summary>Complete / Incomplete (SupplierDispatch.status).</summary>
        public string SupplyStatus { get; set; } = string.Empty;
        /// <summary>Complete / Incomplete (Supplierinstallation.status); filled for the installation view.</summary>
        public string InstallationStatus { get; set; } = string.Empty;
        public bool HasInvoiceDoc { get; set; }
    }

    public class DispatchConsigneeRowDto
    {
        public int PoId { get; set; }
        public int PoItemId { get; set; }
        public int ConsigneeId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string LocationName { get; set; } = string.Empty;
        public decimal? UnitPrice { get; set; }
        public decimal Quantity { get; set; }
        public decimal? TotalPrice { get; set; }
        public decimal DispatchedQty { get; set; }
        public List<DispatchConsigneeBatchDto> Batches { get; set; } = new();
    }

    public class DispatchConsigneeViewDto
    {
        public int PoId { get; set; }
        public string PoNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public List<DispatchConsigneeRowDto> Rows { get; set; } = new();
    }

    public class IncompleteDispatchPoOptionDto
    {
        public int PoId { get; set; }
        public string PoNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
    }

    public class IncompleteDispatchHeaderDto
    {
        public int PoId { get; set; }
        public string PoNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public decimal TotalPoQty { get; set; }
        public int DirectorateId { get; set; }
        public string DirectorateName { get; set; } = string.Empty;
    }

    public class IncompleteDispatchHistoryDto
    {
        public string PoNo { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string EntryDate { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
        public int DocId { get; set; }
    }

    public class IncompleteDispatchItemDto
    {
        public int IssueId { get; set; }
        public int LocationId { get; set; }
    }
}
