using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Text;

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

        /// <summary>EMSRole/Complain/ComplaintStatus.aspx - list for a store user (by user id + status).</summary>
        [HttpGet("status-list")]
        public async Task<IActionResult> GetStatusList([FromQuery] int userId, [FromQuery] string status)
        {
            if (userId <= 0)
                return BadRequest(new { message = "userId is required." });

            const string sql = @"
SELECT complaints.status, complaints.complaint_id, complaints.complaint_no,
       CONVERT(varchar, complaints.complaint_date, 103) AS complaint_date,
       complaints.item_id, complaints.complaint_details, complaints.location_id,
       complaints.supplier_id, complaints.complaints_trouble_id,
       CONVERT(varchar, complaints.not_function_date, 103) AS not_function_date,
       masitems.item_name, masitems.item_code, maslocations.location_name,
       complaints_trouble.complaints_troubleshoot, maslocations.user_id,
       users.user_name, complaints.Make_No
FROM complaints
INNER JOIN masitems ON complaints.item_id = masitems.item_id
INNER JOIN maslocations ON complaints.location_id = maslocations.location_id
INNER JOIN complaints_trouble ON complaints.complaints_trouble_id = complaints_trouble.complaints_trouble_id
INNER JOIN users ON maslocations.user_id = users.user_id
WHERE users.user_id = @UserId AND complaints.status = @Status";

            try
            {
                var list = new List<ComplainStatusRowDto>();
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@Status", string.IsNullOrWhiteSpace(status) ? "Booked" : status.Trim());
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(ReadStatusRow(reader));
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading complaints.", detail = ex.Message });
            }
        }

        /// <summary>EMSRole/Complain/ComplaintStatusFacility.aspx - list for a facility (by location id + status).</summary>
        [HttpGet("facility-status-list")]
        public async Task<IActionResult> GetFacilityStatusList([FromQuery] int locationId, [FromQuery] string status)
        {
            if (locationId <= 0)
                return BadRequest(new { message = "locationId is required." });

            const string sql = @"
SELECT c.complaint_id, c.status, c.complaint_no,
       CONVERT(varchar, c.complaint_date, 103) AS complaint_date,
       c.item_id, c.complaint_details, c.location_id, c.supplier_id,
       c.complaints_trouble_id, CONVERT(varchar, c.not_function_date, 103) AS not_function_date,
       m.item_name, l.location_name, m.item_code_as_per_tender AS item_code,
       l.user_id, c.Serial_no, mas.name AS supplier_name, mas.email_id, mas.mobile_no,
       '.pdf' AS ext, c.path, c.complaint_id AS extension_id
FROM complaints c
INNER JOIN masitems m ON m.item_id = c.item_id
INNER JOIN maslocations l ON l.location_id = c.location_id
INNER JOIN massuppliers mas ON mas.supplier_id = c.supplier_id
WHERE l.location_id = @LocationId AND c.status = @Status";

            try
            {
                var list = new List<ComplainStatusRowDto>();
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@LocationId", locationId);
                cmd.Parameters.AddWithValue("@Status", string.IsNullOrWhiteSpace(status) ? "Booked" : status.Trim());
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(ReadStatusRow(reader));
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading complaints.", detail = ex.Message });
            }
        }

        /// <summary>EMSRole/Complain/ComplainCMHO.aspx - list for CMHO (by district of the logged-in user + status).</summary>
        [HttpGet("cmho-status-list")]
        public async Task<IActionResult> GetCmhoStatusList([FromQuery] int userId, [FromQuery] string status)
        {
            if (userId <= 0)
                return BadRequest(new { message = "userId is required." });

            const string sql = @"
SELECT c.complaint_id, c.complaint_no,
       CONVERT(varchar, c.complaint_date, 103) AS complaint_date,
       c.item_id, c.complaint_details, c.location_id, c.supplier_id,
       c.complaints_trouble_id, CONVERT(varchar, c.not_function_date, 103) AS not_function_date,
       m.item_name, l.location_name, m.item_code_as_per_tender AS item_code,
       l.user_id, c.Serial_no, mas.name AS supplier_name, mas.email_id, mas.mobile_no,
       c.path, c.ext, c.complaint_id AS extension_id,
       u.user_name AS store_name
FROM complaints c
INNER JOIN masitems m ON m.item_id = c.item_id
INNER JOIN maslocations l ON l.location_id = c.location_id
INNER JOIN Districts d ON d.DP_DistrictID = l.DP_DistrictID
INNER JOIN massuppliers mas ON mas.supplier_id = c.supplier_id
INNER JOIN users u ON u.user_id = l.user_id
WHERE 1 = 1 AND c.status = @Status
  AND (@DistrictId = 0 OR d.DP_DistrictID = @DistrictId)
ORDER BY l.location_name";

            try
            {
                var districtId = await GetUserDistrictIdAsync(userId);
                var list = new List<ComplainStatusRowDto>();
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@DistrictId", districtId);
                cmd.Parameters.AddWithValue("@Status", string.IsNullOrWhiteSpace(status) ? "Booked" : status.Trim());
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(ReadStatusRow(reader));
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading complaints.", detail = ex.Message });
            }
        }

        /// <summary>EMSRole/Complain/ComplaintStatusEdit.aspx + ComplaintStatusFacilityEdit.aspx - detail view.</summary>
        [HttpGet("detail/{complaintId:int}")]
        public async Task<IActionResult> GetDetail(int complaintId)
        {
            const string sql = @"
SELECT c.complaint_id, c.complaint_no, mi.item_name,
       CONVERT(varchar, c.complaint_date, 103) AS complaint_date,
       CONVERT(varchar, c.not_function_date, 103) AS not_function_date,
       ct.complaints_troubleshoot, ml.location_name, ms.name AS supplier_name,
       ms.mobile_no, ms.email_id, c.Serial_no, ml.location_id, ms.supplier_id,
       c.complaint_details,
       CONVERT(varchar, c.comp_closed_on, 103) AS comp_closed_on,
       c.status, CONVERT(varchar, c.supplier_service_date, 103) AS supplier_service_date,
       c.corrective_action_taken, c.preventive_action, c.changed_parts, c.parts_replaced,
       ISNULL(CONVERT(varchar, ei.warranty_upto, 103), '') AS warranty_upto
FROM complaints c
INNER JOIN masitems mi ON mi.item_id = c.item_id
INNER JOIN complaints_trouble ct ON ct.complaints_trouble_id = c.complaints_trouble_id
INNER JOIN maslocations ml ON ml.location_id = c.location_id
INNER JOIN massuppliers ms ON ms.supplier_id = c.supplier_id
LEFT OUTER JOIN existing_item ei ON ei.item_id = mi.item_id
WHERE c.complaint_id = @ComplaintId";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ComplaintId", complaintId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound(new { message = "Complaint not found." });

                return Ok(new ComplainDetailDto
                {
                    ComplaintId = Convert.ToInt32(reader["complaint_id"]),
                    ComplaintNo = reader["complaint_no"]?.ToString() ?? string.Empty,
                    ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                    ComplaintDate = reader["complaint_date"]?.ToString() ?? string.Empty,
                    NotFunctionDate = reader["not_function_date"]?.ToString() ?? string.Empty,
                    ComplainTroubleshoot = reader["complaints_troubleshoot"]?.ToString() ?? string.Empty,
                    LocationName = reader["location_name"]?.ToString() ?? string.Empty,
                    SupplierName = reader["supplier_name"]?.ToString() ?? string.Empty,
                    SupplierMobile = reader["mobile_no"]?.ToString() ?? string.Empty,
                    SupplierEmail = reader["email_id"]?.ToString() ?? string.Empty,
                    WarrantyValidDate = reader["warranty_upto"]?.ToString() ?? string.Empty,
                    MakeNo = reader["Serial_no"]?.ToString() ?? string.Empty,
                    SerialNo = reader["Serial_no"]?.ToString() ?? string.Empty,
                    LocationId = Convert.ToInt32(reader["location_id"]),
                    SupplierId = Convert.ToInt32(reader["supplier_id"]),
                    ComplaintDetails = reader["complaint_details"]?.ToString() ?? string.Empty,
                    CompClosedOn = reader["comp_closed_on"]?.ToString() ?? string.Empty,
                    Status = reader["status"]?.ToString() ?? string.Empty,
                    SupplierServiceDate = reader["supplier_service_date"]?.ToString() ?? string.Empty,
                    CorrectiveActionTaken = reader["corrective_action_taken"]?.ToString() ?? string.Empty,
                    PreventiveAction = reader["preventive_action"]?.ToString() ?? string.Empty,
                    ChangedParts = reader["changed_parts"]?.ToString() ?? string.Empty,
                    PartsReplaced = reader["parts_replaced"]?.ToString() ?? string.Empty,
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading complaint detail.", detail = ex.Message });
            }
        }

        /// <summary>EMSRole/Complain/ComplaintStatusEdit.aspx + ComplaintStatusFacilityEdit.aspx - close/update complaint.</summary>
        [HttpPost("close-complaint")]
        public async Task<IActionResult> CloseComplaint([FromBody] CloseComplainRequest req)
        {
            if (req.ComplaintId <= 0)
                return BadRequest(new { message = "complaintId is required." });
            if (string.IsNullOrWhiteSpace(req.CompClosedOn))
                return BadRequest(new { message = "Complain Close Date is required." });
            if (string.IsNullOrWhiteSpace(req.SupplierServiceDate))
                return BadRequest(new { message = "Supplier Service Date is required." });
            if (req.PartsReplaced < 0)
                return BadRequest(new { message = "No of Parts Replaced must be zero or more." });

            if (!TryParseLegacyDate(req.CompClosedOn, out var closeDate))
                return BadRequest(new { message = "Invalid Complain Close Date. Use dd/MM/yyyy." });
            if (!TryParseLegacyDate(req.SupplierServiceDate, out var serviceDate))
                return BadRequest(new { message = "Invalid Supplier Service Date. Use dd/MM/yyyy." });
            if (serviceDate.Date > DateTime.Today)
                return BadRequest(new { message = "Supplier Service Date cannot be greater than today." });

            var preventiveAction = req.CorrectiveActionTaken?.Trim() == "Yes"
                ? (req.PreventiveAction ?? string.Empty).Trim()
                : string.Empty;

            const string sql = @"
UPDATE complaints
SET comp_closed_on = @CloseDate,
    status = @Status,
    preventive_action = @PreventiveAction,
    corrective_action_taken = @CorrectiveActionTaken,
    parts_replaced = @PartsReplaced,
    changed_parts = @ChangedParts,
    preventive_action_taken = @CorrectiveActionTaken,
    supplier_service_date = @ServiceDate
WHERE complaint_id = @ComplaintId";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@CloseDate", closeDate);
                cmd.Parameters.AddWithValue("@Status", string.IsNullOrWhiteSpace(req.Status) ? "Closed" : req.Status.Trim());
                cmd.Parameters.AddWithValue("@PreventiveAction", preventiveAction);
                cmd.Parameters.AddWithValue("@CorrectiveActionTaken", req.CorrectiveActionTaken ?? string.Empty);
                cmd.Parameters.AddWithValue("@PartsReplaced", req.PartsReplaced);
                cmd.Parameters.AddWithValue("@ChangedParts", string.IsNullOrWhiteSpace(req.ChangedParts) ? "NotChanged" : req.ChangedParts.Trim());
                cmd.Parameters.AddWithValue("@ServiceDate", serviceDate);
                cmd.Parameters.AddWithValue("@ComplaintId", req.ComplaintId);
                var affected = await cmd.ExecuteNonQueryAsync();
                if (affected == 0)
                    return NotFound(new { message = "Complaint not found." });

                return Ok(new { message = "Complaint Status Successfully Changed." });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error updating complaint.", detail = ex.Message });
            }
        }

        /// <summary>EMSRole/Complain/DisplayComFile.ashx - serve uploaded complaint letter.</summary>
        [HttpGet("letter/{complaintId:int}")]
        public async Task<IActionResult> GetComplaintLetter(int complaintId)
        {
            const string sql = @"
SELECT path, ext, location_id
FROM dbo.complaints
WHERE complaint_id = @ComplaintId";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ComplaintId", complaintId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound(new { message = "Complaint not found." });

                var fileKey = reader["path"]?.ToString();
                var ext = reader["ext"]?.ToString();
                if (string.IsNullOrWhiteSpace(fileKey))
                    return NotFound(new { message = "Complaint letter not uploaded." });

                var fileName = fileKey + (string.IsNullOrWhiteSpace(ext) ? ".pdf" : ext.Trim());
                var fullPath = Path.Combine(_complaintFileRoot, fileName);
                if (!System.IO.File.Exists(fullPath))
                    return NotFound(new { message = "Complaint letter file not found." });

                var contentType = ".jpg".Equals(ext, StringComparison.OrdinalIgnoreCase) || ".jpeg".Equals(ext, StringComparison.OrdinalIgnoreCase)
                    ? "image/jpeg"
                    : ".png".Equals(ext, StringComparison.OrdinalIgnoreCase)
                        ? "image/png"
                        : "application/pdf";
                var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
                return File(bytes, contentType, Path.GetFileName(fileName));
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading complaint letter.", detail = ex.Message });
            }
        }

        private async Task<int> GetUserDistrictIdAsync(int userId)
        {
            const string sql = @"
SELECT TOP 1 l.DP_DistrictID
FROM users u
INNER JOIN maslocations l ON l.location_id = u.location_id
WHERE u.user_id = @UserId";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        private static ComplainStatusRowDto ReadStatusRow(SqlDataReader reader)
        {
            bool Has(string name)
            {
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    if (string.Equals(reader.GetName(i), name, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }

            int SafeInt(string name) => Has(name) && reader[name] != DBNull.Value ? Convert.ToInt32(reader[name]) : 0;
            string SafeStr(string name) => Has(name) && reader[name] != DBNull.Value ? reader[name].ToString() ?? string.Empty : string.Empty;

            var storeName = SafeStr("store_name");
            if (string.IsNullOrWhiteSpace(storeName))
                storeName = SafeStr("user_name");

            return new ComplainStatusRowDto
            {
                ComplaintId = SafeInt("complaint_id"),
                ComplaintNo = SafeStr("complaint_no"),
                Status = SafeStr("status"),
                ComplaintDate = SafeStr("complaint_date"),
                NotFunctionDate = SafeStr("not_function_date"),
                ItemId = SafeInt("item_id"),
                ItemName = SafeStr("item_name"),
                ItemCode = SafeStr("item_code"),
                MakeNo = SafeStr("Make_No"),
                SerialNo = SafeStr("Serial_no"),
                ComplainTroubleshoot = SafeStr("complaints_troubleshoot"),
                LocationName = SafeStr("location_name"),
                StoreName = storeName,
                SupplierName = SafeStr("supplier_name"),
                SupplierMobile = SafeStr("mobile_no"),
                SupplierEmail = SafeStr("email_id"),
                ComplaintDetails = SafeStr("complaint_details"),
                Path = SafeStr("path"),
                Ext = SafeStr("ext"),
                ExtensionId = SafeInt("extension_id") != 0 ? SafeInt("extension_id") : SafeInt("complaint_id"),
                LocationId = SafeInt("location_id"),
                SupplierId = SafeInt("supplier_id"),
            };
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
