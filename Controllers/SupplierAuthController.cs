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
        private readonly string _invoiceDocRoot;
        private readonly string _emsRoleRoot;
        private readonly MongoService _mongoService;

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

            var invoiceConfigured = configuration["FileStorage:InvoiceDocPath"];
            _invoiceDocRoot = string.IsNullOrWhiteSpace(invoiceConfigured)
                ? Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "EMSRole", "Upload_invoiceDoc"))
                : Path.GetFullPath(invoiceConfigured);

            _emsRoleRoot = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "EMSRole"));
            _mongoService = new MongoService();

            Directory.CreateDirectory(_emdFileRoot);
            Directory.CreateDirectory(_sdFileRoot);
            Directory.CreateDirectory(_extensionFileRoot);
            Directory.CreateDirectory(_invoiceDocRoot);
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

            string phone = request.PhoneNo.Trim();
            if (phone.Length < 10 || phone.Length > 11)
                return BadRequest(new { message = "The limit of phn No is 10 digits." });

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
                          update_date = GETDATE()
                      WHERE supplier_id = @SupplierId", con);
                updateCmd.Parameters.AddWithValue("@SupplierId", request.SupplierId);
                updateCmd.Parameters.AddWithValue("@MobileNo", mobile);
                updateCmd.Parameters.AddWithValue("@Email", email);
                updateCmd.Parameters.AddWithValue("@PhoneNo", phone);
                updateCmd.Parameters.AddWithValue("@Address", request.Address.Trim());

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
       DocumentNo,
       ISNULL(SubmissionStatus, 'N') AS SubmissionStatus
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
                        string submissionStatus = ReadStringColumn(reader, "SubmissionStatus");
                        bool isSubmitted = submissionStatus.Equals("Y", StringComparison.OrdinalIgnoreCase);
                        if (hasExisting && sdAmount <= 0)
                        {
                            // gross value may be omitted on edit reload — amount still shown from DB on client
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
                            IsSubmitted = isSubmitted,
                            PaymentMode = paymentMode,
                            IssueDate = issueDate,
                            MaturityDate = string.IsNullOrWhiteSpace(maturityDate) ? null : maturityDate,
                            DocumentNo = documentNo,
                            PaymentModes = paymentModes,
                        });
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
                    IsSubmitted = false,
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

                string sdModeName = await GetSdPaymentModeNameAsync(con, paymentMode);
                if (!IsSdMaturityOptional(sdModeName) && string.IsNullOrWhiteSpace(maturityDate))
                    return BadRequest(new { message = "Please fill Maturity Date" });

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

                const string submittedSql = @"
