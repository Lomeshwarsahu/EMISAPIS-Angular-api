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
        /// Loads facility / consignee contact details for the logged-in DME user.
        /// </summary>
        [HttpGet("consignee/{userId:int}")]
        public async Task<IActionResult> GetConsigneeInformation(int userId)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });

            const string query = @"
SELECT
    u.user_id,
    ISNULL(u.e_mail_id, '') AS LoginEmail,
    ISNULL(u.StoreName, '') AS StoreOfficerName,
    ISNULL(u.storeOfficerMob, '') AS StoreOfficerMobile,
    ISNULL(u.emailID, '') AS OfficeEmail,
    ISNULL(u.StoreNo, '') AS OfficeContactNo,
    ISNULL(m.conduct_person, '') AS DeanName,
    ISNULL(m.mob_no, '') AS DeanMobile,
    ISNULL(m.address_1, '') AS AddressLine1,
    ISNULL(m.address_2, '') AS AddressLine2,
    ISNULL(m.address_3, '') AS AddressLine3,
    ISNULL(m.location_name, '') AS LocationName,
    ISNULL(m.location_id, 0) AS LocationId
FROM dbo.users u
LEFT JOIN dbo.maslocations m ON m.location_id = u.location_id
WHERE u.user_id = @UserId";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);

                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound(new { message = "User not found." });

                var dto = new ConsigneeInformationDTO
                {
                    UserId = reader["user_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["user_id"]),
                    LoginEmail = reader["LoginEmail"]?.ToString() ?? string.Empty,
                    StoreOfficerName = reader["StoreOfficerName"]?.ToString() ?? string.Empty,
                    StoreOfficerMobile = reader["StoreOfficerMobile"]?.ToString() ?? string.Empty,
                    OfficeEmail = reader["OfficeEmail"]?.ToString() ?? string.Empty,
                    OfficeContactNo = reader["OfficeContactNo"]?.ToString() ?? string.Empty,
                    DeanName = reader["DeanName"]?.ToString() ?? string.Empty,
                    DeanMobile = reader["DeanMobile"]?.ToString() ?? string.Empty,
                    AddressLine1 = reader["AddressLine1"]?.ToString() ?? string.Empty,
                    AddressLine2 = reader["AddressLine2"]?.ToString() ?? string.Empty,
                    AddressLine3 = reader["AddressLine3"]?.ToString() ?? string.Empty,
                    LocationName = reader["LocationName"]?.ToString() ?? string.Empty,
                    LocationId = reader["LocationId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["LocationId"]),
                };

                return Ok(dto);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Database error loading consignee information.", detail = ex.Message });
            }
        }

        /// <summary>
        /// Persists consignee contact updates to users and maslocations (legacy EMIS schema).
        /// </summary>
        [HttpPut("consignee")]
        public async Task<IActionResult> UpdateConsigneeInformation([FromBody] ConsigneeInformationUpdateDTO dto)
        {
            if (dto == null || dto.UserId <= 0)
                return BadRequest(new { message = "Invalid payload." });

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var tx = conn.BeginTransaction();

                const string updateUser = @"
UPDATE dbo.users SET
    StoreName = @StoreOfficerName,
    storeOfficerMob = @StoreOfficerMobile,
    emailID = @OfficeEmail,
    StoreNo = @OfficeContactNo
WHERE user_id = @UserId";

                await using (var cmdUser = new SqlCommand(updateUser, conn, tx))
                {
                    cmdUser.Parameters.AddWithValue("@UserId", dto.UserId);
                    cmdUser.Parameters.AddWithValue("@StoreOfficerName", NullIfEmpty(dto.StoreOfficerName));
                    cmdUser.Parameters.AddWithValue("@StoreOfficerMobile", NullIfEmpty(dto.StoreOfficerMobile));
                    cmdUser.Parameters.AddWithValue("@OfficeEmail", NullIfEmpty(dto.OfficeEmail));
                    cmdUser.Parameters.AddWithValue("@OfficeContactNo", NullIfEmpty(dto.OfficeContactNo));

                    int n = await cmdUser.ExecuteNonQueryAsync();
                    if (n == 0)
                    {
                        tx.Rollback();
                        return NotFound(new { message = "User not found for update." });
                    }
                }

                const string getLoc = "SELECT location_id FROM dbo.users WHERE user_id = @UserId";
                int locationId = 0;
                await using (var cmdLoc = new SqlCommand(getLoc, conn, tx))
                {
                    cmdLoc.Parameters.AddWithValue("@UserId", dto.UserId);
                    var o = await cmdLoc.ExecuteScalarAsync();
                    if (o != null && o != DBNull.Value)
                        locationId = Convert.ToInt32(o);
                }

                if (locationId > 0)
                {
                    const string updateLoc = @"
UPDATE dbo.maslocations SET
    conduct_person = @DeanName,
    mob_no = @DeanMobile,
    address_1 = @A1,
    address_2 = @A2,
    address_3 = @A3
WHERE location_id = @LocationId";

                    await using var cmdLocUp = new SqlCommand(updateLoc, conn, tx);
                    cmdLocUp.Parameters.AddWithValue("@LocationId", locationId);
                    cmdLocUp.Parameters.AddWithValue("@DeanName", NullIfEmpty(dto.DeanName));
                    cmdLocUp.Parameters.AddWithValue("@DeanMobile", NullIfEmpty(dto.DeanMobile));
                    cmdLocUp.Parameters.AddWithValue("@A1", NullIfEmpty(dto.AddressLine1));
                    cmdLocUp.Parameters.AddWithValue("@A2", NullIfEmpty(dto.AddressLine2));
                    cmdLocUp.Parameters.AddWithValue("@A3", NullIfEmpty(dto.AddressLine3));
                    await cmdLocUp.ExecuteNonQueryAsync();
                }

                tx.Commit();
                return Ok(new { message = "Updated successfully." });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Database error saving consignee information.", detail = ex.Message });
            }
        }

        private static object NullIfEmpty(string? s) =>
            string.IsNullOrWhiteSpace(s) ? DBNull.Value : s.Trim();
    }
}
