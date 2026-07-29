using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EMISAPIS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PendingInstallDrillDownController : ControllerBase
    {
        private readonly IConfiguration _config;
        public PendingInstallDrillDownController(IConfiguration config) => _config = config;
        private string ConnStr() => _config.GetConnectionString("DefaultConnection")!;

        [HttpGet("grid")]
        public async Task<IActionResult> GetGrid([FromQuery] int poId)
        {
            try
            {
                var list = new List<PendingInstallDto>();
                using var conn = new SqlConnection(ConnStr());
                await conn.OpenAsync();
                var sql = @"
SELECT d.dbstart_name_en DistrictName, l.location_name LocationName,
m.item_code_as_per_tender ItemCode, m.item_name ItemName,
sp.name Supplier, p.po_no PoNo,
CASE WHEN p.soissueDT IS NULL THEN CONVERT(varchar,p.po_date,103) ELSE CONVERT(varchar,p.soissueDT,103) END po_dat,
SUM(pi.quantity) quantity,
ISNULL(sup.Supplyqty,0) DispatchedQTY,
ISNULL(re.receiptQTY,0) receiptQTY,
ISNULL(re.insqty,0) insqty,
CASE WHEN (ISNULL(re.receiptQTY,0)>ISNULL(re.insqty,0)) THEN 'To be installed' ELSE 'To be received' END remarks
FROM po_items pi
INNER JOIN purchase_order p ON p.po_id=pi.po_id
INNER JOIN masitems m ON m.item_id=pi.item_id
INNER JOIN massuppliers sp ON sp.supplier_id=p.supplier_id
INNER JOIN maslocations l ON l.location_id=pi.consignee_id
LEFT OUTER JOIN districts d ON d.dp_districtid=l.dp_districtid
LEFT OUTER JOIN (
    SELECT d.po_id, d.location_id, SUM(Supplyqty) Supplyqty
    FROM SupplierDispatch d
    INNER JOIN Issue_item_details i ON d.Issue_id=i.Issue_id
    INNER JOIN maslocations u ON u.location_id=d.location_id
    WHERE d.status='C'
    GROUP BY d.po_id, d.location_id
) sup ON sup.po_id=pi.po_id AND sup.location_id=pi.consignee_id
LEFT OUTER JOIN (
    SELECT po_id, location_id, SUM(receiptQTY) receiptQTY, SUM(insqty) insqty FROM (
        SELECT r.po_id, r.location_id, r.receipt_id,
        r.receipt_qty receiptQTY,
        ISNULL(ins.insqty,0) insqty
        FROM receipts r
        LEFT OUTER JOIN (
            SELECT r.po_id, r.location_id, r.receipt_id, SUM(ri.received_qty) insqty
            FROM receipts r
            LEFT OUTER JOIN receipt_item_details ri ON ri.receipt_id=r.receipt_id
            WHERE r.recieved_date IS NOT NULL AND r.status IN ('C')
            GROUP BY r.po_id, r.location_id, r.receipt_id
        ) ins ON ins.po_id=r.po_id AND ins.location_id=r.location_id AND ins.receipt_id=r.receipt_id
        WHERE r.recieved_date IS NOT NULL AND r.status IN ('C','Received')
    ) a GROUP BY po_id, location_id
) re ON re.po_id=pi.po_id AND re.location_id=l.location_id
WHERE p.po_id=@poId AND p.status IN ('Order Placed','Completed','Partially Received')
GROUP BY pi.po_id, l.location_name, l.location_id, d.dp_districtid, d.dbstart_name_en,
m.item_code_as_per_tender, m.item_name, sp.name, p.soissueDT, p.po_date, p.po_no, sup.Supplyqty, re.receiptQTY, re.insqty
HAVING SUM(pi.quantity) > ISNULL(re.insqty,0)
ORDER BY d.dbstart_name_en";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@poId", poId);
                using var dr = await cmd.ExecuteReaderAsync();
                while (await dr.ReadAsync())
                {
                    list.Add(new PendingInstallDto
                    {
                        DistrictName = dr["DistrictName"]?.ToString() ?? "",
                        LocationName = dr["LocationName"]?.ToString() ?? "",
                        ItemCode = dr["ItemCode"]?.ToString() ?? "",
                        ItemName = dr["ItemName"]?.ToString() ?? "",
                        Supplier = dr["Supplier"]?.ToString() ?? "",
                        PoNo = dr["PoNo"]?.ToString() ?? "",
                        PoDate = dr["po_dat"]?.ToString() ?? "",
                        Quantity = Convert.ToDecimal(dr["quantity"]),
                        DispatchedQty = Convert.ToDecimal(dr["DispatchedQTY"]),
                        ReceiptQty = Convert.ToDecimal(dr["receiptQTY"]),
                        InstalledQty = Convert.ToDecimal(dr["insqty"]),
                        Remarks = dr["remarks"]?.ToString() ?? ""
                    });
                }
                return Ok(new { items = list, total = list.Count });
            }
            catch { return Ok(new { items = new List<PendingInstallDto>(), total = 0 }); }
        }
    }
}
