using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EMISAPIS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MasterController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public MasterController(IConfiguration config, IWebHostEnvironment env)
        {
            _config = config;
            _env = env;
        }

        private string ConnStr() => _config.GetConnectionString("DefaultConnection")!;

        #region DHS Facility Users Locations

        [HttpGet("districts")]
        public async Task<IActionResult> GetDistricts()
        {
            var list = new List<DistrictDto>();
            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();
            using var cmd = new SqlCommand("SELECT DP_DistrictID, DBStart_Name_En FROM Districts ORDER BY DBStart_Name_En", conn);
            using var dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
            {
                list.Add(new DistrictDto
                {
                    DP_DistrictID = Convert.ToInt32(dr["DP_DistrictID"]),
                    DBStart_Name_En = dr["DBStart_Name_En"]?.ToString() ?? string.Empty
                });
            }
            return Ok(list);
        }

        [HttpGet("facility-types")]
        public async Task<IActionResult> GetFacilityTypes([FromQuery] int authority = 0)
        {
            var list = new List<FacilityTypeDto>();
            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();
            var sql = authority > 0
                ? "SELECT facility_type_id, facility_type_name FROM facility_type WHERE authority = @authority ORDER BY facility_type_name"
                : "SELECT facility_type_id, facility_type_name FROM facility_type ORDER BY facility_type_name";
            using var cmd = new SqlCommand(sql, conn);
            if (authority > 0) cmd.Parameters.AddWithValue("@authority", authority);
            using var dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
            {
                list.Add(new FacilityTypeDto
                {
                    FacilityTypeId = Convert.ToInt32(dr["facility_type_id"]),
                    FacilityTypeName = dr["facility_type_name"]?.ToString() ?? string.Empty
                });
            }
            return Ok(list);
        }

        [HttpPost("dhs-facility-grid")]
        public async Task<IActionResult> GetDHSFacilityGrid([FromBody] DHSFacilityGridRequest req)
        {
            var list = new List<DHSFacilityGridDto>();
            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();

            var sql = @"SELECT f.facility_type_id, f.authority, f.location_id, f.location_name,
                        d.DBStart_Name_En as District, u.user_id, u.e_mail_id, u.StoreNo, u.StoreName
                        FROM maslocations f
                        INNER JOIN facility_type ft ON ft.facility_type_id = f.facility_type_id
                        LEFT OUTER JOIN Districts d ON d.DP_DistrictID = f.DP_DistrictID
                        LEFT OUTER JOIN users u ON u.location_id = f.location_id
                        WHERE f.facility_type_id = @ftId AND f.DP_DistrictID = @distId
                        ORDER BY d.DBStart_Name_En";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ftId", req.FacilityTypeId);
            cmd.Parameters.AddWithValue("@distId", req.DistrictId);
            using var dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
            {
                list.Add(new DHSFacilityGridDto
                {
                    FacilityTypeId = Convert.ToInt32(dr["facility_type_id"]),
                    Authority = Convert.ToInt32(dr["authority"]),
                    LocationId = Convert.ToInt32(dr["location_id"]),
                    LocationName = dr["location_name"]?.ToString() ?? string.Empty,
                    District = dr["District"]?.ToString() ?? string.Empty,
                    UserId = dr["user_id"] == DBNull.Value ? 0 : Convert.ToInt32(dr["user_id"]),
                    EmailId = dr["e_mail_id"]?.ToString() ?? string.Empty,
                    StoreNo = dr["StoreNo"]?.ToString() ?? string.Empty,
                    StoreName = dr["StoreName"]?.ToString() ?? string.Empty
                });
            }
            return Ok(list);
        }

        [HttpPost("dhs-add-facility-user")]
        public async Task<IActionResult> AddFacilityUser([FromBody] AddFacilityUserRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.EmailId))
                return BadRequest("Email is required");

            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();

            var getLocationSql = "SELECT location_name FROM maslocations WHERE location_id = @locId";
            using var locCmd = new SqlCommand(getLocationSql, conn);
            locCmd.Parameters.AddWithValue("@locId", req.LocationId);
            var locationName = (await locCmd.ExecuteScalarAsync())?.ToString() ?? string.Empty;

            var sql = @"INSERT INTO users (user_name, e_mail_id, password, passcommon, user_type, designation, location_id, pmis, hrms, ems)
                        VALUES (@userName, @email, @password, @password, 'FU', 'Facility', @locId, 'F', 'F', 'T')";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@userName", locationName);
            cmd.Parameters.AddWithValue("@email", req.EmailId);
            cmd.Parameters.AddWithValue("@password", "salt{yls3DI1n}hash{uTr9CjqETs4TWI85p3sij5xg2Vo=}");
            cmd.Parameters.AddWithValue("@locId", req.LocationId);
            await cmd.ExecuteNonQueryAsync();

            return Ok(new { message = "Saved Successfully" });
        }

        #endregion

        #region Health Facility Details (NodleMaster)

        [HttpGet("nodle-master-grid")]
        public async Task<IActionResult> GetNodleMasterGrid([FromQuery] int userId)
        {
            var list = new List<NodleMasterGridDto>();
            if (userId <= 0) return Ok(list);

            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();

            var sql = @"SELECT d.DBStart_Name_En, m.location_id, m.location_name,
                        ft.facility_type_name, u.e_mail_id as User_Id, u.storeOfficerMob, u.emailID,
                        u.USER_ID as UserId, nd.id as NodleId, nd.name, nd.designation, nd.Mobileno, nd.emailid
                        FROM maslocations m
                        INNER JOIN facility_type ft ON ft.facility_type_id = m.facility_type_id
                        INNER JOIN Districts d ON d.DP_DistrictID = m.DP_DistrictID
                        INNER JOIN users u ON u.location_id = m.location_id
                        LEFT OUTER JOIN (
                            SELECT id, USER_ID, name, designation, Mobileno, emailid FROM NodleMaster WHERE Isactive = 'Y'
                        ) nd ON nd.user_id = u.user_id
                        WHERE m.authority = 5 AND ft.facility_type_id NOT IN (13)
                        AND m.DP_DistrictID IN (
                            SELECT l.DP_DistrictID FROM users u
                            INNER JOIN maslocations l ON l.location_id = u.location_id
                            WHERE u.user_id = @userId
                        )
                        ORDER BY m.DP_DistrictID, ft.facility_type_id";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@userId", userId);
            using var dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
            {
                list.Add(new NodleMasterGridDto
                {
                    DistrictName = dr["DBStart_Name_En"]?.ToString() ?? string.Empty,
                    LocationId = Convert.ToInt32(dr["location_id"]),
                    LocationName = dr["location_name"]?.ToString() ?? string.Empty,
                    FacilityTypeName = dr["facility_type_name"]?.ToString() ?? string.Empty,
                    UserEmail = dr["User_Id"]?.ToString() ?? string.Empty,
                    StoreOfficerMob = dr["storeOfficerMob"]?.ToString() ?? string.Empty,
                    EmailID = dr["emailID"]?.ToString() ?? string.Empty,
                    UserId = Convert.ToInt32(dr["UserId"]),
                    NodleId = dr["NodleId"] == DBNull.Value ? 0 : Convert.ToInt32(dr["NodleId"]),
                    NodleName = dr["name"]?.ToString() ?? string.Empty,
                    NodleDesignation = dr["designation"]?.ToString() ?? string.Empty,
                    NodleMobile = dr["Mobileno"]?.ToString() ?? string.Empty,
                    NodleEmail = dr["emailid"]?.ToString() ?? string.Empty
                });
            }
            return Ok(list);
        }

        [HttpPost("nodle-master-save")]
        public async Task<IActionResult> SaveNodleMaster([FromBody] NodleMasterSaveRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Designation)
                || string.IsNullOrWhiteSpace(req.EmailId) || string.IsNullOrWhiteSpace(req.MobileNo))
                return BadRequest("All fields are required");

            if (!IsValidMobileNo(req.MobileNo))
                return BadRequest("Not a valid Mobile No, Must be Start with 5,6,7,8 or 9");

            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();

            var sql = @"INSERT INTO NodleMaster (user_id, Name, Designation, Emailid, MobileNO, EntryDate, Isactive)
                        VALUES (@userId, @name, @designation, @emailid, @mobileno, GETDATE(), 'Y')";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@userId", req.UserId);
            cmd.Parameters.AddWithValue("@name", req.Name);
            cmd.Parameters.AddWithValue("@designation", req.Designation);
            cmd.Parameters.AddWithValue("@emailid", req.EmailId);
            cmd.Parameters.AddWithValue("@mobileno", req.MobileNo);
            await cmd.ExecuteNonQueryAsync();

            return Ok(new { message = "Save Successfully" });
        }

        [HttpPost("nodle-master-delete")]
        public async Task<IActionResult> DeleteNodleMaster([FromBody] NodleMasterDeleteRequest req)
        {
            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();
            using var cmd = new SqlCommand("UPDATE NodleMaster SET Isactive = 'N' WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("@id", req.Id);
            await cmd.ExecuteNonQueryAsync();
            return Ok(new { message = "Deleted Successfully" });
        }

        private static bool IsValidMobileNo(string mobNo)
        {
            if (string.IsNullOrEmpty(mobNo)) return false;
            var first = mobNo[0];
            return first == '5' || first == '6' || first == '7' || first == '8' || first == '9';
        }

        #endregion

        #region Item Specification

        [HttpGet("eqp-categories")]
        public async Task<IActionResult> GetEqpCategories()
        {
            var list = new List<EqpCategoryDto>();
            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();
            using var cmd = new SqlCommand("SELECT eqpcatid, eqpcatname FROM maseqpcat ORDER BY eqpcatname", conn);
            using var dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
            {
                list.Add(new EqpCategoryDto
                {
                    Eqpcatid = Convert.ToInt32(dr["eqpcatid"]),
                    Eqpcatname = dr["eqpcatname"]?.ToString() ?? string.Empty
                });
            }
            return Ok(list);
        }

        [HttpGet("item-spec-grid")]
        public async Task<IActionResult> GetItemSpecGrid([FromQuery] int? categoryId, [FromQuery] string? search)
        {
            var list = new List<ItemSpecGridDto>();
            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();

            var sql = @"SELECT DISTINCT m.item_id, m.item_code_as_per_tender, m.item_name, c.eqpcatname,
                        CASE WHEN mu.item_id IS NOT NULL THEN 1 ELSE 0 END as HasFile,
                        ISNULL(mu.FILE_NAME, '') as FileName
                        FROM masitems m
                        INNER JOIN maseqpcat c ON m.eqpcatid = c.eqpcatid
                        LEFT OUTER JOIN masitems_upload mu ON mu.item_id = m.item_id
                        WHERE m.CME_EEL = 'Y'";

            if (categoryId.HasValue && categoryId > 0)
                sql += " AND m.eqpcatid = @catId";

            if (!string.IsNullOrWhiteSpace(search))
                sql += " AND (m.item_name LIKE @search OR c.eqpcatname LIKE @search OR m.item_code_as_per_tender LIKE @search)";

            sql += " ORDER BY m.item_code_as_per_tender";

            using var cmd = new SqlCommand(sql, conn);
            if (categoryId.HasValue && categoryId > 0)
                cmd.Parameters.AddWithValue("@catId", categoryId.Value);
            if (!string.IsNullOrWhiteSpace(search))
                cmd.Parameters.AddWithValue("@search", $"%{search}%");

            using var dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
            {
                list.Add(new ItemSpecGridDto
                {
                    ItemId = Convert.ToInt32(dr["item_id"]),
                    ItemCodeAsPerTender = dr["item_code_as_per_tender"]?.ToString() ?? string.Empty,
                    ItemName = dr["item_name"]?.ToString() ?? string.Empty,
                    Eqpcatname = dr["eqpcatname"]?.ToString() ?? string.Empty,
                    HasFile = Convert.ToBoolean(dr["HasFile"]),
                    FileName = dr["FileName"]?.ToString() ?? string.Empty
                });
            }
            return Ok(list);
        }

        [HttpGet("item-spec-download/{itemId}")]
        public async Task<IActionResult> GetItemSpecDownloadInfo(int itemId)
        {
            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();
            using var cmd = new SqlCommand("SELECT upload_folder_name, FILE_NAME FROM masitems_upload WHERE item_id = @itemId", conn);
            cmd.Parameters.AddWithValue("@itemId", itemId);
            using var dr = await cmd.ExecuteReaderAsync();
            if (await dr.ReadAsync())
            {
                return Ok(new ItemSpecDownloadDto
                {
                    UploadFolderName = dr["upload_folder_name"]?.ToString() ?? string.Empty,
                    FileName = dr["FILE_NAME"]?.ToString() ?? string.Empty
                });
            }
            return NotFound(new { message = "File not found" });
        }

        [HttpPost("item-spec-upload/{itemId}")]
        public async Task<IActionResult> UploadItemSpec(int itemId, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Please select a document to upload");

            var ext = Path.GetExtension(file.FileName).ToLower();
            if (ext != ".pdf")
                return BadRequest("Please upload PDF file only");

            if (file.Length > 2_000_000)
                return BadRequest("You cannot upload file more than 2 MB");

            var folderName = "Specification";
            var fileName = $"{itemId}.pdf";
            var uploadsDir = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, folderName);
            Directory.CreateDirectory(uploadsDir);
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();

            using (var delCmd = new SqlCommand("DELETE FROM masitems_upload WHERE item_id = @itemId", conn))
            {
                delCmd.Parameters.AddWithValue("@itemId", itemId);
                await delCmd.ExecuteNonQueryAsync();
            }

            var sql = @"INSERT INTO masitems_upload (upload_folder_name, file_name, item_id)
                        VALUES (@folder, @file, @itemId)";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@folder", folderName);
            cmd.Parameters.AddWithValue("@file", fileName);
            cmd.Parameters.AddWithValue("@itemId", itemId);
            await cmd.ExecuteNonQueryAsync();

            return Ok(new { message = "Uploaded Successfully" });
        }

        #endregion

        #region Medical Facility Users Locations (DME authority=12)

        [HttpGet("medical-college-users")]
        public async Task<IActionResult> GetMedicalCollegeUsers()
        {
            var list = new List<MedicalCollegeUserDto>();
            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();
            using var cmd = new SqlCommand("SELECT user_name, user_id FROM users WHERE authority = 12 AND user_id != 12 ORDER BY user_name", conn);
            using var dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
            {
                list.Add(new MedicalCollegeUserDto
                {
                    UserId = Convert.ToInt32(dr["user_id"]),
                    UserName = dr["user_name"]?.ToString() ?? string.Empty
                });
            }
            return Ok(list);
        }

        [HttpPost("med-facility-grid")]
        public async Task<IActionResult> GetMedFacilityGrid([FromBody] MedFacilityGridRequest req)
        {
            var list = new List<MedFacilityGridDto>();
            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();

            string sql;
            if (req.AuthorityId != 12)
            {
                sql = @"SELECT f.facility_type_id, f.authority, f.location_id, f.location_name,
                        d.DBStart_Name_En as District, u.user_id, u.e_mail_id, u.StoreNo, u.StoreName
                        FROM maslocations f
                        INNER JOIN facility_type ft ON ft.facility_type_id = f.facility_type_id
                        LEFT OUTER JOIN Districts d ON d.DP_DistrictID = f.DP_DistrictID
                        LEFT OUTER JOIN users u ON u.location_id = f.location_id
                        WHERE f.facility_type_id = @ftId AND f.DP_DistrictID = @distId
                        ORDER BY d.DBStart_Name_En";
            }
            else
            {
                sql = @"SELECT f.facility_type_id, f.authority, f.location_id, f.location_name,
                        u.user_name as District, u.user_id, u.e_mail_id, u.StoreNo, u.StoreName
                        FROM maslocations f
                        INNER JOIN facility_type ft ON ft.facility_type_id = f.facility_type_id
                        LEFT OUTER JOIN Districts d ON d.DP_DistrictID = f.DP_DistrictID
                        LEFT OUTER JOIN users u ON u.user_id = f.user_id
                        WHERE f.facility_type_id = @ftId AND u.user_id = @userId
                        ORDER BY u.user_id";
            }

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ftId", req.FacilityTypeId);
            if (req.AuthorityId != 12)
                cmd.Parameters.AddWithValue("@distId", req.DistrictId);
            else
                cmd.Parameters.AddWithValue("@userId", req.UserId);

            using var dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
            {
                list.Add(new MedFacilityGridDto
                {
                    FacilityTypeId = Convert.ToInt32(dr["facility_type_id"]),
                    Authority = Convert.ToInt32(dr["authority"]),
                    LocationId = Convert.ToInt32(dr["location_id"]),
                    LocationName = dr["location_name"]?.ToString() ?? string.Empty,
                    District = dr["District"]?.ToString() ?? string.Empty,
                    UserId = dr["user_id"] == DBNull.Value ? 0 : Convert.ToInt32(dr["user_id"]),
                    EmailId = dr["e_mail_id"]?.ToString() ?? string.Empty,
                    StoreNo = dr["StoreNo"]?.ToString() ?? string.Empty,
                    StoreName = dr["StoreName"]?.ToString() ?? string.Empty
                });
            }
            return Ok(list);
        }

        [HttpPost("med-facility-add")]
        public async Task<IActionResult> AddMedFacility([FromBody] AddMedFacilityRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.LocationName) || req.DistrictId <= 0
                || req.FacilityTypeId <= 0 || string.IsNullOrWhiteSpace(req.MobileNo)
                || string.IsNullOrWhiteSpace(req.Address1) || string.IsNullOrWhiteSpace(req.EmailId))
                return BadRequest("Please Enter Location Name/District/Address1/Mobile No");

            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();

            var sql = @"INSERT INTO maslocations (location_name, user_id, DP_DistrictID, facility_type_id, authority,
                        address_1, address_2, address_3, mob_no, conduct_person, email_id)
                        VALUES (@locName, @userId, @distId, @facTypeId, @authority,
                        @addr1, @addr2, @addr3, @mob, @contact, @email)";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@locName", req.LocationName);
            cmd.Parameters.AddWithValue("@userId", req.UserId > 0 ? req.UserId : 0);
            cmd.Parameters.AddWithValue("@distId", req.DistrictId);
            cmd.Parameters.AddWithValue("@facTypeId", req.FacilityTypeId);
            cmd.Parameters.AddWithValue("@authority", req.Authority);
            cmd.Parameters.AddWithValue("@addr1", req.Address1);
            cmd.Parameters.AddWithValue("@addr2", req.Address2 ?? string.Empty);
            cmd.Parameters.AddWithValue("@addr3", req.Address3 ?? string.Empty);
            cmd.Parameters.AddWithValue("@mob", req.MobileNo);
            cmd.Parameters.AddWithValue("@contact", req.ContactPerson ?? string.Empty);
            cmd.Parameters.AddWithValue("@email", req.EmailId);
            await cmd.ExecuteNonQueryAsync();

            return Ok(new { message = "Saved Successfully" });
        }

        #endregion

        #region Master Supplier Add

        [HttpGet("supplier-detail/{supplierId}")]
        public async Task<IActionResult> GetSupplierDetail(int supplierId)
        {
            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();

            var sql = @"SELECT supplier_id, name, service_engineer_name, service_engineer_number,
                        mobile_no, email_id, GST_no, ph_no, tin_no, address
                        FROM massuppliers WHERE supplier_id = @supplierId";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@supplierId", supplierId);
            using var dr = await cmd.ExecuteReaderAsync();
            if (await dr.ReadAsync())
            {
                return Ok(new SupplierDetailDto
                {
                    SupplierId = Convert.ToInt32(dr["supplier_id"]),
                    Name = dr["name"]?.ToString() ?? string.Empty,
                    ServiceEngineerName = dr["service_engineer_name"]?.ToString() ?? string.Empty,
                    ServiceEngineerNumber = dr["service_engineer_number"]?.ToString() ?? string.Empty,
                    MobileNo = dr["mobile_no"]?.ToString() ?? string.Empty,
                    EmailId = dr["email_id"]?.ToString() ?? string.Empty,
                    GSTNo = dr["GST_no"]?.ToString() ?? string.Empty,
                    PhNo = dr["ph_no"]?.ToString() ?? string.Empty,
                    TinNo = dr["tin_no"]?.ToString() ?? string.Empty,
                    Address = dr["address"]?.ToString() ?? string.Empty
                });
            }
            return NotFound(new { message = "Supplier not found" });
        }

        [HttpPost("supplier-check-mobile")]
        public async Task<IActionResult> CheckMobileExists([FromBody] SupplierAddRequest req)
        {
            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();
            using var cmd = new SqlCommand("SELECT COUNT(*) FROM massuppliers WHERE mobile_no = @mobile", conn);
            cmd.Parameters.AddWithValue("@mobile", req.MobileNo);
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new { exists = count > 0 });
        }

        [HttpPost("supplier-add")]
        public async Task<IActionResult> AddSupplier([FromBody] SupplierAddRequest req)
        {
            var validation = ValidateSupplier(req);
            if (validation != null) return BadRequest(validation);

            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();

            using (var checkCmd = new SqlCommand("SELECT COUNT(*) FROM massuppliers WHERE mobile_no = @mobile", conn))
            {
                checkCmd.Parameters.AddWithValue("@mobile", req.MobileNo);
                var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                if (exists > 0)
                    return BadRequest("Mobile No already exists, please enter different mobile no");
            }

            var sql = @"INSERT INTO massuppliers (name, service_engineer_name, service_engineer_number, mobile_no,
                        email_id, GST_no, ph_no, tin_no, address, entrydate)
                        VALUES (@name, @seName, @seNo, @mobile, @email, @gst, @ph, @tin, @address, GETDATE())";
            using var cmd = new SqlCommand(sql, conn);
            AddSupplierParameters(cmd, req);
            await cmd.ExecuteNonQueryAsync();

            return Ok(new { message = "Record Successfully Inserted" });
        }

        [HttpPost("supplier-update")]
        public async Task<IActionResult> UpdateSupplier([FromBody] SupplierEditRequest req)
        {
            var validation = ValidateSupplier(req);
            if (validation != null) return BadRequest(validation);

            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();

            using (var checkCmd = new SqlCommand(
                "SELECT COUNT(*) FROM massuppliers WHERE mobile_no = @mobile AND supplier_id != @sid", conn))
            {
                checkCmd.Parameters.AddWithValue("@mobile", req.MobileNo);
                checkCmd.Parameters.AddWithValue("@sid", req.SupplierId);
                var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                if (exists > 0)
                    return BadRequest("Mobile No already exists, please enter different mobile no");
            }

            var sql = @"UPDATE massuppliers SET name = @name, email_id = @email, ph_no = @ph, address = @address,
                        mobile_no = @mobile, GST_no = @gst, service_engineer_name = @seName,
                        service_engineer_number = @seNo, tin_no = @tin, update_date = GETDATE()
                        WHERE supplier_id = @supplierId";
            using var cmd = new SqlCommand(sql, conn);
            AddSupplierParameters(cmd, req);
            cmd.Parameters.AddWithValue("@supplierId", req.SupplierId);
            await cmd.ExecuteNonQueryAsync();

            return Ok(new { message = "Record Successfully Updated" });
        }

        private static string? ValidateSupplier(SupplierAddRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name)) return "Please insert Supplier Name.";
            if (string.IsNullOrWhiteSpace(req.ContactPersonName)) return "Please insert Contact Person Name.";
            if (string.IsNullOrWhiteSpace(req.ContactPersonNumber)) return "Please insert Contact Person Number.";
            if (string.IsNullOrWhiteSpace(req.MobileNo)) return "Please insert Mobile Number.";
            if (string.IsNullOrWhiteSpace(req.EmailId)) return "Please insert Email Id.";
            if (string.IsNullOrWhiteSpace(req.GSTNo)) return "Please insert GST No.";
            if (string.IsNullOrWhiteSpace(req.Address)) return "Please insert Address";
            if (req.Name.Length > 100) return "The limit of Supplier name is 100 character";
            if (req.ContactPersonName.Length > 100) return "The limit of Contact person name is 100 characters";
            if (req.MobileNo.Length != 10) return "The limit of Mobile Number is 10 digits";
            if (req.EmailId.Length > 50) return "The limit of Email Id is 50 characters";
            if (req.GSTNo.Length > 15) return "The limit of GST No is 15 characters";
            if (!string.IsNullOrWhiteSpace(req.TinNo) && req.TinNo.Length > 15) return "The limit of tin No is 15 digits";
            if (req.Address.Length > 500) return "The limit of Address is 500 characters";
            return null;
        }

        private static void AddSupplierParameters(SqlCommand cmd, SupplierAddRequest req)
        {
            cmd.Parameters.AddWithValue("@name", req.Name.Trim());
            cmd.Parameters.AddWithValue("@seName", req.ContactPersonName.Trim());
            cmd.Parameters.AddWithValue("@seNo", req.ContactPersonNumber.Trim());
            cmd.Parameters.AddWithValue("@mobile", req.MobileNo.Trim());
            cmd.Parameters.AddWithValue("@email", req.EmailId.Trim());
            cmd.Parameters.AddWithValue("@gst", req.GSTNo.Trim());
            cmd.Parameters.AddWithValue("@ph", req.PhNo?.Trim() ?? string.Empty);
            cmd.Parameters.AddWithValue("@tin", req.TinNo?.Trim() ?? string.Empty);
            cmd.Parameters.AddWithValue("@address", req.Address.Trim());
        }

        #endregion

        #region Store Home

        [HttpGet("store-home/{userId}")]
        public async Task<IActionResult> GetStoreHome(int userId)
        {
            if (userId <= 0)
                return Ok(new StoreHomeDto());

            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();

            var sql = @"SELECT user_name, address, address2, HODName, HODNo, emailID,
                        user_id, e_mail_id, storeOfficer, storeOfficerMob, storelandline
                        FROM users WHERE user_id = @userId";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@userId", userId);
            using var dr = await cmd.ExecuteReaderAsync();
            if (await dr.ReadAsync())
            {
                return Ok(new StoreHomeDto
                {
                    UserName = dr["user_name"]?.ToString() ?? string.Empty,
                    Address = dr["address"]?.ToString() ?? string.Empty,
                    Address2 = dr["address2"]?.ToString() ?? string.Empty,
                    HODName = dr["HODName"]?.ToString() ?? string.Empty,
                    HODNo = dr["HODNo"]?.ToString() ?? string.Empty,
                    EmailID = dr["emailID"]?.ToString() ?? string.Empty,
                    LoginEmail = dr["e_mail_id"]?.ToString() ?? string.Empty,
                    StoreOfficer = dr["storeOfficer"]?.ToString() ?? string.Empty,
                    StoreOfficerMob = dr["storeOfficerMob"]?.ToString() ?? string.Empty,
                    StoreLandline = dr["storelandline"]?.ToString() ?? string.Empty
                });
            }
            return Ok(new StoreHomeDto());
        }

        [HttpPost("store-home-update/{userId}")]
        public async Task<IActionResult> UpdateStoreHome(int userId, [FromBody] StoreHomeUpdateRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.UserName) || string.IsNullOrWhiteSpace(req.Address)
                || string.IsNullOrWhiteSpace(req.HODNo) || string.IsNullOrWhiteSpace(req.EmailID)
                || string.IsNullOrWhiteSpace(req.StoreOfficerMob) || string.IsNullOrWhiteSpace(req.StoreOfficer)
                || string.IsNullOrWhiteSpace(req.StoreLandline))
                return BadRequest("Please Fill all the fields");

            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();

            var sql = @"UPDATE users SET user_name = @userName, address = @address, address2 = @address2,
                        HODName = @hodName, HODNo = @hodNo, emailID = @emailID, updateDT = GETDATE(),
                        storeOfficerMob = @storeOfficerMob, storeOfficer = @storeOfficer,
                        storelandline = @storeLandline WHERE user_id = @userId";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@userName", req.UserName);
            cmd.Parameters.AddWithValue("@address", req.Address);
            cmd.Parameters.AddWithValue("@address2", req.Address2 ?? string.Empty);
            cmd.Parameters.AddWithValue("@hodName", req.HODName);
            cmd.Parameters.AddWithValue("@hodNo", req.HODNo);
            cmd.Parameters.AddWithValue("@emailID", req.EmailID);
            cmd.Parameters.AddWithValue("@storeOfficerMob", req.StoreOfficerMob);
            cmd.Parameters.AddWithValue("@storeOfficer", req.StoreOfficer);
            cmd.Parameters.AddWithValue("@storeLandline", req.StoreLandline);
            await cmd.ExecuteNonQueryAsync();

            return Ok(new { message = "Contact Updated Successfully" });
        }

        #endregion
    }
}
