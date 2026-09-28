using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EMISAPIS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PODetailsRDLCController : ControllerBase
    {
        private readonly IConfiguration _config;
        public PODetailsRDLCController(IConfiguration config) => _config = config;
        private string ConnStr() => _config.GetConnectionString("DefaultConnection")!;

        [HttpGet("items")]
        public async Task<IActionResult> GetItems([FromQuery] int poNoId)
        {
            try
            {
                var list = new List<PoDetailItemDto>();
                using var conn = new SqlConnection(ConnStr());
                await conn.OpenAsync();
                var sql = @"
SELECT ISNULL(CONVERT(varchar,p.soissueDT,103),'-') soissueDate,
pi.item_id, m.item_code_as_per_tender itemcode, m.item_name itemname,
pi.percentage percentvalue, pi.basicrate,
t.tender_no SchemeName, CONVERT(varchar,p.po_date,103) PoDate,
f.year AccYear, s.name SupplierName, p.po_no PoNo,
ROUND(pi.basicrate+((pi.basicrate*pi.percentage)/100),2) finalrate,
SUM(pi.quantity) poqty,
ROUND((SUM(pi.quantity)*basicrate),0)+ROUND((SUM(pi.quantity)*basicrate*pi.percentage)/100,0) poValue,
p.po_id PoId,
CASE WHEN p.Potype IS NULL THEN dbo.GetAllowedDays(pt.tranche_days,
CASE WHEN p.soissueDT IS NULL THEN p.po_date ELSE p.soissueDT END) ELSE pt.tranche_days END tranche_days,
CASE WHEN t.penaltytype='D' THEN 'Days' ELSE 'Week' END penaltytype,
t.penaltypercent,
CASE WHEN p.Potype IS NULL THEN '-' ELSE 'COVID19 PO' END PoType,
p.outward_no, ci.make, ci.model,
ISNULL(r.receivedQTY,0) receivedQTY, ISNULL(ins.installedQTY,0) installedQTY,
SUM(pi.quantity)-ISNULL(r.receivedQTY,0) BalanceQTY,
ISNULL(r.receivedQTY,0)-ISNULL(ins.installedQTY,0) PendingInstall,
ISNULL(ic.categoryName,'Equipment') categoryName
FROM purchase_order p
INNER JOIN po_items pi ON pi.po_id=p.po_id
INNER JOIN contract_items ci ON ci.contract_item_id=pi.contract_item_id
INNER JOIN tenders t ON t.tender_id=p.tender_id
INNER JOIN po_tranche pt ON pt.po_id=p.po_id
INNER JOIN masitems m ON m.item_id=pi.item_id
INNER JOIN mas_financial_year f ON f.financial_year_id=p.financial_year_id
INNER JOIN massuppliers s ON s.supplier_id=p.supplier_id
LEFT OUTER JOIN masCategory ic ON ic.categoryId=m.categoryId
LEFT OUTER JOIN (
    SELECT r.po_id, SUM(r.receipt_qty) receivedQTY FROM receipts r
    INNER JOIN SupplierDispatch d ON d.Issue_id=r.issue_id AND d.po_id=r.po_id
    WHERE r.status IN ('C','Received') AND d.status='C' AND r.dispatch_date IS NOT NULL
    GROUP BY r.po_id
) r ON r.po_id=p.po_id
LEFT OUTER JOIN (
    SELECT r.po_id, SUM(ri.received_qty) installedQTY FROM receipts r
    INNER JOIN receipt_item_details ri ON ri.receipt_id=r.receipt_id
    INNER JOIN SupplierDispatch d ON d.Issue_id=r.issue_id AND d.po_id=r.po_id
    WHERE r.status IN ('C') AND d.status='C' AND r.dispatch_date IS NOT NULL
    GROUP BY r.po_id
) ins ON ins.po_id=p.po_id
WHERE p.po_id=@poNoId
GROUP BY pi.item_id,m.item_code_as_per_tender,m.item_name,pi.percentage,pi.basicrate,
p.po_date,t.tender_no,f.year,s.name,p.po_no,p.po_id,p.soissueDT,p.Potype,
pt.tranche_days,t.penaltytype,t.penaltypercent,
p.outward_no,ci.make,ci.model,ins.installedQTY,r.receivedQTY,ic.categoryName";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@poNoId", poNoId);
                using var dr = await cmd.ExecuteReaderAsync();
                while (await dr.ReadAsync())
                {
                    list.Add(new PoDetailItemDto
                    {
                        SoissueDate = dr["soissueDate"]?.ToString() ?? "",
                        ItemId = Convert.ToInt32(dr["item_id"]),
                        ItemCode = dr["itemcode"]?.ToString() ?? "",
                        ItemName = dr["itemname"]?.ToString() ?? "",
                        PercentValue = Convert.ToDecimal(dr["percentvalue"]),
                        BasicRate = Convert.ToDecimal(dr["basicrate"]),
                        SchemeName = dr["SchemeName"]?.ToString() ?? "",
                        PoDate = dr["PoDate"]?.ToString() ?? "",
                        AccYear = dr["AccYear"]?.ToString() ?? "",
                        SupplierName = dr["SupplierName"]?.ToString() ?? "",
                        PoNo = dr["PoNo"]?.ToString() ?? "",
                        FinalRate = Convert.ToDecimal(dr["finalrate"]),
                        PoQty = Convert.ToDecimal(dr["poqty"]),
                        PoValue = Convert.ToDecimal(dr["poValue"]),
                        PoId = Convert.ToInt32(dr["PoId"]),
                        TrancheDays = dr["tranche_days"]?.ToString() ?? "",
                        PenaltyType = dr["penaltytype"]?.ToString() ?? "",
                        PenaltyPercent = Convert.ToDecimal(dr["penaltypercent"]),
                        PoType = dr["PoType"]?.ToString() ?? "",
                        OutwardNo = dr["outward_no"]?.ToString() ?? "",
                        Make = dr["make"]?.ToString() ?? "",
                        Model = dr["model"]?.ToString() ?? "",
                        ReceivedQty = Convert.ToDecimal(dr["receivedQTY"]),
                        InstalledQty = Convert.ToDecimal(dr["installedQTY"]),
                        BalanceQty = Convert.ToDecimal(dr["BalanceQTY"]),
                        PendingInstall = Convert.ToDecimal(dr["PendingInstall"]),
                        CategoryName = dr["categoryName"]?.ToString() ?? ""
                    });
                }
                return Ok(list);
            }
            catch { return Ok(new List<PoDetailItemDto>()); }
        }

        [HttpGet("receipts")]
        public async Task<IActionResult> GetReceipts([FromQuery] int poNoId)
        {
            try
            {
                var list = new List<PoDetailReceiptDto>();
                using var conn = new SqlConnection(ConnStr());
                await conn.OpenAsync();
                var sql = @"
SELECT ISNULL(ri.make_no,'-') MachineSrno,
m.location_name, d.invoice_no InvoiceNo,
CONVERT(varchar,d.invoice_date,103) InvoiceDate,
pi.OrderedQTY, SUM(ri.received_qty) InvoiceAbsQty,
pi.GST, pi.basicrate BasicRate,
ROUND(pi.basicrate+(pi.basicrate*pi.GST/100),2) SUP,
CONVERT(varchar,r.recieved_date,103) RecievedDate,
DATEDIFF(DAY, CASE WHEN p.soissueDT IS NULL THEN p.po_date ELSE p.soissueDT END, r.recieved_date) Daystaken,
CONVERT(varchar,ri.installation_date,103) InstallationDate,
ISNULL(ri.LogoCharges_HOVerified,'NA') Logo
FROM receipts r
LEFT OUTER JOIN receipt_item_details ri ON ri.receipt_id=r.receipt_id
INNER JOIN purchase_order p ON p.po_id=r.po_id
INNER JOIN maslocations m ON m.location_id=r.location_id
INNER JOIN SupplierDispatch d ON d.Issue_id=r.issue_id AND r.location_id=m.location_id
LEFT OUTER JOIN (
    SELECT pi.po_item_id, pi.po_id, SUM(pi.quantity) OrderedQTY,
    pi.percentage GST, pi.basicrate, pi.consignee_id
    FROM po_items pi GROUP BY pi.po_id,pi.percentage,pi.basicrate,pi.consignee_id,pi.po_item_id
) pi ON pi.po_id=r.po_id AND pi.consignee_id=r.location_id
WHERE r.po_id=@poNoId AND r.status IN ('C','Received')
GROUP BY ri.make_no,m.location_name,d.invoice_no,d.invoice_date,
pi.OrderedQTY,pi.GST,pi.basicrate,r.recieved_date,p.po_date,p.soissueDT,
ri.installation_date,ri.LogoCharges_HOVerified
ORDER BY m.location_name";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@poNoId", poNoId);
                using var dr = await cmd.ExecuteReaderAsync();
                while (await dr.ReadAsync())
                {
                    list.Add(new PoDetailReceiptDto
                    {
                        MachineSrno = dr["MachineSrno"]?.ToString() ?? "",
                        LocationName = dr["location_name"]?.ToString() ?? "",
                        InvoiceNo = dr["InvoiceNo"]?.ToString() ?? "",
                        InvoiceDate = dr["InvoiceDate"]?.ToString() ?? "",
                        OrderedQty = Convert.ToDecimal(dr["OrderedQTY"]),
                        InvoiceAbsQty = Convert.ToDecimal(dr["InvoiceAbsQty"]),
                        Gst = Convert.ToDecimal(dr["GST"]),
                        BasicRate = Convert.ToDecimal(dr["BasicRate"]),
                        Sup = Convert.ToDecimal(dr["SUP"]),
                        RecievedDate = dr["RecievedDate"]?.ToString() ?? "",
                        Daystaken = Convert.ToInt32(dr["Daystaken"]),
                        InstallationDate = dr["InstallationDate"]?.ToString() ?? "",
                        Logo = dr["Logo"]?.ToString() ?? ""
                    });
                }
                return Ok(list);
            }
            catch { return Ok(new List<PoDetailReceiptDto>()); }
        }
    }
}
