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
    }
}
