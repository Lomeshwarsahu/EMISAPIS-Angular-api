using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using EMISAPIS.DTOS;
using EMISAPIS.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

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
            if (poId <= 0)
                return BadRequest(new { message = "Valid poId is required." });

            try
            {
                var list = new List<SiteNotReadyReceiptDto>();
                using var conn = new SqlConnection(ConnStr());
                await conn.OpenAsync();
                var sql = @"
SELECT r.receipt_id, r.receipt_no,
CONVERT(varchar,r.recieved_date,103) recieved_date,
r.total_rec_qty,
l.location_name,
r.SiteNotReadyFile,
r.SiteNotFlag
FROM receipts r
JOIN maslocations l ON r.location_id = l.location_id
WHERE r.status = 'Received' AND r.po_id = @poId";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@poId", poId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new SiteNotReadyReceiptDto
                    {
                        ReceiptId = reader.GetInt32(0),
                        ReceiptNo = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        RecievedDate = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        ReceiptQty = reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetValue(3)),
                        LocationName = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        SiteNotReadyFile = reader.IsDBNull(5) ? null : reader.GetString(5),
                        SiteNotFlag = reader.IsDBNull(6) ? null : reader.GetString(6),
                    });
                }
                return Ok(list);
            }
            catch
            {
                return Ok(new List<SiteNotReadyReceiptDto>());
            }
        }

        [HttpPost("upload")]
        [RequestSizeLimit(FileValidationHelper.DefaultMaxFileSizeBytes + 1024)]
        public async Task<IActionResult> Upload(
            [FromForm] int receiptId,
            IFormFile file)
        {
            if (receiptId <= 0)
                return BadRequest(new { message = "Valid receiptId is required." });

            if (!FileValidationHelper.ValidateDocumentOrImage(file, out var fileErr, maxSizeBytes: FileValidationHelper.DefaultMaxFileSizeBytes))
                return BadRequest(new { message = fileErr });

            try
            {
                var safeExt = Path.GetExtension(file.FileName).ToLowerInvariant();
                var fileName = $"SiteNotReady_{receiptId}_{Guid.NewGuid():N}{safeExt}";
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
