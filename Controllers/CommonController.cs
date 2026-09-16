using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EMISAPIS.Controllers
{
    /// <summary>Shared dropdown endpoints used by report pages (district-wise PO detail, tender status, etc.).</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CommonController : ControllerBase
    {
        private readonly string _connectionString;

        public CommonController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [HttpGet("financial-years")]
        public async Task<IActionResult> GetFinancialYears()
        {
            const string sql = @"
SELECT financial_year_id, year
FROM dbo.mas_financial_year
ORDER BY financial_year_id DESC";

            var list = new List<CommonFinancialYearDTO>();

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new CommonFinancialYearDTO
                    {
                        financial_year_id = Convert.ToInt32(reader["financial_year_id"]),
                        year = reader["year"]?.ToString() ?? string.Empty,
                    });
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading financial years.", detail = ex.Message });
            }
        }

        [HttpGet("directorates")]
        public async Task<IActionResult> GetDirectorates()
        {
            const string sql = @"
SELECT facility_aut_id, facility_aut_name
FROM dbo.facility_aut
ORDER BY ordercase, facility_aut_name";

            var list = new List<DiectorateDTO>();

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new DiectorateDTO
                    {
                        facility_aut_id = reader["facility_aut_id"] != DBNull.Value ? Convert.ToInt32(reader["facility_aut_id"]) : 0,
                        facility_aut_name = reader["facility_aut_name"]?.ToString() ?? string.Empty,
                    });
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading directorates.", detail = ex.Message });
            }
        }
    }
}
