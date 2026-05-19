using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver.Core.Configuration;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Text.Json.Serialization;
using static System.Runtime.InteropServices.JavaScript.JSType;
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

        //select categoryId, categoryName from mascategory
        [HttpGet("GetCategorylist")]
        public async Task<IActionResult> GetCategorylist()
        {
            List<CategoryDTO> list = new List<CategoryDTO>();

            string query = @"select categoryId, categoryName from mascategory";

            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                SqlCommand cmd = new SqlCommand(query, conn);

                await conn.OpenAsync();

                SqlDataReader reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    CategoryDTO data = new CategoryDTO
                    {
                        categoryId = reader["categoryId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["categoryId"]),
                        categoryName = reader["categoryName"] == DBNull.Value ? null : reader["categoryName"].ToString(),
                       
                    };

                    list.Add(data);
                }

                return Ok(list);
            }






        }

        //itmelist
        [HttpGet("GetEquipmentIte")]
        public async Task<IActionResult> GetEquipmentIte()
        {
            List<EquipmentItemDTO> list = new List<EquipmentItemDTO>();

            string query = @"select rc.contract_item_id, item_name, m.item_id,item_code_as_per_tender,
 case when rc.basic_rate is null then m.estimated_cost else rc.basic_rate end as estimated_cost ,
case when amc ='t' then 'Yes' else 'No' end amc ,case when preventive_maintanence='t' then 'Yes' else 'No' end PM,preventive_period pm_MONTH 
,case when rc.basic_rate is not null then 'Yes' else 'No' end as RCValid, rc.basic_rate,rc.percentage
,dbo.GetTenderNo(m.item_id) as tender_no,case when m.categoryid = 2 then 'Reagent' else 'Equipment' end as category,m.categoryid  from masitems m
left outer join 
(
select ci.contract_item_id, ci.item_id, ci.basic_rate,ci.percentage from contract_items ci 
inner join  award_of_contract ac on ac.award_of_contract_id=ci.award_of_contract_id
where GETDATE() between ac.contract_date and ac.contract_end_date 
)  rc on rc.item_id=m.item_id
order by dbo.GetTenderNo(m.item_id) desc";

            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                SqlCommand cmd = new SqlCommand(query, conn);

                await conn.OpenAsync();

                SqlDataReader reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    EquipmentItemDTO data = new EquipmentItemDTO
                    {
                        ContractItemId = reader["contract_item_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["contract_item_id"]),
                        ItemName = reader["item_name"] == DBNull.Value ? null : reader["item_name"].ToString(),
                        ItemId = reader["item_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["item_id"]),
                        ItemCodeAsPerTender = reader["item_code_as_per_tender"] == DBNull.Value ? null : reader["item_code_as_per_tender"].ToString(),
                        EstimatedCost = reader["estimated_cost"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(reader["estimated_cost"]),
                        AMC = reader["amc"] == DBNull.Value ? null : reader["amc"].ToString(),
                        PM = reader["PM"] == DBNull.Value ? null : reader["PM"].ToString(),
                        PmMonth = reader["pm_MONTH"] == DBNull.Value ? null : reader["pm_MONTH"].ToString(),
                        RCValid = reader["RCValid"] == DBNull.Value ? null : reader["RCValid"].ToString(),
                        BasicRate = reader["basic_rate"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(reader["basic_rate"]),
                        Percentage = reader["percentage"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(reader["percentage"]),
                        TenderNo = reader["tender_no"] == DBNull.Value ? null : reader["tender_no"].ToString(),
                        Category = reader["category"] == DBNull.Value ? null : reader["category"].ToString(),
                        CategoryId = reader["categoryid"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["categoryid"])
                    };

                    list.Add(data);
                }

                return Ok(list);
            }

        }



        [HttpPost("CreateEquipment")]
        public async Task<IActionResult> CreateEquipment([FromBody] EquipmentCreateDTO dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                using (SqlConnection con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    await con.OpenAsync();

                    // 1. Check if Equipment Code already exists (पुरानी CheckEQUcode की जगह)
                    string checkQuery = "SELECT COUNT(1) FROM masitems WHERE item_code_as_per_tender = @ItemCode";
                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, con))
                    {
                        checkCmd.Parameters.AddWithValue("@ItemCode", dto.ItemCode.Trim());
                        int exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                        if (exists > 0)
                        {
                            // अगर कोड पहले से मौजूद है, तो BadRequest (400) वापस भेजें
                            return BadRequest(new { message = $"Already Exist Code: {dto.ItemCode}" });
                        }
                    }

                    // 2. Insert New Equipment (पुराने btnSave_Click की जगह)
                    string insertQuery = @"
                        INSERT INTO masitems 
                        (item_name, item_code_as_per_tender, parent_item_id, estimated_cost, preventive_period, 
                         amc, warrenty, installation, preventive_maintanence, categoryId) 
                        VALUES 
                        (@ItemName, @ItemCode, @ParentItemId, @EstimatedCost, @PreventivePeriod, 
                         @AMC, @Warranty, @Installation, @PrevMaint, @CategoryId)";

                    using (SqlCommand insertCmd = new SqlCommand(insertQuery, con))
                    {
                        // Parameters (SQL Injection से बचने के लिए)
                        insertCmd.Parameters.AddWithValue("@ItemName", dto.ItemName.Trim());
                        insertCmd.Parameters.AddWithValue("@ItemCode", dto.ItemCode.Trim());

                        // पुराने कोड में parent_item_id हमेशा '1' जा रहा था
                        insertCmd.Parameters.AddWithValue("@ParentItemId", 1);

                        // Price और Period के लिए NULL/Empty हैंडलिंग
                        insertCmd.Parameters.AddWithValue("@EstimatedCost", dto.Price.HasValue ? (object)dto.Price.Value : DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@PreventivePeriod", dto.PreventivePeriod.HasValue ? (object)dto.PreventivePeriod.Value : 0);

                        // Radio Buttons (Yes/No = 't'/'f')
                        insertCmd.Parameters.AddWithValue("@AMC", string.IsNullOrEmpty(dto.AMC) ? "f" : dto.AMC);
                        insertCmd.Parameters.AddWithValue("@Warranty", string.IsNullOrEmpty(dto.Warranty) ? "f" : dto.Warranty);
                        insertCmd.Parameters.AddWithValue("@Installation", string.IsNullOrEmpty(dto.Installation) ? "f" : dto.Installation);
                        insertCmd.Parameters.AddWithValue("@PrevMaint", string.IsNullOrEmpty(dto.PrevMaint) ? "f" : dto.PrevMaint);

                        insertCmd.Parameters.AddWithValue("@CategoryId", dto.CategoryId);

                        // Execute Insert
                        int rowsAffected = await insertCmd.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            return Ok(new { message = $"Saved New Items Successfully with Code {dto.ItemCode}" });
                        }
                        else
                        {
                            return StatusCode(500, new { message = "Failed to save the equipment." });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Error Logging
                return StatusCode(500, new { message = "An error occurred while saving the data.", error = ex.Message });
            }
        }


        [HttpGet("GetUnmappedItems")]
        public async Task<IActionResult> GetUnmappedItems()
        {
            List<UnmappedItemDTO> list = new List<UnmappedItemDTO>();

            string query = @"
        SELECT m.item_code_as_per_tender, m.item_name, m.item_id, m.pid, p.PItemName 
        FROM masitems m 
        LEFT OUTER JOIN masitemP p ON p.PID = m.pid
        WHERE (m.categoryId IS NULL OR m.categoryId = 1)
        AND m.item_name IS NOT NULL 
        AND m.pid IS NULL
        ORDER BY m.item_name";

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                UnmappedItemDTO item = new UnmappedItemDTO
                                {
                                    // DBNull.Value चेक करके डेटा असाइन करना
                                    ItemCodeAsPerTender = reader["item_code_as_per_tender"] == DBNull.Value ? null : reader["item_code_as_per_tender"].ToString(),

                                    ItemName = reader["item_name"] == DBNull.Value ? null : reader["item_name"].ToString(),

                                    ItemId = reader["item_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["item_id"]),

                                    Pid = reader["pid"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["pid"]),

                                    PItemName = reader["PItemName"] == DBNull.Value ? null : reader["PItemName"].ToString()
                                };

                                list.Add(item);
                            }
                        }
                    }
                }

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching unmapped items", error = ex.Message });
            }
        }



        [HttpPost("MapItemsToMainType")]
        public async Task<IActionResult> MapItemsToMainType([FromBody] ItemMappingDTO dto)
        {
            if (dto.SelectedItemIds == null || dto.SelectedItemIds.Count == 0)
            {
                return BadRequest(new { message = "Please select at least one item from the table to map." });
            }

            try
            {
                using (SqlConnection con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    await con.OpenAsync();

                    // 1. Check if Main Item Type already exists
                    string checkQuery = "SELECT COUNT(1) FROM masitemP WHERE PItemName = @MainItemType";
                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, con))
                    {
                        checkCmd.Parameters.AddWithValue("@MainItemType", dto.MainItemType.Trim());
                        int exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                        if (exists > 0)
                        {
                            return BadRequest(new { message = "Main Item Type Already Exists!" });
                        }
                    }

                    // 2. Insert into masitemP AND Get the new PID instantly
                    int newPid = 0;
                    string insertQuery = @"
                INSERT INTO masitemP (PItemName, amcReq, ProgReq, SRorBulkEntry, IsElectrical) 
                VALUES (@PItemName, @amcReq, @ProgReq, @SRorBulkEntry, @IsElectrical);
                SELECT SCOPE_IDENTITY();";

                    using (SqlCommand insertCmd = new SqlCommand(insertQuery, con))
                    {
                        insertCmd.Parameters.AddWithValue("@PItemName", dto.MainItemType.Trim());
                        insertCmd.Parameters.AddWithValue("@amcReq", dto.AmcRequired);
                        insertCmd.Parameters.AddWithValue("@ProgReq", dto.ProgressRequired);
                        insertCmd.Parameters.AddWithValue("@SRorBulkEntry", dto.EntryType);
                        insertCmd.Parameters.AddWithValue("@IsElectrical", dto.IsElectrical);

                        newPid = Convert.ToInt32(await insertCmd.ExecuteScalarAsync());
                    }

                    // 3. Bulk Update selected items in masitems table
                    // String.Join से हम List<int> को "1,2,3,4" में बदल देंगे
                    string itemIdsString = string.Join(",", dto.SelectedItemIds);
                    string updateQuery = $"UPDATE masitems SET pid = @NewPid WHERE item_id IN ({itemIdsString})";

                    using (SqlCommand updateCmd = new SqlCommand(updateQuery, con))
                    {
                        updateCmd.Parameters.AddWithValue("@NewPid", newPid);
                        await updateCmd.ExecuteNonQueryAsync();
                    }

                    return Ok(new { message = "Items Successfully Mapped With Main Item Type!" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
            }
        }



        [HttpGet("GetmappedItems")]
        public async Task<IActionResult> GetmappedItems()
        {
            List<mappedItemDTO> list = new List<mappedItemDTO>();

            string query = @"select PID, PItemName from masitemP";

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                mappedItemDTO item = new mappedItemDTO
                                {

                                    PID = reader["PID"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["PID"]),

                                    PItemName = reader["PItemName"] == DBNull.Value ? null : reader["PItemName"].ToString()
                                };

                                list.Add(item);
                            }
                        }
                    }
                }

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching unmapped items", error = ex.Message });
            }
        }


        [HttpPost("MapExistingItems")]
        public async Task<IActionResult> MapExistingItems([FromBody] MapExistingItemDTO dto)
        {
            if (dto.MainItemTypeId <= 0)
            {
                return BadRequest(new { message = "Please Select Main Item Type" });
            }

            if (dto.SelectedItemIds == null || dto.SelectedItemIds.Count == 0)
            {
                return BadRequest(new { message = "You have not checked any of the checkboxes" });
            }

            try
            {
                using (SqlConnection con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    await con.OpenAsync();

                    string itemIdsString = string.Join(",", dto.SelectedItemIds);

                    string updateQuery = $"UPDATE masitems SET pid = @Pid WHERE item_id IN ({itemIdsString})";

                    using (SqlCommand updateCmd = new SqlCommand(updateQuery, con))
                    {
                        updateCmd.Parameters.AddWithValue("@Pid", dto.MainItemTypeId);

                        int rowsAffected = await updateCmd.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            return Ok(new { message = "Items Successfully Mapped With Main Item Type!" });
                        }
                        else
                        {
                            return BadRequest(new { message = "No items were updated. Please check the Item IDs." });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while mapping.", error = ex.Message });
            }
        }



        [HttpGet("GetmappedItemsReport")]
        public async Task<IActionResult> GetmappedItemsReport()
        {
            List<mappedItemsReportDTO> list = new List<mappedItemsReportDTO>();

            string query = @"select p.PItemName,case when m.item_code_as_per_tender is not null  then m.item_code_as_per_tender else m.item_code end as itemcode ,m.item_name
,p.IsElectrical,p.ProgReq,case when p.SRorBulkEntry ='S' then 'Serial No Wise' else 'Bulk' end as SRorBulkEntry ,p.amcReq,
m.item_id from masitems m 
inner join masitemP p on p.PID=m.pid
 where m.pid is not null order by p.PItemName";

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                mappedItemsReportDTO item = new mappedItemsReportDTO
                                {
                                    PItemName = reader["PItemName"] == DBNull.Value ? null : reader["PItemName"].ToString(),

                                    ItemCode = reader["itemcode"] == DBNull.Value ? null : reader["itemcode"].ToString(),

                                    ItemName = reader["item_name"] == DBNull.Value ? null : reader["item_name"].ToString(),

                                    IsElectrical = reader["IsElectrical"] == DBNull.Value ? null : reader["IsElectrical"].ToString(),

                                    ProgReq = reader["ProgReq"] == DBNull.Value ? null : reader["ProgReq"].ToString(),

                                    SRorBulkEntry = reader["SRorBulkEntry"] == DBNull.Value ? null : reader["SRorBulkEntry"].ToString(),

                                    AmcReq = reader["amcReq"] == DBNull.Value ? null : reader["amcReq"].ToString(),

                                    ItemId = reader["item_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["item_id"])
                                };

                                list.Add(item);
                            }
                        }
                    }
                }

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching unmapped items", error = ex.Message });
            }
        }
        //jkui
//        public DataTable Get_TendersStatus(string stid, string undOBClaim)
//        {
//            string whUnderObjectClaim = "";
//            string strcsid = "";
//            if (undOBClaim == "Under Prep")
//            {
//                strcsid = " and csid in (2)";
//                whUnderObjectClaim = " and sc.IsCOVFinEli is not null and sc.ISCovTechEli is not null ";
//                // whUnderObjectClaim = " and csid=7"; 
//            }
//            if (undOBClaim == "Under Obj")
//            {
//                strcsid = " and csid in (2,7)";
//                whUnderObjectClaim = " and getdate()> ObjCEndDT ";
//                // whUnderObjectClaim = " and csid=7"; 
//            }
//            else if (undOBClaim == "COVERB")
//            {
//                strcsid = " and csid in (3,4)";
//                whUnderObjectClaim = " ";
//                // whUnderObjectClaim = " and csid=7"; 
//            }
//            else if (undOBClaim == "COVERC")
//            {
//                strcsid = " and csid in (3,4,5)";
//                whUnderObjectClaim = " ";
//                // whUnderObjectClaim = " and csid=7"; 
//            }

//            else if (undOBClaim == "GEM")
//            {

//                strcsid = " and csid in (2) and isgemTender = 'Y'";

//                whUnderObjectClaim = " ";
//                // whUnderObjectClaim = " and csid=7"; 
//            }