SELECT ISNULL(SubmissionStatus, 'N') FROM PO_SDDetails WHERE po_id = @PoId";
                using (SqlCommand submittedCmd = new SqlCommand(submittedSql, con))
                {
                    submittedCmd.Parameters.AddWithValue("@PoId", poId);
                    object? submittedResult = await submittedCmd.ExecuteScalarAsync();
                    if (submittedResult?.ToString()?.Equals("Y", StringComparison.OrdinalIgnoreCase) == true)
                        return BadRequest(new { message = "Submitted SD detail cannot be edited." });
                }

                string sdModeName = await GetSdPaymentModeNameAsync(con, paymentMode);
                if (!IsSdMaturityOptional(sdModeName) && string.IsNullOrWhiteSpace(maturityDate))
                    return BadRequest(new { message = "Please fill Maturity Date" });

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

        /// <summary>po_supply_details.aspx — load equipment dispatch entry page.</summary>
        [HttpGet("dispatch-entry/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierDispatchEntryPage(
            int userId,
            [FromQuery] int poId,
            [FromQuery] int locId,
            [FromQuery] int issueId = 0,
            [FromQuery] int itemId = 0)
        {
            if (userId <= 0 || poId <= 0 || locId <= 0)
                return BadRequest(new { message = "PO id and consignee id are required." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                if (!await PoBelongsToSupplierAsync(con, poId, supplierId.Value))
                    return NotFound(new { message = "Purchase order not found." });

                SupplierDispatchEntryPageDto? page = await LoadDispatchEntryPageAsync(
                    con, poId, locId, itemId, issueId, supplierId.Value);
                if (page == null)
                    return NotFound(new { message = "Consignee PO line not found." });

                if (page.CategoryId == 2)
                    return BadRequest(new { message = "Reagent dispatch entry is not migrated yet. Use legacy reagent page." });

                return Ok(page);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading dispatch entry.", error = ex.Message });
            }
        }

        /// <summary>po_supply_details.aspx — generate / update invoice tab.</summary>
        [HttpPost("dispatch-entry/invoice/by-user/{userId:int}")]
        public async Task<IActionResult> SaveSupplierDispatchInvoice(
            int userId,
            [FromForm] int poId,
            [FromForm] int locId,
            [FromForm] int issueId,
            [FromForm] string challanNo,
            [FromForm] string challanDate,
            [FromForm] string invoiceNo,
            [FromForm] string invoiceDate,
            [FromForm] string ewayBillNo,
            [FromForm] string ewayBillDate,
            [FromForm] string hsnCode,
            [FromForm] string tcsValue,
            [FromForm] string invoiceGst,
            [FromForm] string remarks,
            [FromForm] string bulkVsSerial,
            IFormFile? file)
        {
            if (userId <= 0 || poId <= 0 || locId <= 0)
                return BadRequest(new { message = "Invalid request." });

            if (bulkVsSerial != "1" && bulkVsSerial != "2")
                return BadRequest(new { message = "Please Select Bulk Supply or Serial No wise Supply" });

            if (string.IsNullOrWhiteSpace(challanNo) || string.IsNullOrWhiteSpace(challanDate)
                || string.IsNullOrWhiteSpace(invoiceNo) || string.IsNullOrWhiteSpace(invoiceDate)
                || string.IsNullOrWhiteSpace(ewayBillNo) || string.IsNullOrWhiteSpace(ewayBillDate)
                || string.IsNullOrWhiteSpace(hsnCode) || string.IsNullOrWhiteSpace(tcsValue))
                return BadRequest(new { message = "Please Enter Challan & Invoice Details" });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                if (!await PoBelongsToSupplierAsync(con, poId, supplierId.Value))
                    return NotFound(new { message = "Purchase order not found." });

                string poDateDisplay = await GetPoDateDisplayAsync(con, poId);
                if (!TryParseSdDate(challanDate, out DateTime parsedChallanDate)
                    || !TryParseSdDate(invoiceDate, out DateTime parsedInvoiceDate)
                    || !TryParseSdDate(ewayBillDate, out DateTime parsedEwayDate))
                    return BadRequest(new { message = "Invalid date format. Use DD/MM/YYYY" });

                if (!string.IsNullOrWhiteSpace(poDateDisplay) && TryParseSdDate(poDateDisplay, out DateTime poDate))
                {
                    if (parsedChallanDate.Date < poDate.Date)
                        return BadRequest(new { message = "Challan Date Should be After to PO Date." });
                    if (parsedInvoiceDate.Date < poDate.Date)
                        return BadRequest(new { message = "Invoice Date Should be After to PO Date." });
                }

                int resolvedIssueId = issueId;
                if (resolvedIssueId <= 0)
                    resolvedIssueId = await GetIncompleteDispatchIssueIdAsync(con, poId, locId);

                bool isNewIssue = resolvedIssueId <= 0;

                string? existingInvoicePath = null;
                if (!isNewIssue)
                    existingInvoicePath = await GetDispatchInvoicePathAsync(con, resolvedIssueId, poId, locId, supplierId.Value);

                string virtualPath;
                if (file != null && file.Length > 0)
                {
                    if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                        return BadRequest(new { message = "Please upload pdf file only." });
                    if (file.Length > 5_000_000)
                        return BadRequest(new { message = "Your can't upload file more than 3mb." });

                    int fileIssueId = isNewIssue
                        ? await GetNextDispatchIssueIdAsync(con)
                        : resolvedIssueId;
                    string fileName = $"{fileIssueId}-InvoicDoc.pdf";
                    virtualPath = $"~/Upload_invoiceDoc/{fileName}";
                    string savePath = Path.Combine(_invoiceDocRoot, fileName);
                    await using (var stream = new FileStream(savePath, FileMode.Create))
                        await file.CopyToAsync(stream);
                }
                else if (!string.IsNullOrWhiteSpace(existingInvoicePath))
                {
                    virtualPath = existingInvoicePath;
                }
                else
                {
                    return BadRequest(new { message = "Please select document to be uplaoded." });
                }

                if (isNewIssue)
                {
                    const string insertSql = @"
INSERT INTO SupplierDispatch
    (po_id, location_id, remarks, status, challan_no, invoice_no, supplierid,
     invoice_date, challan_date, BulkVsSerial, EntryDATE, invoicedocpath,
     invoiceGST, EwayBillNo, EwayBilldt, HSNcode, TCSValue)
VALUES
    (@PoId, @LocId, @Remarks, 'I', @ChallanNo, @InvoiceNo, @SupplierId,
     @InvoiceDate, @ChallanDate, @BulkVsSerial, GETDATE(), @InvoicePath,
     @InvoiceGst, @EwayBillNo, @EwayBillDate, @HsnCode, @TcsValue);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

                    using SqlCommand insertCmd = new SqlCommand(insertSql, con);
                    insertCmd.Parameters.AddWithValue("@PoId", poId);
                    insertCmd.Parameters.AddWithValue("@LocId", locId);
                    insertCmd.Parameters.AddWithValue("@Remarks", remarks?.Trim() ?? string.Empty);
                    insertCmd.Parameters.AddWithValue("@ChallanNo", challanNo.Trim());
                    insertCmd.Parameters.AddWithValue("@InvoiceNo", invoiceNo.Trim());
                    insertCmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);
                    insertCmd.Parameters.AddWithValue("@InvoiceDate", parsedInvoiceDate);
                    insertCmd.Parameters.AddWithValue("@ChallanDate", parsedChallanDate);
                    insertCmd.Parameters.AddWithValue("@BulkVsSerial", bulkVsSerial);
                    insertCmd.Parameters.AddWithValue("@InvoicePath", virtualPath);
                    insertCmd.Parameters.AddWithValue("@InvoiceGst", invoiceGst?.Trim() ?? string.Empty);
                    insertCmd.Parameters.AddWithValue("@EwayBillNo", ewayBillNo.Trim());
                    insertCmd.Parameters.AddWithValue("@EwayBillDate", parsedEwayDate);
                    insertCmd.Parameters.AddWithValue("@HsnCode", hsnCode.Trim());
                    insertCmd.Parameters.AddWithValue("@TcsValue", tcsValue.Trim());

                    object? newId = await insertCmd.ExecuteScalarAsync();
                    resolvedIssueId = Convert.ToInt32(newId);
                    return Ok(new { message = "Invoice Details Saved,Please Fill Items Details", issueId = resolvedIssueId });
                }

                const string updateSql = @"
UPDATE SupplierDispatch
SET remarks = @Remarks, challan_no = @ChallanNo, invoice_no = @InvoiceNo,
    challan_date = @ChallanDate, invoice_date = @InvoiceDate, BulkVsSerial = @BulkVsSerial,
    invoicedocpath = @InvoicePath, entryDATE = GETDATE(), invoiceGST = @InvoiceGst,
    EwayBillNo = @EwayBillNo, EwayBilldt = @EwayBillDate, HSNcode = @HsnCode, TCSValue = @TcsValue
WHERE issue_id = @IssueId AND po_id = @PoId AND location_id = @LocId AND supplierid = @SupplierId AND status = 'I'";

                using (SqlCommand updateCmd = new SqlCommand(updateSql, con))
                {
                    updateCmd.Parameters.AddWithValue("@IssueId", resolvedIssueId);
                    updateCmd.Parameters.AddWithValue("@PoId", poId);
                    updateCmd.Parameters.AddWithValue("@LocId", locId);
                    updateCmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);
                    updateCmd.Parameters.AddWithValue("@Remarks", remarks?.Trim() ?? string.Empty);
                    updateCmd.Parameters.AddWithValue("@ChallanNo", challanNo.Trim());
                    updateCmd.Parameters.AddWithValue("@InvoiceNo", invoiceNo.Trim());
                    updateCmd.Parameters.AddWithValue("@ChallanDate", parsedChallanDate);
                    updateCmd.Parameters.AddWithValue("@InvoiceDate", parsedInvoiceDate);
                    updateCmd.Parameters.AddWithValue("@BulkVsSerial", bulkVsSerial);
                    updateCmd.Parameters.AddWithValue("@InvoicePath", virtualPath);
                    updateCmd.Parameters.AddWithValue("@InvoiceGst", invoiceGst?.Trim() ?? string.Empty);
                    updateCmd.Parameters.AddWithValue("@EwayBillNo", ewayBillNo.Trim());
                    updateCmd.Parameters.AddWithValue("@EwayBillDate", parsedEwayDate);
                    updateCmd.Parameters.AddWithValue("@HsnCode", hsnCode.Trim());
                    updateCmd.Parameters.AddWithValue("@TcsValue", tcsValue.Trim());

                    int rows = await updateCmd.ExecuteNonQueryAsync();
                    if (rows == 0)
                        return NotFound(new { message = "Incomplete dispatch issue not found." });
                }

                return Ok(new { message = "Invoice Details Updated,Please Fill Items Details", issueId = resolvedIssueId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error saving dispatch invoice.", error = ex.Message });
            }
        }

        /// <summary>po_supply_details.aspx — add / update equipment line.</summary>
        [HttpPost("dispatch-entry/equipment-line/by-user/{userId:int}")]
        public async Task<IActionResult> SaveSupplierDispatchEquipmentLine(
            int userId,
            [FromBody] SupplierDispatchEquipmentLineRequestDto request)
        {
            if (userId <= 0 || request.IssueId <= 0)
                return BadRequest(new { message = "Issue id is required." });

            if (request.SupplyQty <= 0)
                return BadRequest(new { message = "Please Check Dispatch QTY" });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                var dispatchMeta = await GetDispatchIssueMetaAsync(con, request.IssueId, supplierId.Value);
                if (dispatchMeta == null)
                    return NotFound(new { message = "Dispatch issue not found." });

                if (dispatchMeta.Value.Status != "I")
                    return BadRequest(new { message = "Dispatch is already completed." });

                decimal balanceQty = await GetDispatchBalanceQtyAsync(
                    con, dispatchMeta.Value.PoId, dispatchMeta.Value.LocationId);
                string bulkVsSerial = await GetDispatchBulkVsSerialAsync(con, request.IssueId);
                bool qtyValid = bulkVsSerial == "1"
                    ? request.SupplyQty == 1
                    : request.SupplyQty >= 1;
                if (!qtyValid)
                    return BadRequest(new { message = "Please enter 1 by 1 in Serial No Supply or more than 1 qty in Bulk Supply" });

                if (request.IssueDetailId <= 0)
                {
                    if (balanceQty < request.SupplyQty)
                        return BadRequest(new { message = "Please Check Dispatch QTY" });

                    const string insertSql = @"
INSERT INTO Issue_item_details
    (model_no, make, make_no, issue_id, item_id, equpitment_code, Supplyqty, status, entry_date, warranty_certificate_no)
VALUES
    (@ModelNo, @Make, @SerialNo, @IssueId, @ItemId, @ItemCode, @SupplyQty, 'I', GETDATE(), @WarrantyCardNo)";

                    using SqlCommand insertCmd = new SqlCommand(insertSql, con);
                    insertCmd.Parameters.AddWithValue("@ModelNo", dispatchMeta.Value.ModelNo);
                    insertCmd.Parameters.AddWithValue("@Make", dispatchMeta.Value.Make);
                    insertCmd.Parameters.AddWithValue("@SerialNo", request.SerialNo?.Trim() ?? string.Empty);
                    insertCmd.Parameters.AddWithValue("@IssueId", request.IssueId);
                    insertCmd.Parameters.AddWithValue("@ItemId", dispatchMeta.Value.ItemId);
                    insertCmd.Parameters.AddWithValue("@ItemCode", dispatchMeta.Value.ItemCode);
                    insertCmd.Parameters.AddWithValue("@SupplyQty", request.SupplyQty);
                    insertCmd.Parameters.AddWithValue("@WarrantyCardNo", request.WarrantyCardNo?.Trim() ?? string.Empty);
                    await insertCmd.ExecuteNonQueryAsync();
                    return Ok(new { message = "Saved Successfully" });
                }

                if (balanceQty + request.SupplyQty < request.SupplyQty)
                    return BadRequest(new { message = "Please Check Dispatch QTY" });

                const string updateSql = @"
UPDATE Issue_item_details
SET make_no = @SerialNo, warranty_certificate_no = @WarrantyCardNo,
    Supplyqty = @SupplyQty, entry_date = GETDATE()
WHERE issue_detail_id = @IssueDetailId AND issue_id = @IssueId";

                using SqlCommand updateCmd = new SqlCommand(updateSql, con);
                updateCmd.Parameters.AddWithValue("@SerialNo", request.SerialNo?.Trim() ?? string.Empty);
                updateCmd.Parameters.AddWithValue("@WarrantyCardNo", request.WarrantyCardNo?.Trim() ?? string.Empty);
                updateCmd.Parameters.AddWithValue("@SupplyQty", request.SupplyQty);
                updateCmd.Parameters.AddWithValue("@IssueDetailId", request.IssueDetailId);
                updateCmd.Parameters.AddWithValue("@IssueId", request.IssueId);
                int rows = await updateCmd.ExecuteNonQueryAsync();
                if (rows == 0)
                    return NotFound(new { message = "Equipment line not found." });

                return Ok(new { message = "Update Successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error saving equipment line.", error = ex.Message });
            }
        }

        /// <summary>po_supply_details.aspx — complete dispatch.</summary>
        [HttpPost("dispatch-entry/complete/by-user/{userId:int}")]
        public async Task<IActionResult> CompleteSupplierDispatch(
            int userId,
            [FromBody] SupplierDispatchCompleteRequestDto request)
        {
            if (userId <= 0 || request.PoId <= 0 || request.LocationId <= 0 || request.IssueId <= 0)
                return BadRequest(new { message = "Invalid request." });

            if (string.IsNullOrWhiteSpace(request.DispatchNo) || string.IsNullOrWhiteSpace(request.DispatchDate)
                || string.IsNullOrWhiteSpace(request.TentativeSupplyDate))
                return BadRequest(new { message = "Please enter dispatch details." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                if (!TryParseSdDate(request.DispatchDate, out _))
                    return BadRequest(new { message = "Invalid dispatch date format." });
                if (!TryParseSdDate(request.TentativeSupplyDate, out _))
                    return BadRequest(new { message = "Invalid tentative supply date format." });

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                if (!await PoBelongsToSupplierAsync(con, request.PoId, supplierId.Value))
                    return NotFound(new { message = "Purchase order not found." });

                var dispatchMeta = await GetDispatchIssueMetaAsync(con, request.IssueId, supplierId.Value);
                if (dispatchMeta == null || dispatchMeta.Value.PoId != request.PoId || dispatchMeta.Value.LocationId != request.LocationId)
                    return NotFound(new { message = "Dispatch issue not found." });

                string? challanDate = await GetDispatchChallanDateAsync(con, request.IssueId);
                if (!string.IsNullOrWhiteSpace(challanDate)
                    && TryParseSdDate(challanDate, out DateTime parsedChallan)
                    && TryParseSdDate(request.DispatchDate, out DateTime parsedDispatch)
                    && parsedDispatch.Date < parsedChallan.Date)
                    return BadRequest(new { message = "Dispatch Date Should be After/Equal to Challan Date." });

                if (TryParseSdDate(request.DispatchDate, out DateTime dispatchDate)
                    && TryParseSdDate(request.TentativeSupplyDate, out DateTime tentativeDate)
                    && tentativeDate.Date < dispatchDate.Date)
                    return BadRequest(new { message = "Tentative Date Should be After to Dispatch Date." });

                if (!await DispatchIssueHasEquipmentLinesAsync(con, request.IssueId))
                    return BadRequest(new { message = "Please Fill Equipment Entry Tab and Click + Button Before Complete Dispatch" });

                const string updateDispatchSql = @"
UPDATE SupplierDispatch
SET status = 'C', dispatch_no = @DispatchNo,
    Tentative_Sdate = CONVERT(date, @TentativeDate, 103),
    dispatch_date = CONVERT(date, @DispatchDate, 103),
    supplier_entry = GETDATE()
WHERE Issue_id = @IssueId AND po_id = @PoId AND location_id = @LocId AND supplierid = @SupplierId";

                using (SqlCommand updateDispatchCmd = new SqlCommand(updateDispatchSql, con))
                {
                    updateDispatchCmd.Parameters.AddWithValue("@DispatchNo", request.DispatchNo.Trim());
                    updateDispatchCmd.Parameters.AddWithValue("@TentativeDate", request.TentativeSupplyDate.Trim());
                    updateDispatchCmd.Parameters.AddWithValue("@DispatchDate", request.DispatchDate.Trim());
                    updateDispatchCmd.Parameters.AddWithValue("@IssueId", request.IssueId);
                    updateDispatchCmd.Parameters.AddWithValue("@PoId", request.PoId);
                    updateDispatchCmd.Parameters.AddWithValue("@LocId", request.LocationId);
                    updateDispatchCmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);
                    await updateDispatchCmd.ExecuteNonQueryAsync();
                }

                const string updateItemsSql = @"
UPDATE Issue_item_details
SET status = 'C',
    cgmsc_log_printed = @CgmscLogo,
    opening_manual_provided = @OperatingManual,
    calibration_certificate_prov = @CalibrationCertificate,
    org_warranty_card_rec = @WarrantyCard,
    other_statutory = @OtherStatutory,
    warranty_validity = @WarrantyValidity,
    All_otherPODoc = @PoDocuments,
    ServiceManual = @ServiceManual,
    entry_date = GETDATE()
WHERE issue_id = @IssueId";

                using SqlCommand updateItemsCmd = new SqlCommand(updateItemsSql, con);
                updateItemsCmd.Parameters.AddWithValue("@CgmscLogo", NormalizeYesNo(request.CgmscLogoPrinted));
                updateItemsCmd.Parameters.AddWithValue("@OperatingManual", NormalizeYesNo(request.OperatingManual));
                updateItemsCmd.Parameters.AddWithValue("@CalibrationCertificate", NormalizeYesNo(request.CalibrationCertificate));
                updateItemsCmd.Parameters.AddWithValue("@WarrantyCard", NormalizeYesNo(request.WarrantyCard));
                updateItemsCmd.Parameters.AddWithValue("@OtherStatutory", NormalizeYesNo(request.OtherStatutory));
                updateItemsCmd.Parameters.AddWithValue("@WarrantyValidity", NormalizeYesNo(request.WarrantyValidity));
                updateItemsCmd.Parameters.AddWithValue("@PoDocuments", NormalizeYesNo(request.PoDocuments));
                updateItemsCmd.Parameters.AddWithValue("@ServiceManual", NormalizeYesNo(request.ServiceManual));
                updateItemsCmd.Parameters.AddWithValue("@IssueId", request.IssueId);
                await updateItemsCmd.ExecuteNonQueryAsync();

                return Ok(new { message = "Equipment Dispatched Successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error completing dispatch.", error = ex.Message });
            }
        }

        [HttpGet("dispatch-entry/invoice-file/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierDispatchInvoiceFile(int userId, [FromQuery] int issueId)
        {
            if (userId <= 0 || issueId <= 0)
                return BadRequest(new { message = "Invalid request." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                const string sql = @"
SELECT invoicedocpath FROM SupplierDispatch
WHERE Issue_id = @IssueId AND supplierid = @SupplierId";

                using SqlCommand cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@IssueId", issueId);
                cmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);
                object? result = await cmd.ExecuteScalarAsync();
                string virtualPath = result?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(virtualPath))
                    return NotFound(new { message = "File not found." });

                string physicalPath = ResolveLegacyUploadPath(virtualPath, _invoiceDocRoot);
                if (!System.IO.File.Exists(physicalPath))
                    return NotFound(new { message = "File not found." });

                return PhysicalFile(physicalPath, "application/pdf", Path.GetFileName(physicalPath));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error downloading invoice file.", error = ex.Message });
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
                    new() { PoId = 0, DisplayText = "All PO", Status = string.Empty }
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
                    string status = ReadStringColumn(reader, "status");
                    options.Add(new SupplierPoReceiptOptionDto
                    {
                        PoId = ReadIntColumn(reader, "po_id"),
                        DisplayText = ReadStringColumn(reader, "data"),
                        Status = status,
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
            [FromQuery] int poId = 0,
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
SELECT c1.location_name, a.po_id, a.po_item_id, a.item_id, a.quantity,
       ISNULL(re.receiptQTY, 0) AS receiptQTY, ISNULL(ins.insqty, 0) AS insqty,
       ISNULL(sup.Supplyqty, 0) AS Supplyqty, a.consignee_id,
       R.item_name, R.item_code_as_per_tender AS item_code,
       ISNULL(dsp.DeniedQTY, 0) AS Deniedqty,
       ISNULL(dsp.deniedstatus, '-') AS DeniedStatus,
       b.outward_no + '-' + b.po_no AS po_no,
       CONVERT(VARCHAR, b.po_date, 103) AS po_date,
       CASE WHEN a.quantity = ISNULL(ins.insqty, 0) AND a.quantity = ISNULL(sup.Supplyqty, 0)
            THEN 'Installation Completed'
            WHEN ISNULL(sup.Supplyqty, 0) = 0 OR a.quantity > ISNULL(sup.Supplyqty, 0)
            THEN 'Dispatch Pending'
            ELSE 'Pending For Receipt/Installation' END AS row_status
FROM po_items a
INNER JOIN masitems R ON R.item_id = a.item_id
INNER JOIN purchase_order b ON a.po_id = b.po_id
INNER JOIN massuppliers s ON s.supplier_id = b.supplier_id
LEFT OUTER JOIN maslocations c1 ON c1.location_id = a.consignee_id
LEFT OUTER JOIN (
    SELECT po_id, ISNULL(SUM(Supplyqty), 0) AS Supplyqty, d.location_id
    FROM SupplierDispatch d
    INNER JOIN Issue_item_details i ON d.Issue_id = i.Issue_id
    WHERE d.status = 'C'
    GROUP BY po_id, d.location_id
) sup ON sup.po_id = a.po_id AND sup.location_id = a.consignee_id
LEFT OUTER JOIN (
    SELECT ISNULL(SUM(r.receipt_qty), 0) AS receiptQTY, r.po_id, r.location_id
    FROM receipts r
    WHERE r.recieved_date IS NOT NULL AND r.status IN ('C', 'Received')
    GROUP BY po_id, r.location_id
) re ON re.po_id = a.po_id AND re.location_id = a.consignee_id
LEFT OUTER JOIN (
    SELECT SUM(ri.received_qty) AS insqty, r.po_id, r.location_id
    FROM receipts r
    LEFT OUTER JOIN receipt_item_details ri ON ri.receipt_id = r.receipt_id
    WHERE r.recieved_date IS NOT NULL AND r.status IN ('C')
    GROUP BY r.po_id, r.location_id
) ins ON ins.po_id = a.po_id AND ins.location_id = a.consignee_id
LEFT OUTER JOIN (
    SELECT d.ponoid, d.DeniedQTY, d.consigneeid,
           CASE WHEN receiptid IS NULL THEN 'Receipt & Installation both denied'
                ELSE 'Installation denied' END AS deniedstatus
    FROM Descrepency d
) dsp ON dsp.ponoid = a.po_id AND dsp.consigneeid = a.consignee_id
WHERE b.supplier_id = @SupplierId
  AND b.status IN ('Order Placed', 'Partially Received', 'Completed')
  AND (@PoId = 0 OR a.po_id = @PoId)
  AND (@FinancialYearId = 0 OR b.financial_year_id = @FinancialYearId)
  AND (@StatusFilter = '' OR (
        CASE WHEN a.quantity = ISNULL(ins.insqty, 0) AND a.quantity = ISNULL(sup.Supplyqty, 0)
             THEN 'Installation Completed'
             WHEN ISNULL(sup.Supplyqty, 0) = 0 OR a.quantity > ISNULL(sup.Supplyqty, 0)
             THEN 'Dispatch Pending'
             ELSE 'Pending For Receipt/Installation' END) = @StatusFilter)
ORDER BY b.po_date DESC, c1.location_name";

                var rows = new List<SupplierPoReceiptRowDto>();
                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                var pendingRows = new List<(SupplierPoReceiptRowDto Row, int ConsigneeId, int PoId)>();
                using (SqlCommand cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@PoId", poId);
                    cmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);
                    cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId);
                    cmd.Parameters.AddWithValue("@StatusFilter", statusFilter);

                    using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int consigneeId = ReadIntColumn(reader, "consignee_id");
                        int rowPoId = ReadIntColumn(reader, "po_id");
                        pendingRows.Add((new SupplierPoReceiptRowDto
                        {
                            PoItemId = ReadIntColumn(reader, "po_item_id"),
                            PoId = rowPoId,
                            PoNo = ReadStringColumn(reader, "po_no"),
                            PoDate = ReadStringColumn(reader, "po_date"),
                            RowStatus = ReadStringColumn(reader, "row_status"),
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
                        }, consigneeId, rowPoId));
                    }
                }

                foreach (var entry in pendingRows)
                {
                    entry.Row.Batches = await LoadReceiptBatchesAsync(con, entry.PoId, entry.ConsigneeId);
                    rows.Add(entry.Row);
                }

                return Ok(rows);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading receipt details.", error = ex.Message });
            }
        }

        /// <summary>FacilityPO_Receipt1_SUP.aspx — receipt entry page for equipment dispatch issue.</summary>
        [HttpGet("receipt-entry/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierReceiptEntryPage(
            int userId,
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
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                SupplierReceiptEntryPageDto page = await LoadSupplierReceiptEntryPageAsync(
                    con, supplierId.Value, poId, locId, issueId);
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

        [HttpPost("receipt-entry/by-user/{userId:int}")]
        public async Task<IActionResult> SaveSupplierReceiptEntry(
            int userId,
            [FromBody] SupplierReceiptSaveRequestDto request)
        {
            if (userId <= 0)
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
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                SupplierReceiptEntryPageDto page = await LoadSupplierReceiptEntryPageAsync(
                    con, supplierId.Value, request.PoId, request.LocationId, request.IssueId);

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
UPDATE receipts
SET recieved_date = @ReceivedDate,
    receipt_no = @ReceiptNo,
    receipt_qty = @ReceiptQty,
    remarks = @ReceiptRemarks,
    status = 'Received',
    entryDT = GETDATE()
WHERE receipt_id = @ReceiptId";
                    using SqlCommand updateCmd = new SqlCommand(updateSql, con);
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
INSERT INTO receipts(issue_id, po_id, location_id, remarks, status, challan_no, challan_date, recieved_date,
                     receipt_no, SupplierRemarks, receipt_qty, entryDT)
VALUES(@IssueId, @PoId, @LocId, @SupplierRemarks, 'Received', @ChallanNo, @ChallanDate, @ReceivedDate,
       @ReceiptNo, @ReceiptRemarks, @ReceiptQty, GETDATE());
SELECT CAST(SCOPE_IDENTITY() AS INT);";
                    using SqlCommand insertCmd = new SqlCommand(insertSql, con);
                    insertCmd.Parameters.AddWithValue("@IssueId", request.IssueId);
                    insertCmd.Parameters.AddWithValue("@PoId", request.PoId);
                    insertCmd.Parameters.AddWithValue("@LocId", request.LocationId);
                    insertCmd.Parameters.AddWithValue("@SupplierRemarks", page.SupplierRemarks);
                    insertCmd.Parameters.AddWithValue("@ChallanNo", page.ChallanNo);
                    insertCmd.Parameters.AddWithValue("@ChallanDate", TryParseLegacyDate(page.ChallanDate, out DateTime challanDate) ? challanDate : DBNull.Value);
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

        [HttpPost("receipt-entry/installation/by-user/{userId:int}")]
        public async Task<IActionResult> SaveSupplierReceiptInstallation(
            int userId,
            [FromBody] SupplierReceiptInstallationSaveRequestDto request)
        {
            if (userId <= 0)
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
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                if (!await ReceiptBelongsToSupplierAsync(con, request.ReceiptId, supplierId.Value))
                    return NotFound(new { message = "Receipt not found for this supplier." });

                const string issueSql = @"
SELECT i.issue_detail_id, i.make_no, i.warranty_certificate_no, i.Supplyqty,
       d.issue_id, d.recieved_date
FROM Issue_item_details i
INNER JOIN SupplierDispatch d ON d.Issue_id = i.Issue_id
INNER JOIN receipts r ON r.issue_id = d.Issue_id AND r.receipt_id = @ReceiptId
WHERE i.issue_detail_id = @IssueDetailId";
                string serialNo = string.Empty;
                string warrantyCertificate = string.Empty;
                decimal dispatchQty = 0;
                DateTime? receiptDate = null;
                using (SqlCommand issueCmd = new SqlCommand(issueSql, con))
                {
                    issueCmd.Parameters.AddWithValue("@ReceiptId", request.ReceiptId);
                    issueCmd.Parameters.AddWithValue("@IssueDetailId", request.IssueDetailId);
                    using SqlDataReader reader = await issueCmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                        return NotFound(new { message = "Dispatch serial detail not found." });
                    serialNo = ReadStringColumn(reader, "make_no");
                    warrantyCertificate = ReadStringColumn(reader, "warranty_certificate_no", "Warranty_CertificateNo");
                    dispatchQty = ReadDecimalColumn(reader, "Supplyqty");
                }
                if (request.ReceivedQty > dispatchQty)
                    return BadRequest(new { message = "Installed qty cannot be more than dispatched qty." });

                const string receiptDateSql = "SELECT recieved_date FROM receipts WHERE receipt_id = @ReceiptId";
                using (SqlCommand recCmd = new SqlCommand(receiptDateSql, con))
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
FROM receipts r
INNER JOIN purchase_order p ON p.po_id = r.po_id
LEFT JOIN tenders t ON t.tender_id = p.tender_id
WHERE r.receipt_id = @ReceiptId";
                using (SqlCommand warrantyCmd = new SqlCommand(warrantySql, con))
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
UPDATE receipt_item_details
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
                using SqlCommand updateCmd = new SqlCommand(updateExistingSql, con);
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
INSERT INTO receipt_item_details(model_no, make_no, installation_date, warenty_from, warenty_to, status,
    equpitment_code, make, installation_location, receipt_id, issue_detail_id, received_qty, warranty_card_no,
    installation_by, cgmsc_log_printed, warranty_validity, manual_provided, calibration_certificate_prov,
    org_warranty_card_rec, other_statutory, inticated_po_are_received, opening_manual_provided,
    warranty_certificate_no, entryDT)
SELECT ISNULL(ci.model, '') AS model_no, i.make_no, @InstallationDate, @WarrantyFrom, @WarrantyTo, 'I',
       mi.item_code_as_per_tender, ISNULL(ci.make, ''), @InstallationLocation, @ReceiptId, @IssueDetailId, @ReceivedQty,
       @WarrantyCardNo, @InstallationBy, @CgmscLogoPrinted, @WarrantyValidity, @ServiceManual,
       @CalibrationCertificate, @WarrantyCard, @OtherStatutory, @PoDocuments, @OperatingManual,
       @WarrantyCertificateNo, GETDATE()
FROM Issue_item_details i
INNER JOIN SupplierDispatch d ON d.Issue_id = i.Issue_id
INNER JOIN purchase_order po ON po.po_id = d.po_id
INNER JOIN po_items pi ON pi.po_id = d.po_id AND pi.consignee_id = d.location_id
LEFT JOIN contract_items ci ON ci.contract_item_id = pi.contract_item_id
INNER JOIN masitems mi ON mi.item_id = pi.item_id
WHERE i.issue_detail_id = @IssueDetailId";
                    using SqlCommand insertCmd = new SqlCommand(insertSql, con);
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
UPDATE receipts
SET BulkInst = @BulkInst
WHERE receipt_id = @ReceiptId";
                using (SqlCommand bulkCmd = new SqlCommand(bulkSql, con))
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

        [HttpPost("receipt-entry/complete/by-user/{userId:int}")]
        public async Task<IActionResult> CompleteSupplierReceiptEntry(
            int userId,
            [FromBody] SupplierReceiptCompleteRequestDto request)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });
            if (request.PoId <= 0 || request.LocationId <= 0 || request.IssueId <= 0 || request.ReceiptId <= 0)
                return BadRequest(new { message = "PO, consignee, issue and receipt are required." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                if (!await ReceiptBelongsToSupplierAsync(con, request.ReceiptId, supplierId.Value))
                    return NotFound(new { message = "Receipt not found for this supplier." });

                const string countSql = "SELECT COUNT(*) FROM receipt_item_details WHERE receipt_id = @ReceiptId";
                using (SqlCommand countCmd = new SqlCommand(countSql, con))
                {
                    countCmd.Parameters.AddWithValue("@ReceiptId", request.ReceiptId);
                    int count = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                    if (count == 0)
                        return BadRequest(new { message = "Please save installation details before completion." });
                }

                const string dispatchCountSql = "SELECT COUNT(*) FROM Issue_item_details WHERE Issue_id = @IssueId";
                int dispatchCount;
                using (SqlCommand dispatchCountCmd = new SqlCommand(dispatchCountSql, con))
                {
                    dispatchCountCmd.Parameters.AddWithValue("@IssueId", request.IssueId);
                    dispatchCount = Convert.ToInt32(await dispatchCountCmd.ExecuteScalarAsync());
                }

                const string installedCountSql = "SELECT COUNT(*) FROM receipt_item_details WHERE receipt_id = @ReceiptId";
                int installedCount;
                using (SqlCommand installedCountCmd = new SqlCommand(installedCountSql, con))
                {
                    installedCountCmd.Parameters.AddWithValue("@ReceiptId", request.ReceiptId);
                    installedCount = Convert.ToInt32(await installedCountCmd.ExecuteScalarAsync());
                }
                if (dispatchCount != installedCount)
                    return BadRequest(new { message = "Number of dispatched items and installed items do not match." });

                bool bulkInst = false;
                const string bulkFlagSql = "SELECT ISNULL(BulkInst, 'N') FROM receipts WHERE receipt_id = @ReceiptId";
                using (SqlCommand bulkFlagCmd = new SqlCommand(bulkFlagSql, con))
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
FROM receipts
WHERE receipt_id = @ReceiptId";
                    using SqlCommand bulkFilesCmd = new SqlCommand(bulkFilesSql, con);
                    bulkFilesCmd.Parameters.AddWithValue("@ReceiptId", request.ReceiptId);
                    using SqlDataReader reader = await bulkFilesCmd.ExecuteReaderAsync();
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
FROM receipt_item_details
WHERE receipt_id = @ReceiptId";
                    using SqlCommand rowFilesCmd = new SqlCommand(rowFilesSql, con);
                    rowFilesCmd.Parameters.AddWithValue("@ReceiptId", request.ReceiptId);
                    using SqlDataReader reader = await rowFilesCmd.ExecuteReaderAsync();
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
UPDATE receipts
SET IsSUPInstEntry = 'Y', status = 'C', entryDT = GETDATE()
WHERE receipt_id = @ReceiptId";
                using (SqlCommand recCmd = new SqlCommand(updateReceiptSql, con))
                {
                    recCmd.Parameters.AddWithValue("@ReceiptId", request.ReceiptId);
                    await recCmd.ExecuteNonQueryAsync();
                }

                const string updateDetailsSql = @"
UPDATE receipt_item_details
SET status = 'C', entryDT = GETDATE()
WHERE receipt_id = @ReceiptId";
                using (SqlCommand detCmd = new SqlCommand(updateDetailsSql, con))
                {
                    detCmd.Parameters.AddWithValue("@ReceiptId", request.ReceiptId);
                    await detCmd.ExecuteNonQueryAsync();
                }

                const string updateDispatchSql = @"
UPDATE SupplierDispatch
SET status = 'C'
WHERE Issue_id = @IssueId AND po_id = @PoId AND location_id = @LocId";
                using (SqlCommand dspCmd = new SqlCommand(updateDispatchSql, con))
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

        [HttpPost("receipt-entry/file/by-user/{userId:int}")]
        public async Task<IActionResult> UploadSupplierReceiptInstallationFile(
            int userId,
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
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                if (!await ReceiptBelongsToSupplierAsync(con, receiptId, supplierId.Value))
                    return NotFound(new { message = "Receipt not found for this supplier." });

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
UPDATE receipts SET {columnName} = @FileToken WHERE receipt_id = @ReceiptId;
UPDATE receipt_item_details SET bulkinst = 'Y', {columnName} = @FileToken, ISmongo = 'Y' WHERE receipt_id = @ReceiptId;";
                    using SqlCommand bulkCmd = new SqlCommand(bulkSql, con);
                    bulkCmd.Parameters.AddWithValue("@FileToken", fileToken);
                    bulkCmd.Parameters.AddWithValue("@ReceiptId", receiptId);
                    await bulkCmd.ExecuteNonQueryAsync();
                }
                else
                {
                    string rowSql = $@"
UPDATE receipt_item_details
SET {columnName} = @FileToken, ISmongo = 'Y'
WHERE item_detail_id = @ItemDetailId AND receipt_id = @ReceiptId";
                    using SqlCommand rowCmd = new SqlCommand(rowSql, con);
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

        /// <summary>Facility_InstallationReportSUP.aspx — installation report grid for a receipt.</summary>
        [HttpGet("installation-report/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierInstallationReport(
            int userId,
            [FromQuery] int receiptId)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });
            if (receiptId <= 0)
                return BadRequest(new { message = "Receipt id is required." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                if (!await ReceiptBelongsToSupplierAsync(con, receiptId, supplierId.Value))
                    return NotFound(new { message = "Receipt not found for this supplier." });

                const string headerSql = @"
SELECT receipt_id,
       CONVERT(VARCHAR, recieved_date, 103) AS received_date,
       ISNULL(BulkInst, 'N') AS BulkInst,
       ISNULL(InstalationReportFile, 'N') AS InstalationReportFile,
       ISNULL(InstalationPhoto, 'N') AS InstalationPhoto,
       ISNULL(Challanfile, 'N') AS Challanfile,
       ISNULL(WarrantyCardFile, 'N') AS WarrantyCardFile
FROM receipts
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
FROM receipt_item_details ri
WHERE ri.receipt_id = @ReceiptId
ORDER BY ri.item_detail_id";

                SupplierInstallationReportPageDto page = new()
                {
                    ReceiptId = receiptId,
                };

                using (SqlCommand headerCmd = new SqlCommand(headerSql, con))
                {
                    headerCmd.Parameters.AddWithValue("@ReceiptId", receiptId);
                    using SqlDataReader reader = await headerCmd.ExecuteReaderAsync();
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
                using (SqlCommand rowsCmd = new SqlCommand(rowsSql, con))
                {
                    rowsCmd.Parameters.AddWithValue("@ReceiptId", receiptId);
                    using SqlDataReader reader = await rowsCmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        slNo++;
                        bool isMongo = ReadStringColumn(reader, "ISmongo")
                            .Equals("Y", StringComparison.OrdinalIgnoreCase);

                        page.Rows.Add(new SupplierInstallationReportRowDto
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

        /// <summary>InstalationReport.aspx — printable installation certificate.</summary>
        [HttpGet("installation-report/print/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierInstallationPrintReport(
            int userId,
            [FromQuery] int receiptItemId)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });
            if (receiptItemId <= 0)
                return BadRequest(new { message = "Receipt item id is required." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                const string sql = @"
SELECT po.po_no,
       mi.item_name,
       s.name,
       ii.Supplyqty,
       l.location_name,
       l.address_1 + ' ' + ISNULL(l.address_2, '') + ' ' + ISNULL(l.address_3, '') AS Addresss,
       CONVERT(VARCHAR, r.recieved_date, 103) AS recieved_date,
       ri.item_detail_id,
       ri.make_no,
       ri.installation_location,
       mi.item_name AS training_item_name,
       CASE WHEN ri.warranty_validity = 'Y' THEN 'YES' ELSE 'NO' END AS warranty_validity,
       CASE WHEN ri.cgmsc_log_printed = 'Y' THEN 'YES' ELSE 'NO' END AS cgmsc_log_printed,
       CASE WHEN ri.manual_provided = 'Y' THEN 'YES' ELSE 'NO' END AS manual_provided,
       CASE WHEN ri.opening_manual_provided = 'Y' THEN 'YES' ELSE 'NO' END AS opening_manual_provided,
       CASE WHEN ri.calibration_certificate_prov = 'Y' THEN 'YES' ELSE 'NO' END AS calibration_certificate_prov,
       CASE WHEN ri.org_warranty_card_rec = 'Y' THEN 'YES' ELSE 'NO' END AS org_warranty_card_rec,
       CASE WHEN ri.other_statutory = 'Y' THEN 'YES' ELSE 'NO' END AS other_statutory,
       CASE WHEN ri.inticated_po_are_received = 'Y' THEN 'YES' ELSE 'NO' END AS inticated_po_are_received,
       r.dispatch_no,
       CONVERT(VARCHAR, r.dispatch_date, 103) AS dispatch_date
FROM receipt_item_details ri
INNER JOIN receipts r ON r.receipt_id = ri.receipt_id
INNER JOIN Issue_item_details ii ON ii.issue_detail_id = ri.issue_detail_id
INNER JOIN SupplierDispatch d ON d.Issue_id = ii.Issue_id
INNER JOIN purchase_order po ON po.po_id = d.po_id
INNER JOIN masitems mi ON mi.item_id = ii.item_id
INNER JOIN massuppliers s ON s.supplier_id = po.supplier_id
INNER JOIN maslocations l ON l.location_id = r.location_id
WHERE ri.item_detail_id = @ReceiptItemId AND po.supplier_id = @SupplierId";

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                using SqlCommand cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@ReceiptItemId", receiptItemId);
                cmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound(new { message = "Installation report not found." });

                string dispatchNo = ReadStringColumn(reader, "dispatch_no").Trim();
                var report = new SupplierInstallationPrintDto
                {
                    ItemDetailId = receiptItemId,
                    ItemName = ReadStringColumn(reader, "item_name"),
                    SupplierName = ReadStringColumn(reader, "name"),
                    SupplyQty = ReadStringColumn(reader, "Supplyqty"),
                    PoNo = ReadStringColumn(reader, "po_no"),
                    SerialNo = ReadStringColumn(reader, "make_no"),
                    ConsigneeAddress = ReadStringColumn(reader, "Addresss"),
                    ReceiptDate = ReadStringColumn(reader, "recieved_date"),
                    InstallationLocation = ReadStringColumn(reader, "installation_location"),
                    TrainingItemName = ReadStringColumn(reader, "training_item_name"),
                    WarrantyValidity = ReadStringColumn(reader, "warranty_validity"),
                    CgmscLogoPrinted = ReadStringColumn(reader, "cgmsc_log_printed"),
                    ServiceManualProvided = ReadStringColumn(reader, "manual_provided"),
                    OperatingManualProvided = ReadStringColumn(reader, "opening_manual_provided"),
                    CalibrationCertificateProvided = ReadStringColumn(reader, "calibration_certificate_prov"),
                    OriginalWarrantyCardReceived = ReadStringColumn(reader, "org_warranty_card_rec"),
                    OtherStatutoryDocuments = ReadStringColumn(reader, "other_statutory"),
                    AllAccessoriesReceived = ReadStringColumn(reader, "inticated_po_are_received"),
                    DispatchNo = dispatchNo.Length > 0 ? $"DispatchNo:{dispatchNo}" : string.Empty,
                    DispatchDate = dispatchNo.Length > 0
                        ? $"Dispatch Date :{ReadStringColumn(reader, "dispatch_date")}"
                        : string.Empty,
                };

                return Ok(report);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading installation print report.", error = ex.Message });
            }
        }

        /// <summary>rdlcPoReport.aspx — printable purchase order.</summary>
        [HttpGet("po-report/print/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierPoPrintReport(int userId, [FromQuery] int poId)
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

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                if (!await PoBelongsToSupplierAsync(con, poId, supplierId.Value))
                    return NotFound(new { message = "Purchase order not found for this supplier." });

                const string itemsSql = @"
SELECT dbo.purchase_order.outward_no,
       CASE WHEN dbo.purchase_order.outward_no IS NOT NULL
            THEN CONVERT(VARCHAR(10), dbo.purchase_order.po_date, 103) ELSE '' END AS po_date,
       dbo.purchase_order.po_no,
       dbo.purchase_order.po_id,
       dbo.masitems.item_name,
       dbo.massuppliers.name,
       dbo.massuppliers.address,
       dbo.massuppliers.mobile_no,
       dbo.massuppliers.email_id,
       dbo.tenders.tender_no,
       CONVERT(VARCHAR(10), dbo.tenders.tender_date, 103) AS tender_date,
       dbo.masitems.item_code_as_per_tender AS item_code,
       dbo.masitems.item_desc,
       dbo.contract_items.model,
       dbo.contract_items.single_unit_price,
       dbo.contract_items.basic_rate,
       SUM(dbo.po_items.quantity) AS quantity,
       dbo.purchase_order.total_po_valuew AS Expr2,
       dbo.purchase_order.po_valuew AS Expr3,
       dbo.purchase_order.total_po_value AS totalsum,
       dbo.purchase_order.po_value,
       dbo.contract_items.percentage,
       dbo.po_tranche.tranche_days,
       ISNULL(LTP.CMC1, 0) AS CMC1,
       ISNULL(LTP.cmc2, 0) AS CMC2,
       ISNULL(LTP.cmc3, 0) AS CMC3,
       ISNULL(LTP.cmc4, 0) AS CMC4,
       ISNULL(LTP.cmc5, 0) AS CMC5,
       pia.Prev_SoissueNo,
       CONVERT(VARCHAR(10), pia.Prev_SoissueDT, 103) AS Prev_SoissueDT,
       ISNULL(amencount, 0) AS amencount,
       CASE WHEN dbo.tenders.isGemTender = 'Y' THEN dbo.purchase_order.GeMBidno ELSE 'NA' END AS GemPO
FROM dbo.purchase_order
INNER JOIN dbo.po_items ON dbo.purchase_order.po_id = dbo.po_items.po_id
INNER JOIN dbo.po_tranche ON dbo.po_tranche.po_id = dbo.purchase_order.po_id
INNER JOIN dbo.masitems ON dbo.po_items.item_id = dbo.masitems.item_id
INNER JOIN dbo.massuppliers ON dbo.purchase_order.supplier_id = dbo.massuppliers.supplier_id
INNER JOIN dbo.tenders ON dbo.purchase_order.tender_id = dbo.tenders.tender_id
LEFT OUTER JOIN DBO.tender_items TI ON TI.tender_id = DBO.tenders.tender_id AND TI.item_id = dbo.po_items.item_id
LEFT OUTER JOIN live_tender_price LTP ON LTP.tender_item_id = TI.tender_item_id AND ltp.isaccept = 'Y'
INNER JOIN dbo.maslocations ON dbo.po_items.consignee_id = dbo.maslocations.location_id
INNER JOIN dbo.facility_aut ON dbo.facility_aut.facility_aut_id = dbo.maslocations.authority
INNER JOIN dbo.contract_items ON dbo.contract_items.contract_item_id = dbo.po_items.contract_item_id
INNER JOIN dbo.award_of_contract ON dbo.award_of_contract.award_of_contract_id = dbo.contract_items.award_of_contract_id
LEFT OUTER JOIN (
    SELECT po_id, MAX(po_ammdid) AS po_ammdid FROM PODTAmendment GROUP BY po_id
) mAmd ON mAmd.po_id = dbo.purchase_order.po_id
LEFT OUTER JOIN PODTAmendment pia ON pia.po_ammdid = mAmd.po_ammdid
LEFT OUTER JOIN (
    SELECT po_id, COUNT(po_ammdid) AS amencount FROM PODTAmendment GROUP BY po_id
) mamtcount ON mamtcount.po_id = dbo.purchase_order.po_id
WHERE dbo.purchase_order.po_id = @PoId
GROUP BY dbo.purchase_order.po_no,
         dbo.purchase_order.po_id,
         dbo.masitems.item_name,
         dbo.massuppliers.name,
         dbo.massuppliers.address,
         dbo.massuppliers.mobile_no,
         dbo.massuppliers.email_id,
         dbo.tenders.tender_no,
         dbo.tenders.tender_date,
         dbo.masitems.item_code_as_per_tender,
         dbo.masitems.item_desc,
         dbo.contract_items.model,
         dbo.contract_items.single_unit_price,
         dbo.contract_items.basic_rate,
         dbo.purchase_order.outward_no,
         dbo.purchase_order.po_date,
         dbo.purchase_order.total_po_valuew,
         dbo.purchase_order.po_valuew,
         dbo.purchase_order.total_po_value,
         dbo.purchase_order.po_value,
         dbo.contract_items.percentage,
         dbo.po_tranche.tranche_days,
         LTP.cmc1, LTP.cmc2, LTP.cmc3, LTP.cmc4, LTP.cmc5,
         pia.Prev_SoissueNo, pia.Prev_SoissueDT, amencount,
         dbo.purchase_order.GeMBidno, dbo.tenders.isGemTender";

                const string termsSql = @"
SELECT term_condition_id, term_condition
FROM terms_conditions t
INNER JOIN purchase_order p ON p.tender_id = t.tender_id
WHERE p.po_id = @PoId";

                const string consigneeSql = @"
SELECT DISTINCT CONVERT(VARCHAR, icd.consolidated_date, 103) AS consolidated_date,
       ml.location_name,
       i.quantity
FROM po_items i
INNER JOIN PURCHASE_ORDER p ON i.po_id = p.po_id
INNER JOIN award_of_contract awc ON awc.tender_id = p.tender_id AND awc.supplier_id = p.supplier_id
INNER JOIN maslocations ml ON ml.location_id = i.consignee_id
INNER JOIN indent_items ii ON ii.item_id = i.item_id AND ii.indent_item_id = i.indent_item_id AND ii.indent_id = i.indent_id
INNER JOIN indent id ON id.indent_id = ii.indent_id AND id.facility_id = ml.location_id
INNER JOIN indent_cons_items ic ON ic.indent_cons_items_id = id.indent_cons_items_id AND ic.item_id = i.item_id
INNER JOIN indent_consolidation icd ON icd.indent_consolidation_id = ic.indent_consolidated_id
    AND icd.directorate_id = i.directorate_id AND icd.indent_consolidation_id = i.INDENT_CONSOLIDATION_ID
WHERE p.po_id = @PoId";

                var report = new SupplierPoPrintDto { PoId = poId };
                bool hasItems = false;

                using (SqlCommand itemsCmd = new SqlCommand(itemsSql, con))
                {
                    itemsCmd.Parameters.AddWithValue("@PoId", poId);
                    using SqlDataReader reader = await itemsCmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        if (!hasItems)
                        {
                            hasItems = true;
                            report.OutwardNo = ReadStringColumn(reader, "outward_no");
                            report.PoDate = ReadStringColumn(reader, "po_date");
                            report.PoNo = ReadStringColumn(reader, "po_no");
                            report.SupplierName = ReadStringColumn(reader, "name");
                            report.SupplierAddress = ReadStringColumn(reader, "address");
                            report.MobileNo = ReadStringColumn(reader, "mobile_no");
                            report.EmailId = ReadStringColumn(reader, "email_id");
                            report.TenderNo = ReadStringColumn(reader, "tender_no");
                            report.TenderDate = ReadStringColumn(reader, "tender_date");
                            report.TotalPoValueWords = ReadStringColumn(reader, "Expr2");
                            report.BasicRate = ReadDecimalColumn(reader, "basic_rate");
                            report.GstPercent = ReadDecimalColumn(reader, "percentage");
                            report.TrancheDays = (int)ReadDecimalColumn(reader, "tranche_days");
                            report.GemPo = ReadStringColumn(reader, "GemPO");
                            report.Cmc1 = FormatCmcValue(ReadStringColumn(reader, "CMC1"));
                            report.Cmc2 = FormatCmcValue(ReadStringColumn(reader, "CMC2"));
                            report.Cmc3 = FormatCmcValue(ReadStringColumn(reader, "CMC3"));
                            report.Cmc4 = FormatCmcValue(ReadStringColumn(reader, "CMC4"));
                            report.Cmc5 = FormatCmcValue(ReadStringColumn(reader, "CMC5"));
                            report.AmendNo = ReadStringColumn(reader, "amencount");
                            report.PreviousOutwardNo = ReadStringColumn(reader, "Prev_SoissueNo");
                            report.PreviousPoDate = ReadStringColumn(reader, "Prev_SoissueDT");
                        }

                        decimal quantity = ReadDecimalColumn(reader, "quantity");
                        decimal unitPrice = ReadDecimalColumn(reader, "single_unit_price");
                        decimal lineAmount = quantity * unitPrice;
                        report.Items.Add(new SupplierPoPrintItemDto
                        {
                            ItemCode = ReadStringColumn(reader, "item_code"),
                            ItemName = ReadStringColumn(reader, "item_name"),
                            Model = ReadStringColumn(reader, "model"),
                            Quantity = quantity,
                            SingleUnitPrice = unitPrice,
                            LineAmount = lineAmount,
                        });
                        report.ItemsTotal += lineAmount;
                    }
                }

                if (!hasItems)
                    return NotFound(new { message = "Purchase order report not found." });

                using (SqlCommand termsCmd = new SqlCommand(termsSql, con))
                {
                    termsCmd.Parameters.AddWithValue("@PoId", poId);
                    using SqlDataReader reader = await termsCmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        report.Terms.Add(new SupplierPoPrintTermDto
                        {
                            TermConditionId = (int)ReadDecimalColumn(reader, "term_condition_id"),
                            TermCondition = ReadStringColumn(reader, "term_condition"),
                        });
                    }
                }

                using (SqlCommand consigneeCmd = new SqlCommand(consigneeSql, con))
                {
                    consigneeCmd.Parameters.AddWithValue("@PoId", poId);
                    using SqlDataReader reader = await consigneeCmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        report.Consignees.Add(new SupplierPoPrintConsigneeDto
                        {
                            ConsolidatedDate = ReadStringColumn(reader, "consolidated_date"),
                            LocationName = ReadStringColumn(reader, "location_name"),
                            Quantity = ReadDecimalColumn(reader, "quantity"),
                        });
                    }
                }

                string? copyToSql = await BuildPoPrintCopyToSqlAsync(con, poId);
                if (!string.IsNullOrWhiteSpace(copyToSql))
                {
                    using SqlCommand copyCmd = new SqlCommand(copyToSql, con);
                    copyCmd.Parameters.AddWithValue("@PoId", poId);
                    using SqlDataReader reader = await copyCmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        report.CopyTo.Add(new SupplierPoPrintCopyToDto
                        {
                            Designation = ReadStringColumn(reader, "desig"),
                            Office = ReadStringColumn(reader, "office"),
                        });
                    }
                }

                return Ok(report);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading PO print report.", error = ex.Message });
            }
        }

        /// <summary>Facility_InstallationReportSUP.aspx — view/download installation documents.</summary>
        [HttpGet("installation-report/file/by-user/{userId:int}")]
        public async Task<IActionResult> DownloadSupplierInstallationFile(
            int userId,
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
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                if (!await ReceiptBelongsToSupplierAsync(con, receiptId, supplierId.Value))
                    return NotFound(new { message = "Receipt not found for this supplier." });

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

                    using SqlCommand bulkCmd = new SqlCommand(bulkSql, con);
                    bulkCmd.Parameters.AddWithValue("@ReceiptId", receiptId);
                    using SqlDataReader reader = await bulkCmd.ExecuteReaderAsync();
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

                    using SqlCommand itemCmd = new SqlCommand(itemSql, con);
                    itemCmd.Parameters.AddWithValue("@ItemDetailId", itemDetailId);
                    itemCmd.Parameters.AddWithValue("@ReceiptId", receiptId);
                    using SqlDataReader reader = await itemCmd.ExecuteReaderAsync();
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

        /// <summary>SanctionsRDLC.aspx — printable sanction report for paid PO (supplier).</summary>
        [HttpGet("sanction-report/by-user/{userId:int}")]
        public async Task<IActionResult> GetSupplierSanctionReport(
            int userId,
            [FromQuery] int poId,
            [FromQuery] int sanctionId)
        {
            if (userId <= 0)
                return BadRequest(new { message = "Invalid user id." });
            if (poId <= 0 || sanctionId <= 0)
                return BadRequest(new { message = "PO id and sanction id are required." });

            try
            {
                int? supplierId = await ResolveSupplierIdForUserAsync(userId);
                if (supplierId == null)
                    return NotFound(new { message = "Supplier user not found." });

                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                if (!await PoBelongsToSupplierAsync(con, poId, supplierId.Value))
                    return NotFound(new { message = "Purchase order not found for this supplier." });

                const string ownSql = @"
SELECT COUNT(1)
FROM BLPSANCTIONS s
INNER JOIN purchase_order p ON p.po_id = s.po_id
WHERE s.SANCTIONID = @SanctionId AND s.po_id = @PoId AND p.supplier_id = @SupplierId";
                using (SqlCommand ownCmd = new SqlCommand(ownSql, con))
                {
                    ownCmd.Parameters.AddWithValue("@SanctionId", sanctionId);
                    ownCmd.Parameters.AddWithValue("@PoId", poId);
                    ownCmd.Parameters.AddWithValue("@SupplierId", supplierId.Value);
                    int owned = Convert.ToInt32(await ownCmd.ExecuteScalarAsync() ?? 0);
                    if (owned <= 0)
                        return NotFound(new { message = "Sanction not found for this supplier PO." });
                }

                var report = new SupplierSanctionReportDto
                {
                    PoId = poId,
                    SanctionId = sanctionId,
                };

                const string itemsSql = @"
SELECT p.outward_no,
       ISNULL(CONVERT(VARCHAR, p.soissueDT, 103), '-') AS soissueDate,
       m.item_code_as_per_tender AS itemcode,
       m.item_name AS itemname,
       pi.percentage AS percentvalue,
       pi.basicrate AS basicrate,
       t.tender_no AS SchemeName,
       CONVERT(VARCHAR, p.po_date, 103) AS PoDate,
       f.year AS AccYear,
       s.name AS SupplierName,
       p.po_no AS PoNo,
       ROUND(pi.basicrate + ((pi.basicrate * pi.percentage) / 100), 2) AS finalrate,
       dbo.GetPOQTY(p.po_id) AS poqty,
       ROUND((SUM(pi.quantity) * pi.basicrate), 0)
         + ROUND((SUM(pi.quantity) * pi.basicrate * pi.percentage) / 100, 0) AS poValue,
       ISNULL(sanc.SUPGST, '') AS SUPGST,
       ISNULL(sanc.HSNcode, '-') AS HSNcode,
       ISNULL(sanc.remarks, '-') AS sancremarks,
       sanc.SANCTIONNO
FROM purchase_order p
INNER JOIN po_items pi ON pi.po_id = p.po_id
INNER JOIN tenders t ON t.tender_id = p.tender_id
INNER JOIN masitems m ON m.item_id = pi.item_id
INNER JOIN mas_financial_year f ON f.financial_year_id = p.financial_year_id
INNER JOIN massuppliers s ON s.supplier_id = p.supplier_id
INNER JOIN BLPSANCTIONS sanc ON sanc.po_id = p.po_id
WHERE p.po_id = @PoId AND sanc.SANCTIONID = @SanctionId
GROUP BY pi.item_id, m.item_code_as_per_tender, m.item_name, pi.percentage, pi.basicrate,
         p.po_date, t.tender_no, f.year, s.name, p.po_no, p.po_id, p.outward_no, p.soissueDT,
         sanc.SUPGST, sanc.HSNcode, sanc.remarks, sanc.SANCTIONNO";

                using (SqlCommand cmd = new SqlCommand(itemsSql, con))
                {
                    cmd.Parameters.AddWithValue("@PoId", poId);
                    cmd.Parameters.AddWithValue("@SanctionId", sanctionId);
                    using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        if (string.IsNullOrWhiteSpace(report.PoNo))
                        {
                            report.OutwardNo = ReadStringColumn(reader, "outward_no");
                            report.SoIssueDate = ReadStringColumn(reader, "soissueDate");
                            report.SchemeName = ReadStringColumn(reader, "SchemeName");
                            report.PoDate = ReadStringColumn(reader, "PoDate");
                            report.AccYear = ReadStringColumn(reader, "AccYear");
                            report.SupplierName = ReadStringColumn(reader, "SupplierName");
                            report.PoNo = ReadStringColumn(reader, "PoNo");
                            report.SupGst = ReadStringColumn(reader, "SUPGST");
                            report.HsnCode = ReadStringColumn(reader, "HSNcode");
                            report.Remarks = ReadStringColumn(reader, "sancremarks");
                            report.SanctionNo = ReadStringColumn(reader, "SANCTIONNO");
                        }

                        report.Items.Add(new SupplierSanctionReportItemDto
                        {
                            ItemCode = ReadStringColumn(reader, "itemcode"),
                            ItemName = ReadStringColumn(reader, "itemname"),
                            PercentValue = ReadDecimalColumn(reader, "percentvalue"),
                            BasicRate = ReadDecimalColumn(reader, "basicrate"),
                            FinalRate = ReadDecimalColumn(reader, "finalrate"),
                            PoQty = ReadDecimalColumn(reader, "poqty"),
                            PoValue = ReadDecimalColumn(reader, "poValue"),
                        });
                    }
                }

                const string linesSql = @"
SELECT location_name,
       invoice_no AS InvoiceNo,
       invoice_date,
       OrderedQTY AS OrderedQty,
       SUM(received_qty) AS InvoiceAbsQty,
       GST,
       basicrate,
       SUP,
       CASE WHEN GROSSAMOUNT50 > 0 THEN '50%' ELSE '100%' END AS ptype,
       CASE WHEN GROSSAMOUNT50 > 0
            THEN ISNULL(GROSSAMOUNT50, (ROUND((SUM(received_qty) * basicrate), 0)
                 + ROUND((SUM(received_qty) * basicrate * GST) / 100, 0)))
            ELSE ISNULL(GROSSAMOUNT, (ROUND((SUM(received_qty) * basicrate), 0)
                 + ROUND((SUM(received_qty) * basicrate * GST) / 100, 0)))
       END AS Invalueonbill,
       recieved_date,
       DATEDIFF(DAY, (CASE WHEN soissueDT IS NULL THEN po_date ELSE soissueDT END), rDate) AS daystaken,
       LDDays,
       PenaltyAmount,
       ISNULL(LogoCharges_HOVerified, 'NA') AS Logo,
       LogoPenaltyAmt
FROM (
    SELECT b.InvoiceID,
           m.location_name,
           m.location_id,
           r.receipt_id,
           CASE WHEN b.INVOICENO IS NULL THEN d.invoice_no ELSE b.INVOICENO END AS invoice_no,
           CASE WHEN b.INVOICEDATE IS NULL
                THEN CONVERT(VARCHAR, d.invoice_date, 103)
                ELSE CONVERT(VARCHAR, b.INVOICEDATE, 103)
           END AS invoice_date,
           CONVERT(VARCHAR, r.recieved_date, 103) AS recieved_date,
           OrderedQTY,
           pi.basicrate,
           pi.GST,
           CASE WHEN ri.received_qty IS NULL THEN r.receipt_qty ELSE ri.received_qty END AS received_qty,
           b.GROSSAMOUNT,
           b.GROSSAMOUNT50,
           p.po_date,
           p.soissueDT,
           r.recieved_date AS rDate,
           LogoCharges_HOVerified,
           LogoPenaltyAmt,
           LDDays,
           PenaltyAmount,
           ROUND(pi.basicrate + (pi.basicrate * pi.GST / 100), 2) AS SUP
    FROM receipts r
    LEFT OUTER JOIN receipt_item_details ri ON ri.receipt_id = r.receipt_id
    INNER JOIN purchase_order p ON p.po_id = r.po_id
    LEFT OUTER JOIN (
        SELECT pi.po_item_id,
               pi.po_id,
               SUM(pi.quantity) AS OrderedQTY,
               pi.percentage AS GST,
               pi.basicrate,
               pi.consignee_id
        FROM po_items pi
        GROUP BY pi.po_id, pi.percentage, pi.basicrate, pi.consignee_id, pi.po_item_id
    ) pi ON pi.po_id = r.po_id AND pi.consignee_id = r.location_id
    INNER JOIN maslocations m ON m.location_id = r.location_id
    INNER JOIN SupplierDispatch d ON d.Issue_id = r.issue_id AND r.location_id = m.location_id
    LEFT OUTER JOIN Issue_item_details sud ON sud.Issue_id = d.Issue_id AND sud.issue_detail_id = ri.issue_detail_id
    INNER JOIN BLPINVOICES b ON b.RECEIPTID = r.receipt_id AND b.location_id = r.location_id AND b.po_id = r.po_id
    WHERE r.po_id = @PoId
      AND r.status IN ('C', 'Received')
      AND ISNULL(b.SANCTIONIDP, b.SANCTIONID) = @SanctionId
) a
GROUP BY LogoCharges_HOVerified, soissueDT, receipt_id, location_id, location_name, invoice_no, invoice_date,
         GST, basicrate, OrderedQTY, InvoiceID, GROSSAMOUNT, GROSSAMOUNT50, po_date, rDate, recieved_date,
         LogoPenaltyAmt, LDDays, PenaltyAmount, a.SUP";

                using (SqlCommand cmd = new SqlCommand(linesSql, con))
                {
                    cmd.Parameters.AddWithValue("@PoId", poId);
                    cmd.Parameters.AddWithValue("@SanctionId", sanctionId);
                    using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var line = new SupplierSanctionReportLineDto
                        {
                            LocationName = ReadStringColumn(reader, "location_name"),
                            InvoiceNo = ReadStringColumn(reader, "InvoiceNo"),
                            InvoiceDate = ReadStringColumn(reader, "invoice_date"),
                            OrderedQty = ReadDecimalColumn(reader, "OrderedQty"),
                            InvoiceAbsQty = ReadDecimalColumn(reader, "InvoiceAbsQty"),
                            Gst = ReadDecimalColumn(reader, "GST"),
                            BasicRate = ReadDecimalColumn(reader, "basicrate"),
                            Sup = ReadDecimalColumn(reader, "SUP"),
                            PaymentType = ReadStringColumn(reader, "ptype"),
                            InvoiceValueOnBill = ReadDecimalColumn(reader, "Invalueonbill"),
                            ReceivedDate = ReadStringColumn(reader, "recieved_date"),
                            DaysTaken = ReadIntColumn(reader, "daystaken"),
                            LdDays = ReadIntColumn(reader, "LDDays"),
                            PenaltyAmount = ReadDecimalColumn(reader, "PenaltyAmount"),
                            Logo = ReadStringColumn(reader, "Logo"),
                            LogoPenaltyAmt = ReadDecimalColumn(reader, "LogoPenaltyAmt"),
                        };
                        report.Lines.Add(line);
                        report.GrossInvoiceAmount += line.InvoiceValueOnBill;
                    }
                }

                report.GrossInvoiceAmount = Math.Round(report.GrossInvoiceAmount, 0);

                const string taxSql = @"
SELECT t.sanctionid,
       t.TAXPER,
       ty.taxtypename + ' (' + (CASE WHEN ty.taxcategory = 'D' THEN '-' ELSE '+' END) + ')'
         + (CASE WHEN ty.taxper IS NOT NULL AND (t.taxtypeid = 247 OR t.taxtypeid = 248 OR t.taxtypeid = 249)
                 THEN CAST(ty.taxper AS VARCHAR) + '%' ELSE '' END) AS taxtypename,
       t.taxvalue,
       ty.taxcategory,
       t.taxtypeid
FROM blptaxs t
INNER JOIN blptaxtypes ty ON t.taxtypeid = ty.taxtypeid
WHERE t.sanctionid = @SanctionId";

                using (SqlCommand cmd = new SqlCommand(taxSql, con))
                {
                    cmd.Parameters.AddWithValue("@SanctionId", sanctionId);
                    using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        report.Taxes.Add(new SupplierSanctionTaxLineDto
                        {
                            SanctionId = ReadIntColumn(reader, "sanctionid"),
                            TaxPer = ReadDecimalColumn(reader, "TAXPER"),
                            TaxTypeName = ReadStringColumn(reader, "taxtypename"),
                            TaxValue = ReadDecimalColumn(reader, "taxvalue"),
                            TaxCategory = ReadStringColumn(reader, "taxcategory"),
                            TaxTypeId = ReadIntColumn(reader, "taxtypeid"),
                        });
                    }
                }

                const string totalsSql = @"
SELECT ISNULL((SELECT SUM(ISNULL(TAXVALUE, 0)) FROM blpTaxs b
               INNER JOIN blpTaxTypes b1 ON b1.TaxTypeID = b.TaxTypeID
               WHERE sanctionid = @SanctionId AND taxcategory = 'D'), 0) AS totaldeductions,
       ISNULL((SELECT SUM(ISNULL(TAXVALUE, 0)) FROM blpTaxs b
               INNER JOIN blpTaxTypes b1 ON b1.TaxTypeID = b.TaxTypeID
               WHERE sanctionid = @SanctionId AND taxcategory = 'A'), 0) AS totaladditions,
       ISNULL((SELECT ISNULL(chequeAmt, 0) FROM BLPSANCTIONS WHERE SANCTIONID = @SanctionId), 0) AS Paid";

                using (SqlCommand cmd = new SqlCommand(totalsSql, con))
                {
                    cmd.Parameters.AddWithValue("@SanctionId", sanctionId);
                    using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        report.TotalDeductions = ReadDecimalColumn(reader, "totaldeductions");
                        report.TotalAdditions = ReadDecimalColumn(reader, "totaladditions");
                        report.PaidAmount = Math.Round(ReadDecimalColumn(reader, "Paid"), 0);
                    }
                }

                const string datesSql = @"
SELECT ISNULL(FinReceiptDT, 'Offline Receipt') AS FinReceiptDT,
       CONVERT(VARCHAR, sanctiondate, 103) AS sanctiondate
FROM blpsanctions sc
LEFT OUTER JOIN (
    SELECT MAX(fileid) AS fileid, ponoid
    FROM MASFILEMOVEMENT
    WHERE touserid = 5 AND flag = 'S'
    GROUP BY ponoid
) f ON f.ponoid = sc.po_id
LEFT OUTER JOIN (
    SELECT CONVERT(VARCHAR, todate, 103) AS FinReceiptDT, ponoid, fileid
    FROM MASFILEMOVEMENT
    WHERE touserid = 5 AND flag = 'S'
) s ON s.ponoid = sc.po_id AND s.fileid = f.fileid
WHERE sc.sanctionid = @SanctionId";

                using (SqlCommand cmd = new SqlCommand(datesSql, con))
                {
                    cmd.Parameters.AddWithValue("@SanctionId", sanctionId);
                    using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        report.FinReceiptDate = ReadStringColumn(reader, "FinReceiptDT");
                        report.SanctionDate = ReadStringColumn(reader, "sanctiondate");
                    }
                }

                report.PaidAmountWords = AmountToIndianWords(report.PaidAmount);
                report.GrossAmountWords = AmountToIndianWords(report.GrossInvoiceAmount);

                return Ok(report);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading sanction report.", error = ex.Message });
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

        /// <summary>Simple Indian-style amount-in-words (rupees, no paise).</summary>
        private static string AmountToIndianWords(decimal amount)
        {
            long n = (long)Math.Round(Math.Abs(amount), MidpointRounding.AwayFromZero);
            if (n == 0)
                return "Zero Rupees Only";

            string[] ones =
            {
                "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
                "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen",
            };
            string[] tens =
            {
                "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety",
            };

            string TwoDigits(long value)
            {
                if (value < 20)
                    return ones[value];
                long t = value / 10;
                long o = value % 10;
                return (tens[t] + (o > 0 ? " " + ones[o] : "")).Trim();
            }

            string ThreeDigits(long value)
            {
                long h = value / 100;
                long rest = value % 100;
                string part = h > 0 ? ones[h] + " Hundred" : "";
                if (rest > 0)
                    part = (part + " " + TwoDigits(rest)).Trim();
                return part;
            }

            var parts = new List<string>();
            long crore = n / 10000000;
            n %= 10000000;
            long lakh = n / 100000;
            n %= 100000;
            long thousand = n / 1000;
            n %= 1000;
            long hundred = n;

            if (crore > 0)
                parts.Add(TwoDigits(crore) + " Crore");
            if (lakh > 0)
                parts.Add(TwoDigits(lakh) + " Lakh");
            if (thousand > 0)
                parts.Add(TwoDigits(thousand) + " Thousand");
            if (hundred > 0)
                parts.Add(ThreeDigits(hundred));

            return string.Join(" ", parts) + " Rupees Only";
        }

        private static string FormatCmcValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "NA";

            return decimal.TryParse(value, out decimal parsed) && parsed == 0 ? "NA" : value;
        }

        private static async Task<string?> BuildPoPrintCopyToSqlAsync(SqlConnection con, int poId)
        {
            const string directorateSql = "SELECT directorate_id FROM purchase_order WHERE po_id = @PoId";
            string directorateId = string.Empty;

            using (SqlCommand directorateCmd = new SqlCommand(directorateSql, con))
            {
                directorateCmd.Parameters.AddWithValue("@PoId", poId);
                object? result = await directorateCmd.ExecuteScalarAsync();
                directorateId = result == null || result == DBNull.Value ? string.Empty : Convert.ToString(result) ?? string.Empty;
            }

            return directorateId switch
            {
                "12" => @"
SELECT nameconsignee AS desig, ', ' + facility_aut_name AS office
FROM facility_aut WHERE facility_aut_id = 12
UNION ALL
SELECT DISTINCT u.designation AS desig, ', ' + u.user_name AS office
FROM po_items pi
INNER JOIN purchase_order p ON p.po_id = pi.po_id
INNER JOIN maslocations l ON l.location_id = pi.consignee_id
INNER JOIN users u ON u.user_id = l.user_id
LEFT OUTER JOIN facility_type ft ON ft.facility_type_id = l.facility_type_id
WHERE pi.po_id = @PoId
UNION ALL
SELECT 'GM Finance' AS desig, ', CGMSC' AS office FROM facility_aut WHERE facility_aut_id = 12",
                "5" => @"
SELECT nameconsignee AS desig, ',' + facility_aut_name AS office
FROM facility_aut WHERE facility_aut_id = 5
UNION ALL
SELECT DISTINCT 'CMHO' AS desig, ', ' + d.DBStart_Name_En AS office
FROM po_items pi
INNER JOIN purchase_order p ON p.po_id = pi.po_id
INNER JOIN maslocations l ON l.location_id = pi.consignee_id
INNER JOIN Districts d ON d.DP_DistrictID = l.DP_DistrictID
LEFT OUTER JOIN facility_type ft ON ft.facility_type_id = l.facility_type_id
WHERE pi.po_id = @PoId
UNION ALL
SELECT DISTINCT ft.designation, ',' + l.location_name AS office
FROM po_items pi
INNER JOIN purchase_order p ON p.po_id = pi.po_id
INNER JOIN maslocations l ON l.location_id = pi.consignee_id
LEFT OUTER JOIN facility_type ft ON ft.facility_type_id = l.facility_type_id
WHERE pi.po_id = @PoId
UNION ALL
SELECT 'GM Finance' AS desig, ',CGMSC' AS office FROM facility_aut WHERE facility_aut_id = 12",
                "9" or "11" or "1" or "7" or "6" or "13" => $@"
SELECT nameconsignee AS desig, ', ' + facility_aut_name AS office
FROM facility_aut WHERE facility_aut_id = {directorateId}
UNION ALL
SELECT DISTINCT u.designation AS desig, ', ' + u.user_name AS office
FROM po_items pi
INNER JOIN purchase_order p ON p.po_id = pi.po_id
INNER JOIN maslocations l ON l.location_id = pi.consignee_id
INNER JOIN users u ON u.user_id = l.user_id
LEFT OUTER JOIN facility_type ft ON ft.facility_type_id = l.facility_type_id
WHERE pi.po_id = @PoId
UNION ALL
SELECT 'GM Finance' AS desig, ', CGMSC' AS office FROM facility_aut WHERE facility_aut_id = 12",
                _ => null,
            };
        }

        private static async Task<bool> ReceiptBelongsToSupplierAsync(
            SqlConnection con, int receiptId, int supplierId)
        {
            const string sql = @"
SELECT 1
FROM receipts r
INNER JOIN purchase_order po ON po.po_id = r.po_id
WHERE r.receipt_id = @ReceiptId AND po.supplier_id = @SupplierId";

            using SqlCommand cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@ReceiptId", receiptId);
            cmd.Parameters.AddWithValue("@SupplierId", supplierId);

            object? result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value;
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
         re.recieved_date, re.status";

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

        private async Task<SupplierReceiptEntryPageDto> LoadSupplierReceiptEntryPageAsync(
            SqlConnection con,
            int supplierId,
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
       (SELECT SUM(quantity) FROM po_items WHERE po_id = @PoId) AS poqty_all,
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
FROM SupplierDispatch d
INNER JOIN purchase_order po ON po.po_id = d.po_id
INNER JOIN po_items poi ON poi.po_id = d.po_id AND poi.consignee_id = d.location_id
LEFT JOIN po_tranche pt ON pt.po_id = po.po_id
LEFT JOIN contract_items ci ON ci.contract_item_id = poi.contract_item_id
INNER JOIN masitems mi ON mi.item_id = poi.item_id
LEFT OUTER JOIN tenders t ON t.tender_id = po.tender_id
LEFT OUTER JOIN maslocations ml ON ml.location_id = d.location_id
LEFT OUTER JOIN (
    SELECT sd.issue_id, SUM(i.Supplyqty) AS Supplyqty
    FROM SupplierDispatch sd
    INNER JOIN Issue_item_details i ON i.Issue_id = sd.Issue_id
    GROUP BY sd.issue_id
) sup ON sup.issue_id = d.issue_id
WHERE d.po_id = @PoId AND d.location_id = @LocId AND d.issue_id = @IssueId AND po.supplier_id = @SupplierId";

            SupplierReceiptEntryPageDto? page = null;
            using (SqlCommand cmd = new SqlCommand(headerSql, con))
            {
                cmd.Parameters.AddWithValue("@PoId", poId);
                cmd.Parameters.AddWithValue("@LocId", locId);
                cmd.Parameters.AddWithValue("@IssueId", issueId);
                cmd.Parameters.AddWithValue("@SupplierId", supplierId);
                using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    page = new SupplierReceiptEntryPageDto
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
                throw new InvalidOperationException("Receipt issue not found for this supplier.");
            if (page.CategoryId == 2)
                throw new InvalidOperationException("Reagent receipt entry is not migrated yet.");

            const string receiptSql = @"
SELECT TOP 1 receipt_id,
       CONVERT(VARCHAR, recieved_date, 103) AS recieved_date,
       receipt_no,
       receipt_qty,
       remarks,
       ISNULL(BulkInst, 'N') AS BulkInst,
       ISNULL(InstalationReportFile, '') AS InstalationReportFile,
       ISNULL(InstalationPhoto, '') AS InstalationPhoto,
       ISNULL(WarrantyCardFile, '') AS WarrantyCardFile,
       ISNULL(Challanfile, '') AS Challanfile
FROM receipts
WHERE issue_id = @IssueId AND po_id = @PoId AND location_id = @LocId
ORDER BY receipt_id DESC";
            using (SqlCommand receiptCmd = new SqlCommand(receiptSql, con))
            {
                receiptCmd.Parameters.AddWithValue("@IssueId", issueId);
                receiptCmd.Parameters.AddWithValue("@PoId", poId);
                receiptCmd.Parameters.AddWithValue("@LocId", locId);
                using SqlDataReader reader = await receiptCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    page.ReceiptId = ReadIntColumn(reader, "receipt_id");
                    page.ReceivedDate = ReadStringColumn(reader, "recieved_date");
                    page.ReceiptNo = ReadStringColumn(reader, "receipt_no");
                    page.ReceiptQty = ReadStringColumn(reader, "receipt_qty");
                    page.ReceiptRemarks = ReadStringColumn(reader, "remarks");
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
FROM Issue_item_details
WHERE Issue_id = @IssueId
ORDER BY issue_detail_id";
            using (SqlCommand itemCmd = new SqlCommand(issueDetailsSql, con))
            {
                itemCmd.Parameters.AddWithValue("@IssueId", issueId);
                using SqlDataReader reader = await itemCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    page.IssueDetailOptions.Add(new SupplierReceiptIssueDetailOptionDto
                    {
                        IssueDetailId = ReadIntColumn(reader, "issue_detail_id"),
                        SerialNo = ReadStringColumn(reader, "make_no"),
                        WarrantyCertificateNo = ReadStringColumn(reader, "warranty_certificate_no", "Warranty_CertificateNo"),
                        DispatchedQty = ReadDecimalColumn(reader, "Supplyqty")
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
FROM receipt_item_details
WHERE receipt_id = @ReceiptId
ORDER BY item_detail_id";
                using SqlCommand linesCmd = new SqlCommand(linesSql, con);
                linesCmd.Parameters.AddWithValue("@ReceiptId", page.ReceiptId);
                using SqlDataReader reader = await linesCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    page.InstallationLines.Add(new SupplierReceiptInstallationLineDto
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

        private static bool TryParseLegacyDate(string? input, out DateTime value)
        {
            value = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            return DateTime.TryParseExact(
                input.Trim(),
                "dd/MM/yyyy",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out value);
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
                string sdName = ReadStringColumn(reader, "SDNAME");
                modes.Add(new SupplierSdPaymentModeDto
                {
                    SdMode = ReadStringColumn(reader, "SDMode"),
                    SdName = sdName,
                    MaturityOptional = IsSdMaturityOptional(sdName),
                });
            }

            return modes;
        }

        private static async Task<string> GetSdPaymentModeNameAsync(SqlConnection con, string sdMode)
        {
            const string sql = "SELECT SDNAME FROM MasSD WHERE SDMode = @SdMode";
            using SqlCommand cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@SdMode", sdMode.Trim());
            object? result = await cmd.ExecuteScalarAsync();
            return result?.ToString() ?? string.Empty;
        }

        private static bool IsSdMaturityOptional(string sdName)
        {
            if (string.IsNullOrWhiteSpace(sdName))
                return false;

            string upper = sdName.ToUpperInvariant();
            return upper.Contains("NEFT") || upper.Contains("RTGS");
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

        private static string NormalizeYesNo(string? value)
        {
            return string.Equals(value, "Y", StringComparison.OrdinalIgnoreCase) ? "Y" : "N";
        }

        private async Task<SupplierDispatchEntryPageDto?> LoadDispatchEntryPageAsync(
            SqlConnection con,
            int poId,
            int locId,
            int itemId,
            int issueId,
            int supplierId)
        {
            string itemFilter = itemId > 0 ? " AND pi.item_id = @ItemId" : string.Empty;
            string headerSql = $@"
SELECT TOP 1 a.po_id, pi.item_id, m.categoryid,
       CASE WHEN a.soissueDT IS NOT NULL THEN CONVERT(VARCHAR, a.soissueDT, 103) ELSE CONVERT(VARCHAR, a.po_date, 103) END AS po_date,
       a.po_no, a.supplier_id, c.tender_no, pi.quantity AS po_qty_consignee,
       (SELECT SUM(quantity) FROM po_items WHERE po_id = @PoId) AS po_qty_all,
       r.item_name, r.item_code_as_per_tender AS item_code, l.location_name,
       pi.percentage, pi.basicrate, pi.totalbasicprice, pi.totalprice,
       ci.make, ci.model, CAST(pt.tranche_days AS VARCHAR(20)) AS tranche_days
FROM purchase_order a
INNER JOIN po_items pi ON pi.po_id = a.po_id AND pi.consignee_id = @LocId
LEFT JOIN po_tranche pt ON pt.po_id = a.po_id
LEFT JOIN contract_items ci ON ci.contract_item_id = pi.contract_item_id
INNER JOIN masitems r ON r.item_id = pi.item_id
INNER JOIN masitems m ON m.item_id = pi.item_id
LEFT JOIN tenders c ON c.tender_id = a.tender_id
LEFT JOIN maslocations l ON l.location_id = pi.consignee_id
WHERE a.po_id = @PoId AND a.supplier_id = @SupplierId{itemFilter}";

            SupplierDispatchEntryPageDto page = new();
            using (SqlCommand headerCmd = new SqlCommand(headerSql, con))
            {
                headerCmd.Parameters.AddWithValue("@PoId", poId);
                headerCmd.Parameters.AddWithValue("@LocId", locId);
                headerCmd.Parameters.AddWithValue("@SupplierId", supplierId);
                if (itemId > 0)
                    headerCmd.Parameters.AddWithValue("@ItemId", itemId);

                using SqlDataReader reader = await headerCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return null;

                page.PoId = poId;
                page.LocationId = locId;
                page.ItemId = ReadIntColumn(reader, "item_id");
                page.SupplierId = supplierId;
                page.CategoryId = ReadIntColumn(reader, "categoryid");
                page.PoNo = ReadStringColumn(reader, "po_no");
                page.PoDate = ReadStringColumn(reader, "po_date");
                page.TenderNo = ReadStringColumn(reader, "tender_no");
                page.ConsigneeName = ReadStringColumn(reader, "location_name");
                page.ItemCode = ReadStringColumn(reader, "item_code");
                page.ItemName = ReadStringColumn(reader, "item_name");
                page.TaxPercent = $"{ReadDecimalColumn(reader, "percentage")} %";
                page.BasicRate = ReadDecimalColumn(reader, "basicrate");
                page.TotalNetPoValue = ReadDecimalColumn(reader, "totalbasicprice");
                page.TotalGrossPoValue = ReadDecimalColumn(reader, "totalprice");
                page.PoQtyConsignee = ReadDecimalColumn(reader, "po_qty_consignee");
                page.PoQtyAllConsignees = ReadDecimalColumn(reader, "po_qty_all");
                page.ModelNo = ReadStringColumn(reader, "model");
                page.Make = ReadStringColumn(reader, "make");
                page.SupplyDays = ReadStringColumn(reader, "tranche_days");
            }

            page.DispatchedQty = await GetDispatchSuppliedQtyAsync(con, poId, locId);
            page.BalanceQty = page.PoQtyConsignee - page.DispatchedQty;

            int resolvedIssueId = issueId;
            if (resolvedIssueId <= 0)
                resolvedIssueId = await GetIncompleteDispatchIssueIdAsync(con, poId, locId);
            page.IssueId = resolvedIssueId;

            if (resolvedIssueId > 0)
                await LoadDispatchInvoiceSectionAsync(con, page, resolvedIssueId);

            page.GstOptions = await LoadSupplierGstOptionsAsync(con, supplierId);
            if (resolvedIssueId > 0)
                page.EquipmentLines = await LoadDispatchEquipmentLinesAsync(con, resolvedIssueId);

            return page;
        }

        private static async Task<decimal> GetDispatchSuppliedQtyAsync(SqlConnection con, int poId, int locId)
        {
            const string sql = @"
SELECT ISNULL(SUM(i.Supplyqty), 0)
FROM SupplierDispatch d
INNER JOIN Issue_item_details i ON d.Issue_id = i.Issue_id
WHERE d.po_id = @PoId AND d.location_id = @LocId";

            using SqlCommand cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@PoId", poId);
            cmd.Parameters.AddWithValue("@LocId", locId);
            object? result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0 : Convert.ToDecimal(result);
        }

        private static async Task<int> GetIncompleteDispatchIssueIdAsync(SqlConnection con, int poId, int locId)
        {
            const string sql = @"
SELECT TOP 1 Issue_id FROM SupplierDispatch
WHERE po_id = @PoId AND location_id = @LocId AND status = 'I'
ORDER BY Issue_id DESC";

            using SqlCommand cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@PoId", poId);
            cmd.Parameters.AddWithValue("@LocId", locId);
            object? result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        private static async Task<int> GetNextDispatchIssueIdAsync(SqlConnection con)
        {
            const string sql = "SELECT ISNULL(MAX(Issue_id), 0) + 1 FROM SupplierDispatch";
            using SqlCommand cmd = new SqlCommand(sql, con);
            object? result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        private static async Task<string?> GetDispatchInvoicePathAsync(
            SqlConnection con, int issueId, int poId, int locId, int supplierId)
        {
            const string sql = @"
SELECT invoicedocpath FROM SupplierDispatch
WHERE Issue_id = @IssueId AND po_id = @PoId AND location_id = @LocId AND supplierid = @SupplierId";

            using SqlCommand cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@IssueId", issueId);
            cmd.Parameters.AddWithValue("@PoId", poId);
            cmd.Parameters.AddWithValue("@LocId", locId);
            cmd.Parameters.AddWithValue("@SupplierId", supplierId);
            object? result = await cmd.ExecuteScalarAsync();
            return result?.ToString();
        }

        private static async Task<string> GetPoDateDisplayAsync(SqlConnection con, int poId)
        {
            const string sql = @"
SELECT CASE WHEN soissueDT IS NOT NULL THEN CONVERT(VARCHAR, soissueDT, 103)
            ELSE CONVERT(VARCHAR, po_date, 103) END
FROM purchase_order WHERE po_id = @PoId";

            using SqlCommand cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@PoId", poId);
            object? result = await cmd.ExecuteScalarAsync();
            return result?.ToString() ?? string.Empty;
        }

        private static async Task LoadDispatchInvoiceSectionAsync(
            SqlConnection con, SupplierDispatchEntryPageDto page, int issueId)
        {
            const string sql = @"
SELECT Issue_id, remarks, challan_no, invoice_no,
       CONVERT(VARCHAR, challan_date, 103) AS challandate,
       CONVERT(VARCHAR, dispatch_date, 103) AS dispatch_date,
       CONVERT(VARCHAR, invoice_date, 103) AS invoice_date,
       CONVERT(VARCHAR, Tentative_Sdate, 103) AS supply_date,
       BulkVsSerial, invoicedocpath, invoiceGST, EwayBillNo,
       CONVERT(VARCHAR, EwayBilldt, 103) AS ewaybilldt, HSNcode, TCSValue, dispatch_no,
       (SELECT TOP 1 cgmsc_log_printed FROM Issue_item_details WHERE issue_id = @IssueId) AS cgmsc_logo,
       (SELECT TOP 1 opening_manual_provided FROM Issue_item_details WHERE issue_id = @IssueId) AS operating_manual,
       (SELECT TOP 1 calibration_certificate_prov FROM Issue_item_details WHERE issue_id = @IssueId) AS calibration_certificate,
       (SELECT TOP 1 org_warranty_card_rec FROM Issue_item_details WHERE issue_id = @IssueId) AS warranty_card,
       (SELECT TOP 1 other_statutory FROM Issue_item_details WHERE issue_id = @IssueId) AS other_statutory,
       (SELECT TOP 1 warranty_validity FROM Issue_item_details WHERE issue_id = @IssueId) AS warranty_validity,
       (SELECT TOP 1 All_otherPODoc FROM Issue_item_details WHERE issue_id = @IssueId) AS po_documents,
       (SELECT TOP 1 ServiceManual FROM Issue_item_details WHERE issue_id = @IssueId) AS service_manual
FROM SupplierDispatch
WHERE Issue_id = @IssueId";

            using SqlCommand cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@IssueId", issueId);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return;

            page.HasInvoice = true;
            string invoicePath = ReadStringColumn(reader, "invoicedocpath");
            page.HasInvoiceFile = !string.IsNullOrWhiteSpace(invoicePath);
            page.ChallanNo = ReadStringColumn(reader, "challan_no");
            page.ChallanDate = ReadStringColumn(reader, "challandate");
            page.InvoiceNo = ReadStringColumn(reader, "invoice_no");
            page.InvoiceDate = ReadStringColumn(reader, "invoice_date");
            page.EwayBillNo = ReadStringColumn(reader, "EwayBillNo");
            page.EwayBillDate = ReadStringColumn(reader, "ewaybilldt");
            page.HsnCode = ReadStringColumn(reader, "HSNcode");
            page.TcsValue = ReadStringColumn(reader, "TCSValue");
            page.InvoiceGst = ReadStringColumn(reader, "invoiceGST");
            page.Remarks = ReadStringColumn(reader, "remarks");
            page.BulkVsSerial = ReadStringColumn(reader, "BulkVsSerial");
            page.DispatchNo = ReadStringColumn(reader, "dispatch_no");
            page.DispatchDate = ReadStringColumn(reader, "dispatch_date");
            page.TentativeSupplyDate = ReadStringColumn(reader, "supply_date");
            page.CgmscLogoPrinted = CoalesceYesNo(ReadStringColumn(reader, "cgmsc_logo"));
            page.OperatingManual = CoalesceYesNo(ReadStringColumn(reader, "operating_manual"));
            page.CalibrationCertificate = CoalesceYesNo(ReadStringColumn(reader, "calibration_certificate"));
            page.WarrantyCard = CoalesceYesNo(ReadStringColumn(reader, "warranty_card"));
            page.OtherStatutory = CoalesceYesNo(ReadStringColumn(reader, "other_statutory"));
            page.WarrantyValidity = CoalesceYesNo(ReadStringColumn(reader, "warranty_validity"));
            page.PoDocuments = CoalesceYesNo(ReadStringColumn(reader, "po_documents"));
            page.ServiceManual = CoalesceYesNo(ReadStringColumn(reader, "service_manual"));
        }

        private static string CoalesceYesNo(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "N" : value;
        }

        private static async Task<List<SupplierGstOptionDto>> LoadSupplierGstOptionsAsync(SqlConnection con, int supplierId)
        {
            const string sql = @"
SELECT gstid, GSTNO AS GST FROM MASSUPPLIERGST
WHERE FLAG = 'Y' AND SUPPLIERID = @SupplierId
ORDER BY gstid";

            var list = new List<SupplierGstOptionDto>();
            using SqlCommand cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@SupplierId", supplierId);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new SupplierGstOptionDto
                {
                    GstId = ReadIntColumn(reader, "gstid"),
                    GstNo = ReadStringColumn(reader, "GST", "gst"),
                });
            }
            return list;
        }

        private static async Task<List<SupplierDispatchEquipmentLineDto>> LoadDispatchEquipmentLinesAsync(
            SqlConnection con, int issueId)
        {
            const string sql = @"
SELECT issue_detail_id, make_no, warranty_certificate_no, Supplyqty
FROM Issue_item_details
WHERE issue_id = @IssueId AND issue_detail_id IS NOT NULL
ORDER BY issue_detail_id";

            var list = new List<SupplierDispatchEquipmentLineDto>();
            using SqlCommand cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@IssueId", issueId);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new SupplierDispatchEquipmentLineDto
                {
                    IssueDetailId = ReadIntColumn(reader, "issue_detail_id"),
                    SerialNo = ReadStringColumn(reader, "make_no"),
                    WarrantyCardNo = ReadStringColumn(reader, "warranty_certificate_no"),
                    SupplyQty = ReadDecimalColumn(reader, "Supplyqty", "supplyqty"),
                });
            }
            return list;
        }

        private readonly record struct DispatchIssueMeta(
            int PoId,
            int LocationId,
            int ItemId,
            string ItemCode,
            string ModelNo,
            string Make,
            string Status);

        private static async Task<DispatchIssueMeta?> GetDispatchIssueMetaAsync(
            SqlConnection con, int issueId, int supplierId)
        {
            const string sql = @"
SELECT TOP 1 d.po_id, d.location_id, d.status, pi.item_id,
       r.item_code_as_per_tender AS item_code, ci.make, ci.model
FROM SupplierDispatch d
INNER JOIN purchase_order p ON p.po_id = d.po_id
INNER JOIN po_items pi ON pi.po_id = d.po_id AND pi.consignee_id = d.location_id
LEFT JOIN contract_items ci ON ci.contract_item_id = pi.contract_item_id
INNER JOIN masitems r ON r.item_id = pi.item_id
WHERE d.Issue_id = @IssueId AND d.supplierid = @SupplierId AND p.supplier_id = @SupplierId";

            using SqlCommand cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@IssueId", issueId);
            cmd.Parameters.AddWithValue("@SupplierId", supplierId);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            return new DispatchIssueMeta(
                ReadIntColumn(reader, "po_id"),
                ReadIntColumn(reader, "location_id"),
                ReadIntColumn(reader, "item_id"),
                ReadStringColumn(reader, "item_code"),
                ReadStringColumn(reader, "model"),
                ReadStringColumn(reader, "make"),
                ReadStringColumn(reader, "status"));
        }

        private static async Task<decimal> GetDispatchBalanceQtyAsync(SqlConnection con, int poId, int locId)
        {
            const string sql = @"
SELECT pi.quantity, ISNULL(SUM(i.Supplyqty), 0) AS supplied
FROM purchase_order po
INNER JOIN po_items pi ON pi.po_id = po.po_id
LEFT OUTER JOIN SupplierDispatch d ON d.po_id = pi.po_id AND d.location_id = pi.consignee_id
LEFT OUTER JOIN Issue_item_details i ON i.Issue_id = d.Issue_id
WHERE pi.po_id = @PoId AND pi.consignee_id = @LocId
GROUP BY pi.quantity";

            using SqlCommand cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@PoId", poId);
            cmd.Parameters.AddWithValue("@LocId", locId);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return 0;

            decimal orderQty = ReadDecimalColumn(reader, "quantity");
            decimal supplied = ReadDecimalColumn(reader, "supplied");
            return orderQty - supplied;
        }

        private static async Task<string> GetDispatchBulkVsSerialAsync(SqlConnection con, int issueId)
        {
            const string sql = "SELECT BulkVsSerial FROM SupplierDispatch WHERE Issue_id = @IssueId";
            using SqlCommand cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@IssueId", issueId);
            object? result = await cmd.ExecuteScalarAsync();
            return result?.ToString() ?? string.Empty;
        }

        private static async Task<string?> GetDispatchChallanDateAsync(SqlConnection con, int issueId)
        {
            const string sql = "SELECT CONVERT(VARCHAR, challan_date, 103) FROM SupplierDispatch WHERE Issue_id = @IssueId";
            using SqlCommand cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@IssueId", issueId);
            object? result = await cmd.ExecuteScalarAsync();
            return result?.ToString();
        }

        private static async Task<bool> DispatchIssueHasEquipmentLinesAsync(SqlConnection con, int issueId)
        {
            const string sql = "SELECT TOP 1 issue_id FROM Issue_item_details WHERE issue_id = @IssueId";
            using SqlCommand cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@IssueId", issueId);
            object? result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value;
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
