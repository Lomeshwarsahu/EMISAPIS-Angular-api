using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EMISAPIS.Controllers
{
    /// <summary>EMSRole/Reports/CMCdetail.aspx</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DMEReportsController : ControllerBase
    {
        private readonly string _connectionString;

        public DMEReportsController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection missing.");
        }

        [HttpGet("cmc-items")]
        public async Task<IActionResult> GetCmcItems()
        {
            const string sql = @"
SELECT A.ITEM_CODE_AS_PER_TENDER, A.item_code_as_per_tender + '-' + A.item_name AS item_name
FROM dbo.MASITEMS A
INNER JOIN dbo.tender_items TI ON TI.item_id = A.item_id
WHERE A.item_code_as_per_tender IS NOT NULL
ORDER BY A.ITEM_CODE_AS_PER_TENDER";

            var list = new List<CmcItemOptionDto> { new() { ItemCodeAsPerTender = "0", ItemName = "--ALL--" } };

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new CmcItemOptionDto
                    {
                        ItemCodeAsPerTender = reader["ITEM_CODE_AS_PER_TENDER"]?.ToString() ?? string.Empty,
                        ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading items.", detail = ex.Message });
            }
        }

        [HttpGet("cmc-tenders")]
        public async Task<IActionResult> GetCmcTenders([FromQuery] string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode) || itemCode == "0")
                return Ok(new List<CmcTenderOptionDto> { new() { TenderId = 0, TenderNo = "--Select--" } });

            const string sql = @"
