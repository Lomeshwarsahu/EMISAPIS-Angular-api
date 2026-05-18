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
    }
}
