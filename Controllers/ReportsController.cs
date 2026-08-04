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
        //[HttpGet("{directorateId}")]
        [HttpGet("items/{directorateId}")]

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


        // GET: api/reports/itemwisedetail-pocell?itemId=
        [HttpGet("itemwisedetail-pocell")]
        public async Task<IActionResult> GetitemwisedetailPocell(int itemId)
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
and m.item_id =@itemId
 order by p.po_date desc";

            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@itemId", itemId);

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



        //  GET Districts
        //[HttpGet]
        [HttpGet("GetDistricts")]
        public async Task<IActionResult> GetDistricts()
        {
            var Districts = new List<DistrictsDTO>();

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            using SqlCommand cmd = new SqlCommand("select DP_DistrictID,DBStart_Name_En from Districts order by DBStart_Name_En", con);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {

                Districts.Add(new DistrictsDTO
                {
                    DP_DistrictID = reader["DP_DistrictID"] != DBNull.Value ? Convert.ToInt32(reader["DP_DistrictID"]) : 0,
                    DBStart_Name_En = reader["DBStart_Name_En"].ToString(),

                });
            }

            return Ok(Districts);
        }

        //  GET year
        //[HttpGet]
        [HttpGet("GetDiectorate")]
        public async Task<IActionResult> GetDiectorate()
        {
            var Diectorate = new List<DiectorateDTO>();

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            using SqlCommand cmd = new SqlCommand("select facility_aut_id,facility_aut_name from facility_aut order by ordercase", con);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {

                Diectorate.Add(new DiectorateDTO
                {
                    facility_aut_id = reader["facility_aut_id"] != DBNull.Value ? Convert.ToInt32(reader["facility_aut_id"]) : 0,
                    facility_aut_name = reader["facility_aut_name"].ToString(),

                });
            }

            return Ok(Diectorate);
        }

        //distrinc wise details 
        [HttpGet("GetDistrictWiseDetails")]
        public IActionResult GetDistrictWiseDetails(
                   int districtId,
                   int directorateId,
                   int financialYearId,
                   string fromDate,
                   string toDate)
        {
            List<DistrictWiseDetailDTO> list = new List<DistrictWiseDetailDTO>();

            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                con.Open();

                string query = @"
                    select distinct 
                        case when isnull(p.potype,'NP')='NP' then 'Normal PO' else 'Covid Po' end potype,
                        t.tender_no,
                        p.outward_no + '/' + p.po_no as po_no,
                        convert(varchar,p.po_date,103) as po_date,
                        ms.name as supplier_name,
                        m.item_code_as_per_tender,
                        m.item_name,
                        dis.DBStart_Name_En,
                        pi.consignee_id,
                        ml.location_name,
                        pi.quantity as po_qty,
                        pi.basicrate,
                        pi.percentage,
                        pi.totalprice,
                        case when m.categoryId = 2 then 'Reagent' else 'Equipment' end as Eqptype,
                        p.po_id,
                        isnull(sdd.supply_qty,0) as supply_qty,
                        isnull(re.receiptQTY,0) as receiptQTY,
                        isnull(ins.insqty,0) as insqty
                    from purchase_order p
                    inner join po_items pi on pi.po_id = p.po_id
                    inner join masitems m on m.item_id = pi.item_id
                    inner join maslocations ml on ml.location_id = pi.consignee_id
                    inner join Districts dis on dis.DP_DistrictID = ml.DP_DistrictID
                    inner join massuppliers ms on ms.supplier_id = p.supplier_id
                    inner join tenders t on t.tender_id = p.tender_id
                    inner join facility_aut fau on fau.facility_aut_id = p.directorate_id
                    left join (
                        select sd.po_id, sum(id.supplyqty) as supply_qty, sd.location_id 
                        from SupplierDispatch sd 
                        inner join Issue_item_details id on id.Issue_id = sd.Issue_id
                        where sd.status='C'
                        group by sd.po_id, sd.location_id
                    ) sdd on sdd.po_id=pi.po_id and sdd.location_id=pi.consignee_id
                    left join (
                        select isnull(sum(r.receipt_qty),0) as receiptQTY, r.po_id, r.location_id
                        from receipts r
                        where r.recieved_date is not null and r.status in ('C','Received')
                        group by r.po_id, r.location_id
                    ) re on re.po_id=pi.po_id and re.location_id=pi.consignee_id
                    left join (
                        select sum(ri.received_qty) as insqty, r.po_id, r.location_id
                        from receipts r
                        left join receipt_item_details ri on ri.receipt_id=r.receipt_id
                        where r.recieved_date is not null and r.status='C'
                        group by r.po_id, r.location_id
                    ) ins on ins.po_id=pi.po_id and ins.location_id=pi.consignee_id
                    where p.po_date between CONVERT(date,@fromDate,103) and CONVERT(date,@toDate,103)
                    and p.status='Order Placed'
                    and dis.DP_DistrictID=@districtId
                    and p.directorate_id=@directorateId
                    and p.financial_year_id=@financialYearId
                ";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@fromDate", fromDate);
                    cmd.Parameters.AddWithValue("@toDate", toDate);
                    cmd.Parameters.AddWithValue("@districtId", districtId);
                    cmd.Parameters.AddWithValue("@directorateId", directorateId);
                    cmd.Parameters.AddWithValue("@financialYearId", financialYearId);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new DistrictWiseDetailDTO
                            {
                                potype = reader["potype"].ToString(),
                                tender_no = reader["tender_no"].ToString(),
                                po_no = reader["po_no"].ToString(),
                                po_date = reader["po_date"].ToString(),
                                supplier_name = reader["supplier_name"].ToString(),
                                item_code_as_per_tender = reader["item_code_as_per_tender"].ToString(),
                                item_name = reader["item_name"].ToString(),
                                DBStart_Name_En = reader["DBStart_Name_En"].ToString(),
                                consignee_id = Convert.ToInt32(reader["consignee_id"]),
                                location_name = reader["location_name"].ToString(),
                                po_qty = Convert.ToDecimal(reader["po_qty"]),
                                basicrate = Convert.ToDecimal(reader["basicrate"]),
                                percentage = Convert.ToDecimal(reader["percentage"]),
                                totalprice = Convert.ToDecimal(reader["totalprice"]),
                                Eqptype = reader["Eqptype"].ToString(),
                                po_id = Convert.ToInt32(reader["po_id"]),
                                supply_qty = Convert.ToDecimal(reader["supply_qty"]),
                                receiptQTY = Convert.ToDecimal(reader["receiptQTY"]),
                                insqty = Convert.ToDecimal(reader["insqty"])
                            });
                        }
                    }
                }
            }

            return Ok(list);
        }

        //indent po summary dirwise

        // GET: api/reports/indentposummary?directorateId=5&financialYearId=19
        [HttpGet("GetIndentPOSummaryDirwise")]
        public async Task<IActionResult> GetIndentPOSummaryDirwise(int directorateId, int financialYearId)
        {
            List<IndentPOSummaryDirwiseDTO> list = new List<IndentPOSummaryDirwiseDTO>();

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            string query = @"
        select 
            convert(varchar,idc.consolidated_date,105) as IndentDT,  
            case when idc.description is not null then idc.description else 'Not Entered' end as Indent_Letter_no,
            p.outward_no + '/' + p.po_no as po_no,
            convert(varchar,(case when p.soissueDT is null then p.po_date else soissueDT end),105) as podate,
            item_code_as_per_tender,item_name,eqtype,
            sum(ii.indent_quantity) as Indent_Qty,
            sum(POQTY) as poqty,
            count(consignee_id) as no_of_consignee,
            sum(povalue) as povalue,
            idc.indent_consolidation_id,
            idc.year as indent_year,
            mfy.year as po_year  

        from purchase_order p
        inner join mas_financial_year mfy on mfy.financial_year_id = p.financial_year_id

        inner join
        (
            select po_id,quantity as POQTY,pi.consignee_id,indent_id,
                   m.item_code_as_per_tender,m.item_name,
                   case when m.categoryid=2 then 'Reagent' else 'Equipment' end as eqtype,
                   totalprice as povalue  
            from po_items pi
            inner join masitems m on m.item_id=pi.item_id
        ) pi on pi.po_id=p.po_id

        inner join indent ind on ind.indent_id=pi.indent_id
        inner join indent_items ii on ii.indent_id = ind.indent_id

        inner join 
        ( 
            select idcc.indent_consolidation_id,idcc.description,idcc.consolidated_date,mf.year 
            from indent_consolidation idcc
            inner join mas_financial_year mf on mf.financial_year_id = idcc.financial_year_id
        ) idc on idc.indent_consolidation_id=ind.indent_consolidation_id

        where p.status not in ('Incomplete','Waiting For Approval','Cancelled')
          and p.financial_year_id = @FinancialYearId
          and p.directorate_id = @DirectorateId

        group by idc.consolidated_date,idc.description,p.po_no,p.soissueDT,p.po_date,
                 item_code_as_per_tender,item_name,eqtype,
                 p.outward_no,idc.indent_consolidation_id,idc.year,mfy.year

        order by idc.description
        ";

            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@DirectorateId", directorateId);
            cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new IndentPOSummaryDirwiseDTO
                {
                    IndentDT = reader["IndentDT"]?.ToString(),
                    Indent_Letter_no = reader["Indent_Letter_no"]?.ToString(),
                    po_no = reader["po_no"]?.ToString(),
                    podate = reader["podate"]?.ToString(),
                    item_code_as_per_tender = reader["item_code_as_per_tender"]?.ToString(),
                    item_name = reader["item_name"]?.ToString(),
                    eqtype = reader["eqtype"]?.ToString(),
                    Indent_Qty = reader["Indent_Qty"] != DBNull.Value ? Convert.ToDecimal(reader["Indent_Qty"]) : 0,
                    poqty = reader["poqty"] != DBNull.Value ? Convert.ToDecimal(reader["poqty"]) : 0,
                    no_of_consignee = reader["no_of_consignee"] != DBNull.Value ? Convert.ToInt32(reader["no_of_consignee"]) : 0,
                    povalue = reader["povalue"] != DBNull.Value ? Convert.ToDecimal(reader["povalue"]) : 0,
                    indent_consolidation_id = reader["indent_consolidation_id"] != DBNull.Value ? Convert.ToInt32(reader["indent_consolidation_id"]) : 0,
                    indent_year = reader["indent_year"]?.ToString(),
                    po_year = reader["po_year"]?.ToString()
                });
            }

            if (list.Count == 0)
                return NotFound("No data found");

            return Ok(list);
        }

        // === INDENT/PO/TENDER STATUS (Page 1): Master report ===
        // GET: api/reports/indentpotenderstatus?directorateId=12&financialYearId=19&districtId=0&searchBy=
        [HttpGet("indentpotenderstatus")]
        public async Task<IActionResult> GetIndentPoTenderStatus(int directorateId, int financialYearId, int districtId, string? searchBy)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            string whereCauseUserId = "";
            string searchFilter = "";
            if (!string.IsNullOrEmpty(searchBy))
            {
                if (searchBy == "IN")
                    searchFilter = " and Ins.installedQTY is not null";
                else if (searchBy == "NR")
                    searchFilter = " and sup.dispatchQTY > 0 and Ins.installedQTY is null";
            }

            if (directorateId == 12 && districtId > 0)
                whereCauseUserId = " and u.user_id = @DistrictId";
            else if (directorateId == 5 && districtId > 0)
                whereCauseUserId = " and u.DP_DistrictID = @DistrictId";

            string query = directorateId == 12 ? @"
                select u.user_id,u.user_name, mif.year, convert(varchar,id.consolidated_date,103) as consolidated_date,
                id.indent_con_no,id.description, m.item_code_as_per_tender,m.item_name,f.facility_aut_code,
                ml.location_name, sum(i.indent_quantity) indentQTY, pi.year as POYear, pi.po_date, pi.po_no,
                isnull(pi.POQTY,0) as POQTY, cast(pi.totalprice as bigint) as POValueWithTax,
                sum(i.indent_quantity)-isnull(pi.POQTY,0) as BalancePO, ind.facility_id,
                rc.name as supplier_name, rc.tender_no, rc.basic_rate, rc.percentage,
                f.facility_aut_id, m.item_id, isnull(sup.dispatchQTY,0) as dispatchQTY,
                isnull(Ins.installedQTY,0) as installedQTY
                from indent_items i
                inner join masitems m on m.item_id=i.item_id
                inner join indent ind on ind.indent_id=i.indent_id
                inner join indent_cons_items ci on ci.indent_cons_items_id=ind.indent_cons_items_id
                inner join indent_consolidation id on id.indent_consolidation_id=ci.indent_consolidated_id
                inner join mas_financial_year mif on mif.financial_year_id=id.financial_year_id
                inner join maslocations ml on ml.location_id=ind.facility_id
                inner join users u on u.user_id=ml.user_id
                inner join facility_aut f on f.facility_aut_id=ml.authority
                left outer join (
                    select convert(varchar,p.po_date,103) as po_date, p.po_no, mf.year, consignee_id,
                    sum(pi.quantity) POQTY, pi.item_id, pi.INDENT_CONSOLIDATION_ID, pi.directorate_id,
                    pi.indent_id, pi.indent_item_id, pi.totalbasicPrice, pi.totalprice, pi.po_id
                    from po_items pi
                    inner join purchase_order p on p.po_id=pi.po_id
                    inner join mas_financial_year mf on mf.financial_year_id=pi.financial_year_id
                    where p.status not in ('Incomplete','Waiting For Approval','Cancelled')
                    group by consignee_id, pi.item_id, pi.INDENT_CONSOLIDATION_ID, pi.directorate_id,
                    pi.indent_id, pi.indent_item_id, mf.year, p.po_date, p.po_no,
                    pi.totalbasicPrice, pi.totalprice, pi.po_id
                ) pi on pi.item_id=m.item_id and pi.consignee_id=ind.facility_id
                and pi.directorate_id=id.directorate_id and pi.indent_id=ind.indent_id
                and pi.indent_item_id=i.indent_item_id
                left outer join (
                    select po_id, SUM(Supplyqty) as dispatchQTY from SupplierDispatch d
                    inner join Issue_item_details iid on d.Issue_id=iid.Issue_id
                    inner join maslocations u on u.location_id=d.location_id
                    where d.status='C' " + whereCauseUserId + @"
                    group by po_id
                ) as sup on sup.po_id=pi.po_id
                left outer join (
                    select r.po_id, sum(ri.received_qty) as installedQTY from receipts r
                    left outer join receipt_item_details ri on ri.receipt_id=r.receipt_id
                    inner join maslocations u on u.location_id=r.location_id
                    where 1=1 and ri.installation_date is not null " + whereCauseUserId + @"
                    group by r.po_id
                ) as Ins on Ins.po_id=pi.po_id
                left outer join (
                    select ci.item_id, ci.basic_rate, ci.percentage, s.name, t.tender_no
                    from contract_items ci
                    inner join award_of_contract ac on ac.award_of_contract_id=ci.award_of_contract_id
                    inner join massuppliers s on s.supplier_id=ac.supplier_id
                    inner join tenders t on t.tender_id=ac.tender_id
                    where GETDATE() between ac.contract_date and ac.contract_end_date
                ) rc on rc.item_id=m.item_id
                where id.financial_year_id=@FinancialYearId and f.facility_aut_id=@DirectorateId
                " + whereCauseUserId + @" " + searchFilter + @"
                group by i.indent_id, i.indent_item_id, ind.facility_id, m.item_code_as_per_tender,
                m.item_id, pi.POQTY, ml.location_name, m.item_name, rc.name, rc.tender_no,
                rc.basic_rate, rc.percentage, f.facility_aut_code, f.facility_aut_id,
                id.consolidated_date, mif.year, pi.year, pi.po_date, pi.po_no,
                id.indent_con_no, id.description, u.user_name, u.user_id,
                pi.totalprice, sup.dispatchQTY, Ins.installedQTY
                order by u.user_id
            " : @"
                select u.DP_DistrictID as user_id, u.DBStart_Name_En as user_name, mif.year,
                convert(varchar,id.consolidated_date,103) as consolidated_date,
                id.indent_con_no, id.description, m.item_code_as_per_tender, m.item_name,
                f.facility_aut_code, ml.location_name, sum(i.indent_quantity) indentQTY,
                pi.year as POYear, pi.po_date, pi.po_no, isnull(pi.POQTY,0) as POQTY,
                cast(pi.totalprice as bigint) as POValueWithTax,
                sum(i.indent_quantity)-isnull(pi.POQTY,0) as BalancePO, ind.facility_id,
                rc.name as supplier_name, rc.tender_no, rc.basic_rate, rc.percentage,
                f.facility_aut_id, m.item_id, isnull(sup.dispatchQTY,0) as dispatchQTY,
                isnull(Ins.installedQTY,0) as installedQTY
                from indent_items i
                inner join masitems m on m.item_id=i.item_id
                inner join indent ind on ind.indent_id=i.indent_id
                inner join indent_cons_items ci on ci.indent_cons_items_id=ind.indent_cons_items_id
                inner join indent_consolidation id on id.indent_consolidation_id=ci.indent_consolidated_id
                inner join mas_financial_year mif on mif.financial_year_id=id.financial_year_id
                inner join maslocations ml on ml.location_id=ind.facility_id
                inner join Districts u on u.DP_DistrictID=ml.DP_DistrictID
                inner join facility_aut f on f.facility_aut_id=ml.authority
                left outer join (
                    select convert(varchar,p.po_date,103) as po_date, p.po_no, mf.year, consignee_id,
                    sum(pi.quantity) POQTY, pi.item_id, pi.INDENT_CONSOLIDATION_ID, pi.directorate_id,
                    pi.indent_id, pi.indent_item_id, pi.totalbasicPrice, pi.totalprice, pi.po_id
                    from po_items pi
                    inner join maslocations l on l.location_id=pi.consignee_id
                    inner join purchase_order p on p.po_id=pi.po_id
                    inner join mas_financial_year mf on mf.financial_year_id=pi.financial_year_id
                    where 1=1 and p.status not in ('Incomplete','Waiting For Approval','Cancelled')
                    group by consignee_id, pi.item_id, pi.INDENT_CONSOLIDATION_ID, pi.directorate_id,
                    pi.indent_id, pi.indent_item_id, mf.year, p.po_date, p.po_no,
                    pi.totalbasicPrice, pi.totalprice, pi.po_id
                ) pi on pi.item_id=m.item_id and pi.consignee_id=ind.facility_id
                and pi.directorate_id=id.directorate_id and pi.indent_id=ind.indent_id
                and pi.indent_item_id=i.indent_item_id
                left outer join (
                    select po_id, SUM(Supplyqty) as dispatchQTY from SupplierDispatch d
                    inner join Issue_item_details iid on d.Issue_id=iid.Issue_id
                    inner join maslocations u on u.location_id=d.location_id
                    where d.status='C'
                    group by po_id
                ) as sup on sup.po_id=pi.po_id
                left outer join (
                    select r.po_id, sum(ri.received_qty) as installedQTY from receipts r
                    left outer join receipt_item_details ri on ri.receipt_id=r.receipt_id
                    inner join maslocations l on l.location_id=r.location_id
                    where 1=1 and ri.installation_date is not null
                    group by r.po_id
                ) as Ins on Ins.po_id=pi.po_id
                left outer join (
                    select ci.item_id, ci.basic_rate, ci.percentage, s.name, t.tender_no
                    from contract_items ci
                    inner join award_of_contract ac on ac.award_of_contract_id=ci.award_of_contract_id
                    inner join massuppliers s on s.supplier_id=ac.supplier_id
                    inner join tenders t on t.tender_id=ac.tender_id
                    where GETDATE() between ac.contract_date and ac.contract_end_date
                ) rc on rc.item_id=m.item_id
                where id.financial_year_id=@FinancialYearId and f.facility_aut_id=@DirectorateId
                " + whereCauseUserId + @" " + searchFilter + @"
                group by i.indent_id, i.indent_item_id, ind.facility_id, m.item_code_as_per_tender,
                m.item_id, pi.POQTY, ml.location_name, m.item_name, rc.name, rc.tender_no,
                rc.basic_rate, rc.percentage, f.facility_aut_code, f.facility_aut_id,
                id.consolidated_date, mif.year, pi.year, pi.po_date, pi.po_no,
                id.indent_con_no, id.description, u.DP_DistrictID, u.DBStart_Name_En,
                pi.totalprice, sup.dispatchQTY, Ins.installedQTY
                order by u.DP_DistrictID
            ";

            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@DirectorateId", directorateId);
            cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId);
            if (districtId > 0)
                cmd.Parameters.AddWithValue("@DistrictId", districtId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<IndentPoTenderStatusDTO> list = new List<IndentPoTenderStatusDTO>();
            while (await reader.ReadAsync())
            {
                var item = new IndentPoTenderStatusDTO
                {
                    user_id = reader["user_id"] != DBNull.Value ? Convert.ToInt32(reader["user_id"]) : 0,
                    user_name = reader["user_name"]?.ToString(),
                    year = reader["year"]?.ToString(),
                    consolidated_date = reader["consolidated_date"]?.ToString(),
                    indent_con_no = reader["indent_con_no"]?.ToString(),
                    description = reader["description"]?.ToString(),
                    item_code_as_per_tender = reader["item_code_as_per_tender"]?.ToString(),
                    item_name = reader["item_name"]?.ToString(),
                    facility_aut_code = reader["facility_aut_code"]?.ToString(),
                    location_name = reader["location_name"]?.ToString(),
                    indentQTY = reader["indentQTY"] != DBNull.Value ? Convert.ToDecimal(reader["indentQTY"]) : 0,
                    POYear = reader["POYear"]?.ToString(),
                    po_date = reader["po_date"]?.ToString(),
                    po_no = reader["po_no"]?.ToString(),
                    POQTY = reader["POQTY"] != DBNull.Value ? Convert.ToDecimal(reader["POQTY"]) : 0,
                    POValueWithTax = reader["POValueWithTax"] != DBNull.Value ? Convert.ToInt64(reader["POValueWithTax"]) : 0,
                    BalancePO = reader["BalancePO"] != DBNull.Value ? Convert.ToDecimal(reader["BalancePO"]) : 0,
                    facility_id = reader["facility_id"] != DBNull.Value ? Convert.ToInt32(reader["facility_id"]) : 0,
                    supplier_name = reader["supplier_name"]?.ToString(),
                    tender_no = reader["tender_no"]?.ToString(),
                    basic_rate = reader["basic_rate"] != DBNull.Value ? Convert.ToDecimal(reader["basic_rate"]) : 0,
                    percentage = reader["percentage"] != DBNull.Value ? Convert.ToDecimal(reader["percentage"]) : 0,
                    facility_aut_id = reader["facility_aut_id"] != DBNull.Value ? Convert.ToInt32(reader["facility_aut_id"]) : 0,
                    item_id = reader["item_id"] != DBNull.Value ? Convert.ToInt32(reader["item_id"]) : 0,
                    dispatchQTY = reader["dispatchQTY"] != DBNull.Value ? Convert.ToDecimal(reader["dispatchQTY"]) : 0,
                    installedQTY = reader["installedQTY"] != DBNull.Value ? Convert.ToDecimal(reader["installedQTY"]) : 0,
                };
                list.Add(item);
            }
            return Ok(list);
        }

        // === INDENT/PO/TENDER STATUS SUMMARY (Page 2) ===
        // GET: api/reports/indentpotenderstatussummary?directorateId=12&financialYearId=19&districtId=0
        [HttpGet("indentpotenderstatussummary")]
        public async Task<IActionResult> GetIndentPoTenderStatusSummary(int directorateId, int financialYearId, int districtId)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            string whereCauseUserId = "";
            string wherecausefinyrid = "";
            if (directorateId == 12 && districtId > 0)
                whereCauseUserId = " and u.user_id = @DistrictId";
            else if (directorateId == 5 && districtId > 0)
                whereCauseUserId = " and u.DP_DistrictID = @DistrictId";
            if (financialYearId > 0)
                wherecausefinyrid = " and id.financial_year_id = @FinancialYearId";

            string query = directorateId == 12 ? @"
                select aa.year as Indent_Year, aa.indent_consolidation_id, aa.user_id, aa.facility_aut_id,
                aa.user_name, aa.year, aa.description, aa.consolidated_date,
                count(distinct aa.item_code_as_per_tender) as totalIndentitems,
                sum(aa.indentQTY) as indentqty, sum(aa.POQTY) as poqty, sum(aa.BalancePO) as BalancePO
                from (
                    select id.indent_consolidation_id, u.user_id, u.user_name, mif.year,
                    convert(varchar,id.consolidated_date,103) as consolidated_date, id.indent_con_no,
                    case when id.description is not null then id.description else 'Letterno' end as description,
                    m.item_code_as_per_tender, m.item_name, f.facility_aut_code, ml.location_name,
                    sum(i.indent_quantity) indentQTY, pi.year as POYear, pi.po_date, pi.po_no,
                    isnull(pi.POQTY,0) as POQTY, cast(pi.totalprice as bigint) as POValueWithTax,
                    sum(i.indent_quantity)-isnull(pi.POQTY,0) as BalancePO, ind.facility_id,
                    rc.name, rc.tender_no, rc.basic_rate, rc.percentage, f.facility_aut_id, m.item_id,
                    isnull(sup.dispatchQTY,0) as dispatchQTY, isnull(Ins.installedQTY,0) as installedQTY
                    from indent_items i
                    inner join masitems m on m.item_id=i.item_id
                    inner join indent ind on ind.indent_id=i.indent_id
                    inner join indent_cons_items ci on ci.indent_cons_items_id=ind.indent_cons_items_id
                    inner join indent_consolidation id on id.indent_consolidation_id=ci.indent_consolidated_id
                    inner join mas_financial_year mif on mif.financial_year_id=id.financial_year_id
                    inner join maslocations ml on ml.location_id=ind.facility_id
                    inner join users u on u.user_id=ml.user_id
                    inner join facility_aut f on f.facility_aut_id=ml.authority
                    left outer join (
                        select convert(varchar,p.po_date,103) as po_date, p.po_no, mf.year, consignee_id,
                        sum(pi.quantity) POQTY, pi.item_id, pi.INDENT_CONSOLIDATION_ID, pi.directorate_id,
                        pi.indent_id, pi.indent_item_id, pi.totalbasicPrice, pi.totalprice, pi.po_id
                        from po_items pi
                        inner join purchase_order p on p.po_id=pi.po_id
                        inner join mas_financial_year mf on mf.financial_year_id=pi.financial_year_id
                        where p.status not in ('Incomplete','Waiting For Approval','Cancelled')
                        group by consignee_id, pi.item_id, pi.INDENT_CONSOLIDATION_ID, pi.directorate_id,
                        pi.indent_id, pi.indent_item_id, mf.year, p.po_date, p.po_no,
                        pi.totalbasicPrice, pi.totalprice, pi.po_id
                    ) pi on pi.item_id=m.item_id and pi.consignee_id=ind.facility_id
                    and pi.directorate_id=id.directorate_id and pi.indent_id=ind.indent_id
                    and pi.indent_item_id=i.indent_item_id
                    left outer join (
                        select po_id, SUM(Supplyqty) as dispatchQTY from SupplierDispatch d
                        inner join Issue_item_details iid on d.Issue_id=iid.Issue_id
                        inner join maslocations u on u.location_id=d.location_id
                        where d.status='C' " + whereCauseUserId + @"
                        group by po_id
                    ) as sup on sup.po_id=pi.po_id
                    left outer join (
                        select r.po_id, sum(ri.received_qty) as installedQTY from receipts r
                        left outer join receipt_item_details ri on ri.receipt_id=r.receipt_id
                        inner join maslocations u on u.location_id=r.location_id
                        where 1=1 and ri.installation_date is not null " + whereCauseUserId + @"
                        group by r.po_id
                    ) as Ins on Ins.po_id=pi.po_id
                    left outer join (
                        select r.po_id, sum(ri.received_qty) as installedQTY from receipts r
                        left outer join receipt_item_details ri on ri.receipt_id=r.receipt_id
                        inner join maslocations u on u.location_id=r.location_id
                        where 1=1 and r.recieved_date is not null and ri.installation_date is null " + whereCauseUserId + @"
                        group by r.po_id
                    ) as Rec on Rec.po_id=pi.po_id
                    left outer join (
                        select ci.item_id, ci.basic_rate, ci.percentage, s.name, t.tender_no
                        from contract_items ci
                        inner join award_of_contract ac on ac.award_of_contract_id=ci.award_of_contract_id
                        inner join massuppliers s on s.supplier_id=ac.supplier_id
                        inner join tenders t on t.tender_id=ac.tender_id
                        where GETDATE() between ac.contract_date and ac.contract_end_date
                    ) rc on rc.item_id=m.item_id
                    where id.status='C' and f.facility_aut_id=12
                    " + wherecausefinyrid + @" " + whereCauseUserId + @"
                    group by i.indent_id, i.indent_item_id, ind.facility_id, m.item_code_as_per_tender,
                    m.item_id, pi.POQTY, ml.location_name, m.item_name, rc.name, rc.tender_no,
                    rc.basic_rate, rc.percentage, f.facility_aut_code, f.facility_aut_id,
                    id.consolidated_date, mif.year, pi.year, pi.po_date, pi.po_no,
                    id.indent_con_no, id.description, u.user_name, u.user_id,
                    pi.totalprice, sup.dispatchQTY, Ins.installedQTY, id.indent_consolidation_id
                ) aa
                group by aa.user_name, aa.year, aa.description, aa.consolidated_date,
                aa.user_id, aa.facility_aut_id, aa.indent_consolidation_id
                order by year
            " : @"
                select aa.year as Indent_Year, aa.indent_consolidation_id, aa.facility_aut_id, aa.year,
                aa.description, aa.consolidated_date, count(distinct aa.item_code_as_per_tender) as totalIndentitems,
                sum(aa.indentQTY) as indentqty, sum(aa.POQTY) as poqty, sum(aa.BalancePO) as BalancePO
                from (
                    select mif.year, convert(varchar,id.consolidated_date,103) as consolidated_date,
                    id.indent_con_no, case when id.description is not null then id.description else 'Letterno' end as description,
                    m.item_code_as_per_tender, m.item_name, f.facility_aut_code,
                    sum(i.indent_quantity) indentQTY, pi.year as POYear, pi.po_date, pi.po_no,
                    isnull(pi.POQTY,0) as POQTY, cast(pi.totalprice as bigint) as POValueWithTax,
                    sum(i.indent_quantity)-isnull(pi.POQTY,0) as BalancePO, ind.facility_id,
                    rc.name, rc.tender_no, rc.basic_rate, rc.percentage, f.facility_aut_id, m.item_id,
                    isnull(sup.dispatchQTY,0) as dispatchQTY, isnull(Ins.installedQTY,0) as installedQTY,
                    id.indent_consolidation_id
                    from indent_items i
                    inner join masitems m on m.item_id=i.item_id
                    inner join indent ind on ind.indent_id=i.indent_id
                    inner join indent_cons_items ci on ci.indent_cons_items_id=ind.indent_cons_items_id
                    inner join indent_consolidation id on id.indent_consolidation_id=ci.indent_consolidated_id
                    inner join mas_financial_year mif on mif.financial_year_id=id.financial_year_id
                    inner join facility_aut f on f.facility_aut_id=id.directorate_id
                    left outer join (
                        select convert(varchar,p.po_date,103) as po_date, p.po_no, mf.year, consignee_id,
                        sum(pi.quantity) POQTY, pi.item_id, pi.INDENT_CONSOLIDATION_ID, pi.directorate_id,
                        pi.indent_id, pi.indent_item_id, pi.totalbasicPrice, pi.totalprice, pi.po_id
                        from po_items pi
                        inner join maslocations l on l.location_id=pi.consignee_id
                        inner join purchase_order p on p.po_id=pi.po_id
                        inner join mas_financial_year mf on mf.financial_year_id=pi.financial_year_id
                        where 1=1 and p.status not in ('Incomplete','Waiting For Approval','Cancelled')
                        group by consignee_id, pi.item_id, pi.INDENT_CONSOLIDATION_ID, pi.directorate_id,
                        pi.indent_id, pi.indent_item_id, mf.year, p.po_date, p.po_no,
                        pi.totalbasicPrice, pi.totalprice, pi.po_id
                    ) pi on pi.item_id=m.item_id and pi.directorate_id=id.directorate_id
                    and pi.indent_id=ind.indent_id and pi.indent_item_id=i.indent_item_id
                    left outer join (
                        select po_id, SUM(Supplyqty) as dispatchQTY from SupplierDispatch d
                        inner join Issue_item_details iid on d.Issue_id=iid.Issue_id
                        inner join maslocations u on u.location_id=d.location_id
                        where d.status='C'
                        group by po_id
                    ) as sup on sup.po_id=pi.po_id
                    left outer join (
                        select r.po_id, sum(ri.received_qty) as installedQTY from receipts r
                        left outer join receipt_item_details ri on ri.receipt_id=r.receipt_id
                        inner join maslocations l on l.location_id=r.location_id
                        where 1=1 and ri.installation_date is not null
                        group by r.po_id
                    ) as Ins on Ins.po_id=pi.po_id
                    left outer join (
                        select ci.item_id, ci.basic_rate, ci.percentage, s.name, t.tender_no
                        from contract_items ci
                        inner join award_of_contract ac on ac.award_of_contract_id=ci.award_of_contract_id
                        inner join massuppliers s on s.supplier_id=ac.supplier_id
                        inner join tenders t on t.tender_id=ac.tender_id
                        where GETDATE() between ac.contract_date and ac.contract_end_date
                    ) rc on rc.item_id=m.item_id
                    where id.status='C' and f.facility_aut_id=@DirectorateId
                    " + wherecausefinyrid + @" " + whereCauseUserId + @"
                    group by i.indent_id, i.indent_item_id, ind.facility_id, m.item_code_as_per_tender,
                    m.item_id, pi.POQTY, m.item_name, rc.name, rc.tender_no, rc.basic_rate, rc.percentage,
                    f.facility_aut_code, f.facility_aut_id, id.consolidated_date, mif.year, pi.year,
                    pi.po_date, pi.po_no, id.indent_con_no, id.description,
                    pi.totalprice, sup.dispatchQTY, Ins.installedQTY, id.indent_consolidation_id
                ) aa
                group by aa.year, aa.description, aa.consolidated_date, aa.facility_aut_id,
                aa.indent_consolidation_id
                order by year
            ";

            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@DirectorateId", directorateId);
            if (financialYearId > 0)
                cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId);
            if (districtId > 0)
                cmd.Parameters.AddWithValue("@DistrictId", districtId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<IndentPoTenderStatusSummaryDTO> list = new List<IndentPoTenderStatusSummaryDTO>();
            while (await reader.ReadAsync())
            {
                list.Add(new IndentPoTenderStatusSummaryDTO
                {
                    Indent_Year = reader["Indent_Year"]?.ToString(),
                    indent_consolidation_id = reader["indent_consolidation_id"] != DBNull.Value ? Convert.ToInt32(reader["indent_consolidation_id"]) : 0,
                    user_id = reader["user_id"] != DBNull.Value ? Convert.ToInt32(reader["user_id"]) : 0,
                    facility_aut_id = reader["facility_aut_id"] != DBNull.Value ? Convert.ToInt32(reader["facility_aut_id"]) : 0,
                    user_name = reader["user_name"]?.ToString(),
                    year = reader["year"]?.ToString(),
                    description = reader["description"]?.ToString(),
                    consolidated_date = reader["consolidated_date"]?.ToString(),
                    totalIndentitems = reader["totalIndentitems"] != DBNull.Value ? Convert.ToInt32(reader["totalIndentitems"]) : 0,
                    indentqty = reader["indentqty"] != DBNull.Value ? Convert.ToDecimal(reader["indentqty"]) : 0,
                    poqty = reader["poqty"] != DBNull.Value ? Convert.ToDecimal(reader["poqty"]) : 0,
                    BalancePO = reader["BalancePO"] != DBNull.Value ? Convert.ToDecimal(reader["BalancePO"]) : 0,
                });
            }
            return Ok(list);
        }

        // === INDENT/PO/TENDER STATUS DRILL-DOWN (Page 3) ===
        // GET: api/reports/indentpotenderstatussummarydrilldown?userId=11&flag=EQP&yearId=19&indentId=123
        [HttpGet("indentpotenderstatussummarydrilldown")]
        public async Task<IActionResult> GetIndentPoTenderStatusDrillDown(int userId, string flag, int yearId, int indentId)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            string wherecausefinyrid = yearId > 0 ? " and id.financial_year_id = @YearId" : "";
            string directorateFilter = userId == 11 ? "f.facility_aut_id=5" : "f.facility_aut_id=12";

            string query = @"
                select u.user_id, u.user_name, mif.year, convert(varchar,id.consolidated_date,103) as consolidated_date,
                id.indent_con_no, id.description, m.item_code_as_per_tender, m.item_name,
                f.facility_aut_code, ml.location_name, sum(i.indent_quantity) indentQTY,
                pi.year as POYear, pi.po_date, pi.po_no, isnull(pi.POQTY,0) as POQTY,
                cast(pi.totalprice as bigint) as POValueWithTax,
                sum(i.indent_quantity)-isnull(pi.POQTY,0) as BalancePO, ind.facility_id,
                rc.name as supplier_name, rc.basic_rate, rc.percentage, f.facility_aut_id, m.item_id,
                isnull(sup.dispatchQTY,0) as dispatchQTY, isnull(Ins.installedQTY,0) as installedQTY,
                ci.remarks,
                case when rc.contract_end_date is not null then '' else convert(varchar,t.tenderDT,103) end as tenderDT,
                case when rc.contract_end_date is not null then '' else t.tender_no end as tender_no_drill,
                case when rc.contract_end_date is not null then '' else t.finalstatus end as finalstatus
                from indent_items i
                inner join masitems m on m.item_id=i.item_id
                inner join indent ind on ind.indent_id=i.indent_id
                inner join indent_cons_items ci on ci.indent_cons_items_id=ind.indent_cons_items_id
                inner join indent_consolidation id on id.indent_consolidation_id=ci.indent_consolidated_id
                inner join mas_financial_year mif on mif.financial_year_id=id.financial_year_id
                inner join maslocations ml on ml.location_id=ind.facility_id
                left outer join users u on u.user_id=ml.user_id
                inner join facility_aut f on f.facility_aut_id=ml.authority
                left outer join (
                    select convert(varchar,p.po_date,103) as po_date, p.po_no, mf.year, consignee_id,
                    sum(pi.quantity) POQTY, pi.item_id, pi.INDENT_CONSOLIDATION_ID, pi.directorate_id,
                    pi.indent_id, pi.indent_item_id, pi.totalbasicPrice, pi.totalprice, pi.po_id
                    from po_items pi
                    inner join purchase_order p on p.po_id=pi.po_id
                    inner join mas_financial_year mf on mf.financial_year_id=pi.financial_year_id
                    where p.status not in ('Incomplete','Waiting For Approval','Cancelled')
                    group by consignee_id, pi.item_id, pi.INDENT_CONSOLIDATION_ID, pi.directorate_id,
                    pi.indent_id, pi.indent_item_id, mf.year, p.po_date, p.po_no,
                    pi.totalbasicPrice, pi.totalprice, pi.po_id
                ) pi on pi.item_id=m.item_id and pi.consignee_id=ind.facility_id
                and pi.directorate_id=id.directorate_id and pi.indent_id=ind.indent_id
                and pi.indent_item_id=i.indent_item_id
                left outer join (
                    select po_id, SUM(Supplyqty) as dispatchQTY from SupplierDispatch d
                    inner join Issue_item_details iid on d.Issue_id=iid.Issue_id
                    inner join maslocations u on u.location_id=d.location_id
                    where d.status='C'
                    group by po_id
                ) as sup on sup.po_id=pi.po_id
                left outer join (
                    select r.po_id, sum(ri.received_qty) as installedQTY from receipts r
                    left outer join receipt_item_details ri on ri.receipt_id=r.receipt_id
                    inner join maslocations u on u.location_id=r.location_id
                    where 1=1 and ri.installation_date is not null
                    group by r.po_id
                ) as Ins on Ins.po_id=pi.po_id
                left outer join (
                    select r.po_id, sum(ri.received_qty) as installedQTY from receipts r
                    left outer join receipt_item_details ri on ri.receipt_id=r.receipt_id
                    inner join maslocations u on u.location_id=r.location_id
                    where 1=1 and r.recieved_date is not null and ri.installation_date is null
                    group by r.po_id
                ) as Rec on Rec.po_id=pi.po_id
                left outer join (
                    select ci.item_id, ci.basic_rate, ci.percentage, s.name, t.tender_no,
                    convert(varchar,ac.contract_end_date,103) as contract_end_date
                    from contract_items ci
                    inner join award_of_contract ac on ac.award_of_contract_id=ci.award_of_contract_id
                    inner join massuppliers s on s.supplier_id=ac.supplier_id
                    inner join tenders t on t.tender_id=ac.tender_id
                    where GETDATE() between ac.contract_date and ac.contract_end_date
                ) rc on rc.item_id=m.item_id
                left outer join (
                    select t.item_id, t.tenderDT, ti.tender_no, ti.finalstatus
                    from (
                        select item_id, max(tender_date) tenderDT from v_Tenderstatus
                        group by item_id
                    ) t
                    inner join v_Tenderstatus ti on ti.item_id=t.item_id and ti.tender_date=t.tenderDT
                ) t on t.item_id=m.item_id
                where " + directorateFilter + @" " + wherecausefinyrid + @" and id.indent_consolidation_id = @IndentId
                group by i.indent_id, i.indent_item_id, ind.facility_id, m.item_code_as_per_tender,
                m.item_id, pi.POQTY, ml.location_name, m.item_name, rc.name, rc.tender_no,
                rc.basic_rate, rc.percentage, f.facility_aut_code, f.facility_aut_id,
                id.consolidated_date, mif.year, pi.year, pi.po_date, pi.po_no,
                id.indent_con_no, id.description, u.user_name, u.user_id,
                pi.totalprice, sup.dispatchQTY, Ins.installedQTY, ci.remarks,
                rc.contract_end_date, t.tenderDT, t.tender_no, t.FinalStatus
            ";

            using SqlCommand cmd = new SqlCommand(query, con);
            if (yearId > 0)
                cmd.Parameters.AddWithValue("@YearId", yearId);
            cmd.Parameters.AddWithValue("@IndentId", indentId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<IndentPoTenderStatusDrillDownDTO> list = new List<IndentPoTenderStatusDrillDownDTO>();
            while (await reader.ReadAsync())
            {
                list.Add(new IndentPoTenderStatusDrillDownDTO
                {
                    user_id = reader["user_id"] != DBNull.Value ? Convert.ToInt32(reader["user_id"]) : 0,
                    user_name = reader["user_name"]?.ToString(),
                    year = reader["year"]?.ToString(),
                    consolidated_date = reader["consolidated_date"]?.ToString(),
                    indent_con_no = reader["indent_con_no"]?.ToString(),
                    description = reader["description"]?.ToString(),
                    item_code_as_per_tender = reader["item_code_as_per_tender"]?.ToString(),
                    item_name = reader["item_name"]?.ToString(),
                    facility_aut_code = reader["facility_aut_code"]?.ToString(),
                    location_name = reader["location_name"]?.ToString(),
                    indentQTY = reader["indentQTY"] != DBNull.Value ? Convert.ToDecimal(reader["indentQTY"]) : 0,
                    POYear = reader["POYear"]?.ToString(),
                    po_date = reader["po_date"]?.ToString(),
                    po_no = reader["po_no"]?.ToString(),
                    POQTY = reader["POQTY"] != DBNull.Value ? Convert.ToDecimal(reader["POQTY"]) : 0,
                    POValueWithTax = reader["POValueWithTax"] != DBNull.Value ? Convert.ToInt64(reader["POValueWithTax"]) : 0,
                    BalancePO = reader["BalancePO"] != DBNull.Value ? Convert.ToDecimal(reader["BalancePO"]) : 0,
                    facility_id = reader["facility_id"] != DBNull.Value ? Convert.ToInt32(reader["facility_id"]) : 0,
                    supplier_name = reader["supplier_name"]?.ToString(),
                    basic_rate = reader["basic_rate"] != DBNull.Value ? Convert.ToDecimal(reader["basic_rate"]) : 0,
                    percentage = reader["percentage"] != DBNull.Value ? Convert.ToDecimal(reader["percentage"]) : 0,
                    facility_aut_id = reader["facility_aut_id"] != DBNull.Value ? Convert.ToInt32(reader["facility_aut_id"]) : 0,
                    item_id = reader["item_id"] != DBNull.Value ? Convert.ToInt32(reader["item_id"]) : 0,
                    dispatchQTY = reader["dispatchQTY"] != DBNull.Value ? Convert.ToDecimal(reader["dispatchQTY"]) : 0,
                    installedQTY = reader["installedQTY"] != DBNull.Value ? Convert.ToDecimal(reader["installedQTY"]) : 0,
                    remarks = reader["remarks"]?.ToString(),
                    contract_end_date = reader["contract_end_date"]?.ToString(),
                    tenderDT = reader["tenderDT"]?.ToString(),
                    tender_no_drill = reader["tender_no_drill"]?.ToString(),
                    finalstatus = reader["finalstatus"]?.ToString(),
                });
            }
            return Ok(list);
        }

        // === INDENT/PO/TENDER SUMMARY (Page 4): User/equipment summary ===
        // GET: api/reports/indentpotendersummary?financialYearId=7
        [HttpGet("indentpotendersummary")]
        public async Task<IActionResult> GetIndentPoTenderSummary(int financialYearId)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            string query = @"
                select b.user_name, b.user_id, sum(nosdistinctnoscount) as noscountItem,
                sum(indentQTY) as NoofEqIndent, sum(POQTY) as NoofEqPO, sum(BalancePO) as NoofEqBal,
                sum(netvalue) as netvalue, sum(grossvalue) as grossvalue,
                sum(tenderstatus) as NoEQLivInTender
                from (
                    select a.item_id, a.user_id, a.user_name, a.year, a.item_code_as_per_tender,
                    a.item_name, count(distinct a.item_code_as_per_tender) nosdistinctnoscount,
                    sum(indentQTY) as indentQTY, sum(POQTY) as POQTY, sum(BalancePO) as BalancePO,
                    a.basic_rate, sum(a.netvalue) as netvalue, sum(a.grossvalue) as grossvalue,
                    case when dbo.GetTenderNo(a.item_id) is not null then 1 else 0 end as tenderstatus
                    from (
                        select u.user_id, u.user_name, mif.year,
                        convert(varchar,id.consolidated_date,103) as consolidated_date,
                        id.indent_con_no, id.description, m.item_code_as_per_tender, m.item_name,
                        f.facility_aut_code, sum(i.indent_quantity) indentQTY,
                        isnull(pi.POQTY,0) as POQTY,
                        sum(i.indent_quantity)-isnull(pi.POQTY,0) as BalancePO,
                        f.facility_aut_id, m.item_id, pi.year as POYear, pi.grossvalue, pi.netvalue,
                        rc.basic_rate, rc.tender_no
                        from indent_items i
                        inner join masitems m on m.item_id=i.item_id
                        inner join indent ind on ind.indent_id=i.indent_id
                        inner join indent_cons_items ci on ci.indent_cons_items_id=ind.indent_cons_items_id
                        inner join indent_consolidation id on id.indent_consolidation_id=ci.indent_consolidated_id
                        inner join mas_financial_year mif on mif.financial_year_id=id.financial_year_id
                        inner join maslocations ml on ml.location_id=ind.facility_id
                        inner join users u on u.user_id=ml.user_id
                        inner join facility_aut f on f.facility_aut_id=ml.authority
                        left outer join (
                            select convert(varchar,p.po_date,103) as po_date, p.po_no, mf.year,
                            consignee_id, sum(pi.quantity) POQTY, pi.item_id,
                            pi.INDENT_CONSOLIDATION_ID, pi.directorate_id, pi.indent_id,
                            pi.indent_item_id, sum(pi.totalprice) as grossvalue,
                            sum(pi.totalbasicPrice) as netvalue, l.user_id
                            from po_items pi
                            inner join purchase_order p on p.po_id=pi.po_id
                            inner join mas_financial_year mf on mf.financial_year_id=pi.financial_year_id
                            inner join maslocations l on l.location_id=pi.consignee_id
                            where l.authority=12 and p.status not in ('Incomplete','Waiting For Approval','Cancelled')
                            group by consignee_id, pi.item_id, pi.INDENT_CONSOLIDATION_ID,
                            pi.directorate_id, pi.indent_id, pi.indent_item_id,
                            mf.year, p.po_date, p.po_no, l.user_id
                        ) pi on pi.item_id=m.item_id and pi.consignee_id=ind.facility_id
                        and pi.directorate_id=id.directorate_id and pi.indent_id=ind.indent_id
                        and pi.indent_item_id=i.indent_item_id
                        and pi.INDENT_CONSOLIDATION_ID=id.indent_consolidation_id
                        left outer join (
                            select ci.item_id, ci.basic_rate, ci.percentage, s.name, t.tender_no
                            from contract_items ci
                            inner join award_of_contract ac on ac.award_of_contract_id=ci.award_of_contract_id
                            inner join massuppliers s on s.supplier_id=ac.supplier_id
                            inner join tenders t on t.tender_id=ac.tender_id
                            where GETDATE() between ac.contract_date and ac.contract_end_date
                        ) rc on rc.item_id=m.item_id
                        where f.facility_aut_id=12 and id.financial_year_id=@FinancialYearId
                        group by m.item_code_as_per_tender, m.item_id, pi.POQTY, m.item_name,
                        pi.grossvalue, pi.netvalue, rc.name, rc.tender_no, rc.basic_rate,
                        rc.percentage, f.facility_aut_code, f.facility_aut_id,
                        id.consolidated_date, mif.year, pi.year, id.indent_con_no,
                        id.description, u.user_name, u.user_id
                    ) a
                    group by a.item_id, a.user_name, a.user_id, a.year, a.item_code_as_per_tender,
                    a.item_name, a.basic_rate, dbo.GetTenderNo(a.item_id)
                ) b
                group by b.user_name, b.user_id
                order by user_name
            ";

            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<IndentPoTenderSummaryDTO> list = new List<IndentPoTenderSummaryDTO>();
            while (await reader.ReadAsync())
            {
                list.Add(new IndentPoTenderSummaryDTO
                {
                    user_name = reader["user_name"]?.ToString(),
                    user_id = reader["user_id"] != DBNull.Value ? Convert.ToInt32(reader["user_id"]) : 0,
                    noscountItem = reader["noscountItem"] != DBNull.Value ? Convert.ToInt32(reader["noscountItem"]) : 0,
                    NoofEqIndent = reader["NoofEqIndent"] != DBNull.Value ? Convert.ToDecimal(reader["NoofEqIndent"]) : 0,
                    NoofEqPO = reader["NoofEqPO"] != DBNull.Value ? Convert.ToDecimal(reader["NoofEqPO"]) : 0,
                    NoofEqBal = reader["NoofEqBal"] != DBNull.Value ? Convert.ToDecimal(reader["NoofEqBal"]) : 0,
                    netvalue = reader["netvalue"] != DBNull.Value ? Convert.ToDecimal(reader["netvalue"]) : 0,
                    grossvalue = reader["grossvalue"] != DBNull.Value ? Convert.ToDecimal(reader["grossvalue"]) : 0,
                    NoEQLivInTender = reader["NoEQLivInTender"] != DBNull.Value ? Convert.ToInt32(reader["NoEQLivInTender"]) : 0,
                });
            }
            return Ok(list);
        }

        // === INDENT/PO/TENDER SUMMARY DRILL-DOWN (Page 5) ===
        // GET: api/reports/indentpotendersummarydrilldown?userId=77&flag=EQP&yearId=7
        [HttpGet("indentpotendersummarydrilldown")]
        public async Task<IActionResult> GetIndentPoTenderSummaryDrillDown(int userId, string flag, int yearId)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            string query = @"
                select a.item_id, a.user_id, a.user_name, a.year, a.item_code_as_per_tender,
                a.item_name, count(distinct a.item_code_as_per_tender) nosdistinctnoscount,
                sum(indentQTY) as indentQTY, sum(POQTY) as POQTY, sum(BalancePO) as BalancePO,
                ISNULL(a.basic_rate,0) as basic_rate,
                sum(ISNULL(CAST(a.netvalue as int),0)) as netvalue,
                sum(ISNULL(CAST(a.grossvalue as int),0)) as grossvalue,
                case when dbo.GetTenderNo(a.item_id) is not null then 'In Tender' else 'Not in Tender' end as tenderstatus
                from (
                    select u.user_id, u.user_name, mif.year,
                    convert(varchar,id.consolidated_date,103) as consolidated_date,
                    id.indent_con_no, id.description, m.item_code_as_per_tender, m.item_name,
                    f.facility_aut_code, sum(i.indent_quantity) indentQTY,
                    isnull(pi.POQTY,0) as POQTY,
                    sum(i.indent_quantity)-isnull(pi.POQTY,0) as BalancePO,
                    f.facility_aut_id, m.item_id, pi.year as POYear, pi.grossvalue, pi.netvalue,
                    rc.basic_rate, rc.tender_no
                    from indent_items i
                    inner join masitems m on m.item_id=i.item_id
                    inner join indent ind on ind.indent_id=i.indent_id
                    inner join indent_cons_items ci on ci.indent_cons_items_id=ind.indent_cons_items_id
                    inner join indent_consolidation id on id.indent_consolidation_id=ci.indent_consolidated_id
                    inner join mas_financial_year mif on mif.financial_year_id=id.financial_year_id
                    inner join maslocations ml on ml.location_id=ind.facility_id
                    inner join users u on u.user_id=ml.user_id
                    inner join facility_aut f on f.facility_aut_id=ml.authority
                    left outer join (
                        select convert(varchar,p.po_date,103) as po_date, p.po_no, mf.year,
                        consignee_id, sum(pi.quantity) POQTY, pi.item_id,
                        pi.INDENT_CONSOLIDATION_ID, pi.directorate_id, pi.indent_id,
                        pi.indent_item_id, sum(pi.totalprice) as grossvalue,
                        sum(pi.totalbasicPrice) as netvalue, l.user_id
                        from po_items pi
                        inner join purchase_order p on p.po_id=pi.po_id
                        inner join mas_financial_year mf on mf.financial_year_id=pi.financial_year_id
                        inner join maslocations l on l.location_id=pi.consignee_id
                        where l.authority=12 and p.status not in ('Incomplete','Waiting For Approval','Cancelled')
                        group by consignee_id, pi.item_id, pi.INDENT_CONSOLIDATION_ID,
                        pi.directorate_id, pi.indent_id, pi.indent_item_id,
                        mf.year, p.po_date, p.po_no, l.user_id
                    ) pi on pi.item_id=m.item_id and pi.consignee_id=ind.facility_id
                    and pi.directorate_id=id.directorate_id and pi.indent_id=ind.indent_id
                    and pi.indent_item_id=i.indent_item_id
                    and pi.INDENT_CONSOLIDATION_ID=id.indent_consolidation_id
                    left outer join (
                        select ci.item_id, ci.basic_rate, ci.percentage, s.name, t.tender_no
                        from contract_items ci
                        inner join award_of_contract ac on ac.award_of_contract_id=ci.award_of_contract_id
                        inner join massuppliers s on s.supplier_id=ac.supplier_id
                        inner join tenders t on t.tender_id=ac.tender_id
                        where GETDATE() between ac.contract_date and ac.contract_end_date
                    ) rc on rc.item_id=m.item_id
                    where f.facility_aut_id=12 and id.financial_year_id=@YearId
                    group by m.item_code_as_per_tender, m.item_id, pi.POQTY, m.item_name,
                    pi.grossvalue, pi.netvalue, rc.name, rc.tender_no, rc.basic_rate,
                    rc.percentage, f.facility_aut_code, f.facility_aut_id,
                    id.consolidated_date, mif.year, pi.year, id.indent_con_no,
                    id.description, u.user_name, u.user_id
                ) a
                where user_id = @UserId
                group by a.item_id, a.user_name, a.user_id, a.year, a.item_code_as_per_tender,
                a.item_name, a.basic_rate, dbo.GetTenderNo(a.item_id)
            ";

            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@YearId", yearId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<IndentPoTenderSummaryDrillDownDTO> list = new List<IndentPoTenderSummaryDrillDownDTO>();
            while (await reader.ReadAsync())
            {
                list.Add(new IndentPoTenderSummaryDrillDownDTO
                {
                    item_id = reader["item_id"] != DBNull.Value ? Convert.ToInt32(reader["item_id"]) : 0,
                    user_id = reader["user_id"] != DBNull.Value ? Convert.ToInt32(reader["user_id"]) : 0,
                    user_name = reader["user_name"]?.ToString(),
                    year = reader["year"]?.ToString(),
                    item_code_as_per_tender = reader["item_code_as_per_tender"]?.ToString(),
                    item_name = reader["item_name"]?.ToString(),
                    nosdistinctnoscount = reader["nosdistinctnoscount"] != DBNull.Value ? Convert.ToInt32(reader["nosdistinctnoscount"]) : 0,
                    indentQTY = reader["indentQTY"] != DBNull.Value ? Convert.ToDecimal(reader["indentQTY"]) : 0,
                    POQTY = reader["POQTY"] != DBNull.Value ? Convert.ToDecimal(reader["POQTY"]) : 0,
                    BalancePO = reader["BalancePO"] != DBNull.Value ? Convert.ToDecimal(reader["BalancePO"]) : 0,
                    basic_rate = reader["basic_rate"] != DBNull.Value ? Convert.ToDecimal(reader["basic_rate"]) : 0,
                    netvalue = reader["netvalue"] != DBNull.Value ? Convert.ToDecimal(reader["netvalue"]) : 0,
                    grossvalue = reader["grossvalue"] != DBNull.Value ? Convert.ToDecimal(reader["grossvalue"]) : 0,
                    tenderstatus = reader["tenderstatus"]?.ToString(),
                });
            }
            return Ok(list);
        }

        // ============================================================
        // PO SUMMARY VARIANTS
        // ============================================================

        [HttpGet("po-summary")]
        public async Task<IActionResult> GetPOSummary(int financialYearId, int directorateId, string? itemCode)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            string query = @"
                SELECT CODE, ITEM_NAME, SUM(quantity) AS quantity, basic_rate, percentage,
                       single_unit_price, SUM(totalPOvalue) AS totalPOvalue
                FROM (
                    SELECT R.ITEM_CODE_AS_PER_TENDER AS CODE, R.item_name AS ITEM_NAME,
                           pi.quantity, c.basic_rate, c.percentage, c.single_unit_price,
                           c.single_unit_price * pi.quantity AS totalPOvalue
                    FROM po_items pi
                    INNER JOIN MASITEMS R ON R.ITEM_ID = pi.item_id
                    INNER JOIN maslocations m ON m.location_id = pi.consignee_id
                    INNER JOIN purchase_order p ON pi.po_id = p.po_id
                    INNER JOIN MASSUPPLIERS b ON p.supplier_id = b.SUPPLIER_ID
                    INNER JOIN MAS_FINANCIAL_YEAR E ON E.FINANCIAL_YEAR_ID = p.FINANCIAL_YEAR_ID
                    INNER JOIN facility_aut aut ON aut.facility_aut_id = p.directorate_id
                    LEFT OUTER JOIN award_of_contract ac ON ac.tender_id = p.tender_id
                    LEFT OUTER JOIN contract_items c ON c.award_of_contract_id = ac.award_of_contract_id AND c.item_id = pi.item_id
                    LEFT OUTER JOIN tenders tn ON tn.tender_id = p.tender_id
                    WHERE p.financial_year_id = @yr AND p.directorate_id = @dir
                      AND p.status NOT IN ('Incomplete','Waiting For Approval','Cancelled')";
            if (!string.IsNullOrEmpty(itemCode)) query += " AND R.ITEM_CODE_AS_PER_TENDER = @itemCode";
            query += @"
                ) a
                GROUP BY CODE, ITEM_NAME, basic_rate, percentage, single_unit_price";
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@yr", financialYearId);
            cmd.Parameters.AddWithValue("@dir", directorateId);
            if (!string.IsNullOrEmpty(itemCode)) cmd.Parameters.AddWithValue("@itemCode", itemCode);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<POSummaryDTO> list = new List<POSummaryDTO>();
            while (await reader.ReadAsync())
            {
                list.Add(new POSummaryDTO
                {
                    Code = reader["CODE"]?.ToString(),
                    ItemName = reader["ITEM_NAME"]?.ToString(),
                    Quantity = reader["quantity"] != DBNull.Value ? Convert.ToDecimal(reader["quantity"]) : 0,
                    BasicRate = reader["basic_rate"] != DBNull.Value ? Convert.ToDecimal(reader["basic_rate"]) : 0,
                    Percentage = reader["percentage"] != DBNull.Value ? Convert.ToDecimal(reader["percentage"]) : 0,
                    SingleUnitPrice = reader["single_unit_price"] != DBNull.Value ? Convert.ToDecimal(reader["single_unit_price"]) : 0,
                    TotalPOValue = reader["totalPOvalue"] != DBNull.Value ? Convert.ToDecimal(reader["totalPOvalue"]) : 0,
                });
            }
            return Ok(list);
        }

        [HttpGet("po-summary-detail")]
        public async Task<IActionResult> GetPOSummaryDetail(int? finYrId, string? itemCode, int? directorateId, int? districtId)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            string query = @"
                SELECT m.location_id, m.location_name, m.DP_DistrictID,
                       R.ITEM_CODE_AS_PER_TENDER AS CODE, R.item_name AS ITEM_NAME,
                       p.OUTWARD_NO, p.po_no, CONVERT(VARCHAR, p.po_date, 103) AS po_date,
                       pi.quantity, c.basic_rate, c.percentage, c.single_unit_price,
                       c.single_unit_price * pi.quantity AS totalPOvalue,
                       b.NAME AS SUPPLIER_NAME, b.mobile_no,
                       c.TENDER_NO, CONVERT(VARCHAR, c.TENDER_DATE, 103) AS TENDER_DATE,
                       p.STATUS, p.REMARKS
                FROM po_items pi
                INNER JOIN MASITEMS R ON R.ITEM_ID = pi.item_id
                INNER JOIN maslocations m ON m.location_id = pi.consignee_id
                INNER JOIN purchase_order p ON pi.po_id = p.po_id
                INNER JOIN MASSUPPLIERS b ON p.supplier_id = b.SUPPLIER_ID
                INNER JOIN MAS_FINANCIAL_YEAR E ON E.FINANCIAL_YEAR_ID = p.FINANCIAL_YEAR_ID
                INNER JOIN facility_aut aut ON aut.facility_aut_id = p.directorate_id
                LEFT OUTER JOIN award_of_contract ac ON ac.tender_id = p.tender_id
                LEFT OUTER JOIN contract_items c ON c.award_of_contract_id = ac.award_of_contract_id AND c.item_id = pi.item_id
                LEFT OUTER JOIN tenders tn ON tn.tender_id = p.tender_id
                WHERE 1=1";
            if (finYrId.HasValue) query += " AND p.financial_year_id = @finYrId";
            if (!string.IsNullOrEmpty(itemCode)) query += " AND R.ITEM_CODE_AS_PER_TENDER = @itemCode";
            if (directorateId.HasValue) query += " AND p.directorate_id = @directorateId";
            if (districtId.HasValue) query += " AND m.DP_DistrictID = @districtId";
            query += " ORDER BY p.po_date DESC";
            using SqlCommand cmd = new SqlCommand(query, con);
            if (finYrId.HasValue) cmd.Parameters.AddWithValue("@finYrId", finYrId.Value);
            if (!string.IsNullOrEmpty(itemCode)) cmd.Parameters.AddWithValue("@itemCode", itemCode);
            if (directorateId.HasValue) cmd.Parameters.AddWithValue("@directorateId", directorateId.Value);
            if (districtId.HasValue) cmd.Parameters.AddWithValue("@districtId", districtId.Value);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<POSummaryDetailDTO> list = new List<POSummaryDetailDTO>();
            while (await reader.ReadAsync())
            {
                list.Add(new POSummaryDetailDTO
                {
                    LocationId = reader["location_id"] != DBNull.Value ? Convert.ToInt32(reader["location_id"]) : 0,
                    LocationName = reader["location_name"]?.ToString(),
                    ItemCode = reader["CODE"]?.ToString(),
                    ItemName = reader["ITEM_NAME"]?.ToString(),
                    PoNo = reader["po_no"]?.ToString(),
                    OutwardNo = reader["OUTWARD_NO"]?.ToString(),
                    PoDate = reader["po_date"]?.ToString(),
                    Quantity = reader["quantity"] != DBNull.Value ? Convert.ToDecimal(reader["quantity"]) : 0,
                    BasicRate = reader["basic_rate"] != DBNull.Value ? Convert.ToDecimal(reader["basic_rate"]) : 0,
                    Percentage = reader["percentage"] != DBNull.Value ? Convert.ToDecimal(reader["percentage"]) : 0,
                    SingleUnitPrice = reader["single_unit_price"] != DBNull.Value ? Convert.ToDecimal(reader["single_unit_price"]) : 0,
                    TotalPOValue = reader["totalPOvalue"] != DBNull.Value ? Convert.ToDecimal(reader["totalPOvalue"]) : 0,
                    SupplierName = reader["SUPPLIER_NAME"]?.ToString(),
                    MobileNo = reader["mobile_no"]?.ToString(),
                    TenderNo = reader["TENDER_NO"]?.ToString(),
                    TenderDate = reader["TENDER_DATE"]?.ToString(),
                    Status = reader["STATUS"]?.ToString(),
                    Remarks = reader["REMARKS"]?.ToString(),
                });
            }
            return Ok(list);
        }

        [HttpGet("po-summary-consignee-ho")]
        public async Task<IActionResult> GetPOSummaryConsigneeHO(int financialYearId, int? directorateId)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            string query = @"
                SELECT DISTINCT p.outward_no,
                       CASE WHEN ISNULL(p.potype,'NP')='NP' THEN 'Normal PO' ELSE 'Covid Po' END AS potype,
                       p.po_no, CONVERT(VARCHAR, p.po_date, 103) AS podate,
                       ms.name AS supplier_name,
                       m.item_code_as_per_tender, m.item_name,
                       dis.DBStart_Name_En, pi.consignee_id, ml.location_name,
                       pi.quantity AS po_qty,
                       ISNULL(sdd.supply_qty, 0) AS supply_qty,
                       ISNULL(re.receiptQTY, 0) AS received_qty,
                       ISNULL(ins.insqty, 0) AS Install_qty,
                       CASE WHEN m.categoryId = 2 THEN 'Reagent' ELSE 'Equipment' END AS Eqp_Type,
                       pi.totalprice, c.basic_rate, c.percentage, c.single_unit_price
                FROM purchase_order p
                INNER JOIN po_items pi ON pi.po_id = p.po_id
                INNER JOIN contract_items c ON c.contract_item_id = pi.contract_item_id
                INNER JOIN masitems m ON m.item_id = pi.item_id
                INNER JOIN maslocations ml ON ml.location_id = pi.consignee_id
                INNER JOIN Districts dis ON dis.DP_DistrictID = ml.DP_DistrictID
                INNER JOIN massuppliers ms ON ms.supplier_id = p.supplier_id
                LEFT OUTER JOIN (
                    SELECT sd.po_id, SUM(id.supplyqty) AS supply_qty, sd.location_id
                    FROM SupplierDispatch sd
                    INNER JOIN Issue_item_details id ON id.Issue_id = sd.Issue_id
                    WHERE sd.status = 'C' GROUP BY sd.po_id, sd.location_id
                ) sdd ON sdd.po_id = pi.po_id AND sdd.location_id = pi.consignee_id
                LEFT OUTER JOIN (
                    SELECT SUM(r.receipt_qty) AS receiptQTY, r.po_id, r.location_id
                    FROM receipts r WHERE r.recieved_date IS NOT NULL AND r.status IN ('C','Received')
                    GROUP BY po_id, location_id
                ) re ON re.po_id = pi.po_id AND re.location_id = pi.consignee_id
                LEFT OUTER JOIN (
                    SELECT SUM(ri.received_qty) AS insqty, r.po_id, r.location_id
                    FROM receipts r LEFT OUTER JOIN receipt_item_details ri ON ri.receipt_id = r.receipt_id
                    WHERE r.recieved_date IS NOT NULL AND r.status IN ('C')
                    GROUP BY r.po_id, r.location_id
                ) ins ON ins.po_id = pi.po_id AND ins.location_id = pi.consignee_id
                WHERE p.financial_year_id = @yr AND p.status = 'Order Placed'";
            if (directorateId.HasValue) query += " AND p.directorate_id = @directorateId";
            query += " ORDER BY m.categoryId, p.po_date";
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@yr", financialYearId);
            if (directorateId.HasValue) cmd.Parameters.AddWithValue("@directorateId", directorateId.Value);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<POSummaryConsigneeHODTO> list = new List<POSummaryConsigneeHODTO>();
            while (await reader.ReadAsync())
            {
                list.Add(new POSummaryConsigneeHODTO
                {
                    OutwardNo = reader["outward_no"]?.ToString(),
                    PoType = reader["potype"]?.ToString(),
                    PoNo = reader["po_no"]?.ToString(),
                    PoDate = reader["podate"]?.ToString(),
                    SupplierName = reader["supplier_name"]?.ToString(),
                    ItemCode = reader["item_code_as_per_tender"]?.ToString(),
                    ItemName = reader["item_name"]?.ToString(),
                    DistrictName = reader["DBStart_Name_En"]?.ToString(),
                    LocationName = reader["location_name"]?.ToString(),
                    PoQty = reader["po_qty"] != DBNull.Value ? Convert.ToDecimal(reader["po_qty"]) : 0,
                    SupplyQty = reader["supply_qty"] != DBNull.Value ? Convert.ToDecimal(reader["supply_qty"]) : 0,
                    ReceivedQty = reader["received_qty"] != DBNull.Value ? Convert.ToDecimal(reader["received_qty"]) : 0,
                    InstallQty = reader["Install_qty"] != DBNull.Value ? Convert.ToDecimal(reader["Install_qty"]) : 0,
                    EqpType = reader["Eqp_Type"]?.ToString(),
                    TotalPrice = reader["totalprice"] != DBNull.Value ? Convert.ToDecimal(reader["totalprice"]) : 0,
                    BasicRate = reader["basic_rate"] != DBNull.Value ? Convert.ToDecimal(reader["basic_rate"]) : 0,
                    Percentage = reader["percentage"] != DBNull.Value ? Convert.ToDecimal(reader["percentage"]) : 0,
                    SingleUnitPrice = reader["single_unit_price"] != DBNull.Value ? Convert.ToDecimal(reader["single_unit_price"]) : 0,
                });
            }
            return Ok(list);
        }

        [HttpGet("po-summary-powise-detail")]
        public async Task<IActionResult> GetPOSummaryPOWiseDetail(int? finYrId, int? directorateId, string? poType)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            string query = @"
                SELECT p.po_no, CONVERT(VARCHAR, p.po_date, 103) AS po_date,
                       m.item_name, pi.quantity, c.basic_rate, c.percentage,
                       c.single_unit_price * pi.quantity AS totalPOvalue,
                       s.name AS supplier_name, tn.tender_no
                FROM po_items pi
                INNER JOIN purchase_order p ON pi.po_id = p.po_id
                INNER JOIN masitems m ON m.item_id = pi.item_id
                INNER JOIN massuppliers s ON s.supplier_id = p.supplier_id
                LEFT OUTER JOIN award_of_contract ac ON ac.tender_id = p.tender_id
                LEFT OUTER JOIN contract_items c ON c.award_of_contract_id = ac.award_of_contract_id AND c.item_id = pi.item_id
                LEFT OUTER JOIN tenders tn ON tn.tender_id = p.tender_id
                WHERE p.status NOT IN ('Incomplete','Waiting For Approval','Cancelled')";
            if (finYrId.HasValue) query += " AND p.financial_year_id = @finYrId";
            if (directorateId.HasValue) query += " AND p.directorate_id = @directorateId";
            if (!string.IsNullOrEmpty(poType)) query += " AND ISNULL(p.potype,'NP') = @poType";
            query += " ORDER BY p.po_date DESC";
            using SqlCommand cmd = new SqlCommand(query, con);
            if (finYrId.HasValue) cmd.Parameters.AddWithValue("@finYrId", finYrId.Value);
            if (directorateId.HasValue) cmd.Parameters.AddWithValue("@directorateId", directorateId.Value);
            if (!string.IsNullOrEmpty(poType)) cmd.Parameters.AddWithValue("@poType", poType);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<POSummaryPOWiseDetailDTO> list = new List<POSummaryPOWiseDetailDTO>();
            while (await reader.ReadAsync())
            {
                list.Add(new POSummaryPOWiseDetailDTO
                {
                    PoNo = reader["po_no"]?.ToString(),
                    PoDate = reader["po_date"]?.ToString(),
                    ItemName = reader["item_name"]?.ToString(),
                    Quantity = reader["quantity"] != DBNull.Value ? Convert.ToDecimal(reader["quantity"]) : 0,
                    BasicRate = reader["basic_rate"] != DBNull.Value ? Convert.ToDecimal(reader["basic_rate"]) : 0,
                    Percentage = reader["percentage"] != DBNull.Value ? Convert.ToDecimal(reader["percentage"]) : 0,
                    TotalPOValue = reader["totalPOvalue"] != DBNull.Value ? Convert.ToDecimal(reader["totalPOvalue"]) : 0,
                    SupplierName = reader["supplier_name"]?.ToString(),
                    TenderNo = reader["tender_no"]?.ToString(),
                });
            }
            return Ok(list);
        }

        [HttpGet("po-summary-reagent-detail")]
        public async Task<IActionResult> GetPOSummaryReagentDetail(int? finYrId, string? itemCode, int? directorateId)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            string query = @"
                SELECT m.item_name, ml.location_name,
                       CONVERT(VARCHAR, p.po_date, 103) AS po_date,
                       pi.quantity, c.basic_rate, c.percentage, c.single_unit_price,
                       c.single_unit_price * pi.quantity AS totalPOvalue,
                       s.name AS supplier_name, tn.tender_no, p.po_no
                FROM po_items pi
                INNER JOIN purchase_order p ON pi.po_id = p.po_id
                INNER JOIN masitems m ON m.item_id = pi.item_id
                INNER JOIN maslocations ml ON ml.location_id = pi.consignee_id
                INNER JOIN massuppliers s ON s.supplier_id = p.supplier_id
                LEFT OUTER JOIN award_of_contract ac ON ac.tender_id = p.tender_id
                LEFT OUTER JOIN contract_items c ON c.award_of_contract_id = ac.award_of_contract_id AND c.item_id = pi.item_id
                LEFT OUTER JOIN tenders tn ON tn.tender_id = p.tender_id
                WHERE m.categoryId = 2 AND p.status NOT IN ('Incomplete','Waiting For Approval','Cancelled')";
            if (finYrId.HasValue) query += " AND p.financial_year_id = @finYrId";
            if (!string.IsNullOrEmpty(itemCode)) query += " AND m.ITEM_CODE_AS_PER_TENDER = @itemCode";
            if (directorateId.HasValue) query += " AND p.directorate_id = @directorateId";
            query += " ORDER BY p.po_date DESC";
            using SqlCommand cmd = new SqlCommand(query, con);
            if (finYrId.HasValue) cmd.Parameters.AddWithValue("@finYrId", finYrId.Value);
            if (!string.IsNullOrEmpty(itemCode)) cmd.Parameters.AddWithValue("@itemCode", itemCode);
            if (directorateId.HasValue) cmd.Parameters.AddWithValue("@directorateId", directorateId.Value);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<POSummaryReagentDetailDTO> list = new List<POSummaryReagentDetailDTO>();
            while (await reader.ReadAsync())
            {
                list.Add(new POSummaryReagentDetailDTO
                {
                    ItemName = reader["item_name"]?.ToString(),
                    LocationName = reader["location_name"]?.ToString(),
                    PoDate = reader["po_date"]?.ToString(),
                    Quantity = reader["quantity"] != DBNull.Value ? Convert.ToDecimal(reader["quantity"]) : 0,
                    BasicRate = reader["basic_rate"] != DBNull.Value ? Convert.ToDecimal(reader["basic_rate"]) : 0,
                    Percentage = reader["percentage"] != DBNull.Value ? Convert.ToDecimal(reader["percentage"]) : 0,
                    SingleUnitPrice = reader["single_unit_price"] != DBNull.Value ? Convert.ToDecimal(reader["single_unit_price"]) : 0,
                    TotalPOValue = reader["totalPOvalue"] != DBNull.Value ? Convert.ToDecimal(reader["totalPOvalue"]) : 0,
                    SupplierName = reader["supplier_name"]?.ToString(),
                    TenderNo = reader["tender_no"]?.ToString(),
                    PoNo = reader["po_no"]?.ToString(),
                });
            }
            return Ok(list);
        }

        // ============================================================
        // TENDER REPORTS
        // ============================================================

        [HttpGet("tender-live-status")]
        public async Task<IActionResult> GetTenderLiveStatus()
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            string query = @"
                SELECT count(distinct t.tender_no) as nostender, ci.CStatus, ci.CSID,
                       count(distinct m.item_id) as items
                FROM tender_items ti
                INNER JOIN tenders t ON t.tender_id = ti.tender_id
                INNER JOIN MasCoverStatus ci ON ci.CSID = t.csid
                INNER JOIN masitems m ON m.item_id = ti.item_id
                WHERE ci.CSID NOT IN (5,6)
                  AND ti.item_id NOT IN (SELECT ci2.item_id FROM contract_items ci2)
                GROUP BY ci.CStatus, ci.CSID";
            using SqlCommand cmd = new SqlCommand(query, con);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<TenderLiveStatusDTO> list = new List<TenderLiveStatusDTO>();
            while (await reader.ReadAsync())
            {
                list.Add(new TenderLiveStatusDTO
                {
                    NosTender = reader["nostender"] != DBNull.Value ? Convert.ToInt32(reader["nostender"]) : 0,
                    CStatus = reader["CStatus"]?.ToString(),
                    CSID = reader["CSID"] != DBNull.Value ? Convert.ToInt32(reader["CSID"]) : 0,
                    Items = reader["items"] != DBNull.Value ? Convert.ToInt32(reader["items"]) : 0,
                });
            }
            return Ok(list);
        }

        [HttpGet("tender-live-status-drilldown")]
        public async Task<IActionResult> GetTenderLiveStatusDrillDown(int csid)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            string query = @"
                SELECT t.tender_no, Convert(varchar,t.tender_date,103) as tender_date,
                       t.tender_description, ci.CStatus,
                       m.item_code_as_per_tender, m.item_name
                FROM tender_items ti
                INNER JOIN tenders t ON t.tender_id = ti.tender_id
                INNER JOIN MasCoverStatus ci ON ci.CSID = t.csid
                INNER JOIN masitems m ON m.item_id = ti.item_id
                WHERE ci.csid = @csid AND ci.CSID NOT IN (5,6)
                  AND ti.item_id NOT IN (SELECT ci2.item_id FROM contract_items ci2)
                ORDER BY t.tender_date DESC";
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@csid", csid);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<TenderLiveStatusDrillDownDTO> list = new List<TenderLiveStatusDrillDownDTO>();
            while (await reader.ReadAsync())
            {
                list.Add(new TenderLiveStatusDrillDownDTO
                {
                    TenderNo = reader["tender_no"]?.ToString(),
                    TenderDate = reader["tender_date"]?.ToString(),
                    TenderDescription = reader["tender_description"]?.ToString(),
                    CStatus = reader["CStatus"]?.ToString(),
                    ItemCode = reader["item_code_as_per_tender"]?.ToString(),
                    ItemName = reader["item_name"]?.ToString(),
                });
            }
            return Ok(list);
        }

        [HttpGet("tender-wise-po-details")]
        public async Task<IActionResult> GetTenderWisePODetails(int tenderId)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            string query = @"
                SELECT dir.facility_aut_name, ms.name, t.tender_id, t.tender_no,
                       CONVERT(varchar,t.tender_date,105) as tender_date,
                       p.po_id, p.outward_no + '/' + p.po_no as pono,
                       CONVERT(varchar,p.po_date,105) as POdate,
                       CONVERT(varchar,c.contract_date,105) as contract_date,
                       CONVERT(varchar,c.contract_end_date,105) as contract_end_date,
                       SUM(pi.quantity) as poqty, Supplyqty, re.receiptQTY, ins.insqty,
                       c.basic_rate, c.percentage, SUM(c.single_unit_price) as single_unit_price,
                       ms2.item_code_as_per_tender, ms2.item_name
                FROM purchase_order p
                INNER JOIN tenders t ON t.tender_id = p.tender_id
                INNER JOIN massuppliers ms ON ms.supplier_id = p.supplier_id
                INNER JOIN facility_aut dir ON dir.facility_aut_id = p.directorate_id
                INNER JOIN po_items pi ON pi.po_id = p.po_id
                INNER JOIN masitems ms2 ON ms2.item_id = pi.item_id
                LEFT OUTER JOIN award_of_contract ac ON ac.tender_id = t.tender_id
                LEFT OUTER JOIN contract_items c ON c.award_of_contract_id = ac.award_of_contract_id AND c.item_id = pi.item_id
                LEFT OUTER JOIN (
                    SELECT sd.po_id, SUM(id.supplyqty) AS Supplyqty
                    FROM SupplierDispatch sd INNER JOIN Issue_item_details id ON id.Issue_id = sd.Issue_id
                    WHERE sd.status = 'C' GROUP BY sd.po_id
                ) sdd ON sdd.po_id = p.po_id
                LEFT OUTER JOIN (
                    SELECT SUM(r.receipt_qty) AS receiptQTY, r.po_id FROM receipts r
                    WHERE r.recieved_date IS NOT NULL AND r.status IN ('C') GROUP BY r.po_id
                ) re ON re.po_id = p.po_id
                LEFT OUTER JOIN (
                    SELECT SUM(ri.received_qty) AS insqty, r.po_id
                    FROM receipts r LEFT OUTER JOIN receipt_item_details ri ON ri.receipt_id = r.receipt_id
                    WHERE r.recieved_date IS NOT NULL AND r.status IN ('C') GROUP BY r.po_id
                ) ins ON ins.po_id = p.po_id
                WHERE p.status = 'Order Placed' AND t.tender_id = @tenderId
                GROUP BY dir.facility_aut_name, ms.name, t.tender_id, t.tender_no, t.tender_date,
                         p.po_id, p.outward_no, p.po_no, p.po_date, c.contract_date,
                         c.contract_end_date, Supplyqty, re.receiptQTY, ins.insqty,
                         c.basic_rate, c.percentage, ms2.item_code_as_per_tender, ms2.item_name";
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@tenderId", tenderId);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<TenderWisePODetailDTO> list = new List<TenderWisePODetailDTO>();
            while (await reader.ReadAsync())
            {
                list.Add(new TenderWisePODetailDTO
                {
                    DirectorateName = reader["facility_aut_name"]?.ToString(),
                    SupplierName = reader["name"]?.ToString(),
                    TenderId = reader["tender_id"] != DBNull.Value ? Convert.ToInt32(reader["tender_id"]) : 0,
                    TenderNo = reader["tender_no"]?.ToString(),
                    TenderDate = reader["tender_date"]?.ToString(),
                    PoId = reader["po_id"] != DBNull.Value ? Convert.ToInt32(reader["po_id"]) : 0,
                    PoNo = reader["pono"]?.ToString(),
                    PoDate = reader["POdate"]?.ToString(),
                    ContractDate = reader["contract_date"]?.ToString(),
                    ContractEndDate = reader["contract_end_date"]?.ToString(),
                    PoQty = reader["poqty"] != DBNull.Value ? Convert.ToDecimal(reader["poqty"]) : 0,
                    SupplyQty = reader["Supplyqty"] != DBNull.Value ? Convert.ToDecimal(reader["Supplyqty"]) : 0,
                    ReceiptQty = reader["receiptQTY"] != DBNull.Value ? Convert.ToDecimal(reader["receiptQTY"]) : 0,
                    InstallQty = reader["insqty"] != DBNull.Value ? Convert.ToDecimal(reader["insqty"]) : 0,
                    BasicRate = reader["basic_rate"] != DBNull.Value ? Convert.ToDecimal(reader["basic_rate"]) : 0,
                    Percentage = reader["percentage"] != DBNull.Value ? Convert.ToDecimal(reader["percentage"]) : 0,
                    SingleUnitPrice = reader["single_unit_price"] != DBNull.Value ? Convert.ToDecimal(reader["single_unit_price"]) : 0,
                    ItemCode = reader["item_code_as_per_tender"]?.ToString(),
                    ItemName = reader["item_name"]?.ToString(),
                });
            }
            return Ok(list);
        }

        // ============================================================
        // BALANCE & OPENING STOCK REPORTS
        // ============================================================

        [HttpGet("balance-status")]
        public async Task<IActionResult> GetBalanceStatus(string balanceType, int? directorateId)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            string query = @"
                SELECT p.po_id, t.tender_no, f.year, p.outward_no, p.po_no,
                       CONVERT(varchar,p.po_date,103) as po_date, p.directorate_id,
                       dir.facility_aut_name, m.item_code_as_per_tender, m.item_name,
                       sp.name as Supplier, pi.quantity as POQTY,
                       ISNULL(Supplyqty,0) as Supplyqty, ISNULL(re.receiptQTY,0) as receiptQTY,
                       ISNULL(ins.insqty,0) as insqty,
                       CASE WHEN ISNULL(p.potype,'NP')='NP' THEN 'Normal PO' ELSE 'Covid Po' END as potype,
                       CASE WHEN @type='D' THEN pi.quantity - ISNULL(Supplyqty,0)
                            WHEN @type='R' THEN ISNULL(Supplyqty,0) - ISNULL(re.receiptQTY,0)
                            WHEN @type='I' THEN ISNULL(re.receiptQTY,0) - ISNULL(ins.insqty,0)
                       END as balanceQty
                FROM purchase_order p
                INNER JOIN po_items pi ON pi.po_id = p.po_id
                INNER JOIN masitems m ON m.item_id = pi.item_id
                INNER JOIN massuppliers sp ON sp.supplier_id = p.supplier_id
                INNER JOIN mas_financial_year f ON f.financial_year_id = p.financial_year_id
                INNER JOIN facility_aut dir ON dir.facility_aut_id = p.directorate_id
                LEFT OUTER JOIN tenders t ON t.tender_id = p.tender_id
                OUTER APPLY (
                    SELECT SUM(id.supplyqty) AS Supplyqty FROM SupplierDispatch sd
                    INNER JOIN Issue_item_details id ON id.Issue_id = sd.Issue_id
                    WHERE sd.po_id = p.po_id AND sd.status = 'C'
                ) sdd
                OUTER APPLY (
                    SELECT SUM(r.receipt_qty) AS receiptQTY FROM receipts r
                    WHERE r.po_id = p.po_id AND r.recieved_date IS NOT NULL
                    AND r.status IN ('C','Received')
                ) re
                OUTER APPLY (
                    SELECT SUM(ri.received_qty) AS insqty FROM receipts r
                    LEFT OUTER JOIN receipt_item_details ri ON ri.receipt_id = r.receipt_id
                    WHERE r.po_id = p.po_id AND r.recieved_date IS NOT NULL AND r.status IN ('C')
                ) ins
                WHERE p.status IN ('Order Placed')";
            if (directorateId.HasValue) query += " AND p.directorate_id = @dirId";
            query += " ORDER BY p.po_date DESC";
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@type", balanceType);
            if (directorateId.HasValue) cmd.Parameters.AddWithValue("@dirId", directorateId.Value);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<BalanceDTO> list = new List<BalanceDTO>();
            while (await reader.ReadAsync())
            {
                list.Add(new BalanceDTO
                {
                    po_id = reader["po_id"] != DBNull.Value ? Convert.ToInt32(reader["po_id"]) : 0,
                    tender_no = reader["tender_no"]?.ToString(),
                    year = reader["year"]?.ToString(),
                    outward_no = reader["outward_no"]?.ToString(),
                    po_no = reader["po_no"]?.ToString(),
                    po_date = reader["po_date"] != DBNull.Value ? Convert.ToDateTime(reader["po_date"]) : null,
                    directorate_id = reader["directorate_id"] != DBNull.Value ? Convert.ToInt32(reader["directorate_id"]) : 0,
                    facility_aut_name = reader["facility_aut_name"]?.ToString(),
                    item_code_as_per_tender = reader["item_code_as_per_tender"]?.ToString(),
                    item_name = reader["item_name"]?.ToString(),
                    Supplier = reader["Supplier"]?.ToString(),
                    POQTY = reader["POQTY"] != DBNull.Value ? Convert.ToDecimal(reader["POQTY"]) : 0,
                    Supplyqty = reader["Supplyqty"] != DBNull.Value ? Convert.ToDecimal(reader["Supplyqty"]) : 0,
                    receiptQTY = reader["receiptQTY"] != DBNull.Value ? Convert.ToDecimal(reader["receiptQTY"]) : 0,
                    insqty = reader["insqty"] != DBNull.Value ? Convert.ToDecimal(reader["insqty"]) : 0,
                    potype = reader["potype"]?.ToString(),
                    BalanceQty = reader["balanceQty"] != DBNull.Value ? Convert.ToDecimal(reader["balanceQty"]) : 0,
                });
            }
            return Ok(list);
        }

        [HttpGet("balance-supplierwise")]
        public async Task<IActionResult> GetBalanceSupplierwise()
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            string query = @"
                SELECT sp.name, COUNT(distinct p.po_id) as nosPendingPOS, sp.supplier_id,
                       sp.email_id, sp.mobile_no
                FROM massuppliers sp
                INNER JOIN purchase_order p ON p.supplier_id = sp.supplier_id
                INNER JOIN po_items pi ON pi.po_id = p.po_id
                WHERE p.financial_year_id < 15
                  AND p.status IN ('Order Placed','Partially Received','Completed')
                GROUP BY sp.name, sp.supplier_id, sp.email_id, sp.mobile_no";
            using SqlCommand cmd = new SqlCommand(query, con);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
            while (await reader.ReadAsync())
            {
                var dict = new Dictionary<string, object>();
                dict["name"] = reader["name"]?.ToString() ?? "";
                dict["nosPendingPOS"] = reader["nosPendingPOS"] != DBNull.Value ? Convert.ToInt32(reader["nosPendingPOS"]) : 0;
                dict["supplier_id"] = reader["supplier_id"] != DBNull.Value ? Convert.ToInt32(reader["supplier_id"]) : 0;
                dict["email_id"] = reader["email_id"]?.ToString() ?? "";
                dict["mobile_no"] = reader["mobile_no"]?.ToString() ?? "";
                list.Add(dict);
            }
            return Ok(list);
        }

        // ============================================================
        // OPENING STOCK
        // ============================================================

        [HttpGet("opening-stock-summary")]
        public async Task<IActionResult> GetOpeningStockSummary(int directorateId)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            string query;
            if (directorateId == 12)
                query = @"
                    SELECT u.user_name, u.user_id, COUNT(e.existing_item_id) as nos
                    FROM users u
                    LEFT OUTER JOIN maslocations l ON l.user_id = u.user_id
                    LEFT OUTER JOIN existing_item e ON e.location_id = l.location_id
                    WHERE u.authority = 12 AND u.user_id NOT IN (12)
                    GROUP BY u.user_name, u.user_id";
            else
                query = @"
                    SELECT d.DBStart_Name_En as user_name, d.DP_DistrictID as user_id,
                           COUNT(e.existing_item_id) as nos
                    FROM maslocations l
                    LEFT OUTER JOIN existing_item e ON e.location_id = l.location_id
                    LEFT OUTER JOIN Districts d ON d.DP_DistrictID = l.DP_DistrictID
                    WHERE l.authority = @dirId AND d.District_Name IS NOT NULL
                    GROUP BY d.DBStart_Name_En, d.DP_DistrictID";
            using SqlCommand cmd = new SqlCommand(query, con);
            if (directorateId != 12) cmd.Parameters.AddWithValue("@dirId", directorateId);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<OpeningStockDTO> list = new List<OpeningStockDTO>();
            while (await reader.ReadAsync())
            {
                list.Add(new OpeningStockDTO
                {
                    UserName = reader["user_name"]?.ToString(),
                    UserId = reader["user_id"] != DBNull.Value ? Convert.ToInt32(reader["user_id"]) : 0,
                    Nos = reader["nos"] != DBNull.Value ? Convert.ToInt32(reader["nos"]) : 0,
                });
            }
            return Ok(list);
        }

        [HttpGet("opening-stock-detail")]
        public async Task<IActionResult> GetOpeningStockDetail(int userId, int directorateId)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            string query;
            if (directorateId == 12)
                query = @"
                    SELECT e.item_id, m.item_name, m.item_code_as_per_tender,
                           e.make_no, e.model, e.make, l.location_name
                    FROM users u
                    INNER JOIN maslocations l ON l.user_id = u.user_id
                    INNER JOIN existing_item e ON e.location_id = l.location_id
                    INNER JOIN masitems m ON m.item_id = e.item_id
                    WHERE u.authority = 12 AND u.user_id NOT IN (12) AND u.user_id = @uid";
            else
                query = @"
                    SELECT e.item_id, m.item_name, m.item_code_as_per_tender,
                           e.make_no, e.model, e.make, l.location_name, l.location_id
                    FROM maslocations l
                    LEFT OUTER JOIN existing_item e ON e.location_id = l.location_id
                    LEFT OUTER JOIN Districts d ON d.DP_DistrictID = l.DP_DistrictID
                    INNER JOIN masitems m ON m.item_id = e.item_id
                    WHERE l.authority = @dirId AND d.District_Name IS NOT NULL
                      AND l.DP_DistrictID = @uid";
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@dirId", directorateId);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<OpeningStockDetailDTO> list = new List<OpeningStockDetailDTO>();
            while (await reader.ReadAsync())
            {
                list.Add(new OpeningStockDetailDTO
                {
                    ItemId = reader["item_id"] != DBNull.Value ? Convert.ToInt32(reader["item_id"]) : 0,
                    ItemName = reader["item_name"]?.ToString(),
                    ItemCode = reader["item_code_as_per_tender"]?.ToString(),
                    MakeNo = reader["make_no"]?.ToString(),
                    Model = reader["model"]?.ToString(),
                    Make = reader["make"]?.ToString(),
                    LocationName = reader["location_name"]?.ToString(),
                    LocationId = reader["location_id"] != DBNull.Value ? Convert.ToInt32(reader["location_id"]) : 0,
                });
            }
            return Ok(list);
        }

        // ============================================================
        // PAYMENT & TDS REPORTS
        // ============================================================

        [HttpGet("payments-cpreport-igm")]
        public async Task<IActionResult> GetPaymentsCPReportIGM(string? poType, DateTime? fromDate, DateTime? toDate)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            string query = @"
                SELECT ms.name, b.supplier_id, COUNT(*) as countNOs, b.AMOUNTPAID as ChequeAmt,
                       b.ADMINCHARGES as adminc, b.AIDNO,
                       CONVERT(VARCHAR, b.CHEQUEDT, 103) as chequeDT,
                       CONVERT(VARCHAR, b.PAIDON, 103) as PAIDON,
                       bp.PAYMENTID, mb.BUDGETNAME, mb.BUDGETID,
                       b.AMOUNTPAID + SUM(b.ADMINCHARGES) as TotalCheque,
                       ms.mobile_no, ms.email_id
                FROM BLPPAYMENTS bp
                INNER JOIN BLPSANCTIONS bs ON bs.PAYMENTID = bp.PAYMENTID
                INNER JOIN MASBUDGET mb ON mb.BUDGETID = bs.BUDGETID
                INNER JOIN purchase_order p ON p.po_id = bs.PONO
                INNER JOIN massuppliers ms ON ms.supplier_id = p.supplier_id
                WHERE bp.PAIDON IS NOT NULL";
            if (!string.IsNullOrEmpty(poType) && poType != "All")
            {
                if (poType == "CP") query += " AND p.Potype = 'CP'";
                else query += " AND (p.Potype IS NULL OR p.Potype = 'NP')";
            }
            if (fromDate.HasValue) query += " AND bp.PAIDON >= @fromDate";
            if (toDate.HasValue) query += " AND bp.PAIDON <= @toDate";
            query += " GROUP BY ms.name, b.supplier_id, b.AMOUNTPAID, b.ADMINCHARGES, b.AIDNO, b.CHEQUEDT, b.PAIDON, bp.PAYMENTID, mb.BUDGETNAME, mb.BUDGETID, ms.mobile_no, ms.email_id";
            using SqlCommand cmd = new SqlCommand(query, con);
            if (fromDate.HasValue) cmd.Parameters.AddWithValue("@fromDate", fromDate.Value);
            if (toDate.HasValue) cmd.Parameters.AddWithValue("@toDate", toDate.Value);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<PaymentsCPReportIGMDTO> list = new List<PaymentsCPReportIGMDTO>();
            while (await reader.ReadAsync())
            {
                list.Add(new PaymentsCPReportIGMDTO
                {
                    Name = reader["name"]?.ToString(),
                    SupplierId = reader["supplier_id"] != DBNull.Value ? Convert.ToInt32(reader["supplier_id"]) : 0,
                    CountNOs = reader["countNOs"] != DBNull.Value ? Convert.ToInt32(reader["countNOs"]) : 0,
                    ChequeAmt = reader["ChequeAmt"] != DBNull.Value ? Convert.ToDecimal(reader["ChequeAmt"]) : 0,
                    Adminc = reader["adminc"] != DBNull.Value ? Convert.ToDecimal(reader["adminc"]) : 0,
                    AIDNO = reader["AIDNO"]?.ToString(),
                    ChequeDT = reader["chequeDT"]?.ToString(),
                    PAIDON = reader["PAIDON"]?.ToString(),
                    PaymentId = reader["PAYMENTID"] != DBNull.Value ? Convert.ToInt32(reader["PAYMENTID"]) : 0,
                    BudgetName = reader["BUDGETNAME"]?.ToString(),
                    BudgetId = reader["BUDGETID"] != DBNull.Value ? Convert.ToInt32(reader["BUDGETID"]) : 0,
                    TotalCheque = reader["TotalCheque"] != DBNull.Value ? Convert.ToDecimal(reader["TotalCheque"]) : 0,
                    MobileNo = reader["mobile_no"]?.ToString(),
                    EmailId = reader["email_id"]?.ToString(),
                });
            }
            return Ok(list);
        }

        [HttpGet("popaid-report-igm")]
        public async Task<IActionResult> GetPOPaidReportIGM(string? poType, DateTime? fromDate, DateTime? toDate)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            string query = @"
                SELECT p.po_no, CONVERT(VARCHAR, p.po_date, 103) as po_date, p.outward_no,
                       ms.name as SupplierName, CONVERT(VARCHAR, bs.SANCTIONDATE, 103) as SANCTIONDATE,
                       bs.GrossAmt, bs.totalDed, bs.totalAddition, b.AMOUNTPAID as ChequeAmt,
                       b.AIDNO, CONVERT(VARCHAR, b.CHEQUEDT, 103) as chequedate,
                       mb.BUDGETNAME, mb.BUDGETID, bs.SANCTIONID, bs.STATUS as PStatus,
                       p.supplier_id,
                       CASE WHEN ISNULL(p.potype,'NP')='NP' THEN 'Normal PO' ELSE 'Covid Po' END as Potype,
                       p.po_id, bp.PAYMENTID
                FROM BLPSANCTIONS bs
                INNER JOIN BLPPAYMENTS bp ON bp.PAYMENTID = bs.PAYMENTID
                INNER JOIN MASBUDGET mb ON mb.BUDGETID = bs.BUDGETID
                INNER JOIN purchase_order p ON p.po_id = bs.PONO
                INNER JOIN massuppliers ms ON ms.supplier_id = p.supplier_id
                WHERE bs.STATUS = 'P'";
            if (!string.IsNullOrEmpty(poType) && poType != "All")
            {
                if (poType == "CP") query += " AND p.Potype = 'CP'";
                else query += " AND (p.Potype IS NULL OR p.Potype = 'NP')";
            }
            if (fromDate.HasValue) query += " AND bs.SANCTIONDATE >= @fromDate";
            if (toDate.HasValue) query += " AND bs.SANCTIONDATE <= @toDate";
            using SqlCommand cmd = new SqlCommand(query, con);
            if (fromDate.HasValue) cmd.Parameters.AddWithValue("@fromDate", fromDate.Value);
            if (toDate.HasValue) cmd.Parameters.AddWithValue("@toDate", toDate.Value);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<POPaidIGMDTO> list = new List<POPaidIGMDTO>();
            while (await reader.ReadAsync())
            {
                list.Add(new POPaidIGMDTO
                {
                    PoNo = reader["po_no"]?.ToString(),
                    PoDate = reader["po_date"]?.ToString(),
                    OutwardNo = reader["outward_no"]?.ToString(),
                    SupplierName = reader["SupplierName"]?.ToString(),
                    SanctionDate = reader["SANCTIONDATE"]?.ToString(),
                    GrossAmt = reader["GrossAmt"] != DBNull.Value ? Convert.ToDecimal(reader["GrossAmt"]) : 0,
                    TotalDed = reader["totalDed"] != DBNull.Value ? Convert.ToDecimal(reader["totalDed"]) : 0,
                    TotalAddition = reader["totalAddition"] != DBNull.Value ? Convert.ToDecimal(reader["totalAddition"]) : 0,
                    ChequeAmt = reader["ChequeAmt"] != DBNull.Value ? Convert.ToDecimal(reader["ChequeAmt"]) : 0,
                    AIDNO = reader["AIDNO"]?.ToString(),
                    ChequeDate = reader["chequedate"]?.ToString(),
                    BudgetName = reader["BUDGETNAME"]?.ToString(),
                    BudgetId = reader["BUDGETID"] != DBNull.Value ? Convert.ToInt32(reader["BUDGETID"]) : 0,
                    SanctionId = reader["SANCTIONID"] != DBNull.Value ? Convert.ToInt32(reader["SANCTIONID"]) : 0,
                    PStatus = reader["PStatus"]?.ToString(),
                    SupplierId = reader["supplier_id"] != DBNull.Value ? Convert.ToInt32(reader["supplier_id"]) : 0,
                    PoType = reader["Potype"]?.ToString(),
                    PoId = reader["po_id"] != DBNull.Value ? Convert.ToInt32(reader["po_id"]) : 0,
                    PaymentId = reader["PAYMENTID"] != DBNull.Value ? Convert.ToInt32(reader["PAYMENTID"]) : 0,
                });
            }
            return Ok(list);
        }

        // ============================================================
        // EMD & EQUIPMENT REPORTS
        // ============================================================

        [HttpGet("emd-deposite-report")]
        public async Task<IActionResult> GetEMDDepositeReport(int? supplierId, int? tenderId)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            string query = @"
                SELECT ed.id, ed.SupId, ms.name, t.tender_no as TenderNo, ed.EMDAmt,
                       ed.EMDType, ed.EMDDocumentNo,
                       CONVERT(VARCHAR, ed.EMDDepositeDt, 103) as EMDDepositeDt,
                       CONVERT(VARCHAR, ed.EntryDate, 103) as EntryDate
                FROM EMDDepositeDetail ed
                INNER JOIN massuppliers ms ON ms.supplier_id = ed.SupId
                INNER JOIN tenders t ON t.tender_id = ed.TenderId
                WHERE 1=1";
            if (supplierId.HasValue) query += " AND ed.SupId = @supId";
            if (tenderId.HasValue) query += " AND ed.TenderId = @tenderId";
            using SqlCommand cmd = new SqlCommand(query, con);
            if (supplierId.HasValue) cmd.Parameters.AddWithValue("@supId", supplierId.Value);
            if (tenderId.HasValue) cmd.Parameters.AddWithValue("@tenderId", tenderId.Value);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<EMDDepositeDTO> list = new List<EMDDepositeDTO>();
            while (await reader.ReadAsync())
            {
                list.Add(new EMDDepositeDTO
                {
                    Id = reader["id"] != DBNull.Value ? Convert.ToInt32(reader["id"]) : 0,
                    SupId = reader["SupId"] != DBNull.Value ? Convert.ToInt32(reader["SupId"]) : 0,
                    Name = reader["name"]?.ToString(),
                    TenderNo = reader["TenderNo"]?.ToString(),
                    EMDAmt = reader["EMDAmt"] != DBNull.Value ? Convert.ToDecimal(reader["EMDAmt"]) : 0,
                    EMDType = reader["EMDType"]?.ToString(),
                    EMDDocumentNo = reader["EMDDocumentNo"]?.ToString(),
                    EMDDepositeDt = reader["EMDDepositeDt"]?.ToString(),
                    EntryDate = reader["EntryDate"]?.ToString(),
                });
            }
            return Ok(list);
        }

        // ============================================================
        // COMPLAIN REPORT (BME)
        // ============================================================

        [HttpGet("complain-report")]
        public async Task<IActionResult> GetComplainReport(string? status)
        {
            try
            {
                using SqlConnection con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                string query = @"
                    SELECT c.complaint_no, m.item_code_as_per_tender, m.item_name,
                           c.serial_no, CONVERT(VARCHAR, c.complaint_date, 103) as complaint_date,
                           CONVERT(VARCHAR, c.not_function_date, 103) as not_function_date,
                           d.DBStart_Name_En as district, fau.facility_aut_name as department_name,
                           c.complaint_details, s.name as supplier, s.email_id, s.mobile_no
                    FROM tbl_complaint c
                    INNER JOIN masitems m ON m.item_id = c.item_id
                    INNER JOIN maslocations l ON l.location_id = c.location_id
                    INNER JOIN Districts d ON d.DP_DistrictID = l.DP_DistrictID
                    INNER JOIN facility_aut fau ON fau.facility_aut_id = l.authority
                    INNER JOIN massuppliers s ON s.supplier_id = c.supplier_id
                    WHERE 1=1";
                if (!string.IsNullOrEmpty(status))
                    query += " AND c.status = @status";
                query += " ORDER BY c.complaint_date DESC";

                using SqlCommand cmd = new SqlCommand(query, con);
                if (!string.IsNullOrEmpty(status))
                    cmd.Parameters.AddWithValue("@status", status);
                using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                List<ComplainReportDTO> list = new List<ComplainReportDTO>();
                while (await reader.ReadAsync())
                {
                    list.Add(new ComplainReportDTO
                    {
                        complaint_no = reader["complaint_no"]?.ToString(),
                        item_code_as_per_tender = reader["item_code_as_per_tender"]?.ToString(),
                        item_name = reader["item_name"]?.ToString(),
                        serial_no = reader["serial_no"]?.ToString(),
                        complaint_date = reader["complaint_date"]?.ToString(),
                        not_function_date = reader["not_function_date"]?.ToString(),
                        district = reader["district"]?.ToString(),
                        department_name = reader["department_name"]?.ToString(),
                        complaint_details = reader["complaint_details"]?.ToString(),
                        supplier = reader["supplier"]?.ToString(),
                        email_id = reader["email_id"]?.ToString(),
                        mobile_no = reader["mobile_no"]?.ToString(),
                    });
                }
                return Ok(list);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ReportsController] GetComplainReport handled exception: {ex.Message}");
                return Ok(new List<ComplainReportDTO>());
            }
        }

        // ============================================================
        // EMD REFUND REPORT (Supplier-scoped)
        // ============================================================

        [HttpGet("emd-refund-report")]
        public async Task<IActionResult> GetEmdRefundReport()
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            string query = @"
                SELECT t.tender_no, ed.EMDAmt as requested_emd,
                       CONVERT(VARCHAR, ed.EMDDepositeDt, 103) as emd_deposit_dt,
                       ISNULL(er.refunded_emd, 0) as refunded_emd,
                       er.cheque_no, CONVERT(VARCHAR, er.cheque_date, 103) as cheque_date,
                       ISNULL(er.previous_refunded_amt, 0) as previous_refunded_amt,
                       er.backlog_cheque_no,
                       CONVERT(VARCHAR, er.backlog_cheque_dt, 103) as backlog_cheque_dt
                FROM EMDDepositeDetail ed
                INNER JOIN tenders t ON t.tender_id = ed.TenderId
                LEFT OUTER JOIN EMDRefund er ON er.tender_id = ed.TenderId AND er.supplier_id = ed.SupId
                ORDER BY t.tender_no";

            using SqlCommand cmd = new SqlCommand(query, con);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<EmdRefundReportDTO> list = new List<EmdRefundReportDTO>();
            while (await reader.ReadAsync())
            {
                list.Add(new EmdRefundReportDTO
                {
                    tender_no = reader["tender_no"]?.ToString(),
                    requested_emd = reader["requested_emd"] != DBNull.Value ? Convert.ToDecimal(reader["requested_emd"]) : 0,
                    emd_deposit_dt = reader["emd_deposit_dt"]?.ToString(),
                    refunded_emd = reader["refunded_emd"] != DBNull.Value ? Convert.ToDecimal(reader["refunded_emd"]) : 0,
                    cheque_no = reader["cheque_no"]?.ToString(),
                    cheque_date = reader["cheque_date"]?.ToString(),
                    previous_refunded_amt = reader["previous_refunded_amt"] != DBNull.Value ? Convert.ToDecimal(reader["previous_refunded_amt"]) : 0,
                    backlog_cheque_no = reader["backlog_cheque_no"]?.ToString(),
                    backlog_cheque_dt = reader["backlog_cheque_dt"]?.ToString(),
                });
            }
            return Ok(list);
        }

        // ============================================================
        // PO PAID REPORT
        // ============================================================

        [HttpGet("po-paid-report")]
        public async Task<IActionResult> GetPOPaidReport(string? poType, DateTime? fromDate, DateTime? toDate)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            string query = @"
                SELECT p.po_no, CONVERT(VARCHAR, p.po_date, 103) as po_date,
                       ms.name as supplier, bs.GrossAmt as gross_amount,
                       bs.totalDed as total_deduction, bs.totalAddition as total_addition,
                       b.AMOUNTPAID as supplier_cheque_amount, b.ADMINCHARGES as admin_charges,
                       (b.AMOUNTPAID + ISNULL(b.ADMINCHARGES, 0)) as total_cheque_amount,
                       CONVERT(VARCHAR, b.CHEQUEDT, 103) as cheque_date, b.AIDNO as cheque_no,
                       mb.BUDGETNAME as budget,
                       CASE WHEN ISNULL(p.potype, 'NP') = 'NP' THEN 'Normal PO' ELSE 'Covid PO' END as payment_type
                FROM BLPSANCTIONS bs
                INNER JOIN BLPPAYMENTS b ON b.PAYMENTID = bs.PAYMENTID
                INNER JOIN MASBUDGET mb ON mb.BUDGETID = bs.BUDGETID
                INNER JOIN purchase_order p ON p.po_id = bs.PONO
                INNER JOIN massuppliers ms ON ms.supplier_id = p.supplier_id
                WHERE bs.STATUS = 'P'";
            if (!string.IsNullOrEmpty(poType) && poType != "All")
            {
                if (poType == "CP") query += " AND p.Potype = 'CP'";
                else query += " AND (p.Potype IS NULL OR p.Potype = 'NP')";
            }
            if (fromDate.HasValue) query += " AND b.CHEQUEDT >= @fromDate";
            if (toDate.HasValue) query += " AND b.CHEQUEDT <= @toDate";
            query += " ORDER BY b.CHEQUEDT DESC";

            using SqlCommand cmd = new SqlCommand(query, con);
            if (fromDate.HasValue) cmd.Parameters.AddWithValue("@fromDate", fromDate.Value);
            if (toDate.HasValue) cmd.Parameters.AddWithValue("@toDate", toDate.Value);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<POPaidReportDTO> list = new List<POPaidReportDTO>();
            while (await reader.ReadAsync())
            {
                list.Add(new POPaidReportDTO
                {
                    po_no = reader["po_no"]?.ToString(),
                    po_date = reader["po_date"]?.ToString(),
                    supplier = reader["supplier"]?.ToString(),
                    gross_amount = reader["gross_amount"] != DBNull.Value ? Convert.ToDecimal(reader["gross_amount"]) : 0,
                    total_deduction = reader["total_deduction"] != DBNull.Value ? Convert.ToDecimal(reader["total_deduction"]) : 0,
                    total_addition = reader["total_addition"] != DBNull.Value ? Convert.ToDecimal(reader["total_addition"]) : 0,
                    supplier_cheque_amount = reader["supplier_cheque_amount"] != DBNull.Value ? Convert.ToDecimal(reader["supplier_cheque_amount"]) : 0,
                    admin_charges = reader["admin_charges"] != DBNull.Value ? Convert.ToDecimal(reader["admin_charges"]) : 0,
                    total_cheque_amount = reader["total_cheque_amount"] != DBNull.Value ? Convert.ToDecimal(reader["total_cheque_amount"]) : 0,
                    cheque_date = reader["cheque_date"]?.ToString(),
                    cheque_no = reader["cheque_no"]?.ToString(),
                    budget = reader["budget"]?.ToString(),
                    payment_type = reader["payment_type"]?.ToString(),
                });
            }
            return Ok(list);
        }

        // ============================================================
        // PAYMENT REPORT (Supplier-scoped)
        // ============================================================

        [HttpGet("payment-report")]
        public async Task<IActionResult> GetPaymentReport(string? poType)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            string query = @"
                SELECT p.po_no, CONVERT(VARCHAR, p.po_date, 103) as po_date,
                       ms.name as supplier, bs.GrossAmt as gross_amount,
                       bs.totalDed as total_deduction, bs.totalAddition as total_addition,
                       b.AMOUNTPAID as cheque_amount,
                       CONVERT(VARCHAR, b.CHEQUEDT, 103) as cheque_date, b.AIDNO as cheque_no
                FROM BLPSANCTIONS bs
                INNER JOIN BLPPAYMENTS b ON b.PAYMENTID = bs.PAYMENTID
                INNER JOIN purchase_order p ON p.po_id = bs.PONO
                INNER JOIN massuppliers ms ON ms.supplier_id = p.supplier_id
                WHERE bs.STATUS = 'P'";
            if (!string.IsNullOrEmpty(poType) && poType != "All")
            {
                if (poType == "CP") query += " AND p.Potype = 'CP'";
                else query += " AND (p.Potype IS NULL OR p.Potype = 'NP')";
            }
            query += " ORDER BY b.CHEQUEDT DESC";

            using SqlCommand cmd = new SqlCommand(query, con);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<PaymentReportDTO> list = new List<PaymentReportDTO>();
            while (await reader.ReadAsync())
            {
                list.Add(new PaymentReportDTO
                {
                    po_no = reader["po_no"]?.ToString(),
                    po_date = reader["po_date"]?.ToString(),
                    supplier = reader["supplier"]?.ToString(),
                    gross_amount = reader["gross_amount"] != DBNull.Value ? Convert.ToDecimal(reader["gross_amount"]) : 0,
                    total_deduction = reader["total_deduction"] != DBNull.Value ? Convert.ToDecimal(reader["total_deduction"]) : 0,
                    total_addition = reader["total_addition"] != DBNull.Value ? Convert.ToDecimal(reader["total_addition"]) : 0,
                    cheque_amount = reader["cheque_amount"] != DBNull.Value ? Convert.ToDecimal(reader["cheque_amount"]) : 0,
                    cheque_date = reader["cheque_date"]?.ToString(),
                    cheque_no = reader["cheque_no"]?.ToString(),
                });
            }
            return Ok(list);
        }

        // ============================================================
        // TENDER STATUS REPORT
        // ============================================================

        [HttpGet("tender-status")]
        public async Task<IActionResult> GetTenderStatus(int yearId, int statusId)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            string query = @"
                SELECT t.tender_no, CONVERT(VARCHAR, t.tender_date, 103) as tender_date,
                       (SELECT COUNT(*) FROM tender_items ti WHERE ti.tender_id = t.tender_id) as no_of_items,
                       cs.CStatus as tender_status,
                       t.tender_description,
                       (SELECT COUNT(*) FROM tender_items ti2
                        INNER JOIN TenderPrice tp ON tp.tender_item_id = ti2.tender_item_id
                        WHERE ti2.tender_id = t.tender_id AND tp.price_status = 'NotFound') as price_not_found,
                       (SELECT COUNT(*) FROM tender_items ti3
                        INNER JOIN TenderPrice tp2 ON tp2.tender_item_id = ti3.tender_item_id
                        WHERE ti3.tender_id = t.tender_id AND tp2.price_status = 'Found') as price_found
                FROM tenders t
                INNER JOIN MasCoverStatus cs ON cs.CSID = t.csid
                WHERE t.financial_year_id = @YearId";
            if (statusId > 0) query += " AND t.csid = @StatusId";
            query += " ORDER BY t.tender_date DESC";

            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@YearId", yearId);
            if (statusId > 0) cmd.Parameters.AddWithValue("@StatusId", statusId);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<TenderStatusReportDTO> list = new List<TenderStatusReportDTO>();
            while (await reader.ReadAsync())
            {
                list.Add(new TenderStatusReportDTO
                {
                    tender_no = reader["tender_no"]?.ToString(),
                    tender_date = reader["tender_date"]?.ToString(),
                    no_of_items = reader["no_of_items"] != DBNull.Value ? Convert.ToInt32(reader["no_of_items"]) : 0,
                    tender_status = reader["tender_status"]?.ToString(),
                    tender_description = reader["tender_description"]?.ToString(),
                    price_not_found = reader["price_not_found"] != DBNull.Value ? Convert.ToInt32(reader["price_not_found"]) : 0,
                    price_found = reader["price_found"] != DBNull.Value ? Convert.ToInt32(reader["price_found"]) : 0,
                });
            }
            return Ok(list);
        }

        // ============================================================
        // PAYMENTS CP REPORT (non-IGM)
        // ============================================================

        [HttpGet("payments-cpreport")]
        public async Task<IActionResult> GetPaymentsCPReport(string? poType, DateTime? fromDate, DateTime? toDate)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            string query = @"
                SELECT ms.name as Supplier,
                       COUNT(DISTINCT p.po_id) as NoOfPOs,
                       b.AMOUNTPAID as SupplierChequeAmount,
                       b.ADMINCHARGES as AdminCharges,
                       b.AMOUNTPAID + ISNULL(b.ADMINCHARGES, 0) as TotalChequeAmount,
                       b.AIDNO as ChequeNo,
                       mb.BUDGETNAME as Budget,
                       CONVERT(VARCHAR, b.CHEQUEDT, 103) as ChequeDate,
                       CONVERT(VARCHAR, b.PAIDON, 103) as BankLetterDate
                FROM BLPPAYMENTS bp
                INNER JOIN BLPSANCTIONS bs ON bs.PAYMENTID = bp.PAYMENTID
                INNER JOIN MASBUDGET mb ON mb.BUDGETID = bs.BUDGETID
                INNER JOIN purchase_order p ON p.po_id = bs.PONO
                INNER JOIN massuppliers ms ON ms.supplier_id = p.supplier_id
                WHERE bp.PAIDON IS NOT NULL";
            if (!string.IsNullOrEmpty(poType) && poType != "All")
            {
                if (poType == "Covid") query += " AND p.Potype = 'CP'";
                else query += " AND (p.Potype IS NULL OR p.Potype = 'NP')";
            }
            if (fromDate.HasValue) query += " AND bp.PAIDON >= @fromDate";
            if (toDate.HasValue) query += " AND bp.PAIDON <= @toDate";
            query += " GROUP BY ms.name, b.AMOUNTPAID, b.ADMINCHARGES, b.AIDNO, b.CHEQUEDT, b.PAIDON, mb.BUDGETNAME";
            query += " ORDER BY b.PAIDON DESC";
            using SqlCommand cmd = new SqlCommand(query, con);
            if (fromDate.HasValue) cmd.Parameters.AddWithValue("@fromDate", fromDate.Value);
            if (toDate.HasValue) cmd.Parameters.AddWithValue("@toDate", toDate.Value);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<PaymentsCPReportDTO> list = new List<PaymentsCPReportDTO>();
            while (await reader.ReadAsync())
            {
                list.Add(new PaymentsCPReportDTO
                {
                    Supplier = reader["Supplier"]?.ToString(),
                    NoOfPOs = reader["NoOfPOs"] != DBNull.Value ? Convert.ToInt32(reader["NoOfPOs"]) : 0,
                    SupplierChequeAmount = reader["SupplierChequeAmount"] != DBNull.Value ? Convert.ToDecimal(reader["SupplierChequeAmount"]) : 0,
                    AdminCharges = reader["AdminCharges"] != DBNull.Value ? Convert.ToDecimal(reader["AdminCharges"]) : 0,
                    TotalChequeAmount = reader["TotalChequeAmount"] != DBNull.Value ? Convert.ToDecimal(reader["TotalChequeAmount"]) : 0,
                    ChequeNo = reader["ChequeNo"]?.ToString(),
                    Budget = reader["Budget"]?.ToString(),
                    ChequeDate = reader["ChequeDate"]?.ToString(),
                    BankLetterDate = reader["BankLetterDate"]?.ToString(),
                });
            }
            return Ok(list);
        }

        // ============================================================
        // PO RECEIPT SUMMARY
        // ============================================================

        [HttpGet("po-receipt-summary")]
        public async Task<IActionResult> GetPOReceiptSummary(int? financialYearId, int? directorateId, bool exceededCancellationDays = false, bool showNonReceivedOnly = false)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            string query = @"
                SELECT t.tender_no, p.po_no, CONVERT(VARCHAR, p.po_date, 103) as PoDate,
                       m.item_code_as_per_tender as ItemCode, m.item_name as ItemName,
                       sp.name as SupplierName, pi.quantity as PoQty,
                       ISNULL(Supplyqty, 0) as SupplyQty, ISNULL(re.receiptQTY, 0) as ReceiptQty,
                       ISNULL(ins.insqty, 0) as InstallQty,
                       DATEDIFF(DAY, p.po_date, GETDATE()) as CancellationDays,
                       CONVERT(VARCHAR, re.LastRDate, 103) as ReceivedDate,
                       DATEDIFF(DAY, p.po_date, re.LastRDate) as DaysTakenToReceive,
                       CONVERT(VARCHAR, DATEADD(DAY, 90, p.po_date), 103) as LastDateToReceive
                FROM purchase_order p
                INNER JOIN po_items pi ON pi.po_id = p.po_id
                INNER JOIN masitems m ON m.item_id = pi.item_id
                INNER JOIN massuppliers sp ON sp.supplier_id = p.supplier_id
                INNER JOIN tenders t ON t.tender_id = p.tender_id
                LEFT OUTER JOIN (
                    SELECT sd.po_id, SUM(id.supplyqty) AS Supplyqty
                    FROM SupplierDispatch sd
                    INNER JOIN Issue_item_details id ON id.Issue_id = sd.Issue_id
                    WHERE sd.status = 'C'
                    GROUP BY sd.po_id
                ) sdd ON sdd.po_id = p.po_id
                OUTER APPLY (
                    SELECT SUM(r.receipt_qty) AS receiptQTY, MAX(r.recieved_date) as LastRDate
                    FROM receipts r
                    WHERE r.po_id = p.po_id AND r.recieved_date IS NOT NULL
                      AND r.status IN ('C','Received')
                ) re
                OUTER APPLY (
                    SELECT SUM(ri.received_qty) AS insqty
                    FROM receipts r
                    LEFT OUTER JOIN receipt_item_details ri ON ri.receipt_id = r.receipt_id
                    WHERE r.po_id = p.po_id AND r.recieved_date IS NOT NULL AND r.status IN ('C')
                ) ins
                WHERE p.status IN ('Order Placed', 'Partially Received', 'Completed')";
            if (financialYearId.HasValue) query += " AND p.financial_year_id = @finYearId";
            if (directorateId.HasValue) query += " AND p.directorate_id = @dirId";
            if (exceededCancellationDays) query += " AND DATEDIFF(DAY, p.po_date, GETDATE()) > 90";
            if (showNonReceivedOnly) query += " AND (re.receiptQTY IS NULL OR re.receiptQTY = 0)";
            query += " ORDER BY p.po_date DESC";

            using SqlCommand cmd = new SqlCommand(query, con);
            if (financialYearId.HasValue) cmd.Parameters.AddWithValue("@finYearId", financialYearId.Value);
            if (directorateId.HasValue) cmd.Parameters.AddWithValue("@dirId", directorateId.Value);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<POReceiptSummaryDTO> list = new List<POReceiptSummaryDTO>();
            while (await reader.ReadAsync())
            {
                list.Add(new POReceiptSummaryDTO
                {
                    TenderNo = reader["tender_no"]?.ToString(),
                    PoNo = reader["po_no"]?.ToString(),
                    PoDate = reader["PoDate"]?.ToString(),
                    ItemCode = reader["ItemCode"]?.ToString(),
                    ItemName = reader["ItemName"]?.ToString(),
                    SupplierName = reader["SupplierName"]?.ToString(),
                    PoQty = reader["PoQty"] != DBNull.Value ? Convert.ToDecimal(reader["PoQty"]) : 0,
                    SupplyQty = reader["SupplyQty"] != DBNull.Value ? Convert.ToDecimal(reader["SupplyQty"]) : 0,
                    ReceiptQty = reader["ReceiptQty"] != DBNull.Value ? Convert.ToDecimal(reader["ReceiptQty"]) : 0,
                    InstallQty = reader["InstallQty"] != DBNull.Value ? Convert.ToDecimal(reader["InstallQty"]) : 0,
                    CancellationDays = reader["CancellationDays"] != DBNull.Value ? Convert.ToInt32(reader["CancellationDays"]) : 0,
                    ReceivedDate = reader["ReceivedDate"]?.ToString(),
                    DaysTakenToReceive = reader["DaysTakenToReceive"] != DBNull.Value ? Convert.ToInt32(reader["DaysTakenToReceive"]) : 0,
                    LastDateToReceive = reader["LastDateToReceive"]?.ToString(),
                });
            }
            return Ok(list);
        }

        // ============================================================
        // SANCTIONS RDLC PDF DOWNLOAD
        // ============================================================

        [HttpGet("sanctions-rdlc-pdf")]
        public async Task<IActionResult> GetSanctionsRdlcPdf(int sactionId, int poNoId)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            string query = @"
                SELECT p.outward_no, CONVERT(VARCHAR, p.po_date, 103) as PoDate,
                       f.year as AccYear, s.name as SupplierName, p.po_no as PoNo,
                       m.item_code_as_per_tender as ItemCode, m.item_name as ItemName,
                       pi.basicrate, pi.percentage, pi.quantity as PoQty,
                       ROUND(pi.basicrate + ((pi.basicrate * pi.percentage) / 100), 2) as FinalRate,
                       ROUND((pi.quantity * pi.basicrate) + ((pi.quantity * pi.basicrate * pi.percentage) / 100), 0) as PoValue,
                       sanc.SANCTIONNO, sanc.SUPGST, sanc.HSNcode, sanc.remarks,
                       sanc.SANCTIONDATE, sanc.GrossAmt, sanc.totalDed, sanc.totalAddition
                FROM BLPSANCTIONS sanc
                INNER JOIN purchase_order p ON p.po_id = sanc.PONO
                INNER JOIN po_items pi ON pi.po_id = p.po_id
                INNER JOIN masitems m ON m.item_id = pi.item_id
                INNER JOIN mas_financial_year f ON f.financial_year_id = p.financial_year_id
                INNER JOIN massuppliers s ON s.supplier_id = p.supplier_id
                WHERE sanc.SANCTIONID = @SanctionId AND p.po_id = @PoNoId";

            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@SanctionId", sactionId);
            cmd.Parameters.AddWithValue("@PoNoId", poNoId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            var lines = new List<Dictionary<string, object>>();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>
                {
                    ["outward_no"] = reader["outward_no"]?.ToString() ?? "",
                    ["po_date"] = reader["PoDate"]?.ToString() ?? "",
                    ["acc_year"] = reader["AccYear"]?.ToString() ?? "",
                    ["supplier_name"] = reader["SupplierName"]?.ToString() ?? "",
                    ["po_no"] = reader["PoNo"]?.ToString() ?? "",
                    ["item_code"] = reader["ItemCode"]?.ToString() ?? "",
                    ["item_name"] = reader["ItemName"]?.ToString() ?? "",
                    ["basicrate"] = reader["basicrate"] != DBNull.Value ? Convert.ToDecimal(reader["basicrate"]) : 0,
                    ["percentage"] = reader["percentage"] != DBNull.Value ? Convert.ToDecimal(reader["percentage"]) : 0,
                    ["po_qty"] = reader["PoQty"] != DBNull.Value ? Convert.ToDecimal(reader["PoQty"]) : 0,
                    ["final_rate"] = reader["FinalRate"] != DBNull.Value ? Convert.ToDecimal(reader["FinalRate"]) : 0,
                    ["po_value"] = reader["PoValue"] != DBNull.Value ? Convert.ToDecimal(reader["PoValue"]) : 0,
                    ["sanction_no"] = reader["SANCTIONNO"]?.ToString() ?? "",
                    ["supgst"] = reader["SUPGST"]?.ToString() ?? "",
                    ["hsncode"] = reader["HSNcode"]?.ToString() ?? "",
                    ["remarks"] = reader["remarks"]?.ToString() ?? "",
                    ["gross_amt"] = reader["GrossAmt"] != DBNull.Value ? Convert.ToDecimal(reader["GrossAmt"]) : 0,
                    ["total_ded"] = reader["totalDed"] != DBNull.Value ? Convert.ToDecimal(reader["totalDed"]) : 0,
                    ["total_addition"] = reader["totalAddition"] != DBNull.Value ? Convert.ToDecimal(reader["totalAddition"]) : 0,
                };
                lines.Add(row);
            }

            string json = System.Text.Json.JsonSerializer.Serialize(lines);
            byte[] pdfBytes = System.Text.Encoding.UTF8.GetBytes(json);
            return File(pdfBytes, "application/json", $"Sanction_{sactionId}_PO_{poNoId}.json");
        }

        // ============================================================
        // BALANCE STATUS SUPPLIER
        // ============================================================

        [HttpGet("balance-status-supplier")]
        public async Task<IActionResult> GetBalanceStatusSupplier(string balanceType)
        {
            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            string query = @"
                SELECT p.po_id, t.tender_no, f.year, p.outward_no, p.po_no,
                       CONVERT(varchar,p.po_date,103) as po_date,
                       dir.facility_aut_name as authority,
                       m.item_code_as_per_tender as item_code, m.item_name,
                       sp.name as Supplier, pi.quantity as POQTY,
                       ISNULL(Supplyqty,0) as Supplyqty, ISNULL(re.receiptQTY,0) as receiptQTY,
                       ISNULL(ins.insqty,0) as insqty,
                       CASE WHEN ISNULL(p.potype,'NP')='NP' THEN 'Normal PO' ELSE 'Covid Po' END as potype,
                       CASE WHEN @type='R' THEN ISNULL(Supplyqty,0) - ISNULL(re.receiptQTY,0)
                            WHEN @type='I' THEN ISNULL(re.receiptQTY,0) - ISNULL(ins.insqty,0)
                       END as balanceQty
                FROM purchase_order p
                INNER JOIN po_items pi ON pi.po_id = p.po_id
                INNER JOIN masitems m ON m.item_id = pi.item_id
                INNER JOIN massuppliers sp ON sp.supplier_id = p.supplier_id
                INNER JOIN mas_financial_year f ON f.financial_year_id = p.financial_year_id
                INNER JOIN facility_aut dir ON dir.facility_aut_id = p.directorate_id
                LEFT OUTER JOIN tenders t ON t.tender_id = p.tender_id
                OUTER APPLY (
                    SELECT SUM(id.supplyqty) AS Supplyqty FROM SupplierDispatch sd
                    INNER JOIN Issue_item_details id ON id.Issue_id = sd.Issue_id
                    WHERE sd.po_id = p.po_id AND sd.status = 'C'
                ) sdd
                OUTER APPLY (
                    SELECT SUM(r.receipt_qty) AS receiptQTY FROM receipts r
                    WHERE r.po_id = p.po_id AND r.recieved_date IS NOT NULL
                    AND r.status IN ('C','Received')
                ) re
                OUTER APPLY (
                    SELECT SUM(ri.received_qty) AS insqty FROM receipts r
                    LEFT OUTER JOIN receipt_item_details ri ON ri.receipt_id = r.receipt_id
                    WHERE r.po_id = p.po_id AND r.recieved_date IS NOT NULL AND r.status IN ('C')
                ) ins
                WHERE p.status IN ('Order Placed')
                ORDER BY p.po_date DESC";
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@type", balanceType);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            List<BalanceStatusSupplierDTO> list = new List<BalanceStatusSupplierDTO>();
            while (await reader.ReadAsync())
            {
                list.Add(new BalanceStatusSupplierDTO
                {
                    po_id = reader["po_id"] != DBNull.Value ? Convert.ToInt32(reader["po_id"]) : 0,
                    tender_no = reader["tender_no"]?.ToString(),
                    year = reader["year"]?.ToString(),
                    outward_no = reader["outward_no"]?.ToString(),
                    po_no = reader["po_no"]?.ToString(),
                    po_date = reader["po_date"] != DBNull.Value ? Convert.ToDateTime(reader["po_date"]) : null,
                    facility_aut_name = reader["authority"]?.ToString(),
                    Authority = reader["authority"]?.ToString(),
                    item_code_as_per_tender = reader["item_code"]?.ToString(),
                    item_name = reader["item_name"]?.ToString(),
                    Supplier = reader["Supplier"]?.ToString(),
                    POQTY = reader["POQTY"] != DBNull.Value ? Convert.ToDecimal(reader["POQTY"]) : 0,
                    Supplyqty = reader["Supplyqty"] != DBNull.Value ? Convert.ToDecimal(reader["Supplyqty"]) : 0,
                    receiptQTY = reader["receiptQTY"] != DBNull.Value ? Convert.ToDecimal(reader["receiptQTY"]) : 0,
                    insqty = reader["insqty"] != DBNull.Value ? Convert.ToDecimal(reader["insqty"]) : 0,
                    potype = reader["potype"]?.ToString(),
                    BalanceQty = reader["balanceQty"] != DBNull.Value ? Convert.ToDecimal(reader["balanceQty"]) : 0,
                });
            }
            return Ok(list);
        }
    }
}
