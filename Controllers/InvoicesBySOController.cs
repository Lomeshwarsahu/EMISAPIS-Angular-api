using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EMISAPIS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InvoicesBySOController : ControllerBase
    {
        private readonly IConfiguration _config;
        public InvoicesBySOController(IConfiguration config) => _config = config;
        private string ConnStr() => _config.GetConnectionString("DefaultConnection")!;

        [HttpGet("header")]
        public async Task<IActionResult> GetHeader([FromQuery] int poNoId)
        {
            try
            {
                using var conn = new SqlConnection(ConnStr());
                await conn.OpenAsync();
                var sql = @"
SELECT p.po_id, p.outward_no + '/' + p.po_no po_no,
CONVERT(varchar,p.po_date,103) po_date, sp.name supplier_name
FROM purchase_order p
INNER JOIN massuppliers sp ON sp.supplier_id = p.supplier_id
WHERE p.po_id = @poNoId";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@poNoId", poNoId);
                using var dr = await cmd.ExecuteReaderAsync();
                if (await dr.ReadAsync())
                {
                    return Ok(new InvoiceSoHeaderDto
                    {
                        PoNoId = Convert.ToInt32(dr["po_id"]),
                        PoNo = dr["po_no"]?.ToString() ?? "",
                        PoDate = dr["po_date"]?.ToString() ?? "",
                        SupplierName = dr["supplier_name"]?.ToString() ?? ""
                    });
                }
                return Ok(new InvoiceSoHeaderDto());
            }
            catch { return Ok(new InvoiceSoHeaderDto()); }
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetList([FromQuery] int poNoId)
        {
            try
            {
                var list = new List<InvoiceSoLineDto>();
                using var conn = new SqlConnection(ConnStr());
                await conn.OpenAsync();
                var sql = @"
SELECT inv_id, invoice_no, CONVERT(varchar,invoice_date,103) invoice_date,
invoice_value, ISNULL(cgst,0) cgst, ISNULL(sgst,0) sgst
FROM invoices WHERE po_id = @poNoId ORDER BY inv_id";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@poNoId", poNoId);
                using var dr = await cmd.ExecuteReaderAsync();
                while (await dr.ReadAsync())
                {
                    list.Add(new InvoiceSoLineDto
                    {
                        InvoiceId = Convert.ToInt32(dr["inv_id"]),
                        InvoiceNo = dr["invoice_no"]?.ToString() ?? "",
                        InvoiceDate = dr["invoice_date"]?.ToString() ?? "",
                        InvoiceValue = Convert.ToDecimal(dr["invoice_value"]),
                        Cgst = Convert.ToDecimal(dr["cgst"]),
                        Sgst = Convert.ToDecimal(dr["sgst"])
                    });
                }
                return Ok(list);
            }
            catch { return Ok(new List<InvoiceSoLineDto>()); }
        }

        [HttpPost("save")]
        public async Task<IActionResult> Save([FromBody] InvoiceSoSaveRequestDto dto)
        {
            try
            {
                using var conn = new SqlConnection(ConnStr());
                await conn.OpenAsync();
                if (dto.InvoiceId > 0)
                {
                    var sql = @"UPDATE invoices SET invoice_no=@invNo, invoice_date=@invDate,
invoice_value=@invValue, cgst=@cgst, sgst=@sgst WHERE inv_id=@id";
                    using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@id", dto.InvoiceId);
                    cmd.Parameters.AddWithValue("@invNo", dto.InvoiceNo);
                    cmd.Parameters.AddWithValue("@invDate", dto.InvoiceDate);
                    cmd.Parameters.AddWithValue("@invValue", dto.InvoiceValue);
                    cmd.Parameters.AddWithValue("@cgst", dto.Cgst);
                    cmd.Parameters.AddWithValue("@sgst", dto.Sgst);
                    await cmd.ExecuteNonQueryAsync();
                }
                else
                {
                    var sql = @"INSERT INTO invoices (po_id, invoice_no, invoice_date, invoice_value, cgst, sgst)
VALUES (@poId, @invNo, @invDate, @invValue, @cgst, @sgst)";
                    using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@poId", dto.PoNoId);
                    cmd.Parameters.AddWithValue("@invNo", dto.InvoiceNo);
                    cmd.Parameters.AddWithValue("@invDate", dto.InvoiceDate);
                    cmd.Parameters.AddWithValue("@invValue", dto.InvoiceValue);
                    cmd.Parameters.AddWithValue("@cgst", dto.Cgst);
                    cmd.Parameters.AddWithValue("@sgst", dto.Sgst);
                    await cmd.ExecuteNonQueryAsync();
                }
                return Ok(new { message = "Invoice saved" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
