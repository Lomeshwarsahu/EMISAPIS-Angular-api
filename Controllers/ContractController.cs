using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EMISAPIS.Controllers
{
   
    [ApiController]
    [Route("api/[controller]")]
    public class ContractController : ControllerBase
    {
        private readonly IConfiguration _config;

        public ContractController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet("GetConTenterlist")]
        public async Task<IActionResult> GetConTenterlist()
        {
            List<ConTenterlistDTO> list = new List<ConTenterlistDTO>();

            string query = @"select distinct t.tender_no,t.tender_id
from  contract_items c
inner join  masitems m on m.item_id= c.item_id
inner join award_of_contract ac on ac.award_of_contract_id=c.award_of_contract_id
inner join massuppliers s on s.supplier_id=ac.supplier_id
inner join tenders t on t.tender_id=ac.tender_id
where GETDATE() between ac.contract_date and ac.contract_end_date";

            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                SqlCommand cmd = new SqlCommand(query, conn);

                await conn.OpenAsync();

                SqlDataReader reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    ConTenterlistDTO data = new ConTenterlistDTO
                    {
                        tender_id = reader["tender_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["tender_id"]),
                        tender_no = reader["tender_no"] == DBNull.Value ? null : reader["tender_no"].ToString()
                    };

                    list.Add(data);
                }
            }

            return Ok(list);
        }





        [HttpGet("GetRcDetailReport")]
        public async Task<IActionResult> GetRcDetailReport(
            [FromQuery] RcDetailReportRequestDTO req)
        {
            List<RcDetailReportDTO> list = new List<RcDetailReportDTO>();

            try
            {
                string whereTender = req.TenderId.HasValue
                    ? " AND t.tender_id = @TenderId"
                    : "";

                string whereCategory = req.CategoryId == 2
                    ? " AND m.categoryid = 2"
                    : " AND (m.categoryid = 1 OR m.categoryid IS NULL)";

                string whereRcType = req.RcType == "R"
                    ? " AND GETDATE() BETWEEN ac.contract_date AND ac.contract_end_date"
                    : " AND GETDATE() > ac.contract_end_date";

                string query = @"SELECT  
    c.contract_item_id,
    m.item_id,
    m.item_code_as_per_tender AS item_codeE,
    m.item_name AS item_nameE,
    c.make,
    c.model,
    s.name AS SupplierName,
    t.tender_no,
    CONVERT(VARCHAR, ac.contract_date, 103) AS contract_date,
    CONVERT(VARCHAR, 
        CASE 
            WHEN c.contract_new_end_date IS NOT NULL 
            THEN c.contract_new_end_date 
            ELSE ac.contract_end_date 
        END, 103) AS contract_end_date,
    c.basic_rate,
    c.percentage AS GST,
    c.single_unit_price,
    ISNULL(tt.CMC1,0) CMC1,
    ISNULL(tt.CMC2,0) CMC2,
    ISNULL(tt.CMC3,0) CMC3,
    ISNULL(tt.CMC4,0) CMC4,
    ISNULL(tt.CMC5,0) CMC5,
    t.tender_id
FROM contract_items c
INNER JOIN masitems m ON m.item_id = c.item_id
INNER JOIN award_of_contract ac ON ac.award_of_contract_id = c.award_of_contract_id
INNER JOIN massuppliers s ON s.supplier_id = ac.supplier_id
INNER JOIN tenders t ON t.tender_id = ac.tender_id
LEFT JOIN (
    SELECT ti.item_id, ti.tender_id, ltp.supplier_id,
           ltp.CMC1, ltp.CMC2, ltp.CMC3, ltp.CMC4, ltp.CMC5
    FROM tender_items ti
    INNER JOIN live_tender_price ltp 
        ON ltp.tender_item_id = ti.tender_item_id
) tt 
ON tt.tender_id = t.tender_id 
AND tt.supplier_id = s.supplier_id 
AND tt.item_id = c.item_id
WHERE c.isfreezed IS NULL
" + whereTender + whereCategory + whereRcType + @"
ORDER BY ac.contract_date DESC";

                using (SqlConnection conn =
                    new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.CommandType = System.Data.CommandType.Text;

                        if (req.TenderId.HasValue)
                            cmd.Parameters.AddWithValue("@TenderId", req.TenderId);

                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                list.Add(new RcDetailReportDTO
                                {
                                    ContractItemId = Convert.ToInt32(reader["contract_item_id"]),
                                    ItemId = Convert.ToInt32(reader["item_id"]),
                                    ItemCode = reader["item_codeE"].ToString(),
                                    ItemName = reader["item_nameE"].ToString(),
                                    Make = reader["make"].ToString(),
                                    Model = reader["model"].ToString(),
                                    SupplierName = reader["SupplierName"].ToString(),
                                    TenderNo = reader["tender_no"].ToString(),
                                    ContractDate = reader["contract_date"].ToString(),
                                    ContractEndDate = reader["contract_end_date"].ToString(),
                                    BasicRate = Convert.ToDecimal(reader["basic_rate"]),
                                    GST = Convert.ToDecimal(reader["GST"]),
                                    SingleUnitPrice = Convert.ToDecimal(reader["single_unit_price"]),
                                    CMC1 = Convert.ToDecimal(reader["CMC1"]),
                                    CMC2 = Convert.ToDecimal(reader["CMC2"]),
                                    CMC3 = Convert.ToDecimal(reader["CMC3"]),
                                    CMC4 = Convert.ToDecimal(reader["CMC4"]),
                                    CMC5 = Convert.ToDecimal(reader["CMC5"]),
                                    TenderId = Convert.ToInt32(reader["tender_id"])
                                });
                            }
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
        
        
        //AcceptedAtActionResult Report 


        [HttpGet("GetAccTenterlist")]
        public async Task<IActionResult> GetAccTenterlist()
        {
            List<ConTenterlistDTO> list = new List<ConTenterlistDTO>();

            string query = @"select distinct  t.tender_no ,t.tender_id
from live_tender_price tp 
inner join tender_items ti on ti.tender_item_id =tp.tender_item_id
inner join tenders t on t.tender_id=ti.tender_id
inner join massuppliers s on s.supplier_id=tp.supplier_id
inner join masitems m on m.item_id=ti.item_id
where tp.isaccept='Y' and m.item_id not in (SELECT DISTINCT m.item_id
FROM            AWARD_OF_CONTRACT d1 INNER JOIN
                 CONTRACT_ITEMS w1 ON (w1.AWARD_OF_CONTRACT_ID = d1.AWARD_OF_CONTRACT_ID) INNER JOIN
                       masitems m ON m.item_id = w1.item_id
WHERE        getdate() BETWEEN d1.CONTRACT_SIGN_DATE AND d1.CONTRACT_END_DATE)";

            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                SqlCommand cmd = new SqlCommand(query, conn);

                await conn.OpenAsync();

                SqlDataReader reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    ConTenterlistDTO data = new ConTenterlistDTO
                    {
                        tender_id = reader["tender_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["tender_id"]),
                        tender_no = reader["tender_no"] == DBNull.Value ? null : reader["tender_no"].ToString()
                    };

                    list.Add(data);
                }
            }

            return Ok(list);
        }

        [HttpGet("GetAccSupplierlist")]
        public async Task<IActionResult> GetAccSupplierlist()
        {
            List<AccSupplierlistDTO> list = new List<AccSupplierlistDTO>();

            string query = @"select distinct  s.name, tp.supplier_id
from live_tender_price tp 
inner join tender_items ti on ti.tender_item_id =tp.tender_item_id
inner join tenders t on t.tender_id=ti.tender_id
inner join massuppliers s on s.supplier_id=tp.supplier_id
inner join masitems m on m.item_id=ti.item_id
where  1=1 and  tp.isaccept='Y' and m.item_id not in (SELECT DISTINCT m.item_id
FROM            AWARD_OF_CONTRACT d1 INNER JOIN
                 CONTRACT_ITEMS w1 ON (w1.AWARD_OF_CONTRACT_ID = d1.AWARD_OF_CONTRACT_ID) INNER JOIN
                       masitems m ON m.item_id = w1.item_id
WHERE        getdate() BETWEEN d1.CONTRACT_SIGN_DATE AND d1.CONTRACT_END_DATE)";

            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                SqlCommand cmd = new SqlCommand(query, conn);

                await conn.OpenAsync();

                SqlDataReader reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    AccSupplierlistDTO data = new AccSupplierlistDTO
                    {
                        supplier_id = reader["supplier_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["supplier_id"]),
                        name = reader["name"] == DBNull.Value ? null : reader["name"].ToString()
                    };

                    list.Add(data);
                }
            }

            return Ok(list);
        }


        [HttpGet("GetTenderSupplierData")]
        public async Task<IActionResult> GetTenderSupplierData(
         [FromQuery] TenderSupplierRequestDTO req)
        {
            List<TenderSupplierDTO> list = new List<TenderSupplierDTO>();

            try
            {
                string whereTender = "";
                string whereSupplier = "";

                if (req.FilterType == "tender" && req.TenderId.HasValue)
                {
                    whereTender = " AND t.tender_id = @TenderId ";
                }
                else if (req.FilterType == "supplier" && req.SupplierId.HasValue)
                {
                    whereSupplier = " AND tp.supplier_id = @SupplierId ";
                }

                string query = @"
SELECT 
    m.item_id,
    m.item_code_as_per_tender,
    m.item_name,
    s.name,
    t.tender_no,
    CONVERT(VARCHAR, t.tender_date, 103) AS tender_date,
    ti.tender_quantity,
    tp.basicrate,
    tp.gst,
    tp.fbasicrate AS acceptedBasicrate,
    CONVERT(VARCHAR, tp.fdate, 103) AS AcceptedDt,
    t.tender_id,
    tp.supplier_id
FROM live_tender_price tp
INNER JOIN tender_items ti ON ti.tender_item_id = tp.tender_item_id
INNER JOIN tenders t ON t.tender_id = ti.tender_id
INNER JOIN massuppliers s ON s.supplier_id = tp.supplier_id
INNER JOIN masitems m ON m.item_id = ti.item_id
WHERE tp.isaccept = 'Y'
" + whereSupplier + whereTender + @"
ORDER BY tp.fdate DESC";

                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        if (req.TenderId.HasValue)
                            cmd.Parameters.AddWithValue("@TenderId", req.TenderId);

                        if (req.SupplierId.HasValue)
                            cmd.Parameters.AddWithValue("@SupplierId", req.SupplierId);

                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                list.Add(new TenderSupplierDTO
                                {
                                    ItemId = Convert.ToInt32(reader["item_id"]),
                                    ItemCode = reader["item_code_as_per_tender"].ToString(),
                                    ItemName = reader["item_name"].ToString(),
                                    SupplierName = reader["name"].ToString(),
                                    TenderNo = reader["tender_no"].ToString(),
                                    TenderDate = reader["tender_date"].ToString(),
                                    TenderQuantity = Convert.ToInt32(reader["tender_quantity"]),
                                    BasicRate = Convert.ToDecimal(reader["basicrate"]),
                                    GST = Convert.ToDecimal(reader["gst"]),
                                    AcceptedBasicRate = Convert.ToDecimal(reader["acceptedBasicrate"]),
                                    AcceptedDate = reader["AcceptedDt"].ToString(),
                                    TenderId = Convert.ToInt32(reader["tender_id"]),
                                    SupplierId = Convert.ToInt32(reader["supplier_id"])
                                });
                            }
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



        //intedent
        [HttpGet("Indent/GetUsersByAuthority")]
        public async Task<IActionResult> GetUsersByAuthority([FromQuery] int authority_id)
        {
            List<UsersDTO> list = new List<UsersDTO>();

            try
            {
                string whereCondition = "";

                if (authority_id == 12)
                {
                    whereCondition = " AND user_type = 'DME' AND u.authority = @AuthorityId AND u.user_id != 12 ";
                }

                string query = @"
SELECT DISTINCT 
    u.USER_ID,
    u.user_name,
    u.designation
FROM maslocations l
INNER JOIN users u ON u.user_id = l.user_id
WHERE u.USER_ID IS NOT NULL
" + whereCondition + @"
ORDER BY u.designation";

                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        if (authority_id == 12)
                        {
                            cmd.Parameters.AddWithValue("@AuthorityId", authority_id);
                        }

                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                list.Add(new UsersDTO
                                {
                                    UserId = Convert.ToInt32(reader["USER_ID"]),
                                    UserName = reader["user_name"].ToString(),
                                    Designation = reader["designation"].ToString()
                                });
                            }
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
        //getitem
        [HttpGet("Indent/GetItemsList")]
        public async Task<IActionResult> GetItemsList()
        {
            var list = new List<ItemListsDTO>();

            try
            {
                string query = @"SELECT 
                            A.item_id AS ItemId,
                            A.item_name AS ItemName
                         FROM MASITEMS A 
                         WHERE A.PARENT_ITEM_ID IS NOT NULL 
                         ORDER BY A.ITEM_NAME";

                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                list.Add(new ItemListsDTO
                                {
                                    ItemId = reader["ItemId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ItemId"]),
                                    ItemName = reader["ItemName"] == DBNull.Value ? "" : reader["ItemName"].ToString()
                                });
                            }
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
        //[HttpGet("Indent/GetItemsList")]
        //public async Task<IActionResult> GetItemsList()
        //{
        //    List<ItemDTO> list = new List<ItemDTO>();

        //    try
        //    {
        //        string query = @"
        //    SELECT 
        //        A.item_id AS ItemId,
        //        A.item_name AS ItemName
        //    FROM MASITEMS A 
        //    WHERE A.PARENT_ITEM_ID IS NOT NULL 
        //    ORDER BY A.ITEM_NAME";

        //        using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        //        using (SqlCommand cmd = new SqlCommand(query, conn))
        //        {
        //            await conn.OpenAsync();

        //            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
        //            {
        //                while (await reader.ReadAsync())
        //                {
        //                    list.Add(new ItemDTO
        //                    {
        //                        ItemId = reader["ItemId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ItemId"]),
        //                        ItemName = reader["ItemName"]?.ToString()
        //                    });
        //                }
        //            }
        //        }

        //        return Ok(list);
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, $"Error: {ex.Message}");
        //    }
        //}
        ////testing
        [HttpGet("Indent/GetIndentConsolidation")]
        public async Task<IActionResult> GetIndentConsolidation([FromQuery] IndentFilterRequestDTO req)
        {
            List<IndentConsolidationDTO> list = new List<IndentConsolidationDTO>();

            try
            {
                string whereYear = "";
                string whereItem = "";
                string whereAuthority = "";
                string whereUser = "";

                if (!string.IsNullOrEmpty(req.FinancialYearId))
                    whereYear = " AND A.financial_year_id = @FinancialYearId ";

                if (!string.IsNullOrEmpty(req.ItemId))
                    whereItem = " AND A.ITEM_ID = @ItemId ";

                if (!string.IsNullOrEmpty(req.AuthorityId))
                    whereAuthority = " AND A.directorate_id = @AuthorityId ";

                if (!string.IsNullOrEmpty(req.UserId))
                    whereUser = " AND u.USER_ID = @UserId ";

                string query = @"
SELECT 
    A.INDENT_CONSOLIDATION_ID,
    A.INDENT_CON_NO,
    CONVERT(VARCHAR(10), A.CONSOLIDATED_DATE, 103) AS INDENT_DATE,
    G.item_count,
    A.STATUS,
    CASE 
        WHEN A.directorate_id != '12' THEN B.FACILITY_AUT_NAME 
        ELSE u.user_name 
    END AS FACILITY_AUT_NAME,
    A.DESCRIPTION,
    A.path,
    u.user_type,
    u.designation,
    u.user_id,
    u.user_name
FROM INDENT_CONSOLIDATION A
INNER JOIN users u ON u.user_id = A.user_id
INNER JOIN FACILITY_AUT B ON B.FACILITY_AUT_ID = A.DIRECTORATE_ID
INNER JOIN (
    SELECT 
        B.INDENT_CONSOLIDATION_ID,
        COUNT(A.indent_cons_items_id) AS item_count
    FROM INDENT_CONS_ITEMS A
    INNER JOIN INDENT_CONSOLIDATION B 
        ON B.INDENT_CONSOLIDATION_ID = A.INDENT_CONSOLIDATED_ID
    INNER JOIN MASITEMS J ON J.ITEM_ID = A.ITEM_ID
    WHERE B.STATUS != 'I'
    " + whereItem + @"
    GROUP BY B.INDENT_CONSOLIDATION_ID
) G ON G.INDENT_CONSOLIDATION_ID = A.INDENT_CONSOLIDATION_ID
WHERE A.STATUS != 'I'
" + whereYear + whereAuthority + whereUser + @"
ORDER BY A.CONSOLIDATED_DATE DESC";

                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        if (!string.IsNullOrEmpty(req.FinancialYearId))
                            cmd.Parameters.AddWithValue("@FinancialYearId", req.FinancialYearId);

                        if (!string.IsNullOrEmpty(req.ItemId))
                            cmd.Parameters.AddWithValue("@ItemId", req.ItemId);

                        if (!string.IsNullOrEmpty(req.AuthorityId))
                            cmd.Parameters.AddWithValue("@AuthorityId", req.AuthorityId);

                        if (!string.IsNullOrEmpty(req.UserId))
                            cmd.Parameters.AddWithValue("@UserId", req.UserId);

                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                list.Add(new IndentConsolidationDTO
                                {
                                    IndentConsolidationId = Convert.ToInt32(reader["INDENT_CONSOLIDATION_ID"]),
                                    IndentConNo = reader["INDENT_CON_NO"].ToString(),
                                    IndentDate = reader["INDENT_DATE"].ToString(),
                                    ItemCount = Convert.ToInt32(reader["item_count"]),
                                    Status = reader["STATUS"].ToString(),
                                    FacilityAutName = reader["FACILITY_AUT_NAME"].ToString(),
                                    Description = reader["DESCRIPTION"].ToString(),
                                    Path = reader["path"].ToString(),
                                    UserType = reader["user_type"].ToString(),
                                    Designation = reader["designation"].ToString(),
                                    UserId = Convert.ToInt32(reader["user_id"]),
                                    UserName = reader["user_name"].ToString()
                                });
                            }
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

        //complains
        [HttpGet("complain/GetComplainReport")]
        public async Task<IActionResult> GetComplainReport([FromQuery] ComplaintRequestDTO req)
        {
            List<ComplaintDTO> list = new List<ComplaintDTO>();

            try
            {
                string whdid = "";

                if (!string.IsNullOrEmpty(req.Did) && req.Did != "0")
                {
                    whdid = " and l.authority = @Did ";
                }

                string query = @"
SELECT   
    c.complaint_id, 
    c.complaint_no, 
    CONVERT(varchar,c.complaint_date,103) as complaint_date, 
    c.item_id, 
    c.complaint_details, 
    c.location_id,
    c.supplier_id, 
    c.complaints_trouble_id,
    CONVERT(varchar,c.not_function_date,103) as not_function_date, 
    m.item_name, 
    l.location_name, 
    m.item_code_as_per_tender, 
    l.user_id, 
    c.Serial_no, 
    mas.name,
    mas.email_id,
    mas.mobile_no,
    c.path,
    c.ext,
    c.complaint_id as extensionId
FROM complaints c
INNER JOIN masitems m ON m.item_id = c.item_id 
INNER JOIN maslocations l ON l.location_id = c.location_id 
INNER JOIN massuppliers mas on mas.supplier_id = c.supplier_id
WHERE 1=1 " + whdid + @" 
AND c.status = @Status
ORDER BY complaint_date";

                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (!string.IsNullOrEmpty(req.Did) && req.Did != "0")
                    {
                        cmd.Parameters.AddWithValue("@Did", req.Did);
                    }

                    cmd.Parameters.AddWithValue("@Status", req.Status);

                    await conn.OpenAsync();

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new ComplaintDTO
                            {
                                ComplaintId = reader["complaint_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["complaint_id"]),
                                ComplaintNo = reader["complaint_no"] == DBNull.Value ? "" : reader["complaint_no"].ToString(),
                                ComplaintDate = reader["complaint_date"] == DBNull.Value ? "" : reader["complaint_date"].ToString(),
                                ItemId = reader["item_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["item_id"]),
                                ComplaintDetails = reader["complaint_details"] == DBNull.Value ? "" : reader["complaint_details"].ToString(),
                                LocationId = reader["location_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["location_id"]),
                                SupplierId = reader["supplier_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["supplier_id"]),
                                ComplaintTroubleId = reader["complaints_trouble_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["complaints_trouble_id"]),
                                NotFunctionDate = reader["not_function_date"] == DBNull.Value ? "" : reader["not_function_date"].ToString(),
                                ItemName = reader["item_name"] == DBNull.Value ? "" : reader["item_name"].ToString(),
                                LocationName = reader["location_name"] == DBNull.Value ? "" : reader["location_name"].ToString(),
                                ItemCode = reader["item_code_as_per_tender"] == DBNull.Value ? "" : reader["item_code_as_per_tender"].ToString(),
                                UserId = reader["user_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["user_id"]),
                                SerialNo = reader["Serial_no"] == DBNull.Value ? "" : reader["Serial_no"].ToString(),
                                SupplierName = reader["name"] == DBNull.Value ? "" : reader["name"].ToString(),
                                Email = reader["email_id"] == DBNull.Value ? "" : reader["email_id"].ToString(),
                                MobileNo = reader["mobile_no"] == DBNull.Value ? "" : reader["mobile_no"].ToString(),
                                Path = reader["path"] == DBNull.Value ? "" : reader["path"].ToString(),
                                Ext = reader["ext"] == DBNull.Value ? "" : reader["ext"].ToString(),
                                ExtensionId = reader["extensionId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["extensionId"])
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
        //        [HttpGet("complain/GetComplainReport")]
        //        public async Task<IActionResult> GetComplainReport([FromQuery] ComplaintRequestDTO req)
        //        {
        //            List<ComplaintDTO> list = new List<ComplaintDTO>();

        //            try
        //            {
        //                string whdid = "";

        //                if (!string.IsNullOrEmpty(req.Did) && req.Did != "0")
        //                {
        //                    whdid = " and l.authority = @Did ";
        //                }

        //                string query = @"
        //SELECT   
        //    c.complaint_id, 
        //    c.complaint_no, 
        //    CONVERT(varchar,c.complaint_date,103) as complaint_date, 
        //    c.item_id, 
        //    c.complaint_details, 
        //    c.location_id,
        //    c.supplier_id, 
        //    c.complaints_trouble_id,
        //    CONVERT(varchar,c.not_function_date,103) as not_function_date, 
        //    m.item_name, 
        //    l.location_name, 
        //    m.item_code_as_per_tender, 
        //    l.user_id, 
        //    c.Serial_no, 
        //    mas.name,
        //    mas.email_id,
        //    mas.mobile_no,
        //    c.path,
        //    c.ext,
        //    c.complaint_id as extensionId
        //FROM complaints c
        //INNER JOIN masitems m ON m.item_id = c.item_id 
        //INNER JOIN maslocations l ON l.location_id = c.location_id 
        //INNER JOIN massuppliers mas on mas.supplier_id = c.supplier_id
        //WHERE 1=1 " + whdid + @" 
        //AND c.status = @Status
        //ORDER BY complaint_date";

        //                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        //                {
        //                    using (SqlCommand cmd = new SqlCommand(query, conn))
        //                    {
        //                        if (!string.IsNullOrEmpty(req.Did) && req.Did != "0")
        //                        {
        //                            cmd.Parameters.AddWithValue("@Did", req.Did);
        //                        }

        //                        cmd.Parameters.AddWithValue("@Status", req.Status);

        //                        await conn.OpenAsync();

        //                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
        //                        {
        //                            while (await reader.ReadAsync())
        //                            {
        //                                list.Add(new ComplaintDTO
        //                                {
        //                                    ComplaintId = Convert.ToInt32(reader["complaint_id"]),
        //                                    ComplaintNo = reader["complaint_no"].ToString(),
        //                                    ComplaintDate = reader["complaint_date"].ToString(),
        //                                    ItemId = Convert.ToInt32(reader["item_id"]),
        //                                    ComplaintDetails = reader["complaint_details"].ToString(),
        //                                    LocationId = Convert.ToInt32(reader["location_id"]),
        //                                    SupplierId = Convert.ToInt32(reader["supplier_id"]),
        //                                    ComplaintTroubleId = Convert.ToInt32(reader["complaints_trouble_id"]),
        //                                    NotFunctionDate = reader["not_function_date"].ToString(),
        //                                    ItemName = reader["item_name"].ToString(),
        //                                    LocationName = reader["location_name"].ToString(),
        //                                    ItemCode = reader["item_code_as_per_tender"].ToString(),
        //                                    UserId = Convert.ToInt32(reader["user_id"]),
        //                                    SerialNo = reader["Serial_no"].ToString(),
        //                                    SupplierName = reader["name"].ToString(),
        //                                    Email = reader["email_id"].ToString(),
        //                                    MobileNo = reader["mobile_no"].ToString(),
        //                                    Path = reader["path"].ToString(),
        //                                    Ext = reader["ext"].ToString(),
        //                                    ExtensionId = Convert.ToInt32(reader["extensionId"])
        //                                });
        //                            }
        //                        }
        //                    }
        //                }

        //                return Ok(list);
        //            }
        //            catch (Exception ex)
        //            {
        //                return StatusCode(500, ex.Message);
        //            }
        //        }

        //Reports

        [HttpGet("FinanceRep/Facility")]
        public async Task<IActionResult> GetFacilityReport([FromQuery] int financial_year_id)
        {
            List<FacilityReportDTO> list = new List<FacilityReportDTO>();

            try
            {
                string query = @"
        select  
            facility_aut_id,
            facility_aut_name,
            POtype,
            count(distinct PO_ID) as nospo,
            count(distinct CODE) as nositem,
            round((sum(totalPOvalue)/10000000),2) as totalPOvalueCr,
            CAST(sum(totalPOvalue) AS DECIMAL(15, 2)) as PValue
        from 
        (
            select 
                R.ITEM_CODE_AS_PER_TENDER as CODE,
                p.OUTWARD_NO,
                pi.quantity,
                c.single_unit_price,
                c.single_unit_price*pi.quantity as totalPOvalue,
                p.PO_ID,
                aut.facility_aut_name,
                aut.facility_aut_id,
                case when p.Potype ='CP' then 'COVID19 PO' else 'Normal PO' end as POtype
            from po_items pi 
            inner join MASITEMS R on R.ITEM_ID = pi.item_id
            inner join purchase_order p on pi.po_id = p.po_id
            inner join facility_aut aut on aut.facility_aut_id = p.directorate_id
            left join 
            (
                select a.supplier_id,a.tender_id,ci.item_id,ci.single_unit_price
                from award_of_contract a 
                inner join contract_items ci on ci.award_of_contract_id=a.award_of_contract_id
            ) c on c.item_id=pi.item_id 
               and c.tender_id=p.tender_id 
               and c.supplier_id=p.supplier_id
            where p.financial_year_id = @financial_year_id
            and p.status not in ('Incomplete','Waiting For Approval','Cancelled')
        ) a
        group by facility_aut_name, POtype, facility_aut_id
        order by facility_aut_id";

                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@financial_year_id", financial_year_id);

                    await conn.OpenAsync();

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new FacilityReportDTO
                            {
                                FacilityAutId = reader["facility_aut_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["facility_aut_id"]),
                                FacilityAutName = reader["facility_aut_name"] == DBNull.Value ? "" : reader["facility_aut_name"].ToString(),
                                POtype = reader["POtype"] == DBNull.Value ? "" : reader["POtype"].ToString(),
                                NosPO = reader["nospo"] == DBNull.Value ? 0 : Convert.ToInt32(reader["nospo"]),
                                NosItem = reader["nositem"] == DBNull.Value ? 0 : Convert.ToInt32(reader["nositem"]),
                                TotalPOValueCr = reader["totalPOvalueCr"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["totalPOvalueCr"]),
                                PValue = reader["PValue"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["PValue"])
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

        [HttpGet("FinanceRep/GetPODetailsReport")]
        public async Task<IActionResult> GetPODetailsReport(string finYrId, string directorateId, string POTYPE)
        {
            List<POReportDTO> list = new List<POReportDTO>();

            try
            {
                string whclause = "";

                if (!string.IsNullOrEmpty(finYrId))
                {
                    whclause = " and p.financial_year_id =" + finYrId;
                }

                if (!string.IsNullOrEmpty(directorateId))
                {
                    whclause += " and p.directorate_id=" + directorateId;
                }

                string query = @"
select  facility_aut_id,facility_aut_name,OUTWARD_NO+'/'+PO_NO as PONO,POtype,CODE,ITEM_NAME, po_date,SUPPLIER_NAME,TENDER_NO,
sum(quantity) as quantity,CAST(sum(totalPOvalue) AS DECIMAL(15, 0)) as PValue
,POtype,PDate,percentage,basic_rate
from 
(
    select 
        R.ITEM_CODE_AS_PER_TENDER as CODE,
        R.item_name as ITEM_NAME,
        p.OUTWARD_NO,
        convert(varchar,p.po_date, 103) as po_date,
        p.po_date as PDate,
        pi.quantity,
        c.basic_rate,
        c.percentage,
        c.single_unit_price,
        c.single_unit_price*pi.quantity as totalPOvalue,
        b.NAME as SUPPLIER_NAME,
        c.TENDER_NO,
        p.STATUS,
        pi.item_id,
        p.FINANCIAL_YEAR_ID,
        p.TENDER_ID,
        p.PO_NO,
        p.SUPPLIER_ID,
        p.directorate_id,
        p.PO_ID,
        aut.facility_aut_name,
        aut.facility_aut_id,
        case when p.Potype ='CP' then 'COVID19 PO' else 'Normal PO' end as POtype
    from po_items pi 
    inner join MASITEMS R on R.ITEM_ID = pi.item_id
    inner join maslocations m on m.location_id=pi.consignee_id
    inner join purchase_order p on pi.po_id=p.po_id
    inner join MASSUPPLIERS b on p.supplier_id = b.SUPPLIER_ID
    inner join MAS_FINANCIAL_YEAR E ON E.FINANCIAL_YEAR_ID=p.FINANCIAL_YEAR_ID
    inner join facility_aut aut on aut.facility_aut_id=p.directorate_id
    left outer join 
    (
        select a.supplier_id,a.tender_id,ci.item_id,ci.basic_rate,ci.percentage,
        ci.single_unit_price,t.tender_no,t.tender_date
        from award_of_contract a 
        inner join contract_items ci on ci.award_of_contract_id=a.award_of_contract_id
        inner join tenders t on t.tender_id=a.tender_id
    ) c 
    on c.item_id=pi.item_id and c.tender_id=p.tender_id and c.supplier_id=p.supplier_id
    where 1=1 " + whclause + @"
    and p.status not in ('Incomplete','Waiting For Approval','Cancelled')
) a
where a.POtype = '" + POTYPE + @"'
group by facility_aut_name,POtype,facility_aut_id,
OUTWARD_NO,PO_NO,POtype,CODE,ITEM_NAME,po_date,
SUPPLIER_NAME,TENDER_NO,PDate,percentage,basic_rate
order by PDate desc";

                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    await conn.OpenAsync();

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new POReportDTO
                            {
                                FacilityAutId = reader["facility_aut_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["facility_aut_id"]),
                                FacilityAutName = reader["facility_aut_name"]?.ToString(),
                                PONO = reader["PONO"]?.ToString(),
                                POtype = reader["POtype"]?.ToString(),
                                CODE = reader["CODE"]?.ToString(),
                                ITEM_NAME = reader["ITEM_NAME"]?.ToString(),
                                PODate = reader["po_date"]?.ToString(),
                                SupplierName = reader["SUPPLIER_NAME"]?.ToString(),
                                TenderNo = reader["TENDER_NO"]?.ToString(),
                                Quantity = reader["quantity"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["quantity"]),
                                PValue = reader["PValue"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["PValue"]),
                                PDate = reader["PDate"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["PDate"]),
                                Percentage = reader["percentage"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["percentage"]),
                                BasicRate = reader["basic_rate"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["basic_rate"])
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

        //PODetailsDto wise pyment report 
        [HttpGet("FinanceRep/PaymentUnionReport")]
        public async Task<IActionResult> GetPaymentUnionReport(
              [FromQuery] string potype,
              [FromQuery] string fromDate,
              [FromQuery] string toDate)
        {
            List<PaymentUnionDTO> list = new List<PaymentUnionDTO>();

            try
            {
                string whereclause = "";
                string whereFromTo = "";


                // PO TYPE FILTER (same logic)
                if (potype == "NP")
                {
                    whereclause += " and isnull(p.potype,'NP')='NP' ";
                }
                else if (potype == "CP")
                {
                    whereclause += " and isnull(p.potype,'NP')='CP' ";
                }
                else if (potype == "All")
                {
                    whereclause += "";
                }

                // DATE FILTER (same logic as your code)
                if (!string.IsNullOrEmpty(fromDate) && !string.IsNullOrEmpty(toDate))
                {
                    whereFromTo = @" and py.AIDDATE between 
            CONVERT(varchar(10), CONVERT(date, '" + fromDate + @"', 103), 120)
            and 
            CONVERT(varchar(10), CONVERT(date, '" + toDate + @"', 103), 120) ";
                }

                string query = @"
select * from (

select 
p.po_no,
(case when p.soissueDT is null then convert(varchar,p.po_date,103) else convert(varchar,p.soissueDT,103) end) as po_date,
p.outward_no,
sp.name as SupplierName,
convert(varchar,s.SANCTIONDATE,103) as SANCTIONDATE,
s.SANCTIONEDAMOUNT as GrossAmt,
isnull(s.DEDUCTIONS,0) as TotalDed,
isnull(s.addition,0) as TotalAddition,
s.chequeAmt as ChequeAmt,
py.AIDNO,
convert(varchar,py.AIDDATE,103) as ChequeDate,
b.BUDGETNAME,
b.BUDGETID,
s.SANCTIONID,
mp.PStatus,
sp.supplier_id,
p.Potype,
p.po_id,
s.PAYMENTID,
'PO Payment' as typeP,
py.AIDDATE,
s.ADMINCHARGES,
round((s.ADMINCHARGES+s.chequeAmt),0) as ActChequeAmt,
isnull(mcn.ACCOUNTNO,'') as ACCOUNTNO
from BLPSANCTIONS s
inner join BLPPAYMENTS py on py.PAYMENTID=s.PAYMENTID
inner join MASBUDGET b on b.BUDGETID=s.BUDGETID
inner join purchase_order p on s.po_id=p.po_id
inner join massuppliers sp on sp.supplier_id=p.supplier_id
left outer join MasPStatus mp on mp.PType=s.STATUS
left outer join MasCGMSCAccNos mcn on mcn.bankid = py.CGMSCPAIDBANKID
where s.STATUS in ('P') " + whereclause + whereFromTo + @"

union all

select 
p.po_no,
(case when p.soissueDT is null then convert(varchar,p.po_date,103) else convert(varchar,p.soissueDT,103) end) as po_date,
p.outward_no,
sp.name as SupplierName,
convert(varchar,py.AIDDATE,103) as SANCTIONDATE,
t.RELEASEAMT as GrossAmt,
0 as TotalDed,
0 as TotalAddition,
t.RELEASEAMT as ChequeAmt,
py.AIDNO,
convert(varchar,py.AIDDATE,103) as ChequeDate,
b.BUDGETNAME,
b.BUDGETID,
s.SANCTIONID,
mp.PStatus,
sp.supplier_id,
p.Potype,
p.po_id,
py.PAYMENTID,
'Release Witheld' as typeP,
py.AIDDATE,
s.ADMINCHARGES,
round((s.ADMINCHARGES+s.chequeAmt),0) as ActChequeAmt,
isnull(mcn.ACCOUNTNO,'') as ACCOUNTNO
from BLPTAXS t
inner join BLPSANCTIONS s on s.SANCTIONID=t.SANCTIONID
inner join MASBUDGET b on b.BUDGETID=s.BUDGETID
inner join purchase_order p on p.po_id=s.po_id
inner join massuppliers sp on sp.supplier_id=p.supplier_id
inner join BLPPAYMENTS py on py.paymentid=t.paymentid
left outer join MasPStatus mp on mp.PType=py.STATUS
left outer join MasCGMSCAccNos mcn on mcn.bankid = py.CGMSCPAIDBANKID
where py.STATUS='P' and t.TAXTYPEID=250 " + whereclause + whereFromTo + @"

) a
order by AidDate desc";

                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    await conn.OpenAsync();

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new PaymentUnionDTO
                            {
                                PoNo = reader["po_no"]?.ToString(),
                                PoDate = reader["po_date"]?.ToString(),
                                OutwardNo = reader["outward_no"]?.ToString(),
                                SupplierName = reader["SupplierName"]?.ToString(),
                                SanctionDate = reader["SANCTIONDATE"]?.ToString(),
                                GrossAmt = reader["GrossAmt"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["GrossAmt"]),
                                TotalDed = reader["TotalDed"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["TotalDed"]),
                                TotalAddition = reader["TotalAddition"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["TotalAddition"]),
                                ChequeAmt = reader["ChequeAmt"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["ChequeAmt"]),
                                AidNo = reader["AIDNO"]?.ToString(),
                                ChequeDate = reader["ChequeDate"]?.ToString(),
                                BudgetName = reader["BUDGETNAME"]?.ToString(),
                                BudgetId = reader["BUDGETID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["BUDGETID"]),
                                SanctionId = reader["SANCTIONID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SANCTIONID"]),
                                PStatus = reader["PStatus"]?.ToString(),
                                SupplierId = reader["supplier_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["supplier_id"]),
                                Potype = reader["Potype"]?.ToString(),
                                PoId = reader["po_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["po_id"]),
                                PaymentId = reader["PAYMENTID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["PAYMENTID"]),
                                TypeP = reader["typeP"]?.ToString(),
                                AidDate = reader["AIDDATE"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["AIDDATE"]),
                                AdminCharges = reader["ADMINCHARGES"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["ADMINCHARGES"]),
                                ActChequeAmt = reader["ActChequeAmt"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["ActChequeAmt"]),
                                AccountNo = reader["ACCOUNTNO"]?.ToString()
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


        [HttpGet("FinanceRep/ChequePaymentReport")]
        public async Task<IActionResult> GetChequePaymentReport(
     string potype,
     [FromQuery] string? fromDate = null, // string fromDate,
     [FromQuery] string? toDate = null )// string toDate)
        {
            List<SupplierPaymentSummaryDTO> list = new List<SupplierPaymentSummaryDTO>();

            try
            {
                string whereFromTo = "";
                string whereclause = "";

                // ✅ PO TYPE FILTER (FIXED LOGIC SAME)
                if (potype == "NP")
                {
                    whereclause += " and isnull(po.Potype,'NP')='NP' ";
                }
                else if (potype == "CP")
                {
                    whereclause += " and isnull(po.Potype,'NP')='CP' ";
                }
                else if (potype == "All")
                {
                    whereclause += "";
                }

                // ✅ DATE FILTER
                if (!string.IsNullOrEmpty(fromDate) && !string.IsNullOrEmpty(toDate))
                {
                    whereFromTo = @" and p.AIDDATE between 
            CONVERT(varchar(10), CONVERT(date, '" + fromDate + @"', 103), 120)
            and 
            CONVERT(varchar(10), CONVERT(date, '" + toDate + @"', 103), 120) ";
                }

                string query = @"
select 
sp.name,
sp.supplier_id,
count(distinct s.SANCTIONID) as countNOs,
p.AMOUNTPAID as ChequeAmt,
round(sum(s.ADMINCHARGES),0) adminc,
p.AIDNO,
convert(varchar,p.AIDDATE,103) as chequeDT,
convert(varchar,p.PAIDON,103) PAIDON,
p.PAYMENTID,
b.BUDGETNAME,
b.BUDGETID,
p.AMOUNTPAID + round(sum(s.ADMINCHARGES),0) as TotalCheque,
sp.mobile_no,
len(rtrim(ltrim(sp.mobile_no))) as lenmob,
sp.email_id
from BLPPAYMENTS p
inner join BLPSANCTIONS s on s.PAYMENTID = p.PAYMENTID
inner join MASBUDGET b on b.BUDGETID = s.BUDGETID
inner join purchase_order po on po.po_id = s.po_id
inner join massuppliers sp on sp.supplier_id = po.supplier_id
where p.PAIDON is not null "
        + whereclause + whereFromTo + @"
group by 
p.PAYMENTID,
p.AMOUNTPAID,
b.BUDGETNAME,
b.BUDGETID,
sp.name,
p.AIDNO,
p.AIDDATE,
p.PAIDON,
sp.supplier_id,
sp.mobile_no,
sp.email_id,
po.Potype
order by p.AIDDATE desc";

                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    await conn.OpenAsync();

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new SupplierPaymentSummaryDTO
                            {
                                Name = reader["name"]?.ToString(),
                                SupplierId = reader["supplier_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["supplier_id"]),
                                CountNOs = reader["countNOs"] == DBNull.Value ? 0 : Convert.ToInt32(reader["countNOs"]),
                                ChequeAmt = reader["ChequeAmt"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["ChequeAmt"]),
                                AdminC = reader["adminc"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["adminc"]),
                                AidNo = reader["AIDNO"]?.ToString(),
                                ChequeDT = reader["chequeDT"]?.ToString(),
                                PaidOn = reader["PAIDON"]?.ToString(),
                                PaymentId = reader["PAYMENTID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["PAYMENTID"]),
                                BudgetName = reader["BUDGETNAME"]?.ToString(),
                                BudgetId = reader["BUDGETID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["BUDGETID"]),
                                TotalCheque = reader["TotalCheque"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["TotalCheque"]),
                                MobileNo = reader["mobile_no"]?.ToString(),
                                LenMob = reader["lenmob"] == DBNull.Value ? 0 : Convert.ToInt32(reader["lenmob"]),
                                EmailId = reader["email_id"]?.ToString()
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

//reports 
        [HttpGet("Reports/GetPOSummaryReport")]
        public async Task<IActionResult> GetPOSummaryReport(
    int financialYearId,
    int directorateId,
    string itemCode = "All")
        {
            List<IndentItemSummaryDTO> list = new List<IndentItemSummaryDTO>();

            try
            {
                string whereCauseItem = "";

                if (!string.IsNullOrEmpty(itemCode) && itemCode != "All")
                {
                    whereCauseItem = " AND R.ITEM_CODE_AS_PER_TENDER = @itemCode ";
                }

                string query = @"
        SELECT 
            CODE,
            ITEM_NAME,
            SUM(quantity) AS Quantity,
            basic_rate AS BasicRate,
            percentage AS Percentage,
            single_unit_price AS SingleUnitPrice,
            SUM(totalPOvalue) AS TotalPOValue
        FROM 
        (
            SELECT 
                R.ITEM_CODE_AS_PER_TENDER AS CODE,
                R.item_name AS ITEM_NAME,
                pi.quantity,
                c.basic_rate,
                c.percentage,
                c.single_unit_price,
                c.single_unit_price * pi.quantity AS totalPOvalue
            FROM po_items pi 
            INNER JOIN MASITEMS R ON R.ITEM_ID = pi.item_id
            INNER JOIN maslocations m ON m.location_id = pi.consignee_id
            INNER JOIN purchase_order p ON pi.po_id = p.po_id
            INNER JOIN MASSUPPLIERS b ON p.supplier_id = b.SUPPLIER_ID
            INNER JOIN MAS_FINANCIAL_YEAR E ON E.FINANCIAL_YEAR_ID = p.FINANCIAL_YEAR_ID
            INNER JOIN facility_aut aut ON aut.facility_aut_id = p.directorate_id
            LEFT JOIN 
            (
                SELECT 
                    a.supplier_id,
                    a.tender_id,
                    ci.item_id,
                    ci.basic_rate,
                    ci.percentage,
                    ci.single_unit_price
                FROM award_of_contract a 
                INNER JOIN contract_items ci 
                    ON ci.award_of_contract_id = a.award_of_contract_id
            ) c 
            ON c.item_id = pi.item_id 
            AND c.tender_id = p.tender_id 
            AND c.supplier_id = p.supplier_id

            WHERE 
                p.financial_year_id = @financialYearId
                AND p.directorate_id = @directorateId
                AND p.status NOT IN ('Incomplete','Waiting For Approval','Cancelled')
                " + whereCauseItem + @"
        ) a 
        GROUP BY CODE, ITEM_NAME, basic_rate, percentage, single_unit_price
        ORDER BY ITEM_NAME";

                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@financialYearId", financialYearId);
                    cmd.Parameters.AddWithValue("@directorateId", directorateId);

                    if (whereCauseItem != "")
                        cmd.Parameters.AddWithValue("@itemCode", itemCode);

                    await conn.OpenAsync();

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new IndentItemSummaryDTO
                            {
                                Code = reader["CODE"]?.ToString(),
                                ItemName = reader["ITEM_NAME"]?.ToString(),
                                Quantity = reader["Quantity"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["Quantity"]),
                                BasicRate = reader["BasicRate"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["BasicRate"]),
                                Percentage = reader["Percentage"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["Percentage"]),
                                SingleUnitPrice = reader["SingleUnitPrice"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["SingleUnitPrice"]),
                                TotalPOValue = reader["TotalPOValue"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["TotalPOValue"])
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



        [HttpGet("Reports/GetItemWPODetails")]
        public async Task<IActionResult> GetItemWPODetails(
    string finYrId,
    string itemCode,
    string directorateId)
        {
            List<IndentDetailsDTO> list = new List<IndentDetailsDTO>();

            try
            {
                string whereClause = "";

                if (!string.IsNullOrEmpty(finYrId))
                    whereClause += " AND p.financial_year_id = @finYrId";

                if (!string.IsNullOrEmpty(itemCode))
                    whereClause += " AND R.ITEM_CODE_AS_PER_TENDER = @itemCode";

                if (!string.IsNullOrEmpty(directorateId))
                    whereClause += " AND p.directorate_id = @directorateId";

                string query = @"
        SELECT 
            m.location_id AS LocationId,
            m.location_name AS LocationName,
            m.DP_DistrictID,
            m.user_id AS UserId,
            u.user_name AS UserName,
            u.user_type AS UserType,
            u.designation AS Designation,

            R.ITEM_CODE_AS_PER_TENDER AS Code,
            R.item_name AS ItemName,

            p.OUTWARD_NO AS OutwardNo,
            CONVERT(varchar, p.po_date, 103) AS PoDate,

            pi.quantity AS Quantity,
            c.basic_rate AS BasicRate,
            c.percentage AS Percentage,
            c.single_unit_price AS SingleUnitPrice,
            c.single_unit_price * pi.quantity AS TotalPOValue,

            b.NAME AS SupplierName,
            b.mobile_no AS MobileNo,

            c.TENDER_NO AS TenderNo,
            CONVERT(varchar, c.TENDER_DATE, 103) AS TenderDate,

            p.STATUS,
            p.REMARKS,

            pi.item_id AS ItemId,
            p.FINANCIAL_YEAR_ID AS FinancialYearId,
            E.YEAR,

            p.TENDER_ID AS TenderId,
            p.PO_NO AS PoNo,
            p.SUPPLIER_ID AS SupplierId,

            p.directorate_id AS DirectorateId,
            p.indent_fund_id AS IndentFundId,
            p.PO_ID AS PoId

        FROM po_items pi 
        INNER JOIN MASITEMS R ON R.ITEM_ID = pi.item_id
        INNER JOIN maslocations m ON m.location_id = pi.consignee_id
        INNER JOIN purchase_order p ON pi.po_id = p.po_id
        INNER JOIN MASSUPPLIERS b ON p.supplier_id = b.SUPPLIER_ID
        INNER JOIN MAS_FINANCIAL_YEAR E ON E.FINANCIAL_YEAR_ID = p.FINANCIAL_YEAR_ID
        INNER JOIN facility_aut aut ON aut.facility_aut_id = p.directorate_id

        LEFT JOIN 
        (
            SELECT 
                a.supplier_id,
                a.tender_id,
                ci.item_id,
                ci.basic_rate,
                ci.percentage,
                ci.single_unit_price,
                t.tender_no,
                t.tender_date
            FROM award_of_contract a 
            INNER JOIN contract_items ci 
                ON ci.award_of_contract_id = a.award_of_contract_id
            INNER JOIN tenders t 
                ON t.tender_id = a.tender_id
        ) c 
        ON c.item_id = pi.item_id 
        AND c.tender_id = p.tender_id 
        AND c.supplier_id = p.supplier_id

        LEFT JOIN users u ON u.user_id = m.user_id

        WHERE 1=1 " + whereClause + @"
        ORDER BY p.po_date DESC";

                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (!string.IsNullOrEmpty(finYrId))
                        cmd.Parameters.AddWithValue("@finYrId", finYrId);

                    if (!string.IsNullOrEmpty(itemCode))
                        cmd.Parameters.AddWithValue("@itemCode", itemCode);

                    if (!string.IsNullOrEmpty(directorateId))
                        cmd.Parameters.AddWithValue("@directorateId", directorateId);

                    await conn.OpenAsync();

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new IndentDetailsDTO
                            {
                                LocationId = reader["LocationId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["LocationId"]),
                                LocationName = reader["LocationName"]?.ToString(),
                                DP_DistrictID = reader["DP_DistrictID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["DP_DistrictID"]),
                                UserId = reader["UserId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["UserId"]),
                                UserName = reader["UserName"]?.ToString(),
                                UserType = reader["UserType"]?.ToString(),
                                Designation = reader["Designation"]?.ToString(),

                                Code = reader["Code"]?.ToString(),
                                ItemName = reader["ItemName"]?.ToString(),

                                OutwardNo = reader["OutwardNo"]?.ToString(),
                                PoDate = reader["PoDate"]?.ToString(),

                                Quantity = reader["Quantity"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["Quantity"]),
                                BasicRate = reader["BasicRate"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["BasicRate"]),
                                Percentage = reader["Percentage"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["Percentage"]),
                                SingleUnitPrice = reader["SingleUnitPrice"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["SingleUnitPrice"]),
                                TotalPOValue = reader["TotalPOValue"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["TotalPOValue"]),

                                SupplierName = reader["SupplierName"]?.ToString(),
                                MobileNo = reader["MobileNo"]?.ToString(),

                                TenderNo = reader["TenderNo"]?.ToString(),
                                TenderDate = reader["TenderDate"]?.ToString(),

                                Status = reader["STATUS"]?.ToString(),
                                Remarks = reader["REMARKS"]?.ToString(),

                                ItemId = reader["ItemId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ItemId"]),
                                FinancialYearId = reader["FinancialYearId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["FinancialYearId"]),
                                Year = reader["YEAR"]?.ToString(),

                                TenderId = reader["TenderId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TenderId"]),
                                PoNo = reader["PoNo"]?.ToString(),
                                SupplierId = reader["SupplierId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SupplierId"]),

                                DirectorateId = reader["DirectorateId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["DirectorateId"]),
                                IndentFundId = reader["IndentFundId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["IndentFundId"]),
                                PoId = reader["PoId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["PoId"])
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

        [HttpGet("get-tenders")]
        public IActionResult GetTenders(string yearId, string status, string searchType, string searchText)
        {
            List<TenderDto> list = new List<TenderDto>();

            using (SqlConnection con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                string query = "";
                SqlCommand cmd = new SqlCommand();
                cmd.Connection = con;

                // 🔥 CONDITION BASE QUERY SWITCH
                //if (!string.IsNullOrEmpty(searchText) && searchType == "T"|| searchType == "N")
                    if (!string.IsNullOrEmpty(searchText) && (searchType == "T" || searchType == "N"))
                    {
                    query = @"SELECT A.TENDER_ID,A.TENDER_NO,Convert(varchar(10),A.TENDER_DATE, 103) AS TENDER_DATE,A.TENDER_DESCRIPTION,
A.FLAG,A.financial_year_id,A.warranty_year,A.import_days,A.domestic_days,A.flag
,Convert(varchar(10),A.cover_a, 103) AS cover_a,Convert(varchar(10),A.cover_b, 103) AS cover_b,Convert(varchar(10),A.cover_Demo, 103) AS cover_Demo
,Convert(varchar(10),A.cover_c, 103) AS cover_c
,s.cStatus ,s.csid 
,isnull(t.totali,0) as totali, isnull(fnd.found,0)  as found
,isnull(n.nosNotFound,0) as nosNotFound,isnull(p.PriceEntry,0) as PriceEntry,isnull(ac.accept,0) as accept,isnull(r.reject,0) as reject 
FROM TENDERS A 

left outer join 
(
select COUNT(*) nosNotFound,tender_id from tender_items where  priceflag='N'
and  rejectdate is null
group by tender_id
) n on n.tender_id=A.tender_id

left outer join 
(
select COUNT(distinct ti.item_id) found,ti.tender_id from tender_items ti
inner join tenders t on t.tender_id=ti.tender_id
inner join live_tender_price l on l.tender_item_id=ti.tender_item_id
 where  ti.priceflag is null
group by ti.tender_id
) fnd on fnd.tender_id=A.tender_id

left outer join 
(
select count(distinct ti.item_id) as PriceEntry,t.tender_id   from tender_items ti 
inner join tenders t on t.tender_id=ti.tender_id
inner join live_tender_price l on l.tender_item_id=ti.tender_item_id
where l.basicrate is not null
group by t.tender_id
) p on p.tender_id=A.tender_id

left outer join 
(
select count(distinct ti.item_id) as accept,t.tender_id   from tender_items ti 
inner join tenders t on t.tender_id=ti.tender_id
inner join live_tender_price l on l.tender_item_id=ti.tender_item_id
where l.basicrate is not null and  l.isaccept='Y'
group by t.tender_id
) ac on ac.tender_id=A.tender_id

left outer join 
(
select COUNT(*) reject,tender_id from tender_items where rejectdate is not null
group by tender_id
) r on r.tender_id=A.tender_id

left outer join 
(
select COUNT(*) totali,tender_id from tender_items
group by tender_id
) t on t.tender_id=A.tender_id



left outer join mascoverstatus s on s.csid=a.csid and  A.TENDER_NO LIKE @search";
                 
                    cmd.Parameters.AddWithValue("@search", "%" + searchText + "%");
                }
                else
                {
                    query = @"SELECT A.TENDER_ID,A.TENDER_NO,
            Convert(varchar(10),A.TENDER_DATE,103) AS TENDER_DATE,
            A.TENDER_DESCRIPTION,
            s.cStatus,s.csid,
            isnull(t.totali,0) as totali,
            isnull(fnd.found,0) as found,
            isnull(n.nosNotFound,0) as nosNotFound,
            isnull(p.PriceEntry,0) as PriceEntry,
            isnull(ac.accept,0) as accept,
            isnull(r.reject,0) as reject 
            FROM TENDERS A
            LEFT JOIN mascoverstatus s on s.csid=a.csid
            LEFT JOIN (select COUNT(*) totali,tender_id from tender_items group by tender_id) t on t.tender_id=A.tender_id
            LEFT JOIN (select COUNT(*) nosNotFound,tender_id from tender_items where priceflag='N' and rejectdate is null group by tender_id) n on n.tender_id=A.tender_id
            LEFT JOIN (select COUNT(distinct ti.item_id) found,ti.tender_id from tender_items ti inner join live_tender_price l on l.tender_item_id=ti.tender_item_id where ti.priceflag is null group by ti.tender_id) fnd on fnd.tender_id=A.tender_id
            LEFT JOIN (select count(distinct ti.item_id) PriceEntry,t.tender_id from tender_items ti inner join tenders t on t.tender_id=ti.tender_id inner join live_tender_price l on l.tender_item_id=ti.tender_item_id where l.basicrate is not null group by t.tender_id) p on p.tender_id=A.tender_id
            LEFT JOIN (select count(distinct ti.item_id) accept,t.tender_id from tender_items ti inner join tenders t on t.tender_id=ti.tender_id inner join live_tender_price l on l.tender_item_id=ti.tender_item_id where l.basicrate is not null and l.isaccept='Y' group by t.tender_id) ac on ac.tender_id=A.tender_id
            LEFT JOIN (select COUNT(*) reject,tender_id from tender_items where rejectdate is not null group by tender_id) r on r.tender_id=A.tender_id
            WHERE 1=1";

                    if (!string.IsNullOrEmpty(yearId))
                    {
                        query += " AND A.financial_year_id = @yearId";
                        cmd.Parameters.AddWithValue("@yearId", yearId);
                    }

                    if (!string.IsNullOrEmpty(status))
                    {
                        query += " AND s.csid = @status";
                        cmd.Parameters.AddWithValue("@status", status);
                    }

                    // 👉 Code / Name search
                    //if (!string.IsNullOrEmpty(searchText))
                    //{
                    //    if (searchType == "C")
                    //    {
                    //        query += " AND A.TENDER_NO LIKE @search";
                    //    }
                    //    else if (searchType == "N")
                    //    {
                    //        query += " AND A.TENDER_NO LIKE @search";
                    //    }

                    //    cmd.Parameters.AddWithValue("@search", "%" + searchText + "%");
                    //}
                }

                cmd.CommandText = query;

                con.Open();
                SqlDataReader dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    list.Add(new TenderDto
                    {
                        TenderId = dr["TENDER_ID"] != DBNull.Value ? Convert.ToInt32(dr["TENDER_ID"]) : 0,
                        TenderNo = Convert.ToString(dr["TENDER_NO"]),
                        TenderDate = Convert.ToString(dr["TENDER_DATE"]),
                        TenderDescription = Convert.ToString(dr["TENDER_DESCRIPTION"]),

                        Flag = Convert.ToString(dr["FLAG"]),
                        FinancialYearId = dr["financial_year_id"] != DBNull.Value ? Convert.ToInt32(dr["financial_year_id"]) : 0,
                        WarrantyYear = dr["warranty_year"] != DBNull.Value ? Convert.ToInt32(dr["warranty_year"]) : 0,
                        ImportDays = dr["import_days"] != DBNull.Value ? Convert.ToInt32(dr["import_days"]) : 0,
                        DomesticDays = dr["domestic_days"] != DBNull.Value ? Convert.ToInt32(dr["domestic_days"]) : 0,

                        CoverA = Convert.ToString(dr["cover_a"]),
                        CoverB = Convert.ToString(dr["cover_b"]),
                        CoverDemo = Convert.ToString(dr["cover_Demo"]),
                        CoverC = Convert.ToString(dr["cover_c"]),

                        Status = Convert.ToString(dr["cStatus"]),
                        CsId = dr["csid"] != DBNull.Value ? Convert.ToInt32(dr["csid"]) : 0,

                        TotalItems = dr["totali"] != DBNull.Value ? Convert.ToInt32(dr["totali"]) : 0,
                        Found = dr["found"] != DBNull.Value ? Convert.ToInt32(dr["found"]) : 0,
                        NotFound = dr["nosNotFound"] != DBNull.Value ? Convert.ToInt32(dr["nosNotFound"]) : 0,
                        PriceEntry = dr["PriceEntry"] != DBNull.Value ? Convert.ToInt32(dr["PriceEntry"]) : 0,
                        Accept = dr["accept"] != DBNull.Value ? Convert.ToInt32(dr["accept"]) : 0,
                        Reject = dr["reject"] != DBNull.Value ? Convert.ToInt32(dr["reject"]) : 0
                    });
                }
                //while (dr.Read())
                //{
                //    list.Add(new TenderDto
                //    {
                //        TenderId = Convert.ToInt32(dr["TENDER_ID"]),
                //        TenderNo = dr["TENDER_NO"].ToString(),
                //        TenderDate = dr["TENDER_DATE"].ToString(),
                //        TenderDescription = dr["TENDER_DESCRIPTION"].ToString(),
                //        TotalItems = Convert.ToInt32(dr["totali"]),
                //        Found = Convert.ToInt32(dr["found"]),
                //        NotFound = Convert.ToInt32(dr["nosNotFound"]),
                //        PriceEntry = Convert.ToInt32(dr["PriceEntry"]),
                //        Accept = Convert.ToInt32(dr["accept"]),
                //        Reject = Convert.ToInt32(dr["reject"]),
                //        Status = dr["cStatus"].ToString()
                //    });
                //}
            }

            return Ok(list);
        }






    }

}

