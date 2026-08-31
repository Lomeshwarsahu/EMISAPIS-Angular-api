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

        /// <summary>NodleInformation.ascx / NodleInformationNew.aspx — Nodal Officer entry list.</summary>
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        [HttpGet("nodal-information")]
        public async Task<IActionResult> GetNodalInformation([FromQuery] int userId)
        {
            var list = new List<NodalInformationDto>();
            string sql = @"
SELECT m.location_id,
       m.location_name,
       ft.facility_type_name,
       d.DBStart_Name_En as district_name,
       m.DP_DistrictID as district_id,
       ft.facility_type_id,
       u.USER_ID as user_id,
       isnull(nd.id, 0) as id,
       isnull(nd.name, '') as name,
       isnull(nd.designation, '') as designation,
       isnull(nd.Mobileno, '') as mobile_no,
       isnull(nd.emailid, '') as email_id
FROM maslocations m
INNER JOIN facility_type ft on ft.facility_type_id = m.facility_type_id
INNER JOIN Districts d on d.DP_DistrictID = m.DP_DistrictID
INNER JOIN users u on u.location_id = m.location_id
LEFT OUTER JOIN (
    SELECT id, USER_ID, name, designation, Mobileno, emailid
    FROM NodleMaster
    WHERE Isactive = 'Y'
) nd on nd.user_id = u.user_id
WHERE m.authority = 5 AND ft.facility_type_id NOT IN (13)
  AND (
      @UserId <= 0
      OR m.DP_DistrictID IN (
          SELECT l.DP_DistrictID
          FROM users u2
          INNER JOIN maslocations l on l.location_id = u2.location_id
          WHERE u2.user_id = @UserId
      )
  )
ORDER BY m.DP_DistrictID, ft.facility_type_id";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new NodalInformationDto
                    {
                        LocationId = reader["location_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["location_id"]),
                        LocationName = reader["location_name"]?.ToString() ?? string.Empty,
                        FacilityTypeName = reader["facility_type_name"]?.ToString() ?? string.Empty,
                        DistrictName = reader["district_name"]?.ToString() ?? string.Empty,
                        DistrictId = reader["district_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["district_id"]),
                        FacilityTypeId = reader["facility_type_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["facility_type_id"]),
                        UserId = reader["user_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["user_id"]),
                        Id = reader["id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["id"]),
                        Name = reader["name"]?.ToString() ?? string.Empty,
                        Designation = reader["designation"]?.ToString() ?? string.Empty,
                        MobileNo = reader["mobile_no"]?.ToString() ?? string.Empty,
                        EmailId = reader["email_id"]?.ToString() ?? string.Empty,
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading nodal information.", detail = ex.Message });
            }
        }

        /// <summary>NodleInformation.ascx — Save new nodal officer entry.</summary>
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        [HttpPost("nodal-information")]
        public async Task<IActionResult> SaveNodalInformation([FromBody] NodalInformationSaveDto dto)
        {
            if (dto.UserId <= 0)
                return BadRequest(new { message = "UserId is required." });

            if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Designation) ||
                string.IsNullOrWhiteSpace(dto.EmailId) || string.IsNullOrWhiteSpace(dto.MobileNo))
            {
                return BadRequest(new { message = "Fill Name, Designation, Email, and Mobile No." });
            }

            string sql = @"
INSERT INTO NodleMaster(user_id, Name, Designation, Emailid, MobileNO, EntryDate, Isactive)
VALUES(@UserId, @Name, @Designation, @EmailId, @MobileNo, GETDATE(), 'Y')";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", dto.UserId);
                cmd.Parameters.AddWithValue("@Name", dto.Name.Trim());
                cmd.Parameters.AddWithValue("@Designation", dto.Designation.Trim());
                cmd.Parameters.AddWithValue("@EmailId", dto.EmailId.Trim());
                cmd.Parameters.AddWithValue("@MobileNo", dto.MobileNo.Trim());

                await cmd.ExecuteNonQueryAsync();
                return Ok(new { message = "Saved Successfully" });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error saving nodal officer.", detail = ex.Message });
            }
        }

        /// <summary>NodleInformation.ascx — Deactivate/delete nodal officer entry.</summary>
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        [HttpDelete("nodal-information/{id:int}")]
        public async Task<IActionResult> DeleteNodalInformation(int id)
        {
            if (id <= 0)
                return BadRequest(new { message = "Invalid ID." });

            string sql = "UPDATE NodleMaster SET Isactive = 'N' WHERE id = @Id";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Id", id);

                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                if (rowsAffected > 0)
                    return Ok(new { message = "Deleted Successfully" });

                return NotFound(new { message = "Record not found." });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error deleting nodal officer.", detail = ex.Message });
            }
        }
    }
}
