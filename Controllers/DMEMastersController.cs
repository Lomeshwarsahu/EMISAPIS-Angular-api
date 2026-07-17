using EMISAPIS.DTOS;
using EMISAPIS.Helpers;
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
        private readonly MongoService _mongoService;

        public DMEMastersController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection missing.");
            _mongoService = new MongoService();
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

        [HttpGet("eel-suggestions/{id:int}/file")]
        public async Task<IActionResult> DownloadEelSuggestionFile(int id, [FromQuery] string docType)
        {
            if (id <= 0)
                return BadRequest(new { message = "Invalid EEL suggestion id." });

            string type = (docType ?? string.Empty).Trim().ToLowerInvariant();
            bool isLetter = type is "letter" or "doc1";
            bool isRelevant = type is "relevant" or "doc2";
            if (!isLetter && !isRelevant)
                return BadRequest(new { message = "docType must be letter or relevant." });

            try
            {
                string? uploadLetter = null;
                string? uploadRelevant = null;
                string? ext = ".pdf";

                const string sql = @"
SELECT TOP 1 UPLOADLETTER, UPLOADRELEVANTDOC, EXT
FROM dbo.eelsuggestion
WHERE id = @Id";
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                        return NotFound(new { message = "EEL suggestion not found." });
                    uploadLetter = reader["UPLOADLETTER"]?.ToString();
                    uploadRelevant = reader["UPLOADRELEVANTDOC"]?.ToString();
                    ext = reader["EXT"]?.ToString();
                }

                if (string.IsNullOrWhiteSpace(ext))
                    ext = ".pdf";
                if (!ext.StartsWith('.'))
                    ext = "." + ext;

                var mongoDoc = await _mongoService.GetEelSubmission(id);
                byte[]? bytes = isLetter ? mongoDoc?.FileDateEEL1 : mongoDoc?.FileDateEEL2;
                if (bytes == null || bytes.Length == 0)
                    return NotFound(new { message = "Document not found in file store." });

                string baseName = isLetter
                    ? (string.IsNullOrWhiteSpace(uploadLetter) ? $"EEL_{id}_Letter" : uploadLetter)
                    : (string.IsNullOrWhiteSpace(uploadRelevant) ? $"EEL_{id}_Relevant" : uploadRelevant);
                if (!baseName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    baseName += ext;

                return File(bytes, "application/pdf", baseName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error downloading EEL document.", error = ex.Message });
            }
        }
    }
}