//            else
//            {
//                strcsid = " and csid =" + stid;
//            }
//            if (undOBClaim == "Under Prep")
//            {
//                string strSQL = @" select tender_id,tender_no+' ,OpenDT-'+convert(varchar,cover_a,103) as name   from tenders t
//inner join 
//(
//select sc.SCHEMEID,count(distinct sc.SUPPLIERID) as nossupplierADA from masschemesstatusdetails sc
//inner join SCHEMESTATUSDETAILSCHILD sch on sch.SCHSTATUSDID=sc.SCHSTATUSDID
//inner join tenders t on t.tender_id=sc.SCHEMEID
//where 1=1 " + whUnderObjectClaim + @"
//group by sc.SCHEMEID
//) scADA on scADA.SCHEMEID=t.tender_id
//where 1=1 and csid in (2) and financial_year_id>=15 
//and cover_a is not null order by cover_a desc ";
//                DataTable dt = DBHelper.GetDataTable(strSQL);
//                return dt;
//            }
//            else
//            {
//                string strSQL = @" select tender_id,tender_no+' ,OpenDT-'+convert(varchar,cover_a,103) as name   from tenders
//where 1=1 " + strcsid + @" and financial_year_id>=15 and cover_a is not null " + whUnderObjectClaim + " order by cover_a desc ";
//                DataTable dt = DBHelper.GetDataTable(strSQL);
//                return dt;
//            }
//        }
        //jhjjk

        [HttpGet("GetTenderList1/{mode}")]
        public async Task<IActionResult> GetTenderList1(int mode)
        {
            var strcsid = "";
            var whUnderObjectClaim = " ";
            var list = new List<tenderlistDTO>();
            string connString = _config.GetConnectionString("DefaultConnection");

            string sql = "";
            if (mode == 1)
            {
                sql = @"SELECT tender_id, tender_no + ' ,OpenDT-' + CONVERT(VARCHAR, cover_a, 103) AS name 
                FROM tenders 
                WHERE csid = 2 AND financial_year_id >= 15 AND cover_a IS NOT NULL 
                ORDER BY cover_a DESC";
            }
            else if (mode == 2)
            {
             
                sql = @"SELECT tender_id, tender_no 
                FROM tenders 
                WHERE FINANCIAL_YEAR_ID = 14 
                ORDER BY tender_no";
            }
            else if (mode == 3)
            {
                //strcsid = " and csid in (2)";
                //whUnderObjectClaim = " and sc.IsCOVFinEli is not null and sc.ISCovTechEli is not null";
                sql = @" select tender_id,tender_no + ' ,OpenDT-' + convert(varchar,cover_a,103) AS name from tenders t
                inner join 
                (
                select sc.SCHEMEID,count(distinct sc.SUPPLIERID) as nossupplierADA from masschemesstatusdetails sc
                inner join SCHEMESTATUSDETAILSCHILD sch on sch.SCHSTATUSDID=sc.SCHSTATUSDID
                inner join tenders t on t.tender_id=sc.SCHEMEID
                where 1=1  and sc.IsCOVFinEli is not null and sc.ISCovTechEli is not null
                group by sc.SCHEMEID
                ) scADA on scADA.SCHEMEID=t.tender_id
                where 1=1 and csid in (2) and financial_year_id>=15 
                and cover_a is not null order by cover_a desc ";
            }
            else if (mode == 4)
            {
                //strcsid = " and csid in (2) and isgemTender = 'Y'";

                //whUnderObjectClaim = " ";
                sql = @"select tender_id,tender_no+' ,OpenDT-'+convert(varchar,cover_a,103) as name   from tenders
            where 1=1 and csid in (2) and isgemTender = 'Y' and financial_year_id>=15 and cover_a is not null  order by cover_a desc ";
            }
            else
            {
                return BadRequest(new { message = "Invalid Mode" });
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        await conn.OpenAsync();
                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            while (await dr.ReadAsync())
                            {
                                list.Add(new tenderlistDTO
                                {
                                    Tenderid = Convert.ToInt32(dr["tender_id"]),
                                    // FIX: Mode 1 aur Mode 3 dono mein column ka naam "name" hai
                                    Tenderno = (mode == 1 || mode == 3 || mode == 4) ? dr["name"].ToString() : dr["tender_no"].ToString()
                                });
                            }
                            //while (await dr.ReadAsync())
                            //{
                            //    list.Add(new tenderlistDTO
                            //    {
                            //        Tenderid = Convert.ToInt32(dr["tender_id"]),
                            //        // Mode 1 mein 'name' alias hai, Mode 2 mein 'tender_no'
                            //        Tenderno = mode == 1 ? dr["name"].ToString() : dr["tender_no"].ToString()
                            //    });
                            //}
                        }
                    }
                }
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching tenders", details = ex.Message });
            }
        }



        [HttpGet("GetTenderlist")]
        public async Task<IActionResult> GetTenderlist()
        {
            List<tenderlistDTO> list = new List<tenderlistDTO>();

            string query = @"select tender_id,tender_no from tenders where FINANCIAL_YEAR_ID=14  order by tender_no";

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                tenderlistDTO item = new tenderlistDTO
                                {
                                    Tenderno = reader["tender_no"] == DBNull.Value ? null : reader["tender_no"].ToString(),

                                    Tenderid = reader["tender_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["tender_id"])
                                };

                                list.Add(item);
                            }
                        }
                    }
                }

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching unmapped items", error = ex.Message });
            }
        }


        [HttpGet("GetTenderlist/{financialYearId}")]
        public async Task<IActionResult> GetTenderlist(int financialYearId)
        {
            if (financialYearId <= 0)
            {
                return BadRequest(new { message = "Please provide a valid Financial Year ID." });
            }

            List<tenderlistDTO> list = new List<tenderlistDTO>();

            string query = @"
        SELECT tender_id, tender_no 
        FROM tenders 
        WHERE FINANCIAL_YEAR_ID = @FinancialYearId 
        ORDER BY tender_no";

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId);

                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                tenderlistDTO item = new tenderlistDTO
                                {
                                    Tenderno = reader["tender_no"] != DBNull.Value ? Convert.ToString(reader["tender_no"]) ?? string.Empty : string.Empty,

                                    Tenderid = reader["tender_id"] != DBNull.Value ? Convert.ToInt32(reader["tender_id"]) : 0
                                };

                                list.Add(item);
                            }
                        }
                    }
                }

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching tender list", error = ex.Message });
            }
        }


        [HttpGet("GetSuppliersByTenderId/{tenderId}")]
        public async Task<IActionResult> GetSuppliersByTenderId(int tenderId)
        {
            if (tenderId <= 0)
            {
                return BadRequest(new { message = "Please provide a valid Tender ID." });
            }

            List<TenderSupplierrDTO> list = new List<TenderSupplierrDTO>();

            string query = @"
        SELECT s.name, s.supplier_id 
        FROM award_of_contract a 
        INNER JOIN massuppliers s ON a.supplier_id = s.supplier_id
        WHERE a.tender_id = @TenderId
        ORDER BY s.name"; 

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@TenderId", tenderId);

                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                TenderSupplierrDTO data = new TenderSupplierrDTO
                                {
                                    sId = reader["supplier_id"] != DBNull.Value ? Convert.ToInt32(reader["supplier_id"]) : 0,
                                    sName = reader["name"] != DBNull.Value ? Convert.ToString(reader["name"]) ?? string.Empty : string.Empty
                                };
                                list.Add(data);
                            }
                        }
                    }
                }

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching suppliers for the given tender", error = ex.Message });
            }
        }




        [HttpGet("GetRCreports")]
        public async Task<IActionResult> GetRCreports(
        [FromQuery] string financialYearId = "",
        [FromQuery] string tenderId = "",
        [FromQuery] string supplierId = "",
        [FromQuery] string status = "")
        {
            List<AwardOfContractDTO> list = new List<AwardOfContractDTO>(); 

          
            int parsedFinancialYearId = int.TryParse(financialYearId?.Trim(), out int fy) ? fy : 0;
            int parsedTenderId = int.TryParse(tenderId?.Trim(), out int t) ? t : 0;
            int parsedSupplierId = int.TryParse(supplierId?.Trim(), out int s) ? s : 0;
            string parsedStatus = status?.Trim() ?? "";

            // 2. Base Query
            string query = @"
        SELECT 
            A.CONTRACT_NUMBER, convert(varchar, A.CONTRACT_DATE, 103) AS CONTRACT_DATE, 
            A.SUPPLIER_ID, B.NAME, A.CONTRACT_TYPE, A.CONTRACT_DESCRIPTION, 
            C.TENDER_NO, convert(varchar, C.TENDER_DATE, 103) AS TENDER_DATE, A.TENDER_ID,
            A.CONTRACT_DURATION, convert(varchar, A.CONTRACT_SIGN_DATE, 103) AS CONTRACT_SIGN_DATE, 
            convert(varchar, A.CONTRACT_END_DATE, 103) AS CONTRACT_END_DATE,
            A.DOCUMENT_TYPE, A.DOCUMENT_NUMBER, convert(varchar, A.DOCUMENT_DATE, 103) AS DOCUMENT_DATE, 
            convert(varchar, A.DOCUMENT_EXPIRY_DATE, 103) AS DOCUMENT_EXPIRY_DATE, 
            A.FINANCIAL_YEAR_ID, E.YEAR, A.DOCUMENT_VALUE, A.award_of_contract_id, A.status
        FROM AWARD_OF_CONTRACT A
        LEFT OUTER JOIN MASSUPPLIERS B ON (A.SUPPLIER_ID = B.SUPPLIER_ID)
        LEFT OUTER JOIN TENDERS C ON (A.TENDER_ID = C.TENDER_ID)
        LEFT OUTER JOIN DOCUMENT_TYPE D ON (A.DOCUMENT_TYPE = D.DOCUMENT_TYPE_ID)
        LEFT OUTER JOIN MAS_FINANCIAL_YEAR E ON (A.FINANCIAL_YEAR_ID = E.FINANCIAL_YEAR_ID)";

            // 3. Dynamic Conditions
            List<string> conditions = new List<string>();

            if (parsedFinancialYearId > 0)
                conditions.Add("A.FINANCIAL_YEAR_ID = @FinancialYearId");

            if (parsedTenderId > 0)
                conditions.Add("A.TENDER_ID = @TenderId");

            if (parsedSupplierId > 0)
                conditions.Add("A.SUPPLIER_ID = @SupplierId");

            if (!string.IsNullOrEmpty(parsedStatus) && parsedStatus != "0")
                conditions.Add("A.STATUS = @Status");

            if (conditions.Count > 0)
            {
                query += " WHERE " + string.Join(" AND ", conditions);
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        // 4. Parameters असाइन करना
                        if (parsedFinancialYearId > 0) cmd.Parameters.AddWithValue("@FinancialYearId", parsedFinancialYearId);
                        if (parsedTenderId > 0) cmd.Parameters.AddWithValue("@TenderId", parsedTenderId);
                        if (parsedSupplierId > 0) cmd.Parameters.AddWithValue("@SupplierId", parsedSupplierId);
                        if (!string.IsNullOrEmpty(parsedStatus) && parsedStatus != "0") cmd.Parameters.AddWithValue("@Status", parsedStatus);

                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                AwardOfContractDTO data = new AwardOfContractDTO
                                {
                                    // Strings
                                    ContractNumber = reader["CONTRACT_NUMBER"] != DBNull.Value ? Convert.ToString(reader["CONTRACT_NUMBER"]) ?? "" : "",
                                    ContractDate = reader["CONTRACT_DATE"] != DBNull.Value ? Convert.ToString(reader["CONTRACT_DATE"]) ?? "" : "",
                                    SupplierName = reader["NAME"] != DBNull.Value ? Convert.ToString(reader["NAME"]) ?? "" : "",
                                    ContractType = reader["CONTRACT_TYPE"] != DBNull.Value ? Convert.ToString(reader["CONTRACT_TYPE"]) ?? "" : "",
                                    ContractDescription = reader["CONTRACT_DESCRIPTION"] != DBNull.Value ? Convert.ToString(reader["CONTRACT_DESCRIPTION"]) ?? "" : "",
                                    TenderNo = reader["TENDER_NO"] != DBNull.Value ? Convert.ToString(reader["TENDER_NO"]) ?? "" : "",
                                    TenderDate = reader["TENDER_DATE"] != DBNull.Value ? Convert.ToString(reader["TENDER_DATE"]) ?? "" : "",
                                    ContractDuration = reader["CONTRACT_DURATION"] != DBNull.Value ? Convert.ToString(reader["CONTRACT_DURATION"]) ?? "" : "",
                                    ContractSignDate = reader["CONTRACT_SIGN_DATE"] != DBNull.Value ? Convert.ToString(reader["CONTRACT_SIGN_DATE"]) ?? "" : "",
                                    ContractEndDate = reader["CONTRACT_END_DATE"] != DBNull.Value ? Convert.ToString(reader["CONTRACT_END_DATE"]) ?? "" : "",
                                    DocumentNumber = reader["DOCUMENT_NUMBER"] != DBNull.Value ? Convert.ToString(reader["DOCUMENT_NUMBER"]) ?? "" : "",
                                    DocumentDate = reader["DOCUMENT_DATE"] != DBNull.Value ? Convert.ToString(reader["DOCUMENT_DATE"]) ?? "" : "",
                                    DocumentExpiryDate = reader["DOCUMENT_EXPIRY_DATE"] != DBNull.Value ? Convert.ToString(reader["DOCUMENT_EXPIRY_DATE"]) ?? "" : "",
                                    Year = reader["YEAR"] != DBNull.Value ? Convert.ToString(reader["YEAR"]) ?? "" : "",
                                    Status = reader["status"] != DBNull.Value ? Convert.ToString(reader["status"]) ?? "" : "",

                                    // Integers & Decimals
                                    SupplierId = reader["SUPPLIER_ID"] != DBNull.Value ? Convert.ToInt32(reader["SUPPLIER_ID"]) : 0,
                                    TenderId = reader["TENDER_ID"] != DBNull.Value ? Convert.ToInt32(reader["TENDER_ID"]) : 0,
                                    DocumentType = reader["DOCUMENT_TYPE"] != DBNull.Value ? Convert.ToInt32(reader["DOCUMENT_TYPE"]) : 0,
                                    FinancialYearId = reader["FINANCIAL_YEAR_ID"] != DBNull.Value ? Convert.ToInt32(reader["FINANCIAL_YEAR_ID"]) : 0,
                                    AwardOfContractId = reader["award_of_contract_id"] != DBNull.Value ? Convert.ToInt32(reader["award_of_contract_id"]) : 0,
                                    DocumentValue = reader["DOCUMENT_VALUE"] != DBNull.Value ? Convert.ToDecimal(reader["DOCUMENT_VALUE"]) : 0m
                                };

                                list.Add(data);
                            }
                        }
                    }
                }

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching contract details", error = ex.Message });
            }
        }



        // API 1: Generate New Contract Header (पुराना btnGenPO_Click)
        [HttpPost("GenerateContract")]
        public async Task<IActionResult> GenerateContract([FromBody] GenerateContractDTO data)
        {
            try
            {
                // Note: यहाँ आपको अपना पुराना genPO() वाला लॉजिक लगाकर नया contractNo बनाना होगा
                // उदाहरण के लिए मान लेते हैं नया नंबर बन गया:
                string newContractNo = "Cont/150/1/2021-2022";

                string query = @"
                INSERT INTO AWARD_OF_CONTRACT 
                (TENDER_ID, CONTRACT_DATE, SUPPLIER_ID, flag, FINANCIAL_YEAR_ID, CONTRACT_NUMBER, CONTRACT_TYPE, status) 
                OUTPUT INSERTED.award_of_contract_id
                VALUES (@TenderId, @ContractDate, @SupplierId, 'F', @FinYearId, @ContractNo, 'Rate Contract', 'I')";

                int newAwardOfContractId = 0;

                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@TenderId", data.TenderId);
                        cmd.Parameters.AddWithValue("@ContractDate", data.ContractDate);
                        cmd.Parameters.AddWithValue("@SupplierId", data.SupplierId);
                        cmd.Parameters.AddWithValue("@FinYearId", data.FinancialYearId);
                        cmd.Parameters.AddWithValue("@ContractNo", newContractNo);

                        await conn.OpenAsync();
                        // OUTPUT INSERTED से हमें तुरंत नई ID मिल जाएगी
                        newAwardOfContractId = (int)await cmd.ExecuteScalarAsync();
                    }
                }

                return Ok(new
                {
                    message = "Contract Generated Successfully",
                    awardOfContractId = newAwardOfContractId,
                    contractNumber = newContractNo
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error generating contract", error = ex.Message });
            }
        }

        //api 2.a for Tax
        [HttpGet("GetTaxlist")]
        public async Task<IActionResult> GetTaxlist()
        {
            List<GetTaxDTO> list = new List<GetTaxDTO>();

            string query = @"select tax_type_id, tax_type_name from tax_type";

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                GetTaxDTO item = new GetTaxDTO
                                {
                                    TaxId = reader["tax_type_id"] != DBNull.Value ? Convert.ToInt32(reader["tax_type_id"]) : 0,

                                    Taxname = reader["tax_type_name"] != DBNull.Value ? Convert.ToString(reader["tax_type_name"]) ?? string.Empty : string.Empty
                                };

                                list.Add(item);
                            }
                        }
                    }
                }

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching unmapped items", error = ex.Message });
            }
        }

        ////api 2.a for Item auto fill this api is not working 
        //[HttpGet("GetItemRateDetails/{tenderId}/{supplierId}/{itemId}")]
        //public async Task<IActionResult> GetItemRateDetails(int tenderId, int supplierId, int itemId)
        //{
        //    // 1. Validation: चेक करें कि कोई भी ID 0 या माइनस में न हो
        //    if (tenderId <= 0 || supplierId <= 0 || itemId <= 0)
        //    {
        //        return BadRequest(new { message = "Please provide valid Tender ID, Supplier ID, and Item ID." });
        //    }

        //    ItemRateDetailDTO? itemDetail = null;

        //    // 2. SQL Query (पैरामीटर्स के साथ)
        //    string query = @"
        //SELECT 
        //    m.item_id,
        //    m.item_name + '-' + m.item_code_as_per_tender AS item_name,
        //    acc.fbasicrate,
        //    acc.gst,
        //    acc.supplier_id,
        //    ti.tender_id  
        //FROM tenders t
        //INNER JOIN tender_items ti ON t.tender_id = ti.tender_id
        //INNER JOIN masitems m ON m.item_id = ti.item_id                        
        //LEFT OUTER JOIN 
        //(
        //    SELECT DISTINCT ti.item_id, lp.supplier_id, lp.fbasicrate, lp.gst, ti.tender_id  
        //    FROM live_tender_price lp 
        //    INNER JOIN tender_items ti ON ti.tender_item_id = lp.tender_item_id                                                
        //    WHERE lp.isaccept = 'Y' 
        //) acc ON acc.item_id = m.item_id AND ti.tender_id = acc.tender_id
        //INNER JOIN massuppliers s ON s.supplier_id = acc.supplier_id
        //WHERE ti.tender_id = @TenderId 
        //  AND acc.supplier_id = @SupplierId 
        //  AND m.item_id = @ItemId 
        //  AND ti.item_id NOT IN (
        //      SELECT DISTINCT item_id 
        //      FROM award_of_contract aoc
        //      INNER JOIN contract_items ci ON ci.award_of_contract_id = aoc.award_of_contract_id
        //  )";

        //    try
        //    {
        //        using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        //        {
        //            using (SqlCommand cmd = new SqlCommand(query, conn))
        //            {
        //                // 3. Parameters असाइन करें (SQL Injection से बचाव)
        //                cmd.Parameters.AddWithValue("@TenderId", tenderId);
        //                cmd.Parameters.AddWithValue("@SupplierId", supplierId);
        //                cmd.Parameters.AddWithValue("@ItemId", itemId);

        //                await conn.OpenAsync();

        //                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
        //                {
        //                    // चूँकि हमें सिर्फ़ एक ही आइटम का रेट मिलेगा, इसलिए 'if' का इस्तेमाल किया है
        //                    if (await reader.ReadAsync())
        //                    {
        //                        itemDetail = new ItemRateDetailDTO
        //                        {
        //                            // 100% Safe Null Handling
        //                            ItemId = reader["item_id"] != DBNull.Value ? Convert.ToInt32(reader["item_id"]) : 0,
        //                            ItemName = reader["item_name"] != DBNull.Value ? Convert.ToString(reader["item_name"]) ?? string.Empty : string.Empty,
        //                            BasicRate = reader["fbasicrate"] != DBNull.Value ? Convert.ToDecimal(reader["fbasicrate"]) : 0m,
        //                            Gst = reader["gst"] != DBNull.Value ? Convert.ToDecimal(reader["gst"]) : 0m,
        //                            SupplierId = reader["supplier_id"] != DBNull.Value ? Convert.ToInt32(reader["supplier_id"]) : 0,
        //                            TenderId = reader["tender_id"] != DBNull.Value ? Convert.ToInt32(reader["tender_id"]) : 0
        //                        };
        //                    }
        //                }
        //            }
        //        }

        //        // अगर आइटम का रेट मिल गया
        //        if (itemDetail != null)
        //        {
        //            return Ok(itemDetail);
        //        }
        //        else
        //        {
        //            // अगर आइटम पहले ही कॉन्ट्रैक्ट में जुड़ चुका है या रेट नहीं है
        //            return NotFound(new { message = "Item rate details not found or item is already added to another contract." });
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { message = "Error fetching item rate details", error = ex.Message });
        //    }
        //}



        [HttpGet("GetTenderItemDetails/{tenderId}/{supplierId}/{itemId?}")]
        public async Task<IActionResult> GetTenderItemDetails(int tenderId, int supplierId, int itemId = 0)
        {
            // 1. Validation
            if (tenderId <= 0 || supplierId <= 0)
            {
                return BadRequest(new { message = "Please provide valid Tender ID and Supplier ID." });
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    await conn.OpenAsync();

                    // ====================================================================
                    // CONDITION 1: जब ItemId पास किया गया हो (Rate और GST लाने के लिए)
                    // ====================================================================
                    if (itemId > 0)
                    {
                        ItemRateDetailDTO? itemDetail = null;
                        string query1 = @"
                    SELECT 
                        m.item_id, m.item_name + '-' + m.item_code_as_per_tender AS item_name,
                        acc.fbasicrate, acc.gst, acc.supplier_id, ti.tender_id  
                    FROM tenders t
                    INNER JOIN tender_items ti ON t.tender_id = ti.tender_id
                    INNER JOIN masitems m ON m.item_id = ti.item_id                        
                    LEFT OUTER JOIN 
                    (
                        SELECT DISTINCT ti.item_id, lp.supplier_id, lp.fbasicrate, lp.gst, ti.tender_id  
                        FROM live_tender_price lp 
                        INNER JOIN tender_items ti ON ti.tender_item_id = lp.tender_item_id                                                
                        WHERE lp.isaccept = 'Y' 
                    ) acc ON acc.item_id = m.item_id AND ti.tender_id = acc.tender_id
                    INNER JOIN massuppliers s ON s.supplier_id = acc.supplier_id
                    WHERE ti.tender_id = @TenderId 
                      AND acc.supplier_id = @SupplierId 
                      AND m.item_id = @ItemId 
                      AND ti.item_id NOT IN (
                          SELECT DISTINCT item_id 
                          FROM award_of_contract aoc
                          INNER JOIN contract_items ci ON ci.award_of_contract_id = aoc.award_of_contract_id
                      )";

                        using (SqlCommand cmd = new SqlCommand(query1, conn))
                        {
                            cmd.Parameters.AddWithValue("@TenderId", tenderId);
                            cmd.Parameters.AddWithValue("@SupplierId", supplierId);
                            cmd.Parameters.AddWithValue("@ItemId", itemId);

                            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    itemDetail = new ItemRateDetailDTO
                                    {
                                        ItemId = reader["item_id"] != DBNull.Value ? Convert.ToInt32(reader["item_id"]) : 0,
                                        ItemName = reader["item_name"] != DBNull.Value ? Convert.ToString(reader["item_name"]) ?? "" : "",
                                        BasicRate = reader["fbasicrate"] != DBNull.Value ? Convert.ToDecimal(reader["fbasicrate"]) : 0m,
                                        Gst = reader["gst"] != DBNull.Value ? Convert.ToDecimal(reader["gst"]) : 0m,
                                        SupplierId = reader["supplier_id"] != DBNull.Value ? Convert.ToInt32(reader["supplier_id"]) : 0,
                                        TenderId = reader["tender_id"] != DBNull.Value ? Convert.ToInt32(reader["tender_id"]) : 0
                                    };
                                }
                            }
                        }

                        if (itemDetail != null) return Ok(itemDetail); // 1 Object वापस करेगा
                        return NotFound(new { message = "Item rate details not found." });
                    }

                    // ====================================================================
                    // CONDITION 2: जब ItemId 0 हो (सिर्फ ड्रॉपडाउन की लिस्ट लाने के लिए)
                    // ====================================================================
                    else
                    {
                        List<TenderItemDTO> itemList = new List<TenderItemDTO>();
                        string query2 = @"
                    SELECT 
                        m.item_id, m.item_name + '-' + m.item_code_as_per_tender AS item_name  
                    FROM tenders t
                    INNER JOIN tender_items ti ON t.tender_id = ti.tender_id
                    INNER JOIN masitems m ON m.item_id = ti.item_id                        
                    LEFT OUTER JOIN 
                    (
                        SELECT DISTINCT ti.item_id, lp.supplier_id, lp.fbasicrate, lp.gst, ti.tender_id  
                        FROM live_tender_price lp 
                        INNER JOIN tender_items ti ON ti.tender_item_id = lp.tender_item_id                                                
                        WHERE lp.isaccept = 'Y' 
                    ) acc ON acc.item_id = m.item_id AND ti.tender_id = acc.tender_id
                    INNER JOIN massuppliers s ON s.supplier_id = acc.supplier_id
                    WHERE ti.tender_id = @TenderId 
                      AND acc.supplier_id = @SupplierId 
                      AND ti.item_id NOT IN (
                          SELECT DISTINCT item_id 
                          FROM award_of_contract aoc
                          INNER JOIN contract_items ci ON ci.award_of_contract_id = aoc.award_of_contract_id
                      )";

                        using (SqlCommand cmd = new SqlCommand(query2, conn))
                        {
                            cmd.Parameters.AddWithValue("@TenderId", tenderId);
                            cmd.Parameters.AddWithValue("@SupplierId", supplierId);

                            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    itemList.Add(new TenderItemDTO
                                    {
                                        ItemId = reader["item_id"] != DBNull.Value ? Convert.ToInt32(reader["item_id"]) : 0,
                                        ItemName = reader["item_name"] != DBNull.Value ? Convert.ToString(reader["item_name"]) ?? "" : "",
                                    });
                                }
                            }
                        }
                        return Ok(itemList); 
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching details", error = ex.Message });
            }
        }




        [HttpPost("AddContractItem")]
        public async Task<IActionResult> AddContractItem([FromBody] AddContractItemDTO item)
        {
            try
            {
                string query = @"
                INSERT INTO contract_items 
                (award_of_contract_id, no_of_days_for_supply, item_id, basic_rate, tax_type_id, percentage, single_unit_price, licence_number, make, model, domestic_imported)
                VALUES 
                (@AocId, @Days, @ItemId, @BasicRate, @TaxId, @Percent, @UnitPrice, @Licence, @Make, @Model, @Category)";

                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@AocId", item.AwardOfContractId);
                        cmd.Parameters.AddWithValue("@Days", item.NoOfDaysForSupply);
                        cmd.Parameters.AddWithValue("@ItemId", item.ItemId);
                        cmd.Parameters.AddWithValue("@BasicRate", item.BasicRate);
                        cmd.Parameters.AddWithValue("@TaxId", item.TaxTypeId);
                        cmd.Parameters.AddWithValue("@Percent", item.Percentage);
                        cmd.Parameters.AddWithValue("@UnitPrice", item.SingleUnitPrice);
                        cmd.Parameters.AddWithValue("@Licence", item.LicenceNumber ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Make", item.Make ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Model", item.Model ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Category", item.DomesticImported);

                        await conn.OpenAsync();
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                return Ok(new { message = "Item Added Successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error adding item", error = ex.Message });
            }
        }




        [HttpGet("GetContractItemsByContractNo")]
        public async Task<IActionResult> GetContractItemsByContractNo([FromQuery] string contractNo)
        {
            // 1. Validation
            if (string.IsNullOrWhiteSpace(contractNo))
            {
                return BadRequest(new { message = "Please provide a valid Contract Number." });
            }

            List<ContractItemDetailsDTO> list = new List<ContractItemDetailsDTO>();

            // 2. Optimized SQL Query
            string query = @"
        SELECT 
            aoc.award_of_contract_id, aoc.contract_number, ci.contract_item_id, mi.item_name, 
            ci.no_of_days_for_supply, ci.basic_rate, tt.tax_type_name, ci.percentage,
            ci.single_unit_price, ci.licence_number, ci.make, ci.model, ci.item_id, ci.tax_type_id,
            CASE WHEN ci.domestic_imported = '1' THEN 'Imported' ELSE 'Domestic' END AS supplycat,
            CONVERT(varchar, aoc.contract_date, 103) AS contract_date, 
            aoc.supplier_id, aoc.financial_year_id, t.tender_id
        FROM contract_items ci 
        INNER JOIN award_of_contract aoc ON ci.award_of_contract_id = aoc.award_of_contract_id
        INNER JOIN masitems mi ON mi.item_id = ci.item_id
        INNER JOIN tax_type tt ON tt.tax_type_id = ci.tax_type_id
        INNER JOIN tenders t ON t.tender_id = aoc.tender_id
        WHERE aoc.contract_number = @ContractNo";

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        // 3. Parameter Pass करना
                        cmd.Parameters.AddWithValue("@ContractNo", contractNo.Trim());

                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                list.Add(new ContractItemDetailsDTO
                                {
                                    // Integers
                                    AwardOfContractId = reader["award_of_contract_id"] != DBNull.Value ? Convert.ToInt32(reader["award_of_contract_id"]) : 0,
                                    ContractItemId = reader["contract_item_id"] != DBNull.Value ? Convert.ToInt32(reader["contract_item_id"]) : 0,
                                    NoOfDaysForSupply = reader["no_of_days_for_supply"] != DBNull.Value ? Convert.ToInt32(reader["no_of_days_for_supply"]) : 0,
                                    ItemId = reader["item_id"] != DBNull.Value ? Convert.ToInt32(reader["item_id"]) : 0,
                                    TaxTypeId = reader["tax_type_id"] != DBNull.Value ? Convert.ToInt32(reader["tax_type_id"]) : 0,
                                    SupplierId = reader["supplier_id"] != DBNull.Value ? Convert.ToInt32(reader["supplier_id"]) : 0,
                                    FinancialYearId = reader["financial_year_id"] != DBNull.Value ? Convert.ToInt32(reader["financial_year_id"]) : 0,
                                    TenderId = reader["tender_id"] != DBNull.Value ? Convert.ToInt32(reader["tender_id"]) : 0,

                                    // Decimals
                                    BasicRate = reader["basic_rate"] != DBNull.Value ? Convert.ToDecimal(reader["basic_rate"]) : 0m,
                                    Percentage = reader["percentage"] != DBNull.Value ? Convert.ToDecimal(reader["percentage"]) : 0m,
                                    SingleUnitPrice = reader["single_unit_price"] != DBNull.Value ? Convert.ToDecimal(reader["single_unit_price"]) : 0m,

                                    // Strings
                                    ContractNumber = reader["contract_number"] != DBNull.Value ? Convert.ToString(reader["contract_number"]) ?? "" : "",
                                    ItemName = reader["item_name"] != DBNull.Value ? Convert.ToString(reader["item_name"]) ?? "" : "",
                                    TaxTypeName = reader["tax_type_name"] != DBNull.Value ? Convert.ToString(reader["tax_type_name"]) ?? "" : "",
                                    LicenceNumber = reader["licence_number"] != DBNull.Value ? Convert.ToString(reader["licence_number"]) ?? "" : "",
                                    Make = reader["make"] != DBNull.Value ? Convert.ToString(reader["make"]) ?? "" : "",
                                    Model = reader["model"] != DBNull.Value ? Convert.ToString(reader["model"]) ?? "" : "",
                                    SupplyCategory = reader["supplycat"] != DBNull.Value ? Convert.ToString(reader["supplycat"]) ?? "" : "",
                                    ContractDate = reader["contract_date"] != DBNull.Value ? Convert.ToString(reader["contract_date"]) ?? "" : ""
                                });
                            }
                        }
                    }
                }

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching contract items", error = ex.Message });
            }
        }


        [HttpPut("UpdateContractItems")]
        public async Task<IActionResult> UpdateContractItem([FromBody] UpdateContractItemsDTO item)
        {
            if (item.ContractItemId <= 0)
            {
                return BadRequest(new { message = "Please provide a valid Contract Item ID." });
            }

            string query = @"
        UPDATE contract_items  
        SET no_of_days_for_supply = @no_of_days_for_supply,
            basic_rate = @basic_rate,
            tax_type_id = @tax_type_id,
            percentage = @percentage,
            single_unit_price = @single_unit_price,
            licence_number = @licence_number,
            make = @make,
            model = @model
        WHERE contract_item_id = @contract_item_id";

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@contract_item_id", item.ContractItemId);
                        cmd.Parameters.AddWithValue("@no_of_days_for_supply", item.NoOfDaysForSupply);
                        cmd.Parameters.AddWithValue("@basic_rate", item.BasicRate);
                        cmd.Parameters.AddWithValue("@tax_type_id", item.TaxTypeId);
                        cmd.Parameters.AddWithValue("@percentage", item.Percentage);
                        cmd.Parameters.AddWithValue("@single_unit_price", item.SingleUnitPrice);

                        cmd.Parameters.AddWithValue("@licence_number", string.IsNullOrWhiteSpace(item.LicenceNumber) ? (object)DBNull.Value : item.LicenceNumber);
                        cmd.Parameters.AddWithValue("@make", string.IsNullOrWhiteSpace(item.Make) ? (object)DBNull.Value : item.Make);
                        cmd.Parameters.AddWithValue("@model", string.IsNullOrWhiteSpace(item.Model) ? (object)DBNull.Value : item.Model);

                        await conn.OpenAsync();
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            return Ok(new { message = "RC Updated successfully" });
                        }
                        else
                        {
                            return NotFound(new { message = "Contract Item not found." });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating contract item", error = ex.Message });
            }
        }



        [HttpPut("FinalizeContract")]
        public async Task<IActionResult> FinalizeContract([FromBody] FinalizeContractDTO data)
        {
            // 1. Validation
            if (data.AwardOfContractId <= 0)
            {
                return BadRequest(new { message = "Invalid Contract ID." });
            }

            if (data.ContractDuration <= 0)
            {
                return BadRequest(new { message = "Please provide a valid contract duration (in months)." });
            }

            DateTime signDate;
            if (!DateTime.TryParse(data.ContractSignDate, out signDate))
            {
                return BadRequest(new { message = "Invalid Sign Date format. Use yyyy-MM-dd." });
            }

            DateTime endDate = signDate.AddMonths(data.ContractDuration);

            string query = @"
        UPDATE AWARD_OF_CONTRACT 
        SET Status = 'C', 
            contract_duration = @Duration, 
            contract_sign_date = @SignDate, 
            contract_end_date = @EndDate, 
            entryDT = GETDATE() 
        WHERE award_of_contract_id = @AocId";

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Duration", data.ContractDuration.ToString());
                        cmd.Parameters.AddWithValue("@SignDate", signDate);
                        cmd.Parameters.AddWithValue("@EndDate", endDate);
                        cmd.Parameters.AddWithValue("@AocId", data.AwardOfContractId);

                        await conn.OpenAsync();
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            return Ok(new { message = "RC Successfully Completed" }); 
                        }
                        else
                        {
                            return NotFound(new { message = "Contract not found or already finalized." });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error finalizing contract", error = ex.Message });
            }
        }


        //[HttpGet("GetFinYearlist")]
        //public async Task<IActionResult> GetFinYearlist()
        //{
        //    List<FinYearListDTO> list = new List<FinYearListDTO>();

        //    string query = @"select financial_year_id,year from mas_financial_year  order  by financial_year_id  desc";

        //    try
        //    {
        //        using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        //        {
        //            using (SqlCommand cmd = new SqlCommand(query, conn))
        //            {
        //                await conn.OpenAsync();

        //                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
        //                {
        //                    while (await reader.ReadAsync())
        //                    {
        //                        FinYearListDTO item = new FinYearListDTO
        //                        {

        //                            finyear_id = reader["financial_year_id"] != DBNull.Value ? Convert.ToInt32(reader["financial_year_id"]) : 0,
        //                            year = reader["year"] != DBNull.Value ? Convert.ToString(reader["year"]) ?? "" : "",

        //                        };

        //                        list.Add(item);
        //                    }
        //                }
        //            }
        //        }

        //        return Ok(list);
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { message = "Error fetching unmapped items", error = ex.Message });
        //    }
        //}

        [HttpGet("GetTenderDashboardReports/{financialYearId}/{check}")]
        public async Task<IActionResult> GetTenderDashboardReports(int financialYearId, int check)
        {
            List<TenderDashboardReportDTO> reportList = new List<TenderDashboardReportDTO>();

            string query = @"
        SELECT 
            A.TENDER_ID, A.TENDER_NO, Convert(varchar(10),A.TENDER_DATE, 103) AS TENDER_DATE, 
            A.TENDER_DESCRIPTION, A.FLAG, A.financial_year_id, A.warranty_year, A.import_days, A.domestic_days,
            Convert(varchar(10),A.cover_a, 103) AS cover_a, Convert(varchar(10),A.cover_b, 103) AS cover_b, 
            Convert(varchar(10),A.cover_Demo, 103) AS cover_Demo, Convert(varchar(10),A.cover_c, 103) AS cover_c,
            s.cStatus, s.csid,
            isnull(t.totali,0) as totali, isnull(fnd.found,0) as found, isnull(n.nosNotFound,0) as nosNotFound,
            isnull(p.PriceEntry,0) as PriceEntry, isnull(ac.accept,0) as accept, isnull(r.reject,0) as reject,
            isnull(nosBidder,0) nosBidder, isnull(nositems,0) as nositems, isnull(A.tValue,0) as tValue,
            case when isnull(isGemTender,'N')='N' then 'e-Proc' else 'GeM' end tendertype
        FROM TENDERS A 
        LEFT OUTER JOIN (
            select s.SCHEMEID, count(distinct s.SUPPLIERID) as nosBidder, count(distinct sc.itemid) as nositems 
            from masschemesstatusdetails s
            left outer join schemestatusdetailschild sc on sc.SCHSTATUSDID=s.SCHSTATUSDID
            group by s.SCHEMEID
        ) bd on bd.SCHEMEID=A.tender_id
        LEFT OUTER JOIN (
            select COUNT(*) nosNotFound, tender_id from tender_items 
            where priceflag='N' and rejectdate is null group by tender_id
        ) n on n.tender_id=A.tender_id
        LEFT OUTER JOIN (
            select COUNT(distinct ti.item_id) found, ti.tender_id from tender_items ti
            inner join tenders t on t.tender_id=ti.tender_id
            inner join live_tender_price l on l.tender_item_id=ti.tender_item_id
            where ti.priceflag is null group by ti.tender_id
        ) fnd on fnd.tender_id=A.tender_id
        LEFT OUTER JOIN (
            select count(distinct ti.item_id) as PriceEntry, t.tender_id from tender_items ti 
            inner join tenders t on t.tender_id=ti.tender_id
            inner join live_tender_price l on l.tender_item_id=ti.tender_item_id
            where l.basicrate is not null group by t.tender_id
        ) p on p.tender_id=A.tender_id
        LEFT OUTER JOIN (
            select count(distinct ti.item_id) as accept, t.tender_id from tender_items ti 
            inner join tenders t on t.tender_id=ti.tender_id
            inner join live_tender_price l on l.tender_item_id=ti.tender_item_id
            where l.basicrate is not null and l.isaccept='Y' group by t.tender_id
        ) ac on ac.tender_id=A.tender_id
        LEFT OUTER JOIN (
            select COUNT(*) reject, tender_id from tender_items 
            where rejectdate is not null group by tender_id
        ) r on r.tender_id=A.tender_id
        LEFT OUTER JOIN (
            select COUNT(*) totali, tender_id from tender_items group by tender_id
        ) t on t.tender_id=A.tender_id
        LEFT OUTER JOIN mascoverstatus s on s.csid=A.csid 
        WHERE A.financial_year_id = @FinancialYearId ";

            if (check == 0) 
            {
                query += " AND isnull(s.csid,0) not in (6)";
            }
            else 
            {
                query += " AND s.csid = @StatusCheck";
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        
                        cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId);

                        
                        if (check != 0)
                        {
                            cmd.Parameters.AddWithValue("@StatusCheck", check);
                        }

                        await conn.OpenAsync();
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var report = new TenderDashboardReportDTO
                                {
                                    TenderId = reader["TENDER_ID"] != DBNull.Value ? Convert.ToInt32(reader["TENDER_ID"]) : 0,
                                    TenderNo = reader["TENDER_NO"]?.ToString() ?? "",
                                    TenderDate = reader["TENDER_DATE"]?.ToString() ?? "",
                                    TenderDescription = reader["TENDER_DESCRIPTION"]?.ToString() ?? "",
                                    Flag = reader["FLAG"]?.ToString() ?? "",
                                    FinancialYearId = reader["financial_year_id"] != DBNull.Value ? Convert.ToInt32(reader["financial_year_id"]) : 0,
                                    WarrantyYear = reader["warranty_year"] != DBNull.Value ? Convert.ToInt32(reader["warranty_year"]) : 0,
                                    ImportDays = reader["import_days"] != DBNull.Value ? Convert.ToInt32(reader["import_days"]) : 0,
                                    DomesticDays = reader["domestic_days"] != DBNull.Value ? Convert.ToInt32(reader["domestic_days"]) : 0,

                                    CoverA = reader["cover_a"]?.ToString() ?? "",
                                    CoverB = reader["cover_b"]?.ToString() ?? "",
                                    CoverDemo = reader["cover_Demo"]?.ToString() ?? "",
                                    CoverC = reader["cover_c"]?.ToString() ?? "",

                                    CStatus = reader["cStatus"]?.ToString() ?? "",
                                    CsId = reader["csid"] != DBNull.Value ? Convert.ToInt32(reader["csid"]) : 0,

                                    TotalItems = reader["totali"] != DBNull.Value ? Convert.ToInt32(reader["totali"]) : 0,
                                    FoundItems = reader["found"] != DBNull.Value ? Convert.ToInt32(reader["found"]) : 0,
                                    NosNotFound = reader["nosNotFound"] != DBNull.Value ? Convert.ToInt32(reader["nosNotFound"]) : 0,
                                    PriceEntry = reader["PriceEntry"] != DBNull.Value ? Convert.ToInt32(reader["PriceEntry"]) : 0,
                                    Accept = reader["accept"] != DBNull.Value ? Convert.ToInt32(reader["accept"]) : 0,
                                    Reject = reader["reject"] != DBNull.Value ? Convert.ToInt32(reader["reject"]) : 0,
                                    NosBidder = reader["nosBidder"] != DBNull.Value ? Convert.ToInt32(reader["nosBidder"]) : 0,
                                    NosItemsBid = reader["nositems"] != DBNull.Value ? Convert.ToInt32(reader["nositems"]) : 0,
                                    TotalValue = reader["tValue"] != DBNull.Value ? Convert.ToDecimal(reader["tValue"]) : 0m,

                                    TenderType = reader["tendertype"]?.ToString() ?? ""
                                };
                                reportList.Add(report);
                            }
                        }
                    }
                }
                return Ok(reportList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching tender reports", error = ex.Message });
            }
        }

      
        [HttpGet("GetTenderDetailsById/{tenderId}")]
        public async Task<IActionResult> GetTenderDetailsById(int tenderId)
        {
            //if (tenderId <= 0)
            //{
            //    return BadRequest(new { message = "Please provide a valid Tender ID." });
            //}

            List<TenderStatusDto> list = new List<TenderStatusDto>();

            string query = @"SELECT A.TENDER_NO, B.YEAR AS FINANCIAL_YEAR, A.domestic_days, A.import_days, A.warranty_year,
                            convert(varchar, A.TENDER_DATE, 103) as TENDER_DATE, A.TENDER_DESCRIPTION, A.FLAG, A.FINANCIAL_YEAR_ID, A.tender_id,
                            Convert(varchar, A.cover_a, 103) AS cover_a, Convert(varchar, A.cover_b, 103) AS cover_b, 
                            Convert(varchar, A.cover_Demo, 103) AS cover_Demo, Convert(varchar, A.cover_c, 103) AS cover_c, 
                            s.cStatus, s.csid, A.FLAG, Convert(varchar, A.cover_Demo2, 103) AS cover_Demo2, 
                            Convert(varchar, A.cover_Demo3, 103) AS cover_Demo3, TenderRemarks,
                            webSiteUploadID, eprocID, Convert(varchar, A.ENDDate, 103) AS ENDDate 
                            FROM TENDERS A
                            LEFT OUTER JOIN MAS_FINANCIAL_YEAR B ON (A.FINANCIAL_YEAR_ID=B.FINANCIAL_YEAR_ID)
                            LEFT OUTER JOIN mascoverstatus s ON s.csid=A.csid
                            WHERE A.TENDER_ID=@TenderID";

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@TenderId", tenderId);

                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                TenderStatusDto data = new TenderStatusDto
                                {
                                    TENDER_NO = reader["TENDER_NO"]?.ToString(),
                                    FINANCIAL_YEAR = reader["FINANCIAL_YEAR"]?.ToString(),
                                    domestic_days = Convert.ToInt32(reader["domestic_days"]),
                                    import_days = Convert.ToInt32(reader["import_days"]),
                                    warranty_year = Convert.ToInt32(reader["warranty_year"]),
                                    TENDER_DATE = reader["TENDER_DATE"]?.ToString(),
                                    TENDER_DESCRIPTION = reader["TENDER_DESCRIPTION"]?.ToString(),
                                    FLAG = reader["FLAG"]?.ToString(),
                                    FINANCIAL_YEAR_ID = Convert.ToInt32(reader["FINANCIAL_YEAR_ID"]),
                                    tender_id = Convert.ToInt32(reader["tender_id"]),
                                    cover_a = reader["cover_a"]?.ToString(),
                                    cover_b = reader["cover_b"]?.ToString(),
                                    cover_Demo = reader["cover_Demo"]?.ToString(),
                                    cover_c = reader["cover_c"]?.ToString(),
                                    cStatus = reader["cStatus"]?.ToString(),
                                    csid = Convert.ToInt32(reader["csid"]),
                                    cover_Demo2 = reader["cover_Demo2"]?.ToString(),
                                    cover_Demo3 = reader["cover_Demo3"]?.ToString(),
                                    TenderRemarks = reader["TenderRemarks"]?.ToString(),
                                    webSiteUploadID = reader["webSiteUploadID"]?.ToString(),
                                    eprocID = reader["eprocID"]?.ToString(),
                                    ENDDate = reader["ENDDate"]?.ToString()
                                };
                                list.Add(data);
                            }
                        }
                    }
                }

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching suppliers for the given tender", error = ex.Message });
            }
        }


        


        [HttpGet("GetCoverStatusList")]
        public async Task<IActionResult> GetCoverStatusList()
        {
            List<CoverStatusDTO> list = new List<CoverStatusDTO>();

            // Nayi query jo aapne di hai
            string query = @"select csid, cstatus from mascoverstatus where CSID not in (3,5,7)";

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    SqlCommand cmd = new SqlCommand(query, conn);
                    await conn.OpenAsync();

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            CoverStatusDTO data = new CoverStatusDTO
                            {
                                // Null check ke saath mapping
                                csid = reader["csid"] == DBNull.Value ? 0 : Convert.ToInt32(reader["csid"]),
                                cstatus = reader["cstatus"] == DBNull.Value ? null : reader["cstatus"].ToString()
                            };

                            list.Add(data);
                        }
                    }
                }
                return Ok(list);
            }
            catch (Exception ex)
            {
                // Error handling ke liye
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPost("UpdateTenderUploadIds")]
        public async Task<IActionResult> UpdateTenderUploadIds([FromBody] UpdateTenderNoDto dto)
        {
            if (string.IsNullOrEmpty(dto.webSiteUploadID) && string.IsNullOrEmpty(dto.eprocID))
            {
                return BadRequest(new { message = "Both IDs cannot be empty" });
            }

            string query = @"UPDATE TENDERS 
                     SET webSiteUploadID = @webSiteUploadID, 
                         eprocID = @eprocID 
                     WHERE tender_id = @tender_id";

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    SqlCommand cmd = new SqlCommand(query, conn);

                    //cmd.Parameters.AddWithValue("@webSiteUploadID", dto.webSiteUploadID ?? (object)DBNull.Value);
                    //cmd.Parameters.AddWithValue("@eprocID", dto.eprocID ?? (object)DBNull.Value);
                    //cmd.Parameters.AddWithValue("@tender_id", dto.tender_id);

                    cmd.Parameters.Add("@webSiteUploadID", SqlDbType.VarChar, 100).Value = dto.webSiteUploadID ?? (object)DBNull.Value;
                    cmd.Parameters.Add("@eprocID", SqlDbType.VarChar, 100).Value = dto.eprocID ?? (object)DBNull.Value;
                    cmd.Parameters.Add("@tender_id", SqlDbType.Int).Value = dto.tender_id;

                    await conn.OpenAsync();
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();

                    if (rowsAffected > 0)
                    {
                        return Ok(new { message = "Updated Successfully" });
                    }
                    else
                    {
                        return NotFound(new { message = "Tender ID not found" });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }


        //[HttpPost("UpdateTenderFullDetails")]
        //public async Task<IActionResult> UpdateTenderFullDetails([FromBody] TenderUpdateDto dto)
        //{
        //    try
        //    {
        //        using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        //        {
        //            await conn.OpenAsync();

        //            // --- CONDITION 1: Check Items First (Agar Status Live [1] nahi hai) ---
        //            if (dto.Csid != 1)
        //            {
        //                string itemCheckQuery = "SELECT COUNT(*) FROM TENDER_ITEMS WHERE TENDER_ID = @Tid";
        //                int itemCount = (int)new SqlCommand(itemCheckQuery, conn).ExecuteScalar();
        //                if (itemCount == 0) return BadRequest(new { message = "Please Add Items First" });
        //            }

        //            // --- CONDITION 2: checkEarlierCovEntry (Status 3, 4, 5 ke liye Bidders check) ---
        //            if (dto.Csid == 3 || dto.Csid == 4 || dto.Csid == 5)
        //            {
        //                // Isme Bidder Participation aur Item Linking dono check honi chahiye
        //                string bidderCheck = "SELECT COUNT(*) FROM BIDDER_PARTICIPATION WHERE TENDER_ID = @Tid";
        //                int bidderCount = (int)new SqlCommand(bidderCheck, conn).ExecuteScalar();
        //                if (bidderCount == 0) return BadRequest(new { message = "Please Add Bidder in Cover A Section & Link all Items" });
        //            }

        //            // --- CONDITION 3: CheckCurrentCov (Status vs Date Logic) ---
        //            if (dto.Csid == 1 && (!string.IsNullOrEmpty(dto.CoverA) || !string.IsNullOrEmpty(dto.CoverB)))
        //                return BadRequest(new { message = "Status Should not be Live if Dates are entered" });

        //            // --- CONDITION 4: Date Comparisons (A >= End, B >= A, etc.) ---
        //            DateTime endDT = DateTime.Parse(dto.EndDate);
        //            if (!string.IsNullOrEmpty(dto.CoverA))
        //            {
        //                DateTime covA = DateTime.Parse(dto.CoverA);
        //                if (covA < endDT) return BadRequest(new { message = "Cover A Date Should be Greater/Equal to Tender End Date" });
        //            }
        //            // ... (Isi tarah B >= A aur Demo >= B ki conditions bhi lagengi)

        //            // --- CONDITION 5: CheckDTGreaterthanToday (Future date check) ---
        //            if (!string.IsNullOrEmpty(dto.CoverA) && DateTime.Parse(dto.CoverA) > DateTime.Now)
        //                return BadRequest(new { message = "Cover A Should Not be Greater than today" });

        //            // --- FINAL UPDATE QUERY ---
        //            string updateQuery = @"UPDATE TENDERS SET 
        //                            TENDER_NO = @TenderNo, 
        //                            warranty_year = @Warranty, 
        //                            domestic_days = @DomDays, 
        //                            import_days = @ImpDays, 
        //                            csid = @Csid, 
        //                            TenderRemarks = @Remarks, 
        //                            tender_description = @Desc,
        //                            ENDDate = @EndDate,
        //                            cover_a = @CoverA,
        //                            cover_b = @CoverB,
        //                            cover_c = @CoverC,
        //                            cover_Demo = @CoverDemo,
        //                            flag = @Flag
        //                            WHERE TENDER_ID = @TenderId";

        //            SqlCommand cmd = new SqlCommand(updateQuery, conn);
        //            cmd.Parameters.AddWithValue("@Tid", dto.TenderId);
        //            cmd.Parameters.AddWithValue("@TenderNo", dto.TenderNo);
        //            cmd.Parameters.AddWithValue("@Warranty", dto.WarrantyYear);
        //            cmd.Parameters.AddWithValue("@DomDays", dto.DomesticDays);
        //            cmd.Parameters.AddWithValue("@ImpDays", dto.ImportDays);
        //            cmd.Parameters.AddWithValue("@Csid", dto.Csid);
        //            cmd.Parameters.AddWithValue("@Remarks", dto.TenderRemarks ?? "");
        //            cmd.Parameters.AddWithValue("@Desc", dto.TenderDescription ?? "");
        //            cmd.Parameters.AddWithValue("@Flag", dto.Csid == 5 ? "T" : "F");

        //            // Dates mapping (Null handling ke sath)
        //            cmd.Parameters.AddWithValue("@EndDate", dto.EndDate);
        //            cmd.Parameters.AddWithValue("@CoverA", (object)dto.CoverA ?? DBNull.Value);
        //            cmd.Parameters.AddWithValue("@CoverB", (object)dto.CoverB ?? DBNull.Value);
        //            cmd.Parameters.AddWithValue("@CoverC", (object)dto.CoverC ?? DBNull.Value);
        //            cmd.Parameters.AddWithValue("@CoverDemo", (object)dto.CoverDemo ?? DBNull.Value);

        //            int rows = await cmd.ExecuteNonQueryAsync();
        //            return Ok(new { message = "Update Successfully" });
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { message = ex.Message });
        //    }
        //}

        //[HttpPost("UpdateTenderFullDetails")]
        //public async Task<IActionResult> UpdateTenderFullDetails([FromBody] TenderUpdateDto dto)
        //{
        //    // 1. Basic Validations (Jaisa aapke code mein tha)
        //    if (dto.Csid == 0) return BadRequest(new { message = "Select Tender Status" });
        //    if (string.IsNullOrEmpty(dto.EndDate)) return BadRequest(new { message = "Enter Tender End Date" });

        //    // 2. Query Building (Parameterized for Security)
        //    string query = @"UPDATE TENDERS SET 
        //                TENDER_NO = @TenderNo, 
        //                warranty_year = @WarrantyYear, 
        //                domestic_days = @DomesticDays, 
        //                import_days = @ImportDays, 
        //                csid = @Csid, 
        //                TenderRemarks = @Remarks, 
        //                tender_description = @Desc, 
        //                flag = @Flag";

        //    // Date fields ko update string mein add karna
        //    if (!string.IsNullOrEmpty(dto.TenderDate)) query += ", tender_date = @TenderDate";
        //    if (!string.IsNullOrEmpty(dto.EndDate)) query += ", ENDDate = @EndDate";
        //    if (!string.IsNullOrEmpty(dto.ExtendDt)) query += ", extenddt = @ExtendDt";
        //    if (!string.IsNullOrEmpty(dto.CoverA)) query += ", cover_a = @CoverA";
        //    if (!string.IsNullOrEmpty(dto.CoverB)) query += ", cover_b = @CoverB";
        //    if (!string.IsNullOrEmpty(dto.CoverC)) query += ", cover_c = @CoverC";
        //    if (!string.IsNullOrEmpty(dto.CoverDemo)) query += ", cover_Demo = @CoverDemo";
        //    if (!string.IsNullOrEmpty(dto.CoverDemo2)) query += ", cover_Demo2 = @CoverDemo2";
        //    if (!string.IsNullOrEmpty(dto.CoverDemo3)) query += ", cover_Demo3 = @CoverDemo3";
        //    if (dto.Csid == 6 && !string.IsNullOrEmpty(dto.CancelledDt)) query += ", CancelledDT = @CancelledDt";

        //    query += " WHERE TENDER_ID = @TenderId";

        //    try
        //    {
        //        using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        //        {
        //            SqlCommand cmd = new SqlCommand(query, conn);

        //            // Parameters
        //            cmd.Parameters.AddWithValue("@TenderId", dto.TenderId);
        //            cmd.Parameters.AddWithValue("@TenderNo", dto.TenderNo ?? "");
        //            cmd.Parameters.AddWithValue("@WarrantyYear", dto.WarrantyYear);
        //            cmd.Parameters.AddWithValue("@DomesticDays", dto.DomesticDays);
        //            cmd.Parameters.AddWithValue("@ImportDays", dto.ImportDays);
        //            cmd.Parameters.AddWithValue("@Csid", dto.Csid);
        //            cmd.Parameters.AddWithValue("@Remarks", dto.TenderRemarks ?? "");
        //            cmd.Parameters.AddWithValue("@Desc", dto.TenderDescription ?? "");
        //            cmd.Parameters.AddWithValue("@Flag", dto.Csid == 5 ? "T" : "F");

        //            // Date Parameters with Null Checks (Converting string to DateTime)
        //            cmd.Parameters.AddWithValue("@TenderDate", string.IsNullOrEmpty(dto.TenderDate) ? DBNull.Value : DateTime.ParseExact(dto.TenderDate, "yyyy-MM-dd", null));
        //            cmd.Parameters.AddWithValue("@EndDate", string.IsNullOrEmpty(dto.EndDate) ? DBNull.Value : DateTime.ParseExact(dto.EndDate, "yyyy-MM-dd", null));
        //            cmd.Parameters.AddWithValue("@ExtendDt", string.IsNullOrEmpty(dto.ExtendDt) ? DBNull.Value : DateTime.ParseExact(dto.ExtendDt, "yyyy-MM-dd", null));
        //            cmd.Parameters.AddWithValue("@CoverA", string.IsNullOrEmpty(dto.CoverA) ? DBNull.Value : DateTime.ParseExact(dto.CoverA, "yyyy-MM-dd", null));
        //            cmd.Parameters.AddWithValue("@CoverB", string.IsNullOrEmpty(dto.CoverB) ? DBNull.Value : DateTime.ParseExact(dto.CoverB, "yyyy-MM-dd", null));
        //            cmd.Parameters.AddWithValue("@CoverC", string.IsNullOrEmpty(dto.CoverC) ? DBNull.Value : DateTime.ParseExact(dto.CoverC, "yyyy-MM-dd", null));
        //            cmd.Parameters.AddWithValue("@CoverDemo", string.IsNullOrEmpty(dto.CoverDemo) ? DBNull.Value : DateTime.ParseExact(dto.CoverDemo, "yyyy-MM-dd", null));
        //            cmd.Parameters.AddWithValue("@CoverDemo2", string.IsNullOrEmpty(dto.CoverDemo2) ? DBNull.Value : DateTime.ParseExact(dto.CoverDemo2, "yyyy-MM-dd", null));
        //            cmd.Parameters.AddWithValue("@CoverDemo3", string.IsNullOrEmpty(dto.CoverDemo3) ? DBNull.Value : DateTime.ParseExact(dto.CoverDemo3, "yyyy-MM-dd", null));
        //            cmd.Parameters.AddWithValue("@CancelledDt", string.IsNullOrEmpty(dto.CancelledDt) ? DBNull.Value : DateTime.ParseExact(dto.CancelledDt, "yyyy-MM-dd", null));

        //            await conn.OpenAsync();
        //            int rows = await cmd.ExecuteNonQueryAsync();

        //            if (rows > 0) return Ok(new { message = "Update Successfully" });
        //            return NotFound(new { message = "Tender not found" });
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { message = "Error: " + ex.Message });
        //    }
        //}

        [HttpPost("UpdateTenderFullDetails")]
        public async Task<IActionResult> UpdateTenderFullDetails([FromBody] TenderFullUpdateDto dto)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    await conn.OpenAsync();

                    // --- CONDITION 1: if (ddlStatus.SelectedValue != "1") ChechkItemsInTender ---
                    if (dto.Csid != 1)
                    {
                        string sql = "SELECT COUNT(*) FROM TENDER_ITEMS WHERE TENDER_ID = @Tid";
                        int itemCount = (int)new SqlCommand(sql, conn) { Parameters = { new SqlParameter("@Tid", dto.TenderId) } }.ExecuteScalar();
                        if (itemCount == 0) return BadRequest(new { message = "Please Add Items First" });
                    }

                    // --- CONDITION 2: checkEarlierCovEntry (Status 3, 4, 5) ---
                    if (dto.Csid == 3 || dto.Csid == 4 || dto.Csid == 5)
                    {
                        // checkSupplierParticipation
                        string sqlSup = "SELECT COUNT(*) FROM BIDDER_PARTICIPATION WHERE TENDER_ID = @Tid";
                        int supCount = (int)new SqlCommand(sqlSup, conn) { Parameters = { new SqlParameter("@Tid", dto.TenderId) } }.ExecuteScalar();

                        // checkSupplierParticipationItems
                        string sqlSupItem = "SELECT COUNT(*) FROM BIDDER_PARTICIPATION_ITEMS WHERE TENDER_ID = @Tid";
                        int supItemCount = (int)new SqlCommand(sqlSupItem, conn) { Parameters = { new SqlParameter("@Tid", dto.TenderId) } }.ExecuteScalar();

                        if (supCount == 0 || supItemCount == 0)
                            return BadRequest(new { message = "Please Add Bidder in Cover A Section & Link all the Participated Items first" });
                    }

                    // --- CONDITION 3: CheckCurrentCov (Logic vs Dates) ---
                    if (dto.Csid == 1 && (!string.IsNullOrEmpty(dto.CoverB) || !string.IsNullOrEmpty(dto.CoverA) || !string.IsNullOrEmpty(dto.CoverC) || !string.IsNullOrEmpty(dto.CoverDemo)
                       || !string.IsNullOrEmpty(dto.CancelledDt))) 
                        return BadRequest(new { message = "Status Should not be Live" });
                    if (dto.Csid == 2 && (!string.IsNullOrEmpty(dto.CoverB) || !string.IsNullOrEmpty(dto.CoverC) || !string.IsNullOrEmpty(dto.CoverDemo)
                      || !string.IsNullOrEmpty(dto.CancelledDt)))
                        return BadRequest(new { message = "Status Should not be Cover A" });
                    if (dto.Csid == 3 && (!string.IsNullOrEmpty(dto.CoverC) || !string.IsNullOrEmpty(dto.CoverDemo) || !string.IsNullOrEmpty(dto.CancelledDt)))
                        return BadRequest(new { message = "Status Should not be Cover B" });
                    if (dto.Csid == 4 && (!string.IsNullOrEmpty(dto.CoverC) || !string.IsNullOrEmpty(dto.CancelledDt)))
                        return BadRequest(new { message = "Status Should not be Demo" });
                    if (dto.Csid == 5 && (!string.IsNullOrEmpty(dto.CancelledDt)))
                        return BadRequest(new { message = "Status Should not be Cover C" });

                    // --- CONDITION 4: CheckCoverStatus (Mandatory Date check) ---
                    if (dto.Csid == 1 && string.IsNullOrEmpty(dto.EndDate)) return BadRequest(new { message = "Please Enter End Date of Tender" });

                    if (dto.Csid == 2 && string.IsNullOrEmpty(dto.CoverA)) return BadRequest(new { message = "Please Enter Cover A Date" });
                    if (dto.Csid == 3 && string.IsNullOrEmpty(dto.CoverB)) return BadRequest(new { message = "Please Enter Cover B Date" });
                    if (dto.Csid == 4 && string.IsNullOrEmpty(dto.CoverDemo)) return BadRequest(new { message = "Please Enter Demo Date" });

                    if (dto.Csid == 5 && string.IsNullOrEmpty(dto.CoverC)) return BadRequest(new { message = "Please Enter Cover C Date" });
                    if (dto.Csid == 6 && string.IsNullOrEmpty(dto.CancelledDt)) return BadRequest(new { message = "Please Enter Cancelled Date" });


                    // --- CONDITION 5: CheckDTGreaterthanToday (Future date check) ---
                    if (dto.Csid == 2)
                    {
                        if (!string.IsNullOrEmpty(dto.CoverA) && DateTime.Parse(dto.CoverA) > DateTime.Now)
                            return BadRequest(new { message = "Cover A Should Not be Greater than today" });

                    }
                    if (dto.Csid == 3)
                    {
                        if (!string.IsNullOrEmpty(dto.CoverB) && DateTime.Parse(dto.CoverB) > DateTime.Now)
                            return BadRequest(new { message = "Cover B Should Not be Greater than today" });

                    }
                    if (dto.Csid == 5)
                    {
                        if (!string.IsNullOrEmpty(dto.CoverC) && DateTime.Parse(dto.CoverC) > DateTime.Now)
                            return BadRequest(new { message = "Cover C Should Not be Greater than today" });

                    }
                    if (dto.Csid == 6)
                    {
                        if (!string.IsNullOrEmpty(dto.CancelledDt) && DateTime.Parse(dto.CancelledDt) > DateTime.Now)
                            return BadRequest(new { message = "Cancelled Date Should Not be Greater than today" });

                    }
                    // 1. Warranty Year Check (Agar int hai toh 0 se check karein, agar string toh IsNullOrEmpty)
                    if (dto.WarrantyYear <= 0)
                        return BadRequest(new { message = "Enter Items Warranty In Years" });

                    // 2. Domestic Days Check
                    if (dto.DomesticDays <= 0)
                        return BadRequest(new { message = "Enter Supply Days for Domestic Equipments" });

                  
                    // Agar ImportDays integer hai, toh aise check karein:
                    if (dto.ImportDays <= 0)
                    {
                        return BadRequest(new { message = "Enter Supply Days for International Equipments" });
                    }
                    // 4. Status (CSID) Check
                    if (dto.Csid == 0)
                        return BadRequest(new { message = "Select Tender Status" });

                    // 5. End Date Check
                    if (string.IsNullOrEmpty(dto.EndDate))
                        return BadRequest(new { message = "Enter Tender End Date" });

                    //if (string.IsNullOrEmpty(dto.WarrantyYear)) return BadRequest(new { message = "Please Enter Warranty In Years" });
                    //if (string.IsNullOrEmpty(dto.DomesticDays)) return BadRequest(new { message = "Please Enter Supply Days for Domestic Equipments" });
                    //if (string.IsNullOrEmpty(dto.ImportDays)) return BadRequest(new { message = "Please Enter Supply Days for International Equipments" });
                    //if (dto.Csid == 0 return { message = "Please Enter End Date of Tender" });
                    //if (dto.EndDate == "" && string.IsNullOrEmpty(dto.EndDate)) return BadRequest(new { message = "Please Enter tender End Date" });
                    // --- FLAG LOGIC ---
                    string flag = (dto.Csid == 5) ? "T" : "F";

                    // --- FINAL UPDATE QUERY ---
                    string updateSql = @"UPDATE TENDERS SET 
                                TENDER_NO = @TenderNo, 
                                warranty_year = @Warranty, 
                                domestic_days = @DomDays, 
                                import_days = @ImpDays, 
                                csid = @Csid, 
                                TenderRemarks = @Remarks, 
                                tender_description = @Desc, 
                                flag = @Flag,
                                tender_date = @TDate,
                                ENDDate = @EDate,
                                extenddt = @ExDate,
                                cover_a = @CA,
                                cover_b = @CB,
                                cover_c = @CC,
                                cover_Demo = @D1,
                                cover_Demo2 = @D2,
                                cover_Demo3 = @D3,
                                CancelledDT = @CanDt
                                WHERE TENDER_ID = @Tid";

                    SqlCommand cmd = new SqlCommand(updateSql, conn);
                    cmd.Parameters.AddWithValue("@Tid", dto.TenderId);
                    cmd.Parameters.AddWithValue("@TenderNo", dto.TenderNo ?? "");
                    cmd.Parameters.AddWithValue("@Warranty", dto.WarrantyYear);
                    cmd.Parameters.AddWithValue("@DomDays", dto.DomesticDays);
                    cmd.Parameters.AddWithValue("@ImpDays", dto.ImportDays);
                    cmd.Parameters.AddWithValue("@Csid", dto.Csid);
                    cmd.Parameters.AddWithValue("@Remarks", dto.TenderRemarks ?? "");
                    cmd.Parameters.AddWithValue("@Desc", dto.TenderDescription ?? "");
                    cmd.Parameters.AddWithValue("@Flag", flag);

                    // Date Parameters
                    cmd.Parameters.AddWithValue("@TDate", string.IsNullOrEmpty(dto.TenderDate) ? DBNull.Value : (object)dto.TenderDate);
                    cmd.Parameters.AddWithValue("@EDate", string.IsNullOrEmpty(dto.EndDate) ? DBNull.Value : (object)dto.EndDate);
                    cmd.Parameters.AddWithValue("@ExDate", string.IsNullOrEmpty(dto.ExtendDt) ? DBNull.Value : (object)dto.ExtendDt);
                    cmd.Parameters.AddWithValue("@CA", string.IsNullOrEmpty(dto.CoverA) ? DBNull.Value : (object)dto.CoverA);
                    cmd.Parameters.AddWithValue("@CB", string.IsNullOrEmpty(dto.CoverB) ? DBNull.Value : (object)dto.CoverB);
                    cmd.Parameters.AddWithValue("@CC", string.IsNullOrEmpty(dto.CoverC) ? DBNull.Value : (object)dto.CoverC);
                    cmd.Parameters.AddWithValue("@D1", string.IsNullOrEmpty(dto.CoverDemo) ? DBNull.Value : (object)dto.CoverDemo);
                    cmd.Parameters.AddWithValue("@D2", string.IsNullOrEmpty(dto.CoverDemo2) ? DBNull.Value : (object)dto.CoverDemo2);
                    cmd.Parameters.AddWithValue("@D3", string.IsNullOrEmpty(dto.CoverDemo3) ? DBNull.Value : (object)dto.CoverDemo3);
                    cmd.Parameters.AddWithValue("@CanDt", (dto.Csid == 6 && !string.IsNullOrEmpty(dto.CancelledDt)) ? (object)dto.CancelledDt : DBNull.Value);

                    await cmd.ExecuteNonQueryAsync();
                    return Ok(new { message = "Update Successfully" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal Server Error", error = ex.Message });
            }
        }



        [HttpGet("GetSelectableItems")]
        public async Task<IActionResult> GetSelectableItems()
        {
            List<TenderItemSelectionDto> list = new List<TenderItemSelectionDto>();

            // Exact Query as provided
            string query = @"
    select item_id, Eqtype, item_code_as_per_tender, item_name + '(' + item_code_as_per_tender + ')RC:' + RCStatus as item_name,
           RCStatus, daysRCValid, Titemid, tender_no, categoryId
    from (
        select m.item_id, 
               case when m.categoryId is null or m.categoryId=1 then 'Equipment' else 'Reagent' end as Eqtype,
               m.item_code_as_per_tender, m.item_name, t.Titemid, t.tender_no, rcitemd, nitemid, m.categoryId,
               case when rcitemd is not null then 'RC Valid' else 'RC Not Valid' end as RCStatus,
               isnull(daysRCValid, 0) as daysRCValid
        from masitems m
        left outer join (
            select distinct t.item_id as Titemid, tm.tender_no from tender_items t
            inner join tenders tm on tm.tender_id = t.tender_id
            where tm.financial_year_id >= 14
        ) t on t.Titemid = m.item_id
        left outer join (
            select distinct m.item_id as rcitemd, DATEDIFF(day, getdate(), max(ac.contract_end_date)) as daysRCValid 
            from contract_items c
            inner join masitems m on m.item_id = c.item_id
            inner join award_of_contract ac on ac.award_of_contract_id = c.award_of_contract_id
            where ac.contract_end_date >= GETDATE()
            group by m.item_id
        ) as rc on rc.rcitemd = m.item_id
        left outer join (
            select distinct t.item_id as nitemid from masitems t where CreatedOn > '2022-12-01'
        ) n on n.nitemid = m.item_id
    ) a 
    where (case when Titemid is not null then 1 
                else case when rcitemd is not null then 1 
                else case when nitemid is not null then 1 else 0 end end end ) = 1";

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    SqlCommand cmd = new SqlCommand(query, conn);
                    await conn.OpenAsync();
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new TenderItemSelectionDto
                            {
                                item_id = Convert.ToInt32(reader["item_id"]),
                                Eqtype = reader["Eqtype"]?.ToString(),
                                item_code_as_per_tender = reader["item_code_as_per_tender"]?.ToString(),
                                item_name = reader["item_name"]?.ToString(),
                                RCStatus = reader["RCStatus"]?.ToString(),
                                daysRCValid = Convert.ToInt32(reader["daysRCValid"]),
                                Titemid = reader["Titemid"]?.ToString(),
                                tender_no = reader["tender_no"]?.ToString(),
                                categoryId = reader["categoryId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["categoryId"])
                            });
                        }
                    }
                }
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error", details = ex.Message });
            }
        }


        [HttpGet("GetItemEligibility/{itemid}")]
        public async Task<IActionResult> GetItemEligibility(int itemid)
        {
            List<TenderItemDetailDto> result = new List<TenderItemDetailDto>();

            // Exact Query with @itemid parameter
            string query = @"
    select item_id, Eqtype, item_code_as_per_tender, item_name, 
           isnull(RCStatus,'RC Not Valid') as RCStatus, 
           isnull(daysRCValid,0) as daysRCValid, 
           Titemid, tender_no, categoryId
    from (
        select m.item_id, 
               case when m.categoryId is null or m.categoryId=1 then 'Equipment' else 'Reagent' end as Eqtype,
               m.item_code_as_per_tender, m.item_name, t.Titemid, t.tender_no, rcitemd, m.categoryId,
               case when rcitemd is not null then 'RC Valid' else 'RC Not Valid' end as RCStatus, 
               daysRCValid,
               case when rcitemd is not null and daysRCValid > 180 then '1' 
                    else case when rcitemd is null and t.Titemid is null then '0'
                    else '0' end 
               end as TEligible
        from masitems m
        left outer join (
            select t.item_id as Titemid, tm.tender_no from tender_items t
            inner join tenders tm on tm.tender_id = t.tender_id
            where tm.csid not in (1,2,3,4,5) and tm.financial_year_id >= 16
        ) t on t.Titemid = m.item_id
        left outer join (
            select distinct m.item_id as rcitemd, DATEDIFF(day, getdate(), max(ac.contract_end_date)) as daysRCValid 
            from contract_items c
            inner join masitems m on m.item_id = c.item_id
            inner join award_of_contract ac on ac.award_of_contract_id = c.award_of_contract_id
            where ac.contract_end_date >= GETDATE()
            group by m.item_id
        ) as rc on rc.rcitemd = m.item_id
    ) a 
    where a.TEligible = '0' and a.item_id = @itemid
    order by a.item_name";

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    SqlCommand cmd = new SqlCommand(query, conn);
                    // Parameterization for Security
                    cmd.Parameters.AddWithValue("@itemid", itemid);

                    await conn.OpenAsync();
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(new TenderItemDetailDto
                            {
                                item_id = Convert.ToInt32(reader["item_id"]),
                                Eqtype = reader["Eqtype"].ToString(),
                                item_code_as_per_tender = reader["item_code_as_per_tender"]?.ToString(),
                                item_name = reader["item_name"].ToString(),
                                RCStatus = reader["RCStatus"].ToString(),
                                daysRCValid = Convert.ToInt32(reader["daysRCValid"]),
                                Titemid = reader["Titemid"]?.ToString(),
                                tender_no = reader["tender_no"]?.ToString(),
                                categoryId = reader["categoryId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["categoryId"])
                            });
                        }
                    }
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Database Error", error = ex.Message });
            }
        }

        [HttpPost("AddItemToTender")]
        public async Task<IActionResult> AddItemToTender([FromBody] AddTenderItemDto dto)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    await conn.OpenAsync();

                    // --- 1. Validation Logic: EMD Amount Check ---
                    if (dto.EmdAmount <= 0)
                    {
                        return BadRequest(new { message = "Please Enter EMD" });
                    }

                    // --- 2. Validation Logic: Tender Quantity Check ---
                    if (dto.TenderQuantity <= 0)
                    {
                        return BadRequest(new { message = "Please Enter Tender QTY" });
                    }

                    // --- 3. Duplicate Check: CheckTenderIdAndItemIdExistance ---
                    //string checkQuery = "SELECT COUNT(*) FROM TENDER_ITEMS WHERE TENDER_ID = @Tid AND ITEM_ID = @Iid";
                    string checkQuery = "SELECT COUNT(*) FROM tender_items WHERE tender_id = @Tid AND item_id = @Iid";
                    //string checkQuery = " select * from tender_items where tender_id = @Tid and item_id = @Iid";
                    SqlCommand checkCmd = new SqlCommand(checkQuery, conn);
                    checkCmd.Parameters.AddWithValue("@Tid", dto.TenderId);
                    checkCmd.Parameters.AddWithValue("@Iid", dto.ItemId);

                    int existCount = (int)await checkCmd.ExecuteScalarAsync();
                    if (existCount > 0)
                    {
                        return BadRequest(new { message = "Item is Already Linked to this Tender" });
                    }

                    // --- 4. Final Insert Logic (Exactly as per your SQL) ---
                    // Values: FLAG='T', status='B', bid_status='1' (As per your code)
                    string insertSql = @"INSERT INTO TENDER_ITEMS 
                                (TENDER_ID, ITEM_ID, TENDER_QUANTITY, EMD_AMOUNT, FLAG, status, bid_status) 
                                VALUES (@Tid, @Iid, @Qty, @Emd, 'T', 'B', '1')";

                    SqlCommand insertCmd = new SqlCommand(insertSql, conn);
                    insertCmd.Parameters.AddWithValue("@Tid", dto.TenderId);
                    insertCmd.Parameters.AddWithValue("@Iid", dto.ItemId);
                    insertCmd.Parameters.AddWithValue("@Qty", dto.TenderQuantity);
                    insertCmd.Parameters.AddWithValue("@Emd", dto.EmdAmount);

                    await insertCmd.ExecuteNonQueryAsync();

                    return Ok(new { message = "Saved Successfully" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error occurred", error = ex.Message });
            }
        }

      
       
        [HttpGet("GetLinkedItemsByTender/{tenderId}/{flag}")]
        public async Task<IActionResult> GetLinkedItemsByTender(int tenderId, string flag)
        {
            List<TenderLinkedItemDto> list = new List<TenderLinkedItemDto>();
            string query = ""; 

            // 1. Flag wise Query Selection (Use == for comparison)
            if (flag == "A") // 'A' for All/Linked Items
            {
                query = @"
            SELECT 0 as SlNo, m.ITEM_ID, ti.tender_item_id, t.tender_id, m.item_name, 
                   m.item_code_as_per_tender, m.item_code, ti.emd_amount, ti.tender_quantity
            FROM tenders t
            INNER JOIN tender_items ti ON ti.tender_id = t.tender_id
            INNER JOIN masitems m ON m.item_id = ti.item_id
            WHERE t.tender_id = @Tid";
            }
            else if (flag == "E") // 'E' for Extended/Detailed view
            {
                // Yahan maine hardcoded '680' ko hata kar @Tid parameter use kiya hai
                query = @"
            select ti.item_id,item_code_as_per_tender as item_code,item_name,item_desc, case when item_name is null then item_desc else item_name end as itemName1, c.categoryName 
,isnull(ti.emd_amount,0) as emd_amount,isnull(ti.tender_quantity,0) as tender_quantity  from masitems m
left outer join masCategory c on c.categoryId = m.categoryid 
inner join tender_items ti on ti.item_id = m.item_id
inner join tenders t on t.tender_id = ti.tender_id
where 1=1 and t.tender_id = @Tid";
            }
            else
            {
                return BadRequest(new { message = "Invalid Flag. Use 'A' or 'E'." });
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@Tid", tenderId);
                    await conn.OpenAsync();

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // Object initializer ka sahi upyog
                            //list.Add(new TenderLinkedItemDto
                            //{
                            //    SlNo = reader["SlNo"] != DBNull.Value ? Convert.ToInt32(reader["SlNo"]) : 0,
                            //    ItemId = reader["ITEM_ID"] != DBNull.Value ? Convert.ToInt32(reader["ITEM_ID"]) : 0,
                            //    TenderItemId = reader["tender_item_id"] != DBNull.Value ? Convert.ToInt32(reader["tender_item_id"]) : 0,
                            //    TenderId = reader["tender_id"] != DBNull.Value ? Convert.ToInt32(reader["tender_id"]) : 0,
                            //    ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                            //    ItemCodeAsPerTender = reader["item_code_as_per_tender"]?.ToString() ?? string.Empty,
                            //    ItemCode = reader["item_code"]?.ToString() ?? string.Empty,
                            //    EmdAmount = reader["emd_amount"] != DBNull.Value ? Convert.ToDecimal(reader["emd_amount"]) : 0m,
                            //    TenderQuantity = reader["tender_quantity"] != DBNull.Value ? Convert.ToDecimal(reader["tender_quantity"]) : 0m
                            //});
                            list.Add(new TenderLinkedItemDto
                            {
                                // Common Fields
                                SlNo = ColumnExists(reader, "SlNo") ? Convert.ToInt32(reader["SlNo"]) : 0,

                                // ItemId logic (ItemId ya ITEM_ID dono handle karega)
                                ItemId = ColumnExists(reader, "item_id") ? Convert.ToInt32(reader["item_id"]) :
                                (ColumnExists(reader, "ITEM_ID") ? Convert.ToInt32(reader["ITEM_ID"]) : 0),

                                TenderItemId = ColumnExists(reader, "tender_item_id") ? Convert.ToInt32(reader["tender_item_id"]) : 0,
                                TenderId = ColumnExists(reader, "tender_id") ? Convert.ToInt32(reader["tender_id"]) : 0,

                                // Item Name logic (itemName1 ya item_name dono handle karega)
                                ItemName = ColumnExists(reader, "itemName1") ? reader["itemName1"].ToString() :
                                (ColumnExists(reader, "item_name") ? reader["item_name"].ToString() : string.Empty),

                                ItemCodeAsPerTender = ColumnExists(reader, "item_code_as_per_tender") ? reader["item_code_as_per_tender"].ToString() : string.Empty,

                                ItemCode = ColumnExists(reader, "item_code") ? reader["item_code"].ToString() : string.Empty,

                                EmdAmount = ColumnExists(reader, "emd_amount") ? Convert.ToDecimal(reader["emd_amount"]) : 0m,

                                TenderQuantity = ColumnExists(reader, "tender_quantity") ? Convert.ToDecimal(reader["tender_quantity"]) : 0m,

                                // New Fields Mapping
                                CategoryName = ColumnExists(reader, "categoryName") ? reader["categoryName"].ToString() : string.Empty,
                                ItemDesc = ColumnExists(reader, "item_desc") ? reader["item_desc"].ToString() : string.Empty
                            });
                        }
                    }
                }
                return Ok(list);
            }
            catch (Exception ex)
            {
                // Debugging ke liye error message return kar rahe hain
                return StatusCode(500, new { message = ex.Message });
            }
        
        }

        private bool ColumnExists(SqlDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetName(i).Equals(columnName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        [HttpPost("AddItemToTender1")]
        public async Task<IActionResult> AddItemToTender1([FromBody] AddTenderItemDto dto)
        {
            // DTO null check to prevent NullReferenceException
            if (dto == null) return BadRequest(new { message = "Invalid data" });

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    await conn.OpenAsync();

                    // 1. EMD Validation (if (emd == "0" || emd == ""))
                    if (dto.EmdAmount <= 0)
                    {
                        return BadRequest(new { message = "Please Enter EMD" });
                    }

                    // 2. Quantity Validation (if (aqty == ""))
                    if (dto.TenderQuantity <= 0)
                    {
                        return BadRequest(new { message = "Please Enter Tender QTY" });
                    }

                    // 3. Duplicate Check (CheckTenderIdAndItemIdExistance)
                    // Note: COUNT(*) use karein taaki ExecuteScalar safely integer return kare
                    string checkQuery = "SELECT COUNT(*) FROM TENDER_ITEMS WHERE TENDER_ID = @Tid AND ITEM_ID = @Iid";
                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@Tid", dto.TenderId);
                        checkCmd.Parameters.AddWithValue("@Iid", dto.ItemId);

                        int existCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                        if (existCount > 0)
                        {
                            return BadRequest(new { message = "Item is Already Linked to this Tender" });
                        }
                    }

                    // 4. Insert Logic (Exactly as per your SQL strSQL)
                    // Values: FLAG='T', status='B', bid_status='1'
                    string insertSql = @"INSERT INTO TENDER_ITEMS 
                                (TENDER_ID, ITEM_ID, TENDER_QUANTITY, EMD_AMOUNT, FLAG, status, bid_status) 
                                VALUES (@Tid, @Iid, @Qty, @Emd, 'T', 'B', '1')";

                    using (SqlCommand insertCmd = new SqlCommand(insertSql, conn))
                    {
                        insertCmd.Parameters.AddWithValue("@Tid", dto.TenderId);
                        insertCmd.Parameters.AddWithValue("@Iid", dto.ItemId);
                        insertCmd.Parameters.AddWithValue("@Qty", dto.TenderQuantity);
                        insertCmd.Parameters.AddWithValue("@Emd", dto.EmdAmount);

                        await insertCmd.ExecuteNonQueryAsync();
                    }

                    return Ok(new { message = "Saved Successfully" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error occurred", error = ex.Message });
            }
        }



        //        [HttpGet("GetTenderHeaderDetails/{tenderId}")]
        //        public async Task<IActionResult> GetTenderHeaderDetails(int tenderId)
        //        {
        //            List<TenderDetailDto> list = new List<TenderDetailDto>();

        //            string query = @"
        //        SELECT A.TENDER_NO, B.YEAR AS FINANCIAL_YEAR, A.domestic_days, A.import_days, A.warranty_year,
        //               CONVERT(VARCHAR, A.TENDER_DATE, 103) AS TENDER_DATE, A.TENDER_DESCRIPTION, A.FLAG, 
        //               A.FINANCIAL_YEAR_ID, A.tender_id,
        //               CONVERT(VARCHAR, A.cover_a, 103) AS cover_a, CONVERT(VARCHAR, A.cover_b, 103) AS cover_b, 
        //               CONVERT(VARCHAR, A.cover_Demo, 103) AS cover_Demo, CONVERT(VARCHAR, A.cover_c, 103) AS cover_c, 
        //               s.cStatus, s.csid, CONVERT(VARCHAR, A.cover_Demo2, 103) AS cover_Demo2, 
        //               CONVERT(VARCHAR, A.cover_Demo3, 103) AS cover_Demo3, TenderRemarks,
        //               webSiteUploadID, eprocID	
        //        FROM TENDERS A
        //        LEFT OUTER JOIN MAS_FINANCIAL_YEAR B ON (A.FINANCIAL_YEAR_ID = B.FINANCIAL_YEAR_ID)
        //        LEFT OUTER JOIN mascoverstatus s ON s.csid = A.csid
        //        WHERE A.TENDER_ID = @Tid";


        ////            SELECT A.TENDER_NO,B.YEAR AS FINANCIAL_YEAR,A.domestic_days,A.import_days,A.warranty_year,
        ////				convert(varchar, A.TENDER_DATE, 103) as TENDER_DATE,A.TENDER_DESCRIPTION,A.FLAG,A.FINANCIAL_YEAR_ID,A.tender_id
        ////                ,Convert(varchar, A.cover_a, 103) AS cover_a, Convert(varchar, A.cover_b, 103) AS cover_b, Convert(varchar, A.cover_Demo, 103) AS cover_Demo
        ////, Convert(varchar, A.cover_c, 103) AS cover_c, s.cStatus,s.csid,A.FLAG,Convert(varchar, A.cover_Demo2, 103) AS cover_Demo2, Convert(varchar, A.cover_Demo3, 103) AS cover_Demo3, TenderRemarks
        ////            , webSiteUploadID, eprocID    FROM TENDERS A
        ////                LEFT OUTER JOIN MAS_FINANCIAL_YEAR B ON(A.FINANCIAL_YEAR_ID = B.FINANCIAL_YEAR_ID)
        ////                left outer join mascoverstatus s on s.csid = A.csid

        ////                WHERE A.TENDER_ID = 680

        //            try
        //            {
        //                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        //                {
        //                    SqlCommand cmd = new SqlCommand(query, conn);
        //                    // Parameterization for Security
        //                    cmd.Parameters.AddWithValue("@Tid", tenderId);

        //                    await conn.OpenAsync();
        //                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
        //                    {
        //                        while (await reader.ReadAsync())
        //                        {
        //                            list.Add(new TenderDetailDto
        //                            {
        //                                TenderNo = reader["TENDER_NO"]?.ToString(),
        //                                FinancialYear = reader["FINANCIAL_YEAR"]?.ToString(),
        //                                DomesticDays = reader["domestic_days"] != DBNull.Value ? Convert.ToInt32(reader["domestic_days"]) : (int?)null,
        //                                ImportDays = reader["import_days"] != DBNull.Value ? Convert.ToInt32(reader["import_days"]) : (int?)null,
        //                                WarrantyYear = reader["warranty_year"] != DBNull.Value ? Convert.ToInt32(reader["warranty_year"]) : (int?)null,
        //                                TenderDate = reader["TENDER_DATE"]?.ToString(),
        //                                TenderDescription = reader["TENDER_DESCRIPTION"]?.ToString(),
        //                                Flag = reader["FLAG"]?.ToString(),
        //                                FinancialYearId = reader["FINANCIAL_YEAR_ID"] != DBNull.Value ? Convert.ToInt32(reader["FINANCIAL_YEAR_ID"]) : (int?)null,
        //                                TenderId = Convert.ToInt32(reader["tender_id"]),
        //                                CoverA = reader["cover_a"]?.ToString(),
        //                                CoverB = reader["cover_b"]?.ToString(),
        //                                CoverDemo = reader["cover_demo"]?.ToString(),
        //                                CoverC = reader["cover_c"]?.ToString(),
        //                                CStatus = reader["cStatus"]?.ToString(),
        //                                Csid = reader["csid"] != DBNull.Value ? Convert.ToInt32(reader["csid"]) : (int?)null,
        //                                CoverDemo2 = reader["cover_Demo2"]?.ToString(),
        //                                CoverDemo3 = reader["cover_Demo3"]?.ToString(),
        //                                TenderRemarks = reader["TenderRemarks"]?.ToString(),
        //                                WebSiteUploadId = reader["webSiteUploadID"]?.ToString(),
        //                                EprocId = reader["eprocID"]?.ToString()
        //                            });
        //                        }
        //                    }
        //                }
        //                return Ok(list);
        //            }
        //            catch (Exception ex)
        //            {
        //                return StatusCode(500, new { message = "Database Error", error = ex.Message });
        //            }
        //        }

        //select dtypeid, dtypename from MASDOCUMENTTYPE
        //public class MASDOCUMENTTYPEDTO



        [HttpGet("GETMASDOCUMENTTYPEList")]
        public async Task<IActionResult> GETMASDOCUMENTTYPEList()
        {
            List<MASDOCUMENTTYPEDTO> list = new List<MASDOCUMENTTYPEDTO>();

            // Exact Query as provided
            string query = @"select dtypeid, dtypename from MASDOCUMENTTYPE";

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    SqlCommand cmd = new SqlCommand(query, conn);
                    await conn.OpenAsync();
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new MASDOCUMENTTYPEDTO
                            {
                                dtypeid = Convert.ToInt32(reader["dtypeid"]),
                                dtypename = reader["dtypename"]?.ToString(),
                             
                            });
                        }
                    }
                }
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error", details = ex.Message });
            }
        }

        [HttpGet("GetSupplierParticipationDetails/{tenderId}/{flag}")]
        public async Task<IActionResult> GetSupplierParticipationDetails(int tenderId, int flag)
        {
            List<TenderSupplierParticipationDto> list = new List<TenderSupplierParticipationDto>();
            string query = "";

            if (flag == 1)
            {
                query = @"
        SELECT 0 slno, ms.SCHSTATUSDID, t.tender_id, mas.name, ms.EMD, isnull(em.ReqEMDAMt,0) as ReqEMDAMt, 
               isnull(em.submittedEMDAMT,0) as submittedEMDAMT, ms.TPAMOUNT, ms.EMDDOCTYPE, ms.EMDPATH, 
               ms.EMDFILENAME, ms.TPFILENAME, ms.TPPATH, ms.EMDDOCNO, mas.supplier_id, ms.REMARK, 
               isnull(piitem.cntparticipated,0) pitems, ms.ISELIGIBLE_B, ms.ISCovTechEli, ms.IsCOVFinEli, 
               ms.CovATechRemarksBefore_OBClM, ms.CovAFINRemarksBefore_OBClM, dt.dtypename, t.csid
        FROM tenders t
        INNER JOIN masschemesstatusdetails ms ON ms.SCHEMEID = t.tender_id
        INNER JOIN massuppliers mas ON mas.supplier_id = ms.SUPPLIERID
        INNER JOIN MASDOCUMENTTYPE dt ON dt.dtypeid = ms.EMDDOCTYPE
        LEFT OUTER JOIN (
            SELECT count(ch.ITEMID) cntparticipated, sc.SCHEMEID, sc.SUPPLIERID 
            FROM SCHEMESTATUSDETAILSCHILD ch
            INNER JOIN masschemesstatusdetails sc ON sc.SCHSTATUSDID = ch.SCHSTATUSDID
            GROUP BY sc.SCHEMEID, sc.SUPPLIERID
        ) piitem ON piitem.SCHEMEID = t.tender_id AND piitem.SUPPLIERID = mas.supplier_id
        LEFT OUTER JOIN (
            SELECT SUPPLIERID, sum(emd_amount) ReqEMDAMt, emd as submittedEMDAMT, SCHEMEID
            FROM (
                SELECT ch.ITEMID, sc.SUPPLIERID, sc.EMD, ti.emd_amount, sc.SCHEMEID 
                FROM SCHEMESTATUSDETAILSCHILD ch
                INNER JOIN masschemesstatusdetails sc ON sc.SCHSTATUSDID = ch.SCHSTATUSDID
                INNER JOIN tender_items ti ON ti.item_id = ch.ITEMID AND ti.tender_id = sc.SCHEMEID
                WHERE sc.SCHEMEID = @Tid
            ) b GROUP BY SUPPLIERID, EMD, SCHEMEID
        ) em ON em.SCHEMEID = t.tender_id AND em.SUPPLIERID = ms.SUPPLIERID
        WHERE t.tender_id = @Tid
        ORDER BY mas.name";
            }
            else if (flag == 2)
            {
                query = @"SELECT distinct 0 as SlNo, m.ITEM_ID, ti.tender_item_id, t.tender_id, m.item_name, 
                         m.item_code_as_per_tender, m.item_code, ti.emd_amount, ti.tender_quantity
                  FROM tenders t
                  INNER JOIN tender_items ti ON ti.tender_id = t.tender_id
                  INNER JOIN masitems m ON m.item_id = ti.item_id
                  LEFT OUTER JOIN (
                      SELECT s.SCHEMEID, sc.ITEMID, count(sc.ITEMID) as nosQualified 
                      FROM schemestatusdetailschild sc 
                      INNER JOIN masschemesstatusdetails s ON s.SCHSTATUSDID = sc.SCHSTATUSDID
                      GROUP BY s.SCHEMEID, sc.ITEMID
                  ) Q ON Q.SCHEMEID = t.tender_id AND q.ITEMID = m.item_id
                  WHERE t.tender_id = @Tid AND q.ITEMID is not null";
            }
            else
            {
                query = @"
        SELECT 0 as slno, ms.SCHSTATUSDID, t.tender_id, mas.name, ms.EMD, ms.TPAMOUNT, 
               ms.EMDDOCTYPE, ms.EMDPATH, ms.EMDFILENAME, ms.TPFILENAME, ms.TPPATH, 
               ms.EMDDOCNO, mas.supplier_id, ms.REMARK, isnull(piitem.cntparticipated, 0) as pitems, 
               ms.ISELIGIBLE_B 
        FROM tenders t
        INNER JOIN masschemesstatusdetails ms ON ms.SCHEMEID = t.tender_id
        INNER JOIN massuppliers mas ON mas.supplier_id = ms.SUPPLIERID
        LEFT OUTER JOIN (
            SELECT count(ch.ITEMID) as cntparticipated, sc.SCHEMEID, sc.SUPPLIERID 
            FROM SCHEMESTATUSDETAILSCHILD ch
            INNER JOIN masschemesstatusdetails sc ON sc.SCHSTATUSDID = ch.SCHSTATUSDID
            GROUP BY sc.SCHEMEID, sc.SUPPLIERID
        ) piitem ON piitem.SCHEMEID = t.tender_id AND piitem.SUPPLIERID = mas.supplier_id
        WHERE t.tender_id = @Tid
        ORDER BY mas.name";
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@Tid", tenderId);
                    await conn.OpenAsync();

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        int counter = 1;
                        while (await reader.ReadAsync())
                        {
                            var item = new TenderSupplierParticipationDto();
                            item.SlNo = counter++;

                          
                            if (flag == 2)
                            {
                                item.TenderId = Convert.ToInt32(reader["tender_id"]);
                                item.ItemId = reader["ITEM_ID"] != DBNull.Value ? Convert.ToInt32(reader["ITEM_ID"]) : 0;
                                item.TenderItemId = reader["tender_item_id"] != DBNull.Value ? Convert.ToInt32(reader["tender_item_id"]) : 0;
                                item.ItemName = reader["item_name"]?.ToString() ?? "";
                                item.ItemCodeAsPerTender = reader["item_code_as_per_tender"]?.ToString() ?? "";
                                item.ItemCode = reader["item_code"]?.ToString() ?? "";
                                item.Emd = reader["emd_amount"] != DBNull.Value ? Convert.ToDecimal(reader["emd_amount"]) : 0m;
                                item.TenderQuantity = reader["tender_quantity"] != DBNull.Value ? Convert.ToDecimal(reader["tender_quantity"]) : 0m;
                            }
                            else
                            {
                                item.SchStatusDid = Convert.ToInt32(reader["SCHSTATUSDID"]);
                                item.TenderId = Convert.ToInt32(reader["tender_id"]);
                                item.SupplierName = reader["name"]?.ToString() ?? "";
                                item.Emd = Convert.ToDecimal(reader["EMD"]);
                                item.TpAmount = Convert.ToDecimal(reader["TPAMOUNT"]);
                                item.EmdDocType = reader["EMDDOCTYPE"]?.ToString() ?? "";
                                item.EmdPath = reader["EMDPATH"]?.ToString() ?? "";
                                item.EmdFileName = reader["EMDFILENAME"]?.ToString() ?? "";
                                item.TpFileName = reader["TPFILENAME"]?.ToString() ?? "";
                                item.TpPath = reader["TPPATH"]?.ToString() ?? "";
                                item.EmdDocNo = reader["EMDDOCNO"]?.ToString() ?? "";
                                item.SupplierId = Convert.ToInt32(reader["supplier_id"]);
                                item.Remark = reader["REMARK"]?.ToString() ?? "";
                                item.PItems = Convert.ToInt32(reader["pitems"]);
                                item.IsEligibleB = reader["ISELIGIBLE_B"]?.ToString() ?? "";

                                if (flag == 1)
                                {
                                    item.ReqEMDAMt = Convert.ToDecimal(reader["ReqEMDAMt"]);
                                    item.SubmittedEMDAMT = Convert.ToDecimal(reader["submittedEMDAMT"]);
                                    item.DTypeName = reader["dtypename"]?.ToString() ?? "";
                                    item.IsCovTechEli = reader["ISCovTechEli"]?.ToString() ?? "";
                                    item.IsCOVFinEli = reader["IsCOVFinEli"]?.ToString() ?? "";
                                    item.CovATechRemarksBefore_OBClM = reader["CovATechRemarksBefore_OBClM"]?.ToString() ?? "";
                                    item.CovAFINRemarksBefore_OBClM = reader["CovAFINRemarksBefore_OBClM"]?.ToString() ?? "";
                                    item.Csid = Convert.ToInt32(reader["csid"]);
                                }
                            }

                            list.Add(item);
                        }
                    }
                }
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching supplier data", error = ex.Message });
            }
        }

        //        [HttpGet("GetSupplierParticipationDetails/{tenderId}/{flag}")]
        //        public async Task<IActionResult> GetSupplierParticipationDetails(int tenderId, int flag)
        //        {
        //            List<TenderSupplierParticipationDto> list = new List<TenderSupplierParticipationDto>();
        //            string query = "";

        //            if (flag == 1)
        //            {
        //                query = @"
        //        SELECT 0 slno, ms.SCHSTATUSDID, t.tender_id, mas.name, ms.EMD, isnull(em.ReqEMDAMt,0) as ReqEMDAMt, 
        //               isnull(em.submittedEMDAMT,0) as submittedEMDAMT, ms.TPAMOUNT, ms.EMDDOCTYPE, ms.EMDPATH, 
        //               ms.EMDFILENAME, ms.TPFILENAME, ms.TPPATH, ms.EMDDOCNO, mas.supplier_id, ms.REMARK, 
        //               isnull(piitem.cntparticipated,0) pitems, ms.ISELIGIBLE_B, ms.ISCovTechEli, ms.IsCOVFinEli, 
        //               ms.CovATechRemarksBefore_OBClM, ms.CovAFINRemarksBefore_OBClM, dt.dtypename, t.csid
        //        FROM tenders t
        //        INNER JOIN masschemesstatusdetails ms ON ms.SCHEMEID = t.tender_id
        //        INNER JOIN massuppliers mas ON mas.supplier_id = ms.SUPPLIERID
        //        INNER JOIN MASDOCUMENTTYPE dt ON dt.dtypeid = ms.EMDDOCTYPE
        //        LEFT OUTER JOIN (
        //            SELECT count(ch.ITEMID) cntparticipated, sc.SCHEMEID, sc.SUPPLIERID 
        //            FROM SCHEMESTATUSDETAILSCHILD ch
        //            INNER JOIN masschemesstatusdetails sc ON sc.SCHSTATUSDID = ch.SCHSTATUSDID
        //            GROUP BY sc.SCHEMEID, sc.SUPPLIERID
        //        ) piitem ON piitem.SCHEMEID = t.tender_id AND piitem.SUPPLIERID = mas.supplier_id
        //        LEFT OUTER JOIN (
        //            SELECT SUPPLIERID, sum(emd_amount) ReqEMDAMt, emd as submittedEMDAMT, SCHEMEID
        //            FROM (
        //                SELECT ch.ITEMID, sc.SUPPLIERID, sc.EMD, ti.emd_amount, sc.SCHEMEID 
        //                FROM SCHEMESTATUSDETAILSCHILD ch
        //                INNER JOIN masschemesstatusdetails sc ON sc.SCHSTATUSDID = ch.SCHSTATUSDID
        //                INNER JOIN tender_items ti ON ti.item_id = ch.ITEMID AND ti.tender_id = sc.SCHEMEID
        //                WHERE sc.SCHEMEID = @Tid
        //            ) b GROUP BY SUPPLIERID, EMD, SCHEMEID
        //        ) em ON em.SCHEMEID = t.tender_id AND em.SUPPLIERID = ms.SUPPLIERID
        //        WHERE t.tender_id = @Tid
        //        ORDER BY mas.name";
        //            }
        //            else if (flag == 2) 
        //            {

        //                query = @"select distinct 0 as SlNo,m.ITEM_ID,ti.tender_item_id,t.tender_id,m.item_name,m.item_code_as_per_tender,m.item_code
        //,ti.emd_amount,ti.tender_quantity
        // from tenders t
        //inner join tender_items ti on ti.tender_id=t.tender_id
        //inner join masitems m on m.item_id=ti.item_id
        //left outer join 
        //(
        //select s.SCHEMEID,sc.ITEMID,count(sc.ITEMID) as nosQualified from schemestatusdetailschild sc 
        //inner join masschemesstatusdetails s on s.SCHSTATUSDID=sc.SCHSTATUSDID
        //where 1=1 --and  s.ISELIGIBLE_B='Y' and sc.FLAGCOB='Y' and FLAGCOC='Y' 
        //group by s.SCHEMEID,sc.ITEMID
        //) Q on Q.SCHEMEID=t.tender_id and q.ITEMID=m.item_id

        //where 1=1  and   t.tender_id=@Tid and
        //  q.ITEMID is not null";
        //            }

        //            else
        //            {
        //                query = @"
        //        SELECT 0 as slno, ms.SCHSTATUSDID, t.tender_id, mas.name, ms.EMD, ms.TPAMOUNT, 
        //               ms.EMDDOCTYPE, ms.EMDPATH, ms.EMDFILENAME, ms.TPFILENAME, ms.TPPATH, 
        //               ms.EMDDOCNO, mas.supplier_id, ms.REMARK, isnull(piitem.cntparticipated, 0) as pitems, 
        //               ms.ISELIGIBLE_B 
        //        FROM tenders t
        //        INNER JOIN masschemesstatusdetails ms ON ms.SCHEMEID = t.tender_id
        //        INNER JOIN massuppliers mas ON mas.supplier_id = ms.SUPPLIERID
        //        LEFT OUTER JOIN (
        //            SELECT count(ch.ITEMID) as cntparticipated, sc.SCHEMEID, sc.SUPPLIERID 
        //            FROM SCHEMESTATUSDETAILSCHILD ch
        //            INNER JOIN masschemesstatusdetails sc ON sc.SCHSTATUSDID = ch.SCHSTATUSDID
        //            GROUP BY sc.SCHEMEID, sc.SUPPLIERID
        //        ) piitem ON piitem.SCHEMEID = t.tender_id AND piitem.SUPPLIERID = mas.supplier_id
        //        WHERE t.tender_id = @Tid
        //        ORDER BY mas.name";
        //            }

        //            try
        //            {
        //                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        //                {
        //                    SqlCommand cmd = new SqlCommand(query, conn);
        //                    cmd.Parameters.AddWithValue("@Tid", tenderId);
        //                    await conn.OpenAsync();

        //                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
        //                    {
        //                        int counter = 1;
        //                        while (await reader.ReadAsync())
        //                        {
        //                            var item = new TenderSupplierParticipationDto
        //                            {
        //                                SlNo = counter++,
        //                                SchStatusDid = Convert.ToInt32(reader["SCHSTATUSDID"]),
        //                                TenderId = Convert.ToInt32(reader["tender_id"]),
        //                                SupplierName = reader["name"]?.ToString() ?? "",
        //                                Emd = Convert.ToDecimal(reader["EMD"]),
        //                                TpAmount = Convert.ToDecimal(reader["TPAMOUNT"]),
        //                                EmdDocType = reader["EMDDOCTYPE"]?.ToString() ?? "",
        //                                EmdPath = reader["EMDPATH"]?.ToString() ?? "",
        //                                EmdFileName = reader["EMDFILENAME"]?.ToString() ?? "",
        //                                TpFileName = reader["TPFILENAME"]?.ToString() ?? "",
        //                                TpPath = reader["TPPATH"]?.ToString() ?? "",
        //                                EmdDocNo = reader["EMDDOCNO"]?.ToString() ?? "",
        //                                SupplierId = Convert.ToInt32(reader["supplier_id"]),
        //                                Remark = reader["REMARK"]?.ToString() ?? "",
        //                                PItems = Convert.ToInt32(reader["pitems"]),
        //                                IsEligibleB = reader["ISELIGIBLE_B"]?.ToString() ?? ""
        //                            };

        //                            // Flag 1 ke extra fields handle karein
        //                            if (flag == 1)
        //                            {
        //                                item.ReqEMDAMt = Convert.ToDecimal(reader["ReqEMDAMt"]);
        //                                item.SubmittedEMDAMT = Convert.ToDecimal(reader["submittedEMDAMT"]);
        //                                item.DTypeName = reader["dtypename"]?.ToString() ?? "";
        //                                item.IsCovTechEli = reader["ISCovTechEli"]?.ToString() ?? "";
        //                                item.IsCOVFinEli = reader["IsCOVFinEli"]?.ToString() ?? "";
        //                                item.CovATechRemarksBefore_OBClM = reader["CovATechRemarksBefore_OBClM"]?.ToString() ?? "";
        //                                item.CovAFINRemarksBefore_OBClM = reader["CovAFINRemarksBefore_OBClM"]?.ToString() ?? "";
        //                                item.Csid = Convert.ToInt32(reader["csid"]);
        //                            }

        //                            list.Add(item);
        //                        }
        //                    }
        //                }
        //                return Ok(list);
        //            }
        //            catch (Exception ex)
        //            {
        //                return StatusCode(500, new { message = "Error fetching supplier data", error = ex.Message });
        //            }
        //        }
        //[HttpGet("GetSupplierParticipationDetails/{tenderId}")]
        //public async Task<IActionResult> GetSupplierParticipationDetails(int tenderId)
        //{
        //    List<TenderSupplierParticipationDto> list = new List<TenderSupplierParticipationDto>();

        //    string query = @"
        //SELECT 0 as slno, ms.SCHSTATUSDID, t.tender_id, mas.name, ms.EMD, ms.TPAMOUNT, 
        //       ms.EMDDOCTYPE, ms.EMDPATH, ms.EMDFILENAME, ms.TPFILENAME, ms.TPPATH, 
        //       ms.EMDDOCNO, mas.supplier_id, ms.REMARK, isnull(piitem.cntparticipated, 0) as pitems, 
        //       ms.ISELIGIBLE_B 
        //FROM tenders t
        //INNER JOIN masschemesstatusdetails ms ON ms.SCHEMEID = t.tender_id
        //INNER JOIN massuppliers mas ON mas.supplier_id = ms.SUPPLIERID
        //LEFT OUTER JOIN (
        //    SELECT count(ch.ITEMID) as cntparticipated, sc.SCHEMEID, sc.SUPPLIERID 
        //    FROM SCHEMESTATUSDETAILSCHILD ch
        //    INNER JOIN masschemesstatusdetails sc ON sc.SCHSTATUSDID = ch.SCHSTATUSDID
        //    GROUP BY sc.SCHEMEID, sc.SUPPLIERID
        //) piitem ON piitem.SCHEMEID = t.tender_id AND piitem.SUPPLIERID = mas.supplier_id
        //WHERE t.tender_id = @Tid
        //ORDER BY mas.name";

        //    try
        //    {
        //        using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        //        {
        //            SqlCommand cmd = new SqlCommand(query, conn);
        //            cmd.Parameters.AddWithValue("@Tid", tenderId);
        //            await conn.OpenAsync();

        //            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
        //            {
        //                int counter = 1;
        //                while (await reader.ReadAsync())
        //                {
        //                    list.Add(new TenderSupplierParticipationDto
        //                    {
        //                        SlNo = counter++, // Row index for frontend
        //                        SchStatusDid = reader["SCHSTATUSDID"] != DBNull.Value ? Convert.ToInt32(reader["SCHSTATUSDID"]) : 0,
        //                        TenderId = reader["tender_id"] != DBNull.Value ? Convert.ToInt32(reader["tender_id"]) : 0,
        //                        SupplierName = reader["name"]?.ToString() ?? string.Empty,
        //                        Emd = reader["EMD"] != DBNull.Value ? Convert.ToDecimal(reader["EMD"]) : 0m,
        //                        TpAmount = reader["TPAMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["TPAMOUNT"]) : 0m,
        //                        EmdDocType = reader["EMDDOCTYPE"]?.ToString() ?? string.Empty,
        //                        EmdPath = reader["EMDPATH"]?.ToString() ?? string.Empty,
        //                        EmdFileName = reader["EMDFILENAME"]?.ToString() ?? string.Empty,
        //                        TpFileName = reader["TPFILENAME"]?.ToString() ?? string.Empty,
        //                        TpPath = reader["TPPATH"]?.ToString() ?? string.Empty,
        //                        EmdDocNo = reader["EMDDOCNO"]?.ToString() ?? string.Empty,
        //                        SupplierId = reader["supplier_id"] != DBNull.Value ? Convert.ToInt32(reader["supplier_id"]) : 0,
        //                        Remark = reader["REMARK"]?.ToString() ?? string.Empty,
        //                        PItems = reader["pitems"] != DBNull.Value ? Convert.ToInt32(reader["pitems"]) : 0,
        //                        IsEligibleB = reader["ISELIGIBLE_B"]?.ToString() ?? string.Empty
        //                    });
        //                }
        //            }
        //        }
        //        return Ok(list);
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { message = "Error fetching supplier data", error = ex.Message });
        //    }
        //}

        [HttpPost("SaveSupplierParticipation")]
        public async Task<IActionResult> SaveSupplierParticipation([FromBody] SupplierParticipationDto dto)
        {
            if (dto == null) return BadRequest(new { message = "Invalid Data" });

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    await conn.OpenAsync();

                    // 1. Validation: Check Already Inserted (Duplicate Check)
                    string checkQuery = "SELECT COUNT(*) FROM masschemesstatusdetails WHERE schemeid = @Tid AND SUPPLIERID = @Sid";
                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@Tid", dto.TenderId);
                        checkCmd.Parameters.AddWithValue("@Sid", dto.SupplierId);
                        int existCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                        if (existCount > 0)
                        {
                            return BadRequest(new { message = "Already Saved Selected Supplier for Participation" });
                        }
                    }

                    // 2. Final Insert Logic
                    string insertSql = @"
                INSERT INTO masschemesstatusdetails
                (schemeid, Status, SUPPLIERID, EMD, EMDDOCTYPE, TPAMOUNT, remark, emddocno) 
                VALUES 
                (@Tid, 2, @Sid, @Emd, @DocType, @Fee, @Remark, @DocNo)";

                    using (SqlCommand insertCmd = new SqlCommand(insertSql, conn))
                    {
                        insertCmd.Parameters.AddWithValue("@Tid", dto.TenderId);
                        insertCmd.Parameters.AddWithValue("@Sid", dto.SupplierId);
                        insertCmd.Parameters.AddWithValue("@Emd", dto.EmdAmount);
                        insertCmd.Parameters.AddWithValue("@DocType", dto.DocTypeId);
                        insertCmd.Parameters.AddWithValue("@Fee", dto.TenderProFee);
                        insertCmd.Parameters.AddWithValue("@Remark", (object)dto.Remarks ?? DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@DocNo", dto.EmdDocNo);

                        await insertCmd.ExecuteNonQueryAsync();
                    }

                    return Ok(new { message = "Successfully Saved Supplier Participation" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error occurred", error = ex.Message });
            }
        }



        [HttpGet("GetTenderStatus/{tenderId}")]
        public async Task<IActionResult> GetTenderStatus(int tenderId)
        {
            TenderStatusDetailsDto result = null;
            string query = @"
        SELECT ms.tender_id, ms.tender_no, ms.financial_year_id,
        (CASE WHEN ms.csid='1' THEN 'Tender Live'
              WHEN ms.csid='2' THEN 'Cover A Opened' 
              WHEN ms.csid='3' THEN 'Cover B Opened' 
              WHEN ms.csid='4' THEN 'Under Demonstration' 
              WHEN ms.csid='5' THEN 'Price Bid Opened' 
              ELSE 'Cancelled' END) as Status,
        CONVERT(VARCHAR, tender_date, 103) as tender_date, 
        mas.SCHSTATUSDID, 
        CONVERT(VARCHAR, ENDDate, 103) as ENDDate
        FROM tenders ms 
        INNER JOIN masschemesstatusdetails mas ON mas.SCHEMEID = ms.tender_id  
        WHERE ms.tender_id = @Tid";

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@Tid", tenderId);
                    await conn.OpenAsync();
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            result = new TenderStatusDetailsDto
                            {
                                TenderId = Convert.ToInt32(reader["tender_id"]),
                                TenderNo = reader["tender_no"].ToString(),
                                FinancialYearId = Convert.ToInt32(reader["financial_year_id"]),
                                Status = reader["Status"].ToString(),
                                TenderDate = reader["tender_date"].ToString(),
                                SchStatusDid = Convert.ToInt32(reader["SCHSTATUSDID"]),
                                EndDate = reader["ENDDate"].ToString()
                            };
                        }
                    }
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("GetTenderItems/{tenderId}")]
        public async Task<IActionResult> GetTenderItems(int tenderId)
        {
            List<TenderItemDetailsDto> list = new List<TenderItemDetailsDto>();
            string query = @"
        SELECT 0 as SlNo, ms.tender_id, ms.financial_year_id, mi.item_id, 
               mi.item_code_as_per_tender, mi.item_name
        FROM tenders ms 
        LEFT OUTER JOIN tender_items a ON a.tender_id = ms.tender_id
        LEFT OUTER JOIN masitems mi ON mi.item_id = a.item_id  
        WHERE ms.tender_id = @Tid";

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@Tid", tenderId);
                    await conn.OpenAsync();
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        int count = 1;
                        while (await reader.ReadAsync())
                        {
                            list.Add(new TenderItemDetailsDto
                            {
                                SlNo = count++,
                                TenderId = Convert.ToInt32(reader["tender_id"]),
                                FinancialYearId = Convert.ToInt32(reader["financial_year_id"]),
                                ItemId = reader["item_id"] != DBNull.Value ? Convert.ToInt32(reader["item_id"]) : 0,
                                ItemCodeAsPerTender = reader["item_code_as_per_tender"]?.ToString() ?? "",
                                ItemName = reader["item_name"]?.ToString() ?? ""
                            });
                        }
                    }
                }
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("SaveBulkEquipmentParticipation")]
        public async Task<IActionResult> SaveBulkEquipmentParticipation([FromBody] BulkParticipationRequest request)
        {
            if (request == null || request.ItemIds == null || request.ItemIds.Count == 0)
                return BadRequest(new { message = "No items selected." });

            int flag = 0;
            string connString = _config.GetConnectionString("DefaultConnection");

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    await conn.OpenAsync();

                    foreach (var itemId in request.ItemIds)
                    {
                        // 1. Check If Already Inserted
                        bool exists = false;
                        string checkQuery = @"SELECT COUNT(*) FROM schemestatusdetailschild 
                                      WHERE supplierId = @Sid AND schemeid = @SchemeId 
                                      AND itemId = @Iid AND schstatusdid = @Sdid";

                        using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                        {
                            checkCmd.Parameters.AddWithValue("@Sid", request.SupplierId);
                            checkCmd.Parameters.AddWithValue("@SchemeId", request.SchemeId);
                            checkCmd.Parameters.AddWithValue("@Iid", itemId);
                            checkCmd.Parameters.AddWithValue("@Sdid", request.SchStatusDid);

                            exists = (int)await checkCmd.ExecuteScalarAsync() > 0;
                        }

                        // 2. If Not Exists, then Insert
                        if (!exists)
                        {
                            string insertQuery = @"INSERT INTO schemestatusdetailschild (supplierid, schemeid, itemid, schstatusdid, flagcoa)
                                           VALUES (@Sid, @SchemeId, @Iid, @Sdid, 'Y')";

                            using (SqlCommand insertCmd = new SqlCommand(insertQuery, conn))
                            {
                                insertCmd.Parameters.AddWithValue("@Sid", request.SupplierId);
                                insertCmd.Parameters.AddWithValue("@SchemeId", request.SchemeId);
                                insertCmd.Parameters.AddWithValue("@Iid", itemId);
                                insertCmd.Parameters.AddWithValue("@Sdid", request.SchStatusDid);

                                await insertCmd.ExecuteNonQueryAsync();
                                flag++;
                            }
                        }
                    }
                }

                return Ok(new { message = $"{flag} No of Equipment Participated by Bidder, Successfully Saved" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Database Error", details = ex.Message });
            }
        }


     

            [HttpPost("SaveTender")]
            public async Task<IActionResult> SaveTender([FromBody] TenderSaveRequest11 request)
            {
                // 1. Validations (Exactly as your logic)
                if (string.IsNullOrEmpty(request.TenderNo))
                    return BadRequest(new { message = "Enter Tender Number" });

                if (IsTenderExisted1(request.TenderNo))
                    return BadRequest(new { message = "Tender has been Already Added" });

                if (string.IsNullOrEmpty(request.TenderDescription))
                    return BadRequest(new { message = "Enter tender Description" });

                if (string.IsNullOrEmpty(request.FinancialYearId) || request.FinancialYearId == "0")
                    return BadRequest(new { message = "Select Financial Year" });

                if (string.IsNullOrEmpty(request.TenderDate))
                    return BadRequest(new { message = "Enter Tender Date" });

                // 2. Logic Check for Financial Year (Placeholder for your CheckYearBasedonTDate)
                // if (!CheckYearBasedonTDate(request.FinancialYearId, request.TenderDate)) 
                // { return BadRequest(new { message = "Please Check Financial Year, it must be based on Tender Date" }); }

                try
                {
                    using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                    {
                        string isGemTenderStr = request.IsGemTender ? "Y" : "N";

                        string query = @"INSERT INTO TENDERS 
                                (TENDER_NO, tender_date, TENDER_DESCRIPTION, FINANCIAL_YEAR_ID, isGemTender, eProcID, tValue) 
                                VALUES 
                                (@TNo, @TDate, @TDesc, @FinId, @IsGem, @GemNo, @TVal)";

                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@TNo", request.TenderNo.Trim());
                            cmd.Parameters.AddWithValue("@TDate", DateTime.Parse(request.TenderDate));
                            cmd.Parameters.AddWithValue("@TDesc", request.TenderDescription.Trim());
                            cmd.Parameters.AddWithValue("@FinId", request.FinancialYearId);
                            cmd.Parameters.AddWithValue("@IsGem", isGemTenderStr);

                            // Handle NULL for GemBidNo
                            cmd.Parameters.AddWithValue("@GemNo", string.IsNullOrEmpty(request.GemBidNo) ? (object)DBNull.Value : request.GemBidNo.Trim());
                            cmd.Parameters.AddWithValue("@TVal", request.TenderValue);

                            await conn.OpenAsync();
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    return Ok(new
                    {
                        message = $"Saved Successfully with Tender No {request.TenderNo}, Please Add Items & Leavy for the Tender"
                    });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { message = ex.Message });
                }
            }

            // Existing Check Logic
            private bool IsTenderExisted1(string tenderNo)
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    string query = "SELECT COUNT(*) FROM TENDERS WHERE TENDER_NO = @TNo";
                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@TNo", tenderNo);
                    conn.Open();
                    int count = (int)cmd.ExecuteScalar();
                    return count > 0;
                }
            }

        [HttpGet("GetParticipationItems")]
        public async Task<IActionResult> GetParticipationItems(int schemeId, int supplierId)
        {
            List<ParticipationItemDTO> items = new List<ParticipationItemDTO>();
            string connString = _config.GetConnectionString("DefaultConnection");

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    // Query with Parameters
                    string sql = @"SELECT 0 AS SlNo, ms.schemeid, mi.item_code_as_per_tender AS itemcode, 
                                  mi.item_name AS itemname, ti.emd_amount, mi.item_id AS itemId
                           FROM masschemesstatusdetails ms 
                           INNER JOIN schemestatusdetailschild msc ON msc.schemeid = ms.schemeid AND msc.supplierId = ms.supplierId 
                           INNER JOIN masitems mi ON mi.item_id = msc.itemid 
                           INNER JOIN tender_items ti ON ti.item_id = mi.item_id AND ti.tender_id = ms.SCHEMEID
                           WHERE ms.schemeid = @SchemeId AND ms.supplierId = @SupplierId";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@SchemeId", schemeId);
                        cmd.Parameters.AddWithValue("@SupplierId", supplierId);

                        await conn.OpenAsync();
                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            while (await dr.ReadAsync())
                            {
                                items.Add(new ParticipationItemDTO
                                {
                                    SlNo = Convert.ToInt32(dr["SlNo"]),
                                    SchemeId = Convert.ToInt32(dr["schemeid"]),
                                    ItemCode = dr["itemcode"].ToString(),
                                    ItemName = dr["itemname"].ToString(),
                                    EmdAmount = Convert.ToDecimal(dr["emd_amount"]),
                                    ItemId = Convert.ToInt32(dr["itemId"])
                                });
                            }
                        }
                    }
                }
                return Ok(items);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching data", details = ex.Message });
            }
        }
       
        [HttpPost("DeleteParticipationItems")]
        public async Task<IActionResult> DeleteParticipationItems([FromBody] DeleteParticipationRequest request)
        {
            if (request == null || request.ItemIds == null || request.ItemIds.Count == 0)
                return BadRequest(new { message = "No items selected for deletion." });

            string connString = _config.GetConnectionString("DefaultConnection");
            int deletedCount = 0;

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    await conn.OpenAsync();

                    foreach (var itemId in request.ItemIds)
                    {
                        // SQL Injection se bachne ke liye Parameters ka use
                        string sql = @"DELETE FROM schemestatusdetailschild 
                               WHERE supplierid = @Sid 
                               AND schemeid = @SchemeId 
                               AND itemid = @ItemId";

                        using (SqlCommand cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@Sid", request.SupplierId);
                            cmd.Parameters.AddWithValue("@SchemeId", request.SchemeId);
                            cmd.Parameters.AddWithValue("@ItemId", itemId);

                            int rowsAffected = await cmd.ExecuteNonQueryAsync();
                            if (rowsAffected > 0) deletedCount++;
                        }
                    }
                }

                return Ok(new { message = $"{deletedCount} Items deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error during deletion", details = ex.Message });
            }
        }

        [HttpPost("UpdateTenderLevy")]
        public async Task<IActionResult> UpdateTenderLevy([FromBody] UpdateLevyRequest request)
        {
            // 1. Basic Validations
            if (request.CancellationDays <= 0) return BadRequest(new { message = "Enter Valid Cancellation Days" });
            if (request.LogoCharges < 0) return BadRequest(new { message = "Enter Valid Logo Charges" });

            string connString = _config.GetConnectionString("DefaultConnection");

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    await conn.OpenAsync();
                    string query = "";

                    // 2. Logic: If both dates are empty (First time update)
                    if (string.IsNullOrEmpty(request.LeavyEntryDt) && string.IsNullOrEmpty(request.PerfEntryDt))
                    {
                        query = @"UPDATE TENDERS SET 
                            cancellationdays = @CDays, 
                            cancellationpercentage = @CPer, 
                            penaltypercent120 = @P120, 
                            penaltypercent = @PPer, 
                            penaltytype = @PType, 
                            releasetype = @RType, 
                            performacereq = @PReq, 
                            releasevalue = @RVal, 
                            logocharges = @LChar, 
                            logochargesUpper = @LUp, 
                            leavyEntryDt = GETDATE(), 
                            performanceentrydt = GETDATE() 
                          WHERE TENDER_ID = @TId";
                    }
                    // 3. Logic: If only performance date is empty
                    else if (string.IsNullOrEmpty(request.PerfEntryDt))
                    {
                        query = @"UPDATE TENDERS SET 
                            releasetype = @RType, 
                            performacereq = @PReq, 
                            releasevalue = @RVal, 
                            performanceentrydt = GETDATE() 
                          WHERE TENDER_ID = @TId";
                    }
                    else
                    {
                        // Default Update if both exist (Optional, as per your old logic)
                        return Ok(new { message = "Data already exists, no update needed." });
                    }

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@TId", request.TenderId);
                        cmd.Parameters.AddWithValue("@CDays", request.CancellationDays);
                        cmd.Parameters.AddWithValue("@CPer", request.CancellationPercentage);
                        cmd.Parameters.AddWithValue("@P120", request.PenaltyPercent120);
                        cmd.Parameters.AddWithValue("@PPer", request.PenaltyPercent);
                        cmd.Parameters.AddWithValue("@PType", request.PenaltyType);
                        cmd.Parameters.AddWithValue("@RType", request.ReleaseType);
                        cmd.Parameters.AddWithValue("@PReq", request.PerformanceReq);
                        cmd.Parameters.AddWithValue("@RVal", request.ReleaseValue);
                        cmd.Parameters.AddWithValue("@LChar", request.LogoCharges);
                        cmd.Parameters.AddWithValue("@LUp", request.LogoChargesUpper);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                return Ok(new { message = "Levy Successfully Updated" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Database Error", details = ex.Message });
            }
        }

        [HttpGet("GetTenderSummary/{tenderId}")]
        public async Task<IActionResult> GetTenderSummary(int tenderId)
        {
            var resultList = new List<AddTenderStatusDTO>();
            string connString = _config.GetConnectionString("DefaultConnection");

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    // Aapki poori query yahan string mein aayegi
                    // Maine query ko short rakha hai, aap wahi exact query copy karein
                    string sql = @"select b.financial_year_id,b.tender_no,convert(varchar,b.tender_date,105) as LiveDT,convert(varchar,b.ENDDate,105) as TLast,a.NoSitems,
case when b.csid=5 and b.cover_c is not null and noofPriceFound>0 and a.noofPriceFound>(nosAccepted+nosRejected)
then  'Cov-C,Price Entered-Acceptance/Rejection Pending since '+cast(DATEDIFF(DAY,b.cover_c,GETDATE()) as varchar)+' days' 
else case when b.csid=5 and b.cover_c is not null and noofPriceFound>0 and (nosAccepted+nosRejected)=a.noofPriceFound and (nosAccepted)>a.nosRC and isnull(a.nosRC,0)=0
then  'Price Accepted,'+cast(a.nosAccepted as varchar)+' Items' 
else case when b.csid=5 and b.cover_c is not null and noofPriceFound>0 and (nosAccepted)=a.nosRC
then  'Rate Contract'+cast(a.nosRC as varchar)+' Items'

else case when b.csid=5 and b.cover_c is not null and  (nosAccepted)>a.nosRC
then  'RC Pending for,'+cast((nosAccepted-a.nosRC) as varchar)+' Items'
else case when b.csid=6 then 'Cancelled(Dated '+convert(varchar,b.CancelledDT,105)+')' 
else case when b.csid=4 then 'Under Demo'
else a.FinalStatus end end end end end end as FinalStatus,
cast(a.noItemsA as varchar) as nosItemsA,cast(a.NoSupplierA as varchar) as AItemsSupplier,
convert(varchar,b.cover_a,105) as cover_a ,
DATEDIFF(DAY,b.ENDDate,b.cover_a) as COVA_LastDays, cast(a.nositemsDA as varchar) nositemsDA ,cast(a.nossupplierDA as varchar) as ADAItemsSupplier
,convert(varchar,B.ObjCEndDT,105) AS ObjClaimLastDate 
,convert(varchar,B.ObjCStartDT,105) AS ObjCStartDT 

,convert(varchar,B.cover_b,105) AS COVBDT
,convert(varchar,B.cover_c,105) AS COVCDT
 ,a.noofPriceFound,a.nosAccepted,a.nosRejected
 ,case when b.cover_c is not null and b.CancelledDT is null then DATEDIFF(DAY,b.tender_date,b.cover_c)
 else case when b.CancelledDT is not null then DATEDIFF(DAY,b.tender_date,b.CancelledDT)
 else DATEDIFF(DAY,b.tender_date, getdate()) end end  as DaysTakenFromLiveDT
 ,DATEDIFF(DAY,b.cover_a,b.ObjCStartDT) as COVAToObjStartDays,
 DATEDIFF(DAY,b.ObjCStartDT,b.ObjCEndDT) as OBJDays
 ,DATEDIFF(DAY,b.ObjCEndDT,b.cover_b) as ClaimEndToBDays
 ,DATEDIFF(DAY,b.cover_b,b.cover_c) as CovBToCovCDays
 ,b.csid
 ,b.tender_id
 , case when  getdate()>b.ENDDate then DATEDIFF(DAY,b.ENDDate,getdate()) else 0 end as daysClosed
 ,case when (nosAccepted)>0  and nosRC=(nosAccepted) then 1 else 0 end  as Show
 ,nosRC
from tenders b
inner join MasCoverStatus sc on sc.CSID=b.csid
left outer join 
 (
select t.tender_id, isnull(nosItems,0) as NoSitems,
               isnull(nositemsA,0) noItemsA,isnull(nossupplier,0) as NoSupplierA
               
               ,isnull(nositemsDA,0) nositemsDA,isnull(nossupplierDA,0) as nossupplierDA 
			
               , case when t.csid=1 then (case when getdate()-1>t.ENDDate then 'Cov-A Opening Pending Since '+cast(DATEDIFF(DAY,t.ENDDate,getdate()-1) as varchar)+' days' else 'Live' end)
               else case when t.csid=2 and isnull(nositemsA,0)=0 then 'Cov-A Item Entry Pending Since '+cast(DATEDIFF(DAY,t.cover_a,GETDATE()) as varchar)+' days' 
               else case when t.csid=2 and isnull(nositemsA,0)>0 and scT.nosTEch>0 and isnull(scF.nosFIN,0)=0  then 'Cov-A Technical Evaluation Pending Since '+cast(DATEDIFF(DAY,t.cover_a,GETDATE()) as varchar)+' days' 
               else case when t.csid=2 and isnull(nositemsA,0)>0 and isnull(scT.nosTEch,0)>0 and isnull(scF.nosFIN,0)>0  then 'Cov-A Technical and Finalcial Evaluation Pending Since '+cast(DATEDIFF(DAY,t.cover_a,GETDATE()) as varchar)+' days'  
               else case when t.csid=2 and isnull(nositemsA,0)>0 and isnull(scT.nosTEch,0)=0 and isnull(scF.nosFIN,0)>0  then 'Cov-A Finalcial Evaluation Pending Since '+cast(DATEDIFF(DAY,t.cover_a,GETDATE()) as varchar)+' days'  
               else case when t.csid=2 and isnull(nositemsA,0)>0 and isnull(scT.nosTEch,0)=0 and isnull(scF.nosFIN,0)=0 and isnull(nosPrep,0)>0   then 'Cov-A Final Summary Sheet Upload Pending' 
               else case when t.csid=7 and getdate()<t.ObjCEndDT  then 'Cov-A Claim Objection Time Valid' 
               else case when t.csid=7 and getdate()>t.ObjCEndDT  then 'Cov-A Claim Objection Closed Since '+cast(DATEDIFF(DAY,t.ObjCEndDT,GETDATE()) as varchar)+' days'
               else case when t.csid=3 and t.cover_b is not null  then 'Cov-B Since '+cast(DATEDIFF(DAY,t.cover_b,GETDATE()) as varchar)+' days'
               else case when t.csid=5 and t.cover_c is not null and isnull(pr.nosPriceOpened,0)=0  then 'Cov-C,Price Entry Pending since  '+cast(DATEDIFF(DAY,t.cover_c,GETDATE()) as varchar)+' days'           

               else ''
               end end end end end end end end end end   as FinalStatus,
               isnull(pr.nosPriceOpened,0) noofPriceFound,
			   isnull(acc.nosAccepted,0) as nosAccepted,
		        isnull(rj.nosRejected,0) as nosRejected
			  ,isnull(rc.nosRC,0) as nosRC,
			   c.CStatus
                from tenders t
               left outer join MasCoverStatus c on c.CSID=t.csid
               left outer join 
               (
               select ti.tender_id,count(distinct tender_item_id) as nosItems  from tender_items ti
               group by ti.tender_id
               ) ti on ti.tender_id=t.tender_id
               left outer join 
               (
               select sc.SCHEMEID,count(distinct sc.SUPPLIERID) as nossupplier,count(distinct sch.ITEMID) nositemsA from masschemesstatusdetails sc
               inner join SCHEMESTATUSDETAILSCHILD sch on sch.SCHSTATUSDID=sc.SCHSTATUSDID
               inner join tenders t on t.tender_id=sc.SCHEMEID
               group by sc.SCHEMEID
               ) sc on sc.SCHEMEID=t.tender_id
               
               left outer join 
               (
               select sc.SCHEMEID,count(distinct sch.ITEMID) nosTEch from masschemesstatusdetails sc
               inner join SCHEMESTATUSDETAILSCHILD sch on sch.SCHSTATUSDID=sc.SCHSTATUSDID
               inner join tenders t on t.tender_id=sc.SCHEMEID
               where 1=1 and sc.ISCovTechEli is null
               group by sc.SCHEMEID
               ) scT on scT.SCHEMEID=t.tender_id
               
               left outer join 
               (
               select sc.SCHEMEID,count(distinct sch.ITEMID) nosFIN from masschemesstatusdetails sc
               inner join SCHEMESTATUSDETAILSCHILD sch on sch.SCHSTATUSDID=sc.SCHSTATUSDID
               inner join tenders t on t.tender_id=sc.SCHEMEID
               where 1=1 and sc.IsCOVFinEli is null
               group by sc.SCHEMEID
               ) scF on scF.SCHEMEID=t.tender_id
               
               left outer join 
               (
               select sc.SCHEMEID,count(distinct sch.ITEMID) nosPrep from masschemesstatusdetails sc
               inner join SCHEMESTATUSDETAILSCHILD sch on sch.SCHSTATUSDID=sc.SCHSTATUSDID
               inner join tenders t on t.tender_id=sc.SCHEMEID
               where 1=1 and sc.IsCOVFinEli is not null and sc.ISCovTechEli is not null and t.ObjCEndDT is null
               group by sc.SCHEMEID
               ) scP on scP.SCHEMEID=t.tender_id
               
               left outer join 
               (
               select sc.SCHEMEID,count(distinct sc.SUPPLIERID) as nossupplierDA,count(distinct sch.ITEMID) nositemsDA from masschemesstatusdetails sc
               inner join SCHEMESTATUSDETAILSCHILD sch on sch.SCHSTATUSDID=sc.SCHSTATUSDID
               inner join tenders t on t.tender_id=sc.SCHEMEID
               where  sch.FLAGCOB='Y' and sc.ISELIGIBLE_B='Y'
               group by sc.SCHEMEID
               ) scB on scB.SCHEMEID=t.tender_id

			   left outer join 
			   (
			   select sch.SCHEMEID,count(distinct sch.ITEMID) nosPriceOpened  from SCHEMESTATUSDETAILSCHILD sch			    
			   inner join masschemesstatusdetails sc on sc.SCHSTATUSDID=sch.SCHSTATUSDID
			   inner join tenders t on t.tender_id=sc.SCHEMEID
			   left outer join live_tender_price tp on tp.ChildID=sch.ChildID
			    where t.csid=5 and sch.FLAGCOB='Y' and sc.ISELIGIBLE_B='Y' and FLAGCOC='Y'
				 group by sch.SCHEMEID
			   ) pr on pr.SCHEMEID=t.tender_id   
			   
			      left outer join 
			   (
			   select sch.SCHEMEID,count(distinct sch.ITEMID) nosAccepted  from SCHEMESTATUSDETAILSCHILD sch			    
			   inner join masschemesstatusdetails sc on sc.SCHSTATUSDID=sch.SCHSTATUSDID
			   inner join tenders t on t.tender_id=sc.SCHEMEID
			   left outer join live_tender_price tp on tp.ChildID=sch.ChildID
			    where t.csid=5 and sch.FLAGCOB='Y' and sc.ISELIGIBLE_B='Y' and FLAGCOC='Y'
				 and tp.isaccept is not null and tp.fdate is not null  
				
				 group by sch.SCHEMEID
			   ) acc on acc.SCHEMEID=t.tender_id  
			       left outer join 
			   (
			    select sch.SCHEMEID,count(distinct sch.ITEMID) nosRejected  from SCHEMESTATUSDETAILSCHILD sch			    
			   inner join masschemesstatusdetails sc on sc.SCHSTATUSDID=sch.SCHSTATUSDID
			   inner join tenders t on t.tender_id=sc.SCHEMEID
			   inner join tender_items ti on ti.tender_id=t.tender_id
			   left outer join live_tender_price tp on tp.ChildID=sch.ChildID
			    where t.csid=5 and sch.FLAGCOB='Y' and sc.ISELIGIBLE_B='Y' and FLAGCOC='Y'
				 and tp.isaccept='N' and ti.rejectdate is not null
				 group by sch.SCHEMEID
				) rj on rj.SCHEMEID=t.tender_id  

				   left outer join 
			   (
			   select count(distinct ci.item_id) nosRC,c.tender_id from contract_items ci 
			   inner join award_of_contract c on c.award_of_contract_id=ci.award_of_contract_id
			   group by c.tender_id
			   ) rc on rc.tender_id=t.tender_id

              where 1=1  
			   ) a on a.tender_id=b.tender_id
    
 where 1=1   and b.tender_id=@TId ORDER BY sc.OrderID";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@TId", tenderId);
                        await conn.OpenAsync();

                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            while (await dr.ReadAsync())
                            {
                                //resultList.Add(new AddTenderStatusDTO
                                //{
                                //    FinancialYearId = Convert.ToInt32(dr["financial_year_id"]),
                                //    TenderNo = dr["tender_no"].ToString(),
                                //    LiveDT = dr["LiveDT"].ToString(),
                                //    TLast = dr["TLast"].ToString(),
                                //    NoSitems = Convert.ToInt32(dr["NoSitems"]),
                                //    FinalStatus = dr["FinalStatus"].ToString(),
                                //    NosItemsA = dr["nosItemsA"].ToString(),
                                //    AItemsSupplier = dr["AItemsSupplier"].ToString(),
                                //    CoverA = dr["cover_a"].ToString(),
                                //    COVA_LastDays = Convert.ToInt32(dr["COVA_LastDays"]),
                                //    NositemsDA = dr["nositemsDA"].ToString(),
                                //    ADAItemsSupplier = dr["ADAItemsSupplier"].ToString(),
                                //    ObjClaimLastDate = dr["ObjClaimLastDate"].ToString(),
                                //    ObjCStartDT = dr["ObjCStartDT"].ToString(),
                                //    COVBDT = dr["COVBDT"].ToString(),
                                //    COVCDT = dr["COVCDT"].ToString(),
                                //    NoofPriceFound = Convert.ToInt32(dr["noofPriceFound"]),
                                //    NosAccepted = Convert.ToInt32(dr["nosAccepted"]),
                                //    NosRejected = Convert.ToInt32(dr["nosRejected"]),
                                //    DaysTakenFromLiveDT = Convert.ToInt32(dr["DaysTakenFromLiveDT"]),
                                //    COVAToObjStartDays = Convert.ToInt32(dr["COVAToObjStartDays"]),
                                //    OBJDays = Convert.ToInt32(dr["OBJDays"]),
                                //    ClaimEndToBDays = Convert.ToInt32(dr["ClaimEndToBDays"]),
                                //    CovBToCovCDays = Convert.ToInt32(dr["CovBToCovCDays"]),
                                //    Csid = Convert.ToInt32(dr["csid"]),
                                //    TenderId = Convert.ToInt32(dr["tender_id"]),
                                //    DaysClosed = Convert.ToInt32(dr["daysClosed"]),
                                //    Show = Convert.ToInt32(dr["Show"]),
                                //    NosRC = Convert.ToInt32(dr["nosRC"])
                                //});
                                resultList.Add(new AddTenderStatusDTO
                                {
                                    FinancialYearId = dr["financial_year_id"] == DBNull.Value ? 0 : Convert.ToInt32(dr["financial_year_id"]),
                                    TenderNo = dr["tender_no"]?.ToString() ?? "",
                                    LiveDT = dr["LiveDT"]?.ToString() ?? "",
                                    TLast = dr["TLast"]?.ToString() ?? "",

                                    // Numbers ke liye handle karein
                                    NoSitems = dr["NoSitems"] == DBNull.Value ? 0 : Convert.ToInt32(dr["NoSitems"]),
                                    FinalStatus = dr["FinalStatus"]?.ToString() ?? "N/A",

                                    // Decimal/Amounts ke liye
                                    NosAccepted = dr["nosAccepted"] == DBNull.Value ? 0 : Convert.ToInt32(dr["nosAccepted"]),
                                    NosRejected = dr["nosRejected"] == DBNull.Value ? 0 : Convert.ToInt32(dr["nosRejected"]),
                                    NoofPriceFound = dr["noofPriceFound"] == DBNull.Value ? 0 : Convert.ToInt32(dr["noofPriceFound"]),

                                    // DATEDIFF wale fields
                                    DaysTakenFromLiveDT = dr["DaysTakenFromLiveDT"] == DBNull.Value ? 0 : Convert.ToInt32(dr["DaysTakenFromLiveDT"]),

                                    // Baki saare fields ke liye bhi yahi format use karein...
                                    Csid = dr["csid"] == DBNull.Value ? 0 : Convert.ToInt32(dr["csid"]),
                                    TenderId = dr["tender_id"] == DBNull.Value ? 0 : Convert.ToInt32(dr["tender_id"]),
                                    NosRC = dr["nosRC"] == DBNull.Value ? 0 : Convert.ToInt32(dr["nosRC"])
                                });
                            }
                        }
                    }
                }
                return Ok(resultList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Query Execution Error", details = ex.Message });
            }
        }



            [HttpGet("GetFacilityList")]
            public async Task<IActionResult> GetFacilityList()
            {
                var facilities = new List<FacilityAuthDTOnew>();

                try
                {
                    using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                    {
                        // Aapki Query: null check aur ordering ke sath
                        string sql = @"SELECT facility_aut_id, facility_aut_name, facility_aut_code 
                               FROM facility_aut 
                               WHERE ordercase IS NOT NULL 
                               ORDER BY ordercase";

                        using (SqlCommand cmd = new SqlCommand(sql, conn))
                        {
                            await conn.OpenAsync();
                            using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                            {
                                while (await dr.ReadAsync())
                                {
                                    facilities.Add(new FacilityAuthDTOnew
                                    {
                                        FacilityAutId = Convert.ToInt32(dr["facility_aut_id"]),
                                        FacilityAutName = dr["facility_aut_name"]?.ToString() ?? "",
                                        FacilityAutCode = dr["facility_aut_code"]?.ToString() ?? ""
                                    });
                                }
                            }
                        }
                    }
                    return Ok(facilities);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { message = "Error fetching facilities", details = ex.Message });
                }
            }

        [HttpGet("GetHodConversations/{tenderId}")]
        public async Task<IActionResult> GetHodConversations(int tenderId)
        {
            var conversations = new List<HodConversationDTO>();
            string connString = _config.GetConnectionString("DefaultConnection");

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    string sql = @"SELECT t.tender_id AS SCHEMEID, s.tender_no AS SCHEMENAME, 
                                  ft.facility_aut_code AS FACILITYTYPECODE, t.letterno, 
                                  CONVERT(VARCHAR, t.letterdate, 103) AS letterdate,
                                  t.remarks, CONVERT(VARCHAR, t.senddate, 103) AS senddate, 
                                  t.entrydate, t.filename, t.filepath, t.convid 
                           FROM TBHODCONVERSATION t
                           INNER JOIN tenders s ON s.tender_id = t.tender_id
                           INNER JOIN facility_aut ft ON ft.facility_aut_id = t.hodid
                           WHERE s.tender_id = @TId 
                           ORDER BY t.convid DESC";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@TId", tenderId);
                        await conn.OpenAsync();

                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            while (await dr.ReadAsync())
                            {
                                conversations.Add(new HodConversationDTO
                                {
                                    SCHEMEID = Convert.ToInt32(dr["SCHEMEID"]),
                                    SCHEMENAME = dr["SCHEMENAME"]?.ToString(),
                                    FACILITYTYPECODE = dr["FACILITYTYPECODE"]?.ToString(),
                                    LetterNo = dr["letterno"]?.ToString(),
                                    LetterDate = dr["letterdate"]?.ToString(),
                                    Remarks = dr["remarks"]?.ToString(),
                                    SendDate = dr["senddate"]?.ToString(),
                                    EntryDate = dr["entrydate"] == DBNull.Value ? null : Convert.ToDateTime(dr["entrydate"]),
                                    FileName = dr["filename"]?.ToString(),
                                    FilePath = dr["filepath"]?.ToString(),
                                    Convid = Convert.ToInt32(dr["convid"])
                                });
                            }
                        }
                    }
                }
                return Ok(conversations);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching conversation data", details = ex.Message });
            }
        }



        [HttpPost("SaveHodConversation")]
        public async Task<IActionResult> SaveHodConversation([FromForm] HodConversationSaveRequest request)
        {
            // 1. File Validations
            if (request.File == null || request.File.Length == 0)
                return BadRequest(new { message = "Please select a file to be uploaded." });

            string extension = Path.GetExtension(request.File.FileName).ToLower();
            if (extension != ".pdf")
                return BadRequest(new { message = "Please upload PDF file only." });

            if (request.File.Length > 2000000) // 2MB Check
                return BadRequest(new { message = "You can't upload file more than 2MB." });

            string connString = _config.GetConnectionString("DefaultConnection");

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    await conn.OpenAsync();

                    using (SqlTransaction trans = conn.BeginTransaction())
                    {
                        try
                        {
                            // 2. Insert Data
                            // Note: Scope_Identity() ya Output Inserted use karna zyada safe hai MAX() se
                            string insertSql = @"INSERT INTO TBHODCONVERSATION (tender_id, HODID, sendDate, letterno, letterdate, remarks, entryby, entrydate) 
                                         VALUES (@Tid, @Hid, @SDate, @LNo, @LDate, @Rem, '01', GETDATE());
                                         SELECT SCOPE_IDENTITY();";

                            int newConvId;
                            using (SqlCommand cmd = new SqlCommand(insertSql, conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@Tid", request.TenderId);
                                cmd.Parameters.AddWithValue("@Hid", request.HodId);
                                cmd.Parameters.AddWithValue("@SDate", DateTime.Parse(request.SendDate));
                                cmd.Parameters.AddWithValue("@LNo", request.LetterNo);
                                cmd.Parameters.AddWithValue("@LDate", DateTime.Parse(request.LetterDate));
                                cmd.Parameters.AddWithValue("@Rem", request.Remarks);

                                // ExecuteScalar se direct nayi ID mil jayegi
                                newConvId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                            }

                            // 3. File Saving Logic
                            string fileName = $"Conv{request.TenderId}_{newConvId}.pdf";
                            string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/Tender/UploadConv");

                            if (!Directory.Exists(folderPath))
                                Directory.CreateDirectory(folderPath);

                            string filePath = Path.Combine(folderPath, fileName);
                            string dbFilePath = "~/Tender/UploadConv/" + fileName;

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await request.File.CopyToAsync(stream);
                            }

                            // 4. Update Filename and Path in DB
                            string updateSql = "UPDATE TBHODCONVERSATION SET filename = @Fname, filepath = @FPath WHERE convID = @Cid";
                            using (SqlCommand updateCmd = new SqlCommand(updateSql, conn, trans))
                            {
                                updateCmd.Parameters.AddWithValue("@Fname", fileName);
                                updateCmd.Parameters.AddWithValue("@FPath", dbFilePath);
                                updateCmd.Parameters.AddWithValue("@Cid", newConvId);
                                await updateCmd.ExecuteNonQueryAsync();
                            }

                            trans.Commit();
                            return Ok(new { message = "Save Successfully.", convId = newConvId });
                        }
                        catch (Exception ex)
                        {
                            trans.Rollback();
                            return StatusCode(500, new { message = "Transaction Error", details = ex.Message });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Database Connection Error", details = ex.Message });
            }
        }


        [HttpPost("SaveHodReply")]
        public async Task<IActionResult> SaveHodReply([FromForm] HodReplyRequest request)
        {
            // 1. File Validation
            if (request.File == null || request.File.Length == 0)
                return BadRequest(new { message = "Please select a file to be uploaded." });

            string extension = Path.GetExtension(request.File.FileName).ToLower();
            if (extension != ".pdf")
                return BadRequest(new { message = "Please upload PDF file only." });

            if (request.File.Length > 2000000) // 2MB check
                return BadRequest(new { message = "You can't upload file more than 2MB." });

            string connString = _config.GetConnectionString("DefaultConnection");

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    await conn.OpenAsync();
                    using (SqlTransaction trans = conn.BeginTransaction())
                    {
                        try
                        {
                            DateTime parsedRecvDate = DateTime.ParseExact(request.RecvDate, "yyyy-dd-MM", CultureInfo.InvariantCulture);
                            DateTime parsedLetterDate = DateTime.ParseExact(request.LetterDt, "yyyy-dd-MM", CultureInfo.InvariantCulture);

                          
                            // 2. INSERT Logic (TBHODCONVERSATIONREPLY)
                            string insertSql = @"INSERT INTO TBHODCONVERSATIONREPLY (CONVID, RecvDate, LetterNo, LetterDT, Remarks, entryby, entrydate) 
                                         VALUES (@Cid, @RDate, @LNo, @LDt, @Rem, '01', GETDATE())";

                            using (SqlCommand cmd = new SqlCommand(insertSql, conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@Cid", request.Convid);
                                cmd.Parameters.AddWithValue("@RDate", parsedRecvDate);
                                cmd.Parameters.AddWithValue("@LDt", parsedLetterDate);
                                //cmd.Parameters.AddWithValue("@RDate", DateTime.Parse(request.RecvDate));
                                //cmd.Parameters.AddWithValue("@LDt", DateTime.Parse(request.LetterDt));
                                cmd.Parameters.AddWithValue("@LNo", request.LetterNo);
                                cmd.Parameters.AddWithValue("@Rem", request.Remarks);
                                await cmd.ExecuteNonQueryAsync();
                            }
                            //cmd.Parameters.AddWithValue("@SDate", DateTime.Parse(request.SendDate));
                            //cmd.Parameters.AddWithValue("@LNo", request.LetterNo);
                            //cmd.Parameters.AddWithValue("@LDate", DateTime.Parse(request.LetterDate));
                            // 3. GetCONVReplyID Logic (Aapki query ke mutabiq)
                            string maxIdSql = "SELECT ISNULL(MAX(CONRID), 0) FROM TBHODCONVERSATIONREPLY WHERE CONVID = @Cid";
                            int conRid;
                            using (SqlCommand cmdMax = new SqlCommand(maxIdSql, conn, trans))
                            {
                                cmdMax.Parameters.AddWithValue("@Cid", request.Convid);
                                conRid = Convert.ToInt32(await cmdMax.ExecuteScalarAsync());
                            }

                            // 4. File Saving Logic
                            string fileName = $"ConReply{request.Convid}_{conRid}.pdf";
                            string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/Tender/UploadConv");

                            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                            string filePath = Path.Combine(folderPath, fileName);
                            string dbSavePath = "~/Tender/UploadConv/" + fileName;

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await request.File.CopyToAsync(stream);
                            }

                            // 5. Update FileName and FilePath in DB
                            string updateSql = "UPDATE TBHODCONVERSATIONREPLY SET fileName = @Fname, FilePath = @FPath WHERE CONRID = @CRid";
                            using (SqlCommand cmdUpd = new SqlCommand(updateSql, conn, trans))
                            {
                                cmdUpd.Parameters.AddWithValue("@Fname", fileName);
                                cmdUpd.Parameters.AddWithValue("@FPath", dbSavePath);
                                cmdUpd.Parameters.AddWithValue("@CRid", conRid);
                                await cmdUpd.ExecuteNonQueryAsync();
                            }

                            trans.Commit();
                            return Ok(new { message = "Save Successfully.", conRid = conRid });
                        }
                        catch (Exception ex)
                        {
                            trans.Rollback();
                            return StatusCode(500, new { message = "Error during saving", details = ex.Message });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Database error", details = ex.Message });
            }
        }

        [HttpGet("GetHodReplyList/{convId}")]
        public async Task<IActionResult> GetHodReplyList(int convId)
        {
            var replies = new List<HodReplyDTO>();
            string connString = _config.GetConnectionString("DefaultConnection");

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    // Aapki exact query with parameters
                    string sql = @"SELECT CONRID, CONVID, CONVERT(VARCHAR, RecvDate, 103) AS RecvDate, 
                                  LetterNo, CONVERT(VARCHAR, LetterDT, 103) AS LetterDT, 
                                  Remarks, FileName, FilePath, entryby, entrydate 
                           FROM TBHODCONVERSATIONREPLY 
                           WHERE CONVID = @CId 
                           ORDER BY entrydate DESC";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@CId", convId);
                        await conn.OpenAsync();

                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            while (await dr.ReadAsync())
                            {
                                replies.Add(new HodReplyDTO
                                {
                                    CONRID = Convert.ToInt32(dr["CONRID"]),
                                    CONVID = Convert.ToInt32(dr["CONVID"]),
                                    RecvDate = dr["RecvDate"]?.ToString(),
                                    LetterNo = dr["LetterNo"]?.ToString(),
                                    LetterDT = dr["LetterDT"]?.ToString(),
                                    Remarks = dr["Remarks"]?.ToString(),
                                    FileName = dr["FileName"]?.ToString(),
                                    FilePath = dr["FilePath"]?.ToString(),
                                    EntryBy = dr["entryby"]?.ToString(),
                                    EntryDate = dr["entrydate"] == DBNull.Value ? null : Convert.ToDateTime(dr["entrydate"])
                                });
                            }
                        }
                    }
                }
                return Ok(replies);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching replies", details = ex.Message });
            }
        }


        [HttpGet("GetEligiblity")]
        public async Task<IActionResult> GetEligiblity()
        {
            List<EligiblityDTO> list = new List<EligiblityDTO>();

            string query = @"select ID,Eligiblity from masEligiblity order by ID";

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    SqlCommand cmd = new SqlCommand(query, conn);
                    await conn.OpenAsync();

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            EligiblityDTO data = new EligiblityDTO
                            {
                                id = reader["ID"] == DBNull.Value ? string.Empty : reader["ID"].ToString(),

                                Eligiblity = reader["Eligiblity"] == DBNull.Value ? null : reader["Eligiblity"].ToString()
                            };

                            list.Add(data);
                        }
                    }
                }
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPost("UpdateEligibility")]
        public async Task<IActionResult> UpdateEligibility([FromBody] EligibilityUpdateRequest request)
        {
            // 1. Validations (Same as WebForms logic)
            if (request.Eligibility == "0" || string.IsNullOrEmpty(request.Eligibility))
                return BadRequest(new { message = "Please select a valid eligibility status." });

            if (request.Eligibility == "N" && string.IsNullOrEmpty(request.Remarks))
                return BadRequest(new { message = "Please Enter Rejection Remark for Not Eligible." });

            if (request.Eligibility == "C" && string.IsNullOrEmpty(request.Remarks))
                return BadRequest(new { message = "Please Enter Clarification Remarks." });

            string connString = _config.GetConnectionString("DefaultConnection");

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    // 2. Role based Dynamic Column Logic
                    string techFinEval = "";
                    if (request.RoleId == "22" || request.RoleId == "12")
                    {
                        techFinEval = @"ISCovTechEli = @Eli, 
                                CovATechRemarksBefore_OBClM = @Remarks, 
                                TechCOVAEntryDT = GETDATE(), 
                                TechUserid = @UId";
                    }
                    else
                    {
                        techFinEval = @"IsCOVFinEli = @Eli, 
                                CovAFINRemarksBefore_OBClM = @Remarks, 
                                FINCOVAEntryDT = GETDATE(), 
                                FinUserid = @UId";
                    }

                    string updateSql = $@"UPDATE masschemesstatusdetails 
                                 SET {techFinEval} 
                                 WHERE SCHSTATUSDID = @StatusId AND schemeid = @SId";

                    using (SqlCommand cmd = new SqlCommand(updateSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Eli", request.Eligibility);
                        cmd.Parameters.AddWithValue("@Remarks", request.Remarks ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@UId", request.UserId);
                        cmd.Parameters.AddWithValue("@StatusId", request.SchStatusDid);
                        cmd.Parameters.AddWithValue("@SId", request.SchemeId);

                        await conn.OpenAsync();
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                            return Ok(new { message = "Updated Successfully" });
                        else
                            return NotFound(new { message = "Record not found to update." });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error during update", details = ex.Message });
            }
        }



        [HttpGet("GetTenderDetails/{tenderId}")]
        public async Task<IActionResult> GetTenderDetails(int tenderId)
        {
          
            TenderDetailsDTO tenderDetails = null;
            string connString = _config.GetConnectionString("DefaultConnection");

            // Aapki exact query with parameters (@Tid)
            string sql = @"SELECT A.TENDER_NO, B.YEAR AS FINANCIAL_YEAR, A.domestic_days, A.import_days, A.warranty_year,
                          CONVERT(VARCHAR(10), A.TENDER_DATE, 103) AS TENDER_DATE, A.TENDER_DESCRIPTION, A.FLAG, 
                          A.FINANCIAL_YEAR_ID, A.tender_id, CONVERT(VARCHAR(10), A.cover_a, 103) AS cover_a, 
                          CONVERT(VARCHAR(10), A.cover_b, 103) AS cover_b, CONVERT(VARCHAR(10), A.cover_Demo, 103) AS cover_Demo,
                          CONVERT(VARCHAR(10), A.cover_c, 103) AS cover_c
                   FROM TENDERS A
                   LEFT OUTER JOIN MAS_FINANCIAL_YEAR B ON (A.FINANCIAL_YEAR_ID = B.FINANCIAL_YEAR_ID)
                   WHERE A.TENDER_ID = @Tid";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Tid", tenderId);
                        await conn.OpenAsync();

                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            if (await dr.ReadAsync()) 
                            {
                                tenderDetails = new TenderDetailsDTO
                                {
                                    TenderNo = dr["TENDER_NO"]?.ToString(),
                                    FinancialYear = dr["FINANCIAL_YEAR"]?.ToString(),
                                    DomesticDays = dr["domestic_days"] == DBNull.Value ? null : (int?)Convert.ToInt32(dr["domestic_days"]),
                                    ImportDays = dr["import_days"] == DBNull.Value ? null : (int?)Convert.ToInt32(dr["import_days"]),
                                    WarrantyYear = dr["warranty_year"] == DBNull.Value ? null : (int?)Convert.ToInt32(dr["warranty_year"]),
                                    TenderDate = dr["TENDER_DATE"]?.ToString(),
                                    TenderDescription = dr["TENDER_DESCRIPTION"]?.ToString(),
                                    Flag = dr["FLAG"]?.ToString(),
                                    FinancialYearId = dr["FINANCIAL_YEAR_ID"] == DBNull.Value ? null : (int?)Convert.ToInt32(dr["FINANCIAL_YEAR_ID"]),
                                    TenderId = Convert.ToInt32(dr["tender_id"]),
                                    CoverA = dr["cover_a"]?.ToString(),
                                    CoverB = dr["cover_b"]?.ToString(),
                                    CoverDemo = dr["cover_Demo"]?.ToString(),
                                    CoverC = dr["cover_c"]?.ToString()
                                };
                            }
                        }
                    }
                }

                if (tenderDetails == null)
                {
                    return NotFound(new { message = $"Tender details not found for ID: {tenderId}" });
                }

                return Ok(tenderDetails);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching tender details", details = ex.Message });
            }
        }


        [HttpGet("GetLiveTenderPrice/{tenderItemId}")]
        public async Task<IActionResult> GetLiveTenderPrice(int tenderItemId)
        {
            var list = new List<LiveTenderPriceDTO>();
            string connString = _config.GetConnectionString("DefaultConnection");

            // Parameterized SQL Query (@TItemId)
            string sql = @"SELECT 0 AS SlNo, tppriceid AS tpriceid, t.tender_item_id AS TID, t.basicrate, t.GST, 
                          t.supplier_id AS supplierid, t.tpricestatus, s.name AS suppliername, 
                          t.CMC1, t.CMC2, t.CMC3, t.CMC4, t.CMC5, t.filePathReagent, t.filePathAccessories, 
                          t.fbasicrate, CONVERT(VARCHAR(10), t.fdate, 103) AS fdate, t.isaccept
                   FROM live_tender_price t 
                   INNER JOIN massuppliers s ON s.supplier_id = t.supplier_id
                   WHERE t.tender_item_id = @TItemId";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@TItemId", tenderItemId);
                        await conn.OpenAsync();

                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            int counter = 1;
                            while (await dr.ReadAsync())
                            {
                                list.Add(new LiveTenderPriceDTO
                                {
                                    SlNo = counter++, // Frontend row index ke liye auto-increment
                                    TPriceId = Convert.ToInt32(dr["tpriceid"]),
                                    Tid = Convert.ToInt32(dr["TID"]),
                                    BasicRate = dr["basicrate"] == DBNull.Value ? null : (decimal?)Convert.ToDecimal(dr["basicrate"]),
                                    Gst = dr["GST"] == DBNull.Value ? null : (decimal?)Convert.ToDecimal(dr["GST"]),
                                    SupplierId = Convert.ToInt32(dr["supplierid"]),
                                    TPriceStatus = dr["tpricestatus"]?.ToString() ?? "",
                                    SupplierName = dr["suppliername"]?.ToString() ?? "",
                                    Cmc1 = dr["CMC1"] == DBNull.Value ? null : (decimal?)Convert.ToDecimal(dr["CMC1"]),
                                    Cmc2 = dr["CMC2"] == DBNull.Value ? null : (decimal?)Convert.ToDecimal(dr["CMC2"]),
                                    Cmc3 = dr["CMC3"] == DBNull.Value ? null : (decimal?)Convert.ToDecimal(dr["CMC3"]),
                                    Cmc4 = dr["CMC4"] == DBNull.Value ? null : (decimal?)Convert.ToDecimal(dr["CMC4"]),
                                    Cmc5 = dr["CMC5"] == DBNull.Value ? null : (decimal?)Convert.ToDecimal(dr["CMC5"]),
                                    FilePathReagent = dr["filePathReagent"]?.ToString() ?? "",
                                    FilePathAccessories = dr["filePathAccessories"]?.ToString() ?? "",
                                    FBasicRate = dr["fbasicrate"] == DBNull.Value ? null : (decimal?)Convert.ToDecimal(dr["fbasicrate"]),
                                    FDate = dr["fdate"]?.ToString() ?? "",
                                    IsAccept = dr["isaccept"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching live tender price data", error = ex.Message });
            }
        }




    }
}