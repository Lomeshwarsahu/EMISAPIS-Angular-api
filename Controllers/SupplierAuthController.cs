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

        public SupplierAuthController(IConfiguration configuration, IWebHostEnvironment env)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection is not configured.");

            var configured = configuration["FileStorage:ComplaintPath"];
            _complaintFileRoot = string.IsNullOrWhiteSpace(configured)
                ? Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "EMSRole", "ComplainUploads"))
                : Path.GetFullPath(configured);
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
    }
}
