using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EMISAPIS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PoReportNewController : ControllerBase
    {
        private readonly IConfiguration _config;
        public PoReportNewController(IConfiguration config) => _config = config;
        private string ConnStr() => _config.GetConnectionString("DefaultConnection")!;

        [HttpGet("items")]
        public async Task<IActionResult> GetItems()
        {
            try
            {
                var list = new List<PoReportItemOptionDto>();
                using var conn = new SqlConnection(ConnStr());
                await conn.OpenAsync();
                var sql = @"
SELECT DISTINCT a.item_id,
a.item_code_as_per_tender + '-' + a.item_name item_name
FROM masitems a
INNER JOIN (SELECT DISTINCT pi.item_id FROM po_items pi
INNER JOIN purchase_order p ON p.po_id = pi.po_id) p ON p.item_id = a.item_id
WHERE a.parent_item_id IS NOT NULL
ORDER BY item_name";
                using var cmd = new SqlCommand(sql, conn);
                using var dr = await cmd.ExecuteReaderAsync();
                while (await dr.ReadAsync())
                {
                    list.Add(new PoReportItemOptionDto
                    {
                        ItemId = Convert.ToInt32(dr["item_id"]),
                        ItemName = dr["item_name"]?.ToString() ?? ""
                    });
                }
                return Ok(list);
            }
            catch { return Ok(new List<PoReportItemOptionDto>()); }
        }

        [HttpGet("districts")]
        public async Task<IActionResult> GetDistricts()
        {
            try
            {
                var list = new List<PoReportDistrictOptionDto>();
                using var conn = new SqlConnection(ConnStr());
                await conn.OpenAsync();
                var sql = @"
SELECT DISTINCT d.dbstart_name_en, d.dp_districtid
FROM purchase_order p
INNER JOIN facility_aut f ON f.facility_aut_id = p.directorate_id
INNER JOIN districts_po d ON d.dp_districtid = f.district_id
ORDER BY d.dbstart_name_en";
                using var cmd = new SqlCommand(sql, conn);
                using var dr = await cmd.ExecuteReaderAsync();
                while (await dr.ReadAsync())
                {
                    list.Add(new PoReportDistrictOptionDto
                    {
                        DpDistrictId = Convert.ToInt32(dr["dp_districtid"]),
                        DBStart_Name_En = dr["dbstart_name_en"]?.ToString() ?? ""
                    });
                }
                return Ok(list);
            }
            catch { return Ok(new List<PoReportDistrictOptionDto>()); }
        }

        [HttpGet("facilities")]
        public async Task<IActionResult> GetFacilities()
        {
            try
            {
                var list = new List<PoReportFacilityOptionDto>();
                using var conn = new SqlConnection(ConnStr());
                await conn.OpenAsync();
                var sql = @"
SELECT facility_aut_id, facility_aut_name
FROM facility_aut ORDER BY facility_aut_name";
                using var cmd = new SqlCommand(sql, conn);
                using var dr = await cmd.ExecuteReaderAsync();
                while (await dr.ReadAsync())
                {
                    list.Add(new PoReportFacilityOptionDto
                    {
                        FacilityAutId = Convert.ToInt32(dr["facility_aut_id"]),
                        FacilityAutName = dr["facility_aut_name"]?.ToString() ?? ""
                    });
                }
                return Ok(list);
            }
            catch { return Ok(new List<PoReportFacilityOptionDto>()); }
        }

        [HttpGet("report")]
        public async Task<IActionResult> GetReport(
            [FromQuery] int itemId = 0,
            [FromQuery] int districtId = 0,
            [FromQuery] int facilityId = 0)
        {
            try
            {
                var list = new List<PoReportRowDto>();
                using var conn = new SqlConnection(ConnStr());
                await conn.OpenAsync();

                var where = new List<string>();
                if (itemId > 0) where.Add("m.item_id = " + itemId);
                if (districtId > 0) where.Add("p.directorate_id IN (SELECT facility_aut_id FROM facility_aut WHERE district_id = " + districtId + ")");
                if (facilityId > 0) where.Add("p.directorate_id = " + facilityId);
                var whereClause = where.Count > 0 ? " AND " + string.Join(" AND ", where) : "";

                var sql = @"
SELECT p.po_id, t.tender_no, p.outward_no + '/' + p.po_no po_no,
sp.name supplier, CONVERT(varchar,p.po_date,103) po_date,
m.item_code_as_per_tender + '-' + m.item_name item_name,
SUM(pi.quantity) poqty, SUM(pi.totalprice) poValue,
ISNULL(re.receiptQTY,0) receiptQTY, ISNULL(ins.insqty,0) instalationQty,
CONVERT(varchar,red.LastRDate,103) lastRDate1,
dir.facility_aut_name
FROM purchase_order p
INNER JOIN po_items pi ON pi.po_id = p.po_id
INNER JOIN masitems m ON m.item_id = pi.item_id
INNER JOIN massuppliers sp ON sp.supplier_id = p.supplier_id
INNER JOIN tenders t ON t.tender_id = p.tender_id
INNER JOIN facility_aut dir ON dir.facility_aut_id = p.directorate_id
LEFT OUTER JOIN (
    SELECT po_id, SUM(receipt_qty) receiptQTY FROM receipts
    WHERE recieved_date IS NOT NULL AND status IN ('C','Received')
    GROUP BY po_id
) re ON re.po_id = p.po_id
LEFT OUTER JOIN (
    SELECT r.po_id, SUM(ri.received_qty) insqty FROM receipts r
    LEFT OUTER JOIN receipt_item_details ri ON ri.receipt_id = r.receipt_id
    WHERE r.recieved_date IS NOT NULL AND r.status IN ('C')
    GROUP BY r.po_id
) ins ON ins.po_id = p.po_id
LEFT OUTER JOIN (
    SELECT MAX(r.recieved_date) LastRDate, r.po_id FROM receipts r
    WHERE r.recieved_date IS NOT NULL AND r.status IN ('C','Received')
    GROUP BY r.po_id
) red ON red.po_id = p.po_id
WHERE p.status IN ('Completed','Order Placed','Partially Received')
" + whereClause + @"
GROUP BY p.po_id, t.tender_no, p.outward_no, p.po_no, sp.name,
p.po_date, m.item_code_as_per_tender, m.item_name,
re.receiptQTY, ins.insqty, red.LastRDate, dir.facility_aut_name
ORDER BY p.po_date DESC";

                using var cmd = new SqlCommand(sql, conn);
                using var dr = await cmd.ExecuteReaderAsync();
                while (await dr.ReadAsync())
                {
                    list.Add(new PoReportRowDto
                    {
                        PoId = Convert.ToInt32(dr["po_id"]),
                        TenderNo = dr["tender_no"]?.ToString() ?? "",
                        PoNo = dr["po_no"]?.ToString() ?? "",
                        Supplier = dr["supplier"]?.ToString() ?? "",
                        PoDate = dr["po_date"]?.ToString() ?? "",
                        ItemName = dr["item_name"]?.ToString() ?? "",
                        Poqty = Convert.ToDecimal(dr["poqty"]),
                        PoValue = Convert.ToDecimal(dr["poValue"]),
                        ReceiptQTY = Convert.ToDecimal(dr["receiptQTY"]),
                        InstalationQty = Convert.ToDecimal(dr["instalationQty"]),
                        LastRDate1 = dr["lastRDate1"]?.ToString() ?? "",
                        FacilityAutName = dr["facility_aut_name"]?.ToString() ?? ""
                    });
                }
                return Ok(list);
            }
            catch (Exception ex)
            {
                return Ok(new List<PoReportRowDto>());
            }
        }
    }
}
