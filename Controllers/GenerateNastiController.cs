using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EMISAPIS.Controllers
{
    [Route("api/[Controller]")]
    [ApiController]


    public class GenerateNastiController : Controller
    {
        private readonly string _connectionString;

        private readonly IConfiguration _config; //  IConfiguration ko add kiya gaya hai

        public GenerateNastiController(IConfiguration configuration)
        {
            _config = configuration; //  config ko yahan initialize kiya hai
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        //  GET year
        //[HttpGet]
        [HttpGet("Getyear")]
        public async Task<IActionResult> GetYears()
        {
            var years = new List<yearsDTO>();

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            using SqlCommand cmd = new SqlCommand("select financial_year_id,year  from mas_financial_year order by OrderDP desc", con);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {

                years.Add(new yearsDTO
                {
                    financial_year_id = reader["financial_year_id"] != DBNull.Value ? Convert.ToInt32(reader["financial_year_id"]) : 0,
                    year = reader["year"].ToString(),

                });
            }

            return Ok(years);
        }



        [HttpGet("GetPODetails")]
        public async Task<IActionResult> GetPODetails(
    string pono,
    string outwardNo,
    string financialYearId)
        {

            using SqlConnection con = new SqlConnection(_connectionString);
            //using SqlConnection con = new SqlConnection(_config.GetConnectionString("Default"));

            string query = @"
select distinct
    so.po_id as PONOID,
    so.po_no as PONo,
    convert(varchar,so.po_date,103) as PODT,
    mc.tender_no as SchemeCode,
    m.item_code_as_per_tender as ItemCode,
    s.name as SupplierName,
    s.supplier_id as SupplierId,
    isnull(so.fileNo,'-') as FileNo,
    convert(varchar,so.fileDT,103) as FileDT
from purchase_order so 
inner join po_items oi on oi.po_id = so.po_id
inner join masitems m on m.item_id = oi.item_id
inner join tenders mc on mc.tender_id = so.tender_id
inner join massuppliers s on s.supplier_id = so.supplier_id
where so.status not in ('Incomplete','Waiting For Approval','Cancelled')
";

            SqlCommand cmd = new SqlCommand();
            cmd.Connection = con;
            cmd.CommandTimeout = 120;

            if (!string.IsNullOrEmpty(pono))
            {
                query += " AND so.po_no LIKE @pono";
                cmd.Parameters.AddWithValue("@pono", "%" + pono + "%");
            }
            else if (!string.IsNullOrEmpty(outwardNo) && !string.IsNullOrEmpty(financialYearId))
            {
                query += " AND so.financial_year_id = @fy AND so.outward_no = @outward";
                cmd.Parameters.AddWithValue("@fy", financialYearId);
                cmd.Parameters.AddWithValue("@outward", outwardNo);
            }

            cmd.CommandText = query;

            await con.OpenAsync();

            List<PODetailsDto> list = new();

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new PODetailsDto
                {
                    PONOID = Convert.ToInt32(reader["PONOID"]),
                    PONo = reader["PONo"].ToString(),
                    PODT = reader["PODT"].ToString(),
                    SchemeCode = reader["SchemeCode"].ToString(),
                    ItemCode = reader["ItemCode"].ToString(),
                    SupplierName = reader["SupplierName"].ToString(),
                    SupplierId = Convert.ToInt32(reader["SupplierId"]),
                    FileNo = reader["FileNo"].ToString(),
                    FileDT = reader["FileDT"].ToString()
                });
            }

            return Ok(list);
        }
    



//Outputter data
[HttpPut("UpdateFileNo")]
        public async Task<IActionResult> UpdateFileNo([FromBody] UpdateFileNoDto model)
        {
            if (string.IsNullOrWhiteSpace(model.FileNo))
                return BadRequest("Please Enter File No");

            //using SqlConnection con = new SqlConnection(_config.GetConnectionString("Default"));
            using SqlConnection con = new SqlConnection(_connectionString);

            string query = @"
        UPDATE purchase_order
        SET fileno = @fileno,
            fileDT = @fileDT,
            FileEntryDT = GETDATE()
        WHERE po_id = @poid";

            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.CommandTimeout = 120;

            cmd.Parameters.AddWithValue("@fileno", model.FileNo);
            cmd.Parameters.AddWithValue("@fileDT", model.FileDate);
            cmd.Parameters.AddWithValue("@poid", model.PoId);

            await con.OpenAsync();
            int rows = await cmd.ExecuteNonQueryAsync();

            if (rows == 0)
                return NotFound("PO not found");

            return Ok("Generated Successfully");
        }
    }
}
