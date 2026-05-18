using Microsoft.Data.SqlClient;

namespace EMISAPIS.Helpers
{
    /// <summary>
    /// Legacy EMS uses maseqpcat + eqpcatid; EMIS often uses mascategory + categoryid.
    /// </summary>
    public sealed class ReportSpecificationSchema
    {
        public string CategoryTable { get; init; } = string.Empty;
        public string CategoryIdColumn { get; init; } = string.Empty;
        public string CategoryNameColumn { get; init; } = string.Empty;
        public string ItemCategoryFkColumn { get; init; } = string.Empty;
        public bool HasCmeEelColumn { get; init; }
        public bool HasUploadTable { get; init; }
        public bool HasCategoryTable { get; init; }
    }

    public static class ReportSpecificationSchemaResolver
    {
        public static async Task<ReportSpecificationSchema> ResolveAsync(SqlConnection conn)
        {
            var hasMaseqpcat = await TableExistsAsync(conn, "maseqpcat");
            var hasMascategory = await TableExistsAsync(conn, "mascategory");
            var hasCmeEel = await ColumnExistsAsync(conn, "masitems", "CME_EEL");
            var hasUpload = await TableExistsAsync(conn, "masitems_upload");

            // dbo.maseqpcat (eqpcatid, eqpcatname) — legacy report_specification.aspx
            var hasEqpcatFk = await ColumnExistsAsync(conn, "masitems", "eqpcatid");
            if (hasMaseqpcat && hasEqpcatFk)
            {
                return new ReportSpecificationSchema
                {
                    HasCategoryTable = true,
                    CategoryTable = "maseqpcat",
                    CategoryIdColumn = "eqpcatid",
                    CategoryNameColumn = "eqpcatname",
                    ItemCategoryFkColumn = "eqpcatid",
                    HasCmeEelColumn = hasCmeEel,
                    HasUploadTable = hasUpload,
                };
            }

            if (hasMascategory)
            {
                var catId = await ColumnExistsAsync(conn, "mascategory", "categoryId")
                    ? "categoryId"
                    : "categoryid";
                var catName = await ColumnExistsAsync(conn, "mascategory", "categoryName")
                    ? "categoryName"
                    : "categoryname";
                var itemFk = await ColumnExistsAsync(conn, "masitems", "categoryid")
                    ? "categoryid"
                    : "categoryId";

                return new ReportSpecificationSchema
                {
                    HasCategoryTable = true,
                    CategoryTable = "mascategory",
                    CategoryIdColumn = catId,
                    CategoryNameColumn = catName,
                    ItemCategoryFkColumn = itemFk,
                    HasCmeEelColumn = hasCmeEel,
                    HasUploadTable = hasUpload,
                };
            }

            return new ReportSpecificationSchema
            {
                HasCategoryTable = false,
                HasCmeEelColumn = hasCmeEel,
                HasUploadTable = hasUpload,
            };
        }

        private static async Task<bool> TableExistsAsync(SqlConnection conn, string tableName)
        {
            const string sql = @"
SELECT 1 FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @Name";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Name", tableName);
            var result = await cmd.ExecuteScalarAsync();
            return result != null;
        }

        private static async Task<bool> ColumnExistsAsync(SqlConnection conn, string tableName, string columnName)
        {
            const string sql = @"
SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @Table AND COLUMN_NAME = @Column";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Table", tableName);
            cmd.Parameters.AddWithValue("@Column", columnName);
            var result = await cmd.ExecuteScalarAsync();
            return result != null;
        }
    }
}
