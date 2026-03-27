using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EMISAPIS.Controllers
{
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








    }

}

