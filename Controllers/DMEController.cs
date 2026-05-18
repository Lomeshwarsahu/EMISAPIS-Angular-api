using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EMISAPIS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DMEController : ControllerBase
    {
        private readonly string _connectionString;

        public DMEController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection missing.");
        }

        /// <summary>
        /// Loads facility contact details (legacy Master/StoreHome.aspx — users table only).
        /// </summary>
        [HttpGet("consignee/{userId:int}")]
        public async Task<IActionResult> GetConsigneeInformation(int userId)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });

            // Same SELECT as Master/StoreHome.aspx.cs fillDetails() — EMIS.dbo.users only
            const string query = ConsigneeSelectSql + " WHERE user_id = @UserId";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);

                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound(new { message = "User not found." });

                return Ok(MapConsigneeReader(reader));
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Database error loading consignee information.", detail = ex.Message });
            }
        }

        /// <summary>Load by login email when user_id is unavailable (StoreHome session uses DistId).</summary>
        [HttpGet("consignee/by-email/{email}")]
        public async Task<IActionResult> GetConsigneeByEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { message = "Email is required." });

            const string query = ConsigneeSelectByEmailSql;

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Email", email.Trim());

                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound(new { message = "User not found for this email." });

                return Ok(MapConsigneeReader(reader));
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Database error loading consignee information.", detail = ex.Message });
            }
        }

        /// <summary>
        /// Persists contact updates (legacy Master/StoreHome.aspx btnComplain_Click).
        /// </summary>
        [HttpPut("consignee")]
        public async Task<IActionResult> UpdateConsigneeInformation([FromBody] ConsigneeInformationUpdateDTO dto)
        {
            if (dto == null || dto.UserId <= 0)
                return BadRequest(new { message = "Invalid payload." });

            if (string.IsNullOrWhiteSpace(dto.AddressLine2)
                || string.IsNullOrWhiteSpace(dto.AddressLine1)
                || string.IsNullOrWhiteSpace(dto.DeanMobile)
                || string.IsNullOrWhiteSpace(dto.StoreOfficerMobile)
                || string.IsNullOrWhiteSpace(dto.StoreOfficerName)
                || string.IsNullOrWhiteSpace(dto.OfficeEmail))
            {
                return BadRequest(new { message = "Please fill all the required fields." });
            }

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string updateUser = @"
UPDATE dbo.users SET
    user_name = @AddressLine1,
    address = @AddressLine2,
    address2 = @AddressLine3,
    HODName = @DeanName,
    HODNo = @DeanMobile,
    emailID = @OfficeEmail,
    storeOfficerMob = @StoreOfficerMobile,
    storeOfficer = @StoreOfficerName,
    storelandline = @OfficeContactNo,
    updateDT = GETDATE()
WHERE user_id = @UserId";

                await using var cmdUser = new SqlCommand(updateUser, conn);
                cmdUser.Parameters.AddWithValue("@UserId", dto.UserId);
                cmdUser.Parameters.AddWithValue("@AddressLine1", dto.AddressLine1.Trim());
                cmdUser.Parameters.AddWithValue("@AddressLine2", dto.AddressLine2.Trim());
                cmdUser.Parameters.AddWithValue("@AddressLine3", (dto.AddressLine3 ?? string.Empty).Trim());
                cmdUser.Parameters.AddWithValue("@DeanName", (dto.DeanName ?? string.Empty).Trim());
                cmdUser.Parameters.AddWithValue("@DeanMobile", dto.DeanMobile.Trim());
                cmdUser.Parameters.AddWithValue("@OfficeEmail", dto.OfficeEmail.Trim());
                cmdUser.Parameters.AddWithValue("@StoreOfficerMobile", dto.StoreOfficerMobile.Trim());
                cmdUser.Parameters.AddWithValue("@StoreOfficerName", dto.StoreOfficerName.Trim());
                cmdUser.Parameters.AddWithValue("@OfficeContactNo", (dto.OfficeContactNo ?? string.Empty).Trim());

                int n = await cmdUser.ExecuteNonQueryAsync();
                if (n == 0)
                    return NotFound(new { message = "User not found for update." });

                return Ok(new { message = "Contact Updated Successfully" });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Database error saving consignee information.", detail = ex.Message });
            }
        }

        /// <summary>StoreHome.aspx fillDetails — maps EMIS.dbo.users columns to API DTO.</summary>
        private const string ConsigneeSelectSql = @"
SELECT
    user_id,
    ISNULL(e_mail_id, '') AS LoginEmail,
    ISNULL(HODName, '') AS DeanName,
    ISNULL(HODNo, '') AS DeanMobile,
    ISNULL(storeOfficer, '') AS StoreOfficerName,
    ISNULL(storeOfficerMob, '') AS StoreOfficerMobile,
    ISNULL(emailID, '') AS OfficeEmail,
    ISNULL(storelandline, '') AS OfficeContactNo,
    ISNULL(user_name, '') AS AddressLine1,
    ISNULL(address, '') AS AddressLine2,
    ISNULL(address2, '') AS AddressLine3
FROM dbo.users";

        private const string ConsigneeSelectByEmailSql = @"
SELECT TOP 1
    user_id,
    ISNULL(e_mail_id, '') AS LoginEmail,
    ISNULL(HODName, '') AS DeanName,
    ISNULL(HODNo, '') AS DeanMobile,
    ISNULL(storeOfficer, '') AS StoreOfficerName,
    ISNULL(storeOfficerMob, '') AS StoreOfficerMobile,
    ISNULL(emailID, '') AS OfficeEmail,
    ISNULL(storelandline, '') AS OfficeContactNo,
    ISNULL(user_name, '') AS AddressLine1,
    ISNULL(address, '') AS AddressLine2,
    ISNULL(address2, '') AS AddressLine3
FROM dbo.users
WHERE LTRIM(RTRIM(e_mail_id)) = @Email";

        private static ConsigneeInformationDTO MapConsigneeReader(SqlDataReader reader) =>
            new()
            {
                UserId = reader["user_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["user_id"]),
                LoginEmail = reader["LoginEmail"]?.ToString() ?? string.Empty,
                DeanName = reader["DeanName"]?.ToString() ?? string.Empty,
                DeanMobile = reader["DeanMobile"]?.ToString() ?? string.Empty,
                StoreOfficerName = reader["StoreOfficerName"]?.ToString() ?? string.Empty,
                StoreOfficerMobile = reader["StoreOfficerMobile"]?.ToString() ?? string.Empty,
                OfficeEmail = reader["OfficeEmail"]?.ToString() ?? string.Empty,
                OfficeContactNo = reader["OfficeContactNo"]?.ToString() ?? string.Empty,
                AddressLine1 = reader["AddressLine1"]?.ToString() ?? string.Empty,
                AddressLine2 = reader["AddressLine2"]?.ToString() ?? string.Empty,
                AddressLine3 = reader["AddressLine3"]?.ToString() ?? string.Empty,
            };

        private static object NullIfEmpty(string? s) =>
            string.IsNullOrWhiteSpace(s) ? DBNull.Value : s.Trim();
    }
}
