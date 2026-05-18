using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EMISAPIS.Controllers
{
    /// <summary>EMSRole/Reports/CMCdetail.aspx</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DMEReportsController : ControllerBase
    {
        private readonly string _connectionString;

        public DMEReportsController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection missing.");
        }

        [HttpGet("cmc-items")]
        public async Task<IActionResult> GetCmcItems()
        {
            const string sql = @"
SELECT A.ITEM_CODE_AS_PER_TENDER, A.item_code_as_per_tender + '-' + A.item_name AS item_name
FROM dbo.MASITEMS A
INNER JOIN dbo.tender_items TI ON TI.item_id = A.item_id
WHERE A.item_code_as_per_tender IS NOT NULL
ORDER BY A.ITEM_CODE_AS_PER_TENDER";

            var list = new List<CmcItemOptionDto> { new() { ItemCodeAsPerTender = "0", ItemName = "--ALL--" } };

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new CmcItemOptionDto
                    {
                        ItemCodeAsPerTender = reader["ITEM_CODE_AS_PER_TENDER"]?.ToString() ?? string.Empty,
                        ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading items.", detail = ex.Message });
            }
        }

        [HttpGet("cmc-tenders")]
        public async Task<IActionResult> GetCmcTenders([FromQuery] string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode) || itemCode == "0")
                return Ok(new List<CmcTenderOptionDto> { new() { TenderId = 0, TenderNo = "--Select--" } });

            const string sql = @"
SELECT i.item_id, t.tender_id, t.tender_no
FROM dbo.masitems i
INNER JOIN dbo.tender_items ti ON ti.item_id = i.item_id
INNER JOIN dbo.tenders t ON t.tender_id = ti.tender_id
WHERE i.item_code_as_per_tender = @ItemCode";

            var list = new List<CmcTenderOptionDto> { new() { TenderId = 0, TenderNo = "--Select--" } };

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ItemCode", itemCode.Trim());
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new CmcTenderOptionDto
                    {
                        TenderId = Convert.ToInt32(reader["tender_id"]),
                        TenderNo = reader["tender_no"]?.ToString() ?? string.Empty,
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading tenders.", detail = ex.Message });
            }
        }

        [HttpGet("cmc-detail")]
        public async Task<IActionResult> GetCmcDetail(
            [FromQuery] string? itemCode = null,
            [FromQuery] int tenderId = 0)
        {
            var sql = @"
SELECT i.item_id, t.tender_id, i.item_code, i.item_code_as_per_tender, i.item_name, t.tender_no,
       tp.CMC1, tp.CMC2, tp.CMC3, tp.CMC4, tp.CMC5
FROM dbo.masitems i
INNER JOIN dbo.tender_items ti ON ti.item_id = i.item_id
INNER JOIN dbo.tenders t ON t.tender_id = ti.tender_id
INNER JOIN dbo.live_tender_price tp ON tp.tender_item_id = ti.tender_item_id
WHERE 1 = 1
  AND (@ItemCode IS NULL OR @ItemCode = '' OR @ItemCode = '0' OR i.item_code_as_per_tender = @ItemCode)
  AND (@TenderId = 0 OR t.tender_id = @TenderId)
ORDER BY i.item_code_as_per_tender";

            var list = new List<CmcDetailRowDto>();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ItemCode", string.IsNullOrWhiteSpace(itemCode) ? DBNull.Value : itemCode.Trim());
                cmd.Parameters.AddWithValue("@TenderId", tenderId);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new CmcDetailRowDto
                    {
                        ItemId = Convert.ToInt32(reader["item_id"]),
                        TenderId = Convert.ToInt32(reader["tender_id"]),
                        ItemCode = reader["item_code"]?.ToString() ?? string.Empty,
                        ItemCodeAsPerTender = reader["item_code_as_per_tender"]?.ToString() ?? string.Empty,
                        ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                        TenderNo = reader["tender_no"]?.ToString() ?? string.Empty,
                        Cmc1 = reader["CMC1"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["CMC1"]),
                        Cmc2 = reader["CMC2"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["CMC2"]),
                        Cmc3 = reader["CMC3"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["CMC3"]),
                        Cmc4 = reader["CMC4"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["CMC4"]),
                        Cmc5 = reader["CMC5"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["CMC5"]),
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading CMC detail.", detail = ex.Message });
            }
        }
    }
}
