using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Globalization;

namespace EMISAPIS.Controllers
{
    /// <summary>EMSRole indent pages: DMEFACHeads.aspx, ConsolidatedIndentDME_MC.aspx.</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DMEIndentController : ControllerBase
    {
        private readonly string _connectionString;

        public DMEIndentController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection missing.");
        }

        [HttpGet("budget-heads")]
        public async Task<IActionResult> GetBudgetHeads([FromQuery] int userId)
        {
            if (userId <= 0)
                return BadRequest(new { message = "userId is required." });

            const string sql = @"
SELECT headID, headno, headName
FROM dbo.DMEFACHead
WHERE user_id = @UserId
ORDER BY headName";

            var list = new List<DmeFacHeadDto>();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new DmeFacHeadDto
                    {
                        HeadId = Convert.ToInt32(reader["headID"]),
                        HeadNo = reader["headno"]?.ToString() ?? string.Empty,
                        HeadName = reader["headName"]?.ToString() ?? string.Empty,
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading budget heads.", detail = ex.Message });
            }
        }

        [HttpPost("budget-heads")]
        public async Task<IActionResult> CreateBudgetHead([FromBody] CreateDmeFacHeadRequest req)
        {
            if (req.UserId <= 0)
                return BadRequest(new { message = "userId is required." });
            if (string.IsNullOrWhiteSpace(req.HeadNo) || string.IsNullOrWhiteSpace(req.HeadName))
                return BadRequest(new { message = "Head No and Head Name are required." });

            const string sql = @"
INSERT INTO dbo.DMEFACHead (headno, headName, user_id)
VALUES (@HeadNo, @HeadName, @UserId)";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@HeadNo", req.HeadNo.Trim());
                cmd.Parameters.AddWithValue("@HeadName", req.HeadName.Trim());
                cmd.Parameters.AddWithValue("@UserId", req.UserId);
                await cmd.ExecuteNonQueryAsync();
                return Ok(new { message = "Budget Head Saved Successfully." });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error saving budget head.", detail = ex.Message });
            }
        }

        [HttpGet("facility-indents")]
        public async Task<IActionResult> GetFacilityIndents(
            [FromQuery] int userId,
            [FromQuery] int financialYearId = 0)
        {
            if (userId <= 0)
                return BadRequest(new { message = "userId is required." });

            var sql = @"
SELECT a.indentid, u.user_name, a.USER_ID,
       CONVERT(VARCHAR(10), a.indentdate, 103) AS CONSOLIDATED_DATE,
       SUM(B.dirappqty) AS FINAL_QTY,
       COUNT(DISTINCT b.itemid) AS nosindentQTY,
       CASE WHEN a.STATUS = 'I' THEN 'Incomplete' WHEN a.STATUS = 'C' THEN 'Completed' ELSE '' END AS EStatus,
       CASE WHEN a.path IS NULL THEN 'Not Uploaded' ELSE 'Uploaded' END AS uploadStatus,
       a.financial_year_id,
       ISNULL(a.DispatchNo, '') AS DispatchNo,
       CONVERT(VARCHAR, a.DispatchDT, 103) AS dispatchdate
FROM dbo.mas_indentfacility a
LEFT OUTER JOIN dbo.mas_item_indent b ON b.indentid = a.indentid
INNER JOIN dbo.users u ON u.user_id = a.location_id
WHERE a.directorate_id = 12 AND u.user_id = @UserId
  AND (@FinancialYearId = 0 OR a.financial_year_id = @FinancialYearId)
GROUP BY a.indentid, a.USER_ID, u.user_name, a.FINANCIAL_YEAR_ID, a.STATUS, a.indentdate,
         a.path, a.DispatchNo, a.DispatchDT
ORDER BY a.indentdate DESC";

            var list = new List<FacilityIndentRowDto>();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new FacilityIndentRowDto
                    {
                        IndentId = Convert.ToInt32(reader["indentid"]),
                        McName = reader["user_name"]?.ToString() ?? string.Empty,
                        UserId = Convert.ToInt32(reader["USER_ID"]),
                        ConsolidatedDate = reader["CONSOLIDATED_DATE"]?.ToString() ?? string.Empty,
                        AsLetterNo = string.Empty,
                        AsDate = string.Empty,
                        DispatchNo = reader["DispatchNo"]?.ToString() ?? string.Empty,
                        DispatchDate = reader["dispatchdate"]?.ToString() ?? string.Empty,
                        NosIndentQty = reader["nosindentQTY"] == DBNull.Value ? 0 : Convert.ToInt32(reader["nosindentQTY"]),
                        EStatus = reader["EStatus"]?.ToString() ?? string.Empty,
                        UploadStatus = reader["uploadStatus"]?.ToString() ?? string.Empty,
                        FinancialYearId = reader["financial_year_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["financial_year_id"]),
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading facility indents.", detail = ex.Message });
            }
        }

        [HttpPost("facility-indents")]
        public async Task<IActionResult> CreateFacilityIndent([FromBody] CreateFacilityIndentRequest req)
        {
            if (req.UserId <= 0)
                return BadRequest(new { message = "userId is required." });
            if (req.BudgetId <= 0 || req.FinancialYearId <= 0)
                return BadRequest(new { message = "Budget and Financial Year are required." });
            if (string.IsNullOrWhiteSpace(req.IndentDate) || string.IsNullOrWhiteSpace(req.AsDate))
                return BadRequest(new { message = "Indent Date and AS Date are required." });

            if (!TryParseLegacyDate(req.IndentDate, out var indentDt))
                return BadRequest(new { message = "Invalid Indent Date. Use dd/MM/yyyy." });
            if (!TryParseLegacyDate(req.AsDate, out var asDt))
                return BadRequest(new { message = "Invalid AS Date. Use dd/MM/yyyy." });

            if (indentDt.Date > DateTime.Today)
                return BadRequest(new { message = "Indent Date cannot be greater than Today." });

            const string sql = @"
INSERT INTO dbo.mas_indentfacility
    (BUDGETID, indentdate, location_id, directorate_id, financial_year_id, status, entrydate, user_id, ASLetterNo, ASDate)
VALUES
    (@BudgetId, @IndentDate, @UserId, 12, @FinancialYearId, 'I', GETDATE(), @UserId, @AsLetterNo, @AsDate)";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@BudgetId", req.BudgetId);
                cmd.Parameters.AddWithValue("@IndentDate", indentDt);
                cmd.Parameters.AddWithValue("@UserId", req.UserId);
                cmd.Parameters.AddWithValue("@FinancialYearId", req.FinancialYearId);
                cmd.Parameters.AddWithValue("@AsLetterNo", req.AsLetterNo.Trim());
                cmd.Parameters.AddWithValue("@AsDate", asDt);
                await cmd.ExecuteNonQueryAsync();
                return Ok(new { message = "Indent created successfully." });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error creating indent.", detail = ex.Message });
            }
        }

        private static bool TryParseLegacyDate(string value, out DateTime result)
        {
            var formats = new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" };
            return DateTime.TryParseExact(
                value.Trim(),
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result);
        }
    }
}
