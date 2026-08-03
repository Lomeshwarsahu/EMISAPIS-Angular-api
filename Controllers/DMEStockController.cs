using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EMISAPIS.Controllers
{
    /// <summary>EMSRole/Stock — FACStockCOVIDItemsMC, ExistingCovidItemsDME.</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DMEStockController : ControllerBase
    {
        private readonly string _connectionString;

        public DMEStockController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection missing.");
        }

        [HttpGet("main-equipment-types")]
        public async Task<IActionResult> GetMainEquipmentTypes()
        {
            const string sql = @"SELECT pid, pitemname FROM dbo.masitemP ORDER BY pitemname";
            var list = new List<MainEquipmentTypeDto>();

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new MainEquipmentTypeDto
                    {
                        Pid = reader["pid"] == DBNull.Value ? 0 : Convert.ToInt32(reader["pid"]),
                        PItemName = reader["pitemname"]?.ToString() ?? string.Empty,
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading equipment types.", detail = ex.Message });
            }
        }

        /// <summary>FACStockCOVIDItemsMC.aspx — CGMSC receipts + existing_item opening stock.</summary>
        [HttpGet("covid-stock")]
        public async Task<IActionResult> GetCovidStock(
            [FromQuery] int userId,
            [FromQuery] int? pid = null,
            [FromQuery] string filterType = "All")
        {
            if (userId <= 0)
                return BadRequest(new { message = "userId is required." });

            var whereExisting = string.Empty;
            var whereCgmsc = string.Empty;
            switch ((filterType ?? "All").ToUpperInvariant())
            {
                case "OR":
                    whereExisting = " AND A.installation_date IS NULL AND A.Receipt_Date IS NOT NULL ";
                    whereCgmsc = " AND ri.installation_date IS NULL AND r.recieved_date IS NOT NULL ";
                    break;
                case "RI":
                    whereExisting = " AND A.installation_date IS NOT NULL AND A.Receipt_Date IS NOT NULL ";
                    whereCgmsc = " AND ri.installation_date IS NOT NULL AND r.recieved_date IS NOT NULL ";
                    break;
            }

            var sql = $@"
SELECT m.item_name,
       m.item_code_as_per_tender AS Item_code,
       ri.item_detail_id AS Existing_ITEM_ID,
       ri.make AS Make,
       ri.model_no AS Model_No,
       CONVERT(VARCHAR, ri.installation_date, 106) AS Installation_Date,
       CONVERT(VARCHAR, ri.warenty_to, 106) AS Warranty_Upto,
       ri.make_no AS Make_Serial_No,
       ri.installation_location AS install_location,
       '' AS remarks,
       'CGMSC' AS SupplidFrom,
       CONVERT(VARCHAR, r.recieved_date, 106) AS Receipt_Date,
       m.pid
FROM dbo.receipt_item_details ri
INNER JOIN dbo.receipts r ON r.receipt_id = ri.receipt_id
INNER JOIN dbo.maslocations l ON l.location_id = r.location_id
INNER JOIN dbo.users u ON u.user_id = l.user_id
INNER JOIN dbo.po_items pi ON pi.po_id = r.po_id AND r.location_id = pi.consignee_id
INNER JOIN dbo.masitems m ON m.item_id = pi.item_id AND m.pid IS NOT NULL
WHERE u.user_id = @UserId {whereCgmsc}
  AND (@Pid IS NULL OR m.pid = @Pid)
UNION ALL
SELECT ms.item_name,
       ms.item_code_as_per_tender AS Item_code,
       A.existing_item_id AS Existing_ITEM_ID,
       A.make AS Make,
       A.model AS Model_No,
       CONVERT(VARCHAR, A.installation_date, 106) AS Installation_Date,
       CONVERT(VARCHAR, A.warranty_upto, 106) AS Warranty_Upto,
       A.make_no AS Make_Serial_No,
       CASE WHEN A.WardID IS NULL THEN A.install_location ELSE mw.WNAME END AS install_location,
       A.remarks,
       sm.Name AS SupplidFrom,
       CONVERT(VARCHAR, A.Receipt_Date, 106) AS Receipt_Date,
       ms.pid
FROM dbo.existing_item A
INNER JOIN dbo.masitems ms ON A.item_id = ms.item_id
LEFT OUTER JOIN dbo.MasWards mw ON mw.WID = A.wardID
INNER JOIN dbo.SupplyMaster sm ON sm.SupID = A.SUPID
WHERE A.SUPID IS NOT NULL AND A.userid = @UserId {whereExisting}
  AND (@Pid IS NULL OR ms.pid = @Pid)
ORDER BY pid";

            try
            {
                var list = await ExecuteStockReaderAsync(sql, userId, pid);
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading COVID stock.", detail = ex.Message });
            }
        }

        /// <summary>ExistingCovidItemsDME.aspx — opening stock list.</summary>
        [HttpGet("opening-stock")]
        public async Task<IActionResult> GetOpeningStock([FromQuery] int userId, [FromQuery] int? pid = null)
        {
            if (userId <= 0)
                return BadRequest(new { message = "userId is required." });

            var sql = @"
SELECT ms.item_name,
       ms.item_code_as_per_tender AS item_code,
       mp.pid AS Pid,
       A.existing_item_id AS Existing_ITEM_ID,
       CASE WHEN A.WardID IS NULL THEN A.install_location ELSE mw.WNAME END AS install_location,
       A.make AS Make,
       A.model AS Model_No,
       CONVERT(VARCHAR, A.installation_date, 106) AS Installation_Date,
       CONVERT(VARCHAR, A.warranty_upto, 106) AS Warranty_Upto,
       A.make_no AS Make_Serial_No,
       sm.Name AS SupplidFrom,
       A.remarks,
       CONVERT(VARCHAR, A.Receipt_Date, 106) AS Receipt_Date
FROM dbo.existing_item A
INNER JOIN dbo.masitems ms ON A.item_id = ms.item_id
INNER JOIN dbo.masitemP mp ON mp.pid = ms.pid
LEFT OUTER JOIN dbo.MasWards mw ON mw.WID = A.wardID
LEFT OUTER JOIN dbo.massuppliers s ON s.supplier_id = A.supplierId
INNER JOIN dbo.SupplyMaster sm ON sm.SupID = A.SUPID
WHERE A.SUPID IS NOT NULL AND A.userid = @UserId
  AND (@Pid IS NULL OR mp.pid = @Pid)
ORDER BY A.entryDt DESC";

            try
            {
                var list = await ExecuteOpeningStockReaderAsync(sql, userId, pid);
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading opening stock.", detail = ex.Message });
            }
        }

        private async Task<List<CovidStockRowDto>> ExecuteStockReaderAsync(string sql, int userId, int? pid)
        {
            var list = new List<CovidStockRowDto>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@Pid", pid.HasValue && pid.Value > 0 ? pid.Value : DBNull.Value);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new CovidStockRowDto
                {
                    ExistingItemId = Convert.ToInt32(reader["Existing_ITEM_ID"]),
                    ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                    ItemCode = reader["Item_code"]?.ToString() ?? string.Empty,
                    Make = reader["Make"]?.ToString() ?? string.Empty,
                    ModelNo = reader["Model_No"]?.ToString() ?? string.Empty,
                    MakeSerialNo = reader["Make_Serial_No"]?.ToString() ?? string.Empty,
                    InstallLocation = reader["install_location"]?.ToString() ?? string.Empty,
                    ReceiptDate = reader["Receipt_Date"]?.ToString(),
                    InstallationDate = reader["Installation_Date"]?.ToString(),
                    WarrantyUpto = reader["Warranty_Upto"]?.ToString(),
                    SuppliedFrom = reader["SupplidFrom"]?.ToString() ?? string.Empty,
                    Remarks = reader["remarks"]?.ToString() ?? string.Empty,
                });
            }

            return list;
        }

        private async Task<List<OpeningStockRowDto>> ExecuteOpeningStockReaderAsync(string sql, int userId, int? pid)
        {
            var list = new List<OpeningStockRowDto>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@Pid", pid.HasValue && pid.Value > 0 ? pid.Value : DBNull.Value);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new OpeningStockRowDto
                {
                    ExistingItemId = Convert.ToInt32(reader["Existing_ITEM_ID"]),
                    Pid = reader["Pid"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Pid"]),
                    ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                    ItemCode = reader["item_code"]?.ToString() ?? string.Empty,
                    Make = reader["Make"]?.ToString() ?? string.Empty,
                    ModelNo = reader["Model_No"]?.ToString() ?? string.Empty,
                    MakeSerialNo = reader["Make_Serial_No"]?.ToString() ?? string.Empty,
                    InstallLocation = reader["install_location"]?.ToString() ?? string.Empty,
                    ReceiptDate = reader["Receipt_Date"]?.ToString(),
                    InstallationDate = reader["Installation_Date"]?.ToString(),
                    WarrantyUpto = reader["Warranty_Upto"]?.ToString(),
                    SuppliedFrom = reader["SupplidFrom"]?.ToString() ?? string.Empty,
                    Remarks = reader["remarks"]?.ToString() ?? string.Empty,
                });
            }

            return list;
        }

        [HttpGet("equipment-items")]
        public async Task<IActionResult> GetEquipmentItems([FromQuery] int pid)
        {
            if (pid <= 0)
                return BadRequest(new { message = "pid is required." });

            const string sql = @"
SELECT item_id, item_code_as_per_tender, item_name, pid
FROM dbo.masitems
WHERE pid = @Pid
ORDER BY pid DESC";

            try
            {
                var list = new List<EquipmentItemOptionDto>();
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Pid", pid);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new EquipmentItemOptionDto
                    {
                        ItemId = Convert.ToInt32(reader["item_id"]),
                        ItemCode = reader["item_code_as_per_tender"]?.ToString() ?? string.Empty,
                        ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                        Pid = Convert.ToInt32(reader["pid"]),
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading equipment items.", detail = ex.Message });
            }
        }

        [HttpGet("supply-sources")]
        public async Task<IActionResult> GetSupplySources()
        {
            const string sql = "SELECT SUPID, name FROM dbo.SupplyMaster ORDER BY name";
            try
            {
                var list = new List<SupplySourceOptionDto>();
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new SupplySourceOptionDto
                    {
                        SupId = Convert.ToInt32(reader["SUPID"]),
                        Name = reader["name"]?.ToString() ?? string.Empty,
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading supply sources.", detail = ex.Message });
            }
        }

        [HttpGet("wards")]
        public async Task<IActionResult> GetWards()
        {
            const string sql = "SELECT wID, wname FROM dbo.maswards ORDER BY wname";
            try
            {
                var list = new List<WardOptionDto>();
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new WardOptionDto
                    {
                        WardId = Convert.ToInt32(reader["wID"]),
                        WardName = reader["wname"]?.ToString() ?? string.Empty,
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading wards.", detail = ex.Message });
            }
        }

        [HttpGet("main-equipment-types/{pid:int}/bulk-entry")]
        public async Task<IActionResult> IsBulkEntry(int pid)
        {
            const string sql = "SELECT SRorBulkEntry FROM dbo.masitemP WHERE pid = @Pid";
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Pid", pid);
                object? result = await cmd.ExecuteScalarAsync();
                string flag = result?.ToString() ?? "S";
                return Ok(new { isBulk = flag.Equals("B", StringComparison.OrdinalIgnoreCase) });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error checking bulk entry.", detail = ex.Message });
            }
        }

        [HttpGet("opening-stock/{existingItemId:int}")]
        public async Task<IActionResult> GetOpeningStockById(int existingItemId, [FromQuery] int userId)
        {
            if (userId <= 0 || existingItemId <= 0)
                return BadRequest(new { message = "userId and existingItemId are required." });

            const string sql = @"
SELECT A.existing_item_id, ms.pid, A.item_id, A.SUPID, A.make, A.model, A.make_no,
       CONVERT(VARCHAR(10), A.Receipt_Date, 23) AS Receipt_Date,
       CONVERT(VARCHAR(10), A.installation_date, 23) AS Installation_Date,
       A.warranty_year, CONVERT(VARCHAR(10), A.warranty_upto, 23) AS Warranty_Upto,
       ISNULL(A.wardID, 0) AS wardID, ISNULL(A.install_location, '') AS install_location,
       ISNULL(A.ISAMC, 'N') AS ISAMC,
       CONVERT(VARCHAR(10), A.AMCValidDT, 23) AS AMCValidDT,
       ISNULL(A.AMCFirm, '') AS AMCFirm, ISNULL(A.wstatus, '') AS wstatus,
       ISNULL(A.remarks, '') AS remarks, A.qty,
       ISNULL(mp.SRorBulkEntry, 'S') AS SRorBulkEntry
FROM dbo.existing_item A
INNER JOIN dbo.masitems ms ON A.item_id = ms.item_id
INNER JOIN dbo.masitemP mp ON mp.pid = ms.pid
WHERE A.userid = @UserId AND A.existing_item_id = @ExistingItemId";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@ExistingItemId", existingItemId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound(new { message = "Record not found." });

                return Ok(new OpeningStockDetailDto
                {
                    ExistingItemId = Convert.ToInt32(reader["existing_item_id"]),
                    Pid = Convert.ToInt32(reader["pid"]),
                    ItemId = Convert.ToInt32(reader["item_id"]),
                    SupId = Convert.ToInt32(reader["SUPID"]),
                    Make = reader["make"]?.ToString() ?? string.Empty,
                    Model = reader["model"]?.ToString() ?? string.Empty,
                    SerialNo = reader["make_no"]?.ToString() ?? string.Empty,
                    Qty = reader["qty"] == DBNull.Value ? null : Convert.ToInt32(reader["qty"]),
                    ReceiptDate = reader["Receipt_Date"]?.ToString(),
                    InstallationDate = reader["Installation_Date"]?.ToString(),
                    WarrantyYear = reader["warranty_year"] == DBNull.Value ? null : Convert.ToInt32(reader["warranty_year"]),
                    WarrantyUpto = reader["Warranty_Upto"]?.ToString(),
                    WardId = Convert.ToInt32(reader["wardID"]),
                    InstallLocationOther = reader["install_location"]?.ToString() ?? string.Empty,
                    AmcFlag = reader["ISAMC"]?.ToString() ?? "N",
                    AmcValidDate = reader["AMCValidDT"]?.ToString(),
                    AmcFirm = reader["AMCFirm"]?.ToString() ?? string.Empty,
                    WorkingStatus = reader["wstatus"]?.ToString() ?? string.Empty,
                    Remarks = reader["remarks"]?.ToString() ?? string.Empty,
                    IsBulkEntry = (reader["SRorBulkEntry"]?.ToString() ?? "S").Equals("B", StringComparison.OrdinalIgnoreCase),
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading opening stock detail.", detail = ex.Message });
            }
        }

        [HttpPost("opening-stock")]
        public async Task<IActionResult> SaveOpeningStock([FromBody] OpeningStockSaveDto request)
        {
            return await SaveOrUpdateOpeningStockAsync(request, null);
        }

        [HttpPut("opening-stock/{existingItemId:int}")]
        public async Task<IActionResult> UpdateOpeningStock(int existingItemId, [FromBody] OpeningStockSaveDto request)
        {
            request.ExistingItemId = existingItemId;
            return await SaveOrUpdateOpeningStockAsync(request, existingItemId);
        }

        private async Task<IActionResult> SaveOrUpdateOpeningStockAsync(OpeningStockSaveDto request, int? existingItemId)
        {
            if (request == null || request.UserId <= 0)
                return BadRequest(new { message = "Invalid user." });
            if (request.Pid <= 0 || request.ItemId <= 0 || request.SupId <= 0)
                return BadRequest(new { message = "Please select equipment type, equipment and source." });
            if (string.IsNullOrWhiteSpace(request.Make) || string.IsNullOrWhiteSpace(request.Model) || string.IsNullOrWhiteSpace(request.SerialNo))
                return BadRequest(new { message = "Make, model and serial no are required." });
            if (string.IsNullOrWhiteSpace(request.Remarks))
                return BadRequest(new { message = "Please fill remarks." });
            if (request.WardId <= 0)
                return BadRequest(new { message = "Installation location should not be empty." });
            if (request.WorkingStatus != "W" && request.WorkingStatus != "NW")
                return BadRequest(new { message = "Please select working status of equipment." });
            if (request.AmcFlag == "Y" && (string.IsNullOrWhiteSpace(request.AmcFirm) || string.IsNullOrWhiteSpace(request.AmcValidDate)))
                return BadRequest(new { message = "Please enter AMC firm and AMC validity date." });

            string installLocation = request.WardId == 30 ? request.InstallLocationOther.Trim() : string.Empty;
            if (request.WardId == 30 && string.IsNullOrWhiteSpace(installLocation))
                return BadRequest(new { message = "Please fill installation location for Others." });

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                if (existingItemId.HasValue)
                {
                    const string updateSql = @"
UPDATE dbo.existing_item
SET item_id = @ItemId, make = @Make, model = @Model, make_no = @SerialNo,
    install_location = @InstallLocation, wardID = @WardId, SUPID = @SupId,
    Receipt_Date = @ReceiptDate, installation_date = @InstallationDate,
    warranty_year = @WarrantyYear, warranty_upto = @WarrantyUpto,
    ISAMC = @AmcFlag, AMCFirm = @AmcFirm, AMCValidDT = @AmcValidDate,
    wstatus = @WorkingStatus, remarks = @Remarks, qty = @Qty
WHERE existing_item_id = @ExistingItemId AND userid = @UserId";

                    await using var updateCmd = new SqlCommand(updateSql, conn);
                    AddOpeningStockParameters(updateCmd, request, installLocation);
                    updateCmd.Parameters.AddWithValue("@ExistingItemId", existingItemId.Value);
                    int rows = await updateCmd.ExecuteNonQueryAsync();
                    if (rows == 0)
                        return NotFound(new { message = "Record not found." });
                    return Ok(new { message = "Updated Successfully" });
                }

                const string insertSql = @"
INSERT INTO dbo.existing_item
    (DMEFlag, userid, item_id, make, model, install_location, make_no, supplied, remarks,
     installation_status, wardID, SUPID, installation_date, warranty_upto, warranty_year,
     Receipt_Date, ISAMC, AMCFirm, AMCValidDT, wstatus, qty, entryDt)
VALUES
    ('Y', @UserId, @ItemId, @Make, @Model, @InstallLocation, @SerialNo, 'Other', @Remarks,
     'N', @WardId, @SupId, @InstallationDate, @WarrantyUpto, @WarrantyYear,
     @ReceiptDate, @AmcFlag, @AmcFirm, @AmcValidDate, @WorkingStatus, @Qty, GETDATE())";

                await using var insertCmd = new SqlCommand(insertSql, conn);
                AddOpeningStockParameters(insertCmd, request, installLocation);
                await insertCmd.ExecuteNonQueryAsync();
                return Ok(new { message = "Saved Successfully" });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Save failed.", detail = ex.Message });
            }
        }

        private static void AddOpeningStockParameters(SqlCommand cmd, OpeningStockSaveDto request, string installLocation)
        {
            cmd.Parameters.AddWithValue("@UserId", request.UserId);
            cmd.Parameters.AddWithValue("@ItemId", request.ItemId);
            cmd.Parameters.AddWithValue("@Make", request.Make.Trim());
            cmd.Parameters.AddWithValue("@Model", request.Model.Trim());
            cmd.Parameters.AddWithValue("@SerialNo", request.SerialNo.Trim());
            cmd.Parameters.AddWithValue("@InstallLocation", string.IsNullOrWhiteSpace(installLocation) ? DBNull.Value : installLocation);
            cmd.Parameters.AddWithValue("@WardId", request.WardId);
            cmd.Parameters.AddWithValue("@SupId", request.SupId);
            cmd.Parameters.AddWithValue("@ReceiptDate", ParseDbDate(request.ReceiptDate));
            cmd.Parameters.AddWithValue("@InstallationDate", ParseDbDate(request.InstallationDate));
            cmd.Parameters.AddWithValue("@WarrantyYear", request.WarrantyYear.HasValue ? request.WarrantyYear.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@WarrantyUpto", ParseDbDate(request.WarrantyUpto));
            cmd.Parameters.AddWithValue("@AmcFlag", request.AmcFlag ?? "N");
            cmd.Parameters.AddWithValue("@AmcFirm", string.IsNullOrWhiteSpace(request.AmcFirm) ? DBNull.Value : request.AmcFirm.Trim());
            cmd.Parameters.AddWithValue("@AmcValidDate", ParseDbDate(request.AmcValidDate));
            cmd.Parameters.AddWithValue("@WorkingStatus", request.WorkingStatus);
            cmd.Parameters.AddWithValue("@Remarks", request.Remarks.Trim());
            cmd.Parameters.AddWithValue("@Qty", request.Qty.HasValue ? request.Qty.Value : DBNull.Value);
        }

        private static object ParseDbDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return DBNull.Value;
            if (DateTime.TryParse(value, out DateTime dt))
                return dt.Date;
            return DBNull.Value;
        }

        /// <summary>FACProgress4Cat.aspx — progress month/year options.</summary>
        [HttpGet("progress-month-years")]
        public async Task<IActionResult> GetProgressMonthYears()
        {
            const string sql = @"SELECT ID, MonthYear FROM dbo.ProgressMonthYear
WHERE Year >= YEAR(GETDATE()) AND MONTH >= MONTH(GETDATE())
ORDER BY OrderId";
            var list = new List<ProgressMonthYearOptionDto>();

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new ProgressMonthYearOptionDto
                    {
                        Id = Convert.ToInt32(reader["ID"]),
                        MonthYear = reader["MonthYear"]?.ToString() ?? string.Empty,
                    });
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading progress month/years.", detail = ex.Message });
            }
        }

        /// <summary>FACProgress4Cat.aspx — weekly progress report by equipment category.</summary>
        [HttpGet("progress-category")]
        public async Task<IActionResult> GetProgressCategory(
            [FromQuery] int userId,
            [FromQuery] int pid,
            [FromQuery] int? monthYearId = null)
        {
            if (userId <= 0 || pid <= 0)
                return BadRequest(new { message = "userId and pid are required." });

            int locationId;
            try
            {
                locationId = await ResolveUserLocationIdAsync(userId);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error resolving location.", detail = ex.Message });
            }

            var sql = $@"
SELECT PItemName, item_name,
Make, Model_No, Installation_Date, Warranty_Upto, Make_Serial_No,
supplied, install_location, SupplidFrom, SUPID, Receipt_Date,
ISNULL(b1.Status1 + ' ,Remarks:' + ISNULL(b1.Remark1, 'NA'), 'No Progess Entered') AS Week1,
ISNULL(b2.Status2 + ' ,Remarks:' + ISNULL(b2.Remark2, 'NA'), 'No Progess Entered') AS Week2,
ISNULL(b3.Status3 + ' ,Remarks:' + ISNULL(b3.Remark3, 'NA'), 'No Progess Entered') AS Week3,
ISNULL(b4.Status4 + ' ,Remarks:' + ISNULL(b4.Remark4, 'NA'), 'NA') AS Week4,
Item_code, a.Existing_ITEM_ID, Location, item_id
FROM
(
    SELECT mp.PItemName, m.item_name, m.item_code_as_per_tender AS Item_code,
    ri.item_detail_id AS Existing_ITEM_ID, r.location_id AS Location, m.item_id,
    ri.make AS Make, ri.model_no AS Model_No, ri.installation_date AS Installation_Date,
    ri.warenty_to AS Warranty_Upto, ri.make_no AS Make_Serial_No, 'CGMSC' AS supplied,
    ri.installation_location AS install_location, '' AS remarks, '' AS wstatus,
    'CGMSC' AS SupplidFrom, 0 AS SUPID,
    CONVERT(VARCHAR, r.recieved_date, 105) AS Receipt_Date
    FROM receipt_item_details ri
    INNER JOIN receipts r ON r.receipt_id = ri.receipt_id
    INNER JOIN po_items pi ON pi.po_id = r.po_id AND r.location_id = pi.consignee_id
    INNER JOIN masitems m ON m.item_id = pi.item_id AND m.pid IS NOT NULL
    INNER JOIN masitemP mp ON mp.PID = m.pid
    WHERE 1=1 AND ri.installation_date IS NOT NULL AND r.location_id = {locationId} AND m.pid = {pid}
    UNION ALL
    SELECT mp.PItemName, m.item_Name, m.item_code_as_per_tender AS Item_code,
    A.existing_item_id AS Existing_ITEM_ID, A.location_id AS Location, A.item_id,
    A.make AS Make, A.model AS Model_No, A.installation_date AS Installation_Date,
    A.warranty_upto AS Warranty_Upto, A.make_no AS Make_Serial_No, A.supplied,
    CASE WHEN A.WardID IS NULL THEN A.install_location ELSE mw.WNAME END AS install_location,
    A.remarks, A.wstatus, sm.Name AS SupplidFrom, A.SUPID,
    CONVERT(VARCHAR, A.Receipt_Date, 105) AS Receipt_Date
    FROM existing_item A
    INNER JOIN masitems m ON A.item_id = m.Item_Id
    INNER JOIN masitemP mp ON mp.PID = m.pid
    LEFT OUTER JOIN MasWards mw ON mw.WID = A.wardID
    INNER JOIN SupplyMaster sm ON sm.SupID = A.SUPID
    WHERE A.SUPID IS NOT NULL AND A.installation_date IS NOT NULL
    AND A.location_id = {locationId} AND m.pid = {pid}
) a
LEFT OUTER JOIN
(
    SELECT pid, Existing_ITEM_ID, location_id, da.Remark AS remark3,
    CASE WHEN da.Status IS NULL THEN 'No Progress Entered' ELSE CASE WHEN da.Status='Y' THEN 'Working' ELSE 'Not Working' END END AS status3,
    monthp AS month3, week AS week3
    FROM (
        SELECT MAX(tb.progressid) pid, Existing_ITEM_ID, location_id, monthp, week FROM TblProgress tb
        WHERE 1=1 AND tb.week = 3 AND tb.monthp = 1 AND tb.location_id = {locationId}
        GROUP BY Existing_ITEM_ID, location_id, monthp, week
    ) PMax
    LEFT OUTER JOIN
    (
        SELECT ProgressID, Existing_ITEM_ID AS EItemd, location_id AS lid, Status, Remark FROM TblProgress
        WHERE TblProgress.location_id = {locationId}
    ) da ON da.ProgressID = PMax.pid AND da.EItemd = PMax.Existing_ITEM_ID AND da.lid = PMax.location_id
) b3 ON a.Location = b3.location_id AND a.Existing_ITEM_ID = b3.Existing_ITEM_ID
LEFT OUTER JOIN
(
    SELECT pid, Existing_ITEM_ID, location_id, da.Remark AS remark2,
    CASE WHEN da.Status IS NULL THEN 'No Progress Entered' ELSE CASE WHEN da.Status='Y' THEN 'Working' ELSE 'Not Working' END END AS status2,
    monthp AS month2, week AS week2
    FROM (
        SELECT MAX(tb.progressid) pid, Existing_ITEM_ID, location_id, monthp, week FROM TblProgress tb
        WHERE 1=1 AND tb.week = 2 AND tb.monthp = 1 AND tb.location_id = {locationId}
        GROUP BY Existing_ITEM_ID, location_id, monthp, week
    ) PMax
    LEFT OUTER JOIN
    (
        SELECT ProgressID, Existing_ITEM_ID AS EItemd, location_id AS lid, Status, Remark FROM TblProgress
        WHERE TblProgress.location_id = {locationId}
    ) da ON da.ProgressID = PMax.pid AND da.EItemd = PMax.Existing_ITEM_ID AND da.lid = PMax.location_id
) b2 ON a.Location = b2.location_id AND a.Existing_ITEM_ID = b2.Existing_ITEM_ID
LEFT OUTER JOIN
(
    SELECT pid, Existing_ITEM_ID, location_id, da.Remark AS remark1,
    CASE WHEN da.Status IS NULL THEN 'No Progress Entered' ELSE CASE WHEN da.Status='Y' THEN 'Working' ELSE 'Not Working' END END AS status1,
    monthp AS month1, week AS week1
    FROM (
        SELECT MAX(tb.progressid) pid, Existing_ITEM_ID, location_id, monthp, week FROM TblProgress tb
        WHERE 1=1 AND tb.week = 1 AND tb.monthp = 1 AND tb.location_id = {locationId}
        GROUP BY Existing_ITEM_ID, location_id, monthp, week
    ) PMax
    LEFT OUTER JOIN
    (
        SELECT ProgressID, Existing_ITEM_ID AS EItemd, location_id AS lid, Status, Remark FROM TblProgress
        WHERE TblProgress.location_id = {locationId}
    ) da ON da.ProgressID = PMax.pid AND da.EItemd = PMax.Existing_ITEM_ID AND da.lid = PMax.location_id
) b1 ON a.Location = b1.location_id AND a.Existing_ITEM_ID = b1.Existing_ITEM_ID
LEFT OUTER JOIN
(
    SELECT pid, Existing_ITEM_ID, location_id, da.Remark AS remark4,
    CASE WHEN da.Status IS NULL THEN 'No Progress Entered' ELSE CASE WHEN da.Status='Y' THEN 'Working' ELSE 'Not Working' END END AS status4,
    monthp AS month4, week AS week4
    FROM (
        SELECT MAX(tb.progressid) pid, Existing_ITEM_ID, location_id, monthp, week FROM TblProgress tb
        WHERE 1=1 AND tb.week = 4 AND tb.monthp = 1 AND tb.location_id = {locationId}
        GROUP BY Existing_ITEM_ID, location_id, monthp, week
    ) PMax
    LEFT OUTER JOIN
    (
        SELECT ProgressID, Existing_ITEM_ID AS EItemd, location_id AS lid, Status, Remark FROM TblProgress
        WHERE TblProgress.location_id = {locationId}
    ) da ON da.ProgressID = PMax.pid AND da.EItemd = PMax.Existing_ITEM_ID AND da.lid = PMax.location_id
) b4 ON a.Location = b4.location_id AND a.Existing_ITEM_ID = b4.Existing_ITEM_ID";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                var list = new List<ProgressCategoryRowDto>();
                while (await reader.ReadAsync())
                {
                    list.Add(new ProgressCategoryRowDto
                    {
                        PItemName = reader["PItemName"]?.ToString() ?? string.Empty,
                        ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                        ItemCode = reader["Item_code"]?.ToString() ?? string.Empty,
                        ExistingItemId = reader["Existing_ITEM_ID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Existing_ITEM_ID"]),
                        Location = reader["Location"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Location"]),
                        ItemId = reader["item_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["item_id"]),
                        Make = reader["Make"]?.ToString() ?? string.Empty,
                        ModelNo = reader["Model_No"]?.ToString() ?? string.Empty,
                        InstallationDate = reader["Installation_Date"]?.ToString(),
                        WarrantyUpto = reader["Warranty_Upto"]?.ToString(),
                        MakeSerialNo = reader["Make_Serial_No"]?.ToString() ?? string.Empty,
                        Supplied = reader["supplied"]?.ToString() ?? string.Empty,
                        InstallLocation = reader["install_location"]?.ToString() ?? string.Empty,
                        SuppliedFrom = reader["SupplidFrom"]?.ToString() ?? string.Empty,
                        SupId = reader["SUPID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SUPID"]),
                        ReceiptDate = reader["Receipt_Date"]?.ToString(),
                        Week1 = reader["Week1"]?.ToString() ?? string.Empty,
                        Week2 = reader["Week2"]?.ToString() ?? string.Empty,
                        Week3 = reader["Week3"]?.ToString() ?? string.Empty,
                        Week4 = reader["Week4"]?.ToString() ?? string.Empty,
                    });
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading progress report.", detail = ex.Message });
            }
        }

        /// <summary>Facilityequipmentreceipt.aspx — item dropdown options (parent items).</summary>
        [HttpGet("facility-receipt-items")]
        public async Task<IActionResult> GetFacilityReceiptItems()
        {
            const string sql = @"SELECT A.ITEM_CODE_AS_PER_TENDER AS itemCode, A.item_name AS itemName FROM dbo.MASITEMS A WHERE A.PARENT_ITEM_ID IS NOT NULL ORDER BY A.ITEM_NAME";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                var list = new List<Dictionary<string, object>>();
                while (await reader.ReadAsync())
                {
                    list.Add(new Dictionary<string, object>
                    {
                        ["itemCode"] = reader["itemCode"]?.ToString() ?? string.Empty,
                        ["itemName"] = reader["itemName"]?.ToString() ?? string.Empty,
                    });
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading receipt items.", detail = ex.Message });
            }
        }

        /// <summary>Facilityequipmentreceipt.aspx — financial year options.</summary>
        [HttpGet("financial-years")]
        public async Task<IActionResult> GetFinancialYears()
        {
            const string sql = @"SELECT financial_year_id, year FROM dbo.mas_financial_year ORDER BY financial_year_id";
            var list = new List<KeyValuePair<int, string>>();

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new KeyValuePair<int, string>(
                        Convert.ToInt32(reader["financial_year_id"]),
                        reader["year"]?.ToString() ?? string.Empty));
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading financial years.", detail = ex.Message });
            }
        }

        /// <summary>Facilityequipmentreceipt.aspx — received equipment items (untagged).</summary>
        [HttpGet("facility-receipts")]
        public async Task<IActionResult> GetFacilityReceipts(
            [FromQuery] int locationId,
            [FromQuery] int? financialYearId = null,
            [FromQuery] string? itemCode = null)
        {
            if (locationId <= 0)
                return BadRequest(new { message = "locationId is required." });

            var whereYear = financialYearId.HasValue && financialYearId.Value > 0
                ? $" AND p.financial_year_id = '{financialYearId.Value}'" : string.Empty;
            var whereItem = !string.IsNullOrWhiteSpace(itemCode) && itemCode != "0"
                ? $" AND m.item_code_as_per_tender = '{itemCode.Replace("'", "''")}'" : string.Empty;

            var sql = $@"
SELECT p.po_id, p.po_no, CONVERT(VARCHAR, p.po_date, 103) AS po_date, item_detail_id,
m.item_id, m.item_code_as_per_tender, m.item_name, model_no, make_no,
CONVERT(VARCHAR, installation_date, 103) AS installation_date, r.installation_location,
CONVERT(VARCHAR, warenty_from, 103) AS warenty_from, CONVERT(VARCHAR, warenty_to, 103) AS warenty_to,
pi.consignee_id, ml.location_name, ms.name,
pi.quantity, ri.receipt_qty, r.received_qty
FROM receipt_item_details r
INNER JOIN receipts ri ON ri.receipt_id = r.receipt_id
INNER JOIN po_items pi ON pi.po_id = ri.po_id AND pi.consignee_id = ri.location_id
INNER JOIN purchase_order p ON p.po_id = pi.po_id
INNER JOIN masitems m ON m.item_id = pi.item_id
INNER JOIN maslocations ml ON ml.location_id = pi.consignee_id
INNER JOIN massuppliers ms ON ms.supplier_id = p.supplier_id
WHERE r.status IN ('I', 'C') AND ri.status = 'C' AND r.istagged IS NULL
AND m.categoryId != 2 AND ri.location_id = {locationId}
{whereYear}{whereItem}
ORDER BY installation_date";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                var list = new List<FacilityReceiptRowDto>();
                while (await reader.ReadAsync())
                {
                    list.Add(new FacilityReceiptRowDto
                    {
                        PoId = reader["po_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["po_id"]),
                        PoNo = reader["po_no"]?.ToString() ?? string.Empty,
                        PoDate = reader["po_date"]?.ToString(),
                        ItemDetailId = reader["item_detail_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["item_detail_id"]),
                        ItemId = reader["item_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["item_id"]),
                        ItemCode = reader["item_code_as_per_tender"]?.ToString() ?? string.Empty,
                        ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                        ModelNo = reader["model_no"]?.ToString() ?? string.Empty,
                        MakeNo = reader["make_no"]?.ToString() ?? string.Empty,
                        InstallationDate = reader["installation_date"]?.ToString(),
                        InstallLocation = reader["installation_location"]?.ToString() ?? string.Empty,
                        WarrantyFrom = reader["warenty_from"]?.ToString(),
                        WarrantyTo = reader["warenty_to"]?.ToString(),
                        ConsigneeId = reader["consignee_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["consignee_id"]),
                        LocationName = reader["location_name"]?.ToString() ?? string.Empty,
                        SupplierName = reader["name"]?.ToString() ?? string.Empty,
                        Quantity = ReadDecimal(reader["quantity"]),
                        ReceiptQty = ReadDecimal(reader["receipt_qty"]),
                        ReceivedQty = ReadDecimal(reader["received_qty"]),
                    });
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading facility receipts.", detail = ex.Message });
            }
        }

        /// <summary>Facilityequipmentreceipt.aspx — supply/dispatch batches for a PO+location.</summary>
        [HttpGet("facility-receipt-batches")]
        public async Task<IActionResult> GetFacilityReceiptBatches([FromQuery] int poId, [FromQuery] int locationId)
        {
            if (poId <= 0 || locationId <= 0)
                return BadRequest(new { message = "poId and locationId are required." });

            var sql = $@"
SELECT d.Issue_id, CONVERT(VARCHAR, Tentative_Sdate, 103) AS Tentative_Sdate,
CASE WHEN re.receipt_no IS NOT NULL THEN re.receipt_no ELSE '' END AS receipt_no,
CASE WHEN re.status IS NOT NULL THEN CASE WHEN re.status='C' THEN 'Installation Completed' ELSE 'Installation Pending' END
ELSE CASE WHEN d.status='C' THEN 'Receipt' ELSE 'Not Supplied' END END AS SupplyStatus,
CONVERT(VARCHAR, dispatch_date, 103) AS dispatch_date, dispatch_no,
SUM(i.Supplyqty) quantity, d.po_id, d.location_id,
CASE WHEN re.recieved_date IS NULL THEN 'Not Receipt' ELSE re.recieved_date END AS recieved_date,
re.receipt_id
FROM SupplierDispatch d
INNER JOIN Issue_item_details i ON d.Issue_id = i.Issue_id
LEFT OUTER JOIN
(
    SELECT r.issue_id, CONVERT(VARCHAR, r.recieved_date, 103) AS recieved_date, r.po_id, r.location_id, r.status, r.receipt_no, r.receipt_id
    FROM receipts r WHERE r.status = 'C'
) re ON re.issue_id = d.Issue_id AND re.po_id = d.po_id AND re.location_id = d.location_id
WHERE d.po_id = {poId} AND d.location_id = {locationId}
GROUP BY re.receipt_id, d.Issue_id, dispatch_date, Tentative_Sdate, d.status, dispatch_no, d.po_id, d.location_id, re.recieved_date, re.status, re.receipt_no";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                var list = new List<FacilityReceiptBatchDto>();
                while (await reader.ReadAsync())
                {
                    list.Add(new FacilityReceiptBatchDto
                    {
                        IssueId = reader["Issue_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Issue_id"]),
                        TentativeStartDate = reader["Tentative_Sdate"]?.ToString(),
                        ReceiptNo = reader["receipt_no"]?.ToString() ?? string.Empty,
                        SupplyStatus = reader["SupplyStatus"]?.ToString() ?? string.Empty,
                        DispatchDate = reader["dispatch_date"]?.ToString(),
                        DispatchNo = reader["dispatch_no"]?.ToString() ?? string.Empty,
                        Quantity = ReadDecimal(reader["quantity"]),
                        PoId = reader["po_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["po_id"]),
                        LocationId = reader["location_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["location_id"]),
                        ReceivedDate = reader["recieved_date"]?.ToString() ?? string.Empty,
                        ReceiptId = reader["receipt_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["receipt_id"]),
                    });
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading facility receipt batches.", detail = ex.Message });
            }
        }

        /// <summary>Facilityequipmentreceipt.aspx — tag an item as installed.</summary>
        [HttpPost("facility-receipts/tag")]
        public async Task<IActionResult> TagFacilityReceipt([FromBody] FacilityReceiptTagRequest request)
        {
            if (request.ItemDetailId <= 0)
                return BadRequest(new { message = "itemDetailId is required." });

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                const string sql = @"UPDATE receipt_item_details SET istagged = @Tagged, updateDT = GETDATE() WHERE item_detail_id = @ItemDetailId";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Tagged", request.Tagged == "Y" ? "Y" : "N");
                cmd.Parameters.AddWithValue("@ItemDetailId", request.ItemDetailId);
                var affected = await cmd.ExecuteNonQueryAsync();
                return Ok(new { message = "Update Successfully", affected = affected });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error updating tagging status.", detail = ex.Message });
            }
        }

        /// <summary>NodleInformationNew.aspx / NodleInformation.ascx — nodal officer grid for logged-in user's district.</summary>
        [HttpGet("nodal-information")]
        public async Task<IActionResult> GetNodalInformation([FromQuery] int userId)
        {
            if (userId <= 0)
                return BadRequest(new { message = "userId is required." });

            const string sql = @"
SELECT m.location_id, location_name, ft.facility_type_name, d.DBStart_Name_En, m.DP_DistrictID,
ft.facility_type_id, u.USER_ID, nd.id, nd.name, nd.designation, nd.Mobileno, nd.emailid
FROM maslocations m
INNER JOIN facility_type ft ON ft.facility_type_id = m.facility_type_id
INNER JOIN Districts d ON d.DP_DistrictID = m.DP_DistrictID
INNER JOIN users u ON u.location_id = m.location_id
LEFT OUTER JOIN
(
    SELECT id, USER_ID, name, designation, Mobileno, emailid FROM NodleMaster WHERE Isactive = 'Y'
) nd ON nd.user_id = u.user_id
WHERE m.authority = 5 AND ft.facility_type_id NOT IN (13)
AND m.DP_DistrictID IN (
    SELECT l.DP_DistrictID FROM users uu
    INNER JOIN maslocations l ON l.location_id = uu.location_id
    WHERE uu.user_id = @UserId
)
ORDER BY m.DP_DistrictID, ft.facility_type_id";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                await using var reader = await cmd.ExecuteReaderAsync();
                var list = new List<NodalInfoRowDto>();
                while (await reader.ReadAsync())
                {
                    list.Add(new NodalInfoRowDto
                    {
                        LocationId = reader["location_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["location_id"]),
                        LocationName = reader["location_name"]?.ToString() ?? string.Empty,
                        FacilityTypeName = reader["facility_type_name"]?.ToString() ?? string.Empty,
                        DistrictName = reader["DBStart_Name_En"]?.ToString() ?? string.Empty,
                        DistrictId = reader["DP_DistrictID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["DP_DistrictID"]),
                        FacilityTypeId = reader["facility_type_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["facility_type_id"]),
                        UserId = reader["USER_ID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["USER_ID"]),
                        Id = reader["id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["id"]),
                        Name = reader["name"]?.ToString() ?? string.Empty,
                        Designation = reader["designation"]?.ToString() ?? string.Empty,
                        MobileNo = reader["Mobileno"]?.ToString() ?? string.Empty,
                        EmailId = reader["emailid"]?.ToString() ?? string.Empty,
                    });
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading nodal officer information.", detail = ex.Message });
            }
        }

        /// <summary>NodleInformation.ascx — add nodal officer row.</summary>
        [HttpPost("nodal-information")]
        public async Task<IActionResult> SaveNodalInformation([FromBody] NodalInfoSaveRequest request)
        {
            if (request.UserId <= 0)
                return BadRequest(new { message = "userId is required." });
            if (string.IsNullOrWhiteSpace(request.Name) ||
                string.IsNullOrWhiteSpace(request.Designation) ||
                string.IsNullOrWhiteSpace(request.EmailId) ||
                string.IsNullOrWhiteSpace(request.MobileNo))
                return BadRequest(new { message = "Fill Name, Designation, Emailid, Mobile NO." });

            if (!IsValidMobileNo(request.MobileNo.Trim()))
                return BadRequest(new { message = "Not a valid Mobile No, Must be Start with 5,6,7,8 or 9" });

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                const string sql = @"INSERT INTO NodleMaster(user_id, Name, Designation, Emailid, MobileNO, EntryDate, Isactive)
VALUES (@UserId, @Name, @Designation, @EmailId, @MobileNo, GETDATE(), 'Y')";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", request.UserId);
                cmd.Parameters.AddWithValue("@Name", request.Name.Trim());
                cmd.Parameters.AddWithValue("@Designation", request.Designation.Trim());
                cmd.Parameters.AddWithValue("@EmailId", request.EmailId.Trim());
                cmd.Parameters.AddWithValue("@MobileNo", request.MobileNo.Trim());
                await cmd.ExecuteNonQueryAsync();
                return Ok(new { message = "Save Successfully" });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error saving nodal officer information.", detail = ex.Message });
            }
        }

        /// <summary>NodleInformation.ascx — soft-delete nodal officer row.</summary>
        [HttpDelete("nodal-information/{id:int}")]
        public async Task<IActionResult> DeleteNodalInformation(int id)
        {
            if (id <= 0)
                return BadRequest(new { message = "id is required." });

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                const string sql = @"UPDATE NodleMaster SET Isactive = 'N' WHERE id = @Id";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Id", id);
                await cmd.ExecuteNonQueryAsync();
                return Ok(new { message = "Deleted Successfully" });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error deleting nodal officer information.", detail = ex.Message });
            }
        }

        private static bool IsValidMobileNo(string mobno)
        {
            if (string.IsNullOrWhiteSpace(mobno) || mobno.Length != 10)
                return false;
            char first = mobno[0];
            if (first is not ('5' or '6' or '7' or '8' or '9'))
                return false;
            foreach (char c in mobno)
            {
                if (!char.IsDigit(c))
                    return false;
            }
            return true;
        }

        /// <summary>ProgressDetail.aspx / ProgressDetailDME.aspx — pending equipment grid for weekly progress entry.</summary>
        [HttpGet("nodal-progress")]
        public async Task<IActionResult> GetNodalProgress(
            [FromQuery] int userId,
            [FromQuery] int pid,
            [FromQuery] bool isDme = false)
        {
            if (userId <= 0 || pid <= 0)
                return BadRequest(new { message = "userId and pid are required." });

            int locationId = 0;
            if (!isDme)
            {
                locationId = await ResolveUserLocationIdAsync(userId);
                if (locationId <= 0)
                    return BadRequest(new { message = "Could not resolve user location." });
            }

            var sql = isDme
                ? $@"
SELECT item_name, Item_code, Existing_ITEM_ID, Location, item_id, Make, Model_No, Installation_Date,
Warranty_Upto, Make_Serial_No, supplied, install_location, remarks, wstatus, SupplidFrom, SUPID, Receipt_Date
FROM
(
    SELECT m.item_name, m.item_code_as_per_tender AS Item_code, ri.item_detail_id AS Existing_ITEM_ID,
    r.location_id AS Location, m.item_id, ri.make AS Make, ri.model_no AS Model_No,
    ri.installation_date AS Installation_Date, ri.warenty_to AS Warranty_Upto, ri.make_no AS Make_Serial_No,
    'CGMSC' AS supplied, ri.installation_location AS install_location, '' AS remarks, '' AS wstatus,
    'CGMSC' AS SupplidFrom, 0 AS SUPID, CONVERT(VARCHAR, r.recieved_date, 105) AS Receipt_Date
    FROM receipt_item_details ri
    INNER JOIN receipts r ON r.receipt_id = ri.receipt_id
    INNER JOIN maslocations l ON l.location_id = r.location_id
    INNER JOIN users u ON u.user_id = l.user_id
    INNER JOIN po_items pi ON pi.po_id = r.po_id AND r.location_id = pi.consignee_id
    INNER JOIN masitems m ON m.item_id = pi.item_id AND m.pid IS NOT NULL
    WHERE 1=1 AND ri.installation_date IS NOT NULL AND u.user_id = {userId} AND m.pid = {pid}
    UNION ALL
    SELECT ms.item_Name, ms.Item_code, A.existing_item_id AS Existing_ITEM_ID, A.location_id AS Location,
    A.item_id, A.make AS Make, A.model AS Model_No, A.installation_date AS Installation_Date,
    A.warranty_upto AS Warranty_Upto, A.make_no AS Make_Serial_No, A.supplied,
    CASE WHEN A.WardID IS NULL THEN A.install_location ELSE mw.WNAME END AS install_location,
    A.remarks, A.wstatus, sm.Name AS SupplidFrom, A.SUPID, CONVERT(VARCHAR, A.Receipt_Date, 105) AS Receipt_Date
    FROM existing_item A
    INNER JOIN masitems ms ON A.item_id = ms.Item_Id
    LEFT OUTER JOIN MasWards mw ON mw.WID = A.wardID
    INNER JOIN SupplyMaster sm ON sm.SupID = A.SUPID
    WHERE A.SUPID IS NOT NULL AND A.installation_date IS NOT NULL AND A.userid = {userId} AND ms.pid = {pid}
) x WHERE Existing_ITEM_ID NOT IN (
    SELECT existing_item_id FROM TblProgress WHERE dmeuserid = {userId} AND progressDT = CAST(GETDATE() AS DATE)
)"
                : $@"
SELECT item_name, Item_code, Existing_ITEM_ID, Location, item_id, Make, Model_No, Installation_Date,
Warranty_Upto, Make_Serial_No, supplied, install_location, remarks, wstatus, SupplidFrom, SUPID, Receipt_Date
FROM
(
    SELECT m.item_name, m.item_code_as_per_tender AS Item_code, ri.item_detail_id AS Existing_ITEM_ID,
    r.location_id AS Location, m.item_id, ri.make AS Make, ri.model_no AS Model_No,
    ri.installation_date AS Installation_Date, ri.warenty_to AS Warranty_Upto, ri.make_no AS Make_Serial_No,
    'CGMSC' AS supplied, ri.installation_location AS install_location, '' AS remarks, '' AS wstatus,
    'CGMSC' AS SupplidFrom, 0 AS SUPID, CONVERT(VARCHAR, r.recieved_date, 105) AS Receipt_Date
    FROM receipt_item_details ri
    INNER JOIN receipts r ON r.receipt_id = ri.receipt_id
    INNER JOIN po_items pi ON pi.po_id = r.po_id AND r.location_id = pi.consignee_id
    INNER JOIN masitems m ON m.item_id = pi.item_id AND m.pid IS NOT NULL
    WHERE 1=1 AND ri.installation_date IS NOT NULL AND r.location_id = {locationId} AND m.pid = {pid}
    UNION ALL
    SELECT ms.item_Name, ms.Item_code, A.existing_item_id AS Existing_ITEM_ID, A.location_id AS Location,
    A.item_id, A.make AS Make, A.model AS Model_No, A.installation_date AS Installation_Date,
    A.warranty_upto AS Warranty_Upto, A.make_no AS Make_Serial_No, A.supplied,
    CASE WHEN A.WardID IS NULL THEN A.install_location ELSE mw.WNAME END AS install_location,
    A.remarks, A.wstatus, sm.Name AS SupplidFrom, A.SUPID, CONVERT(VARCHAR, A.Receipt_Date, 105) AS Receipt_Date
    FROM existing_item A
    INNER JOIN masitems ms ON A.item_id = ms.Item_Id
    LEFT OUTER JOIN MasWards mw ON mw.WID = A.wardID
    INNER JOIN SupplyMaster sm ON sm.SupID = A.SUPID
    WHERE A.SUPID IS NOT NULL AND A.installation_date IS NOT NULL AND A.location_id = {locationId} AND ms.pid = {pid}
) x WHERE Existing_ITEM_ID NOT IN (
    SELECT existing_item_id FROM TblProgress WHERE location_id = {locationId} AND progressDT = CAST(GETDATE() AS DATE)
)";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                var list = new List<NodalProgressRowDto>();
                while (await reader.ReadAsync())
                {
                    list.Add(new NodalProgressRowDto
                    {
                        ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                        ItemCode = reader["Item_code"]?.ToString() ?? string.Empty,
                        ExistingItemId = reader["Existing_ITEM_ID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Existing_ITEM_ID"]),
                        Location = reader["Location"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Location"]),
                        ItemId = reader["item_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["item_id"]),
                        Make = reader["Make"]?.ToString() ?? string.Empty,
                        ModelNo = reader["Model_No"]?.ToString() ?? string.Empty,
                        InstallationDate = reader["Installation_Date"]?.ToString(),
                        WarrantyUpto = reader["Warranty_Upto"]?.ToString(),
                        MakeSerialNo = reader["Make_Serial_No"]?.ToString() ?? string.Empty,
                        Supplied = reader["supplied"]?.ToString() ?? string.Empty,
                        InstallLocation = reader["install_location"]?.ToString() ?? string.Empty,
                        Remarks = reader["remarks"]?.ToString() ?? string.Empty,
                        WStatus = reader["wstatus"]?.ToString() ?? string.Empty,
                        SuppliedFrom = reader["SupplidFrom"]?.ToString() ?? string.Empty,
                        SupId = reader["SUPID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SUPID"]),
                        ReceiptDate = reader["Receipt_Date"]?.ToString(),
                    });
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading nodal progress grid.", detail = ex.Message });
            }
        }

        /// <summary>ProgressDetail.aspx / ProgressDetailDME.aspx — save weekly progress rows.</summary>
        [HttpPost("nodal-progress")]
        public async Task<IActionResult> SaveNodalProgress([FromBody] NodalProgressSaveRequest request)
        {
            if (request.UserId <= 0)
                return BadRequest(new { message = "userId is required." });
            if (request.Items == null || request.Items.Count == 0)
                return BadRequest(new { message = "Select at least one equipment row." });

            int locationId = 0;
            if (!request.IsDme)
            {
                locationId = await ResolveUserLocationIdAsync(request.UserId);
                if (locationId <= 0)
                    return BadRequest(new { message = "Could not resolve user location." });
            }

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                string sql = request.IsDme
                    ? @"INSERT INTO TblProgress(Existing_ITEM_ID, ProgressDT, Status, Remark, EntryDate, dmeuserid, week, monthP, EntryDTTime)
VALUES(@ExistingItemId, GETDATE(), @Status, @Remark, GETDATE(), @UserId, @Week, @Month, GETDATE())"
                    : @"INSERT INTO TblProgress(Existing_ITEM_ID, ProgressDT, Status, Remark, EntryDate, location_id, week, monthP, EntryDTTime)
VALUES(@ExistingItemId, GETDATE(), @Status, @Remark, GETDATE(), @LocationId, @Week, @Month, GETDATE())";

                foreach (var item in request.Items)
                {
                    await using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@ExistingItemId", item.ExistingItemId);
                    cmd.Parameters.AddWithValue("@Status", item.Status == "Y" ? "Y" : "N");
                    cmd.Parameters.AddWithValue("@Remark", string.IsNullOrWhiteSpace(item.Remark) ? DBNull.Value : item.Remark.Trim());
                    cmd.Parameters.AddWithValue("@UserId", request.UserId);
                    cmd.Parameters.AddWithValue("@LocationId", locationId);
                    cmd.Parameters.AddWithValue("@Week", request.Week);
                    cmd.Parameters.AddWithValue("@Month", request.Month);
                    await cmd.ExecuteNonQueryAsync();
                }
                return Ok(new { message = "Record Saved Successfully" });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error saving progress.", detail = ex.Message });
            }
        }

        /// <summary>ProgressDetail.aspx / ProgressDetailDME.aspx — send OTP to nodal officer mobile.</summary>
        [HttpPost("nodal-progress/send-otp")]
        public async Task<IActionResult> SendNodalProgressOtp([FromBody] NodalOtpRequest request)
        {
            if (request.UserId <= 0)
                return BadRequest(new { message = "userId is required." });

            string mobileNo;
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                const string sql = @"SELECT MobileNO FROM NodleMaster WHERE Isactive = 'Y' AND user_id = @UserId";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", request.UserId);
                var result = await cmd.ExecuteScalarAsync();
                mobileNo = result?.ToString() ?? string.Empty;
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading nodal officer mobile.", detail = ex.Message });
            }

            if (string.IsNullOrWhiteSpace(mobileNo))
                return BadRequest(new { message = "No mobile number registered for this nodal officer." });

            var otp = new Random().Next(1000, 9999).ToString();

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                const string sql = @"UPDATE users SET pwdChangeOTP = @Otp WHERE user_id = @UserId";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Otp", otp);
                cmd.Parameters.AddWithValue("@UserId", request.UserId);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error storing OTP.", detail = ex.Message });
            }

            try
            {
                var message = $"Your OTP for COVID Equipment Progress entry is {otp}. - CGMSC";
                using var sms = new System.Net.Http.HttpClient();
                // Best-effort SMS dispatch (log failure only).
                await sms.GetStringAsync($"https://api.msg91.com/api/sendhttp.php?mobiles={mobileNo}&message={Uri.EscapeDataString(message)}&sender=CGMSCL&route=4&country=91");
            }
            catch
            {
                // SMS dispatch is best-effort; progress entry still allowed via OTP stored in DB.
            }

            return Ok(new NodalOtpResponse { Success = true, Message = "OTP sent successfully." });
        }

        private async Task<int> ResolveUserLocationIdAsync(int userId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            const string sql = @"SELECT l.location_id FROM users u INNER JOIN maslocations l ON l.location_id = u.location_id WHERE u.user_id = @UserId";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        private static decimal ReadDecimal(object? value)
        {
            return value == null || value == DBNull.Value ? 0m : Convert.ToDecimal(value);
        }
    }
}
