using EMISAPIS.DTOS;
using EMISAPIS.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EMISAPIS.Controllers
{
    /// <summary>Master/report_specification.aspx — CME EEL specification PDF upload.</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ReportSpecificationController : ControllerBase
    {
        private const string UploadFolderName = "Specification";
        private const long MaxPdfBytes = 2_000_000;

        private readonly string _connectionString;
        private readonly string _specificationRoot;
        private ReportSpecificationSchema? _schema;
        private readonly SemaphoreSlim _schemaLock = new(1, 1);

        public ReportSpecificationController(IConfiguration configuration, IWebHostEnvironment env)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection missing.");

            var configured = configuration["FileStorage:SpecificationPath"];
            _specificationRoot = string.IsNullOrWhiteSpace(configured)
                ? Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "EMSRole", UploadFolderName))
                : Path.GetFullPath(configured);

            Directory.CreateDirectory(_specificationRoot);
        }

        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            var list = new List<EquipmentCategoryDto> { new() { EqpCatId = 0, EqpCatName = "--SelectAll--" } };

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                var schema = await GetSchemaAsync(conn);

                if (!schema.HasCategoryTable)
                    return Ok(list);

                var sql = $@"
SELECT {schema.CategoryIdColumn} AS cat_id, {schema.CategoryNameColumn} AS cat_name
FROM dbo.{schema.CategoryTable}
ORDER BY {schema.CategoryNameColumn}";

                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new EquipmentCategoryDto
                    {
                        EqpCatId = Convert.ToInt32(reader["cat_id"]),
                        EqpCatName = reader["cat_name"]?.ToString() ?? string.Empty,
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading categories.", detail = ex.Message });
            }
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] int categoryId = 0)
        {
            try
            {
                var summary = await QuerySummaryAsync(categoryId);
                return Ok(summary);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading summary.", detail = ex.Message });
            }
        }

        [HttpGet("search-options")]
        public async Task<IActionResult> GetSearchOptions([FromQuery] int categoryId = 0)
        {
            try
            {
                var items = await QueryItemsAsync(categoryId, null, singleItemId: null);
                var options = items.Select(i => new EquipmentSearchOptionDto
                {
                    ItemId = i.ItemId,
                    DisplayText = $"{i.ItemName} ({i.ItemCode})",
                }).ToList();

                return Ok(options);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading equipment list.", detail = ex.Message });
            }
        }

        [HttpGet("items")]
        public async Task<IActionResult> GetItems([FromQuery] int categoryId = 0, [FromQuery] string? search = null)
        {
            try
            {
                var items = await QueryItemsAsync(categoryId, search, singleItemId: null);
                return Ok(items);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading items.", detail = ex.Message });
            }
        }

        [HttpGet("items/{itemId:int}")]
        public async Task<IActionResult> GetItem(int itemId)
        {
            try
            {
                var items = await QueryItemsAsync(0, null, singleItemId: itemId);
                if (items.Count == 0)
                    return NotFound(new { message = "Item not found." });
                return Ok(items[0]);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading item.", detail = ex.Message });
            }
        }

        [HttpGet("items/{itemId:int}/download")]
        public async Task<IActionResult> DownloadSpecification(int itemId)
        {
            try
            {
                var meta = await GetUploadMetaAsync(itemId);
                if (meta == null)
                    return NotFound(new { message = "File not found." });

                var physicalPath = Path.Combine(_specificationRoot, meta.Value.FileName);
                if (!System.IO.File.Exists(physicalPath))
                    return NotFound(new { message = "File not found on disk." });

                var downloadName = "E_cgmscEMSRoleSpecification" + meta.Value.FileName;
                return PhysicalFile(physicalPath, "application/pdf", downloadName);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error downloading file.", detail = ex.Message });
            }
        }

        [HttpPost("items/{itemId:int}/upload")]
        [RequestSizeLimit(MaxPdfBytes + 1024)]
        public async Task<IActionResult> UploadSpecification(int itemId, IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Please select a document to upload." });

            if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Please upload PDF file only." });

            if (file.Length > MaxPdfBytes)
                return BadRequest(new { message = "You cannot upload file more than 2 MB." });

            var fileName = $"{itemId}.pdf";
            var physicalPath = Path.Combine(_specificationRoot, fileName);

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                var schema = await GetSchemaAsync(conn);

                if (!schema.HasUploadTable)
                    return StatusCode(500, new { message = "Upload table masitems_upload not found in database." });

                await using (var stream = new FileStream(physicalPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                const string deleteSql = "DELETE FROM dbo.masitems_upload WHERE item_id = @ItemId";
                await using (var del = new SqlCommand(deleteSql, conn))
                {
                    del.Parameters.AddWithValue("@ItemId", itemId);
                    await del.ExecuteNonQueryAsync();
                }

                const string insertSql = @"
INSERT INTO dbo.masitems_upload (upload_folder_name, file_name, item_id)
VALUES (@Folder, @FileName, @ItemId)";

                await using var ins = new SqlCommand(insertSql, conn);
                ins.Parameters.AddWithValue("@Folder", UploadFolderName);
                ins.Parameters.AddWithValue("@FileName", fileName);
                ins.Parameters.AddWithValue("@ItemId", itemId);
                await ins.ExecuteNonQueryAsync();

                return Ok(new { message = "Uploaded Successfully." });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Database error during upload.", detail = ex.Message });
            }
            catch (IOException ex)
            {
                return StatusCode(500, new { message = "Could not save file.", detail = ex.Message });
            }
        }

        private async Task<ReportSpecificationSchema> GetSchemaAsync(SqlConnection conn)
        {
            if (_schema != null)
                return _schema;

            await _schemaLock.WaitAsync();
            try
            {
                _schema ??= await ReportSpecificationSchemaResolver.ResolveAsync(conn);
                return _schema;
            }
            finally
            {
                _schemaLock.Release();
            }
        }

        private async Task<List<ReportSpecificationItemDto>> QueryItemsAsync(
            int categoryId,
            string? search,
            int? singleItemId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            var schema = await GetSchemaAsync(conn);

            var categorySelect = schema.HasCategoryTable
                ? $"c.{schema.CategoryNameColumn}"
                : "''";
            var categoryJoin = schema.HasCategoryTable
                ? $"INNER JOIN dbo.{schema.CategoryTable} c ON m.{schema.ItemCategoryFkColumn} = c.{schema.CategoryIdColumn}"
                : string.Empty;
            var uploadJoin = schema.HasUploadTable
                ? "LEFT OUTER JOIN dbo.masitems_upload mu ON mu.item_id = m.item_id"
                : string.Empty;
            var hasSpecExpr = schema.HasUploadTable
                ? "CASE WHEN mu.item_id IS NOT NULL THEN 1 ELSE 0 END"
                : "0";

            var sql = $@"
SELECT DISTINCT
    m.item_id,
    m.item_code_as_per_tender,
    m.item_name,
    {categorySelect} AS eqpcatname,
    {hasSpecExpr} AS HasSpecification
FROM dbo.masitems m
{categoryJoin}
{uploadJoin}
WHERE 1=1";

            if (schema.HasCmeEelColumn)
                sql += " AND ISNULL(m.CME_EEL, 'N') = 'Y'";

            var parameters = new List<SqlParameter>();

            if (singleItemId.HasValue)
            {
                sql += " AND m.item_id = @ItemId";
                parameters.Add(new SqlParameter("@ItemId", singleItemId.Value));
            }

            if (categoryId > 0 && schema.HasCategoryTable)
            {
                sql += $" AND m.{schema.ItemCategoryFkColumn} = @CategoryId";
                parameters.Add(new SqlParameter("@CategoryId", categoryId));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchParts = new List<string>
                {
                    "m.item_name LIKE @Search",
                    "m.item_code_as_per_tender LIKE @Search",
                };
                if (schema.HasCategoryTable)
                    searchParts.Insert(1, $"c.{schema.CategoryNameColumn} LIKE @Search");

                sql += " AND (" + string.Join(" OR ", searchParts) + ")";
                parameters.Add(new SqlParameter("@Search", "%" + search.Trim() + "%"));
            }

            sql += " ORDER BY m.item_code_as_per_tender";

            var list = new List<ReportSpecificationItemDto>();
            await using var cmd = new SqlCommand(sql, conn);
            if (parameters.Count > 0)
                cmd.Parameters.AddRange(parameters.ToArray());

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new ReportSpecificationItemDto
                {
                    ItemId = Convert.ToInt32(reader["item_id"]),
                    ItemCode = reader["item_code_as_per_tender"]?.ToString() ?? string.Empty,
                    ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                    EqpCatName = reader["eqpcatname"]?.ToString() ?? string.Empty,
                    HasSpecification = reader["HasSpecification"] != DBNull.Value &&
                                         Convert.ToInt32(reader["HasSpecification"]) == 1,
                });
            }

            return list;
        }

        private async Task<(string FolderName, string FileName)?> GetUploadMetaAsync(int itemId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            var schema = await GetSchemaAsync(conn);

            if (!schema.HasUploadTable)
                return null;

            var fileCol = await ColumnExistsAsync(conn, "masitems_upload", "file_name")
                ? "file_name"
                : "FILE_NAME";

            var sql = $@"
SELECT upload_folder_name, {fileCol} AS file_name
FROM dbo.masitems_upload
WHERE item_id = @ItemId";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ItemId", itemId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            return (
                reader["upload_folder_name"]?.ToString() ?? UploadFolderName,
                reader["file_name"]?.ToString() ?? $"{itemId}.pdf"
            );
        }

        private async Task<ReportSpecificationSummaryDto> QuerySummaryAsync(int categoryId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            var schema = await GetSchemaAsync(conn);

            var summary = new ReportSpecificationSummaryDto();
            var parameters = new List<SqlParameter>();
            var where = "WHERE 1=1";
            if (schema.HasCmeEelColumn)
                where += " AND ISNULL(m.CME_EEL, 'N') = 'Y'";

            if (categoryId > 0 && schema.HasCategoryTable)
            {
                where += $" AND m.{schema.ItemCategoryFkColumn} = @CategoryId";
                parameters.Add(new SqlParameter("@CategoryId", categoryId));
            }

            var uploadJoin = schema.HasUploadTable
                ? "LEFT OUTER JOIN dbo.masitems_upload mu ON mu.item_id = m.item_id"
                : string.Empty;
            var uploadedExpr = schema.HasUploadTable
                ? "COUNT(DISTINCT CASE WHEN mu.item_id IS NOT NULL THEN m.item_id END)"
                : "0";

            if (schema.HasCategoryTable)
            {
                var categoryJoin =
                    $"INNER JOIN dbo.{schema.CategoryTable} c ON m.{schema.ItemCategoryFkColumn} = c.{schema.CategoryIdColumn}";
                var sql = $@"
SELECT c.{schema.CategoryIdColumn} AS cat_id,
       LTRIM(RTRIM(c.{schema.CategoryNameColumn})) AS cat_name,
       COUNT(DISTINCT m.item_id) AS total_items,
       {uploadedExpr} AS uploaded
FROM dbo.masitems m
{categoryJoin}
{uploadJoin}
{where}
GROUP BY c.{schema.CategoryIdColumn}, LTRIM(RTRIM(c.{schema.CategoryNameColumn}))
ORDER BY cat_name";

                await using (var cmd = new SqlCommand(sql, conn))
                {
                    if (parameters.Count > 0)
                        cmd.Parameters.AddRange(parameters.ToArray());
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        summary.Categories.Add(new CategoryUploadSummaryDto
                        {
                            CategoryId = reader["cat_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["cat_id"]),
                            CategoryName = reader["cat_name"]?.ToString()?.Trim() ?? string.Empty,
                            Total = Convert.ToInt32(reader["total_items"]),
                            Uploaded = Convert.ToInt32(reader["uploaded"]),
                        });
                    }
                }
            }

            var totalSql = $@"
SELECT COUNT(DISTINCT m.item_id) AS total_items,
       {uploadedExpr} AS uploaded
FROM dbo.masitems m
{(schema.HasCategoryTable ? $"INNER JOIN dbo.{schema.CategoryTable} c ON m.{schema.ItemCategoryFkColumn} = c.{schema.CategoryIdColumn}" : string.Empty)}
{uploadJoin}
{where}";

            await using (var totalCmd = new SqlCommand(totalSql, conn))
            {
                if (parameters.Count > 0)
                    totalCmd.Parameters.AddRange(parameters.Select(p =>
                        new SqlParameter(p.ParameterName, p.Value)).ToArray());
                await using var reader = await totalCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    summary.TotalItems = Convert.ToInt32(reader["total_items"]);
                    summary.TotalUploaded = Convert.ToInt32(reader["uploaded"]);
                }
            }

            return summary;
        }

        private static async Task<bool> ColumnExistsAsync(SqlConnection conn, string table, string column)
        {
            const string sql = @"
SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @Table AND COLUMN_NAME = @Column";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Table", table);
            cmd.Parameters.AddWithValue("@Column", column);
            return await cmd.ExecuteScalarAsync() != null;
        }
    }
}
