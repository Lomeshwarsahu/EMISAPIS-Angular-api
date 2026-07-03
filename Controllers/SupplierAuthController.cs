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
        private readonly string _complaintFileRoot;
        private readonly string _emdFileRoot;
        private readonly string _sdFileRoot;
        private readonly string _extensionFileRoot;

        public SupplierAuthController(IConfiguration configuration, IWebHostEnvironment env)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection is not configured.");

            var complaintConfigured = configuration["FileStorage:ComplaintPath"];
            _complaintFileRoot = string.IsNullOrWhiteSpace(complaintConfigured)
                ? Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "EMSRole", "ComplainUploads"))
                : Path.GetFullPath(complaintConfigured);

            var emdConfigured = configuration["FileStorage:EmdDepositPath"];
            _emdFileRoot = string.IsNullOrWhiteSpace(emdConfigured)
                ? Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "EMSRole", "EMDUploads"))
                : Path.GetFullPath(emdConfigured);

            var sdConfigured = configuration["FileStorage:SdDetailPath"];
            _sdFileRoot = string.IsNullOrWhiteSpace(sdConfigured)
                ? Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "EMSRole", "Upload_SDdeatil"))
                : Path.GetFullPath(sdConfigured);

            var extensionConfigured = configuration["FileStorage:PoExtensionPath"];
            _extensionFileRoot = string.IsNullOrWhiteSpace(extensionConfigured)
                ? Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "EMSRole", "PO_Ext_Docs"))
                : Path.GetFullPath(extensionConfigured);

            Directory.CreateDirectory(_emdFileRoot);
            Directory.CreateDirectory(_sdFileRoot);
            Directory.CreateDirectory(_extensionFileRoot);
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

        /// <summary>po_supply.aspx — financial years, tenders, current year for logged-in supplier.</summary>
        [HttpGet("po-supply/filters/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierPoSupplyFilters(int userId)
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

                var financialYears = new List<FinancialYearOptionDto>
                {
                    new() { FinancialYearId = 0, Year = "--ALL--" }
                };

                using (SqlCommand yearCmd = new SqlCommand(
                    "SELECT financial_year_id, year FROM mas_financial_year ORDER BY OrderDP DESC", con))
                using (SqlDataReader yearReader = await yearCmd.ExecuteReaderAsync())
                {
                    while (await yearReader.ReadAsync())
                    {
                        financialYears.Add(new FinancialYearOptionDto
                        {
                            FinancialYearId = Convert.ToInt32(yearReader["financial_year_id"]),
                            Year = yearReader["year"]?.ToString() ?? string.Empty
                        });
                    }
                }

                int currentFinancialYearId = 0;
                using (SqlCommand currentYearCmd = new SqlCommand(
                    @"SELECT financial_year_id FROM mas_financial_year
                      WHERE GETDATE() BETWEEN from_date AND to_date", con))
                {
                    object? currentYear = await currentYearCmd.ExecuteScalarAsync();
                    if (currentYear != null && currentYear != DBNull.Value)
                        currentFinancialYearId = Convert.ToInt32(currentYear);
                }

                var tenders = new List<SupplierTenderOptionDto>
                {
                    new() { TenderId = 0, TenderNo = "--ALL--" }
                };

                using (SqlCommand tenderCmd = new SqlCommand(
                    @"SELECT a.tender_id, a.tender_no
                      FROM tenders a
                      INNER JOIN award_of_contract b ON a.tender_id = b.tender_id
                      WHERE b.supplier_id = @SupplierId
                      GROUP BY a.tender_id, a.tender_no, a.tender_date
                      ORDER BY a.tender_date", con))
                {
                    tenderCmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);
                    using SqlDataReader tenderReader = await tenderCmd.ExecuteReaderAsync();
                    while (await tenderReader.ReadAsync())
                    {
                        tenders.Add(new SupplierTenderOptionDto
                        {
                            TenderId = Convert.ToInt32(tenderReader["tender_id"]),
                            TenderNo = tenderReader["tender_no"]?.ToString() ?? string.Empty
                        });
                    }
                }

                return Ok(new SupplierPoSupplyFiltersDto
                {
                    SupplierId = supplierId.Value,
                    CurrentFinancialYearId = currentFinancialYearId,
                    FinancialYears = financialYears,
                    Tenders = tenders
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading PO supply filters.", error = ex.Message });
            }
        }

        /// <summary>po_supply.aspx — purchase orders grid.</summary>
        [HttpGet("po-supply/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierPoSupply(
            int userId,
            [FromQuery] int financialYearId = 0,
            [FromQuery] int tenderId = 0)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                const string sql = @"
SELECT pi.item_id, p.po_id, pi.no_of_consignee, pi.basic_rate, pi.percentage, pi.single_unit_price,
       pi.quantity, pi.totalPOvalue, p.OUTWARD_NO,
       p.po_no AS PO_NO,
       CASE WHEN p.soissueDT IS NULL THEN CONVERT(VARCHAR, p.po_date, 103)
            ELSE CONVERT(VARCHAR, p.soissueDT, 103) END AS po_date,
       t.tender_no, pi.CODE, pi.ITEM_NAME, p.status,
       CASE WHEN sd.sdname IS NOT NULL THEN sd.sdname ELSE 'Not Submitted' END AS SD,
       pDet.SubmissionStatus
FROM purchase_order p
INNER JOIN massuppliers b ON b.supplier_id = p.supplier_id
INNER JOIN mas_financial_year E ON E.financial_year_id = p.financial_year_id
INNER JOIN tenders t ON t.tender_id = p.tender_id
LEFT OUTER JOIN (
    SELECT COUNT(DISTINCT pi.consignee_id) AS no_of_consignee,
           R.item_code_as_per_tender AS CODE,
           R.item_name AS ITEM_NAME,
           c.basic_rate, c.percentage, c.single_unit_price,
           SUM(pi.quantity) AS quantity,
           c.single_unit_price * SUM(pi.quantity) AS totalPOvalue,
           pi.po_id, pi.item_id
    FROM po_items pi
    INNER JOIN purchase_order p ON p.po_id = pi.po_id
    INNER JOIN masitems R ON R.item_id = pi.item_id
    INNER JOIN contract_items c ON c.contract_item_id = pi.contract_item_id
    WHERE p.supplier_id = @SupplierId
    GROUP BY R.item_code_as_per_tender, R.item_name, c.basic_rate, c.percentage,
             c.single_unit_price, pi.po_id, pi.item_id
) pi ON pi.po_id = p.po_id
LEFT OUTER JOIN (
    SELECT s.sdname, ps.po_id
    FROM PO_SDDetails ps
    INNER JOIN massd s ON s.SDMode = ps.SDMode
    WHERE SubmissionStatus = 'Y'
) sd ON sd.po_id = p.po_id
LEFT OUTER JOIN PO_SDDetails pDet ON pDet.po_id = p.po_id
WHERE p.supplier_id = @SupplierId
  AND p.status IN ('Order Placed', 'Partially Received', 'Completed')
  AND (@FinancialYearId = 0 OR p.financial_year_id = @FinancialYearId)
  AND (@TenderId = 0 OR p.tender_id = @TenderId)
ORDER BY p.po_date DESC";

                var rows = new List<SupplierPoSupplyRowDto>();
                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                using SqlCommand cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);
                cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId);
                cmd.Parameters.AddWithValue("@TenderId", tenderId);

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new SupplierPoSupplyRowDto
                    {
                        PoId = ReadIntColumn(reader, "po_id"),
                        ItemId = ReadIntColumn(reader, "item_id"),
                        OutwardNo = ReadStringColumn(reader, "OUTWARD_NO", "outward_no"),
                        PoNo = ReadStringColumn(reader, "PO_NO", "po_no"),
                        PoDate = ReadStringColumn(reader, "po_date"),
                        ItemCode = ReadStringColumn(reader, "CODE", "code"),
                        ItemName = ReadStringColumn(reader, "ITEM_NAME", "item_name"),
                        BasicRate = ReadDecimalColumn(reader, "basic_rate"),
                        Percentage = ReadDecimalColumn(reader, "percentage"),
                        Quantity = ReadDecimalColumn(reader, "quantity"),
                        TotalPoValue = ReadDecimalColumn(reader, "totalPOvalue", "totalPOvalue"),
                        TenderNo = ReadStringColumn(reader, "tender_no"),
                        NoOfConsignee = ReadIntColumn(reader, "no_of_consignee"),
                        Status = ReadStringColumn(reader, "status"),
                        SdName = ReadStringColumn(reader, "SD", "sd"),
                        SubmissionStatus = ReadStringColumn(reader, "SubmissionStatus", "submissionStatus")
                    });
                }

                return Ok(rows);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading purchase orders.", error = ex.Message });
            }
        }

        /// <summary>SDdetailSupplier.aspx — load SD detail form.</summary>
        [HttpGet("po-sd-detail/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierPoSdDetail(
            int userId,
            [FromQuery] int poId,
            [FromQuery] int itemId,
            [FromQuery] decimal grossValue = 0)
        {
            if (userId <= 0 || poId <= 0 || itemId <= 0)
                return BadRequest(new { message = "Invalid request." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                if (!await PoBelongsToSupplierAsync(con, poId, supplierId.Value))
                    return NotFound(new { message = "Purchase order not found." });

                string equipmentName = string.Empty;
                const string itemSql = "SELECT item_name FROM masitems WHERE item_id = @ItemId";
                using (SqlCommand itemCmd = new SqlCommand(itemSql, con))
                {
                    itemCmd.Parameters.AddWithValue("@ItemId", itemId);
                    object? itemResult = await itemCmd.ExecuteScalarAsync();
                    equipmentName = itemResult?.ToString() ?? string.Empty;
                }

                decimal sdAmount = Math.Round(grossValue * 0.05m, 0, MidpointRounding.AwayFromZero);
                var paymentModes = await LoadSdPaymentModesAsync(con);

                bool hasExisting = false;
                bool hasFile = false;
                string? paymentMode = null;
                string? issueDate = null;
                string? maturityDate = null;
                string? documentNo = null;

                const string sdSql = @"
SELECT SDMode,
       CONVERT(VARCHAR, IssueDT, 103) AS IssueDT,
       CONVERT(VARCHAR, MaturityDT, 103) AS MaturityDT,
       SDDoctPath,
       DocumentNo
FROM PO_SDDetails
WHERE po_id = @PoId";

                using (SqlCommand sdCmd = new SqlCommand(sdSql, con))
                {
                    sdCmd.Parameters.AddWithValue("@PoId", poId);
                    using SqlDataReader reader = await sdCmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        hasExisting = true;
                        paymentMode = ReadStringColumn(reader, "SDMode");
                        issueDate = ReadStringColumn(reader, "IssueDT");
                        maturityDate = ReadStringColumn(reader, "MaturityDT");
                        documentNo = ReadStringColumn(reader, "DocumentNo");
                        string docPath = ReadStringColumn(reader, "SDDoctPath");
                        hasFile = !string.IsNullOrWhiteSpace(docPath);
                        if (hasExisting && sdAmount <= 0)
                        {
                            // gross value may be omitted on edit reload — amount still shown from DB on client
                        }
                    }
                }

                return Ok(new SupplierPoSdDetailDto
                {
                    PoId = poId,
                    SupplierId = supplierId.Value,
                    ItemId = itemId,
                    EquipmentName = equipmentName,
                    GrossValue = grossValue,
                    SdAmount = sdAmount,
                    HasExisting = hasExisting,
                    HasFile = hasFile,
                    PaymentMode = paymentMode,
                    IssueDate = issueDate,
                    MaturityDate = string.IsNullOrWhiteSpace(maturityDate) ? null : maturityDate,
                    DocumentNo = documentNo,
                    PaymentModes = paymentModes,
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading SD detail.", error = ex.Message });
            }
        }

        /// <summary>SDdetailSupplier.aspx — submit new SD detail.</summary>
        [HttpPost("po-sd-detail/by-user/{userId:int}")]
        public async Task<IActionResult> SaveSupplierPoSdDetail(
            int userId,
            [FromForm] int poId,
            [FromForm] int itemId,
            [FromForm] int supplierId,
            [FromForm] string paymentMode,
            [FromForm] string issueDate,
            [FromForm] decimal sdAmount,
            [FromForm] string documentNo,
            [FromForm] string? maturityDate,
            IFormFile? file)
        {
            if (userId <= 0 || poId <= 0 || itemId <= 0 || supplierId <= 0)
                return BadRequest(new { message = "Invalid request." });

            if (string.IsNullOrWhiteSpace(paymentMode) || paymentMode == "0")
                return BadRequest(new { message = "Please select Payment mode." });

            if (string.IsNullOrWhiteSpace(issueDate))
                return BadRequest(new { message = "Please fill Issue Date" });

            if (sdAmount <= 0)
                return BadRequest(new { message = "Please fill Amount" });

            if (string.IsNullOrWhiteSpace(documentNo))
                return BadRequest(new { message = "Please SD Document Ref. No" });

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Please select document to be uplaoded." });

            if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Please upload pdf file only." });

            if (file.Length > 3_000_000)
                return BadRequest(new { message = "Your can't upload file more than 3mb." });

            try
            {
                int? loggedInSupplierId = await ResolveSupplierIdForUserAsync(userId);
                if (loggedInSupplierId == null || loggedInSupplierId.Value != supplierId)
                    return NotFound(new { message = "Supplier user not found." });

                if (!TryParseSdDate(issueDate, out DateTime parsedIssueDate))
                    return BadRequest(new { message = "Invalid issue date format." });

                DateTime? parsedMaturityDate = null;
                if (!string.IsNullOrWhiteSpace(maturityDate))
                {
                    if (!TryParseSdDate(maturityDate, out DateTime maturityParsed))
                        return BadRequest(new { message = "Invalid maturity date format." });

                    if (parsedIssueDate > maturityParsed)
                        return BadRequest(new { message = "Issue Date Cannot be greater than Maturity Date." });

                    parsedMaturityDate = maturityParsed;
                }

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                if (!await PoBelongsToSupplierAsync(con, poId, supplierId))
                    return NotFound(new { message = "Purchase order not found." });

                const string existsSql = "SELECT 1 FROM PO_SDDetails WHERE po_id = @PoId";
                using (SqlCommand existsCmd = new SqlCommand(existsSql, con))
                {
                    existsCmd.Parameters.AddWithValue("@PoId", poId);
                    object? exists = await existsCmd.ExecuteScalarAsync();
                    if (exists != null && exists != DBNull.Value)
                        return BadRequest(new { message = "SD detail already exists for this PO." });
                }

                string fileName = $"{itemId}-{poId}-{supplierId}-{paymentMode}-SdDoc.pdf";
                string virtualPath = $"~/Upload_SDdeatil/{fileName}";
                string savePath = Path.Combine(_sdFileRoot, fileName);
                await using (var stream = new FileStream(savePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                string insertSql = parsedMaturityDate.HasValue
                    ? @"INSERT INTO PO_SDDetails(po_id, SDMode, SDAmount, SDDoctPath, entryDT, IssueDT, MaturityDT, SubmissionStatus, DocumentNo)
VALUES (@PoId, @SdMode, @SdAmount, @DocPath, @EntryDt, @IssueDt, @MaturityDt, 'Y', @DocumentNo)"
                    : @"INSERT INTO PO_SDDetails(po_id, SDMode, SDAmount, SDDoctPath, entryDT, IssueDT, SubmissionStatus, DocumentNo)
VALUES (@PoId, @SdMode, @SdAmount, @DocPath, @EntryDt, @IssueDt, 'Y', @DocumentNo)";

                using SqlCommand insertCmd = new SqlCommand(insertSql, con);
                insertCmd.Parameters.AddWithValue("@PoId", poId);
                insertCmd.Parameters.AddWithValue("@SdMode", paymentMode.Trim());
                insertCmd.Parameters.AddWithValue("@SdAmount", sdAmount);
                insertCmd.Parameters.AddWithValue("@DocPath", virtualPath);
                insertCmd.Parameters.AddWithValue("@EntryDt", DateTime.Now.Date);
                insertCmd.Parameters.AddWithValue("@IssueDt", parsedIssueDate);
                insertCmd.Parameters.AddWithValue("@DocumentNo", documentNo.Trim());
                if (parsedMaturityDate.HasValue)
                    insertCmd.Parameters.AddWithValue("@MaturityDt", parsedMaturityDate.Value);

                await insertCmd.ExecuteNonQueryAsync();
                return Ok(new { message = "Successfully Saved." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error saving SD detail.", error = ex.Message });
            }
        }

        /// <summary>SDdetailSupplier.aspx — update existing SD detail.</summary>
        [HttpPost("po-sd-detail/update/by-user/{userId:int}")]
        public async Task<IActionResult> UpdateSupplierPoSdDetail(
            int userId,
            [FromForm] int poId,
            [FromForm] int itemId,
            [FromForm] int supplierId,
            [FromForm] string paymentMode,
            [FromForm] string issueDate,
            [FromForm] decimal sdAmount,
            [FromForm] string? maturityDate,
            [FromForm] string fileMode,
            IFormFile? file)
        {
            if (userId <= 0 || poId <= 0 || itemId <= 0 || supplierId <= 0)
                return BadRequest(new { message = "Invalid request." });

            if (string.IsNullOrWhiteSpace(paymentMode) || paymentMode == "0")
                return BadRequest(new { message = "Please select Payment mode." });

            if (string.IsNullOrWhiteSpace(issueDate))
                return BadRequest(new { message = "Please fill Issue Date" });

            if (sdAmount <= 0)
                return BadRequest(new { message = "Please fill Amount" });

            bool uploadNewFile = string.Equals(fileMode, "UPLOAD", StringComparison.OrdinalIgnoreCase);
            if (uploadNewFile)
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { message = "Please select document to be uplaoded." });

                if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { message = "Please upload pdf file only." });

                if (file.Length > 3_000_000)
                    return BadRequest(new { message = "Your can't upload file more than 3mb." });
            }

            try
            {
                int? loggedInSupplierId = await ResolveSupplierIdForUserAsync(userId);
                if (loggedInSupplierId == null || loggedInSupplierId.Value != supplierId)
                    return NotFound(new { message = "Supplier user not found." });

                if (!TryParseSdDate(issueDate, out DateTime parsedIssueDate))
                    return BadRequest(new { message = "Invalid issue date format." });

                DateTime? parsedMaturityDate = null;
                if (!string.IsNullOrWhiteSpace(maturityDate))
                {
                    if (!TryParseSdDate(maturityDate, out DateTime maturityParsed))
                        return BadRequest(new { message = "Invalid maturity date format." });

                    if (parsedIssueDate > maturityParsed)
                        return BadRequest(new { message = "Issue Date Cannot be greater than Maturity Date." });

                    parsedMaturityDate = maturityParsed;
                }

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                if (!await PoBelongsToSupplierAsync(con, poId, supplierId))
                    return NotFound(new { message = "Purchase order not found." });

                string? virtualPath = null;
                if (uploadNewFile)
                {
                    string fileName = $"{itemId}-{poId}-{supplierId}-{paymentMode}-SdDoc.pdf";
                    virtualPath = $"~/Upload_SDdeatil/{fileName}";
                    string savePath = Path.Combine(_sdFileRoot, fileName);
                    await using (var stream = new FileStream(savePath, FileMode.Create))
                    {
                        await file!.CopyToAsync(stream);
                    }
                }

                string updateSql;
                if (uploadNewFile && parsedMaturityDate.HasValue)
                {
                    updateSql = @"UPDATE PO_SDDetails
SET SDMode = @SdMode, SDAmount = @SdAmount, SDDoctPath = @DocPath, entryDT = @EntryDt,
    IssueDT = @IssueDt, MaturityDT = @MaturityDt, SubmissionStatus = 'Y'
WHERE po_id = @PoId";
                }
                else if (uploadNewFile)
                {
                    updateSql = @"UPDATE PO_SDDetails
SET SDMode = @SdMode, SDAmount = @SdAmount, SDDoctPath = @DocPath, entryDT = @EntryDt,
    IssueDT = @IssueDt, MaturityDT = NULL, SubmissionStatus = 'Y'
WHERE po_id = @PoId";
                }
                else if (parsedMaturityDate.HasValue)
                {
                    updateSql = @"UPDATE PO_SDDetails
SET SDMode = @SdMode, SDAmount = @SdAmount, entryDT = @EntryDt,
    IssueDT = @IssueDt, MaturityDT = @MaturityDt, SubmissionStatus = 'Y'
WHERE po_id = @PoId";
                }
                else
                {
                    updateSql = @"UPDATE PO_SDDetails
SET SDMode = @SdMode, SDAmount = @SdAmount, entryDT = @EntryDt,
    IssueDT = @IssueDt, MaturityDT = NULL, SubmissionStatus = 'Y'
WHERE po_id = @PoId";
                }

                using SqlCommand updateCmd = new SqlCommand(updateSql, con);
                updateCmd.Parameters.AddWithValue("@PoId", poId);
                updateCmd.Parameters.AddWithValue("@SdMode", paymentMode.Trim());
                updateCmd.Parameters.AddWithValue("@SdAmount", sdAmount);
                updateCmd.Parameters.AddWithValue("@EntryDt", DateTime.Now.Date);
                updateCmd.Parameters.AddWithValue("@IssueDt", parsedIssueDate);
                if (uploadNewFile)
                    updateCmd.Parameters.AddWithValue("@DocPath", virtualPath!);
                if (parsedMaturityDate.HasValue)
                    updateCmd.Parameters.AddWithValue("@MaturityDt", parsedMaturityDate.Value);

                int rows = await updateCmd.ExecuteNonQueryAsync();
                if (rows == 0)
                    return NotFound(new { message = "SD detail not found." });

                return Ok(new { message = "Successfully Saved." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating SD detail.", error = ex.Message });
            }
        }

        /// <summary>SDdetailSupplier.aspx — SD document download.</summary>
        [HttpGet("po-sd-detail/file/by-user/{userId:int}")]
        public async Task<IActionResult> DownloadSupplierPoSdFile(int userId, [FromQuery] int poId)
        {
            if (userId <= 0 || poId <= 0)
                return BadRequest(new { message = "Invalid request." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                if (!await PoBelongsToSupplierAsync(con, poId, supplierId.Value))
                    return NotFound(new { message = "Purchase order not found." });

                const string sql = "SELECT SDDoctPath FROM PO_SDDetails WHERE po_id = @PoId";
                using SqlCommand cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@PoId", poId);

                object? result = await cmd.ExecuteScalarAsync();
                if (result == null || result == DBNull.Value)
                    return NotFound(new { message = "File Not Found." });

                string? virtualPath = result.ToString();
                string physicalPath = ResolveLegacyUploadPath(virtualPath, _sdFileRoot);
                if (!System.IO.File.Exists(physicalPath))
                    return NotFound(new { message = "File Not Found." });

                string downloadName = Path.GetFileName(physicalPath);
                return PhysicalFile(physicalPath, "application/pdf", downloadName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error downloading SD document.", error = ex.Message });
            }
        }

        /// <summary>ApplyForExtension.aspx — load extension page.</summary>
        [HttpGet("po-extension/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierPoExtensionPage(int userId, [FromQuery] int poId)
        {
            if (userId <= 0 || poId <= 0)
                return BadRequest(new { message = "Invalid request." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                if (!await PoBelongsToSupplierAsync(con, poId, supplierId.Value))
                    return NotFound(new { message = "Purchase order not found." });

                bool hasSdRecord = await PoSdDetailExistsAsync(con, poId);
                if (!hasSdRecord)
                    return NotFound(new { message = "PO Id not found." });

                const string headerSql = @"
SELECT TOP 1
       m.item_name,
       p.PO_NO,
       CONVERT(VARCHAR, p.po_date, 103) AS po_date,
       PT.tranche_days,
       CONVERT(VARCHAR, DATEADD(DAY, PT.tranche_days, p.po_date), 103) AS po_end_date
FROM purchase_order p
INNER JOIN po_items pi ON pi.po_id = p.po_id
INNER JOIN masitems m ON m.item_id = pi.item_id
INNER JOIN po_tranche PT ON PT.po_id = p.po_id
WHERE p.po_id = @PoId AND p.supplier_id = @SupplierId";

                string equipmentName = string.Empty;
                string poNo = string.Empty;
                string poDate = string.Empty;
                int supplyDays = 0;
                string poEndDate = string.Empty;

                using (SqlCommand headerCmd = new SqlCommand(headerSql, con))
                {
                    headerCmd.Parameters.AddWithValue("@PoId", poId);
                    headerCmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);
                    using SqlDataReader reader = await headerCmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                        return NotFound(new { message = "PO Id not found." });

                    equipmentName = ReadStringColumn(reader, "item_name");
                    poNo = ReadStringColumn(reader, "PO_NO");
                    poDate = ReadStringColumn(reader, "po_date");
                    supplyDays = ReadIntColumn(reader, "tranche_days");
                    poEndDate = ReadStringColumn(reader, "po_end_date");
                }

                string baseEndDate = await GetExtensionBaseEndDateAsync(con, poId, poEndDate);
                bool hasPending = await HasPendingPoExtensionAsync(con, poId);
                var extensions = await LoadPoExtensionsAsync(con, poId);

                return Ok(new SupplierPoExtensionPageDto
                {
                    PoId = poId,
                    SupplierId = supplierId.Value,
                    EquipmentName = equipmentName,
                    PoNo = poNo,
                    PoDate = poDate,
                    SupplyDays = supplyDays,
                    PoEndDate = poEndDate,
                    BaseEndDate = baseEndDate,
                    HasSdRecord = hasSdRecord,
                    CanApply = !hasPending,
                    HasPendingExtension = hasPending,
                    Extensions = extensions,
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading extension page.", error = ex.Message });
            }
        }

        /// <summary>ApplyForExtension.aspx — submit extension request.</summary>
        [HttpPost("po-extension/by-user/{userId:int}")]
        public async Task<IActionResult> SaveSupplierPoExtension(
            int userId,
            [FromForm] int poId,
            [FromForm] int extensionDays,
            [FromForm] string letterDate,
            [FromForm] string remark,
            IFormFile? file)
        {
            if (userId <= 0 || poId <= 0)
                return BadRequest(new { message = "Invalid request." });

            if (extensionDays <= 0)
                return BadRequest(new { message = "Extension Days Should Not be Zero." });

            if (string.IsNullOrWhiteSpace(letterDate))
                return BadRequest(new { message = "Letter Date Should Not be Empty." });

            if (string.IsNullOrWhiteSpace(remark))
                return BadRequest(new { message = "Remark Should Not be Empty" });

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "You are not selected any File yet." });

            if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Please upload pdf file only." });

            if (file.Length > 1_000_000)
                return BadRequest(new { message = "Your can't upload file more than 1mb." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                if (!TryParseLetterDate(letterDate, out DateTime parsedLetterDate))
                    return BadRequest(new { message = "Invalid Format. Use DD-MM-YYYY" });

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                if (!await PoBelongsToSupplierAsync(con, poId, supplierId.Value))
                    return NotFound(new { message = "Purchase order not found." });

                if (!await PoSdDetailExistsAsync(con, poId))
                    return NotFound(new { message = "PO Id not found." });

                if (await HasPendingPoExtensionAsync(con, poId))
                    return BadRequest(new { message = "An extension request is already pending for this PO." });

                string poEndDateDisplay = string.Empty;
                const string headerSql = @"
SELECT TOP 1 CONVERT(VARCHAR, DATEADD(DAY, PT.tranche_days, p.po_date), 103) AS po_end_date
FROM purchase_order p
INNER JOIN po_tranche PT ON PT.po_id = p.po_id
WHERE p.po_id = @PoId AND p.supplier_id = @SupplierId";

                using (SqlCommand headerCmd = new SqlCommand(headerSql, con))
                {
                    headerCmd.Parameters.AddWithValue("@PoId", poId);
                    headerCmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);
                    object? headerResult = await headerCmd.ExecuteScalarAsync();
                    poEndDateDisplay = headerResult?.ToString() ?? string.Empty;
                }

                string baseEndDateDisplay = await GetExtensionBaseEndDateAsync(con, poId, poEndDateDisplay);
                if (!TryParseSdDate(baseEndDateDisplay, out DateTime baseEndDate))
                    return BadRequest(new { message = "Unable to determine PO end date." });

                int randomSuffix = Random.Shared.Next(100, 1000);
                string letterNo = $"{randomSuffix}-{poId}-{supplierId.Value}-Extension";
                string fileName = $"{letterNo}.pdf";
                string virtualPath = $"~/PO_Ext_Docs/{fileName}";
                string savePath = Path.Combine(_extensionFileRoot, fileName);

                await using (var stream = new FileStream(savePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                const string insertSql = @"
INSERT INTO PO_extension_detail
    (po_id, remark, days, extended_date, po_end_date, path, letter_date, letter_no, sys_gen_apply_date, status, isrequestedby)
VALUES
    (@PoId, @Remark, @Days, DATEADD(DAY, @Days, @PoEndDate), @PoEndDate, @Path, @LetterDate, @LetterNo, @SysDate, 'P', 'S')";

                using SqlCommand insertCmd = new SqlCommand(insertSql, con);
                insertCmd.Parameters.AddWithValue("@PoId", poId);
                insertCmd.Parameters.AddWithValue("@Remark", remark.Trim());
                insertCmd.Parameters.AddWithValue("@Days", extensionDays);
                insertCmd.Parameters.AddWithValue("@PoEndDate", baseEndDate);
                insertCmd.Parameters.AddWithValue("@Path", virtualPath);
                insertCmd.Parameters.AddWithValue("@LetterDate", parsedLetterDate);
                insertCmd.Parameters.AddWithValue("@LetterNo", letterNo);
                insertCmd.Parameters.AddWithValue("@SysDate", DateTime.Now.Date);

                await insertCmd.ExecuteNonQueryAsync();
                return Ok(new { message = "Successfully Saved." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error saving extension request.", error = ex.Message });
            }
        }

        /// <summary>ApplyForExtension.aspx — extension document download.</summary>
        [HttpGet("po-extension/file/by-user/{userId:int}")]
        public async Task<IActionResult> DownloadSupplierPoExtensionFile(int userId, [FromQuery] int extensionId)
        {
            if (userId <= 0 || extensionId <= 0)
                return BadRequest(new { message = "Invalid request." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                const string sql = @"
SELECT ped.path
FROM PO_extension_detail ped
INNER JOIN purchase_order p ON p.po_id = ped.po_id
WHERE ped.extensionId = @ExtensionId AND p.supplier_id = @SupplierId";

                string? virtualPath = null;
                using (SqlConnection con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();
                    using SqlCommand cmd = new SqlCommand(sql, con);
                    cmd.Parameters.AddWithValue("@ExtensionId", extensionId);
                    cmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);

                    object? result = await cmd.ExecuteScalarAsync();
                    if (result == null || result == DBNull.Value)
                        return NotFound(new { message = "File Not Found." });

                    virtualPath = result.ToString();
                }

                string physicalPath = ResolveLegacyUploadPath(virtualPath, _extensionFileRoot);
                if (!System.IO.File.Exists(physicalPath))
                    return NotFound(new { message = "File Not Found." });

                return PhysicalFile(physicalPath, "application/pdf", Path.GetFileName(physicalPath));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error downloading extension document.", error = ex.Message });
            }
        }

        /// <summary>po_supplyDispatch.aspx — dispatch desk grid.</summary>
        [HttpGet("po-supply-dispatch/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierPoDispatch(
            int userId,
            [FromQuery] int financialYearId = 0,
            [FromQuery] int tenderId = 0)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                const string sql = @"
SELECT CODE, ITEM_NAME, OUTWARD_NO, po_date, PO_NO,
       SUM(quantity) AS quantity,
       COUNT(location_id) AS no_of_consignee,
       basic_rate, percentage, single_unit_price,
       SUM(totalPOvalue) AS totalPOvalue,
       PO_ID, Squantity,
       CASE WHEN SUM(quantity) = Squantity THEN 'Complete Supplied'
            WHEN Squantity > 0 AND SUM(quantity) != Squantity THEN 'Partial Spplied'
            ELSE 'Not Supplied' END AS SupplyStatus
FROM (
    SELECT m.location_id, R.item_code_as_per_tender AS CODE, R.item_name AS ITEM_NAME,
           p.OUTWARD_NO, CONVERT(VARCHAR, p.po_date, 103) AS po_date,
           pi.quantity, c.basic_rate, c.percentage, c.single_unit_price,
           c.single_unit_price * pi.quantity AS totalPOvalue,
           p.PO_NO, p.PO_ID, ISNULL(di.Squantity, 0) AS Squantity
    FROM po_items pi
    INNER JOIN masitems R ON R.item_id = pi.item_id
    INNER JOIN maslocations m ON m.location_id = pi.consignee_id
    INNER JOIN purchase_order p ON pi.po_id = p.po_id
    INNER JOIN massuppliers b ON p.supplier_id = b.supplier_id
    LEFT OUTER JOIN (
        SELECT SUM(i.Supplyqty) AS Squantity, d.po_id
        FROM SupplierDispatch d
        INNER JOIN Issue_item_details i ON d.Issue_id = i.Issue_id
        WHERE d.status = 'C'
        GROUP BY d.po_id
    ) di ON di.po_id = pi.po_id
    LEFT OUTER JOIN (
        SELECT a.supplier_id, a.tender_id, ci.item_id, ci.basic_rate, ci.percentage,
               ci.single_unit_price
        FROM award_of_contract a
        INNER JOIN contract_items ci ON ci.award_of_contract_id = a.award_of_contract_id
    ) c ON c.item_id = pi.item_id AND c.tender_id = p.tender_id AND c.supplier_id = p.supplier_id
    WHERE b.supplier_id = @SupplierId
      AND p.status IN ('Order Placed', 'Partially Received', 'Completed')
      AND (@FinancialYearId = 0 OR p.financial_year_id = @FinancialYearId)
      AND (@TenderId = 0 OR p.tender_id = @TenderId)
) a
GROUP BY ITEM_NAME, OUTWARD_NO, po_date, PO_NO, CODE, basic_rate, percentage,
         single_unit_price, PO_ID, Squantity
ORDER BY po_date";

                var rows = new List<SupplierPoDispatchRowDto>();
                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                using SqlCommand cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);
                cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId);
                cmd.Parameters.AddWithValue("@TenderId", tenderId);

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new SupplierPoDispatchRowDto
                    {
                        PoId = ReadIntColumn(reader, "PO_ID", "po_id"),
                        OutwardNo = ReadStringColumn(reader, "OUTWARD_NO", "outward_no"),
                        PoNo = ReadStringColumn(reader, "PO_NO", "po_no"),
                        PoDate = ReadStringColumn(reader, "po_date"),
                        ItemCode = ReadStringColumn(reader, "CODE", "code"),
                        ItemName = ReadStringColumn(reader, "ITEM_NAME", "item_name"),
                        BasicRate = ReadDecimalColumn(reader, "basic_rate"),
                        Percentage = ReadDecimalColumn(reader, "percentage"),
                        NoOfConsignee = ReadIntColumn(reader, "no_of_consignee"),
                        Quantity = ReadDecimalColumn(reader, "quantity"),
                        TotalPoValue = ReadDecimalColumn(reader, "totalPOvalue"),
                        DispatchedQty = ReadDecimalColumn(reader, "Squantity", "squantity"),
                        SupplyStatus = ReadStringColumn(reader, "SupplyStatus", "supplyStatus")
                    });
                }

                return Ok(rows);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading dispatch orders.", error = ex.Message });
            }
        }

        /// <summary>po_supply_edit.aspx — dispatch equipment desk for a PO.</summary>
        [HttpGet("po-supply-edit/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierPoDispatchEdit(
            int userId,
            [FromQuery] int poId)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });
            if (poId <= 0)
                return BadRequest(new { message = "PO id is required." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                const string sql = @"
SELECT a.po_id, a.po_item_id, a.item_id, a.quantity, a.consignee_id,
       c.item_name, c.item_code_as_per_tender AS item_code, c.categoryid,
       x.single_unit_price, c1.location_name,
       x.single_unit_price * a.quantity AS Total_Price,
       CONVERT(VARCHAR, b.po_date, 103) AS po_date, b.PO_NO
FROM po_items a
INNER JOIN purchase_order b ON a.po_id = b.po_id
LEFT OUTER JOIN maslocations c1 ON c1.location_id = a.consignee_id
LEFT OUTER JOIN masitems c ON a.item_id = c.item_id
LEFT OUTER JOIN (
    SELECT D.item_id, F.tender_id, F.supplier_id, D.single_unit_price
    FROM award_of_contract F
    INNER JOIN contract_items D ON D.award_of_contract_id = F.award_of_contract_id
) x ON x.item_id = a.item_id AND x.tender_id = b.tender_id AND x.supplier_id = b.supplier_id
WHERE a.po_id = @PoId AND b.supplier_id = @SupplierId
ORDER BY c1.location_name";

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                string poNo = string.Empty;
                string poDate = string.Empty;
                var pendingRows = new List<(SupplierPoDispatchEditRowDto Row, int ConsigneeId)>();

                using (SqlCommand cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@PoId", poId);
                    cmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);

                    using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        if (string.IsNullOrEmpty(poNo))
                        {
                            poNo = ReadStringColumn(reader, "PO_NO", "po_no");
                            poDate = ReadStringColumn(reader, "po_date");
                        }

                        int consigneeId = ReadIntColumn(reader, "consignee_id");
                        pendingRows.Add((new SupplierPoDispatchEditRowDto
                        {
                            PoItemId = ReadIntColumn(reader, "po_item_id"),
                            PoId = ReadIntColumn(reader, "po_id"),
                            ItemId = ReadIntColumn(reader, "item_id"),
                            ConsigneeId = consigneeId,
                            CategoryId = ReadIntColumn(reader, "categoryid"),
                            ItemName = ReadStringColumn(reader, "item_name"),
                            ItemCode = ReadStringColumn(reader, "item_code"),
                            LocationName = ReadStringColumn(reader, "location_name"),
                            UnitPrice = ReadDecimalColumn(reader, "single_unit_price"),
                            Quantity = ReadDecimalColumn(reader, "quantity"),
                            TotalPrice = ReadDecimalColumn(reader, "Total_Price", "total_price"),
                        }, consigneeId));
                    }
                }

                if (pendingRows.Count == 0)
                    return NotFound(new { message = "Purchase order not found for this supplier." });

                var rows = new List<SupplierPoDispatchEditRowDto>();
                foreach (var entry in pendingRows)
                {
                    entry.Row.CanAddDispatch = await CanAddDispatchAsync(con, poId, entry.ConsigneeId);
                    entry.Row.Batches = await LoadDispatchEditBatchesAsync(con, poId, entry.ConsigneeId);
                    rows.Add(entry.Row);
                }

                return Ok(new SupplierPoDispatchEditDto
                {
                    PoId = poId,
                    PoNo = poNo,
                    PoDate = poDate,
                    Rows = rows
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading dispatch equipment desk.", error = ex.Message });
            }
        }

        /// <summary>rptDispatchDetails.aspx — printable dispatch report for a completed issue.</summary>
        [HttpGet("dispatch-report/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierDispatchReport(
            int userId,
            [FromQuery] int poId,
            [FromQuery] int locId,
            [FromQuery] int issueId)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });
            if (poId <= 0 || locId <= 0 || issueId <= 0)
                return BadRequest(new { message = "PO id, location id and issue id are required." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                if (!await PoBelongsToSupplierAsync(con, poId, supplierId.Value))
                    return NotFound(new { message = "Purchase order not found for this supplier." });

                const string headerSql = @"
SELECT a.PO_ID, CONVERT(VARCHAR, a.po_date, 103) AS po_date, a.PO_NO,
       c.TENDER_NO, R.item_name AS ITEM_NAME, R.item_code_as_per_tender AS CODE,
       pi.quantity AS POQTY, l.location_name,
       pi.percentage, pi.basicrate, pi.totalbasicPrice, pi.totalprice,
       ci.make, ci.model, pt.tranche_days
FROM purchase_order a
LEFT OUTER JOIN po_items pi ON pi.po_id = a.po_id
LEFT OUTER JOIN po_tranche pt ON pt.po_id = a.po_id AND pt.po_id = pi.po_id
LEFT OUTER JOIN contract_items ci ON ci.contract_item_id = pi.contract_item_id AND ci.item_id = pi.item_id
LEFT OUTER JOIN masitems R ON R.item_id = pi.item_id
LEFT OUTER JOIN tenders c ON c.tender_id = a.tender_id
LEFT OUTER JOIN maslocations l ON l.location_id = pi.consignee_id
WHERE a.po_id = @PoId AND pi.consignee_id = @LocId AND a.supplier_id = @SupplierId";

                const string dispatchSql = @"
SELECT d.Issue_id,
       d.remarks,
       CONVERT(VARCHAR, d.challan_date, 103) AS challandate,
       d.challan_no,
       CONVERT(VARCHAR, d.dispatch_date, 103) AS dispatch_date,
       CONVERT(VARCHAR, d.invoice_date, 103) AS invoice_date,
       d.dispatch_no,
       d.invoice_no
FROM SupplierDispatch d
INNER JOIN Issue_item_details i ON i.Issue_id = d.Issue_id
WHERE d.po_id = @PoId AND d.location_id = @LocId AND d.Issue_id = @IssueId";

                SupplierDispatchReportDto? report = null;

                using (SqlCommand headerCmd = new SqlCommand(headerSql, con))
                {
                    headerCmd.Parameters.AddWithValue("@PoId", poId);
                    headerCmd.Parameters.AddWithValue("@LocId", locId);
                    headerCmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);

                    using SqlDataReader reader = await headerCmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                        return NotFound(new { message = "Dispatch report header not found." });

                    report = new SupplierDispatchReportDto
                    {
                        PoId = poId,
                        LocationId = locId,
                        IssueId = issueId,
                        ItemCode = ReadStringColumn(reader, "CODE", "code"),
                        ItemName = ReadStringColumn(reader, "ITEM_NAME", "item_name"),
                        PoNo = ReadStringColumn(reader, "PO_NO", "po_no"),
                        PoDate = ReadStringColumn(reader, "po_date"),
                        TenderNo = ReadStringColumn(reader, "TENDER_NO", "tender_no"),
                        ConsigneeName = ReadStringColumn(reader, "location_name"),
                        ModelNo = ReadStringColumn(reader, "model"),
                        Make = ReadStringColumn(reader, "make"),
                        BasicRate = ReadDecimalColumn(reader, "basicrate"),
                        TotalNetPoValue = ReadDecimalColumn(reader, "totalbasicPrice"),
                        TotalGrossPoValue = ReadDecimalColumn(reader, "totalprice"),
                        PoQtyForConsignee = ReadDecimalColumn(reader, "POQTY", "poqty"),
                        SupplyDays = ReadStringColumn(reader, "tranche_days"),
                        TaxPercent = ReadDecimalColumn(reader, "percentage"),
                    };
                }

                using (SqlCommand dispatchCmd = new SqlCommand(dispatchSql, con))
                {
                    dispatchCmd.Parameters.AddWithValue("@PoId", poId);
                    dispatchCmd.Parameters.AddWithValue("@LocId", locId);
                    dispatchCmd.Parameters.AddWithValue("@IssueId", issueId);

                    using SqlDataReader reader = await dispatchCmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                        return NotFound(new { message = "Dispatch issue not found." });

                    report!.DispatchNo = ReadStringColumn(reader, "dispatch_no");
                    report.DispatchDate = ReadStringColumn(reader, "dispatch_date");
                    report.ChallanNo = ReadStringColumn(reader, "challan_no");
                    report.ChallanDate = ReadStringColumn(reader, "challandate");
                    report.InvoiceNo = ReadStringColumn(reader, "invoice_no");
                    report.InvoiceDate = ReadStringColumn(reader, "invoice_date");
                    report.Remarks = ReadStringColumn(reader, "remarks");
                }

                return Ok(report);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading dispatch report.", error = ex.Message });
            }
        }

        /// <summary>Facilitypo_supply_ReceiptSUP.aspx — financial years for receipt desk.</summary>
        [HttpGet("po-supply-receipt/filters/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierPoReceiptFilters(int userId)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                var financialYears = new List<FinancialYearOptionDto>
                {
                    new() { FinancialYearId = 0, Year = "Select PO Year" }
                };

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                using SqlCommand yearCmd = new SqlCommand(
                    @"SELECT financial_year_id, year FROM mas_financial_year
                      WHERE financial_year_id NOT IN (5, 12, 13, 11)
                      ORDER BY financial_year_id DESC", con);
                using SqlDataReader yearReader = await yearCmd.ExecuteReaderAsync();
                while (await yearReader.ReadAsync())
                {
                    financialYears.Add(new FinancialYearOptionDto
                    {
                        FinancialYearId = Convert.ToInt32(yearReader["financial_year_id"]),
                        Year = yearReader["year"]?.ToString() ?? string.Empty
                    });
                }

                return Ok(new SupplierPoReceiptFiltersDto
                {
                    SupplierId = supplierId.Value,
                    FinancialYears = financialYears
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading receipt filters.", error = ex.Message });
            }
        }

        /// <summary>Facilitypo_supply_ReceiptSUP.aspx — PO dropdown by status filter.</summary>
        [HttpGet("po-supply-receipt/pos/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierPoReceiptOptions(
            int userId,
            [FromQuery] int financialYearId = 0,
            [FromQuery] string poType = "All")
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                string statusFilter = poType switch
                {
                    "PRI" => "Pending For Receipt/Installation",
                    "PD" => "Dispatch Pending",
                    "C" => "Installation Completed",
                    _ => string.Empty
                };

                const string sql = @"
SELECT b.outward_no + '-' + b.po_no + '-DT-' + CONVERT(VARCHAR, b.po_date, 103) AS data,
       b.po_id,
       CASE WHEN SUM(a.quantity) = ISNULL(ins.insqty, 0) AND SUM(a.quantity) = ISNULL(sup.Supplyqty, 0)
            THEN 'Installation Completed'
            WHEN ISNULL(sup.Supplyqty, 0) = 0 OR SUM(a.quantity) > ISNULL(sup.Supplyqty, 0)
            THEN 'Dispatch Pending'
            ELSE 'Pending For Receipt/Installation' END AS status
FROM po_items a
INNER JOIN purchase_order b ON a.po_id = b.po_id
LEFT OUTER JOIN (
    SELECT po_id, ISNULL(SUM(Supplyqty), 0) AS Supplyqty
    FROM SupplierDispatch d
    INNER JOIN Issue_item_details i ON d.Issue_id = i.Issue_id
    WHERE d.status = 'C'
    GROUP BY po_id
) sup ON sup.po_id = a.po_id
LEFT OUTER JOIN (
    SELECT SUM(ri.received_qty) AS insqty, r.po_id
    FROM receipts r
    LEFT OUTER JOIN receipt_item_details ri ON ri.receipt_id = r.receipt_id
    WHERE r.recieved_date IS NOT NULL AND r.status IN ('C')
    GROUP BY r.po_id
) ins ON ins.po_id = a.po_id
WHERE b.supplier_id = @SupplierId
  AND b.status IN ('Order Placed', 'Partially Received', 'Completed')
  AND (@FinancialYearId = 0 OR b.financial_year_id = @FinancialYearId)
GROUP BY b.po_id, ISNULL(sup.Supplyqty, 0), ISNULL(ins.insqty, 0), b.po_no, b.po_date, b.outward_no
HAVING (@StatusFilter = '' OR (
    CASE WHEN SUM(a.quantity) = ISNULL(ins.insqty, 0) AND SUM(a.quantity) = ISNULL(sup.Supplyqty, 0)
         THEN 'Installation Completed'
         WHEN ISNULL(sup.Supplyqty, 0) = 0 OR SUM(a.quantity) > ISNULL(sup.Supplyqty, 0)
         THEN 'Dispatch Pending'
         ELSE 'Pending For Receipt/Installation' END) = @StatusFilter)
ORDER BY b.po_date DESC";

                var options = new List<SupplierPoReceiptOptionDto>
                {
                    new() { PoId = 0, DisplayText = "Select PO" }
                };

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                using SqlCommand cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);
                cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId);
                cmd.Parameters.AddWithValue("@StatusFilter", statusFilter);

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    options.Add(new SupplierPoReceiptOptionDto
                    {
                        PoId = ReadIntColumn(reader, "po_id"),
                        DisplayText = ReadStringColumn(reader, "data")
                    });
                }

                return Ok(options);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading PO list.", error = ex.Message });
            }
        }

        /// <summary>Facilitypo_supply_ReceiptSUP.aspx — consignee-wise receipt grid.</summary>
        [HttpGet("po-supply-receipt/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierPoReceipt(
            int userId,
            [FromQuery] int poId)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });
            if (poId <= 0)
                return BadRequest(new { message = "PO id is required." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                const string sql = @"
SELECT c1.location_name, a.po_id, a.po_item_id, a.item_id, a.quantity,
       ISNULL(re.receiptQTY, 0) AS receiptQTY, ISNULL(ins.insqty, 0) AS insqty,
       ISNULL(sup.Supplyqty, 0) AS Supplyqty, a.consignee_id,
       R.item_name, R.item_code_as_per_tender AS item_code,
       ISNULL(dsp.DeniedQTY, 0) AS Deniedqty,
       ISNULL(dsp.deniedstatus, '-') AS DeniedStatus
FROM po_items a
INNER JOIN masitems R ON R.item_id = a.item_id
INNER JOIN purchase_order b ON a.po_id = b.po_id
INNER JOIN massuppliers s ON s.supplier_id = b.supplier_id
LEFT OUTER JOIN maslocations c1 ON c1.location_id = a.consignee_id
LEFT OUTER JOIN (
    SELECT po_id, ISNULL(SUM(Supplyqty), 0) AS Supplyqty, d.location_id
    FROM SupplierDispatch d
    INNER JOIN Issue_item_details i ON d.Issue_id = i.Issue_id
    WHERE d.status = 'C' AND d.po_id = @PoId
    GROUP BY po_id, d.location_id
) sup ON sup.po_id = a.po_id AND sup.location_id = a.consignee_id
LEFT OUTER JOIN (
    SELECT ISNULL(SUM(r.receipt_qty), 0) AS receiptQTY, r.po_id, r.location_id
    FROM receipts r
    WHERE r.recieved_date IS NOT NULL AND r.status IN ('C', 'Received') AND r.po_id = @PoId
    GROUP BY po_id, r.location_id
) re ON re.po_id = a.po_id AND re.location_id = a.consignee_id
LEFT OUTER JOIN (
    SELECT SUM(ri.received_qty) AS insqty, r.po_id, r.location_id
    FROM receipts r
    LEFT OUTER JOIN receipt_item_details ri ON ri.receipt_id = r.receipt_id
    WHERE r.recieved_date IS NOT NULL AND r.status IN ('C') AND r.po_id = @PoId
    GROUP BY r.po_id, r.location_id
) ins ON ins.po_id = a.po_id AND ins.location_id = a.consignee_id
LEFT OUTER JOIN (
    SELECT d.ponoid, d.DeniedQTY, d.consigneeid,
           CASE WHEN receiptid IS NULL THEN 'Receipt & Installation both denied'
                ELSE 'Installation denied' END AS deniedstatus
    FROM Descrepency d
    INNER JOIN purchase_order p ON p.po_id = d.ponoid
    WHERE p.po_id = @PoId
) dsp ON dsp.ponoid = a.po_id AND dsp.consigneeid = a.consignee_id
WHERE a.po_id = @PoId AND b.supplier_id = @SupplierId
ORDER BY ISNULL(ins.insqty, 0)";

                var rows = new List<SupplierPoReceiptRowDto>();
                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                using SqlCommand cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@PoId", poId);
                cmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);

                var pendingRows = new List<(SupplierPoReceiptRowDto Row, int ConsigneeId)>();
                using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int consigneeId = ReadIntColumn(reader, "consignee_id");
                    pendingRows.Add((new SupplierPoReceiptRowDto
                    {
                        PoItemId = ReadIntColumn(reader, "po_item_id"),
                        PoId = ReadIntColumn(reader, "po_id"),
                        ConsigneeId = consigneeId,
                        LocationName = ReadStringColumn(reader, "location_name"),
                        ItemName = ReadStringColumn(reader, "item_name"),
                        ItemCode = ReadStringColumn(reader, "item_code"),
                        Quantity = ReadDecimalColumn(reader, "quantity"),
                        SupplyQty = ReadDecimalColumn(reader, "Supplyqty"),
                        ReceiptQty = ReadDecimalColumn(reader, "receiptQTY"),
                        InstQty = ReadDecimalColumn(reader, "insqty"),
                        DeniedQty = ReadDecimalColumn(reader, "Deniedqty"),
                        DeniedStatus = ReadStringColumn(reader, "DeniedStatus", "deniedstatus"),
                    }, consigneeId));
                }

                foreach (var entry in pendingRows)
                {
                    entry.Row.Batches = await LoadReceiptBatchesAsync(con, poId, entry.ConsigneeId);
                    rows.Add(entry.Row);
                }

                return Ok(rows);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading receipt details.", error = ex.Message });
            }
        }

        /// <summary>RCDetailReportForSupplier.aspx — tender dropdown.</summary>
        [HttpGet("rc-detail-report/tenders/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierRcDetailTenders(int userId)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                const string sql = @"
SELECT DISTINCT t.tender_no, t.tender_id
FROM contract_items c
INNER JOIN masitems m ON m.item_id = c.item_id
INNER JOIN award_of_contract ac ON ac.award_of_contract_id = c.award_of_contract_id
INNER JOIN massuppliers s ON s.supplier_id = ac.supplier_id
INNER JOIN tenders t ON t.tender_id = ac.tender_id
WHERE c.isfreezed IS NULL
  AND GETDATE() BETWEEN ac.contract_date AND ac.contract_end_date
  AND s.supplier_id = @SupplierId
ORDER BY t.tender_no";

                var tenders = new List<SupplierTenderOptionDto>();
                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                using SqlCommand cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    tenders.Add(new SupplierTenderOptionDto
                    {
                        TenderId = ReadIntColumn(reader, "tender_id"),
                        TenderNo = ReadStringColumn(reader, "tender_no"),
                    });
                }

                return Ok(tenders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading RC tenders.", error = ex.Message });
            }
        }

        /// <summary>RCDetailReportForSupplier.aspx — grid.</summary>
        [HttpGet("rc-detail-report/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierRcDetailReport(
            int userId,
            [FromQuery] int tenderId = 0)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                string tenderFilter = tenderId > 0 ? " AND t.tender_id = @TenderId" : string.Empty;

                string sql = @"
SELECT m.item_id, c.contract_item_id,
       m.item_code_as_per_tender AS item_codeE, m.item_name AS item_nameE,
       c.basic_rate, c.percentage, c.single_unit_price,
       CONVERT(VARCHAR, ac.contract_date, 103) AS contract_date,
       CONVERT(VARCHAR, ac.contract_end_date, 103) AS contract_end_date,
       s.name, t.tender_no, t.tender_id,
       CASE WHEN mu.item_id IS NOT NULL THEN 1 ELSE 0 END AS HasSpecification
FROM contract_items c
INNER JOIN masitems m ON m.item_id = c.item_id
INNER JOIN award_of_contract ac ON ac.award_of_contract_id = c.award_of_contract_id
INNER JOIN massuppliers s ON s.supplier_id = ac.supplier_id
INNER JOIN tenders t ON t.tender_id = ac.tender_id
LEFT JOIN masitems_upload mu ON mu.item_id = m.item_id
WHERE c.isfreezed IS NULL
  AND GETDATE() BETWEEN ac.contract_date AND ac.contract_end_date
  AND s.supplier_id = @SupplierId" + tenderFilter + @"
ORDER BY ac.contract_date DESC";

                var rows = new List<SupplierRcDetailRowDto>();
                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                using SqlCommand cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);
                if (tenderId > 0)
                    cmd.Parameters.AddWithValue("@TenderId", tenderId);

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new SupplierRcDetailRowDto
                    {
                        ContractItemId = ReadIntColumn(reader, "contract_item_id"),
                        ItemId = ReadIntColumn(reader, "item_id"),
                        ItemCode = ReadStringColumn(reader, "item_codeE"),
                        ItemName = ReadStringColumn(reader, "item_nameE"),
                        SupplierName = ReadStringColumn(reader, "name"),
                        TenderNo = ReadStringColumn(reader, "tender_no"),
                        TenderId = ReadIntColumn(reader, "tender_id"),
                        ContractDate = ReadStringColumn(reader, "contract_date"),
                        ContractEndDate = ReadStringColumn(reader, "contract_end_date"),
                        BasicRate = ReadDecimalColumn(reader, "basic_rate"),
                        Percentage = ReadDecimalColumn(reader, "percentage"),
                        SingleUnitPrice = ReadDecimalColumn(reader, "single_unit_price"),
                        HasSpecification = ReadIntColumn(reader, "HasSpecification") == 1,
                    });
                }

                return Ok(rows);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading RC detail report.", error = ex.Message });
            }
        }

        /// <summary>AcceptedReoprtSupplier.aspx — tender dropdown.</summary>
        [HttpGet("accepted-report/tenders/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierAcceptedTenders(int userId)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                const string sql = @"
SELECT DISTINCT t.tender_no, t.tender_id
FROM live_tender_price tp
INNER JOIN tender_items ti ON ti.tender_item_id = tp.tender_item_id
INNER JOIN tenders t ON t.tender_id = ti.tender_id
INNER JOIN massuppliers s ON s.supplier_id = tp.supplier_id
INNER JOIN masitems m ON m.item_id = ti.item_id
WHERE tp.isaccept = 'Y' AND s.supplier_id = @SupplierId
ORDER BY t.tender_no";

                var tenders = new List<SupplierTenderOptionDto>();
                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                using SqlCommand cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    tenders.Add(new SupplierTenderOptionDto
                    {
                        TenderId = ReadIntColumn(reader, "tender_id"),
                        TenderNo = ReadStringColumn(reader, "tender_no"),
                    });
                }

                return Ok(tenders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading accepted tenders.", error = ex.Message });
            }
        }

        /// <summary>AcceptedReoprtSupplier.aspx — logged-in supplier option.</summary>
        [HttpGet("accepted-report/supplier/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierAcceptedSupplierOption(int userId)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                const string sql = @"
SELECT name, supplier_id
FROM massuppliers
WHERE supplier_id = @SupplierId";

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                using SqlCommand cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound(new { message = "Supplier not found." });

                return Ok(new SupplierAcceptedSupplierOptionDto
                {
                    SupplierId = ReadIntColumn(reader, "supplier_id"),
                    Name = ReadStringColumn(reader, "name"),
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading supplier.", error = ex.Message });
            }
        }

        /// <summary>AcceptedReoprtSupplier.aspx — grid.</summary>
        [HttpGet("accepted-report/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierAcceptedReport(
            int userId,
            [FromQuery] string filterType = "tender",
            [FromQuery] int tenderId = 0,
            [FromQuery] int supplierId = 0)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });

            try
            {
                int? loggedInSupplierId = await ResolveSupplierIdForUserAsync(userId);
                if (loggedInSupplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                string tenderFilter = string.Empty;
                if (string.Equals(filterType, "tender", StringComparison.OrdinalIgnoreCase) && tenderId > 0)
                    tenderFilter = " AND t.tender_id = @TenderId";

                string sql = @"
SELECT m.item_code_as_per_tender, m.item_name, s.name, t.tender_no,
       CONVERT(VARCHAR, t.tender_date, 103) AS tender_date,
       ti.tender_quantity, tp.basicrate, tp.gst,
       tp.fbasicrate AS acceptedBasicrate,
       t.tender_id, tp.supplier_id, ti.item_id
FROM live_tender_price tp
INNER JOIN tender_items ti ON ti.tender_item_id = tp.tender_item_id
INNER JOIN tenders t ON t.tender_id = ti.tender_id
INNER JOIN massuppliers s ON s.supplier_id = tp.supplier_id
INNER JOIN masitems m ON m.item_id = ti.item_id
WHERE tp.isaccept = 'Y' AND tp.supplier_id = @SupplierId" + tenderFilter + @"
ORDER BY tp.fdate DESC";

                var rows = new List<SupplierAcceptedReportRowDto>();
                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                using SqlCommand cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@SupplierId", loggedInSupplierId.Value);
                if (!string.IsNullOrEmpty(tenderFilter))
                    cmd.Parameters.AddWithValue("@TenderId", tenderId);

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new SupplierAcceptedReportRowDto
                    {
                        ItemId = ReadIntColumn(reader, "item_id"),
                        TenderId = ReadIntColumn(reader, "tender_id"),
                        SupplierId = ReadIntColumn(reader, "supplier_id"),
                        ItemCode = ReadStringColumn(reader, "item_code_as_per_tender"),
                        ItemName = ReadStringColumn(reader, "item_name"),
                        SupplierName = ReadStringColumn(reader, "name"),
                        TenderNo = ReadStringColumn(reader, "tender_no"),
                        TenderDate = ReadStringColumn(reader, "tender_date"),
                        TenderQuantity = ReadIntColumn(reader, "tender_quantity"),
                        BasicRate = ReadDecimalColumn(reader, "basicrate"),
                        Gst = ReadDecimalColumn(reader, "gst"),
                        AcceptedBasicRate = ReadDecimalColumn(reader, "acceptedBasicrate"),
                    });
                }

                return Ok(rows);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading accepted report.", error = ex.Message });
            }
        }

        /// <summary>ReceiptComplainSupplier.aspx — complaint grid.</summary>
        [HttpGet("receipt-complain/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierReceiptComplain(
            int userId,
            [FromQuery] string status = "Booked")
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });

            if (string.IsNullOrWhiteSpace(status))
                status = "Booked";

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                const string sql = @"
SELECT c.complaint_id, c.complaint_no,
       CONVERT(VARCHAR, c.complaint_date, 103) AS complaint_date,
       CONVERT(VARCHAR, c.not_function_date, 103) AS not_function_date,
       c.complaint_details, c.Serial_no,
       masitems.item_name, maslocations.location_name,
       masitems.item_code_as_per_tender,
       ISNULL(c.ext, '.pdf') AS ext,
       c.path,
       p.outward_no + '/' + p.po_no AS pono,
       CONVERT(VARCHAR, p.po_date, 103) AS po_date,
       u.storeOfficerMob
