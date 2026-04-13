using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using MongoDB.Driver.Core.Configuration;
using System.Text.Json.Serialization;
namespace EMISAPIS.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class BMEController : ControllerBase
    {
        private readonly IConfiguration _config;

        public BMEController(IConfiguration config)
        {
            _config = config;
        }


        [HttpGet("GetSupplierlist")]
        public async Task<IActionResult> GetSupplierlist()
        {
            List<BMEDDTO> list = new List<BMEDDTO>();

            string query = @"SELECT supplier_id,is_contractor,name,email_id,ph_no,address,supplier_code,module_code,mobile_no,
                          GST_no,type,class,is_register,service_engineer_name,service_engineer_number,tin_no
                          FROM massuppliers";

            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                SqlCommand cmd = new SqlCommand(query, conn);

                await conn.OpenAsync();

                SqlDataReader reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    BMEDDTO data = new BMEDDTO
                    {
                        SupplierId = reader["supplier_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["supplier_id"]),
                        IsContractor = reader["is_contractor"] == DBNull.Value ? null : reader["is_contractor"].ToString(),
                        Name = reader["name"] == DBNull.Value ? null : reader["name"].ToString(),
                        EmailId = reader["email_id"] == DBNull.Value ? null : reader["email_id"].ToString(),
                        PhNo = reader["ph_no"] == DBNull.Value ? null : reader["ph_no"].ToString(),
                        Address = reader["address"] == DBNull.Value ? null : reader["address"].ToString(),
                        SupplierCode = reader["supplier_code"] == DBNull.Value ? null : reader["supplier_code"].ToString(),
                        ModuleCode = reader["module_code"] == DBNull.Value ? null : reader["module_code"].ToString(),
                        MobileNo = reader["mobile_no"] == DBNull.Value ? null : reader["mobile_no"].ToString(),
                        GSTNo = reader["GST_no"] == DBNull.Value ? null : reader["GST_no"].ToString(),
                        Type = reader["type"] == DBNull.Value ? null : reader["type"].ToString(),
                        Class = reader["class"] == DBNull.Value ? null : reader["class"].ToString(),
                        IsRegister = reader["is_register"] == DBNull.Value ? null : reader["is_register"].ToString(),
                        ServiceEngineerName = reader["service_engineer_name"] == DBNull.Value ? null : reader["service_engineer_name"].ToString(),
                        ServiceEngineerNumber = reader["service_engineer_number"] == DBNull.Value ? null : reader["service_engineer_number"].ToString(),
                        TinNo = reader["tin_no"] == DBNull.Value ? null : reader["tin_no"].ToString()
                    };

                    list.Add(data);
                }

                return Ok(list);
            }






        }


        [HttpPost("Create")]
        public async Task<IActionResult> CreateSupplier([FromBody] SupplierCreateDTO dto)
        {
            // .NET Core [ApiController] automatically validates the DTO based on Annotations.
            // If validation fails, it automatically returns 400 Bad Request.

            try
            {
                using (SqlConnection con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    await con.OpenAsync();

                    // 1. Check if Mobile No already exists
                    string checkQuery = "SELECT COUNT(1) FROM massuppliers WHERE mobile_no = @MobileNo";
                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, con))
                    {
                        checkCmd.Parameters.AddWithValue("@MobileNo", dto.MobileNo);

                        int exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                        if (exists > 0)
                        {
                            return BadRequest(new { message = "Mobile No already exists, please enter a different mobile no." });
                        }
                    }

                    // 2. Insert new record using Parameterized Query (Protects against SQL Injection)
                    string insertQuery = @"
                        INSERT INTO massuppliers 
                        (name, service_engineer_name, service_engineer_number, mobile_no, email_id, GST_no, ph_no, tin_no, address, entrydate)
                        VALUES 
                        (@Name, @ContactPersonName, @ContactPersonNo, @MobileNo, @Email, @GST, @PhnNo, @TinNo, @Address, GETDATE())";

                    using (SqlCommand insertCmd = new SqlCommand(insertQuery, con))
                    {
                        // Adding parameters. Using DBNull.Value if optional fields are null.
                        insertCmd.Parameters.AddWithValue("@Name", dto.SupplierName.Trim());
                        insertCmd.Parameters.AddWithValue("@ContactPersonName", dto.ContactPersonName.Trim());
                        insertCmd.Parameters.AddWithValue("@ContactPersonNo", dto.ContactPersonNumber.Trim());
                        insertCmd.Parameters.AddWithValue("@MobileNo", dto.MobileNo.Trim());
                        insertCmd.Parameters.AddWithValue("@Email", dto.Email.Trim());
                        insertCmd.Parameters.AddWithValue("@GST", dto.GSTNo.Trim());

                        insertCmd.Parameters.AddWithValue("@PhnNo", string.IsNullOrEmpty(dto.PhnNo) ? DBNull.Value : (object)dto.PhnNo.Trim());
                        insertCmd.Parameters.AddWithValue("@TinNo", string.IsNullOrEmpty(dto.TinNo) ? DBNull.Value : (object)dto.TinNo.Trim());

                        insertCmd.Parameters.AddWithValue("@Address", dto.Address.Trim());

                        int rowsAffected = await insertCmd.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            return Ok(new { message = "Record Successfully Inserted" });
                        }
                        else
                        {
                            return StatusCode(500, new { message = "Failed to insert record." });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // In production, log the exception using a logger (e.g., Serilog/NLog)
                return StatusCode(500, new { message = "An error occurred while saving the data.", error = ex.Message });
            }
        }

        // GET: api/Supplier/GetById/5
        [HttpGet("GetById/{id}")]
        public async Task<IActionResult> GetSupplierById(int id)
        {
            try
            {
                SupplierResponseDTO supplier = null;

                using (SqlConnection con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    await con.OpenAsync();

                    // सुरक्षित Parameterized Query
                    string query = @"
                        SELECT supplier_id, name, service_engineer_name, service_engineer_number, 
                               mobile_no, email_id, GST_no, ph_no, tin_no, address 
                        FROM massuppliers 
                        WHERE supplier_id = @SupplierId";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@SupplierId", id);

                        // DataReader का इस्तेमाल
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            // अगर इस ID का डेटा मौजूद है
                            if (await reader.ReadAsync())
                            {
                                supplier = new SupplierResponseDTO
                                {
                                    // DBNull.Value चेक करना बहुत ज़रूरी है ताकि कोड क्रैश न हो
                                    SupplierId = reader["supplier_id"] != DBNull.Value ? Convert.ToInt32(reader["supplier_id"]) : 0,
                                    SupplierName = reader["name"] != DBNull.Value ? reader["name"].ToString() : null,
                                    ContactPersonName = reader["service_engineer_name"] != DBNull.Value ? reader["service_engineer_name"].ToString() : null,
                                    ContactPersonNumber = reader["service_engineer_number"] != DBNull.Value ? reader["service_engineer_number"].ToString() : null,
                                    MobileNo = reader["mobile_no"] != DBNull.Value ? reader["mobile_no"].ToString() : null,
                                    Email = reader["email_id"] != DBNull.Value ? reader["email_id"].ToString() : null,
                                    GSTNo = reader["GST_no"] != DBNull.Value ? reader["GST_no"].ToString() : null,
                                    PhnNo = reader["ph_no"] != DBNull.Value ? reader["ph_no"].ToString() : null,
                                    TinNo = reader["tin_no"] != DBNull.Value ? reader["tin_no"].ToString() : null,
                                    Address = reader["address"] != DBNull.Value ? reader["address"].ToString() : null
                                };
                            }
                        }
                    }
                }

                // अगर डेटा नहीं मिला (supplier null ही रह गया)
                if (supplier == null)
                {
                    return NotFound(new { message = "Supplier not found." });
                }

                // अगर डेटा मिल गया तो उसे रिटर्न करें
                return Ok(supplier);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching the data.", error = ex.Message });
            }
        }




        // PUT: api/Supplier/Update/5
        [HttpPut("Update/{id}")]
        public async Task<IActionResult> UpdateSupplier(int id, [FromBody] SupplierUpdateDTO dto)
        {
            if (id != dto.SupplierId)
            {
                return BadRequest(new { message = "ID mismatch between URL and payload." });
            }

            try
            {
                using (SqlConnection con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    await con.OpenAsync();

                    // 1. Mobile Number Check (Bug Fix: हम मौजूदा SupplierId को छोड़कर चेक करेंगे)
                    string checkQuery = "SELECT COUNT(1) FROM massuppliers WHERE mobile_no = @MobileNo AND supplier_id != @SupplierId";
                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, con))
                    {
                        checkCmd.Parameters.AddWithValue("@MobileNo", dto.MobileNo);
                        checkCmd.Parameters.AddWithValue("@SupplierId", dto.SupplierId);

                        int exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                        if (exists > 0)
                        {
                            return BadRequest(new { message = "Mobile No already exists for another supplier." });
                        }
                    }

                    // 2. Update Query
                    string updateQuery = @"
                        UPDATE massuppliers 
                        SET name = @Name,
                            email_id = @Email, 
                            ph_no = @PhnNo,
                            address = @Address,
                            mobile_no = @MobileNo,
                            GST_no = @GST,
                            service_engineer_name = @ContactPersonName,
                            service_engineer_number = @ContactPersonNo,
                            tin_no = @TinNo,
                            update_date = GETDATE()
                        WHERE supplier_id = @SupplierId";

                    using (SqlCommand updateCmd = new SqlCommand(updateQuery, con))
                    {
                        updateCmd.Parameters.AddWithValue("@SupplierId", dto.SupplierId);
                        updateCmd.Parameters.AddWithValue("@Name", dto.SupplierName.Trim());
                        updateCmd.Parameters.AddWithValue("@ContactPersonName", dto.ContactPersonName.Trim());
                        updateCmd.Parameters.AddWithValue("@ContactPersonNo", dto.ContactPersonNumber.Trim());
                        updateCmd.Parameters.AddWithValue("@MobileNo", dto.MobileNo.Trim());
                        updateCmd.Parameters.AddWithValue("@Email", dto.Email.Trim());
                        updateCmd.Parameters.AddWithValue("@GST", dto.GSTNo.Trim());

                        // Handle Optional Fields
                        updateCmd.Parameters.AddWithValue("@PhnNo", string.IsNullOrEmpty(dto.PhnNo) ? DBNull.Value : (object)dto.PhnNo.Trim());
                        updateCmd.Parameters.AddWithValue("@TinNo", string.IsNullOrEmpty(dto.TinNo) ? DBNull.Value : (object)dto.TinNo.Trim());
                        updateCmd.Parameters.AddWithValue("@Address", dto.Address.Trim());

                        int rowsAffected = await updateCmd.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            return Ok(new { message = "Record Successfully Updated" });
                        }
                        else
                        {
                            return NotFound(new { message = "Supplier not found or no changes made." });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while updating the data.", error = ex.Message });
            }
        }

    }
}