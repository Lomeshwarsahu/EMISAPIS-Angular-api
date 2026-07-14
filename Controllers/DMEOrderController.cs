using EMISAPIS.DTOS;
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

        public DMEOrderController(IConfiguration configuration, IWebHostEnvironment env)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection missing.");

            _emsRoleRoot = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "EMSRole"));
            var configured = configuration["FileStorage:IndentUploadsPath"];
            _indentUploadsRoot = string.IsNullOrWhiteSpace(configured)
                ? Path.GetFullPath(Path.Combine(_emsRoleRoot, "Uploads"))
                : Path.GetFullPath(configured);
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
       R.item_name AS ITEM_NAME,
       R.ITEM_CODE_AS_PER_TENDER AS CODE,
       E.YEAR AS POYear,
       iy.year AS indentYear,
       CONVERT(VARCHAR, id.consolidated_date, 103) AS IndentDT,
       im.indent_quantity,
       pi.quantity AS POQTY,
       ltp.filePathAccessories,
       ltp.filePathReagent,
       ltp.tender_item_id
FROM dbo.PURCHASE_ORDER a
INNER JOIN dbo.po_items pi ON pi.po_id = a.po_id
INNER JOIN dbo.indent_consolidation id ON id.indent_consolidation_id = pi.INDENT_CONSOLIDATION_ID
INNER JOIN dbo.indent i ON i.indent_id = pi.indent_id
INNER JOIN dbo.indent_items im ON im.indent_item_id = pi.indent_item_id AND im.indent_id = pi.indent_id
INNER JOIN dbo.indent_cons_items ci ON ci.indent_cons_items_id = i.indent_cons_items_id AND ci.indent_consolidated_id = id.indent_consolidation_id
INNER JOIN dbo.maslocations l ON l.location_id = pi.consignee_id
INNER JOIN dbo.MASSUPPLIERS b ON a.SUPPLIER_ID = b.SUPPLIER_ID
INNER JOIN dbo.MASITEMS R ON R.ITEM_ID = a.ITEM_ID
INNER JOIN dbo.TENDERS c ON c.TENDER_ID = a.TENDER_ID
INNER JOIN dbo.tender_items ti ON ti.tender_id = c.tender_id AND ti.item_id = pi.item_id
INNER JOIN dbo.live_tender_price ltp ON ltp.supplier_id = a.supplier_id AND ltp.tender_item_id = ti.tender_item_id
INNER JOIN dbo.MAS_FINANCIAL_YEAR E ON E.FINANCIAL_YEAR_ID = a.FINANCIAL_YEAR_ID
INNER JOIN dbo.mas_financial_year iy ON iy.financial_year_id = id.financial_year_id
WHERE a.status NOT IN ('Incomplete', 'Waiting For Approval', 'Cancelled')
  AND l.user_id = @UserId
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
       c.item_name, c.item_code_as_per_tender AS item_code, s.name AS supplier_name,
       x.single_unit_price * a.quantity AS Total_Price
FROM dbo.po_items a
INNER JOIN dbo.MASITEMS R ON R.ITEM_ID = a.item_id
INNER JOIN dbo.purchase_order b ON a.po_id = b.po_id
INNER JOIN dbo.MASSUPPLIERS s ON s.SUPPLIER_ID = b.supplier_id
LEFT OUTER JOIN (
    SELECT F.SUPPLIER_ID, F.TENDER_ID, D.ITEM_ID, D.single_unit_price
    FROM dbo.AWARD_OF_CONTRACT F
    INNER JOIN dbo.CONTRACT_ITEMS D ON D.AWARD_OF_CONTRACT_ID = F.AWARD_OF_CONTRACT_ID
) x ON x.TENDER_ID = b.TENDER_ID AND b.SUPPLIER_ID = x.SUPPLIER_ID AND a.ITEM_ID = x.ITEM_ID
LEFT OUTER JOIN dbo.maslocations c1 ON c1.location_id = a.consignee_id
LEFT OUTER JOIN dbo.masitems c ON a.item_id = c.item_id
WHERE c1.user_id = @UserId
  AND b.status NOT IN ('Incomplete', 'Waiting For Approval', 'Cancelled')
  AND (@AuthorityId IS NULL OR @AuthorityId = '' OR c1.authority = @AuthorityId)
  AND (@FinancialYearId = 0 OR b.financial_year_id = @FinancialYearId)
  AND (@ItemCode IS NULL OR @ItemCode = '' OR @ItemCode = '0' OR R.item_code_as_per_tender = @ItemCode)
ORDER BY b.po_date";

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
