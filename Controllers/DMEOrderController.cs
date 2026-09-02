using EMISAPIS.DTOS;
using EMISAPIS.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Linq;

namespace EMISAPIS.Controllers
{
    /// <summary>EMSRole/Order/PODashboardDMEFAC.aspx.</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DMEOrderController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly string _emsRoleRoot;
        private readonly string _indentUploadsRoot;
        private readonly MongoService _mongoService;
        private readonly OtpSmsService _otpSms;

        public DMEOrderController(IConfiguration configuration, IWebHostEnvironment env, OtpSmsService otpSms)
        {
            _otpSms = otpSms;
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection missing.");

            _emsRoleRoot = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "EMSRole"));
            var configured = configuration["FileStorage:IndentUploadsPath"];
            _indentUploadsRoot = string.IsNullOrWhiteSpace(configured)
                ? Path.GetFullPath(Path.Combine(_emsRoleRoot, "Uploads"))
                : Path.GetFullPath(configured);
            _mongoService = new MongoService();
        }

        [HttpGet("financial-years")]
        public async Task<IActionResult> GetFinancialYears()
        {
            const string sql = @"
SELECT financial_year_id, year
FROM dbo.mas_financial_year
ORDER BY financial_year_id DESC";

            var list = new List<FinancialYearOptionDto> { new() { FinancialYearId = 0, Year = "Select Fin Year" } };

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new FinancialYearOptionDto
                    {
                        FinancialYearId = Convert.ToInt32(reader["financial_year_id"]),
                        Year = reader["year"]?.ToString() ?? string.Empty,
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading financial years.", detail = ex.Message });
            }
        }

        [HttpGet("po-equipment-options")]
        public async Task<IActionResult> GetPoEquipmentOptions()
        {
            const string sql = @"
SELECT DISTINCT m.item_id, m.item_name + '-' + m.item_code_as_per_tender AS item_name, m.item_code_as_per_tender
FROM dbo.po_items pi
INNER JOIN dbo.masitems m ON m.item_id = pi.item_id
UNION
SELECT DISTINCT m.item_id, m.item_name + '-' + m.item_code_as_per_tender AS item_name, m.item_code_as_per_tender
FROM dbo.indent_cons_items c
INNER JOIN dbo.masitems m ON m.item_id = c.item_id
ORDER BY item_name";

            var list = new List<PoEquipmentOptionDto> { new() { ItemId = 0, ItemName = "--All--", ItemCodeAsPerTender = "0" } };

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new PoEquipmentOptionDto
                    {
                        ItemId = Convert.ToInt32(reader["item_id"]),
                        ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                        ItemCodeAsPerTender = reader["item_code_as_per_tender"]?.ToString() ?? string.Empty,
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading equipment list.", detail = ex.Message });
            }
        }

        [HttpGet("po-dashboard")]
        public async Task<IActionResult> GetPoDashboard(
            [FromQuery] int userId,
            [FromQuery] int financialYearId = 0,
            [FromQuery] string? itemCode = null)
        {
            if (userId <= 0)
                return BadRequest(new { message = "userId is required." });

            // financialYearId=0 and empty/0 itemCode = All
            var sql = @"
SELECT A.PO_ID,
       CONVERT(VARCHAR, a.po_date, 103) AS po_date,
       a.PO_NO,
       b.NAME AS SUPPLIER_NAME,
       c.TENDER_NO,
       CONVERT(VARCHAR, c.TENDER_DATE, 103) AS TENDER_DATE,
       ISNULL(R.item_name, a.item_id) AS ITEM_NAME,
       R.ITEM_CODE_AS_PER_TENDER AS CODE,
       E.YEAR AS POYear,
       iy.year AS indentYear,
       CONVERT(VARCHAR, id.consolidated_date, 103) AS IndentDT,
       ISNULL(im.indent_quantity, 0) AS indent_quantity,
       ISNULL(pi.quantity, 0) AS POQTY,
       ltp.filePathAccessories,
       ltp.filePathReagent,
       ltp.tender_item_id
FROM dbo.PURCHASE_ORDER a
LEFT OUTER JOIN dbo.po_items pi ON pi.po_id = a.po_id
LEFT OUTER JOIN dbo.maslocations l ON l.location_id = pi.consignee_id
LEFT OUTER JOIN dbo.MASSUPPLIERS b ON a.SUPPLIER_ID = b.SUPPLIER_ID
LEFT OUTER JOIN dbo.MASITEMS R ON R.ITEM_ID = ISNULL(pi.item_id, a.ITEM_ID)
LEFT OUTER JOIN dbo.TENDERS c ON c.TENDER_ID = a.TENDER_ID
LEFT OUTER JOIN dbo.indent_consolidation id ON id.indent_consolidation_id = pi.INDENT_CONSOLIDATION_ID
LEFT OUTER JOIN dbo.indent i ON i.indent_id = pi.indent_id
LEFT OUTER JOIN dbo.indent_items im ON im.indent_item_id = pi.indent_item_id AND im.indent_id = pi.indent_id
LEFT OUTER JOIN dbo.indent_cons_items ci ON ci.indent_cons_items_id = i.indent_cons_items_id AND ci.indent_consolidated_id = id.indent_consolidation_id
LEFT OUTER JOIN dbo.tender_items ti ON ti.tender_id = c.tender_id AND ti.item_id = ISNULL(pi.item_id, a.ITEM_ID)
LEFT OUTER JOIN dbo.live_tender_price ltp ON ltp.supplier_id = a.supplier_id AND ltp.tender_item_id = ti.tender_item_id
LEFT OUTER JOIN dbo.MAS_FINANCIAL_YEAR E ON E.FINANCIAL_YEAR_ID = a.FINANCIAL_YEAR_ID
LEFT OUTER JOIN dbo.mas_financial_year iy ON iy.financial_year_id = id.financial_year_id
WHERE a.status NOT IN ('Incomplete', 'Waiting For Approval', 'Cancelled')
  AND (
       @UserId = 0
       OR l.user_id = @UserId 
       OR l.location_id IN (SELECT location_id FROM dbo.users WHERE user_id = @UserId) 
       OR l.location_id = @UserId
       OR l.district_id IN (SELECT district_id FROM dbo.users WHERE user_id = @UserId AND district_id IS NOT NULL AND district_id > 0)
       OR pi.consignee_id IN (SELECT location_id FROM dbo.users WHERE user_id = @UserId)
       OR EXISTS (SELECT 1 FROM dbo.users u WHERE u.user_id = @UserId AND u.Role IN ('AD', 'ADMIN', 'AU', 'AUPO', 'DME', 'DIRECTOR', 'TPO', 'GM', 'MD'))
      )
  AND (@FinancialYearId = 0 OR a.FINANCIAL_YEAR_ID = @FinancialYearId)
  AND (@ItemCode IS NULL OR @ItemCode = '' OR @ItemCode = '0' OR R.item_code_as_per_tender = @ItemCode)
ORDER BY a.po_date DESC";

            var list = new List<PoDashboardRowDto>();

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId);
                cmd.Parameters.AddWithValue("@ItemCode", string.IsNullOrWhiteSpace(itemCode) ? DBNull.Value : itemCode.Trim());

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new PoDashboardRowDto
                    {
                        PoId = Convert.ToInt32(reader["PO_ID"]),
                        ItemName = reader["ITEM_NAME"]?.ToString() ?? string.Empty,
                        Code = reader["CODE"]?.ToString() ?? string.Empty,
                        IndentDt = reader["IndentDT"]?.ToString() ?? string.Empty,
                        IndentYear = reader["indentYear"]?.ToString() ?? string.Empty,
                        IndentQuantity = reader["indent_quantity"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["indent_quantity"]),
                        PoNo = reader["PO_NO"]?.ToString() ?? string.Empty,
                        PoDate = reader["po_date"]?.ToString() ?? string.Empty,
                        PoYear = reader["POYear"]?.ToString() ?? string.Empty,
                        PoQty = reader["POQTY"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["POQTY"]),
                        TenderNo = reader["TENDER_NO"]?.ToString() ?? string.Empty,
                        TenderDate = reader["TENDER_DATE"]?.ToString() ?? string.Empty,
                        SupplierName = reader["SUPPLIER_NAME"]?.ToString() ?? string.Empty,
                        FilePathReagent = reader["filePathReagent"]?.ToString(),
                        FilePathAccessories = reader["filePathAccessories"]?.ToString(),
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading purchase orders.", detail = ex.Message });
            }
        }

        /// <summary>PODashboardDMEFAC — download reagent / accessories list PDF from live_tender_price.</summary>
        [HttpGet("po-attachment")]
        public async Task<IActionResult> DownloadPoAttachment(
            [FromQuery] int userId,
            [FromQuery] int poId,
            [FromQuery] string fileType)
        {
            if (userId <= 0 || poId <= 0)
                return BadRequest(new { message = "userId and poId are required." });

            var kind = (fileType ?? string.Empty).Trim().ToLowerInvariant();
            if (kind is not ("reagent" or "accessories"))
                return BadRequest(new { message = "fileType must be reagent or accessories." });

            const string sql = @"
SELECT TOP 1
       ltp.filePathReagent,
       ltp.filePathAccessories
FROM dbo.PURCHASE_ORDER a
INNER JOIN dbo.po_items pi ON pi.po_id = a.po_id
INNER JOIN dbo.maslocations l ON l.location_id = pi.consignee_id
INNER JOIN dbo.tender_items ti ON ti.tender_id = a.tender_id AND ti.item_id = pi.item_id
INNER JOIN dbo.live_tender_price ltp ON ltp.supplier_id = a.supplier_id AND ltp.tender_item_id = ti.tender_item_id
WHERE a.po_id = @PoId AND (l.user_id = @UserId OR l.location_id IN (SELECT location_id FROM dbo.users WHERE user_id = @UserId) OR l.location_id = @UserId)";

            try
            {
                await using var con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                if (!await PoBelongsToFacilityUserAsync(con, poId, userId))
                    return NotFound(new { message = "Purchase order not found for this facility user." });

                await using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@PoId", poId);
                cmd.Parameters.AddWithValue("@UserId", userId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound(new { message = "Attachment not found for this purchase order." });

                var dbPath = kind == "reagent"
                    ? reader["filePathReagent"]?.ToString()?.Trim() ?? string.Empty
                    : reader["filePathAccessories"]?.ToString()?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(dbPath))
                    return NotFound(new { message = "File not uploaded." });

                var physicalPath = ResolveIndentPhysicalPath(dbPath, Path.GetFileName(dbPath));
                if (physicalPath == null || !System.IO.File.Exists(physicalPath))
                    return NotFound(new { message = "File not found on disk." });

                var downloadName = Path.GetFileName(physicalPath);
                if (string.IsNullOrWhiteSpace(downloadName))
                    downloadName = $"{kind}_{poId}.pdf";

                var contentType = downloadName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                    ? "application/pdf"
                    : "application/octet-stream";

                return PhysicalFile(physicalPath, contentType, downloadName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error downloading attachment.", detail = ex.Message });
            }
        }

        /// <summary>PODashboardDMEFAC Print → rdlcPoReport.aspx — printable purchase order for facility user.</summary>
        [HttpGet("po-report/print")]
        public async Task<IActionResult> GetFacilityPoPrintReport(
            [FromQuery] int userId,
            [FromQuery] int poId)
        {
            if (userId <= 0)
                return BadRequest(new { message = "userId is required." });
            if (poId <= 0)
                return BadRequest(new { message = "PO id is required." });

            try
            {
                await using var con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                if (!await PoBelongsToFacilityUserAsync(con, poId, userId))
                    return NotFound(new { message = "Purchase order not found for this facility user." });

                var report = await PoPrintReportLoader.LoadAsync(con, poId);
                if (report == null)
                    return NotFound(new { message = "Purchase order report not found." });

                return Ok(report);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading PO print report.", detail = ex.Message });
            }
        }

        [HttpGet("po-receipt-items")]
        public async Task<IActionResult> GetPoReceiptItems()
        {
            const string sql = @"
SELECT A.ITEM_CODE_AS_PER_TENDER, A.item_name
FROM dbo.MASITEMS A
WHERE A.PARENT_ITEM_ID IS NOT NULL
ORDER BY A.item_name";

            var list = new List<PoEquipmentOptionDto> { new() { ItemId = 0, ItemName = "--All--", ItemCodeAsPerTender = "0" } };

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new PoEquipmentOptionDto
                    {
                        ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                        ItemCodeAsPerTender = reader["ITEM_CODE_AS_PER_TENDER"]?.ToString() ?? string.Empty,
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading items.", detail = ex.Message });
            }
        }

        /// <summary>Facilitypo_supply_editDME.aspx — PO receipt desk list.</summary>
        [HttpGet("po-receipt-desk")]
        public async Task<IActionResult> GetPoReceiptDesk(
            [FromQuery] int userId,
            [FromQuery] string? authorityId = null,
            [FromQuery] int financialYearId = 0,
            [FromQuery] string? itemCode = null)
        {
            if (userId <= 0)
                return BadRequest(new { message = "userId is required." });

            var sql = @"
SELECT a.po_item_id, a.po_id, a.quantity, a.consignee_id,
       c1.location_name, CONVERT(VARCHAR, b.po_date, 103) AS po_date, b.PO_NO,
       ISNULL(c.item_name, a.item_id) AS item_name, c.item_code_as_per_tender AS item_code, s.name AS supplier_name,
       ISNULL(x.single_unit_price * a.quantity, 0) AS Total_Price
FROM dbo.po_items a
LEFT OUTER JOIN dbo.MASITEMS R ON R.ITEM_ID = a.item_id
LEFT OUTER JOIN dbo.purchase_order b ON a.po_id = b.po_id
LEFT OUTER JOIN dbo.MASSUPPLIERS s ON s.SUPPLIER_ID = b.supplier_id
LEFT OUTER JOIN (
    SELECT F.SUPPLIER_ID, F.TENDER_ID, D.ITEM_ID, D.single_unit_price
    FROM dbo.AWARD_OF_CONTRACT F
    INNER JOIN dbo.CONTRACT_ITEMS D ON D.AWARD_OF_CONTRACT_ID = F.AWARD_OF_CONTRACT_ID
) x ON x.TENDER_ID = b.TENDER_ID AND b.SUPPLIER_ID = x.SUPPLIER_ID AND a.ITEM_ID = x.ITEM_ID
LEFT OUTER JOIN dbo.maslocations c1 ON c1.location_id = a.consignee_id
LEFT OUTER JOIN dbo.masitems c ON a.item_id = c.item_id
WHERE (b.status IS NULL OR b.status NOT IN ('Incomplete', 'Waiting For Approval', 'Cancelled'))
  AND (
       @UserId = 0
       OR c1.user_id = @UserId 
       OR c1.location_id IN (SELECT location_id FROM dbo.users WHERE user_id = @UserId) 
       OR c1.location_id = @UserId
       OR c1.district_id IN (SELECT district_id FROM dbo.users WHERE user_id = @UserId AND district_id IS NOT NULL AND district_id > 0)
       OR a.consignee_id IN (SELECT location_id FROM dbo.users WHERE user_id = @UserId)
       OR EXISTS (SELECT 1 FROM dbo.users u WHERE u.user_id = @UserId AND u.Role IN ('AD', 'ADMIN', 'AU', 'AUPO', 'DME', 'DIRECTOR', 'TPO', 'GM', 'MD'))
      )
  AND (@AuthorityId IS NULL OR @AuthorityId = '' OR c1.authority = @AuthorityId)
  AND (@FinancialYearId = 0 OR b.financial_year_id = @FinancialYearId)
  AND (@ItemCode IS NULL OR @ItemCode = '' OR @ItemCode = '0' OR R.item_code_as_per_tender = @ItemCode)
ORDER BY b.po_date DESC, a.po_id DESC";

            try
            {
                var rows = new List<PoReceiptDeskRowDto>();
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@AuthorityId", string.IsNullOrWhiteSpace(authorityId) ? DBNull.Value : authorityId.Trim());
                cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId);
                cmd.Parameters.AddWithValue("@ItemCode", string.IsNullOrWhiteSpace(itemCode) ? DBNull.Value : itemCode.Trim());

                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        rows.Add(new PoReceiptDeskRowDto
                        {
                            PoItemId = Convert.ToInt32(reader["po_item_id"]),
                            PoId = Convert.ToInt32(reader["po_id"]),
                            ConsigneeId = Convert.ToInt32(reader["consignee_id"]),
                            LocationName = reader["location_name"]?.ToString() ?? string.Empty,
                            PoNo = reader["PO_NO"]?.ToString() ?? string.Empty,
                            PoDate = reader["po_date"]?.ToString() ?? string.Empty,
                            ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                            ItemCode = reader["item_code"]?.ToString() ?? string.Empty,
                            SupplierName = reader["supplier_name"]?.ToString() ?? string.Empty,
                            Quantity = reader["quantity"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["quantity"]),
                            TotalPrice = reader["Total_Price"] == DBNull.Value ? null : Convert.ToDecimal(reader["Total_Price"]),
                        });
                    }
                }

                foreach (var row in rows)
                {
                    row.Batches = await LoadReceiptBatchesAsync(conn, row.PoId, row.ConsigneeId);
                }

                return Ok(rows);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading PO receipt desk.", detail = ex.Message });
            }
        }

        // --- DMEFACHeads / ConsolidatedIndentDME_MC (also on api/DMEIndent after API restart) ---

        [HttpGet("budget-heads")]
        public async Task<IActionResult> GetBudgetHeads([FromQuery] int userId)
        {
            if (userId <= 0)
                return BadRequest(new { message = "userId is required." });

            const string sql = @"
SELECT headID, headno, headName
FROM dbo.DMEFACHead
WHERE user_id = @UserId
ORDER BY headName";

            var list = new List<DmeFacHeadDto>();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new DmeFacHeadDto
                    {
                        HeadId = Convert.ToInt32(reader["headID"]),
                        HeadNo = reader["headno"]?.ToString() ?? string.Empty,
                        HeadName = reader["headName"]?.ToString() ?? string.Empty,
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading budget heads.", detail = ex.Message });
            }
        }

        [HttpPost("budget-heads")]
        public async Task<IActionResult> CreateBudgetHead([FromBody] CreateDmeFacHeadRequest req)
        {
            if (req.UserId <= 0)
                return BadRequest(new { message = "userId is required." });
            if (string.IsNullOrWhiteSpace(req.HeadNo) || string.IsNullOrWhiteSpace(req.HeadName))
                return BadRequest(new { message = "Head No and Head Name are required." });

            const string sql = @"
INSERT INTO dbo.DMEFACHead (headno, headName, user_id)
VALUES (@HeadNo, @HeadName, @UserId)";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@HeadNo", req.HeadNo.Trim());
                cmd.Parameters.AddWithValue("@HeadName", req.HeadName.Trim());
                cmd.Parameters.AddWithValue("@UserId", req.UserId);
                await cmd.ExecuteNonQueryAsync();
                return Ok(new { message = "Budget Head Saved Successfully." });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error saving budget head.", detail = ex.Message });
            }
        }

        [HttpGet("facility-indents")]
        public async Task<IActionResult> GetFacilityIndents(
            [FromQuery] int userId,
            [FromQuery] int financialYearId = 0)
        {
            if (userId <= 0)
                return BadRequest(new { message = "userId is required." });

            var list = new List<FacilityIndentRowDto>();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                var hasAsColumns = await TableHasColumnsAsync(conn, "mas_indentfacility", "ASLetterNo", "ASDate");
                var asSelect = hasAsColumns
                    ? @"ISNULL(a.ASLetterNo, '') AS ASLetterNo,
       CONVERT(VARCHAR, a.ASDate, 103) AS ASDate,"
                    : @"'' AS ASLetterNo,
       '' AS ASDate,";
                var asGroup = hasAsColumns ? ", a.ASLetterNo, a.ASDate" : "";

                var sql = $@"
SELECT a.indentid, u.user_name, a.USER_ID,
       CONVERT(VARCHAR(10), a.indentdate, 103) AS CONSOLIDATED_DATE,
       SUM(B.dirappqty) AS FINAL_QTY,
       COUNT(DISTINCT b.itemid) AS nosindentQTY,
       CASE WHEN a.STATUS = 'I' THEN 'Incomplete' WHEN a.STATUS = 'C' THEN 'Completed' ELSE '' END AS EStatus,
       CASE WHEN a.path IS NULL THEN 'Not Uploaded' ELSE 'Uploaded' END AS uploadStatus,
       a.financial_year_id,
       {asSelect}
       ISNULL(a.DispatchNo, '') AS DispatchNo,
       CONVERT(VARCHAR, a.DispatchDT, 103) AS dispatchdate
FROM dbo.mas_indentfacility a
LEFT OUTER JOIN dbo.mas_item_indent b ON b.indentid = a.indentid
INNER JOIN dbo.users u ON u.user_id = a.location_id
WHERE a.directorate_id = 12 AND u.user_id = @UserId
  AND (@FinancialYearId = 0 OR a.financial_year_id = @FinancialYearId)
GROUP BY a.indentid, a.USER_ID, u.user_name, a.FINANCIAL_YEAR_ID, a.STATUS, a.indentdate,
         a.path, a.DispatchNo, a.DispatchDT{asGroup}
ORDER BY a.indentdate DESC";

                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new FacilityIndentRowDto
                    {
                        IndentId = Convert.ToInt32(reader["indentid"]),
                        McName = reader["user_name"]?.ToString() ?? string.Empty,
                        UserId = Convert.ToInt32(reader["USER_ID"]),
                        ConsolidatedDate = reader["CONSOLIDATED_DATE"]?.ToString() ?? string.Empty,
                        AsLetterNo = reader["ASLetterNo"]?.ToString() ?? string.Empty,
                        AsDate = reader["ASDate"]?.ToString() ?? string.Empty,
                        DispatchNo = reader["DispatchNo"]?.ToString() ?? string.Empty,
                        DispatchDate = reader["dispatchdate"]?.ToString() ?? string.Empty,
                        NosIndentQty = reader["nosindentQTY"] == DBNull.Value ? 0 : Convert.ToInt32(reader["nosindentQTY"]),
                        EStatus = reader["EStatus"]?.ToString() ?? string.Empty,
                        UploadStatus = reader["uploadStatus"]?.ToString() ?? string.Empty,
                        FinancialYearId = reader["financial_year_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["financial_year_id"]),
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading facility indents.", detail = ex.Message });
            }
        }

        [HttpPost("facility-indents")]
        public async Task<IActionResult> CreateFacilityIndent([FromBody] CreateFacilityIndentRequest req)
        {
            if (req.UserId <= 0)
                return BadRequest(new { message = "userId is required." });
            if (req.BudgetId <= 0 || req.FinancialYearId <= 0)
                return BadRequest(new { message = "Budget and Financial Year are required." });
            if (string.IsNullOrWhiteSpace(req.IndentDate) || string.IsNullOrWhiteSpace(req.AsDate))
                return BadRequest(new { message = "Indent Date and AS Date are required." });

            if (!TryParseLegacyDate(req.IndentDate, out var indentDt))
                return BadRequest(new { message = "Invalid Indent Date. Use dd/MM/yyyy." });
            if (!TryParseLegacyDate(req.AsDate, out var asDt))
                return BadRequest(new { message = "Invalid AS Date. Use dd/MM/yyyy." });

            if (indentDt.Date > DateTime.Today)
                return BadRequest(new { message = "Indent Date cannot be greater than Today." });

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var hasAsColumns = await TableHasColumnsAsync(conn, "mas_indentfacility", "ASLetterNo", "ASDate");
                var sql = hasAsColumns
                    ? @"INSERT INTO dbo.mas_indentfacility
    (BUDGETID, indentdate, location_id, directorate_id, financial_year_id, status, entrydate, user_id, ASLetterNo, ASDate)
VALUES
    (@BudgetId, @IndentDate, @UserId, 12, @FinancialYearId, 'I', GETDATE(), @UserId, @AsLetterNo, @AsDate)"
                    : @"INSERT INTO dbo.mas_indentfacility
    (BUDGETID, indentdate, location_id, directorate_id, financial_year_id, status, entrydate, user_id)
VALUES
    (@BudgetId, @IndentDate, @UserId, 12, @FinancialYearId, 'I', GETDATE(), @UserId)";

                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@BudgetId", req.BudgetId);
                cmd.Parameters.AddWithValue("@IndentDate", indentDt);
                cmd.Parameters.AddWithValue("@UserId", req.UserId);
                cmd.Parameters.AddWithValue("@FinancialYearId", req.FinancialYearId);
                if (hasAsColumns)
                {
                    cmd.Parameters.AddWithValue("@AsLetterNo", req.AsLetterNo.Trim());
                    cmd.Parameters.AddWithValue("@AsDate", asDt);
                }

                await cmd.ExecuteNonQueryAsync();
                return Ok(new { message = "Indent created successfully." });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error creating indent.", detail = ex.Message });
            }
        }

        /// <summary>DMEFACADDIndent.aspx — indent header by ICID.</summary>
        [HttpGet("facility-indents/{indentId:int}")]
        public async Task<IActionResult> GetFacilityIndentHeader(int indentId, [FromQuery] int userId)
        {
            if (indentId <= 0 || userId <= 0)
                return BadRequest(new { message = "indentId and userId are required." });

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                var hasAsColumns = await TableHasColumnsAsync(conn, "mas_indentfacility", "ASLetterNo", "ASDate");
                var asSelect = hasAsColumns
                    ? @"ISNULL(i.ASLetterNo, '') AS ASLetterNo,
       CONVERT(VARCHAR, i.ASDate, 103) AS ASDate"
                    : @"'' AS ASLetterNo,
       '' AS ASDate";

                var sql = $@"
SELECT i.indentid, i.user_id, u.user_name, i.financial_year_id, f.year,
       CONVERT(VARCHAR, i.indentdate, 103) AS indentdate,
       ISNULL(i.status, 'N') AS status,
       CASE WHEN i.BUDGETID IS NOT NULL THEN h.headName ELSE '' END AS BudgetName,
       ISNULL(i.DispatchNo, '') AS DispatchNo,
       {asSelect}
FROM dbo.mas_indentfacility i
LEFT OUTER JOIN dbo.DMEFACHead h ON h.headid = i.BUDGETID
INNER JOIN dbo.mas_financial_year f ON f.financial_year_id = i.financial_year_id
INNER JOIN dbo.users u ON u.user_id = i.user_id
WHERE i.indentid = @IndentId AND i.user_id = @UserId AND i.directorate_id = 12";

                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@IndentId", indentId);
                cmd.Parameters.AddWithValue("@UserId", userId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound(new { message = "Indent not found." });

                var status = reader["status"]?.ToString() ?? "N";
                var completed = string.Equals(status, "C", StringComparison.OrdinalIgnoreCase);
                return Ok(new FacilityIndentHeaderDto
                {
                    IndentId = Convert.ToInt32(reader["indentid"]),
                    UserId = Convert.ToInt32(reader["user_id"]),
                    McName = reader["user_name"]?.ToString() ?? string.Empty,
                    BudgetName = reader["BudgetName"]?.ToString() ?? string.Empty,
                    IndentDate = reader["indentdate"]?.ToString() ?? string.Empty,
                    FinancialYearId = reader["financial_year_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["financial_year_id"]),
                    Year = reader["year"]?.ToString() ?? string.Empty,
                    Status = status,
                    StatusLabel = completed ? "Completed" : "Incomplete",
                    AsLetterNo = reader["ASLetterNo"]?.ToString() ?? string.Empty,
                    AsDate = reader["ASDate"]?.ToString() ?? string.Empty,
                    DispatchNo = reader["DispatchNo"]?.ToString() ?? string.Empty,
                    IsCompleted = completed,
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading indent header.", detail = ex.Message });
            }
        }

        /// <summary>DMEFACADDIndent.aspx — saved cart lines.</summary>
        [HttpGet("facility-indents/{indentId:int}/items")]
        public async Task<IActionResult> GetFacilityIndentItems(int indentId, [FromQuery] int userId)
        {
            if (indentId <= 0 || userId <= 0)
                return BadRequest(new { message = "indentId and userId are required." });

            const string sql = @"
SELECT l.location_id, l.location_name,
       ISNULL(dbo.Getstock(l.location_id, m.item_id), 0) AS CurrentStock,
       ISNULL(dbo.GetPipeline(l.location_id, m.item_id), 0) AS Pipeline,
       m.item_id, m.item_name AS itemname,
       mii.facility_ind_qty AS indent_quantity,
       ROUND(mii.approxrate, 0) AS estimated_cost,
       CAST(mii.approxrate * mii.facility_ind_qty AS bigint) AS Value,
       m.item_code_as_per_tender, ISNULL(mii.remarks, '') AS remarks,
       CASE WHEN aa.contract_end_date IS NOT NULL THEN aa.contract_end_date ELSE 'RC not Valid' END AS RCstatus,
       mii.indentitemid
FROM dbo.mas_indentfacility mi
INNER JOIN dbo.mas_item_indent mii ON mii.indentid = mi.indentid
INNER JOIN dbo.masitems m ON m.item_id = mii.itemid
INNER JOIN dbo.maslocations l ON l.location_id = mii.location_id
LEFT OUTER JOIN (
    SELECT m2.item_id,
           CASE WHEN c.contract_new_end_date IS NOT NULL
                THEN CONVERT(VARCHAR, c.contract_new_end_date, 103)
                ELSE CONVERT(VARCHAR, ac.contract_end_date, 103) END AS contract_end_date
    FROM dbo.contract_items c
    INNER JOIN dbo.masitems m2 ON m2.item_id = c.item_id
    INNER JOIN dbo.award_of_contract ac ON ac.award_of_contract_id = c.award_of_contract_id
    WHERE GETDATE() BETWEEN ac.contract_date AND ac.contract_end_date
      AND (m2.categoryid = 1 OR m2.categoryid IS NULL)
) aa ON aa.item_id = mii.itemid
WHERE mi.indentid = @IndentId AND mi.user_id = @UserId AND mi.directorate_id = 12
ORDER BY m.item_name, l.location_name";

            var list = new List<FacilityIndentItemRowDto>();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@IndentId", indentId);
                cmd.Parameters.AddWithValue("@UserId", userId);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new FacilityIndentItemRowDto
                    {
                        IndentItemId = Convert.ToInt32(reader["indentitemid"]),
                        LocationId = Convert.ToInt32(reader["location_id"]),
                        LocationName = reader["location_name"]?.ToString() ?? string.Empty,
                        ItemId = Convert.ToInt32(reader["item_id"]),
                        ItemCode = reader["item_code_as_per_tender"]?.ToString() ?? string.Empty,
                        ItemName = reader["itemname"]?.ToString() ?? string.Empty,
                        EstimatedCost = reader["estimated_cost"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["estimated_cost"]),
                        IndentQuantity = reader["indent_quantity"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["indent_quantity"]),
                        Value = reader["Value"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["Value"]),
                        CurrentStock = reader["CurrentStock"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["CurrentStock"]),
                        Pipeline = reader["Pipeline"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["Pipeline"]),
                        Remarks = reader["remarks"]?.ToString() ?? string.Empty,
                        RcStatus = reader["RCstatus"]?.ToString() ?? string.Empty,
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading indent items.", detail = ex.Message });
            }
        }

        /// <summary>DMEFACADDIndent.aspx — EEL equipment dropdown.</summary>
        [HttpGet("facility-indent-equipment")]
        public async Task<IActionResult> GetFacilityIndentEquipment([FromQuery] int itemId = 0)
        {
            var list = new List<FacilityIndentEquipmentDto>();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Legacy uses CME_EEL; some EMIS DBs only have iseel.
                var hasCmeEel = await TableHasColumnsAsync(conn, "masitems", "CME_EEL");
                var hasIseel = await TableHasColumnsAsync(conn, "masitems", "iseel");
                var eelFilter = hasCmeEel
                    ? "ISNULL(m.CME_EEL, 'N') = 'Y'"
                    : hasIseel
                        ? "ISNULL(m.iseel, 'N') = 'Y'"
                        : "1 = 1";

                var priceEelFilter = hasIseel
                    ? "AND ISNULL(m2.iseel, 'N') = 'Y'"
                    : hasCmeEel
                        ? "AND ISNULL(m2.CME_EEL, 'N') = 'Y'"
                        : "";

                var sql = $@"
SELECT m.item_id, m.item_code_as_per_tender AS item_code, m.item_name AS item_name,
       RTRIM(LTRIM(m.item_name)) + '(' + RTRIM(LTRIM(ISNULL(m.item_code_as_per_tender, ''))) + ')' AS item_nameE,
       ISNULL((
           SELECT TOP 1 c.single_unit_price
           FROM dbo.contract_items c
           INNER JOIN dbo.award_of_contract ac ON ac.award_of_contract_id = c.award_of_contract_id
           INNER JOIN dbo.masitems m2 ON m2.item_id = c.item_id
           WHERE c.item_id = m.item_id
             AND GETDATE() BETWEEN ac.contract_date AND ac.contract_end_date
             {priceEelFilter}
           ORDER BY ac.contract_end_date DESC
       ), 0) AS approxrate
FROM dbo.masitems m
WHERE {eelFilter}
  AND (@ItemId = 0 OR m.item_id = @ItemId)
ORDER BY m.item_name";

                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ItemId", itemId);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new FacilityIndentEquipmentDto
                    {
                        ItemId = Convert.ToInt32(reader["item_id"]),
                        ItemCode = reader["item_code"]?.ToString() ?? string.Empty,
                        ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                        ItemNameDisplay = reader["item_nameE"]?.ToString() ?? string.Empty,
                        ApproxRate = reader["approxrate"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["approxrate"]),
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading equipment list.", detail = ex.Message });
            }
        }

        /// <summary>DMEFACADDIndent.aspx — department rows for selected equipment.</summary>
        [HttpGet("facility-indents/{indentId:int}/departments")]
        public async Task<IActionResult> GetFacilityIndentDepartments(
            int indentId,
            [FromQuery] int userId,
            [FromQuery] int itemId)
        {
            if (indentId <= 0 || userId <= 0 || itemId <= 0)
                return BadRequest(new { message = "indentId, userId and itemId are required." });

            const string priceSql = @"
SELECT TOP 1 c.single_unit_price
FROM dbo.contract_items c
INNER JOIN dbo.award_of_contract ac ON ac.award_of_contract_id = c.award_of_contract_id
INNER JOIN dbo.masitems m ON m.item_id = c.item_id
WHERE c.item_id = @ItemId
  AND GETDATE() BETWEEN ac.contract_date AND ac.contract_end_date
  AND ISNULL(m.iseel, 'N') = 'Y'
ORDER BY ac.contract_end_date DESC";

            const string sql = @"
SELECT l.location_name AS LOCATION_NAME, l.location_id AS LOCATION_ID,
       ISNULL(st.stkQTY, 0) AS CurrentStock,
       ISNULL(pip.pipelineQTY, 0) AS Pipeline,
       ISNULL(b.facility_ind_qty, 0) AS INDENT_QUANTITY
FROM dbo.maslocations l
LEFT OUTER JOIN (
    SELECT pi.consignee_id, SUM(r.receipt_qty) AS stkQTY
    FROM dbo.po_items pi
    INNER JOIN dbo.receipts r ON r.po_id = pi.po_id AND r.location_id = pi.consignee_id
    INNER JOIN dbo.purchase_order p ON p.po_id = pi.po_id
    WHERE p.directorate_id = 12 AND r.status = 'C' AND pi.item_id = @ItemId
    GROUP BY pi.consignee_id
) st ON st.consignee_id = l.location_id
LEFT OUTER JOIN (
    SELECT pi.consignee_id, SUM(pi.quantity) - ISNULL(pip.rqty, 0) AS pipelineQTY
    FROM dbo.po_items pi
    LEFT OUTER JOIN (
        SELECT r.location_id, SUM(r.receipt_qty) AS rqty
        FROM dbo.receipts r
        INNER JOIN dbo.purchase_order p ON p.po_id = r.po_id
        INNER JOIN dbo.po_items pi2 ON pi2.po_id = p.po_id AND pi2.consignee_id = r.location_id
        WHERE p.directorate_id = 12 AND r.status = 'C' AND pi2.item_id = @ItemId AND pi2.directorate_id = 12
        GROUP BY r.location_id
    ) pip ON pip.location_id = pi.consignee_id
    WHERE pi.directorate_id = 12 AND pi.item_id = @ItemId
    GROUP BY pi.consignee_id, pip.rqty
) pip ON pip.consignee_id = l.location_id
INNER JOIN dbo.mas_indentfacility mi ON mi.user_id = l.user_id
LEFT OUTER JOIN dbo.mas_item_indent b
  ON b.location_id = l.location_id AND b.itemid = @ItemId AND b.indentid = @IndentId
WHERE mi.indentid = @IndentId
  AND mi.user_id = @UserId
  AND l.Isactive IS NULL
  AND l.user_id = @UserId
ORDER BY l.location_name";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                decimal approxRate = 0;
                await using (var priceCmd = new SqlCommand(priceSql, conn))
                {
                    priceCmd.Parameters.AddWithValue("@ItemId", itemId);
                    var priceObj = await priceCmd.ExecuteScalarAsync();
                    if (priceObj != null && priceObj != DBNull.Value)
                        approxRate = Convert.ToDecimal(priceObj);
                }

                var list = new List<FacilityIndentDeptRowDto>();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@IndentId", indentId);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@ItemId", itemId);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new FacilityIndentDeptRowDto
                    {
                        LocationId = Convert.ToInt32(reader["LOCATION_ID"]),
                        LocationName = reader["LOCATION_NAME"]?.ToString() ?? string.Empty,
                        CurrentStock = reader["CurrentStock"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["CurrentStock"]),
                        Pipeline = reader["Pipeline"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["Pipeline"]),
                        ExistingIndentQty = reader["INDENT_QUANTITY"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["INDENT_QUANTITY"]),
                        ApproxRate = approxRate,
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading departments.", detail = ex.Message });
            }
        }

        /// <summary>DMEFACADDIndent.aspx — add indent line.</summary>
        [HttpPost("facility-indents/{indentId:int}/items")]
        public async Task<IActionResult> AddFacilityIndentItem(int indentId, [FromBody] AddFacilityIndentItemRequest req)
        {
            if (indentId <= 0 || req.UserId <= 0 || req.ItemId <= 0 || req.LocationId <= 0)
                return BadRequest(new { message = "indentId, userId, itemId and locationId are required." });
            if (req.FacilityIndQty <= 0)
                return BadRequest(new { message = "Indent qty should be greater than zero." });
            if (req.ApproxRate <= 0)
                return BadRequest(new { message = "Price should not be empty." });

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string headerSql = @"
SELECT ISNULL(status, 'N') AS status
FROM dbo.mas_indentfacility
WHERE indentid = @IndentId AND user_id = @UserId AND directorate_id = 12";
                await using (var headerCmd = new SqlCommand(headerSql, conn))
                {
                    headerCmd.Parameters.AddWithValue("@IndentId", indentId);
                    headerCmd.Parameters.AddWithValue("@UserId", req.UserId);
                    var statusObj = await headerCmd.ExecuteScalarAsync();
                    if (statusObj == null || statusObj == DBNull.Value)
                        return NotFound(new { message = "Indent not found." });
                    if (string.Equals(Convert.ToString(statusObj), "C", StringComparison.OrdinalIgnoreCase))
                        return BadRequest(new { message = "Indent is completed. New items cannot be added." });
                }

                const string dupSql = @"
SELECT COUNT(1) FROM dbo.mas_item_indent
WHERE indentid = @IndentId AND itemid = @ItemId AND location_id = @LocationId";
                await using (var dupCmd = new SqlCommand(dupSql, conn))
                {
                    dupCmd.Parameters.AddWithValue("@IndentId", indentId);
                    dupCmd.Parameters.AddWithValue("@ItemId", req.ItemId);
                    dupCmd.Parameters.AddWithValue("@LocationId", req.LocationId);
                    var count = Convert.ToInt32(await dupCmd.ExecuteScalarAsync());
                    if (count > 0)
                        return BadRequest(new { message = "Indent already placed for this item for same department." });
                }

                var approxIndVal = req.ApproxRate * req.FacilityIndQty;
                const string insertSql = @"
INSERT INTO dbo.mas_item_indent
    (location_id, remarks, status, indentid, facility_ind_qty, facilityentrydt, itemid, approxrate, approxindval)
VALUES
    (@LocationId, @Remarks, 'I', @IndentId, @Qty, GETDATE(), @ItemId, @ApproxRate, @ApproxIndVal)";
                await using var insertCmd = new SqlCommand(insertSql, conn);
                insertCmd.Parameters.AddWithValue("@LocationId", req.LocationId);
                insertCmd.Parameters.AddWithValue("@Remarks", (req.Remarks ?? string.Empty).Trim());
                insertCmd.Parameters.AddWithValue("@IndentId", indentId);
                insertCmd.Parameters.AddWithValue("@Qty", req.FacilityIndQty);
                insertCmd.Parameters.AddWithValue("@ItemId", req.ItemId);
                insertCmd.Parameters.AddWithValue("@ApproxRate", req.ApproxRate);
                insertCmd.Parameters.AddWithValue("@ApproxIndVal", approxIndVal);
                await insertCmd.ExecuteNonQueryAsync();

                return Ok(new { message = "Saved successfully for selected department." });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error saving indent item.", detail = ex.Message });
            }
        }

        /// <summary>DMEFACADDIndent.aspx — delete selected cart lines.</summary>
        [HttpPost("facility-indents/{indentId:int}/items/delete")]
        public async Task<IActionResult> DeleteFacilityIndentItems(int indentId, [FromBody] DeleteFacilityIndentItemsRequest req)
        {
            if (indentId <= 0 || req.UserId <= 0 || req.IndentItemIds == null || req.IndentItemIds.Count == 0)
                return BadRequest(new { message = "indentId, userId and indentItemIds are required." });

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string headerSql = @"
SELECT ISNULL(status, 'N') AS status
FROM dbo.mas_indentfacility
WHERE indentid = @IndentId AND user_id = @UserId AND directorate_id = 12";
                await using (var headerCmd = new SqlCommand(headerSql, conn))
                {
                    headerCmd.Parameters.AddWithValue("@IndentId", indentId);
                    headerCmd.Parameters.AddWithValue("@UserId", req.UserId);
                    var statusObj = await headerCmd.ExecuteScalarAsync();
                    if (statusObj == null || statusObj == DBNull.Value)
                        return NotFound(new { message = "Indent not found." });
                    if (string.Equals(Convert.ToString(statusObj), "C", StringComparison.OrdinalIgnoreCase))
                        return BadRequest(new { message = "Indent is completed. Delete not allowed." });
                }

                var ids = req.IndentItemIds.Where(id => id > 0).Distinct().ToList();
                if (ids.Count == 0)
                    return BadRequest(new { message = "No valid indent item ids." });

                var paramNames = ids.Select((_, i) => $"@Id{i}").ToList();
                var deleteSql = $@"
DELETE FROM dbo.mas_item_indent
WHERE indentid = @IndentId
  AND indentitemid IN ({string.Join(", ", paramNames)})
  AND location_id IN (
      SELECT location_id FROM dbo.maslocations WHERE user_id = @UserId
  )";
                await using var deleteCmd = new SqlCommand(deleteSql, conn);
                deleteCmd.Parameters.AddWithValue("@IndentId", indentId);
                deleteCmd.Parameters.AddWithValue("@UserId", req.UserId);
                for (var i = 0; i < ids.Count; i++)
                    deleteCmd.Parameters.AddWithValue(paramNames[i], ids[i]);

                var affected = await deleteCmd.ExecuteNonQueryAsync();
                return Ok(new { message = $"{affected} item(s) deleted." });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error deleting indent items.", detail = ex.Message });
            }
        }

        /// <summary>ConsolidatedIndentDME_MC.aspx — download AI letter PDF (path/filename on mas_indentfacility).</summary>
        [HttpGet("facility-indents/{indentId:int}/download")]
        public async Task<IActionResult> DownloadFacilityIndentFile(int indentId, [FromQuery] int userId)
        {
            if (indentId <= 0 || userId <= 0)
                return BadRequest(new { message = "indentId and userId are required." });

            const string sql = @"
SELECT ISNULL(path, '') AS path, ISNULL(filename, '') AS filename
FROM dbo.mas_indentfacility
WHERE indentid = @IndentId AND user_id = @UserId AND directorate_id = 12";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@IndentId", indentId);
                cmd.Parameters.AddWithValue("@UserId", userId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound(new { message = "Indent not found." });

                var dbPath = reader["path"]?.ToString()?.Trim() ?? string.Empty;
                var fileName = reader["filename"]?.ToString()?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(dbPath) && string.IsNullOrWhiteSpace(fileName))
                    return NotFound(new { message = "File not uploaded for this indent." });

                var physicalPath = ResolveIndentPhysicalPath(dbPath, fileName);
                if (physicalPath == null || !System.IO.File.Exists(physicalPath))
                    return NotFound(new { message = "File not found on disk." });

                var downloadName = !string.IsNullOrWhiteSpace(fileName)
                    ? Path.GetFileName(fileName)
                    : Path.GetFileName(physicalPath);
                if (string.IsNullOrWhiteSpace(downloadName))
                    downloadName = $"indent_{indentId}.pdf";
                if (!downloadName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    downloadName += ".pdf";

                return PhysicalFile(physicalPath, "application/pdf", downloadName);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error downloading indent file.", detail = ex.Message });
            }
        }

        /// <summary>DMEFACADDIndent.aspx FillOTPDetails — store officer mobile/email for finalize modal.</summary>
        [HttpGet("facility-indents/{indentId:int}/otp-contact")]
        public async Task<IActionResult> GetFacilityIndentOtpContact(int indentId, [FromQuery] int userId)
        {
            if (indentId <= 0 || userId <= 0)
                return BadRequest(new { message = "indentId and userId are required." });

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                if (!await FacilityIndentBelongsToUserAsync(conn, indentId, userId))
                    return NotFound(new { message = "Indent not found." });

                const string sql = @"
SELECT ISNULL(storeOfficerMob, '') AS storeOfficerMob,
       ISNULL(emailID, '') AS emailID,
       ISNULL(user_name, '') AS user_name
FROM dbo.users WHERE user_id = @UserId";

                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound(new { message = "User not found." });

                return Ok(new FacilityIndentOtpContactDto
                {
                    Mobile = reader["storeOfficerMob"]?.ToString()?.Trim() ?? string.Empty,
                    Email = reader["emailID"]?.ToString()?.Trim() ?? string.Empty,
                    FacilityName = reader["user_name"]?.ToString()?.Trim() ?? string.Empty,
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading OTP contact.", detail = ex.Message });
            }
        }

        /// <summary>DMEFACADDIndent.aspx lnkSentOtp — store OTP on users.pwdChangeOTP + smslog.</summary>
        [HttpPost("facility-indents/{indentId:int}/send-otp")]
        public async Task<IActionResult> SendFacilityIndentOtp(int indentId, [FromBody] FacilityIndentSendOtpRequest req)
        {
            if (indentId <= 0 || req == null || req.UserId <= 0)
                return BadRequest(new { message = "indentId and userId are required." });

            var mobile = (req.Mobile ?? string.Empty).Trim();
            var email = (req.Email ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(mobile))
                return BadRequest(new { message = "Please Provide Mobile Number." });
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { message = "Please Provide Email Id." });

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var status = await GetFacilityIndentStatusAsync(conn, indentId, req.UserId);
                if (status == null)
                    return NotFound(new { message = "Indent not found." });
                if (string.Equals(status, "C", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { message = "Indent is Complete , Now No updation is allowed." });

                var itemCount = await CountFacilityIndentItemsAsync(conn, indentId);
                if (itemCount <= 0)
                    return BadRequest(new { message = "Please Enter Indent First" });

                var otp = Random.Shared.Next(1000, 9999).ToString();

                await using (var updateCmd = new SqlCommand(
                    "UPDATE dbo.users SET pwdChangeOTP = @Otp WHERE user_id = @UserId", conn))
                {
                    updateCmd.Parameters.AddWithValue("@Otp", otp);
                    updateCmd.Parameters.AddWithValue("@UserId", req.UserId);
                    if (await updateCmd.ExecuteNonQueryAsync() == 0)
                        return NotFound(new { message = "User not found." });
                }

                try
                {
                    var sms = _otpSms.BuildMessage(otp);
                    await using var logCmd = new SqlCommand(
                        @"INSERT INTO dbo.smslog(mobno, sms, entrydate, module, reason)
                          VALUES(@Mob, @Sms, GETDATE(), 'FAC', 'New Indent')", conn);
                    logCmd.Parameters.AddWithValue("@Mob", mobile);
                    logCmd.Parameters.AddWithValue("@Sms", sms);
                    await logCmd.ExecuteNonQueryAsync();
                }
                catch
                {
                    // smslog schema may differ; OTP is already on users.
                }

                var templateId = _otpSms.Options.Templates?.IndentFinalize
                    ?? "1407163911599431374";
                var (sent, detail) = await _otpSms.TrySendOtpAsync(mobile, otp, templateId);
                if (!sent && _otpSms.Options.Enabled)
                {
                    return StatusCode(502, new
                    {
                        message = "OTP saved but SMS gateway failed. Please retry or check OtpSms settings.",
                        detail,
                    });
                }

                var mobileMask = mobile.Length >= 4 ? "xxxxxx" + mobile[^4..] : mobile;
                return Ok(new
                {
                    message = $"OTP is sent to Your Mobile Number ({mobileMask}) / Email.",
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Failed to send OTP.", detail = ex.Message });
            }
        }

        /// <summary>
        /// DMEFACADDIndent.aspx btnYes — verify OTP, save letter+spec PDFs, set Status=C + dispatch fields.
        /// </summary>
        [HttpPost("facility-indents/{indentId:int}/finalize")]
        [RequestSizeLimit(8_000_000)]
        public async Task<IActionResult> FinalizeFacilityIndent(int indentId)
        {
            if (indentId <= 0)
                return BadRequest(new { message = "indentId is required." });

            if (!int.TryParse(Request.Form["userId"], out var userId) || userId <= 0)
                return BadRequest(new { message = "userId is required." });

            var otp = (Request.Form["otp"].ToString() ?? string.Empty).Trim();
            var dispatchNo = (Request.Form["dispatchNo"].ToString() ?? string.Empty).Trim();
            var dispatchDateRaw = (Request.Form["dispatchDate"].ToString() ?? string.Empty).Trim();
            var letter = Request.Form.Files.GetFile("letter") ?? Request.Form.Files.GetFile("Letter");
            var spec = Request.Form.Files.GetFile("spec") ?? Request.Form.Files.GetFile("Spec");

            if (string.IsNullOrWhiteSpace(otp))
                return BadRequest(new { message = "Please Submit 4 digit OTP sent on your mobile/email " });
            if (string.IsNullOrWhiteSpace(dispatchNo))
                return BadRequest(new { message = "Please enter Dispatch No." });
            if (string.IsNullOrWhiteSpace(dispatchDateRaw))
                return BadRequest(new { message = "Please enter Dispatch Date." });

            if (!DateTime.TryParseExact(
                    dispatchDateRaw,
                    new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dispatchDate))
            {
                return BadRequest(new { message = "Dispatch Date must be dd/MM/yyyy." });
            }

            if (letter == null || letter.Length == 0)
                return BadRequest(new { message = "Please Select Document to Upload." });
            if (spec == null || spec.Length == 0)
                return BadRequest(new { message = "Please Upload Specificaiton in Single PDF File" });

            static bool IsPdf(IFormFile f) =>
                Path.GetExtension(f.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

            if (!IsPdf(letter) || !IsPdf(spec))
                return BadRequest(new { message = "Please upload pdf file only" });
            if (letter.Length > 2_000_000)
                return BadRequest(new { message = "You can't upload file more then 2 mb." });
            if (spec.Length > 5_000_000)
                return BadRequest(new { message = "You can't upload file more then 5 mb." });

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var status = await GetFacilityIndentStatusAsync(conn, indentId, userId);
                if (status == null)
                    return NotFound(new { message = "Indent not found." });
                if (string.Equals(status, "C", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { message = "Indent is Complete , Now No updation is allowed." });

                var itemCount = await CountFacilityIndentItemsAsync(conn, indentId);
                if (itemCount <= 0)
                    return BadRequest(new { message = "Please Enter Indent First" });

                string storedOtp;
                await using (var otpCmd = new SqlCommand(
                    "SELECT ISNULL(CAST(pwdChangeOTP AS VARCHAR(20)), '') AS otp FROM dbo.users WHERE user_id = @UserId",
                    conn))
                {
                    otpCmd.Parameters.AddWithValue("@UserId", userId);
                    storedOtp = (await otpCmd.ExecuteScalarAsync())?.ToString()?.Trim() ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(storedOtp))
                    return BadRequest(new { message = "Please click to send OTP first" });
                if (!string.Equals(storedOtp, otp, StringComparison.Ordinal))
                    return BadRequest(new { message = "The OTP Entered is incorrect." });

                Directory.CreateDirectory(_indentUploadsRoot);
                var stamp = $"ID{DateTime.Now.Day}{DateTime.Now.Month}{DateTime.Now:yy}_{indentId}";
                var letterName = $"{stamp}_indentDoc.pdf";
                var specName = $"{stamp}_indentSpex.pdf";
                var letterPhysical = Path.Combine(_indentUploadsRoot, letterName);
                var specPhysical = Path.Combine(_indentUploadsRoot, specName);
                var letterDbPath = $"~/Uploads/{letterName}";
                var specDbPath = $"~/Uploads/{specName}";

                await using (var letterStream = System.IO.File.Create(letterPhysical))
                    await letter.CopyToAsync(letterStream);
                await using (var specStream = System.IO.File.Create(specPhysical))
                    await spec.CopyToAsync(specStream);

                const string updateSql = @"
UPDATE dbo.mas_indentfacility
SET pathSpex = @PathSpex,
    filenameSpex = @FileSpex,
    path = @Path,
    filename = @FileName,
    Status = 'C',
    DispatchNo = @DispatchNo,
    DispatchDT = @DispatchDT,
    DispatchEntryDT = GETDATE()
WHERE indentid = @IndentId AND user_id = @UserId AND directorate_id = 12";

                await using (var updateCmd = new SqlCommand(updateSql, conn))
                {
                    updateCmd.Parameters.AddWithValue("@PathSpex", specDbPath);
                    updateCmd.Parameters.AddWithValue("@FileSpex", specName);
                    updateCmd.Parameters.AddWithValue("@Path", letterDbPath);
                    updateCmd.Parameters.AddWithValue("@FileName", letterName);
                    updateCmd.Parameters.AddWithValue("@DispatchNo", dispatchNo);
                    updateCmd.Parameters.AddWithValue("@DispatchDT", dispatchDate);
                    updateCmd.Parameters.AddWithValue("@IndentId", indentId);
                    updateCmd.Parameters.AddWithValue("@UserId", userId);
                    if (await updateCmd.ExecuteNonQueryAsync() == 0)
                        return NotFound(new { message = "Indent not found." });
                }

                try
                {
                    await using var clearOtp = new SqlCommand(
                        "UPDATE dbo.users SET pwdChangeOTP = NULL WHERE user_id = @UserId", conn);
                    clearOtp.Parameters.AddWithValue("@UserId", userId);
                    await clearOtp.ExecuteNonQueryAsync();
                }
                catch
                {
                    // optional clear
                }

                return Ok(new { message = "Uploaded Successfully." });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Failed to Upload ,Please Retry", detail = ex.Message });
            }
            catch (IOException ex)
            {
                return StatusCode(500, new { message = "Somthing Wrong in Uploading", detail = ex.Message });
            }
        }

        private static async Task<bool> FacilityIndentBelongsToUserAsync(SqlConnection conn, int indentId, int userId)
        {
            const string sql = @"
SELECT 1 FROM dbo.mas_indentfacility
WHERE indentid = @IndentId AND user_id = @UserId AND directorate_id = 12";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@IndentId", indentId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            var result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value;
        }

        private static async Task<string?> GetFacilityIndentStatusAsync(SqlConnection conn, int indentId, int userId)
        {
            const string sql = @"
SELECT ISNULL(status, 'N') FROM dbo.mas_indentfacility
WHERE indentid = @IndentId AND user_id = @UserId AND directorate_id = 12";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@IndentId", indentId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? null : result.ToString();
        }

        private static async Task<int> CountFacilityIndentItemsAsync(SqlConnection conn, int indentId)
        {
            const string sql = "SELECT COUNT(1) FROM dbo.mas_item_indent WHERE indentid = @IndentId";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@IndentId", indentId);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        /// <summary>Indent_ReportDME_MCwise.aspx — annual indent report by ICID.</summary>
        [HttpGet("facility-indents/{indentId:int}/report")]
        public async Task<IActionResult> GetFacilityIndentReport(int indentId, [FromQuery] int userId)
        {
            if (indentId <= 0 || userId <= 0)
                return BadRequest(new { message = "indentId and userId are required." });

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var hasAsColumns = await TableHasColumnsAsync(conn, "mas_indentfacility", "ASLetterNo", "ASDate");
                var asSelect = hasAsColumns
                    ? @"ISNULL(i.ASLetterNo, '') AS ASLetterNo,
       CONVERT(VARCHAR, i.ASDate, 103) AS ASDate"
                    : @"'' AS ASLetterNo,
       '' AS ASDate";

                var headerSql = $@"
SELECT i.indentid, i.user_id, u.user_name, i.financial_year_id, f.year,
       CONVERT(VARCHAR, i.indentdate, 103) AS indentdate,
       ISNULL(i.status, 'N') AS status,
       CASE WHEN i.BUDGETID IS NOT NULL THEN h.headName ELSE '' END AS BudgetName,
       ISNULL(i.DispatchNo, '') AS DispatchNo,
       {asSelect}
FROM dbo.mas_indentfacility i
LEFT OUTER JOIN dbo.DMEFACHead h ON h.headid = i.BUDGETID
INNER JOIN dbo.mas_financial_year f ON f.financial_year_id = i.financial_year_id
INNER JOIN dbo.users u ON u.user_id = i.user_id
WHERE i.indentid = @IndentId AND i.user_id = @UserId AND i.directorate_id = 12";

                FacilityIndentHeaderDto? header = null;
                await using (var headerCmd = new SqlCommand(headerSql, conn))
                {
                    headerCmd.Parameters.AddWithValue("@IndentId", indentId);
                    headerCmd.Parameters.AddWithValue("@UserId", userId);
                    await using var reader = await headerCmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                        return NotFound(new { message = "Indent not found." });

                    var status = reader["status"]?.ToString() ?? "N";
                    var completed = string.Equals(status, "C", StringComparison.OrdinalIgnoreCase);
                    header = new FacilityIndentHeaderDto
                    {
                        IndentId = Convert.ToInt32(reader["indentid"]),
                        UserId = Convert.ToInt32(reader["user_id"]),
                        McName = reader["user_name"]?.ToString() ?? string.Empty,
                        BudgetName = reader["BudgetName"]?.ToString() ?? string.Empty,
                        IndentDate = reader["indentdate"]?.ToString() ?? string.Empty,
                        FinancialYearId = reader["financial_year_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["financial_year_id"]),
                        Year = reader["year"]?.ToString() ?? string.Empty,
                        Status = status,
                        StatusLabel = completed ? "Completed" : "Incomplete",
                        AsLetterNo = reader["ASLetterNo"]?.ToString() ?? string.Empty,
                        AsDate = reader["ASDate"]?.ToString() ?? string.Empty,
                        DispatchNo = reader["DispatchNo"]?.ToString() ?? string.Empty,
                        IsCompleted = completed,
                    };
                }

                // Prefer legacy report query (RC supplier + tender). Fall back if views missing.
                var lines = await TryLoadIndentReportLinesAsync(conn, indentId, rich: true);
                if (lines == null)
                    lines = await TryLoadIndentReportLinesAsync(conn, indentId, rich: false) ?? new List<FacilityIndentReportLineDto>();

                return Ok(new FacilityIndentReportDto { Header = header, Lines = lines });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading indent report.", detail = ex.Message });
            }
        }

        private static async Task<List<FacilityIndentReportLineDto>?> TryLoadIndentReportLinesAsync(
            SqlConnection conn, int indentId, bool rich)
        {
            var sql = rich
                ? @"
SELECT m.item_code_as_per_tender AS eqpcode, m.item_name AS itemname, l.location_name,
       mii.facility_ind_qty AS indent_quantity, mii.approxrate AS estimated_cost,
       CAST(mii.approxrate * mii.facility_ind_qty AS bigint) AS Value,
       ISNULL(mii.remarks, '') AS remarks,
       CASE WHEN aa.contract_end_date IS NOT NULL THEN aa.contract_end_date ELSE 'RC not Valid' END AS RCstatus,
       ISNULL(rc.Supplier, '') AS Supplier,
       ISNULL(CAST(rc.PriceIncGST AS VARCHAR(50)), '') AS PriceIncGST,
       CASE WHEN rc.rcEndDT IS NOT NULL THEN '' ELSE ISNULL(t.tender_no, '') END AS tender_no,
       CASE WHEN rc.rcEndDT IS NOT NULL THEN '' ELSE ISNULL(t.finalstatus, '') END AS finalstatus
FROM dbo.mas_indentfacility mi
INNER JOIN dbo.mas_item_indent mii ON mii.indentid = mi.indentid
INNER JOIN dbo.masitems m ON m.item_id = mii.itemid
INNER JOIN dbo.maslocations l ON l.location_id = mii.location_id
LEFT OUTER JOIN (
    SELECT m2.item_id,
           CASE WHEN c.contract_new_end_date IS NOT NULL
                THEN CONVERT(VARCHAR, c.contract_new_end_date, 103)
                ELSE CONVERT(VARCHAR, ac.contract_end_date, 103) END AS contract_end_date
    FROM dbo.contract_items c
    INNER JOIN dbo.masitems m2 ON m2.item_id = c.item_id
    INNER JOIN dbo.award_of_contract ac ON ac.award_of_contract_id = c.award_of_contract_id
    WHERE GETDATE() BETWEEN ac.contract_date AND ac.contract_end_date
      AND (m2.categoryid = 1 OR m2.categoryid IS NULL)
) aa ON aa.item_id = mii.itemid
LEFT OUTER JOIN (
    SELECT r.item_id, r.Supplier, r.rcStartDT, r.rcEndDT, r.PriceIncGST
    FROM dbo.v_rcvalid r
) rc ON rc.item_id = m.item_id
LEFT OUTER JOIN (
    SELECT ti.item_id, ti.tender_no, ti.finalstatus
    FROM (
        SELECT item_id, MAX(tender_date) AS tenderDT
        FROM dbo.v_Tenderstatus
        GROUP BY item_id
    ) tmax
    INNER JOIN dbo.v_Tenderstatus ti ON ti.item_id = tmax.item_id AND ti.tender_date = tmax.tenderDT
) t ON t.item_id = m.item_id
WHERE mii.indentid = @IndentId
ORDER BY m.item_name, l.location_name"
                : @"
SELECT m.item_code_as_per_tender AS eqpcode, m.item_name AS itemname, l.location_name,
       mii.facility_ind_qty AS indent_quantity, mii.approxrate AS estimated_cost,
       CAST(mii.approxrate * mii.facility_ind_qty AS bigint) AS Value,
       ISNULL(mii.remarks, '') AS remarks,
       CASE WHEN aa.contract_end_date IS NOT NULL THEN aa.contract_end_date ELSE 'RC not Valid' END AS RCstatus,
       '' AS Supplier, '' AS PriceIncGST, '' AS tender_no, '' AS finalstatus
FROM dbo.mas_indentfacility mi
INNER JOIN dbo.mas_item_indent mii ON mii.indentid = mi.indentid
INNER JOIN dbo.masitems m ON m.item_id = mii.itemid
INNER JOIN dbo.maslocations l ON l.location_id = mii.location_id
LEFT OUTER JOIN (
    SELECT m2.item_id,
           CASE WHEN c.contract_new_end_date IS NOT NULL
                THEN CONVERT(VARCHAR, c.contract_new_end_date, 103)
                ELSE CONVERT(VARCHAR, ac.contract_end_date, 103) END AS contract_end_date
    FROM dbo.contract_items c
    INNER JOIN dbo.masitems m2 ON m2.item_id = c.item_id
    INNER JOIN dbo.award_of_contract ac ON ac.award_of_contract_id = c.award_of_contract_id
    WHERE GETDATE() BETWEEN ac.contract_date AND ac.contract_end_date
      AND (m2.categoryid = 1 OR m2.categoryid IS NULL)
) aa ON aa.item_id = mii.itemid
WHERE mii.indentid = @IndentId
ORDER BY m.item_name, l.location_name";

            try
            {
                var list = new List<FacilityIndentReportLineDto>();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@IndentId", indentId);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new FacilityIndentReportLineDto
                    {
                        ItemCode = reader["eqpcode"]?.ToString() ?? string.Empty,
                        ItemName = reader["itemname"]?.ToString() ?? string.Empty,
                        LocationName = reader["location_name"]?.ToString() ?? string.Empty,
                        IndentQuantity = reader["indent_quantity"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["indent_quantity"]),
                        EstimatedCost = reader["estimated_cost"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["estimated_cost"]),
                        Value = reader["Value"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["Value"]),
                        Remarks = reader["remarks"]?.ToString() ?? string.Empty,
                        RcStatus = reader["RCstatus"]?.ToString() ?? string.Empty,
                        Supplier = reader["Supplier"]?.ToString() ?? string.Empty,
                        PriceIncGst = reader["PriceIncGST"]?.ToString() ?? string.Empty,
                        TenderNo = reader["tender_no"]?.ToString() ?? string.Empty,
                        TenderStatus = reader["finalstatus"]?.ToString() ?? string.Empty,
                    });
                }

                return list;
            }
            catch (SqlException)
            {
                return null;
            }
        }

        private string? ResolveIndentPhysicalPath(string dbPath, string fileName)
        {
            var candidates = new List<string>();

            void addCandidate(string? path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    return;
                var full = Path.GetFullPath(path);
                if (!candidates.Contains(full, StringComparer.OrdinalIgnoreCase))
                    candidates.Add(full);
            }

            // Legacy stores "~/Uploads/ID….pdf"
            if (!string.IsNullOrWhiteSpace(dbPath))
            {
                var cleaned = dbPath.Trim().Replace('\\', '/');
                if (cleaned.StartsWith("~/", StringComparison.Ordinal))
                    cleaned = cleaned[2..];
                else if (cleaned.StartsWith("~", StringComparison.Ordinal))
                    cleaned = cleaned[1..].TrimStart('/');

                if (Path.IsPathRooted(dbPath.Trim()) && System.IO.File.Exists(dbPath.Trim()))
                    return Path.GetFullPath(dbPath.Trim());

                addCandidate(Path.Combine(_emsRoleRoot, cleaned.Replace('/', Path.DirectorySeparatorChar)));
                addCandidate(Path.Combine(_indentUploadsRoot, Path.GetFileName(cleaned)));
                addCandidate(Path.Combine(_emsRoleRoot, "Uploads", Path.GetFileName(cleaned)));
            }

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var name = Path.GetFileName(fileName.Trim());
                addCandidate(Path.Combine(_indentUploadsRoot, name));
                addCandidate(Path.Combine(_emsRoleRoot, "Uploads", name));
            }

            return candidates.FirstOrDefault(System.IO.File.Exists);
        }

        private static async Task<bool> TableHasColumnsAsync(SqlConnection conn, string tableName, params string[] columns)
        {
            var sql = @"
SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @Table
  AND COLUMN_NAME IN (" + string.Join(", ", columns.Select((_, i) => $"@C{i}")) + ")";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Table", tableName);
            for (var i = 0; i < columns.Length; i++)
                cmd.Parameters.AddWithValue($"@C{i}", columns[i]);

            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return count == columns.Length;
        }

        // --- CMCdetail.aspx (also on api/DMEReports after API restart) ---

        [HttpGet("cmc-items")]
        public async Task<IActionResult> GetCmcItems()
        {
            const string sql = @"
SELECT A.ITEM_CODE_AS_PER_TENDER, A.item_code_as_per_tender + '-' + A.item_name AS item_name
FROM dbo.MASITEMS A
INNER JOIN dbo.tender_items TI ON TI.item_id = A.item_id
WHERE A.item_code_as_per_tender IS NOT NULL
ORDER BY A.ITEM_CODE_AS_PER_TENDER";

            var list = new List<CmcItemOptionDto> { new() { ItemCodeAsPerTender = "0", ItemName = "--ALL--" } };

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new CmcItemOptionDto
                    {
                        ItemCodeAsPerTender = reader["ITEM_CODE_AS_PER_TENDER"]?.ToString() ?? string.Empty,
                        ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading items.", detail = ex.Message });
            }
        }

        [HttpGet("cmc-tenders")]
        public async Task<IActionResult> GetCmcTenders([FromQuery] string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode) || itemCode == "0")
                return Ok(new List<CmcTenderOptionDto> { new() { TenderId = 0, TenderNo = "--Select--" } });

            const string sql = @"
