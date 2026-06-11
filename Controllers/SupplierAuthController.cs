using EMISAPIS.DTOS;
using EMISAPIS.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EMISAPIS.Controllers
{
    /// <summary>
    /// LoginEmsSUP.aspx supplier login / OTP / password APIs.
    /// Separate controller avoids route clash with AuthController GET {id}.
    /// </summary>
    [Route("api/Auth/supplier")]
    [ApiController]
    public class SupplierAuthController : ControllerBase
    {
        private readonly string _connectionString;

        public SupplierAuthController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection is not configured.");
        }

        [HttpGet("profile/{id:int}")]
        public async Task<IActionResult> GetSupplierProfile(int id, [FromQuery] string mode = "login")
        {
            if (id <= 0)
                return BadRequest(new { message = "Invalid id." });

            try
            {
                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                int supplierId;
                string userEmail = string.Empty;

                if (string.Equals(mode, "new", StringComparison.OrdinalIgnoreCase))
                {
                    supplierId = id;
                }
                else
                {
                    using SqlCommand userCmd = new SqlCommand(
                        @"SELECT supplier_id, e_mail_id
                          FROM users
                          WHERE user_id = @UserId AND user_type = 'SUP'", con);
                    userCmd.Parameters.AddWithValue("@UserId", id);

                    using SqlDataReader userReader = await userCmd.ExecuteReaderAsync();
                    if (!await userReader.ReadAsync())
                        return NotFound(new { message = "Supplier user not found." });

                    supplierId = userReader["supplier_id"] != DBNull.Value
                        ? Convert.ToInt32(userReader["supplier_id"])
                        : 0;
                    userEmail = userReader["e_mail_id"]?.ToString() ?? string.Empty;

                    if (supplierId <= 0)
                        return NotFound(new { message = "Supplier mapping not found for user." });
                }

                using SqlCommand cmd = new SqlCommand(
                    @"SELECT supplier_id, name, mobile_no, ISNULL(email_id, '-') AS email_id
                      FROM massuppliers
                      WHERE supplier_id = @SupplierId", con);
                cmd.Parameters.AddWithValue("@SupplierId", supplierId);

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound(new { message = "Supplier not found." });

                string mobile = reader["mobile_no"]?.ToString()?.Trim() ?? string.Empty;
                string maskedMobile = mobile.Length >= 4 ? "xxxxxx" + mobile[^4..] : mobile;

                return Ok(new SupplierProfileDto
                {
                    SupplierId = supplierId,
                    Name = reader["name"]?.ToString() ?? string.Empty,
                    MaskedMobile = maskedMobile,
                    Email = reader["email_id"]?.ToString() ?? "-",
                    UserEmail = userEmail
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching supplier profile.", error = ex.Message });
            }
        }

        [HttpPost("send-otp")]
        public async Task<IActionResult> SendSupplierOtp([FromBody] SupplierOtpRequestDto request)
        {
            if (request == null || request.SupplierId <= 0)
                return BadRequest(new { message = "Please select a supplier." });

            try
            {
                int otp = Random.Shared.Next(1000, 9999);
                string otpText = otp.ToString();

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                using (SqlCommand updateCmd = new SqlCommand(
                    "UPDATE massuppliers SET OTP = @Otp WHERE supplier_id = @SupplierId", con))
                {
                    updateCmd.Parameters.AddWithValue("@Otp", otpText);
                    updateCmd.Parameters.AddWithValue("@SupplierId", request.SupplierId);

                    int rows = await updateCmd.ExecuteNonQueryAsync();
                    if (rows == 0)
                        return NotFound(new { message = "Supplier not found." });
                }

                string mobile = string.Empty;
                string email = "-";

                using (SqlCommand infoCmd = new SqlCommand(
                    "SELECT mobile_no, ISNULL(email_id, '-') AS email_id FROM massuppliers WHERE supplier_id = @SupplierId", con))
                {
                    infoCmd.Parameters.AddWithValue("@SupplierId", request.SupplierId);
                    using SqlDataReader reader = await infoCmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        mobile = reader["mobile_no"]?.ToString()?.Trim() ?? string.Empty;
                        email = reader["email_id"]?.ToString()?.Trim() ?? "-";
                    }
                }

                bool emailAck = email != "-" && !string.IsNullOrWhiteSpace(email);
                bool smsAck = !string.IsNullOrWhiteSpace(mobile);

                if (!emailAck && !smsAck)
                {
                    return BadRequest(new
                    {
                        message = "Please register your mobile number by communicating with Equipment/IT Section CGMSC."
                    });
                }

                try
                {
                    string content = $"OTP for submission in EMIS is {otpText}. Please do not share with anyone.";
                    string logType = emailAck && smsAck ? "Email_Reg_Mob" : emailAck ? "Email" : "Reg_Mob";

                    using SqlCommand logCmd = new SqlCommand(
                        @"INSERT INTO smslog(logType, supplier_id, sms, entrydate, module, OTP, mobno, email)
                          VALUES(@LogType, @SupplierId, @Sms, GETDATE(), 'Supplier', @Otp, @Mob, @Email)", con);
                    logCmd.Parameters.AddWithValue("@LogType", logType);
                    logCmd.Parameters.AddWithValue("@SupplierId", request.SupplierId);
                    logCmd.Parameters.AddWithValue("@Sms", content);
                    logCmd.Parameters.AddWithValue("@Otp", otpText);
                    logCmd.Parameters.AddWithValue("@Mob", mobile);
                    logCmd.Parameters.AddWithValue("@Email", email);
                    await logCmd.ExecuteNonQueryAsync();
                }
                catch
                {
                    // OTP is saved on massuppliers; smslog is optional (schema may differ on some servers).
                }

                string mobileMask = mobile.Length >= 4 ? "xxxxxx" + mobile[^4..] : mobile;
                string whereSent = emailAck && smsAck
                    ? $"Registered Mobile {mobileMask} and e-MailID: {email}"
                    : emailAck ? $"Registered e-MailID: {email}"
                    : $"Registered Mobile {mobileMask}";

                return Ok(new
                {
                    message = $"OTP has been sent on your {whereSent}. Please use it to generate or reset password."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to send OTP.", error = ex.Message });
            }
        }

        [HttpPost("complete-password")]
        public async Task<IActionResult> CompleteSupplierPassword([FromBody] SupplierPasswordRequestDto request)
        {
            if (request == null || request.SupplierId <= 0)
                return BadRequest(new { message = "Please select a supplier." });

            if (string.IsNullOrWhiteSpace(request.Otp))
                return BadRequest(new { message = "Please submit 4 digit OTP sent on your mobile." });

            if (request.NewPassword != request.RepeatPassword)
                return BadRequest(new { message = "Both new password and repeat password do not match." });

            try
            {
                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                string savedOtp = string.Empty;
                string supplierName = string.Empty;

                using (SqlCommand otpCmd = new SqlCommand(
                    "SELECT OTP, name FROM massuppliers WHERE supplier_id = @SupplierId", con))
                {
                    otpCmd.Parameters.AddWithValue("@SupplierId", request.SupplierId);
                    using SqlDataReader reader = await otpCmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                        return NotFound(new { message = "Supplier not found." });

                    savedOtp = reader["OTP"]?.ToString()?.Trim() ?? string.Empty;
                    supplierName = reader["name"]?.ToString() ?? string.Empty;
                }

                if (savedOtp != request.Otp.Trim())
                    return BadRequest(new { message = "OTP not matched." });

                string storedPassword = SaltedHash.CreateStored(request.NewPassword);
                string mode = (request.Mode ?? "reset").ToLowerInvariant();

                if (mode == "reset")
                {
                    using SqlCommand updateCmd = new SqlCommand(
                        @"UPDATE users
                          SET password = @Password, lastPwdChangeDate = GETDATE()
                          WHERE supplier_id = @SupplierId AND user_type = 'SUP'", con);
                    updateCmd.Parameters.AddWithValue("@Password", storedPassword);
                    updateCmd.Parameters.AddWithValue("@SupplierId", request.SupplierId);

                    int rows = await updateCmd.ExecuteNonQueryAsync();
                    if (rows == 0)
                        return NotFound(new { message = "Supplier user account not found." });
                }
                else if (mode == "new")
                {
                    if (string.IsNullOrWhiteSpace(request.DesiredUserId))
                        return BadRequest(new { message = "Please enter desired user id." });

                    string emailId = request.DesiredUserId.Trim() + "@ems.in";

                    using SqlCommand existsCmd = new SqlCommand(
                        "SELECT COUNT(1) FROM users WHERE e_mail_id = @Email", con);
                    existsCmd.Parameters.AddWithValue("@Email", emailId);

                    int exists = Convert.ToInt32(await existsCmd.ExecuteScalarAsync());
                    if (exists > 0)
                        return BadRequest(new { message = "User id already exists. Choose another user id." });

                    using SqlCommand insertCmd = new SqlCommand(
                        @"INSERT INTO users (user_name, e_mail_id, password, user_type, ems, supplier_id, lastPwdChangeDate)
                          VALUES (@UserName, @Email, @Password, 'SUP', 'T', @SupplierId, GETDATE())", con);
                    insertCmd.Parameters.AddWithValue("@UserName", supplierName);
                    insertCmd.Parameters.AddWithValue("@Email", emailId);
                    insertCmd.Parameters.AddWithValue("@Password", storedPassword);
                    insertCmd.Parameters.AddWithValue("@SupplierId", request.SupplierId);
                    await insertCmd.ExecuteNonQueryAsync();
                }
                else
                {
                    return BadRequest(new { message = "Invalid mode." });
                }

                return Ok(new { message = "You have successfully generated/reset password. Please login in EMIS." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Password not saved.", error = ex.Message });
            }
        }

        /// <summary>ParticularSupplierAdd.aspx — load logged-in supplier profile.</summary>
        [HttpGet("details/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierDetailsByUser(int userId)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });

            try
            {
                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                int supplierId = 0;
                using (SqlCommand userCmd = new SqlCommand(
                    "SELECT supplier_id FROM users WHERE user_id = @UserId AND user_type = 'SUP'", con))
                {
                    userCmd.Parameters.AddWithValue("@UserId", userId);
                    object? result = await userCmd.ExecuteScalarAsync();
                    if (result == null || result == DBNull.Value)
                        return NotFound(new { message = "Supplier user not found." });

                    supplierId = Convert.ToInt32(result);
                }

                using SqlCommand cmd = new SqlCommand(
                    @"SELECT supplier_id, name, service_engineer_name, mobile_no, email_id,
                             GST_no, ISNULL(GST_no2, '') AS GST_no2, ISNULL(GST_no3, '') AS GST_no3,
                             ph_no, tin_no, address
                      FROM massuppliers
                      WHERE supplier_id = @SupplierId", con);
                cmd.Parameters.AddWithValue("@SupplierId", supplierId);

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound(new { message = "Supplier not found." });

                return Ok(new ParticularSupplierDetailsDto
                {
                    SupplierId = supplierId,
                    SupplierName = reader["name"]?.ToString() ?? string.Empty,
                    ContactPersonName = reader["service_engineer_name"]?.ToString() ?? string.Empty,
                    MobileNo = reader["mobile_no"]?.ToString() ?? string.Empty,
                    Email = reader["email_id"]?.ToString() ?? string.Empty,
                    GstNo = reader["GST_no"]?.ToString() ?? string.Empty,
                    GstNo2 = reader["GST_no2"]?.ToString() ?? string.Empty,
                    GstNo3 = reader["GST_no3"]?.ToString() ?? string.Empty,
                    PhoneNo = reader["ph_no"]?.ToString() ?? string.Empty,
                    TinNo = reader["tin_no"]?.ToString() ?? string.Empty,
                    Address = reader["address"]?.ToString() ?? string.Empty
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching supplier details.", error = ex.Message });
            }
        }

        /// <summary>ParticularSupplierAdd.aspx — update (legacy fields only).</summary>
        [HttpPut("details")]
        public async Task<IActionResult> UpdateSupplierDetails([FromBody] ParticularSupplierUpdateDto request)
        {
            if (request == null || request.SupplierId <= 0)
                return BadRequest(new { message = "Invalid supplier." });

            if (string.IsNullOrWhiteSpace(request.MobileNo))
                return BadRequest(new { message = "Please insert Mobile Number." });
            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "Please insert Email Id." });
            if (string.IsNullOrWhiteSpace(request.GstNo))
                return BadRequest(new { message = "Please insert GST No." });
            if (string.IsNullOrWhiteSpace(request.PhoneNo))
                return BadRequest(new { message = "Please insert Phone No." });
            if (string.IsNullOrWhiteSpace(request.Address))
                return BadRequest(new { message = "Please insert Address." });

            string mobile = request.MobileNo.Trim();
            if (mobile.Length != 10)
                return BadRequest(new { message = "The limit of Mobile Number is 10 digits." });

            string email = request.Email.Trim();
            if (email.Length > 50)
                return BadRequest(new { message = "The limit of Email Id is 50 characters." });

            string gst = request.GstNo.Trim();
            if (gst.Length > 15)
                return BadRequest(new { message = "The limit of GST No is 15 characters." });

            string phone = request.PhoneNo.Trim();
            if (phone.Length < 10 || phone.Length > 11)
                return BadRequest(new { message = "The limit of phn No is 11 digits." });

            try
            {
                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                using SqlCommand updateCmd = new SqlCommand(
                    @"UPDATE massuppliers
                      SET mobile_no = @MobileNo,
                          email_id = @Email,
                          ph_no = @PhoneNo,
                          address = @Address,
                          GST_no = @GstNo,
                          GST_no2 = @GstNo2,
                          GST_no3 = @GstNo3,
                          update_date = GETDATE()
                      WHERE supplier_id = @SupplierId", con);
                updateCmd.Parameters.AddWithValue("@SupplierId", request.SupplierId);
                updateCmd.Parameters.AddWithValue("@MobileNo", mobile);
                updateCmd.Parameters.AddWithValue("@Email", email);
                updateCmd.Parameters.AddWithValue("@PhoneNo", phone);
                updateCmd.Parameters.AddWithValue("@Address", request.Address.Trim());
                updateCmd.Parameters.AddWithValue("@GstNo", gst);
                updateCmd.Parameters.AddWithValue("@GstNo2", request.GstNo2?.Trim() ?? string.Empty);
                updateCmd.Parameters.AddWithValue("@GstNo3", request.GstNo3?.Trim() ?? string.Empty);

                int rows = await updateCmd.ExecuteNonQueryAsync();
                if (rows == 0)
                    return NotFound(new { message = "Supplier not found." });

                return Ok(new { message = "Updated Successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Update failed.", error = ex.Message });
            }
        }

        /// <summary>SupplierGSTentry1.aspx — load GST list for logged-in supplier.</summary>
        [HttpGet("gst-entries/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierGstEntriesByUser(int userId)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                string supplierCode = string.Empty;
                string supplierName = string.Empty;

                using (SqlCommand headerCmd = new SqlCommand(
                    "SELECT supplier_code, name FROM massuppliers WHERE supplier_id = @SupplierId", con))
                {
                    headerCmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);
                    using SqlDataReader reader = await headerCmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        supplierCode = reader["supplier_code"]?.ToString() ?? string.Empty;
                        supplierName = reader["name"]?.ToString() ?? string.Empty;
                    }
                }

                var entries = new List<SupplierGstEntryDto>();
                using (SqlCommand listCmd = new SqlCommand(
                    @"SELECT gstid, gstno, supplierid, flag
                      FROM massuppliergst
                      WHERE supplierid = @SupplierId
                      ORDER BY gstid ASC", con))
                {
                    listCmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);
                    using SqlDataReader reader = await listCmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        entries.Add(new SupplierGstEntryDto
                        {
                            GstId = ReadIntColumn(reader, "gstid", "gst_id"),
                            GstNo = ReadStringColumn(reader, "gstno", "gst_no"),
                            SupplierId = supplierId.Value,
                            Flag = ReadStringColumn(reader, "flag")
                        });
                    }
                }

                return Ok(new SupplierGstPageDto
                {
                    SupplierId = supplierId.Value,
                    SupplierCode = supplierCode,
                    SupplierName = supplierName,
                    Entries = entries
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching GST entries.", error = ex.Message });
            }
        }

        [HttpPost("gst-entries")]
        public async Task<IActionResult> AddSupplierGstEntry([FromBody] SupplierGstSaveDto request)
        {
            if (request == null || request.UserId <= 0 || request.SupplierId <= 0)
                return BadRequest(new { message = "Invalid request." });

            if (string.IsNullOrWhiteSpace(request.GstNo))
                return BadRequest(new { message = "GST no is required." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(request.UserId);
                if (supplierId == null || supplierId.Value != request.SupplierId)
                    return Unauthorized(new { message = "Supplier access denied." });

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                using SqlCommand insertCmd = new SqlCommand(
                    @"INSERT INTO massuppliergst (gstno, supplierid, flag)
                      VALUES (@GstNo, @SupplierId, 'Y')", con);
                insertCmd.Parameters.AddWithValue("@GstNo", request.GstNo.Trim());
                insertCmd.Parameters.AddWithValue("@SupplierId", request.SupplierId);
                await insertCmd.ExecuteNonQueryAsync();

                return Ok(new { message = "Added Successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Add failed.", error = ex.Message });
            }
        }

        [HttpPut("gst-entries/{gstId:int}")]
        public async Task<IActionResult> UpdateSupplierGstEntry(int gstId, [FromBody] SupplierGstSaveDto request)
        {
            if (gstId <= 0 || request == null || request.UserId <= 0)
                return BadRequest(new { message = "Invalid request." });

            if (string.IsNullOrWhiteSpace(request.GstNo))
                return BadRequest(new { message = "GST no is required." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(request.UserId);
                if (supplierId == null)
                    return Unauthorized(new { message = "Supplier access denied." });

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                using SqlCommand updateCmd = new SqlCommand(
                    @"UPDATE massuppliergst
                      SET gstno = @GstNo
                      WHERE gstid = @GstId AND supplierid = @SupplierId", con);
                updateCmd.Parameters.AddWithValue("@GstNo", request.GstNo.Trim());
                updateCmd.Parameters.AddWithValue("@GstId", gstId);
                updateCmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);

                int rows = await updateCmd.ExecuteNonQueryAsync();
                if (rows == 0)
                    return NotFound(new { message = "GST entry not found." });

                return Ok(new { message = "Updated Successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Update failed.", error = ex.Message });
            }
        }

        [HttpDelete("gst-entries/{gstId:int}")]
        public async Task<IActionResult> DeleteSupplierGstEntry(int gstId, [FromQuery] int userId)
        {
            if (gstId <= 0 || userId <= 0)
                return BadRequest(new { message = "Invalid request." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return Unauthorized(new { message = "Supplier access denied." });

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                using SqlCommand deleteCmd = new SqlCommand(
                    "DELETE FROM massuppliergst WHERE gstid = @GstId AND supplierid = @SupplierId", con);
                deleteCmd.Parameters.AddWithValue("@GstId", gstId);
                deleteCmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);

                int rows = await deleteCmd.ExecuteNonQueryAsync();
                if (rows == 0)
                    return NotFound(new { message = "GST entry not found." });

                return Ok(new { message = "Deleted successfully" });
            }
            catch (SqlException)
            {
                return BadRequest(new { message = "Delete not allowed, references found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Delete failed.", error = ex.Message });
            }
        }

        private async Task<int?> ResolveSupplierIdForUserAsync(int userId)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            using SqlCommand userCmd = new SqlCommand(
                "SELECT supplier_id FROM users WHERE user_id = @UserId AND user_type = 'SUP'", con);
            userCmd.Parameters.AddWithValue("@UserId", userId);

            object? result = await userCmd.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value)
                return null;

            return Convert.ToInt32(result);
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
                    // try next alias
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
                    // try next alias
                }
            }

            return string.Empty;
        }
    }
}
