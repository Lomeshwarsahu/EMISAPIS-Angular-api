using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EMISAPIS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SiteNotReadyController : ControllerBase
    {
        private readonly IConfiguration _config;
        public SiteNotReadyController(IConfiguration config) => _config = config;
        private string ConnStr() => _config.GetConnectionString("DefaultConnection")!;

        [HttpGet("receipts")]
        public async Task<IActionResult> GetReceipts([FromQuery] int poId)
        {
            try
            {
                var list = new List<SiteNotReadyReceiptDto>();
                using var conn = new SqlConnection(ConnStr());
                await conn.OpenAsync();
                var sql = @"
SELECT r.receipt_id, r.receipt_no,
CONVERT(varchar,r.recieved_date,103) recieved_date,
r.receipt_qty,
ISNULL(r.SiteNotReadyFile,'') SiteNotReadyFile,
ISNULL(r.SiteNotFlag,'N') SiteNotFlag,
r.po_id, m.location_name
FROM receipts r
INNER JOIN maslocations m ON m.location_id = r.location_id
WHERE r.status = 'Received' AND r.po_id = @poId";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@poId", poId);
                using var dr = await cmd.ExecuteReaderAsync();
                while (await dr.ReadAsync())
                {
                    list.Add(new SiteNotReadyReceiptDto
                    {
                        ReceiptId = Convert.ToInt32(dr["receipt_id"]),
                        ReceiptNo = dr["receipt_no"]?.ToString() ?? "",
                        RecievedDate = dr["recieved_date"]?.ToString() ?? "",
                        ReceiptQty = Convert.ToDecimal(dr["receipt_qty"]),
                        LocationName = dr["location_name"]?.ToString() ?? "",
                        SiteNotReadyFile = dr["SiteNotReadyFile"]?.ToString() ?? "",
                        SiteNotFlag = dr["SiteNotFlag"]?.ToString() ?? "",
                        PoId = Convert.ToInt32(dr["po_id"])
                    });
                }
                return Ok(list);
            }
            catch { return Ok(new List<SiteNotReadyReceiptDto>()); }
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] int receiptId, [FromForm] IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { message = "No file provided" });

                var fileName = $"SiteNotReady_{receiptId}{Path.GetExtension(file.FileName)}";
                var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "sitenotready");
                Directory.CreateDirectory(uploadsDir);
                var filePath = Path.Combine(uploadsDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                using var conn = new SqlConnection(ConnStr());
                await conn.OpenAsync();
                var sql = "UPDATE receipts SET SiteNotReadyFile = @file, SiteNotFlag = 'Y' WHERE receipt_id = @id";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@file", filePath);
                cmd.Parameters.AddWithValue("@id", receiptId);
                await cmd.ExecuteNonQueryAsync();

                return Ok(new { message = "Uploaded", filePath });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
