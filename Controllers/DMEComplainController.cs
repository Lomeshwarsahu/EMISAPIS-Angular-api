using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Globalization;

namespace EMISAPIS.Controllers
{
    /// <summary>EMSRole/Complain/FacilityComplainStore.aspx</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DMEComplainController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly string _complaintFileRoot;

        public DMEComplainController(IConfiguration configuration, IWebHostEnvironment env)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection missing.");

            var configured = configuration["FileStorage:ComplaintPath"];
            _complaintFileRoot = string.IsNullOrWhiteSpace(configured)
                ? Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "EMSRole", "ComplainUploads"))
                : Path.GetFullPath(configured);

            Directory.CreateDirectory(_complaintFileRoot);
        }

        [HttpGet("items")]
        public async Task<IActionResult> GetItems([FromQuery] int userId)
        {
            if (userId <= 0)
                return BadRequest(new { message = "userId is required." });

            const string sql = @"
SELECT mi.item_id, mi.item_name + '-' + mi.item_code_as_per_tender AS item_name
FROM dbo.vcurrentstock ei
INNER JOIN dbo.masitems mi ON mi.item_id = ei.item_id
WHERE (mi.categoryid = 1 OR mi.categoryid IS NULL) AND ei.user_id = @UserId
ORDER BY mi.item_name";

            var list = new List<ComplainItemOptionDto>();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new ComplainItemOptionDto
                    {
                        ItemId = Convert.ToInt32(reader["item_id"]),
                        ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading equipment.", detail = ex.Message });
            }
        }

        [HttpGet("troubles")]
        public async Task<IActionResult> GetTroubles()
        {
            const string sql = @"
SELECT complaints_trouble_id, CAST(complaints_troubleshoot AS NVARCHAR(MAX)) AS complaints_troubleshoot
FROM dbo.complaints_trouble
ORDER BY complaints_trouble_id";

            var list = new List<ComplainTroubleOptionDto>();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new ComplainTroubleOptionDto
                    {
                        TroubleId = Convert.ToInt32(reader["complaints_trouble_id"]),
                        TroubleText = reader["complaints_troubleshoot"]?.ToString() ?? string.Empty,
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading problems.", detail = ex.Message });
            }
        }

        [HttpGet("departments")]
        public async Task<IActionResult> GetDepartments([FromQuery] int userId, [FromQuery] int itemId)
        {
            if (userId <= 0 || itemId <= 0)
                return BadRequest(new { message = "userId and itemId are required." });

            var sql = @"
SELECT e.existing_item_id AS item_detail_id,
       m.location_name + '-' + e.make_no + '-' + e.model AS item
FROM dbo.existing_item e
INNER JOIN dbo.maslocations m ON m.location_id = e.location_id
WHERE e.supplied = 'CGMSC' AND m.user_id = @UserId AND e.item_id = @ItemId
UNION ALL
SELECT ri.item_detail_id,
       l.location_name + '-' + ri.make_no + '-' + ri.model_no AS item
FROM dbo.receipts r
INNER JOIN dbo.receipt_item_details ri ON ri.receipt_id = r.receipt_id
INNER JOIN dbo.maslocations l ON l.location_id = r.location_id
INNER JOIN dbo.po_items pi ON pi.po_id = r.po_id
WHERE l.user_id = @UserId AND pi.item_id = @ItemId";

            var list = new List<ComplainDepartmentOptionDto>();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@ItemId", itemId);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new ComplainDepartmentOptionDto
                    {
                        ItemDetailId = Convert.ToInt32(reader["item_detail_id"]),
                        Label = reader["item"]?.ToString() ?? string.Empty,
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading equipment details.", detail = ex.Message });
            }
        }

        [HttpGet("equipment-detail")]
        public async Task<IActionResult> GetEquipmentDetail(
            [FromQuery] int userId,
            [FromQuery] int itemId,
            [FromQuery] int itemDetailId)
        {
            if (userId <= 0 || itemId <= 0 || itemDetailId <= 0)
                return BadRequest(new { message = "userId, itemId and itemDetailId are required." });

            var sql = @"
SELECT m.location_id, m.location_name, e.supplierId AS supplier_id, s.name, s.mobile_no, s.email_id,
       CONVERT(VARCHAR, e.warranty_upto, 103) AS warrantyUpto
FROM dbo.existing_item e
INNER JOIN dbo.maslocations m ON m.location_id = e.location_id
LEFT OUTER JOIN dbo.massuppliers s ON s.supplier_id = e.supplierId
WHERE e.supplied = 'CGMSC' AND e.existing_item_id = @ItemDetailId AND m.user_id = @UserId AND e.item_id = @ItemId
UNION ALL
SELECT l.location_id, l.location_name, p.supplier_id, s.name, s.mobile_no, s.email_id,
       CONVERT(VARCHAR, ri.warenty_to, 103) AS warrantyUpto
FROM dbo.receipts r
INNER JOIN dbo.receipt_item_details ri ON ri.receipt_id = r.receipt_id
INNER JOIN dbo.maslocations l ON l.location_id = r.location_id
INNER JOIN dbo.po_items pi ON pi.po_id = r.po_id
INNER JOIN dbo.purchase_order p ON p.po_id = pi.po_id
INNER JOIN dbo.massuppliers s ON s.supplier_id = p.supplier_id
WHERE l.user_id = @UserId AND ri.item_detail_id = @ItemDetailId AND pi.item_id = @ItemId";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@ItemId", itemId);
                cmd.Parameters.AddWithValue("@ItemDetailId", itemDetailId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound(new { message = "Equipment detail not found." });

                return Ok(new ComplainEquipmentDetailDto
                {
                    LocationId = Convert.ToInt32(reader["location_id"]),
                    LocationName = reader["location_name"]?.ToString() ?? string.Empty,
                    SupplierId = reader["supplier_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["supplier_id"]),
                    SupplierName = reader["name"]?.ToString() ?? "---",
                    SupplierMobile = reader["mobile_no"]?.ToString() ?? "---",
                    SupplierEmail = reader["email_id"]?.ToString() ?? "---",
                    WarrantyValidDate = reader["warrantyUpto"]?.ToString() ?? "---",
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading supplier details.", detail = ex.Message });
            }
        }

        [HttpPost("facility-complain")]
        public async Task<IActionResult> CreateFacilityComplain([FromForm] CreateFacilityComplainRequest req, IFormFile? file)
        {
            if (req.UserId <= 0 || req.ItemId <= 0 || req.ItemDetailId <= 0 || req.TroubleId <= 0)
                return BadRequest(new { message = "Invalid complaint data." });
            if (string.IsNullOrWhiteSpace(req.ComplainDetails))
                return BadRequest(new { message = "Complain Summary is required." });
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Please upload PDF signed copy." });
            if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Please upload PDF file only." });

            if (!TryParseLegacyDate(req.ComplainDate, out var complainDate))
                return BadRequest(new { message = "Invalid Complain Date. Use dd/MM/yyyy." });
            if (!TryParseLegacyDate(req.NotFunctionDate, out var notFunctionDate))
                return BadRequest(new { message = "Invalid Not Functioning Date. Use dd/MM/yyyy." });
            if (complainDate.Date > DateTime.Today || notFunctionDate.Date > DateTime.Today)
                return BadRequest(new { message = "Dates cannot be greater than today." });

            try
            {
                var detail = await LoadEquipmentDetailAsync(req.UserId, req.ItemId, req.ItemDetailId);
                if (detail == null)
                    return BadRequest(new { message = "Could not resolve equipment / supplier." });

                if (TryParseLegacyDate(detail.WarrantyValidDate, out var warrantyDt) && warrantyDt.Date <= DateTime.Today)
                    return BadRequest(new { message = "You cannot book a complain as Warranty date has been expired." });

                var locationId = req.LocationId > 0 ? req.LocationId : detail.LocationId;
                var supplierId = req.SupplierId > 0 ? req.SupplierId : detail.SupplierId;

                var maxId = await GetMaxComplaintIdAsync();
                maxId++;
                var complaintNo = $"COMP/{maxId}/{locationId}";

                const string insertSql = @"
INSERT INTO dbo.complaints
    (complaint_no, complaint_date, item_id, complaint_details, location_id, Serial_no, supplier_id, status,
     complaints_trouble_id, not_function_date, entryDT, user_id, supplierEmaild, supplierMob, item_detail_id)
VALUES
    (@ComplaintNo, @ComplainDate, @ItemId, @Details, @LocationId, @SerialNo, @SupplierId, 'Booked',
     @TroubleId, @NotFunctionDate, GETDATE(), @UserId, @SupplierEmail, @SupplierMobile, @ItemDetailId);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

                int complaintId;
                await using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    await using var cmd = new SqlCommand(insertSql, conn);
                    cmd.Parameters.AddWithValue("@ComplaintNo", complaintNo);
                    cmd.Parameters.AddWithValue("@ComplainDate", complainDate);
                    cmd.Parameters.AddWithValue("@ItemId", req.ItemId);
                    cmd.Parameters.AddWithValue("@Details", req.ComplainDetails.Trim());
                    cmd.Parameters.AddWithValue("@LocationId", locationId);
                    cmd.Parameters.AddWithValue("@SerialNo", req.ItemDetailId.ToString());
                    cmd.Parameters.AddWithValue("@SupplierId", supplierId);
                    cmd.Parameters.AddWithValue("@TroubleId", req.TroubleId);
                    cmd.Parameters.AddWithValue("@NotFunctionDate", notFunctionDate);
                    cmd.Parameters.AddWithValue("@UserId", req.UserId);
                    cmd.Parameters.AddWithValue("@SupplierEmail", detail.SupplierEmail);
                    cmd.Parameters.AddWithValue("@SupplierMobile", detail.SupplierMobile);
                    cmd.Parameters.AddWithValue("@ItemDetailId", req.ItemDetailId);

                    complaintId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                var fileKey = $"Comp_{locationId}_{complaintId}";
                var savePath = Path.Combine(_complaintFileRoot, fileKey + ".pdf");
                await using (var stream = new FileStream(savePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                await using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    const string updateSql = "UPDATE dbo.complaints SET path = @Path, ext = '.pdf' WHERE complaint_id = @Id";
                    await using var cmd = new SqlCommand(updateSql, conn);
                    cmd.Parameters.AddWithValue("@Path", fileKey);
                    cmd.Parameters.AddWithValue("@Id", complaintId);
                    await cmd.ExecuteNonQueryAsync();
                }

                return Ok(new { message = "Complain Booked Successfully.", complaintNo, complaintId });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error saving complaint.", detail = ex.Message });
            }
        }

        private async Task<ComplainEquipmentDetailDto?> LoadEquipmentDetailAsync(int userId, int itemId, int itemDetailId)
        {
            var sql = @"
SELECT TOP 1 m.location_id, m.location_name, e.supplierId AS supplier_id, s.name, s.mobile_no, s.email_id,
       CONVERT(VARCHAR, e.warranty_upto, 103) AS warrantyUpto
FROM dbo.existing_item e
INNER JOIN dbo.maslocations m ON m.location_id = e.location_id
LEFT OUTER JOIN dbo.massuppliers s ON s.supplier_id = e.supplierId
WHERE e.supplied = 'CGMSC' AND e.existing_item_id = @ItemDetailId AND m.user_id = @UserId AND e.item_id = @ItemId
UNION ALL
SELECT TOP 1 l.location_id, l.location_name, p.supplier_id, s.name, s.mobile_no, s.email_id,
       CONVERT(VARCHAR, ri.warenty_to, 103) AS warrantyUpto
FROM dbo.receipts r
INNER JOIN dbo.receipt_item_details ri ON ri.receipt_id = r.receipt_id
INNER JOIN dbo.maslocations l ON l.location_id = r.location_id
INNER JOIN dbo.po_items pi ON pi.po_id = r.po_id
INNER JOIN dbo.purchase_order p ON p.po_id = pi.po_id
INNER JOIN dbo.massuppliers s ON s.supplier_id = p.supplier_id
WHERE l.user_id = @UserId AND ri.item_detail_id = @ItemDetailId AND pi.item_id = @ItemId";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@ItemId", itemId);
            cmd.Parameters.AddWithValue("@ItemDetailId", itemDetailId);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            return new ComplainEquipmentDetailDto
            {
                LocationId = Convert.ToInt32(reader["location_id"]),
                LocationName = reader["location_name"]?.ToString() ?? string.Empty,
                SupplierId = reader["supplier_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["supplier_id"]),
                SupplierName = reader["name"]?.ToString() ?? "---",
                SupplierMobile = reader["mobile_no"]?.ToString() ?? "---",
                SupplierEmail = reader["email_id"]?.ToString() ?? "---",
                WarrantyValidDate = reader["warrantyUpto"]?.ToString() ?? "---",
            };
        }

        private async Task<int> GetMaxComplaintIdAsync()
        {
            const string sql = "SELECT ISNULL(MAX(complaint_id), 0) FROM dbo.complaints";
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
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
