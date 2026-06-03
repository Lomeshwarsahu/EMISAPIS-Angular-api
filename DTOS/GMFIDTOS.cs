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
}
