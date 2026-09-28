using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EMISAPIS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LogoVerifiedHOController : ControllerBase
    {
        private readonly IConfiguration _config;
        public LogoVerifiedHOController(IConfiguration config) => _config = config;
        private string ConnStr() => _config.GetConnectionString("DefaultConnection")!;

        [HttpGet("batches")]
        public async Task<IActionResult> GetBatches([FromQuery] int poId)
        {
            try
            {
                var list = new List<LogoVerifiedBatchDto>();
                using var conn = new SqlConnection(ConnStr());
                await conn.OpenAsync();
                var sql = @"
SELECT ri.item_detail_id, ri.model_no, ri.make_no,
CONVERT(varchar,ri.installation_date,103) installation_date,
m.location_name,
CASE WHEN DATEDIFF(DAY, ri.installation_date, GETDATE())>=42 THEN 'Yes' ELSE 'No' END Div100Per,
ISNULL(ri.ISUserRecom,'N') ISUserRecom,
ISNULL(ri.cgmsc_log_printed,'NA') cgmsc_log_printed,
ISNULL(ri.InstalationReportFile,'') InstalationReportFile,
ISNULL(ri.Challanfile,'') Challanfile,
ISNULL(ri.WarrantyCardFile,'') WarrantyCardFile,
ri.receipt_id
FROM receipt_item_details ri
INNER JOIN receipts r ON r.receipt_id = ri.receipt_id
INNER JOIN maslocations m ON m.location_id = r.location_id
WHERE r.po_id = @poId";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@poId", poId);
                using var dr = await cmd.ExecuteReaderAsync();
                while (await dr.ReadAsync())
                {
                    list.Add(new LogoVerifiedBatchDto
                    {
                        ItemDetailId = Convert.ToInt32(dr["item_detail_id"]),
                        ModelNo = dr["model_no"]?.ToString() ?? "",
                        Make = dr["make_no"]?.ToString() ?? "",
                        InstallationDate = dr["installation_date"]?.ToString() ?? "",
                        LocationName = dr["location_name"]?.ToString() ?? "",
                        Div100Per = dr["Div100Per"]?.ToString() ?? "",
                        IsUserRecom = dr["ISUserRecom"]?.ToString() ?? "",
                        CgmscLogPrinted = dr["cgmsc_log_printed"]?.ToString() ?? "",
                        InstalationReportFile = dr["InstalationReportFile"]?.ToString() ?? "",
                        ChallanFile = dr["Challanfile"]?.ToString() ?? "",
                        WarrantyCardFile = dr["WarrantyCardFile"]?.ToString() ?? "",
                        ReceiptId = Convert.ToInt32(dr["receipt_id"])
                    });
                }
                return Ok(list);
            }
            catch { return Ok(new List<LogoVerifiedBatchDto>()); }
        }

        [HttpPost("save")]
        public async Task<IActionResult> Save([FromBody] List<LogoVerifiedSaveDto> items)
        {
            try
            {
                using var conn = new SqlConnection(ConnStr());
                await conn.OpenAsync();
                foreach (var item in items)
                {
                    var sql = @"UPDATE receipt_item_details SET 
ISUserRecom = @userRecom,
LogoCharges_HOVerified = @logoVerified
WHERE item_detail_id = @id";
                    using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@id", item.ItemDetailId);
                    cmd.Parameters.AddWithValue("@userRecom", item.UserRecom);
                    cmd.Parameters.AddWithValue("@logoVerified", item.LogoVerified);
                    await cmd.ExecuteNonQueryAsync();
                }
                return Ok(new { message = "Saved" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