FROM complaints c
INNER JOIN masitems ON c.item_id = masitems.item_id
INNER JOIN maslocations ON c.location_id = maslocations.location_id
INNER JOIN users u ON u.location_id = maslocations.location_id
LEFT OUTER JOIN receipt_item_details ri ON ri.item_detail_id = c.item_detail_id
LEFT OUTER JOIN receipts r ON r.receipt_id = ri.receipt_id
LEFT OUTER JOIN purchase_order p ON p.po_id = r.po_id
WHERE c.supplier_id = @SupplierId
  AND c.status = @Status
ORDER BY c.complaint_date";

                var rows = new List<SupplierReceiptComplainRowDto>();
                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                using SqlCommand cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);
                cmd.Parameters.AddWithValue("@Status", status);

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string filePath = ReadStringColumn(reader, "path");
                    string fileExt = ReadStringColumn(reader, "ext");
                    if (string.IsNullOrWhiteSpace(fileExt))
                        fileExt = ".pdf";

                    rows.Add(new SupplierReceiptComplainRowDto
                    {
                        ComplaintId = ReadIntColumn(reader, "complaint_id"),
                        ComplaintNo = ReadStringColumn(reader, "complaint_no"),
                        PoNo = ReadStringColumn(reader, "pono"),
                        PoDate = ReadStringColumn(reader, "po_date"),
                        ItemCode = ReadStringColumn(reader, "item_code_as_per_tender"),
                        ItemName = ReadStringColumn(reader, "item_name"),
                        SerialNo = ReadStringColumn(reader, "Serial_no", "serial_no"),
                        ComplaintDate = ReadStringColumn(reader, "complaint_date"),
                        NotFunctionDate = ReadStringColumn(reader, "not_function_date"),
                        LocationName = ReadStringColumn(reader, "location_name"),
                        FacilityContactNo = ReadStringColumn(reader, "storeOfficerMob"),
                        ComplaintDetails = ReadStringColumn(reader, "complaint_details"),
                        FilePath = filePath,
                        FileExt = fileExt,
                        HasFile = !string.IsNullOrWhiteSpace(filePath),
                    });
                }

                return Ok(rows);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading complaints.", error = ex.Message });
            }
        }

        /// <summary>ReceiptComplainSupplier.aspx — complain letter download.</summary>
        [HttpGet("receipt-complain/file/by-user/{userId:int}")]
        public async Task<IActionResult> DownloadSupplierReceiptComplainFile(
            int userId,
            [FromQuery] int complaintId)
        {
            if (userId <= 0 || complaintId <= 0)
                return BadRequest(new { message = "Invalid request." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                const string sql = @"
SELECT path, ISNULL(ext, '.pdf') AS ext
FROM complaints
WHERE complaint_id = @ComplaintId AND supplier_id = @SupplierId";

                string? filePath = null;
                string fileExt = ".pdf";

                using (SqlConnection con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();
                    using SqlCommand cmd = new SqlCommand(sql, con);
                    cmd.Parameters.AddWithValue("@ComplaintId", complaintId);
                    cmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);

                    using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                        return NotFound(new { message = "Complaint not found." });

                    filePath = ReadStringColumn(reader, "path");
                    fileExt = ReadStringColumn(reader, "ext");
                }

                if (string.IsNullOrWhiteSpace(filePath))
                    return NotFound(new { message = "File not found." });

                if (string.IsNullOrWhiteSpace(fileExt))
                    fileExt = ".pdf";

                string physicalPath = Path.Combine(_complaintFileRoot, filePath + fileExt);
                if (!System.IO.File.Exists(physicalPath))
                    return NotFound(new { message = "Complaint file is not available on server." });

                string downloadName = $"Complain_{complaintId}{fileExt}";
                string contentType = fileExt.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                    ? "application/pdf"
                    : "application/octet-stream";

                return PhysicalFile(physicalPath, contentType, downloadName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error downloading complaint file.", error = ex.Message });
            }
        }

        /// <summary>EMDdeposite.aspx — tender dropdown (excluding already submitted).</summary>
        [HttpGet("emd-deposit/tenders/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierEmdDepositTenders(int userId)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                const string sql = @"
SELECT t.tender_id, t.tender_no
FROM tenders t
WHERE t.tender_id NOT IN (
    SELECT e.TenderNo FROM EMDDepositeDetail e WHERE e.SupId = @SupplierId AND e.TenderNo <> 0
)
ORDER BY t.tender_id DESC";

                var tenders = new List<SupplierTenderOptionDto>();
                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                using SqlCommand cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    tenders.Add(new SupplierTenderOptionDto
                    {
                        TenderId = ReadIntColumn(reader, "tender_id"),
                        TenderNo = ReadStringColumn(reader, "tender_no"),
                    });
                }

                return Ok(tenders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading tenders.", error = ex.Message });
            }
        }

        /// <summary>EMDdeposite.aspx — EMD payment mode dropdown.</summary>
        [HttpGet("emd-deposit/emd-types")]
        public async Task<IActionResult> GetSupplierEmdDocumentTypes()
        {
            try
            {
                const string sql = "SELECT dtypeid, dtypename FROM MASDOCUMENTTYPE ORDER BY dtypename";
                var types = new List<SupplierEmdDocumentTypeDto>();

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                using SqlCommand cmd = new SqlCommand(sql, con);
                using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    types.Add(new SupplierEmdDocumentTypeDto
                    {
                        DtypeId = ReadIntColumn(reader, "dtypeid"),
                        DtypeName = ReadStringColumn(reader, "dtypename"),
                    });
                }

                return Ok(types);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading EMD types.", error = ex.Message });
            }
        }

        /// <summary>EMDdeposite.aspx — submitted deposits grid.</summary>
        [HttpGet("emd-deposit/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierEmdDeposits(int userId)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                const string sql = @"
SELECT e.Id, e.SupId,
       CASE WHEN e.OtherTenderNo <> '-' THEN e.OtherTenderNo ELSE t.tender_no END AS TenderNo,
       e.EMDAmt, d.dtypename AS EMDType, e.EMDDocumentNo,
       CONVERT(VARCHAR, e.EMDDepositeDt, 103) AS EMDDepositeDt,
       e.EMDDocument
FROM EMDDepositeDetail e
LEFT OUTER JOIN tenders t ON t.tender_id = e.TenderNo
INNER JOIN MASDOCUMENTTYPE d ON d.dtypeid = e.EMDType
WHERE e.SupId = @SupplierId
ORDER BY e.EntryDate";

                var rows = new List<SupplierEmdDepositRowDto>();
                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                using SqlCommand cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string emdDocument = ReadStringColumn(reader, "EMDDocument");
                    rows.Add(new SupplierEmdDepositRowDto
                    {
                        Id = ReadIntColumn(reader, "Id", "id"),
                        SupId = ReadIntColumn(reader, "SupId"),
                        TenderNo = ReadStringColumn(reader, "TenderNo"),
                        EmdAmt = ReadDecimalColumn(reader, "EMDAmt"),
                        EmdType = ReadStringColumn(reader, "EMDType"),
                        EmdDocumentNo = ReadStringColumn(reader, "EMDDocumentNo"),
                        EmdDepositeDt = ReadStringColumn(reader, "EMDDepositeDt"),
                        EmdDocument = emdDocument,
                        HasFile = !string.IsNullOrWhiteSpace(emdDocument),
                    });
                }

                return Ok(rows);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading EMD deposits.", error = ex.Message });
            }
        }

        /// <summary>EMDdeposite.aspx — save refund request with PDF upload.</summary>
        [HttpPost("emd-deposit/by-user/{userId:int}")]
        public async Task<IActionResult> SaveSupplierEmdDeposit(
            int userId,
            [FromForm] int tenderId,
            [FromForm] string? otherTenderNo,
            [FromForm] decimal emdAmount,
            [FromForm] int emdType,
            [FromForm] string emdDocNo,
            [FromForm] string emdDepositDate,
            IFormFile? file)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });

            if (tenderId < 0)
                return BadRequest(new { message = "Please select tender No." });

            if (tenderId == 0 && string.IsNullOrWhiteSpace(otherTenderNo))
                return BadRequest(new { message = "Please insert Other Tender No." });

            if (emdAmount <= 0)
                return BadRequest(new { message = "Please insert EMD Amount." });

            if (emdType <= 0)
                return BadRequest(new { message = "Please select EMD Type." });

            if (string.IsNullOrWhiteSpace(emdDocNo))
                return BadRequest(new { message = "Please insert EMD Document Number." });

            if (string.IsNullOrWhiteSpace(emdDepositDate))
                return BadRequest(new { message = "Please insert EMD Deposite Date." });

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Please Upload PDF File for EMD Document/Letter." });

            if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Please upload pdf file only." });

            if (file.Length >= 3_000_000)
                return BadRequest(new { message = "Your can't upload file more than 3mb." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                if (!TryParseDepositDate(emdDepositDate, out DateTime depositDate))
                    return BadRequest(new { message = "Invalid date format." });

                if (depositDate.Date > DateTime.Today)
                    return BadRequest(new { message = "EMD Deposite Date Should not be Greater than today." });

                bool isOther = tenderId == 0;
                int tenderNoToSave = isOther ? 0 : tenderId;
                string otherTenderToSave = isOther ? otherTenderNo!.Trim() : "-";

                if (!isOther && !await CheckSupplierParticipatedInTenderAsync(tenderId, supplierId.Value))
                    return BadRequest(new { message = "Please Check Tender No, Seems You have not Participated in Selected Tender" });

                int depositId;
                using (SqlConnection con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();
                    const string insertSql = @"
INSERT INTO EMDDepositeDetail (SupId, TenderNo, OtherTenderNo, EMDAmt, EMDType, EMDDocumentNo, EMDDepositeDt)
OUTPUT INSERTED.Id
VALUES (@SupId, @TenderNo, @OtherTenderNo, @EmdAmt, @EmdType, @EmdDocNo, @EmdDepositeDt)";

                    using SqlCommand cmd = new SqlCommand(insertSql, con);
                    cmd.Parameters.AddWithValue("@SupId", supplierId.Value);
                    cmd.Parameters.AddWithValue("@TenderNo", tenderNoToSave);
                    cmd.Parameters.AddWithValue("@OtherTenderNo", otherTenderToSave);
                    cmd.Parameters.AddWithValue("@EmdAmt", emdAmount);
                    cmd.Parameters.AddWithValue("@EmdType", emdType);
                    cmd.Parameters.AddWithValue("@EmdDocNo", emdDocNo.Trim());
                    cmd.Parameters.AddWithValue("@EmdDepositeDt", depositDate);

                    object? idResult = await cmd.ExecuteScalarAsync();
                    if (idResult == null || idResult == DBNull.Value)
                        return StatusCode(500, new { message = "Unable to save EMD deposit record." });

                    depositId = Convert.ToInt32(idResult);
                }

                string fileKey = GenerateEmdFileName(supplierId.Value);
                string savePath = Path.Combine(_emdFileRoot, fileKey + ".pdf");
                await using (var stream = new FileStream(savePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                using (SqlConnection con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();
                    const string updateSql = "UPDATE EMDDepositeDetail SET EMDDocument = @FileName WHERE Id = @Id AND SupId = @SupId";
                    using SqlCommand cmd = new SqlCommand(updateSql, con);
                    cmd.Parameters.AddWithValue("@FileName", fileKey);
                    cmd.Parameters.AddWithValue("@Id", depositId);
                    cmd.Parameters.AddWithValue("@SupId", supplierId.Value);
                    await cmd.ExecuteNonQueryAsync();
                }

                return Ok(new { message = "Record Successfully Inserted", depositId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error saving EMD deposit.", error = ex.Message });
            }
        }

        /// <summary>EMDdeposite.aspx — document download.</summary>
        [HttpGet("emd-deposit/file/by-user/{userId:int}")]
        public async Task<IActionResult> DownloadSupplierEmdDepositFile(int userId, [FromQuery] int depositId)
        {
            if (userId <= 0 || depositId <= 0)
                return BadRequest(new { message = "Invalid request." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                const string sql = @"
SELECT EMDDocument FROM EMDDepositeDetail
WHERE Id = @Id AND SupId = @SupId";

                string? fileName = null;
                using (SqlConnection con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();
                    using SqlCommand cmd = new SqlCommand(sql, con);
                    cmd.Parameters.AddWithValue("@Id", depositId);
                    cmd.Parameters.AddWithValue("@SupId", supplierId.Value);

                    object? result = await cmd.ExecuteScalarAsync();
                    if (result == null || result == DBNull.Value)
                        return NotFound(new { message = "EMD deposit not found." });

                    fileName = result.ToString();
                }

                if (string.IsNullOrWhiteSpace(fileName))
                    return NotFound(new { message = "File not found." });

                string physicalPath = Path.Combine(_emdFileRoot, fileName + ".pdf");
                if (!System.IO.File.Exists(physicalPath))
                    return NotFound(new { message = "EMD document is not available on server." });

                return PhysicalFile(physicalPath, "application/pdf", fileName + ".pdf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error downloading EMD document.", error = ex.Message });
            }
        }

        /// <summary>PaymentReport.aspx — paid purchase order report.</summary>
        [HttpGet("payment-report/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierPaymentReport(
            int userId,
            [FromQuery] string poType = "NP")
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                string poTypeFilter = string.Empty;
                if (string.Equals(poType, "NP", StringComparison.OrdinalIgnoreCase))
                    poTypeFilter = " AND ISNULL(p.potype, 'NP') = 'NP' ";
                else if (string.Equals(poType, "CP", StringComparison.OrdinalIgnoreCase))
                    poTypeFilter = " AND ISNULL(p.potype, 'NP') = 'CP' ";

                string sql = @"
SELECT p.po_no,
       CASE WHEN p.soissueDT IS NULL THEN CONVERT(VARCHAR, p.po_date, 103) ELSE CONVERT(VARCHAR, p.soissueDT, 103) END AS po_date,
       sp.name,
       s.SANCTIONEDAMOUNT AS GrossAmt,
       ISNULL(s.DEDUCTIONS, 0) AS totalDed,
       ISNULL(s.addition, 0) AS totalAddition,
       s.chequeAmt AS ChequeAmt,
       py.AIDNO,
       CONVERT(VARCHAR, py.AIDDATE, 103) AS chequedate,
       b.BUDGETID,
       s.SANCTIONID,
       sp.supplier_id,
       p.po_id,
       s.PAYMENTID,
       'PO Payment' AS typeP
FROM BLPSANCTIONS s
INNER JOIN BLPPAYMENTS py ON py.PAYMENTID = s.PAYMENTID
INNER JOIN MASBUDGET b ON b.BUDGETID = s.BUDGETID
INNER JOIN purchase_order p ON s.po_id = p.po_id
LEFT OUTER JOIN (
    SELECT b.SANCTIONID, mp.PType, b.po_id
    FROM BLPINVOICES b
    LEFT OUTER JOIN MasPStatus mp ON mp.PType = b.status
    WHERE mp.PType IN ('P')
    GROUP BY po_id, mp.PType, b.SANCTIONID
) bi ON bi.po_id = p.po_id AND bi.SANCTIONID = s.SANCTIONID
LEFT OUTER JOIN MasPStatus mp ON mp.PType = bi.PType
INNER JOIN massuppliers sp ON sp.supplier_id = p.supplier_id
WHERE s.STATUS IN ('P') AND sp.supplier_id = @SupplierId" + poTypeFilter + @"

UNION ALL

SELECT p.po_no,
       CASE WHEN p.soissueDT IS NULL THEN CONVERT(VARCHAR, p.po_date, 103) ELSE CONVERT(VARCHAR, p.soissueDT, 103) END AS po_date,
       sp.name,
       t.RELEASEAMT AS GrossAmt,
       0 AS totalDed,
       0 AS totalAddition,
       t.RELEASEAMT AS ChequeAmt,
       py.AIDNO,
       CONVERT(VARCHAR, py.AIDDATE, 103) AS chequedate,
       b.BUDGETID,
       s.SANCTIONID,
       sp.supplier_id,
       p.po_id,
       py.PAYMENTID,
       'Release Witheld' AS typeP
FROM BLPTAXS t
INNER JOIN BLPSANCTIONS s ON s.SANCTIONID = t.SANCTIONID
INNER JOIN MASBUDGET b ON b.BUDGETID = s.BUDGETID
INNER JOIN purchase_order p ON p.po_id = s.po_id
INNER JOIN massuppliers sp ON sp.supplier_id = p.supplier_id
INNER JOIN BLPPAYMENTS py ON py.paymentid = t.paymentid
LEFT OUTER JOIN MasPStatus mp ON mp.PType = py.status
WHERE py.STATUS = 'P' AND t.TAXTYPEID = 250 AND sp.supplier_id = @SupplierId" + poTypeFilter + @"

ORDER BY PAYMENTID";

                var rows = new List<SupplierPaymentReportRowDto>();
                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                using SqlCommand cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new SupplierPaymentReportRowDto
                    {
                        PoNo = ReadStringColumn(reader, "po_no"),
                        PoDate = ReadStringColumn(reader, "po_date"),
                        SupplierName = ReadStringColumn(reader, "name"),
                        GrossAmt = ReadDecimalColumn(reader, "GrossAmt"),
                        TotalDed = ReadDecimalColumn(reader, "totalDed"),
                        TotalAddition = ReadDecimalColumn(reader, "totalAddition"),
                        ChequeAmt = ReadDecimalColumn(reader, "ChequeAmt"),
                        AidNo = ReadStringColumn(reader, "AIDNO"),
                        ChequeDate = ReadStringColumn(reader, "chequedate"),
                        BudgetId = ReadIntColumn(reader, "BUDGETID"),
                        SanctionId = ReadIntColumn(reader, "SANCTIONID"),
                        SupplierId = ReadIntColumn(reader, "supplier_id"),
                        PoId = ReadIntColumn(reader, "po_id"),
                        PaymentId = ReadIntColumn(reader, "PAYMENTID"),
                        PaymentType = ReadStringColumn(reader, "typeP"),
                    });
                }

                return Ok(rows);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading payment report.", error = ex.Message });
            }
        }

        /// <summary>BalanceStatussupplier.aspx — pending receipt/installation report.</summary>
        [HttpGet("balance-status/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierBalanceStatus(
            int userId,
            [FromQuery] string balanceType = "R")
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });

            string normalizedType = string.IsNullOrWhiteSpace(balanceType) ? "R" : balanceType.Trim().ToUpperInvariant();
            if (normalizedType is not ("R" or "I" or "D"))
                return BadRequest(new { message = "Invalid balance type." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                string whereCause;
                string balanceExpression;
                switch (normalizedType)
                {
                    case "I":
                        whereCause = " AND re.receiptQTY > ins.insqty";
                        balanceExpression = "re.receiptQTY - ins.insqty";
                        break;
                    case "D":
                        whereCause = " AND pi.quantity > ISNULL(Supplyqty, 0)";
                        balanceExpression = "pi.quantity - ISNULL(Supplyqty, 0)";
                        break;
                    default:
                        whereCause = " AND Supplyqty > re.receiptQTY";
                        balanceExpression = "Supplyqty - re.receiptQTY";
                        break;
                }

                string sql = $@"
SELECT p.po_id, t.tender_no, f.year, p.po_no,
       CONVERT(VARCHAR, p.po_date, 103) AS po_date, p.directorate_id,
       dir.facility_aut_name, m.item_code_as_per_tender, m.item_name,
       sp.name AS Supplier, pi.quantity AS POQTY, Supplyqty, re.receiptQTY,
       ins.insqty,
       CASE WHEN ISNULL(p.potype, 'NP') = 'NP' THEN 'Normal PO' ELSE 'Covid Po' END AS potype,
       {balanceExpression} AS balanceQty
FROM purchase_order p
INNER JOIN massuppliers sp ON sp.supplier_id = p.supplier_id
INNER JOIN mas_financial_year f ON f.financial_year_id = p.financial_year_id
LEFT OUTER JOIN (
    SELECT SUM(pi.quantity) AS quantity, pi.po_id, pi.item_id
    FROM po_items pi
    GROUP BY pi.po_id, pi.item_id
) pi ON pi.po_id = p.po_id
INNER JOIN tenders t ON t.tender_id = p.tender_id
INNER JOIN masitems m ON m.item_id = pi.item_id
INNER JOIN facility_aut dir ON dir.facility_aut_id = p.directorate_id
LEFT OUTER JOIN (
    SELECT po_id, ISNULL(SUM(Supplyqty), 0) AS Supplyqty
    FROM SupplierDispatch d
    INNER JOIN Issue_item_details i ON d.Issue_id = i.Issue_id
    INNER JOIN maslocations u ON u.location_id = d.location_id
    WHERE d.status = 'C'
    GROUP BY po_id
) AS sup ON sup.po_id = pi.po_id
LEFT OUTER JOIN (
    SELECT ISNULL(SUM(r.receipt_qty), 0) AS receiptQTY, r.po_id
    FROM receipts r
    WHERE r.recieved_date IS NOT NULL AND r.status IN ('C', 'Received')
    GROUP BY po_id
) AS re ON re.po_id = pi.po_id
LEFT OUTER JOIN (
    SELECT SUM(ri.received_qty) AS insqty, r.po_id
    FROM receipts r
    LEFT OUTER JOIN receipt_item_details ri ON ri.receipt_id = r.receipt_id
    WHERE r.recieved_date IS NOT NULL AND r.status IN ('C')
    GROUP BY r.po_id
) AS ins ON ins.po_id = pi.po_id
WHERE sp.supplier_id = @SupplierId
  AND p.status IN ('Order Placed'){whereCause}
ORDER BY p.po_date DESC";

                var rows = new List<SupplierBalanceStatusRowDto>();
                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                using SqlCommand cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new SupplierBalanceStatusRowDto
                    {
                        PoId = ReadIntColumn(reader, "po_id"),
                        DirectorateId = ReadIntColumn(reader, "directorate_id"),
                        TenderNo = ReadStringColumn(reader, "tender_no"),
                        Year = ReadStringColumn(reader, "year"),
                        PoNo = ReadStringColumn(reader, "po_no"),
                        PoDate = ReadStringColumn(reader, "po_date"),
                        FacilityAutName = ReadStringColumn(reader, "facility_aut_name"),
                        ItemCode = ReadStringColumn(reader, "item_code_as_per_tender"),
                        ItemName = ReadStringColumn(reader, "item_name"),
                        Supplier = ReadStringColumn(reader, "Supplier"),
                        PoQty = ReadDecimalColumn(reader, "POQTY"),
                        SupplyQty = ReadDecimalColumn(reader, "Supplyqty"),
                        ReceiptQty = ReadDecimalColumn(reader, "receiptQTY"),
                        InstQty = ReadDecimalColumn(reader, "insqty"),
                        PoType = ReadStringColumn(reader, "potype"),
                        BalanceQty = ReadDecimalColumn(reader, "balanceQty"),
                    });
                }

                return Ok(rows);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading balance status report.", error = ex.Message });
            }
        }

        private static string GenerateEmdFileName(int supplierId)
        {
            return $"{supplierId}_{DateTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}";
        }

        private static bool TryParseDepositDate(string input, out DateTime depositDate)
        {
            depositDate = default;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            string[] formats = { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy" };
            return DateTime.TryParseExact(
                input.Trim(),
                formats,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out depositDate)
                || DateTime.TryParse(input.Trim(), out depositDate);
        }

        private async Task<bool> CheckSupplierParticipatedInTenderAsync(int tenderId, int supplierId)
        {
            const string tenderSql = @"
SELECT 1 FROM tenders WHERE tender_id = @TenderId AND tender_date > '2022-04-01'";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            using (SqlCommand tenderCmd = new SqlCommand(tenderSql, con))
            {
                tenderCmd.Parameters.AddWithValue("@TenderId", tenderId);
                object? tenderResult = await tenderCmd.ExecuteScalarAsync();
                if (tenderResult == null || tenderResult == DBNull.Value)
                    return true;
            }

            const string participantSql = @"
SELECT 1 FROM masschemesstatusdetails
WHERE SCHEMEID = @TenderId AND SUPPLIERID = @SupplierId";

            using SqlCommand participantCmd = new SqlCommand(participantSql, con);
            participantCmd.Parameters.AddWithValue("@TenderId", tenderId);
            participantCmd.Parameters.AddWithValue("@SupplierId", supplierId);

            object? participantResult = await participantCmd.ExecuteScalarAsync();
            return participantResult != null && participantResult != DBNull.Value;
        }

        private static async Task<bool> PoBelongsToSupplierAsync(SqlConnection con, int poId, int supplierId)
        {
            const string sql = @"
SELECT 1 FROM purchase_order WHERE po_id = @PoId AND supplier_id = @SupplierId";

            using SqlCommand cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@PoId", poId);
            cmd.Parameters.AddWithValue("@SupplierId", supplierId);

            object? result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value;
        }

        private static async Task<bool> CanAddDispatchAsync(SqlConnection con, int poId, int consigneeId)
        {
            const string sql = @"
SELECT pi.quantity, ISNULL(SUM(i.Supplyqty), 0) AS supplied
FROM purchase_order po
INNER JOIN po_items pi ON pi.po_id = po.po_id
LEFT OUTER JOIN SupplierDispatch d ON d.po_id = pi.po_id AND d.location_id = pi.consignee_id
LEFT OUTER JOIN Issue_item_details i ON i.Issue_id = d.Issue_id
WHERE pi.po_id = @PoId AND pi.consignee_id = @ConsigneeId
GROUP BY pi.quantity";

            using SqlCommand cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@PoId", poId);
            cmd.Parameters.AddWithValue("@ConsigneeId", consigneeId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return true;

            decimal orderQty = ReadDecimalColumn(reader, "quantity");
            decimal supplied = ReadDecimalColumn(reader, "supplied");
            return orderQty - supplied != 0;
        }

        private async Task<List<SupplierPoDispatchEditBatchDto>> LoadDispatchEditBatchesAsync(
            SqlConnection con, int poId, int consigneeId)
        {
            const string sql = @"
SELECT d.Issue_id,
       CONVERT(VARCHAR, d.Tentative_Sdate, 103) AS Tentative_Sdate,
       CONVERT(VARCHAR, d.dispatch_date, 103) AS dispatch_date,
       CASE WHEN d.status = 'I' THEN 'Incomplete' ELSE 'Complete' END AS SupplyStatusN,
       d.dispatch_no,
       SUM(i.Supplyqty) AS quantity,
       d.po_id,
       d.location_id,
       CASE WHEN re.recieved_date IS NOT NULL THEN re.recieved_date ELSE 'Not Receipt' END AS recieved_date,
       m.categoryid
FROM SupplierDispatch d
LEFT OUTER JOIN Issue_item_details i ON d.Issue_id = i.Issue_id
INNER JOIN purchase_order po ON po.po_id = d.po_id
LEFT OUTER JOIN (
    SELECT DISTINCT pi.item_id, pi.po_id FROM po_items pi
) pi ON pi.po_id = po.po_id
INNER JOIN masitems m ON m.item_id = pi.item_id
LEFT OUTER JOIN (
    SELECT r.issue_id, CONVERT(VARCHAR, r.recieved_date, 103) AS recieved_date,
           r.po_id, r.location_id
    FROM receipts r
    INNER JOIN maslocations l ON l.location_id = r.location_id
    LEFT OUTER JOIN (
        SELECT SUM(ri.received_qty) AS received_qty, ri.receipt_id
        FROM receipt_item_details ri
        GROUP BY ri.receipt_id
    ) ri ON ri.receipt_id = r.receipt_id
    WHERE ri.received_qty IS NOT NULL
) re ON re.issue_id = d.Issue_id AND re.po_id = d.po_id
WHERE d.po_id = @PoId AND d.location_id = @ConsigneeId
GROUP BY d.Issue_id, d.dispatch_date, d.Tentative_Sdate, d.status, d.dispatch_no,
         d.po_id, d.location_id, re.recieved_date, m.categoryid";

            var batches = new List<SupplierPoDispatchEditBatchDto>();
            using SqlCommand cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@PoId", poId);
            cmd.Parameters.AddWithValue("@ConsigneeId", consigneeId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                batches.Add(new SupplierPoDispatchEditBatchDto
                {
                    IssueId = ReadIntColumn(reader, "Issue_id", "issue_id"),
                    PoId = ReadIntColumn(reader, "po_id"),
                    LocationId = ReadIntColumn(reader, "location_id"),
                    CategoryId = ReadIntColumn(reader, "categoryid"),
                    DispatchNo = ReadStringColumn(reader, "dispatch_no"),
                    DispatchDate = ReadStringColumn(reader, "dispatch_date"),
                    TentativeSupplyDate = ReadStringColumn(reader, "Tentative_Sdate", "tentative_sdate"),
                    ReceivedDate = ReadStringColumn(reader, "recieved_date"),
                    Quantity = ReadDecimalColumn(reader, "quantity"),
                    SupplyStatus = ReadStringColumn(reader, "SupplyStatusN", "supplyStatusN"),
                });
            }

            return batches;
        }

        private async Task<List<SupplierPoReceiptBatchDto>> LoadReceiptBatchesAsync(
            SqlConnection con, int poId, int consigneeId)
        {
            const string sql = @"
SELECT d.Issue_id,
       CONVERT(VARCHAR, dispatch_date, 103) AS dispatch_date,
       d.po_id, d.location_id, re.receipt_id,
       CASE WHEN re.recieved_date IS NULL THEN 'Not Receipt' ELSE re.recieved_date END AS recieved_date,
       CASE WHEN re.status IS NOT NULL THEN
            CASE WHEN re.status = 'C' THEN 'Installation Completed' ELSE 'Installation Pending' END
            ELSE CASE WHEN d.status = 'C' THEN 'Yet to be Received' ELSE 'Dispatch Pending' END
       END AS SupplyStatus
FROM SupplierDispatch d
INNER JOIN Issue_item_details i ON d.Issue_id = i.Issue_id
LEFT OUTER JOIN (
    SELECT r.issue_id, CONVERT(VARCHAR, r.recieved_date, 103) AS recieved_date,
           r.po_id, r.location_id, r.status, r.receipt_id
    FROM receipts r
    WHERE r.status = 'C'
) re ON re.issue_id = d.Issue_id AND re.po_id = d.po_id AND re.location_id = d.location_id
WHERE d.po_id = @PoId AND d.location_id = @ConsigneeId
GROUP BY re.receipt_id, d.Issue_id, dispatch_date, d.status, d.po_id, d.location_id,
         re.recieved_date, re.status, re.receipt_no";

            var batches = new List<SupplierPoReceiptBatchDto>();
            using SqlCommand cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@PoId", poId);
            cmd.Parameters.AddWithValue("@ConsigneeId", consigneeId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                int? receiptId = null;
                try
                {
                    int ordinal = reader.GetOrdinal("receipt_id");
                    if (!reader.IsDBNull(ordinal))
                        receiptId = Convert.ToInt32(reader.GetValue(ordinal));
                }
                catch (IndexOutOfRangeException)
                {
                    // optional column
                }

                batches.Add(new SupplierPoReceiptBatchDto
                {
                    IssueId = ReadIntColumn(reader, "Issue_id", "issue_id"),
                    PoId = ReadIntColumn(reader, "po_id"),
                    LocationId = ReadIntColumn(reader, "location_id"),
                    ReceiptId = receiptId,
                    DispatchDate = ReadStringColumn(reader, "dispatch_date"),
                    ReceivedDate = ReadStringColumn(reader, "recieved_date"),
                    SupplyStatus = ReadStringColumn(reader, "SupplyStatus", "supplyStatus")
                });
            }

            return batches;
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
                    // try next alias
                }
            }

            return 0;
        }

        private static async Task<List<SupplierSdPaymentModeDto>> LoadSdPaymentModesAsync(SqlConnection con)
        {
            var modes = new List<SupplierSdPaymentModeDto>();
            const string sql = "SELECT SDMode, SDNAME FROM MasSD ORDER BY SDNAME";
            using SqlCommand cmd = new SqlCommand(sql, con);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                modes.Add(new SupplierSdPaymentModeDto
                {
                    SdMode = ReadStringColumn(reader, "SDMode"),
                    SdName = ReadStringColumn(reader, "SDNAME"),
                });
            }

            return modes;
        }

        private static async Task<bool> PoSdDetailExistsAsync(SqlConnection con, int poId)
        {
            const string sql = "SELECT 1 FROM PO_SDDetails WHERE po_id = @PoId";
            using SqlCommand cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@PoId", poId);
            object? result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value;
        }

        private static async Task<bool> HasPendingPoExtensionAsync(SqlConnection con, int poId)
        {
            const string sql = "SELECT 1 FROM PO_extension_detail WHERE status = 'P' AND po_id = @PoId";
            using SqlCommand cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@PoId", poId);
            object? result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value;
        }

        private static async Task<string> GetExtensionBaseEndDateAsync(SqlConnection con, int poId, string defaultPoEndDate)
        {
            const string sql = @"
SELECT TOP 1 CONVERT(VARCHAR, extended_date, 103) AS extended_date
FROM PO_extension_detail
WHERE po_id = @PoId AND status IN ('A', 'E')
ORDER BY extended_date DESC";

            using SqlCommand cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@PoId", poId);
            object? result = await cmd.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value)
                return defaultPoEndDate;

            return result.ToString() ?? defaultPoEndDate;
        }

        private static async Task<List<SupplierPoExtensionRowDto>> LoadPoExtensionsAsync(SqlConnection con, int poId)
        {
            var rows = new List<SupplierPoExtensionRowDto>();
            const string sql = @"
SELECT ped.extensionId, ped.po_id, ped.remark, ped.days,
       CONVERT(VARCHAR, ped.extended_date, 105) AS extended_date,
       CONVERT(VARCHAR, ped.po_end_date, 105) AS po_end_date,
       ped.path,
       CONVERT(VARCHAR, ped.letter_date, 105) AS letter_date,
       ped.letter_no,
       CONVERT(VARCHAR, ped.sys_gen_apply_date, 105) AS sys_gen_apply_date,
       s.status
FROM PO_extension_detail ped
INNER JOIN master_po_extension_detail_status s ON ped.status = s.id
WHERE ped.po_id = @PoId
ORDER BY ped.extensionId DESC";

            using SqlCommand cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@PoId", poId);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string path = ReadStringColumn(reader, "path");
                rows.Add(new SupplierPoExtensionRowDto
                {
                    ExtensionId = ReadIntColumn(reader, "extensionId"),
                    PoId = ReadIntColumn(reader, "po_id"),
                    Remark = ReadStringColumn(reader, "remark"),
                    Days = ReadIntColumn(reader, "days"),
                    ExtendedDate = ReadStringColumn(reader, "extended_date"),
                    PoEndDate = ReadStringColumn(reader, "po_end_date"),
                    HasFile = !string.IsNullOrWhiteSpace(path),
                    LetterDate = ReadStringColumn(reader, "letter_date"),
                    LetterNo = ReadStringColumn(reader, "letter_no"),
                    ApplyDate = ReadStringColumn(reader, "sys_gen_apply_date"),
                    Status = ReadStringColumn(reader, "status"),
                });
            }

            return rows;
        }

        private static string ResolveLegacyUploadPath(string? virtualPath, string fileRoot)
        {
            if (string.IsNullOrWhiteSpace(virtualPath))
                return string.Empty;

            string fileName = Path.GetFileName(virtualPath.Replace('\\', '/'));
            return Path.Combine(fileRoot, fileName);
        }

        private static bool TryParseSdDate(string input, out DateTime parsedDate)
        {
            parsedDate = default;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            string[] formats = { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" };
            return DateTime.TryParseExact(
                input.Trim(),
                formats,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out parsedDate)
                || DateTime.TryParse(input.Trim(), out parsedDate);
        }

        private static bool TryParseLetterDate(string input, out DateTime parsedDate)
        {
            parsedDate = default;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            string[] formats = { "dd-MM-yyyy", "d-M-yyyy", "dd/MM/yyyy", "yyyy-MM-dd" };
            return DateTime.TryParseExact(
                input.Trim(),
                formats,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out parsedDate)
                || DateTime.TryParse(input.Trim(), out parsedDate);
        }
    }
}
