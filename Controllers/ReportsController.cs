using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Net;
using System.Net.NetworkInformation;

namespace EMISAPIS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportsController : Controller
    {
        private readonly string _connectionString;

        public ReportsController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        //dropdownlist
        [HttpGet("{directorateId}")]
        public async Task<IActionResult> GetItemlist(int directorateId)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            //string query = @"
            //           select distinct item_id,item_name from
            //           (SELECT a.item_id, A.ITEM_CODE_AS_PER_TENDER, A.ITEM_CODE_AS_PER_TENDER +'-' + A.item_name as item_name FROM MASITEMS A inner join
            //           (select distinct pi.item_id from po_items pi inner join purchase_order p on p.po_id = pi.po_id 
            //            where 1 = 1 and p.directorate_id = 5)
            //            p on p.item_id = a.item_id where A. PARENT_ITEM_ID is not null ) a";
            string query = @"
            SELECT DISTINCT item_id, item_name
            FROM
            (
                SELECT a.item_id, A.ITEM_CODE_AS_PER_TENDER, A.ITEM_CODE_AS_PER_TENDER + '-' + A.item_name AS item_name
                FROM MASITEMS A
                INNER JOIN 
                (
                    SELECT DISTINCT pi.item_id
                    FROM po_items pi
                    INNER JOIN purchase_order p ON p.po_id = pi.po_id
                    WHERE  1 = 1 and p.directorate_id = @DirectorateId
                ) p ON p.item_id = a.item_id
                WHERE A.PARENT_ITEM_ID IS NOT NULL
            ) a
        ";

            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@DirectorateId", directorateId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            List<ItemDTO> itemsList = new List<ItemDTO>();

            while (await reader.ReadAsync())
            {
                var item = new ItemDTO
                {
                    item_id = reader["item_id"] != DBNull.Value ? Convert.ToInt32(reader["item_id"]) : 0,
                    item_name = reader["item_name"] != DBNull.Value ? reader["item_name"].ToString() : string.Empty
                };

                itemsList.Add(item);
            }

            if (itemsList.Count == 0)
                return NotFound("No items found");

            return Ok(itemsList);
        }


        // GET: api/reports/itemwisedetail/{directorateId}
       //ItemsFeature list
        [HttpGet("itemwisedetail/{item_id}")]
        public async Task<IActionResult> Getitemwisedetail(int item_id)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            string query = @"
            select p.financial_year_id, m.item_code_as_per_tender, p.po_id,t.tender_no,f.year,p.outward_no,p.po_no,p.po_date,p.directorate_id,dir.facility_aut_name,m.item_code_as_per_tender,m.item_name,sp.name as Supplier,pi.quantity as POQTY,Supplyqty,re.receiptQTY,
red.LastRDate
,ins.insqty,case when isnull(p.potype, 'NP') = 'NP' then 'Normal PO' else 'Covid Po' end potype
, pi.quantity - isnull(Supplyqty, 0) as balanceToDispatch
,case when isnull(Supplyqty, 0) > 0 then isnull(Supplyqty,0)-isnull(re.receiptQTY, 0) else pi.quantity end  as BalToReceipt
,case when isnull(re.receiptQTY, 0) > 0 then isnull(Supplyqty,0)-isnull(ins.insqty, 0) else  case when isnull(re.receiptQTY, 0) = 0 then pi.quantity else isnull(re.receiptQTY, 0) end end  as BalToInstall


from purchase_order p
 inner
join massuppliers sp on sp.supplier_id = p.supplier_id
 inner
join mas_financial_year f on f.financial_year_id = p.financial_year_id
 left outer join
 (
select sum(pi.quantity) as quantity,pi.po_id,pi.item_id from po_items pi
group by pi.po_id, pi.item_id
) pi on pi.po_id = p.po_id
 inner join tenders t on t.tender_id = p.tender_id
 inner join masitems m on m.item_id = pi.item_id
 inner join facility_aut dir on dir.facility_aut_id = p.directorate_id
  left outer join
 (
 select po_id, isnull(sum(Supplyqty), 0) as Supplyqty  from SupplierDispatch d
inner
                                                       join Issue_item_details i on d.Issue_id = i.Issue_id
inner
                                                       join maslocations u on u.location_id = d.location_id
where d.status = 'C'
                                                       group by po_id
 ) as sup on sup.po_id = pi.po_id


 left outer join
 (
 select isnull(sum(r.receipt_qty), 0) as receiptQTY, r.po_id
 from receipts r
where r.recieved_date is not null and r.status in ('C', 'Received')
group by po_id
 ) as re on re.po_id = pi.po_id

  left outer join
 (
 select sum(ri.received_qty) as insqty, r.po_id from receipts r
left outer join receipt_item_details ri on ri.receipt_id = r.receipt_id
where r.recieved_date is not null and r.status in ('C')
group by r.po_id
 ) as ins on ins.po_id = pi.po_id


  left outer join
 (
 select max(r.recieved_date)LastRDate, r.po_id from receipts r
left outer join receipt_item_details ri on ri.receipt_id = r.receipt_id
where r.recieved_date is not null and r.status in ('C', 'Received')
group by r.po_id
 ) as red on red.po_id = pi.po_id


 where p.status in ('Order Placed', 'Completed')