SELECT i.item_id, t.tender_id, t.tender_no
FROM dbo.masitems i
INNER JOIN dbo.tender_items ti ON ti.item_id = i.item_id
INNER JOIN dbo.tenders t ON t.tender_id = ti.tender_id
WHERE i.item_code_as_per_tender = @ItemCode";

            var list = new List<CmcTenderOptionDto> { new() { TenderId = 0, TenderNo = "--Select--" } };

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ItemCode", itemCode.Trim());
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new CmcTenderOptionDto
                    {
                        TenderId = Convert.ToInt32(reader["tender_id"]),
                        TenderNo = reader["tender_no"]?.ToString() ?? string.Empty,
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading tenders.", detail = ex.Message });
            }
        }

        [HttpGet("cmc-detail")]
        public async Task<IActionResult> GetCmcDetail(
            [FromQuery] string? itemCode = null,
            [FromQuery] int tenderId = 0)
        {
            var sql = @"
SELECT i.item_id, t.tender_id, i.item_code, i.item_code_as_per_tender, i.item_name, t.tender_no,
       tp.CMC1, tp.CMC2, tp.CMC3, tp.CMC4, tp.CMC5
FROM dbo.masitems i
INNER JOIN dbo.tender_items ti ON ti.item_id = i.item_id
INNER JOIN dbo.tenders t ON t.tender_id = ti.tender_id
INNER JOIN dbo.live_tender_price tp ON tp.tender_item_id = ti.tender_item_id
WHERE 1 = 1
  AND (@ItemCode IS NULL OR @ItemCode = '' OR @ItemCode = '0' OR i.item_code_as_per_tender = @ItemCode)
  AND (@TenderId = 0 OR t.tender_id = @TenderId)
ORDER BY i.item_code_as_per_tender";

            var list = new List<CmcDetailRowDto>();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ItemCode", string.IsNullOrWhiteSpace(itemCode) ? DBNull.Value : itemCode.Trim());
                cmd.Parameters.AddWithValue("@TenderId", tenderId);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new CmcDetailRowDto
                    {
                        ItemId = Convert.ToInt32(reader["item_id"]),
                        TenderId = Convert.ToInt32(reader["tender_id"]),
                        ItemCode = reader["item_code"]?.ToString() ?? string.Empty,
                        ItemCodeAsPerTender = reader["item_code_as_per_tender"]?.ToString() ?? string.Empty,
                        ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                        TenderNo = reader["tender_no"]?.ToString() ?? string.Empty,
                        Cmc1 = reader["CMC1"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["CMC1"]),
                        Cmc2 = reader["CMC2"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["CMC2"]),
                        Cmc3 = reader["CMC3"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["CMC3"]),
                        Cmc4 = reader["CMC4"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["CMC4"]),
                        Cmc5 = reader["CMC5"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["CMC5"]),
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading CMC detail.", detail = ex.Message });
            }
        }

        // --- Facility PO receipt / installation entry (FacilityPO_ReceiptDME) ---

        /// <summary>FacilityPO_ReceiptDME.aspx — load receipt entry page for facility DME.</summary>
        [HttpGet("receipt-entry")]
        public async Task<IActionResult> GetReceiptEntryPage(
            [FromQuery] int userId,
            [FromQuery] int poId,
            [FromQuery] int locId,
            [FromQuery] int issueId)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });
            if (poId <= 0 || locId <= 0 || issueId <= 0)
                return BadRequest(new { message = "PO, consignee and issue are required." });

            try
            {
                await using var con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                if (!await LocationBelongsToUserAsync(con, locId, userId))
                    return NotFound(new { message = "Location not found for this facility user." });

                var page = await LoadDmeReceiptEntryPageAsync(con, userId, poId, locId, issueId);
                return Ok(page);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading receipt entry page.", error = ex.Message });
            }
        }

        [HttpPost("receipt-entry")]
        public async Task<IActionResult> SaveReceiptEntry([FromBody] DmeReceiptSaveRequestDto request)
        {
            if (request == null || request.UserId <= 0)
                return BadRequest(new { message = "Invalid user id." });
            if (request.PoId <= 0 || request.LocationId <= 0 || request.IssueId <= 0)
                return BadRequest(new { message = "PO, consignee and issue are required." });
            if (string.IsNullOrWhiteSpace(request.ReceivedDate) ||
                string.IsNullOrWhiteSpace(request.ReceiptNo) ||
                string.IsNullOrWhiteSpace(request.ReceiptQty) ||
                string.IsNullOrWhiteSpace(request.ReceiptRemarks))
                return BadRequest(new { message = "Received date, receipt no, qty and remarks are required." });

            if (!TryParseLegacyDate(request.ReceivedDate, out DateTime receivedDate))
                return BadRequest(new { message = "Invalid received date format." });
            if (!decimal.TryParse(request.ReceiptQty, out decimal receiptQty) || receiptQty <= 0)
                return BadRequest(new { message = "Receipt qty should be greater than zero." });

            try
            {
                await using var con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                if (!await LocationBelongsToUserAsync(con, request.LocationId, request.UserId))
                    return NotFound(new { message = "Location not found for this facility user." });

                var page = await LoadDmeReceiptEntryPageAsync(
                    con, request.UserId, request.PoId, request.LocationId, request.IssueId);

                if (!TryParseLegacyDate(page.DispatchDate, out DateTime dispatchDate))
                    return BadRequest(new { message = "Dispatch date is missing on issue." });
                if (receivedDate < dispatchDate)
                    return BadRequest(new { message = "Received date cannot be before supplier dispatch date." });
                if (receivedDate.Date > DateTime.Today)
                    return BadRequest(new { message = "Received date cannot be greater than today." });
                if (TryParseLegacyDate(page.LastReceiptDate, out DateTime lastReceiptDate) &&
                    receivedDate.Date > lastReceiptDate.Date)
                    return BadRequest(new
                    {
                        message = $"Received date cannot be greater than last date to be received of PO ({page.LastReceiptDate}). Please contact BME for further clarification.",
                    });

                if (receiptQty > page.DispatchedQty)
                    return BadRequest(new { message = "You cannot receive more than dispatched quantity." });

                int receiptId = page.ReceiptId;
                if (receiptId > 0)
                {
                    const string updateSql = @"
UPDATE dbo.receipts
SET recieved_date = @ReceivedDate,
    receipt_no = @ReceiptNo,
    receipt_qty = @ReceiptQty,
    remarks = @ReceiptRemarks,
    status = 'Received',
    entryDT = GETDATE()
WHERE receipt_id = @ReceiptId";
                    await using var updateCmd = new SqlCommand(updateSql, con);
                    updateCmd.Parameters.AddWithValue("@ReceivedDate", receivedDate);
                    updateCmd.Parameters.AddWithValue("@ReceiptNo", request.ReceiptNo.Trim());
                    updateCmd.Parameters.AddWithValue("@ReceiptQty", request.ReceiptQty.Trim());
                    updateCmd.Parameters.AddWithValue("@ReceiptRemarks", request.ReceiptRemarks.Trim());
                    updateCmd.Parameters.AddWithValue("@ReceiptId", receiptId);
                    await updateCmd.ExecuteNonQueryAsync();
                }
                else
                {
                    const string insertSql = @"
INSERT INTO dbo.receipts(issue_id, po_id, location_id, remarks, status, challan_no, challan_date, recieved_date,
                     receipt_no, SupplierRemarks, receipt_qty, entryDT)
VALUES(@IssueId, @PoId, @LocId, @SupplierRemarks, 'Received', @ChallanNo, @ChallanDate, @ReceivedDate,
       @ReceiptNo, @ReceiptRemarks, @ReceiptQty, GETDATE());
SELECT CAST(SCOPE_IDENTITY() AS INT);";
                    await using var insertCmd = new SqlCommand(insertSql, con);
                    insertCmd.Parameters.AddWithValue("@IssueId", request.IssueId);
                    insertCmd.Parameters.AddWithValue("@PoId", request.PoId);
                    insertCmd.Parameters.AddWithValue("@LocId", request.LocationId);
                    insertCmd.Parameters.AddWithValue("@SupplierRemarks", page.SupplierRemarks);
                    insertCmd.Parameters.AddWithValue("@ChallanNo", page.ChallanNo);
                    insertCmd.Parameters.AddWithValue("@ChallanDate",
                        TryParseLegacyDate(page.ChallanDate, out DateTime challanDate) ? challanDate : DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@ReceivedDate", receivedDate);
                    insertCmd.Parameters.AddWithValue("@ReceiptNo", request.ReceiptNo.Trim());
                    insertCmd.Parameters.AddWithValue("@ReceiptRemarks", request.ReceiptRemarks.Trim());
                    insertCmd.Parameters.AddWithValue("@ReceiptQty", request.ReceiptQty.Trim());
                    receiptId = Convert.ToInt32(await insertCmd.ExecuteScalarAsync());
                }

                return Ok(new { message = "Receipt details saved successfully.", receiptId });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error saving receipt details.", error = ex.Message });
            }
        }

        [HttpPost("receipt-entry/installation")]
        public async Task<IActionResult> SaveReceiptInstallation(
            [FromBody] DmeReceiptInstallationSaveRequestDto request)
        {
            if (request == null || request.UserId <= 0)
                return BadRequest(new { message = "Invalid user id." });
            if (request.ReceiptId <= 0 || request.IssueDetailId <= 0)
                return BadRequest(new { message = "Receipt id and issue detail are required." });
            if (request.ReceivedQty <= 0)
                return BadRequest(new { message = "Installed qty should be greater than zero." });
            if (string.IsNullOrWhiteSpace(request.WarrantyCardNo) ||
                string.IsNullOrWhiteSpace(request.InstallationBy) ||
                string.IsNullOrWhiteSpace(request.InstallationLocation) ||
                string.IsNullOrWhiteSpace(request.InstallationDate))
                return BadRequest(new { message = "Warranty card, installation by/location/date are required." });
            if (!TryParseLegacyDate(request.InstallationDate, out DateTime installationDate))
                return BadRequest(new { message = "Invalid installation date format." });

            try
            {
                await using var con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                if (!await ReceiptBelongsToFacilityUserAsync(con, request.ReceiptId, request.UserId))
                    return NotFound(new { message = "Receipt not found for this facility user." });

                const string issueSql = @"
SELECT i.issue_detail_id, i.make_no, i.warranty_certificate_no, i.Supplyqty,
       d.issue_id, d.recieved_date
FROM dbo.Issue_item_details i
INNER JOIN dbo.SupplierDispatch d ON d.Issue_id = i.Issue_id
INNER JOIN dbo.receipts r ON r.issue_id = d.Issue_id AND r.receipt_id = @ReceiptId
WHERE i.issue_detail_id = @IssueDetailId";
                string warrantyCertificate = string.Empty;
                decimal dispatchQty = 0;
                await using (var issueCmd = new SqlCommand(issueSql, con))
                {
                    issueCmd.Parameters.AddWithValue("@ReceiptId", request.ReceiptId);
                    issueCmd.Parameters.AddWithValue("@IssueDetailId", request.IssueDetailId);
                    await using var reader = await issueCmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                        return NotFound(new { message = "Dispatch serial detail not found." });
                    warrantyCertificate = ReadStringColumn(reader, "warranty_certificate_no", "Warranty_CertificateNo");
                    dispatchQty = ReadDecimalColumn(reader, "Supplyqty");
                }

                if (request.ReceivedQty > dispatchQty)
                    return BadRequest(new { message = "Installed qty cannot be more than dispatched qty." });

                DateTime? receiptDate = null;
                const string receiptDateSql = "SELECT recieved_date FROM dbo.receipts WHERE receipt_id = @ReceiptId";
                await using (var recCmd = new SqlCommand(receiptDateSql, con))
                {
                    recCmd.Parameters.AddWithValue("@ReceiptId", request.ReceiptId);
                    object? result = await recCmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                        receiptDate = Convert.ToDateTime(result);
                }

                if (receiptDate == null)
                    return BadRequest(new { message = "Please save receipt details first." });
                if (installationDate.Date < receiptDate.Value.Date)
                    return BadRequest(new { message = "Installation date cannot be before received date." });
                if (installationDate.Date > DateTime.Today)
                    return BadRequest(new { message = "Installation date cannot be greater than today." });

                int warrantyYears = 1;
                const string warrantySql = @"
SELECT ISNULL(t.warranty_year, 1)
FROM dbo.receipts r
INNER JOIN dbo.purchase_order p ON p.po_id = r.po_id
LEFT JOIN dbo.tenders t ON t.tender_id = p.tender_id
WHERE r.receipt_id = @ReceiptId";
                await using (var warrantyCmd = new SqlCommand(warrantySql, con))
                {
                    warrantyCmd.Parameters.AddWithValue("@ReceiptId", request.ReceiptId);
                    object? warrantyResult = await warrantyCmd.ExecuteScalarAsync();
                    if (warrantyResult != null && warrantyResult != DBNull.Value)
                        warrantyYears = Convert.ToInt32(warrantyResult);
                }
                if (warrantyYears <= 0)
                    warrantyYears = 1;

                DateTime warrantyFrom = installationDate.Date;
                DateTime warrantyTo = installationDate.Date.AddYears(warrantyYears);
                const string updateExistingSql = @"
UPDATE dbo.receipt_item_details
SET installation_date = @InstallationDate,
    warenty_from = @WarrantyFrom,
    warenty_to = @WarrantyTo,
    status = 'I',
    installation_location = @InstallationLocation,
    received_qty = @ReceivedQty,
    warranty_card_no = @WarrantyCardNo,
    installation_by = @InstallationBy,
    cgmsc_log_printed = @CgmscLogoPrinted,
    warranty_validity = @WarrantyValidity,
    manual_provided = @ServiceManual,
    calibration_certificate_prov = @CalibrationCertificate,
    org_warranty_card_rec = @WarrantyCard,
    other_statutory = @OtherStatutory,
    inticated_po_are_received = @PoDocuments,
    opening_manual_provided = @OperatingManual,
    warranty_certificate_no = @WarrantyCertificateNo,
    entryDT = GETDATE()
WHERE receipt_id = @ReceiptId AND issue_detail_id = @IssueDetailId";
                await using var updateCmd = new SqlCommand(updateExistingSql, con);
                updateCmd.Parameters.AddWithValue("@InstallationDate", installationDate);
                updateCmd.Parameters.AddWithValue("@WarrantyFrom", warrantyFrom);
                updateCmd.Parameters.AddWithValue("@WarrantyTo", warrantyTo);
                updateCmd.Parameters.AddWithValue("@InstallationLocation", request.InstallationLocation.Trim());
                updateCmd.Parameters.AddWithValue("@ReceivedQty", request.ReceivedQty);
                updateCmd.Parameters.AddWithValue("@WarrantyCardNo", request.WarrantyCardNo.Trim());
                updateCmd.Parameters.AddWithValue("@InstallationBy", request.InstallationBy.Trim());
                updateCmd.Parameters.AddWithValue("@CgmscLogoPrinted", request.CgmscLogoPrinted);
                updateCmd.Parameters.AddWithValue("@WarrantyValidity", request.WarrantyValidity);
                updateCmd.Parameters.AddWithValue("@ServiceManual", request.ServiceManual);
                updateCmd.Parameters.AddWithValue("@CalibrationCertificate", request.CalibrationCertificate);
                updateCmd.Parameters.AddWithValue("@WarrantyCard", request.WarrantyCard);
                updateCmd.Parameters.AddWithValue("@OtherStatutory", request.OtherStatutory);
                updateCmd.Parameters.AddWithValue("@PoDocuments", request.PoDocuments);
                updateCmd.Parameters.AddWithValue("@OperatingManual", request.OperatingManual);
                updateCmd.Parameters.AddWithValue("@WarrantyCertificateNo", warrantyCertificate);
                updateCmd.Parameters.AddWithValue("@ReceiptId", request.ReceiptId);
                updateCmd.Parameters.AddWithValue("@IssueDetailId", request.IssueDetailId);
                int affected = await updateCmd.ExecuteNonQueryAsync();

                if (affected == 0)
                {
                    const string insertSql = @"
INSERT INTO dbo.receipt_item_details(model_no, make_no, installation_date, warenty_from, warenty_to, status,
    equpitment_code, make, installation_location, receipt_id, issue_detail_id, received_qty, warranty_card_no,
    installation_by, cgmsc_log_printed, warranty_validity, manual_provided, calibration_certificate_prov,
    org_warranty_card_rec, other_statutory, inticated_po_are_received, opening_manual_provided,
    warranty_certificate_no, entryDT)
SELECT ISNULL(ci.model, '') AS model_no, i.make_no, @InstallationDate, @WarrantyFrom, @WarrantyTo, 'I',
       mi.item_code_as_per_tender, ISNULL(ci.make, ''), @InstallationLocation, @ReceiptId, @IssueDetailId, @ReceivedQty,
       @WarrantyCardNo, @InstallationBy, @CgmscLogoPrinted, @WarrantyValidity, @ServiceManual,
       @CalibrationCertificate, @WarrantyCard, @OtherStatutory, @PoDocuments, @OperatingManual,
       @WarrantyCertificateNo, GETDATE()
FROM dbo.Issue_item_details i
INNER JOIN dbo.SupplierDispatch d ON d.Issue_id = i.Issue_id
INNER JOIN dbo.purchase_order po ON po.po_id = d.po_id
INNER JOIN dbo.po_items pi ON pi.po_id = d.po_id AND pi.consignee_id = d.location_id
LEFT JOIN dbo.contract_items ci ON ci.contract_item_id = pi.contract_item_id
INNER JOIN dbo.masitems mi ON mi.item_id = pi.item_id
WHERE i.issue_detail_id = @IssueDetailId";
                    await using var insertCmd = new SqlCommand(insertSql, con);
                    insertCmd.Parameters.AddWithValue("@InstallationDate", installationDate);
                    insertCmd.Parameters.AddWithValue("@WarrantyFrom", warrantyFrom);
                    insertCmd.Parameters.AddWithValue("@WarrantyTo", warrantyTo);
                    insertCmd.Parameters.AddWithValue("@InstallationLocation", request.InstallationLocation.Trim());
                    insertCmd.Parameters.AddWithValue("@ReceiptId", request.ReceiptId);
                    insertCmd.Parameters.AddWithValue("@IssueDetailId", request.IssueDetailId);
                    insertCmd.Parameters.AddWithValue("@ReceivedQty", request.ReceivedQty);
                    insertCmd.Parameters.AddWithValue("@WarrantyCardNo", request.WarrantyCardNo.Trim());
                    insertCmd.Parameters.AddWithValue("@InstallationBy", request.InstallationBy.Trim());
                    insertCmd.Parameters.AddWithValue("@CgmscLogoPrinted", request.CgmscLogoPrinted);
                    insertCmd.Parameters.AddWithValue("@WarrantyValidity", request.WarrantyValidity);
                    insertCmd.Parameters.AddWithValue("@ServiceManual", request.ServiceManual);
                    insertCmd.Parameters.AddWithValue("@CalibrationCertificate", request.CalibrationCertificate);
                    insertCmd.Parameters.AddWithValue("@WarrantyCard", request.WarrantyCard);
                    insertCmd.Parameters.AddWithValue("@OtherStatutory", request.OtherStatutory);
                    insertCmd.Parameters.AddWithValue("@PoDocuments", request.PoDocuments);
                    insertCmd.Parameters.AddWithValue("@OperatingManual", request.OperatingManual);
                    insertCmd.Parameters.AddWithValue("@WarrantyCertificateNo", warrantyCertificate);
                    await insertCmd.ExecuteNonQueryAsync();
                }

                const string bulkSql = @"
UPDATE dbo.receipts
SET BulkInst = @BulkInst
WHERE receipt_id = @ReceiptId";
                await using (var bulkCmd = new SqlCommand(bulkSql, con))
                {
                    bulkCmd.Parameters.AddWithValue("@BulkInst", request.BulkInst ? "Y" : "N");
                    bulkCmd.Parameters.AddWithValue("@ReceiptId", request.ReceiptId);
                    await bulkCmd.ExecuteNonQueryAsync();
                }

                return Ok(new { message = "Installation details saved successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error saving installation details.", error = ex.Message });
            }
        }

        /// <summary>Complete installation with PDF file checks and installation dispatch no/date.</summary>
        [HttpPost("receipt-entry/complete")]
        public async Task<IActionResult> CompleteReceiptEntry([FromBody] DmeReceiptCompleteRequestDto request)
        {
            if (request == null || request.UserId <= 0)
                return BadRequest(new { message = "Invalid user id." });
            if (request.PoId <= 0 || request.LocationId <= 0 || request.IssueId <= 0 || request.ReceiptId <= 0)
                return BadRequest(new { message = "PO, consignee, issue and receipt are required." });
            if (string.IsNullOrWhiteSpace(request.InstallDispatchNo) ||
                string.IsNullOrWhiteSpace(request.InstallDispatchDate))
                return BadRequest(new { message = "Please enter installation dispatch no and date." });
            if (!DateTime.TryParseExact(
                    request.InstallDispatchDate.Trim(),
                    "dd/MM/yyyy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime installDispatchDate))
                return BadRequest(new { message = "Invalid installation dispatch date. Use DD/MM/YYYY." });

            try
            {
                await using var con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                if (!await ReceiptBelongsToFacilityUserAsync(con, request.ReceiptId, request.UserId))
                    return NotFound(new { message = "Receipt not found for this facility user." });

                const string countSql = "SELECT COUNT(*) FROM dbo.receipt_item_details WHERE receipt_id = @ReceiptId";
                await using (var countCmd = new SqlCommand(countSql, con))
                {
                    countCmd.Parameters.AddWithValue("@ReceiptId", request.ReceiptId);
                    int count = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                    if (count == 0)
                        return BadRequest(new { message = "Please save installation details before completion." });
                }

                const string dispatchCountSql = "SELECT COUNT(*) FROM dbo.Issue_item_details WHERE Issue_id = @IssueId";
                int dispatchCount;
                await using (var dispatchCountCmd = new SqlCommand(dispatchCountSql, con))
                {
                    dispatchCountCmd.Parameters.AddWithValue("@IssueId", request.IssueId);
                    dispatchCount = Convert.ToInt32(await dispatchCountCmd.ExecuteScalarAsync());
                }

                int installedCount;
                await using (var installedCountCmd = new SqlCommand(countSql, con))
                {
                    installedCountCmd.Parameters.AddWithValue("@ReceiptId", request.ReceiptId);
                    installedCount = Convert.ToInt32(await installedCountCmd.ExecuteScalarAsync());
                }

                if (dispatchCount != installedCount)
                    return BadRequest(new { message = "Number of dispatched items and installed items do not match." });

                bool bulkInst = false;
                const string bulkFlagSql = "SELECT ISNULL(BulkInst, 'N') FROM dbo.receipts WHERE receipt_id = @ReceiptId";
                await using (var bulkFlagCmd = new SqlCommand(bulkFlagSql, con))
                {
                    bulkFlagCmd.Parameters.AddWithValue("@ReceiptId", request.ReceiptId);
                    object? bulkResult = await bulkFlagCmd.ExecuteScalarAsync();
                    bulkInst = bulkResult?.ToString()?.Equals("Y", StringComparison.OrdinalIgnoreCase) == true;
                }

                if (bulkInst)
                {
                    const string bulkFilesSql = @"
SELECT ISNULL(InstalationReportFile, '') AS InstalationReportFile,
       ISNULL(InstalationPhoto, '') AS InstalationPhoto,
       ISNULL(Challanfile, '') AS Challanfile,
       ISNULL(WarrantyCardFile, '') AS WarrantyCardFile
FROM dbo.receipts
WHERE receipt_id = @ReceiptId";
                    await using var bulkFilesCmd = new SqlCommand(bulkFilesSql, con);
                    bulkFilesCmd.Parameters.AddWithValue("@ReceiptId", request.ReceiptId);
                    await using var reader = await bulkFilesCmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                        return BadRequest(new { message = "Receipt not found." });
                    if (!HasInstallationFileValue(ReadStringColumn(reader, "InstalationReportFile")))
                        return BadRequest(new { message = "Please upload signed copy of combined receipt and installation file in PDF." });
                    if (!HasInstallationFileValue(ReadStringColumn(reader, "InstalationPhoto")))
                        return BadRequest(new { message = "Please upload installation photos in PDF." });
                    if (!HasInstallationFileValue(ReadStringColumn(reader, "Challanfile")))
                        return BadRequest(new { message = "Please upload challan and invoice copies in PDF." });
                    if (!HasInstallationFileValue(ReadStringColumn(reader, "WarrantyCardFile")))
                        return BadRequest(new { message = "Please upload warranty cards in PDF." });
                }
                else
                {
                    const string rowFilesSql = @"
SELECT InstalationReportFile, InstalationPhoto, Challanfile, WarrantyCardFile
FROM dbo.receipt_item_details
WHERE receipt_id = @ReceiptId";
                    await using var rowFilesCmd = new SqlCommand(rowFilesSql, con);
                    rowFilesCmd.Parameters.AddWithValue("@ReceiptId", request.ReceiptId);
                    await using var reader = await rowFilesCmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        if (!HasInstallationFileValue(ReadStringColumn(reader, "InstalationReportFile"))
                            || !HasInstallationFileValue(ReadStringColumn(reader, "InstalationPhoto"))
                            || !HasInstallationFileValue(ReadStringColumn(reader, "Challanfile"))
                            || !HasInstallationFileValue(ReadStringColumn(reader, "WarrantyCardFile")))
                        {
                            return BadRequest(new
                            {
                                message = "Please upload receipt, installation report, photos, challan and warranty card in PDF for each serial before completion.",
                            });
                        }
                    }
                }

                const string updateReceiptSql = @"
UPDATE dbo.receipts
SET IsSUPInstEntry = 'Y', status = 'C', entryDT = GETDATE(),
    dispatch_no = @DispatchNo, dispatch_date = @DispatchDate
WHERE receipt_id = @ReceiptId";
                await using (var recCmd = new SqlCommand(updateReceiptSql, con))
                {
                    recCmd.Parameters.AddWithValue("@ReceiptId", request.ReceiptId);
                    recCmd.Parameters.AddWithValue("@DispatchNo", request.InstallDispatchNo.Trim());
                    recCmd.Parameters.AddWithValue("@DispatchDate", installDispatchDate);
                    await recCmd.ExecuteNonQueryAsync();
                }

                const string updateDetailsSql = @"
UPDATE dbo.receipt_item_details
SET status = 'C', entryDT = GETDATE()
WHERE receipt_id = @ReceiptId";
                await using (var detCmd = new SqlCommand(updateDetailsSql, con))
                {
                    detCmd.Parameters.AddWithValue("@ReceiptId", request.ReceiptId);
                    await detCmd.ExecuteNonQueryAsync();
                }

                const string updateDispatchSql = @"
UPDATE dbo.SupplierDispatch
SET status = 'C'
WHERE Issue_id = @IssueId AND po_id = @PoId AND location_id = @LocId";
                await using (var dspCmd = new SqlCommand(updateDispatchSql, con))
                {
                    dspCmd.Parameters.AddWithValue("@IssueId", request.IssueId);
                    dspCmd.Parameters.AddWithValue("@PoId", request.PoId);
                    dspCmd.Parameters.AddWithValue("@LocId", request.LocationId);
                    await dspCmd.ExecuteNonQueryAsync();
                }

                return Ok(new { message = "Equipment installation completed successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error completing installation.", error = ex.Message });
            }
        }

        [HttpPost("receipt-entry/file")]
        public async Task<IActionResult> UploadReceiptInstallationFile(
            [FromForm] int userId,
            [FromForm] int receiptId,
            [FromForm] string fileType,
            [FromForm] int itemDetailId = 0,
            [FromForm] bool bulk = false,
            IFormFile? file = null)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });
            if (receiptId <= 0)
                return BadRequest(new { message = "Receipt id is required." });
            if (!bulk && itemDetailId <= 0)
                return BadRequest(new { message = "Item detail id is required." });
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Please select a file to upload." });
            if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Please upload pdf file only." });
            if (file.Length > 5_000_000)
                return BadRequest(new { message = "Your can't upload file more than 3mb." });

            string normalizedType = (fileType ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedType is not ("insreport" or "insphoto" or "waranty" or "chalan"))
                return BadRequest(new { message = "Invalid file type." });

            try
            {
                await using var con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                if (!await ReceiptBelongsToFacilityUserAsync(con, receiptId, userId))
                    return NotFound(new { message = "Receipt not found for this facility user." });

                byte[] fileBytes;
                await using (var memory = new MemoryStream())
                {
                    await file.CopyToAsync(memory);
                    fileBytes = memory.ToArray();
                }

                string columnName = normalizedType switch
                {
                    "insreport" => "InstalationReportFile",
                    "insphoto" => "InstalationPhoto",
                    "waranty" => "WarrantyCardFile",
                    _ => "Challanfile",
                };

                string fileToken = normalizedType switch
                {
                    "insreport" => bulk ? $"InstallationReport_{receiptId}" : $"InstallationReport_{itemDetailId}",
                    "insphoto" => bulk ? $"InstalPhoto__{receiptId}" : $"InstalPhoto_{itemDetailId}",
                    "waranty" => bulk ? $"WarrantyCard__{receiptId}" : $"WarrantyCard_{itemDetailId}",
                    _ => bulk ? $"Chalan_{receiptId}" : $"Chalan_{itemDetailId}",
                };

                int mongoLookupId = bulk ? receiptId : itemDetailId;
                await _mongoService.UpsertInstallationFile(mongoLookupId, normalizedType, fileBytes);

                if (bulk)
                {
                    string bulkSql = $@"
UPDATE dbo.receipts SET {columnName} = @FileToken WHERE receipt_id = @ReceiptId;
UPDATE dbo.receipt_item_details SET bulkinst = 'Y', {columnName} = @FileToken, ISmongo = 'Y' WHERE receipt_id = @ReceiptId;";
                    await using var bulkCmd = new SqlCommand(bulkSql, con);
                    bulkCmd.Parameters.AddWithValue("@FileToken", fileToken);
                    bulkCmd.Parameters.AddWithValue("@ReceiptId", receiptId);
                    await bulkCmd.ExecuteNonQueryAsync();
                }
                else
                {
                    string rowSql = $@"
UPDATE dbo.receipt_item_details
SET {columnName} = @FileToken, ISmongo = 'Y'
WHERE item_detail_id = @ItemDetailId AND receipt_id = @ReceiptId";
                    await using var rowCmd = new SqlCommand(rowSql, con);
                    rowCmd.Parameters.AddWithValue("@FileToken", fileToken);
                    rowCmd.Parameters.AddWithValue("@ItemDetailId", itemDetailId);
                    rowCmd.Parameters.AddWithValue("@ReceiptId", receiptId);
                    int affected = await rowCmd.ExecuteNonQueryAsync();
                    if (affected == 0)
                        return NotFound(new { message = "Receipt item not found." });
                }

                return Ok(new { message = "File uploaded successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error uploading installation file.", error = ex.Message });
            }
        }

        /// <summary>Facility_InstallationReportDME.aspx — readonly installation lines.</summary>
        [HttpGet("installation-report")]
        public async Task<IActionResult> GetInstallationReport(
            [FromQuery] int userId,
            [FromQuery] int receiptId)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });
            if (receiptId <= 0)
                return BadRequest(new { message = "Receipt id is required." });

            try
            {
                await using var con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                if (!await ReceiptBelongsToFacilityUserAsync(con, receiptId, userId))
                    return NotFound(new { message = "Receipt not found for this facility user." });

                const string headerSql = @"
SELECT receipt_id,
       CONVERT(VARCHAR, recieved_date, 103) AS received_date,
       ISNULL(BulkInst, 'N') AS BulkInst,
       ISNULL(InstalationReportFile, 'N') AS InstalationReportFile,
       ISNULL(InstalationPhoto, 'N') AS InstalationPhoto,
       ISNULL(Challanfile, 'N') AS Challanfile,
       ISNULL(WarrantyCardFile, 'N') AS WarrantyCardFile
FROM dbo.receipts
WHERE receipt_id = @ReceiptId";

                const string rowsSql = @"
SELECT ri.item_detail_id,
       ri.make_no,
       CONVERT(VARCHAR, ri.installation_date, 103) AS installation_date,
       CONVERT(VARCHAR, ri.warenty_from, 103) AS warenty_from,
       CONVERT(VARCHAR, ri.warenty_to, 103) AS warenty_to,
       ri.received_qty,
       ri.warranty_certificate_no,
       ri.installation_location,
       ISNULL(ri.ISmongo, 'N') AS ISmongo,
       ISNULL(ri.InstalationReportFile, '') AS InstalationReportFile,
       ISNULL(ri.InstalationPhoto, '') AS InstalationPhoto,
       ISNULL(ri.Challanfile, '') AS Challanfile,
       ISNULL(ri.WarrantyCardFile, '') AS WarrantyCardFile
FROM dbo.receipt_item_details ri
WHERE ri.receipt_id = @ReceiptId
ORDER BY ri.item_detail_id";

                var page = new DmeInstallationReportPageDto { ReceiptId = receiptId };

                await using (var headerCmd = new SqlCommand(headerSql, con))
                {
                    headerCmd.Parameters.AddWithValue("@ReceiptId", receiptId);
                    await using var reader = await headerCmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                        return NotFound(new { message = "Receipt not found." });

                    page.ReceivedDate = ReadStringColumn(reader, "received_date");
                    page.BulkInst = ReadStringColumn(reader, "BulkInst").Equals("Y", StringComparison.OrdinalIgnoreCase);
                    page.HasBulkInstallationReport = HasInstallationFileValue(
                        ReadStringColumn(reader, "InstalationReportFile"));
                    page.HasBulkInstallationPhoto = HasInstallationFileValue(
                        ReadStringColumn(reader, "InstalationPhoto"));
                    page.HasBulkWarrantyCard = HasInstallationFileValue(
                        ReadStringColumn(reader, "WarrantyCardFile"));
                    page.HasBulkChallan = HasInstallationFileValue(
                        ReadStringColumn(reader, "Challanfile"));
                }

                int slNo = 0;
                await using (var rowsCmd = new SqlCommand(rowsSql, con))
                {
                    rowsCmd.Parameters.AddWithValue("@ReceiptId", receiptId);
                    await using var reader = await rowsCmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        slNo++;
                        bool isMongo = ReadStringColumn(reader, "ISmongo")
                            .Equals("Y", StringComparison.OrdinalIgnoreCase);

                        page.Rows.Add(new DmeInstallationReportRowDto
                        {
                            SlNo = slNo,
                            ItemDetailId = ReadIntColumn(reader, "item_detail_id"),
                            SerialNo = ReadStringColumn(reader, "make_no"),
                            InstallationDate = ReadStringColumn(reader, "installation_date"),
                            WarrantyFrom = ReadStringColumn(reader, "warenty_from"),
                            WarrantyTo = ReadStringColumn(reader, "warenty_to"),
                            ReceivedQty = ReadDecimalColumn(reader, "received_qty"),
                            WarrantyCardNo = ReadStringColumn(reader, "warranty_certificate_no"),
                            InstallationLocation = ReadStringColumn(reader, "installation_location"),
                            IsMongo = isMongo,
                            HasInstallationReport = HasInstallationFileValue(
                                ReadStringColumn(reader, "InstalationReportFile")),
                            HasInstallationPhoto = HasInstallationFileValue(
                                ReadStringColumn(reader, "InstalationPhoto")),
                            HasWarrantyCard = HasInstallationFileValue(
                                ReadStringColumn(reader, "WarrantyCardFile")),
                            HasChallan = HasInstallationFileValue(
                                ReadStringColumn(reader, "Challanfile")),
                        });
                    }
                }

                return Ok(page);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading installation report.", error = ex.Message });
            }
        }

        /// <summary>Facility_InstallationReportDME.aspx — download installation attachment.</summary>
        [HttpGet("installation-report/file")]
        public async Task<IActionResult> DownloadInstallationFile(
            [FromQuery] int userId,
            [FromQuery] int receiptId,
            [FromQuery] string fileType,
            [FromQuery] int itemDetailId = 0,
            [FromQuery] bool bulk = false)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });
            if (receiptId <= 0)
                return BadRequest(new { message = "Receipt id is required." });
            if (!bulk && itemDetailId <= 0)
                return BadRequest(new { message = "Item detail id is required." });

            string normalizedType = (fileType ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedType is not ("insreport" or "insphoto" or "waranty" or "chalan"))
                return BadRequest(new { message = "Invalid file type." });

            try
            {
                await using var con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                if (!await ReceiptBelongsToFacilityUserAsync(con, receiptId, userId))
                    return NotFound(new { message = "Receipt not found for this facility user." });

                string columnName = normalizedType switch
                {
                    "insreport" => "InstalationReportFile",
                    "insphoto" => "InstalationPhoto",
                    "waranty" => "WarrantyCardFile",
                    _ => "Challanfile",
                };

                string? fileRef;
                bool isMongo;
                string? photoExt = null;
                int mongoLookupId;

                if (bulk)
                {
                    const string bulkSql = @"
SELECT ISNULL(InstalationReportFile, 'N') AS InstalationReportFile,
       ISNULL(InstalationPhoto, 'N') AS InstalationPhoto,
       ISNULL(Challanfile, 'N') AS Challanfile,
       ISNULL(WarrantyCardFile, 'N') AS WarrantyCardFile
FROM receipts
WHERE receipt_id = @ReceiptId";

                    await using var bulkCmd = new SqlCommand(bulkSql, con);
                    bulkCmd.Parameters.AddWithValue("@ReceiptId", receiptId);
                    await using var reader = await bulkCmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                        return NotFound(new { message = "Receipt not found." });

                    fileRef = ReadStringColumn(reader, columnName);
                    isMongo = true;
                    mongoLookupId = receiptId;
                }
                else
                {
                    string itemSql = $@"
SELECT ISNULL({columnName}, '') AS FileRef,
       ISNULL(ISmongo, 'N') AS ISmongo,
       ISNULL(InstalationPhoto, '') AS InstalationPhoto
FROM receipt_item_details
WHERE item_detail_id = @ItemDetailId AND receipt_id = @ReceiptId";

                    await using var itemCmd = new SqlCommand(itemSql, con);
                    itemCmd.Parameters.AddWithValue("@ItemDetailId", itemDetailId);
                    itemCmd.Parameters.AddWithValue("@ReceiptId", receiptId);
                    await using var reader = await itemCmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                        return NotFound(new { message = "Receipt item not found." });

                    fileRef = ReadStringColumn(reader, "FileRef");
                    isMongo = ReadStringColumn(reader, "ISmongo")
                        .Equals("Y", StringComparison.OrdinalIgnoreCase);
                    photoExt = Path.GetExtension(ReadStringColumn(reader, "InstalationPhoto"));
                    mongoLookupId = itemDetailId;
                }

                if (!HasInstallationFileValue(fileRef))
                    return NotFound(new { message = "File not found." });

                byte[]? fileBytes;
                string contentType;
                string downloadName;

                if (isMongo)
                {
                    ReceiptItemFiles? mongoFile = await _mongoService.GetFile(mongoLookupId);
                    if (mongoFile == null)
                        return NotFound(new { message = "File not found." });

                    fileBytes = normalizedType switch
                    {
                        "chalan" => mongoFile.FileChalan,
                        "waranty" => mongoFile.FileWarrantyCard,
                        "insphoto" => mongoFile.FilePhoto,
                        _ => mongoFile.File,
                    };

                    string extension = normalizedType == "insphoto" && !string.IsNullOrWhiteSpace(photoExt)
                        ? photoExt
                        : ".pdf";
                    contentType = GetInstallationContentType(extension);
                    downloadName = $"{fileRef}{extension}";
                }
                else
                {
                    string physicalPath = ResolveLegacyVirtualPath(fileRef!);
                    if (!System.IO.File.Exists(physicalPath))
                        return NotFound(new { message = "File not found on server." });

                    fileBytes = await System.IO.File.ReadAllBytesAsync(physicalPath);
                    contentType = GetInstallationContentType(Path.GetExtension(physicalPath));
                    downloadName = Path.GetFileName(physicalPath);
                }

                if (fileBytes == null || fileBytes.Length == 0)
                    return NotFound(new { message = "File not found." });

                return File(fileBytes, contentType, downloadName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error downloading installation file.", error = ex.Message });
            }
        }

        private static async Task<bool> LocationBelongsToUserAsync(SqlConnection con, int locId, int userId)
        {
            const string sql = @"
SELECT 1
FROM dbo.maslocations
WHERE location_id = @LocId AND (user_id = @UserId OR location_id IN (SELECT location_id FROM dbo.users WHERE user_id = @UserId) OR location_id = @UserId)";
            await using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@LocId", locId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            object? result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value;
        }

        private static async Task<bool> ReceiptBelongsToFacilityUserAsync(
            SqlConnection con, int receiptId, int userId)
        {
            const string sql = @"
SELECT 1
FROM dbo.receipts r
INNER JOIN dbo.maslocations ml ON ml.location_id = r.location_id
WHERE r.receipt_id = @ReceiptId AND (ml.user_id = @UserId OR ml.location_id IN (SELECT location_id FROM dbo.users WHERE user_id = @UserId) OR ml.location_id = @UserId)";
            await using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@ReceiptId", receiptId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            object? result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value;
        }

        private static async Task<bool> PoBelongsToFacilityUserAsync(
            SqlConnection con, int poId, int userId)
        {
            const string sql = @"
SELECT TOP 1 1
FROM dbo.po_items pi
INNER JOIN dbo.maslocations l ON l.location_id = pi.consignee_id
WHERE pi.po_id = @PoId AND (l.user_id = @UserId OR l.location_id IN (SELECT location_id FROM dbo.users WHERE user_id = @UserId) OR l.location_id = @UserId)";
            await using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@PoId", poId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            object? result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value;
        }

        private static async Task<DmeReceiptEntryPageDto> LoadDmeReceiptEntryPageAsync(
            SqlConnection con,
            int userId,
            int poId,
            int locId,
            int issueId)
        {
            const string headerSql = @"
SELECT TOP 1
       d.po_id,
       d.location_id,
       d.issue_id,
       mi.categoryid,
       mi.item_code_as_per_tender AS item_code,
       mi.item_name,
       CAST(poi.percentage AS VARCHAR(10)) + ' %' AS taxPercent,
       po.outward_no + '-' + po.po_no AS po_no,
       CASE WHEN po.soissueDT IS NOT NULL THEN CONVERT(VARCHAR, po.soissueDT, 103)
            ELSE CONVERT(VARCHAR, po.po_date, 103) END AS po_date,
       t.tender_no,
       ml.location_name,
       ISNULL(ci.model, '') AS model,
       ISNULL(ci.make, '') AS make,
       poi.basicrate,
       poi.totalbasicPrice,
       poi.totalprice,
       (SELECT SUM(quantity) FROM dbo.po_items WHERE po_id = @PoId) AS poqty_all,
       poi.quantity AS poqty_consignee,
       ISNULL(sup.Supplyqty, 0) AS dispatched_qty,
       poi.quantity - ISNULL(sup.Supplyqty, 0) AS bal_qty,
       CAST(pt.tranche_days AS VARCHAR(20)) AS tranche_days,
       ISNULL(t.warranty_year, 1) AS warranty_year,
       CASE
           WHEN t.cancellationdays IS NULL THEN ''
           ELSE CAST(t.cancellationdays AS VARCHAR(20))
       END AS cancellation_days,
       CASE
           WHEN po.extendeddate > po.po_date THEN CONVERT(VARCHAR, po.extendeddate, 103)
           WHEN t.cancellationdays IS NOT NULL THEN CONVERT(
               VARCHAR,
               DATEADD(DAY, t.cancellationdays, CASE WHEN po.soissueDT IS NOT NULL THEN po.soissueDT ELSE po.po_date END),
               103)
           ELSE '-'
       END AS ldate,
       d.challan_no,
       CONVERT(VARCHAR, d.challan_date, 103) AS challan_date,
       d.invoice_no,
       CONVERT(VARCHAR, d.invoice_date, 103) AS invoice_date,
       d.dispatch_no,
       CONVERT(VARCHAR, d.dispatch_date, 103) AS dispatch_date,
       d.remarks AS supplier_remarks
FROM dbo.SupplierDispatch d
INNER JOIN dbo.purchase_order po ON po.po_id = d.po_id
INNER JOIN dbo.po_items poi ON poi.po_id = d.po_id AND poi.consignee_id = d.location_id
LEFT JOIN dbo.po_tranche pt ON pt.po_id = po.po_id
LEFT JOIN dbo.contract_items ci ON ci.contract_item_id = poi.contract_item_id
INNER JOIN dbo.masitems mi ON mi.item_id = poi.item_id
LEFT OUTER JOIN dbo.tenders t ON t.tender_id = po.tender_id
INNER JOIN dbo.maslocations ml ON ml.location_id = d.location_id
LEFT OUTER JOIN (
    SELECT sd.issue_id, SUM(i.Supplyqty) AS Supplyqty
    FROM dbo.SupplierDispatch sd
    INNER JOIN dbo.Issue_item_details i ON i.Issue_id = sd.Issue_id
    GROUP BY sd.issue_id
) sup ON sup.issue_id = d.issue_id
WHERE d.po_id = @PoId AND d.location_id = @LocId AND d.issue_id = @IssueId
  AND (ml.user_id = @UserId OR ml.location_id IN (SELECT location_id FROM dbo.users WHERE user_id = @UserId) OR ml.location_id = @UserId)";

            DmeReceiptEntryPageDto? page = null;
            await using (var cmd = new SqlCommand(headerSql, con))
            {
                cmd.Parameters.AddWithValue("@PoId", poId);
                cmd.Parameters.AddWithValue("@LocId", locId);
                cmd.Parameters.AddWithValue("@IssueId", issueId);
                cmd.Parameters.AddWithValue("@UserId", userId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    page = new DmeReceiptEntryPageDto
                    {
                        PoId = ReadIntColumn(reader, "po_id"),
                        LocationId = ReadIntColumn(reader, "location_id"),
                        IssueId = ReadIntColumn(reader, "issue_id"),
                        CategoryId = ReadIntColumn(reader, "categoryid"),
                        ItemCode = ReadStringColumn(reader, "item_code"),
                        ItemName = ReadStringColumn(reader, "item_name"),
                        TaxPercent = ReadStringColumn(reader, "taxPercent"),
                        PoNo = ReadStringColumn(reader, "po_no"),
                        PoDate = ReadStringColumn(reader, "po_date"),
                        TenderNo = ReadStringColumn(reader, "tender_no"),
                        ConsigneeName = ReadStringColumn(reader, "location_name"),
                        ModelNo = ReadStringColumn(reader, "model"),
                        Make = ReadStringColumn(reader, "make"),
                        BasicRate = ReadDecimalColumn(reader, "basicrate"),
                        TotalNetPoValue = ReadDecimalColumn(reader, "totalbasicPrice"),
                        TotalGrossPoValue = ReadDecimalColumn(reader, "totalprice"),
                        PoQtyAllConsignees = ReadDecimalColumn(reader, "poqty_all"),
                        PoQtyConsignee = ReadDecimalColumn(reader, "poqty_consignee"),
                        DispatchedQty = ReadDecimalColumn(reader, "dispatched_qty"),
                        BalanceQty = ReadDecimalColumn(reader, "bal_qty"),
                        SupplyDays = ReadStringColumn(reader, "tranche_days"),
                        WarrantyYears = ReadIntColumn(reader, "warranty_year"),
                        CancellationDays = ReadStringColumn(reader, "cancellation_days"),
                        LastReceiptDate = ReadStringColumn(reader, "ldate"),
                        ChallanNo = ReadStringColumn(reader, "challan_no"),
                        ChallanDate = ReadStringColumn(reader, "challan_date"),
                        InvoiceNo = ReadStringColumn(reader, "invoice_no"),
                        InvoiceDate = ReadStringColumn(reader, "invoice_date"),
                        DispatchNo = ReadStringColumn(reader, "dispatch_no"),
                        DispatchDate = ReadStringColumn(reader, "dispatch_date"),
                        SupplierRemarks = ReadStringColumn(reader, "supplier_remarks"),
                    };
                }
            }

            if (page == null)
                throw new InvalidOperationException("Receipt issue not found for this facility.");

            const string receiptSql = @"
SELECT TOP 1 receipt_id,
       CONVERT(VARCHAR, recieved_date, 103) AS recieved_date,
       receipt_no,
       receipt_qty,
       remarks,
       ISNULL(dispatch_no, '') AS install_dispatch_no,
       CASE WHEN dispatch_date IS NULL THEN '' ELSE CONVERT(VARCHAR, dispatch_date, 103) END AS install_dispatch_date,
       ISNULL(BulkInst, 'N') AS BulkInst,
       ISNULL(InstalationReportFile, '') AS InstalationReportFile,
       ISNULL(InstalationPhoto, '') AS InstalationPhoto,
       ISNULL(WarrantyCardFile, '') AS WarrantyCardFile,
       ISNULL(Challanfile, '') AS Challanfile
FROM dbo.receipts
WHERE issue_id = @IssueId AND po_id = @PoId AND location_id = @LocId
ORDER BY receipt_id DESC";
            await using (var receiptCmd = new SqlCommand(receiptSql, con))
            {
                receiptCmd.Parameters.AddWithValue("@IssueId", issueId);
                receiptCmd.Parameters.AddWithValue("@PoId", poId);
                receiptCmd.Parameters.AddWithValue("@LocId", locId);
                await using var reader = await receiptCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    page.ReceiptId = ReadIntColumn(reader, "receipt_id");
                    page.ReceivedDate = ReadStringColumn(reader, "recieved_date");
                    page.ReceiptNo = ReadStringColumn(reader, "receipt_no");
                    page.ReceiptQty = ReadStringColumn(reader, "receipt_qty");
                    page.ReceiptRemarks = ReadStringColumn(reader, "remarks");
                    page.InstallDispatchNo = ReadStringColumn(reader, "install_dispatch_no");
                    page.InstallDispatchDate = ReadStringColumn(reader, "install_dispatch_date");
                    page.BulkInst = ReadStringColumn(reader, "BulkInst")
                        .Equals("Y", StringComparison.OrdinalIgnoreCase);
                    page.HasBulkInstallationReport = HasInstallationFileValue(
                        ReadStringColumn(reader, "InstalationReportFile"));
                    page.HasBulkInstallationPhoto = HasInstallationFileValue(
                        ReadStringColumn(reader, "InstalationPhoto"));
                    page.HasBulkWarrantyCard = HasInstallationFileValue(
                        ReadStringColumn(reader, "WarrantyCardFile"));
                    page.HasBulkChallan = HasInstallationFileValue(
                        ReadStringColumn(reader, "Challanfile"));
                }
            }

            const string issueDetailsSql = @"
