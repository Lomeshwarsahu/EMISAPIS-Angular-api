using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EMISAPIS.Controllers
{
    /// <summary>Reports/EEL_SuggestionReport.aspx</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DMEMastersController : ControllerBase
    {
        private readonly string _connectionString;

        public DMEMastersController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection missing.");
        }

        [HttpGet("eel-suggestions")]
        public async Task<IActionResult> GetEelSuggestions()
        {
            const string sql = @"
SELECT es.id, es.name, es.mobileno, es.emailid, es.itemname, es.SUGGESTIONCONDITION,
       CASE WHEN es.cgmscsupplier = 2 THEN es.othersupplier ELSE s.name END AS supplier,
       CONVERT(VARCHAR, es.entrydt, 103) AS entrydt,
       es.UPLOADLETTER, es.UPLOADRELEVANTDOC, es.EXT
FROM dbo.eelsuggestion es
LEFT OUTER JOIN dbo.massuppliers s ON s.supplier_id = es.supplierid
ORDER BY es.entrydt DESC";

            var list = new List<EelSuggestionRowDto>();

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new EelSuggestionRowDto
                    {
                        Id = Convert.ToInt32(reader["id"]),
                        Name = reader["name"]?.ToString() ?? string.Empty,
                        MobileNo = reader["mobileno"]?.ToString() ?? string.Empty,
                        EmailId = reader["emailid"]?.ToString() ?? string.Empty,
                        Supplier = reader["supplier"]?.ToString() ?? string.Empty,
                        ItemName = reader["itemname"]?.ToString() ?? string.Empty,
                        SuggestionCondition = reader["SUGGESTIONCONDITION"]?.ToString() ?? string.Empty,
                        EntryDt = reader["entrydt"]?.ToString() ?? string.Empty,
                        UploadLetter = reader["UPLOADLETTER"]?.ToString(),
                        UploadRelevantDoc = reader["UPLOADRELEVANTDOC"]?.ToString(),
                        Ext = reader["EXT"]?.ToString(),
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading EEL suggestions.", detail = ex.Message });
            }
        }
    }
}