and m.item_id =@item_id
 order by p.po_date desc";
        //    string query = @"
        //    SELECT DISTINCT item_id, item_name
        //    FROM
        //    (
        //        SELECT a.item_id, A.ITEM_CODE_AS_PER_TENDER, A.ITEM_CODE_AS_PER_TENDER + '-' + A.item_name AS item_name
        //        FROM MASITEMS A
        //        INNER JOIN 
        //        (
        //            SELECT DISTINCT pi.item_id
        //            FROM po_items pi
        //            INNER JOIN purchase_order p ON p.po_id = pi.po_id
        //            WHERE  1 = 1 and p.directorate_id = @DirectorateId
        //        ) p ON p.item_id = a.item_id
        //        WHERE A.PARENT_ITEM_ID IS NOT NULL
        //    ) a
        //";

            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@item_id", item_id);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            List<ItemWiseDetailDTO> itemsList = new List<ItemWiseDetailDTO>();

            while (await reader.ReadAsync())
            {
                var item = new ItemWiseDetailDTO
    {
        financial_year_id = reader["financial_year_id"] != DBNull.Value ? Convert.ToInt32(reader["financial_year_id"]) : 0,
        item_code_as_per_tender = reader["item_code_as_per_tender"]?.ToString(),
        po_id = reader["po_id"] != DBNull.Value ? Convert.ToInt32(reader["po_id"]) : 0,
        tender_no = reader["tender_no"]?.ToString(),
        year = reader["year"]?.ToString(),
        outward_no = reader["outward_no"]?.ToString(),
        po_no = reader["po_no"]?.ToString(),
        po_date = reader["po_date"] != DBNull.Value ? Convert.ToDateTime(reader["po_date"]) : null,
        directorate_id = reader["directorate_id"] != DBNull.Value ? Convert.ToInt32(reader["directorate_id"]) : 0,
        facility_aut_name = reader["facility_aut_name"]?.ToString(),
        item_name = reader["item_name"]?.ToString(),
        Supplier = reader["Supplier"]?.ToString(),
        POQTY = reader["POQTY"] != DBNull.Value ? Convert.ToDecimal(reader["POQTY"]) : (decimal?)null,
        Supplyqty = reader["Supplyqty"] != DBNull.Value ? Convert.ToDecimal(reader["Supplyqty"]) : (decimal?)null,
        receiptQTY = reader["receiptQTY"] != DBNull.Value ? Convert.ToDecimal(reader["receiptQTY"]) : (decimal?)null,
        LastRDate = reader["LastRDate"] != DBNull.Value ? Convert.ToDateTime(reader["LastRDate"]) : null,
        insqty = reader["insqty"] != DBNull.Value ? Convert.ToDecimal(reader["insqty"]) : (decimal?)null,
        potype = reader["potype"]?.ToString(),
        balanceToDispatch = reader["balanceToDispatch"] != DBNull.Value ? Convert.ToDecimal(reader["balanceToDispatch"]) : (decimal?)null,
        BalToReceipt = reader["BalToReceipt"] != DBNull.Value ? Convert.ToDecimal(reader["BalToReceipt"]) : (decimal?)null,
        BalToInstall = reader["BalToInstall"] != DBNull.Value ? Convert.ToDecimal(reader["BalToInstall"]) : (decimal?)null,
    };
                itemsList.Add(item);
            }

            if (itemsList.Count == 0)
                return NotFound("No items found");

            return Ok(itemsList);
        }


        //clickby item?
        // GET: api/reports/itemfull/{itemCode}/{financialYearId}/{poId}
        [HttpGet("itemfull/{itemCode}/{financialYearId}/{poId}")]
        public async Task<IActionResult> GetItemFullDetail(string itemCode, int financialYearId, int poId)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            string query = @"
            SELECT p.po_id, t.tender_no, f.year, p.outward_no, p.po_no,
                   CONVERT(VARCHAR, p.po_date, 103) AS po_date, p.directorate_id,
                   dir.facility_aut_name, m.item_code_as_per_tender, m.item_name,
                   sp.name AS Supplier, d.DBStart_Name_En, l.location_name,
                   pi.quantity AS POQTY, ISNULL(Supplyqty,0) AS Supplyqty,
                   ISNULL(re.receiptQTY,0) AS receiptQTY, ISNULL(ins.insqty,0) AS insqty,
                   CASE WHEN ISNULL(p.potype,'NP')='NP' THEN 'Normal PO' ELSE 'Covid Po' END AS potype,
                   pi.quantity - ISNULL(Supplyqty,0) AS balanceToDispatch,
                   CASE WHEN ISNULL(Supplyqty,0) > 0 THEN ISNULL(Supplyqty,0) - ISNULL(re.receiptQTY,0) ELSE pi.quantity END AS BalToReceipt,
                   CASE WHEN ISNULL(re.receiptQTY,0) > 0 THEN ISNULL(Supplyqty,0)-ISNULL(ins.insqty,0)
                        ELSE CASE WHEN ISNULL(re.receiptQTY,0)=0 THEN pi.quantity ELSE ISNULL(re.receiptQTY,0) END END AS BalToInstall
            FROM purchase_order p
            INNER JOIN massuppliers sp ON sp.supplier_id=p.supplier_id
            INNER JOIN mas_financial_year f ON f.financial_year_id=p.financial_year_id
            INNER JOIN (
                SELECT SUM(pi.quantity) AS quantity, pi.po_id, pi.item_id, pi.consignee_id
                FROM po_items pi
                GROUP BY pi.po_id, pi.item_id, pi.consignee_id
            ) pi ON pi.po_id=p.po_id
            INNER JOIN tenders t ON t.tender_id=p.tender_id
            INNER JOIN masitems m ON m.item_id=pi.item_id
            INNER JOIN facility_aut dir ON dir.facility_aut_id=p.directorate_id
            INNER JOIN maslocations l ON l.location_id=pi.consignee_id
            INNER JOIN Districts d ON d.DP_DistrictID=l.DP_DistrictID
            LEFT JOIN (
                SELECT po_id, ISNULL(SUM(Supplyqty),0) AS Supplyqty, d.location_id
                FROM SupplierDispatch d
                INNER JOIN Issue_item_details i ON d.Issue_id=i.Issue_id
                WHERE d.status='C'
                GROUP BY po_id,d.location_id
            ) AS sup ON sup.po_id=pi.po_id AND sup.location_id=pi.consignee_id
            LEFT JOIN (
                SELECT ISNULL(SUM(r.receipt_qty),0) AS receiptQTY, r.po_id, r.location_id
                FROM receipts r
                WHERE r.recieved_date IS NOT NULL AND r.status IN ('C','Received')
                GROUP BY r.po_id, r.location_id
            ) AS re ON re.po_id=pi.po_id AND re.location_id=pi.consignee_id
            LEFT JOIN (
                SELECT SUM(ri.received_qty) AS insqty, r.po_id, r.location_id
                FROM receipts r
                LEFT JOIN receipt_item_details ri ON ri.receipt_id=r.receipt_id
                WHERE r.recieved_date IS NOT NULL AND r.status='C'
                GROUP BY r.po_id, r.location_id
            ) AS ins ON ins.po_id=pi.po_id AND ins.location_id=pi.consignee_id
            WHERE p.status IN ('Order Placed','Completed')
              AND m.item_code_as_per_tender=@ItemCode
              AND p.financial_year_id=@FinancialYearId
              AND p.po_id=@PoId
            ORDER BY p.po_no DESC;
        ";

            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@ItemCode", itemCode);
            cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId);
            cmd.Parameters.AddWithValue("@PoId", poId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            List<ItemWiseFullDTO> result = new List<ItemWiseFullDTO>();

            while (await reader.ReadAsync())
            {
                result.Add(new ItemWiseFullDTO
                {
                    po_id = reader["po_id"] != DBNull.Value ? Convert.ToInt32(reader["po_id"]) : 0,
                    tender_no = reader["tender_no"]?.ToString(),
                    year = reader["year"]?.ToString(),
                    outward_no = reader["outward_no"]?.ToString(),
                    po_no = reader["po_no"]?.ToString(),
                    po_date = reader["po_date"]?.ToString(),
                    directorate_id = reader["directorate_id"] != DBNull.Value ? Convert.ToInt32(reader["directorate_id"]) : 0,
                    facility_aut_name = reader["facility_aut_name"]?.ToString(),
                    item_code_as_per_tender = reader["item_code_as_per_tender"]?.ToString(),
                    item_name = reader["item_name"]?.ToString(),
                    Supplier = reader["Supplier"]?.ToString(),
                    DBStart_Name_En = reader["DBStart_Name_En"]?.ToString(),
                    location_name = reader["location_name"]?.ToString(),
                    POQTY = reader["POQTY"] != DBNull.Value ? Convert.ToDecimal(reader["POQTY"]) : (decimal?)null,
                    Supplyqty = reader["Supplyqty"] != DBNull.Value ? Convert.ToDecimal(reader["Supplyqty"]) : (decimal?)null,
                    receiptQTY = reader["receiptQTY"] != DBNull.Value ? Convert.ToDecimal(reader["receiptQTY"]) : (decimal?)null,
                    insqty = reader["insqty"] != DBNull.Value ? Convert.ToDecimal(reader["insqty"]) : (decimal?)null,
                    potype = reader["potype"]?.ToString(),
                    balanceToDispatch = reader["balanceToDispatch"] != DBNull.Value ? Convert.ToDecimal(reader["balanceToDispatch"]) : (decimal?)null,
                    BalToReceipt = reader["BalToReceipt"] != DBNull.Value ? Convert.ToDecimal(reader["BalToReceipt"]) : (decimal?)null,
                    BalToInstall = reader["BalToInstall"] != DBNull.Value ? Convert.ToDecimal(reader["BalToInstall"]) : (decimal?)null
                });
            }

            if (result.Count == 0)
                return NotFound("No records found.");

            return Ok(result);
        }
    




}
}