SELECT issue_detail_id, make_no, warranty_certificate_no, Supplyqty
FROM dbo.Issue_item_details
WHERE Issue_id = @IssueId
ORDER BY issue_detail_id";
            await using (var itemCmd = new SqlCommand(issueDetailsSql, con))
            {
                itemCmd.Parameters.AddWithValue("@IssueId", issueId);
                await using var reader = await itemCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    page.IssueDetailOptions.Add(new DmeReceiptIssueDetailOptionDto
                    {
                        IssueDetailId = ReadIntColumn(reader, "issue_detail_id"),
                        SerialNo = ReadStringColumn(reader, "make_no"),
                        WarrantyCertificateNo = ReadStringColumn(reader, "warranty_certificate_no", "Warranty_CertificateNo"),
                        DispatchedQty = ReadDecimalColumn(reader, "Supplyqty"),
                    });
                }
            }

            if (page.ReceiptId > 0)
            {
                const string linesSql = @"
SELECT item_detail_id, issue_detail_id, make_no, warranty_certificate_no, warranty_card_no, received_qty,
       CONVERT(VARCHAR, installation_date, 103) AS installation_date,
       CONVERT(VARCHAR, warenty_from, 103) AS warenty_from,
       CONVERT(VARCHAR, warenty_to, 103) AS warenty_to,
       installation_by, installation_location, cgmsc_log_printed, warranty_validity, manual_provided,
       opening_manual_provided, calibration_certificate_prov, org_warranty_card_rec, other_statutory,
       inticated_po_are_received,
       ISNULL(InstalationReportFile, '') AS InstalationReportFile,
       ISNULL(InstalationPhoto, '') AS InstalationPhoto,
       ISNULL(WarrantyCardFile, '') AS WarrantyCardFile,
       ISNULL(Challanfile, '') AS Challanfile
FROM dbo.receipt_item_details
WHERE receipt_id = @ReceiptId
ORDER BY item_detail_id";
                await using var linesCmd = new SqlCommand(linesSql, con);
                linesCmd.Parameters.AddWithValue("@ReceiptId", page.ReceiptId);
                await using var reader = await linesCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    page.InstallationLines.Add(new DmeReceiptInstallationLineDto
                    {
                        ItemDetailId = ReadIntColumn(reader, "item_detail_id"),
                        IssueDetailId = ReadIntColumn(reader, "issue_detail_id"),
                        SerialNo = ReadStringColumn(reader, "make_no"),
                        WarrantyCertificateNo = ReadStringColumn(reader, "warranty_certificate_no"),
                        WarrantyCardNo = ReadStringColumn(reader, "warranty_card_no"),
                        ReceivedQty = ReadDecimalColumn(reader, "received_qty"),
                        InstallationDate = ReadStringColumn(reader, "installation_date"),
                        WarrantyFromDate = ReadStringColumn(reader, "warenty_from"),
                        WarrantyToDate = ReadStringColumn(reader, "warenty_to"),
                        InstallationBy = ReadStringColumn(reader, "installation_by"),
                        InstallationLocation = ReadStringColumn(reader, "installation_location"),
                        CgmscLogoPrinted = ReadStringColumn(reader, "cgmsc_log_printed"),
                        WarrantyValidity = ReadStringColumn(reader, "warranty_validity"),
                        ServiceManual = ReadStringColumn(reader, "manual_provided"),
                        OperatingManual = ReadStringColumn(reader, "opening_manual_provided"),
                        CalibrationCertificate = ReadStringColumn(reader, "calibration_certificate_prov"),
                        WarrantyCard = ReadStringColumn(reader, "org_warranty_card_rec"),
                        OtherStatutory = ReadStringColumn(reader, "other_statutory"),
                        PoDocuments = ReadStringColumn(reader, "inticated_po_are_received"),
                        HasInstallationReport = HasInstallationFileValue(
                            ReadStringColumn(reader, "InstalationReportFile")),
                        HasInstallationPhoto = HasInstallationFileValue(
                            ReadStringColumn(reader, "InstalationPhoto")),
                        HasWarrantyCard = HasInstallationFileValue(
                            ReadStringColumn(reader, "WarrantyCardFile")),
                        HasChallan = HasInstallationFileValue(
                            ReadStringColumn(reader, "Challanfile")),
                    });
                }
            }

            return page;
        }

        private static bool HasInstallationFileValue(string? value) =>
            !string.IsNullOrWhiteSpace(value) && !value.Trim().Equals("N", StringComparison.OrdinalIgnoreCase);

        private string ResolveLegacyVirtualPath(string virtualPath)
        {
            string relative = virtualPath.Trim();
            if (relative.StartsWith("~/", StringComparison.Ordinal))
                relative = relative[2..];
            else if (relative.StartsWith('/'))
                relative = relative.TrimStart('/');

            return Path.GetFullPath(Path.Combine(
                _emsRoleRoot,
                relative.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string GetInstallationContentType(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".pdf" => "application/pdf",
                _ => "application/octet-stream",
            };
        }

        private static int ReadIntColumn(SqlDataReader reader, params string[] columnNames)
        {
            foreach (string columnName in columnNames)
            {
                try
                {
                    int ordinal = reader.GetOrdinal(columnName);
                    if (!reader.IsDBNull(ordinal))
                        return Convert.ToInt32(reader.GetValue(ordinal));
                }
                catch (IndexOutOfRangeException)
                {
                }
            }

            return 0;
        }

        private static string ReadStringColumn(SqlDataReader reader, params string[] columnNames)
        {
            foreach (string columnName in columnNames)
            {
                try
                {
                    int ordinal = reader.GetOrdinal(columnName);
                    if (!reader.IsDBNull(ordinal))
                        return reader.GetValue(ordinal)?.ToString() ?? string.Empty;
                }
                catch (IndexOutOfRangeException)
                {
                }
            }

            return string.Empty;
        }

        private static decimal ReadDecimalColumn(SqlDataReader reader, params string[] columnNames)
        {
            foreach (string columnName in columnNames)
            {
                try
                {
                    int ordinal = reader.GetOrdinal(columnName);
                    if (!reader.IsDBNull(ordinal))
                        return Convert.ToDecimal(reader.GetValue(ordinal));
                }
                catch (IndexOutOfRangeException)
                {
                }
            }

            return 0;
        }

        private static bool TryParseLegacyDate(string? value, out DateTime result)
        {
            result = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(value) || value.Trim() == "-")
                return false;

            var formats = new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" };
            return DateTime.TryParseExact(
                value.Trim(),
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result);
        }

        private static async Task<List<PoReceiptBatchDto>> LoadReceiptBatchesAsync(SqlConnection conn, int poId, int locationId)
        {
            const string sql = @"
SELECT d.Issue_id,
       CONVERT(VARCHAR, Tentative_Sdate, 103) AS Tentative_Sdate,
       CASE WHEN re.receipt_no IS NOT NULL THEN re.receipt_no ELSE '' END AS receipt_no,
       CASE
           WHEN re.status IS NOT NULL THEN CASE WHEN re.status = 'C' THEN 'Installation Completed' ELSE 'Installation Pending' END
           ELSE CASE WHEN d.status = 'C' THEN 'Receipt' ELSE 'Not Supplied' END
       END AS SupplyStatus,
       CONVERT(VARCHAR, dispatch_date, 103) AS dispatch_date,
       dispatch_no,
       SUM(i.Supplyqty) AS quantity,
       d.po_id,
       d.location_id,
       CASE WHEN re.recieved_date IS NULL THEN 'Not Receipt' ELSE CONVERT(VARCHAR, re.recieved_date, 103) END AS recieved_date,
       re.receipt_id
FROM dbo.SupplierDispatch d
INNER JOIN dbo.Issue_item_details i ON d.Issue_id = i.Issue_id
LEFT OUTER JOIN (
    SELECT r.issue_id, r.recieved_date, r.po_id, r.location_id, r.status, r.receipt_no, r.receipt_id
    FROM dbo.receipts r
    WHERE r.status = 'C'
) re ON re.issue_id = d.Issue_id AND re.po_id = d.po_id AND re.location_id = d.location_id
WHERE d.po_id = @PoId AND d.location_id = @LocationId
GROUP BY re.receipt_id, d.Issue_id, dispatch_date, Tentative_Sdate, d.status, dispatch_no,
         d.po_id, d.location_id, re.recieved_date, re.status, re.receipt_no";

            var list = new List<PoReceiptBatchDto>();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@PoId", poId);
            cmd.Parameters.AddWithValue("@LocationId", locationId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new PoReceiptBatchDto
                {
                    IssueId = reader["Issue_id"]?.ToString() ?? string.Empty,
                    TentativeSupplyDate = reader["Tentative_Sdate"]?.ToString() ?? string.Empty,
                    ReceiptNo = reader["receipt_no"]?.ToString() ?? string.Empty,
                    SupplyStatus = reader["SupplyStatus"]?.ToString() ?? string.Empty,
                    DispatchDate = reader["dispatch_date"]?.ToString() ?? string.Empty,
                    DispatchNo = reader["dispatch_no"]?.ToString() ?? string.Empty,
                    SuppliedQty = reader["quantity"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["quantity"]),
                    PoId = Convert.ToInt32(reader["po_id"]),
                    LocationId = Convert.ToInt32(reader["location_id"]),
                    ReceiptDate = reader["recieved_date"]?.ToString() ?? string.Empty,
                    ReceiptId = reader["receipt_id"] == DBNull.Value ? null : Convert.ToInt32(reader["receipt_id"]),
                });
            }

            return list;
        }
    }
}