SELECT i.item_id, t.tender_id, t.tender_no
FROM dbo.masitems i
INNER JOIN dbo.tender_items ti ON ti.item_id = i.item_id
INNER JOIN dbo.tenders t ON t.tender_id = ti.tender_id
WHERE i.item_code_as_per_tender = @ItemCode";

            var list = new List<CmcTenderOptionDto> { new() { TenderId = 0, TenderNo = "--Select--" } };

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ItemCode", itemCode.Trim());
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new CmcTenderOptionDto
                    {
                        TenderId = Convert.ToInt32(reader["tender_id"]),
                        TenderNo = reader["tender_no"]?.ToString() ?? string.Empty,
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading tenders.", detail = ex.Message });
            }
        }

        [HttpGet("cmc-detail")]
        public async Task<IActionResult> GetCmcDetail(
            [FromQuery] string? itemCode = null,
            [FromQuery] int tenderId = 0)
        {
            var sql = @"
SELECT i.item_id, t.tender_id, i.item_code, i.item_code_as_per_tender, i.item_name, t.tender_no,
       tp.CMC1, tp.CMC2, tp.CMC3, tp.CMC4, tp.CMC5
FROM dbo.masitems i
INNER JOIN dbo.tender_items ti ON ti.item_id = i.item_id
INNER JOIN dbo.tenders t ON t.tender_id = ti.tender_id
INNER JOIN dbo.live_tender_price tp ON tp.tender_item_id = ti.tender_item_id
WHERE 1 = 1
  AND (@ItemCode IS NULL OR @ItemCode = '' OR @ItemCode = '0' OR i.item_code_as_per_tender = @ItemCode)
  AND (@TenderId = 0 OR t.tender_id = @TenderId)
ORDER BY i.item_code_as_per_tender";

            var list = new List<CmcDetailRowDto>();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ItemCode", string.IsNullOrWhiteSpace(itemCode) ? DBNull.Value : itemCode.Trim());
                cmd.Parameters.AddWithValue("@TenderId", tenderId);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new CmcDetailRowDto
                    {
                        ItemId = Convert.ToInt32(reader["item_id"]),
                        TenderId = Convert.ToInt32(reader["tender_id"]),
                        ItemCode = reader["item_code"]?.ToString() ?? string.Empty,
                        ItemCodeAsPerTender = reader["item_code_as_per_tender"]?.ToString() ?? string.Empty,
                        ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                        TenderNo = reader["tender_no"]?.ToString() ?? string.Empty,
                        Cmc1 = reader["CMC1"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["CMC1"]),
                        Cmc2 = reader["CMC2"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["CMC2"]),
                        Cmc3 = reader["CMC3"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["CMC3"]),
                        Cmc4 = reader["CMC4"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["CMC4"]),
                        Cmc5 = reader["CMC5"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["CMC5"]),
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading CMC detail.", detail = ex.Message });
            }
        }

        /// <summary>ReportPOEligible.aspx — financial year options.</summary>
        [HttpGet("eligible-financial-years")]
        public async Task<IActionResult> GetEligibleFinancialYears()
        {
            const string sql = @"SELECT financial_year_id, year FROM dbo.mas_financial_year WHERE financial_year_id > 5 ORDER BY orderdp DESC";
            var list = new List<KeyValuePair<int, string>>();

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new KeyValuePair<int, string>(
                        Convert.ToInt32(reader["financial_year_id"]),
                        reader["year"]?.ToString() ?? string.Empty));
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading financial years.", detail = ex.Message });
            }
        }

        /// <summary>ReportPOEligible.aspx — directorate options.</summary>
        [HttpGet("eligible-directorates")]
        public async Task<IActionResult> GetEligibleDirectorates()
        {
            const string sql = @"SELECT facility_aut_name, facility_aut_id FROM FACILITY_AUT WHERE ordercase IS NOT NULL ORDER BY ordercase";
            var list = new List<KeyValuePair<int, string>>();

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new KeyValuePair<int, string>(
                        reader["facility_aut_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["facility_aut_id"]),
                        reader["facility_aut_name"]?.ToString() ?? string.Empty));
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading directorates.", detail = ex.Message });
            }
        }

        /// <summary>ReportPOEligible.aspx — eligible items for the item dropdown.</summary>
        [HttpGet("eligible-items")]
        public async Task<IActionResult> GetEligibleItems()
        {
            const string sql = @"
SELECT DISTINCT item_code_as_per_tender, item_name FROM
(
    SELECT m.item_code_as_per_tender, m.item_name, f.facility_aut_code, ml.location_name,
    SUM(i.indent_quantity) indentQTY, ISNULL(pi.POQTY, 0) AS POQTY,
    SUM(i.indent_quantity) - ISNULL(pi.POQTY, 0) AS BalancePO, ind.facility_id,
    rc.name, rc.tender_no, rc.basic_rate, rc.percentage, f.facility_aut_id, m.item_id
    FROM indent_items i
    INNER JOIN masitems m ON m.item_id = i.item_id
    INNER JOIN indent ind ON ind.indent_id = i.indent_id
    INNER JOIN maslocations ml ON ml.location_id = ind.facility_id
    INNER JOIN facility_aut f ON f.facility_aut_id = ml.authority
    LEFT OUTER JOIN
    (
        SELECT consignee_id, SUM(pi.quantity) POQTY, pi.item_id FROM po_items pi
        INNER JOIN purchase_order p ON p.po_id = pi.po_id
        WHERE p.status NOT IN ('Incomplete', 'Waiting For Approval', 'Cancelled')
        GROUP BY consignee_id, pi.item_id
    ) pi ON pi.item_id = m.item_id AND pi.consignee_id = ind.facility_id
    LEFT OUTER JOIN
    (
        SELECT ci.item_id, ci.basic_rate, ci.percentage, s.name, t.tender_no FROM contract_items ci
        INNER JOIN award_of_contract ac ON ac.award_of_contract_id = ci.award_of_contract_id
        INNER JOIN massuppliers s ON s.supplier_id = ac.supplier_id
        INNER JOIN tenders t ON t.tender_id = ac.tender_id
        WHERE GETDATE() BETWEEN ac.contract_date AND ac.contract_end_date
    ) rc ON rc.item_id = m.item_id
    WHERE i.indent_quantity > ISNULL(pi.POQTY, 0) AND rc.basic_rate IS NOT NULL
    GROUP BY i.indent_id, i.indent_item_id, ind.facility_id, m.item_code_as_per_tender, m.item_id,
    pi.POQTY, ml.location_name, m.item_name, rc.name, rc.tender_no, rc.basic_rate, rc.percentage,
    f.facility_aut_code, f.facility_aut_id
) x GROUP BY item_code_as_per_tender, item_name ORDER BY x.item_name";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                var list = new List<KeyValuePair<string, string>>();
                while (await reader.ReadAsync())
                {
                    list.Add(new KeyValuePair<string, string>(
                        reader["item_code_as_per_tender"]?.ToString() ?? string.Empty,
                        reader["item_name"]?.ToString() ?? string.Empty));
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading eligible items.", detail = ex.Message });
            }
        }

        /// <summary>ReportPOEligible.aspx — directorate-wise summary grid.</summary>
        [HttpGet("eligible-summary")]
        public async Task<IActionResult> GetEligibleSummary()
        {
            const string sql = @"
SELECT facility_aut_id, facility_aut_code, COUNT(DISTINCT facility_id) NoofConsinee, COUNT(DISTINCT item_id) noof_items
FROM (
    SELECT m.item_code_as_per_tender, m.item_name, f.facility_aut_code, ml.location_name,
    SUM(i.indent_quantity) indentQTY, ISNULL(pi.POQTY, 0) AS POQTY,
    SUM(i.indent_quantity) - ISNULL(pi.POQTY, 0) AS BalancePO, ind.facility_id,
    rc.name, rc.tender_no, rc.basic_rate, rc.percentage, f.facility_aut_id, m.item_id
    FROM indent_items i
    INNER JOIN masitems m ON m.item_id = i.item_id
    INNER JOIN indent ind ON ind.indent_id = i.indent_id
    INNER JOIN maslocations ml ON ml.location_id = ind.facility_id
    INNER JOIN facility_aut f ON f.facility_aut_id = ml.authority
    LEFT OUTER JOIN
    (
        SELECT consignee_id, SUM(pi.quantity) POQTY, pi.item_id FROM po_items pi
        INNER JOIN purchase_order p ON p.po_id = pi.po_id
        WHERE p.status NOT IN ('Incomplete', 'Waiting For Approval', 'Cancelled')
        GROUP BY consignee_id, pi.item_id
    ) pi ON pi.item_id = m.item_id AND pi.consignee_id = ind.facility_id
    LEFT OUTER JOIN
    (
        SELECT ci.item_id, ci.basic_rate, ci.percentage, s.name, t.tender_no FROM contract_items ci
        INNER JOIN award_of_contract ac ON ac.award_of_contract_id = ci.award_of_contract_id
        INNER JOIN massuppliers s ON s.supplier_id = ac.supplier_id
        INNER JOIN tenders t ON t.tender_id = ac.tender_id
        WHERE GETDATE() BETWEEN ac.contract_date AND ac.contract_end_date
    ) rc ON rc.item_id = m.item_id
    WHERE i.indent_quantity > ISNULL(pi.POQTY, 0) AND rc.basic_rate IS NOT NULL
    GROUP BY i.indent_id, i.indent_item_id, ind.facility_id, m.item_code_as_per_tender, m.item_id,
    pi.POQTY, ml.location_name, m.item_name, rc.name, rc.tender_no, rc.basic_rate, rc.percentage,
    f.facility_aut_code, f.facility_aut_id
) x GROUP BY facility_aut_id, facility_aut_code";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                var list = new List<object>();
                while (await reader.ReadAsync())
                {
                    list.Add(new
                    {
                        FacilityAutCode = reader["facility_aut_code"]?.ToString() ?? string.Empty,
                        NoofConsinee = reader["NoofConsinee"] == DBNull.Value ? 0 : Convert.ToInt32(reader["NoofConsinee"]),
                        NoofItems = reader["noof_items"] == DBNull.Value ? 0 : Convert.ToInt32(reader["noof_items"]),
                    });
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading eligibility summary.", detail = ex.Message });
            }
        }

        /// <summary>ReportPOEligible.aspx — detail grid of eligible consignees.</summary>
        [HttpGet("eligible-detail")]
        public async Task<IActionResult> GetEligibleDetail(
            [FromQuery] int financialYearId,
            [FromQuery] int authorityId,
            [FromQuery] string? itemCode = null)
        {
            if (financialYearId <= 0 || authorityId <= 0)
                return BadRequest(new { message = "financialYearId and authorityId are required." });

            var sql = $@"
SELECT u.user_id, u.user_name, mif.year, CONVERT(VARCHAR, id.consolidated_date, 103) AS consolidated_date,
id.indent_con_no, id.description, m.item_code_as_per_tender, m.item_name, f.facility_aut_code,
SUM(i.indent_quantity) indentQTY, ISNULL(pi.POQTY, 0) AS POQTY,
SUM(i.indent_quantity) - ISNULL(pi.POQTY, 0) AS BalancePO,
f.facility_aut_id, m.item_id, pi.year AS POYear, pi.grossvalue, pi.netvalue, rc.basic_rate, rc.tender_no
FROM indent_items i
INNER JOIN masitems m ON m.item_id = i.item_id
INNER JOIN indent ind ON ind.indent_id = i.indent_id
INNER JOIN indent_cons_items ci ON ci.indent_cons_items_id = ind.indent_cons_items_id
INNER JOIN indent_consolidation id ON id.indent_consolidation_id = ci.indent_consolidated_id
INNER JOIN mas_financial_year mif ON mif.financial_year_id = id.financial_year_id
INNER JOIN maslocations ml ON ml.location_id = ind.facility_id
INNER JOIN users u ON u.user_id = ml.user_id
INNER JOIN facility_aut f ON f.facility_aut_id = ml.authority
LEFT OUTER JOIN
(
    SELECT CONVERT(VARCHAR, p.po_date, 103) AS po_date, p.po_no, mf.year, consignee_id,
    SUM(pi.quantity) POQTY, pi.item_id, pi.INDENT_CONSOLIDATION_ID, pi.directorate_id,
    pi.indent_id, pi.indent_item_id, SUM(pi.totalprice) AS grossvalue, SUM(pi.totalbasicPrice) AS netvalue
    FROM po_items pi
    INNER JOIN purchase_order p ON p.po_id = pi.po_id
    INNER JOIN mas_financial_year mf ON mf.financial_year_id = pi.financial_year_id
    WHERE p.status NOT IN ('Incomplete', 'Waiting For Approval', 'Cancelled')
    GROUP BY consignee_id, pi.item_id, pi.INDENT_CONSOLIDATION_ID, pi.directorate_id,
    pi.indent_id, pi.indent_item_id, mf.year, p.po_date, p.po_no
) pi ON pi.item_id = m.item_id AND pi.consignee_id = ind.facility_id
 AND pi.directorate_id = id.directorate_id AND pi.indent_id = ind.indent_id AND pi.indent_item_id = i.indent_item_id
LEFT OUTER JOIN
(
    SELECT ci.item_id, ci.basic_rate, ci.percentage, s.name, t.tender_no FROM contract_items ci
    INNER JOIN award_of_contract ac ON ac.award_of_contract_id = ci.award_of_contract_id
    INNER JOIN massuppliers s ON s.supplier_id = ac.supplier_id
    INNER JOIN tenders t ON t.tender_id = ac.tender_id
    WHERE GETDATE() BETWEEN ac.contract_date AND ac.contract_end_date
) rc ON rc.item_id = m.item_id
WHERE 1=1 AND f.facility_aut_id = {authorityId} AND mif.financial_year_id = {financialYearId}
AND pi.POQTY IS NULL AND rc.tender_no IS NOT NULL
GROUP BY m.item_code_as_per_tender, m.item_id, pi.POQTY, m.item_name, pi.grossvalue, pi.netvalue,
rc.name, rc.tender_no, rc.basic_rate, rc.percentage, f.facility_aut_code, f.facility_aut_id,
id.consolidated_date, mif.year, pi.year, id.indent_con_no, id.description, u.user_name, u.user_id
ORDER BY u.user_name, m.item_name";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                var list = new List<EligibleDetailRowDto>();
                while (await reader.ReadAsync())
                {
                    list.Add(new EligibleDetailRowDto
                    {
                        UserId = reader["user_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["user_id"]),
                        UserName = reader["user_name"]?.ToString() ?? string.Empty,
                        Year = reader["year"]?.ToString() ?? string.Empty,
                        ConsolidatedDate = reader["consolidated_date"]?.ToString() ?? string.Empty,
                        IndentConNo = reader["indent_con_no"]?.ToString() ?? string.Empty,
                        Description = reader["description"]?.ToString() ?? string.Empty,
                        ItemCode = reader["item_code_as_per_tender"]?.ToString() ?? string.Empty,
                        ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                        FacilityAutCode = reader["facility_aut_code"]?.ToString() ?? string.Empty,
                        IndentQty = ReadEligibleDecimal(reader["indentQTY"]),
                        PoQty = ReadEligibleDecimal(reader["POQTY"]),
                        BalancePo = ReadEligibleDecimal(reader["BalancePO"]),
                        BasicRate = ReadEligibleDecimal(reader["basic_rate"]),
                        TenderNo = reader["tender_no"]?.ToString() ?? string.Empty,
                    });
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading eligibility detail.", detail = ex.Message });
            }
        }

        private static decimal ReadEligibleDecimal(object? value)
        {
            return value == null || value == DBNull.Value ? 0m : Convert.ToDecimal(value);
        }

        /// <summary>MainEquipmentMappedReport.aspx fillgrid — item to main item type mapping.</summary>
        [HttpGet("main-equipment-mapped")]
        public async Task<IActionResult> GetMainEquipmentMapped()
        {
            const string sql = @"
select p.PItemName,
       case when m.item_code_as_per_tender is not null then m.item_code_as_per_tender else m.item_code end as itemcode,
       m.item_name, p.IsElectrical, p.ProgReq,
       case when p.SRorBulkEntry = 'S' then 'Serial No Wise' else 'Bulk' end as SRorBulkEntry,
       p.amcReq, m.item_id
from masitems m
inner join masitemP p on p.PID = m.pid
where m.pid is not null
order by p.PItemName";

            try
            {
                var list = new List<MainEquipmentMappedReportRowDto>();
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new MainEquipmentMappedReportRowDto
                    {
                        PItemName = reader["PItemName"]?.ToString() ?? string.Empty,
                        ItemCode = reader["itemcode"]?.ToString() ?? string.Empty,
                        ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                        IsElectrical = reader["IsElectrical"]?.ToString() ?? string.Empty,
                        ProgReq = reader["ProgReq"]?.ToString() ?? string.Empty,
                        SrOrBulkEntry = reader["SRorBulkEntry"]?.ToString() ?? string.Empty,
                        AmcReq = reader["amcReq"]?.ToString() ?? string.Empty,
                        ItemId = reader["item_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["item_id"]),
                    });
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading main equipment mapped report.", detail = ex.Message });
            }
        }
    }
}
