using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver.Core.Configuration;
using System.Data;
using System.Diagnostics.Eventing.Reader;
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

            // 1. खाली स्ट्रिंग "" या " " को सुरक्षित रूप से int में बदलना
            // अगर वैल्यू नहीं मिली या खाली मिली, तो यह डिफ़ॉल्ट रूप से 0 ले लेगा
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
                                    // TaxId (int) है, इसलिए Convert.ToInt32 और डिफ़ॉल्ट 0 लगेगा
                                    TaxId = reader["tax_type_id"] != DBNull.Value ? Convert.ToInt32(reader["tax_type_id"]) : 0,

                                    // Taxname (string) है, इसलिए Convert.ToString और डिफ़ॉल्ट खाली स्ट्रिंग "" लगेगा
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
                                        // लिस्ट में हमें रेट्स की ज़रूरत नहीं है, इसलिए वे डिफ़ॉल्ट 0 रहेंगे
                                    });
                                }
                            }
                        }
                        return Ok(itemList); // Array/List वापस करेगा
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching details", error = ex.Message });
            }
        }



        // API 2: Add New Item in Contract (पुराना btnSave_Click)
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
            // 1. Validation (चेक करें कि ID सही है या नहीं)
            if (item.ContractItemId <= 0)
            {
                return BadRequest(new { message = "Please provide a valid Contract Item ID." });
            }

            // 2. Update Query
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
                        // 3. Parameters असाइन करें
                        cmd.Parameters.AddWithValue("@contract_item_id", item.ContractItemId);
                        cmd.Parameters.AddWithValue("@no_of_days_for_supply", item.NoOfDaysForSupply);
                        cmd.Parameters.AddWithValue("@basic_rate", item.BasicRate);
                        cmd.Parameters.AddWithValue("@tax_type_id", item.TaxTypeId);
                        cmd.Parameters.AddWithValue("@percentage", item.Percentage);
                        cmd.Parameters.AddWithValue("@single_unit_price", item.SingleUnitPrice);

                        // Strings के लिए DBNull हैंडलिंग (ताकि अगर खाली आए तो क्रैश न हो)
                        cmd.Parameters.AddWithValue("@licence_number", string.IsNullOrWhiteSpace(item.LicenceNumber) ? (object)DBNull.Value : item.LicenceNumber);
                        cmd.Parameters.AddWithValue("@make", string.IsNullOrWhiteSpace(item.Make) ? (object)DBNull.Value : item.Make);
                        cmd.Parameters.AddWithValue("@model", string.IsNullOrWhiteSpace(item.Model) ? (object)DBNull.Value : item.Model);

                        await conn.OpenAsync();
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            return Ok(new { message = "RC Updated successfully" }); // आपका पुराना मैसेज
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

            // 2. Date Calculation (महीने जोड़कर End Date निकालना)
            DateTime signDate;
            if (!DateTime.TryParse(data.ContractSignDate, out signDate))
            {
                return BadRequest(new { message = "Invalid Sign Date format. Use yyyy-MM-dd." });
            }

            // पुराने कोड का लॉजिक: Start Date + Duration = End Date
            DateTime endDate = signDate.AddMonths(data.ContractDuration);

            // 3. Update Query (SQL Injection से सुरक्षित)
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
                        // 4. Parameters असाइन करें
                        cmd.Parameters.AddWithValue("@Duration", data.ContractDuration.ToString());
                        cmd.Parameters.AddWithValue("@SignDate", signDate);
                        cmd.Parameters.AddWithValue("@EndDate", endDate);
                        cmd.Parameters.AddWithValue("@AocId", data.AwardOfContractId);

                        await conn.OpenAsync();
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            return Ok(new { message = "RC Successfully Completed" }); // आपका पुराना मैसेज
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

            // 1. Base Query (इसमें WHERE का आखिरी हिस्सा छोड़ दिया है)
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

            // 2. Dynamic Condition (चेक के आधार पर क्वेरी में आगे की लाइन जोड़ें)
            if (check == 0) // अगर ड्रॉपडाउन में 'All' या कोई डिफॉल्ट वैल्यू (0) सेलेक्ट हुई हो
            {
                query += " AND isnull(s.csid,0) not in (6)";
            }
            else // अगर कोई विशेष स्टेटस सेलेक्ट हुआ हो (जैसे 1, 2, 3...)
            {
                query += " AND s.csid = @StatusCheck";
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        // 3. Parameters पास करें
                        cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId);

                        // अगर check 0 नहीं है, तो हमें @StatusCheck पैरामीटर भी पास करना होगा
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









    }
}