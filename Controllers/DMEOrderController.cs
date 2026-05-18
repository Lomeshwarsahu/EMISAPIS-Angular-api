using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EMISAPIS.Controllers
{
    /// <summary>EMSRole/Order/PODashboardDMEFAC.aspx.</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DMEOrderController : ControllerBase
    {
        private readonly string _connectionString;

        public DMEOrderController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection missing.");
        }

        [HttpGet("financial-years")]
        public async Task<IActionResult> GetFinancialYears()
        {
            const string sql = @"
SELECT financial_year_id, year
FROM dbo.mas_financial_year
ORDER BY financial_year_id DESC";

            var list = new List<FinancialYearOptionDto> { new() { FinancialYearId = 0, Year = "Select Fin Year" } };

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new FinancialYearOptionDto
                    {
                        FinancialYearId = Convert.ToInt32(reader["financial_year_id"]),
                        Year = reader["year"]?.ToString() ?? string.Empty,
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading financial years.", detail = ex.Message });
            }
        }

        [HttpGet("po-equipment-options")]
        public async Task<IActionResult> GetPoEquipmentOptions()
        {
            const string sql = @"
SELECT DISTINCT m.item_id, m.item_name + '-' + m.item_code_as_per_tender AS item_name, m.item_code_as_per_tender
FROM dbo.po_items pi
INNER JOIN dbo.masitems m ON m.item_id = pi.item_id
UNION
SELECT DISTINCT m.item_id, m.item_name + '-' + m.item_code_as_per_tender AS item_name, m.item_code_as_per_tender
FROM dbo.indent_cons_items c
INNER JOIN dbo.masitems m ON m.item_id = c.item_id
ORDER BY item_name";

            var list = new List<PoEquipmentOptionDto> { new() { ItemId = 0, ItemName = "--All--", ItemCodeAsPerTender = "0" } };

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new PoEquipmentOptionDto
                    {
                        ItemId = Convert.ToInt32(reader["item_id"]),
                        ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                        ItemCodeAsPerTender = reader["item_code_as_per_tender"]?.ToString() ?? string.Empty,
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading equipment list.", detail = ex.Message });
            }
        }

        [HttpGet("po-dashboard")]
        public async Task<IActionResult> GetPoDashboard(
            [FromQuery] int userId,
            [FromQuery] int financialYearId = 0,
            [FromQuery] string? itemCode = null)
        {
            if (userId <= 0)
                return BadRequest(new { message = "userId is required." });

            if (financialYearId <= 0 && string.IsNullOrWhiteSpace(itemCode))
                return BadRequest(new { message = "Please select PO Year or Equipment." });

            var sql = @"
SELECT A.PO_ID,
       CONVERT(VARCHAR, a.po_date, 103) AS po_date,
       a.PO_NO,
       b.NAME AS SUPPLIER_NAME,
       c.TENDER_NO,
       CONVERT(VARCHAR, c.TENDER_DATE, 103) AS TENDER_DATE,
       R.item_name AS ITEM_NAME,
       R.ITEM_CODE_AS_PER_TENDER AS CODE,
       E.YEAR AS POYear,
       iy.year AS indentYear,
       CONVERT(VARCHAR, id.consolidated_date, 103) AS IndentDT,
       im.indent_quantity,
       pi.quantity AS POQTY,
       ltp.filePathAccessories,
       ltp.filePathReagent,
       ltp.tender_item_id
FROM dbo.PURCHASE_ORDER a
INNER JOIN dbo.po_items pi ON pi.po_id = a.po_id
INNER JOIN dbo.indent_consolidation id ON id.indent_consolidation_id = pi.INDENT_CONSOLIDATION_ID
INNER JOIN dbo.indent i ON i.indent_id = pi.indent_id
INNER JOIN dbo.indent_items im ON im.indent_item_id = pi.indent_item_id AND im.indent_id = pi.indent_id
INNER JOIN dbo.indent_cons_items ci ON ci.indent_cons_items_id = i.indent_cons_items_id AND ci.indent_consolidated_id = id.indent_consolidation_id
INNER JOIN dbo.maslocations l ON l.location_id = pi.consignee_id
INNER JOIN dbo.MASSUPPLIERS b ON a.SUPPLIER_ID = b.SUPPLIER_ID
INNER JOIN dbo.MASITEMS R ON R.ITEM_ID = a.ITEM_ID
INNER JOIN dbo.TENDERS c ON c.TENDER_ID = a.TENDER_ID
INNER JOIN dbo.tender_items ti ON ti.tender_id = c.tender_id AND ti.item_id = pi.item_id
INNER JOIN dbo.live_tender_price ltp ON ltp.supplier_id = a.supplier_id AND ltp.tender_item_id = ti.tender_item_id
INNER JOIN dbo.MAS_FINANCIAL_YEAR E ON E.FINANCIAL_YEAR_ID = a.FINANCIAL_YEAR_ID
INNER JOIN dbo.mas_financial_year iy ON iy.financial_year_id = id.financial_year_id
WHERE a.status NOT IN ('Incomplete', 'Waiting For Approval', 'Cancelled')
  AND l.user_id = @UserId
  AND (@FinancialYearId = 0 OR a.FINANCIAL_YEAR_ID = @FinancialYearId)
  AND (@ItemCode IS NULL OR @ItemCode = '' OR @ItemCode = '0' OR R.item_code_as_per_tender = @ItemCode)
ORDER BY a.po_date DESC";

            var list = new List<PoDashboardRowDto>();

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId);
                cmd.Parameters.AddWithValue("@ItemCode", string.IsNullOrWhiteSpace(itemCode) ? DBNull.Value : itemCode.Trim());

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new PoDashboardRowDto
                    {
                        PoId = Convert.ToInt32(reader["PO_ID"]),
                        ItemName = reader["ITEM_NAME"]?.ToString() ?? string.Empty,
                        Code = reader["CODE"]?.ToString() ?? string.Empty,
                        IndentDt = reader["IndentDT"]?.ToString() ?? string.Empty,
                        IndentYear = reader["indentYear"]?.ToString() ?? string.Empty,
                        IndentQuantity = reader["indent_quantity"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["indent_quantity"]),
                        PoNo = reader["PO_NO"]?.ToString() ?? string.Empty,
                        PoDate = reader["po_date"]?.ToString() ?? string.Empty,
                        PoYear = reader["POYear"]?.ToString() ?? string.Empty,
                        PoQty = reader["POQTY"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["POQTY"]),
                        TenderNo = reader["TENDER_NO"]?.ToString() ?? string.Empty,
                        TenderDate = reader["TENDER_DATE"]?.ToString() ?? string.Empty,
                        SupplierName = reader["SUPPLIER_NAME"]?.ToString() ?? string.Empty,
                        FilePathReagent = reader["filePathReagent"]?.ToString(),
                        FilePathAccessories = reader["filePathAccessories"]?.ToString(),
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading purchase orders.", detail = ex.Message });
            }
        }

        [HttpGet("po-receipt-items")]
        public async Task<IActionResult> GetPoReceiptItems()
        {
            const string sql = @"
SELECT A.ITEM_CODE_AS_PER_TENDER, A.item_name
FROM dbo.MASITEMS A
WHERE A.PARENT_ITEM_ID IS NOT NULL
ORDER BY A.item_name";

            var list = new List<PoEquipmentOptionDto> { new() { ItemId = 0, ItemName = "--All--", ItemCodeAsPerTender = "0" } };

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new PoEquipmentOptionDto
                    {
                        ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                        ItemCodeAsPerTender = reader["ITEM_CODE_AS_PER_TENDER"]?.ToString() ?? string.Empty,
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading items.", detail = ex.Message });
            }
        }

        /// <summary>Facilitypo_supply_editDME.aspx — PO receipt desk list.</summary>
        [HttpGet("po-receipt-desk")]
        public async Task<IActionResult> GetPoReceiptDesk(
            [FromQuery] int userId,
            [FromQuery] string? authorityId = null,
            [FromQuery] int financialYearId = 0,
            [FromQuery] string? itemCode = null)
        {
            if (userId <= 0)
                return BadRequest(new { message = "userId is required." });

            var sql = @"
SELECT a.po_item_id, a.po_id, a.quantity, a.consignee_id,
       c1.location_name, CONVERT(VARCHAR, b.po_date, 103) AS po_date, b.PO_NO,
       c.item_name, c.item_code_as_per_tender AS item_code, s.name AS supplier_name,
       x.single_unit_price * a.quantity AS Total_Price
FROM dbo.po_items a
INNER JOIN dbo.MASITEMS R ON R.ITEM_ID = a.item_id
INNER JOIN dbo.purchase_order b ON a.po_id = b.po_id
INNER JOIN dbo.MASSUPPLIERS s ON s.SUPPLIER_ID = b.supplier_id
LEFT OUTER JOIN (
    SELECT F.SUPPLIER_ID, F.TENDER_ID, D.ITEM_ID, D.single_unit_price
    FROM dbo.AWARD_OF_CONTRACT F
    INNER JOIN dbo.CONTRACT_ITEMS D ON D.AWARD_OF_CONTRACT_ID = F.AWARD_OF_CONTRACT_ID
) x ON x.TENDER_ID = b.TENDER_ID AND b.SUPPLIER_ID = x.SUPPLIER_ID AND a.ITEM_ID = x.ITEM_ID
LEFT OUTER JOIN dbo.maslocations c1 ON c1.location_id = a.consignee_id
LEFT OUTER JOIN dbo.masitems c ON a.item_id = c.item_id
WHERE c1.user_id = @UserId
  AND b.status NOT IN ('Incomplete', 'Waiting For Approval', 'Cancelled')
  AND (@AuthorityId IS NULL OR @AuthorityId = '' OR c1.authority = @AuthorityId)
  AND (@FinancialYearId = 0 OR b.financial_year_id = @FinancialYearId)
  AND (@ItemCode IS NULL OR @ItemCode = '' OR @ItemCode = '0' OR R.item_code_as_per_tender = @ItemCode)
ORDER BY b.po_date";

            try
            {
                var rows = new List<PoReceiptDeskRowDto>();
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@AuthorityId", string.IsNullOrWhiteSpace(authorityId) ? DBNull.Value : authorityId.Trim());
                cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId);
                cmd.Parameters.AddWithValue("@ItemCode", string.IsNullOrWhiteSpace(itemCode) ? DBNull.Value : itemCode.Trim());

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new PoReceiptDeskRowDto
                    {
                        PoItemId = Convert.ToInt32(reader["po_item_id"]),
                        PoId = Convert.ToInt32(reader["po_id"]),
                        ConsigneeId = Convert.ToInt32(reader["consignee_id"]),
                        LocationName = reader["location_name"]?.ToString() ?? string.Empty,
                        PoNo = reader["PO_NO"]?.ToString() ?? string.Empty,
                        PoDate = reader["po_date"]?.ToString() ?? string.Empty,
                        ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                        ItemCode = reader["item_code"]?.ToString() ?? string.Empty,
                        SupplierName = reader["supplier_name"]?.ToString() ?? string.Empty,
                        Quantity = reader["quantity"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["quantity"]),
                        TotalPrice = reader["Total_Price"] == DBNull.Value ? null : Convert.ToDecimal(reader["Total_Price"]),
                    });
                }

                foreach (var row in rows)
                {
                    row.Batches = await LoadReceiptBatchesAsync(conn, row.PoId, row.ConsigneeId);
                }

                return Ok(rows);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading PO receipt desk.", detail = ex.Message });
            }
        }

        private static async Task<List<PoReceiptBatchDto>> LoadReceiptBatchesAsync(SqlConnection conn, int poId, int locationId)
        {
            const string sql = @"
SELECT d.Issue_id,
       CONVERT(VARCHAR, Tentative_Sdate, 103) AS Tentative_Sdate,
       CASE WHEN re.receipt_no IS NOT NULL THEN re.receipt_no ELSE '' END AS receipt_no,
       CASE
           WHEN re.status IS NOT NULL THEN CASE WHEN re.status = 'C' THEN 'Installation Completed' ELSE 'Installation Pending' END
           ELSE CASE WHEN d.status = 'C' THEN 'Receipt' ELSE 'Not Supplied' END
       END AS SupplyStatus,
       CONVERT(VARCHAR, dispatch_date, 103) AS dispatch_date,
       dispatch_no,
       SUM(i.Supplyqty) AS quantity,
       d.po_id,
       d.location_id,
       CASE WHEN re.recieved_date IS NULL THEN 'Not Receipt' ELSE CONVERT(VARCHAR, re.recieved_date, 103) END AS recieved_date,
       re.receipt_id
FROM dbo.SupplierDispatch d
INNER JOIN dbo.Issue_item_details i ON d.Issue_id = i.Issue_id
LEFT OUTER JOIN (
    SELECT r.issue_id, r.recieved_date, r.po_id, r.location_id, r.status, r.receipt_no, r.receipt_id
    FROM dbo.receipts r
    WHERE r.status = 'C'
) re ON re.issue_id = d.Issue_id AND re.po_id = d.po_id AND re.location_id = d.location_id
WHERE d.po_id = @PoId AND d.location_id = @LocationId
GROUP BY re.receipt_id, d.Issue_id, dispatch_date, Tentative_Sdate, d.status, dispatch_no,
         d.po_id, d.location_id, re.recieved_date, re.status, re.receipt_no";

            var list = new List<PoReceiptBatchDto>();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@PoId", poId);
            cmd.Parameters.AddWithValue("@LocationId", locationId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new PoReceiptBatchDto
                {
                    IssueId = reader["Issue_id"]?.ToString() ?? string.Empty,
                    TentativeSupplyDate = reader["Tentative_Sdate"]?.ToString() ?? string.Empty,
                    ReceiptNo = reader["receipt_no"]?.ToString() ?? string.Empty,
                    SupplyStatus = reader["SupplyStatus"]?.ToString() ?? string.Empty,
                    DispatchDate = reader["dispatch_date"]?.ToString() ?? string.Empty,
                    DispatchNo = reader["dispatch_no"]?.ToString() ?? string.Empty,
                    SuppliedQty = reader["quantity"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["quantity"]),
                    PoId = Convert.ToInt32(reader["po_id"]),
                    LocationId = Convert.ToInt32(reader["location_id"]),
                    ReceiptDate = reader["recieved_date"]?.ToString() ?? string.Empty,
                    ReceiptId = reader["receipt_id"] == DBNull.Value ? null : Convert.ToInt32(reader["receipt_id"]),
                });
            }

            return list;
        }
    }
}
