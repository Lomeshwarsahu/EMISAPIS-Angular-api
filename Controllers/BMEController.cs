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
                SELECT SCOPE_IDENTITY();"; // यह लाइन तुरंत नई ID वापस देती है

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



    }
}