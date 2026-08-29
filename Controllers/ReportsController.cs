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

        // GET: api/Reports/balance-status?balanceType=I&directorateId=5
        [HttpGet("balance-status")]
        public async Task<IActionResult> GetBalanceStatus([FromQuery] string balanceType = "I", [FromQuery] int directorateId = 5)
        {
            List<BalanceStatusDTO> list = new List<BalanceStatusDTO>();

            string whereCause = "";
            string conditionalCause = "re.receiptQTY - isnull(ins.insqty,0)";

            string normType = string.IsNullOrWhiteSpace(balanceType) ? "I" : balanceType.Trim().ToUpperInvariant();
            if (normType == "I")
            {
                whereCause = " and re.receiptQTY > isnull(ins.insqty, 0) ";
                conditionalCause = " re.receiptQTY - isnull(ins.insqty, 0) ";
            }
            else if (normType == "R")
            {
                whereCause = " and isnull(Supplyqty, 0) > isnull(re.receiptQTY, 0) ";
                conditionalCause = " isnull(Supplyqty, 0) - isnull(re.receiptQTY, 0) ";
            }
            else if (normType == "D")
            {
                whereCause = " and pi.quantity > isnull(Supplyqty, 0) ";
                conditionalCause = " pi.quantity - isnull(Supplyqty, 0) ";
            }

            string whdid = directorateId > 0 ? " and p.directorate_id = @DirectorateId " : "";

            string query = $@"
select p.po_id, t.tender_no, f.year, p.outward_no, p.po_no,
       convert(varchar, p.po_date, 103) as po_date, p.directorate_id,
       dir.facility_aut_name as directorate, dir.facility_aut_name as authority,
       m.item_code_as_per_tender as item_code, m.item_name, sp.name as supplier,
       pi.quantity as po_qty, isnull(Supplyqty, 0) as supply_qty,
       isnull(re.receiptQTY, 0) as receipt_qty,
       convert(varchar, red.LastRDate, 103) as LastRDate,
       isnull(ins.insqty, 0) as install_qty,
       case when isnull(p.potype, 'NP') = 'NP' then 'Normal PO' else 'Covid Po' end as po_type,
       {conditionalCause} as balance_qty
from purchase_order p
inner join massuppliers sp on sp.supplier_id = p.supplier_id
inner join mas_financial_year f on f.financial_year_id = p.financial_year_id
left outer join (
    select sum(pi.quantity) as quantity, pi.po_id, pi.item_id from po_items pi group by pi.po_id, pi.item_id
) pi on pi.po_id = p.po_id
inner join tenders t on t.tender_id = p.tender_id
inner join masitems m on m.item_id = pi.item_id
inner join facility_aut dir on dir.facility_aut_id = p.directorate_id
left outer join (
    select po_id, isnull(sum(Supplyqty), 0) as Supplyqty from SupplierDispatch d
    inner join Issue_item_details i on d.Issue_id = i.Issue_id
    inner join maslocations u on u.location_id = d.location_id
    where d.status = 'C' group by po_id
) as sup on sup.po_id = pi.po_id
left outer join (
    select isnull(sum(r.receipt_qty), 0) as receiptQTY, r.po_id from receipts r
    where r.recieved_date is not null and r.status in ('C', 'Received') group by po_id
) as re on re.po_id = pi.po_id
left outer join (
    select sum(ri.received_qty) as insqty, r.po_id from receipts r
    left outer join receipt_item_details ri on ri.receipt_id = r.receipt_id
    where r.recieved_date is not null and r.status in ('C') group by r.po_id
) as ins on ins.po_id = pi.po_id
left outer join (
    select max(r.recieved_date) as LastRDate, r.po_id from receipts r
    left outer join receipt_item_details ri on ri.receipt_id = r.receipt_id
    where r.recieved_date is not null and r.status in ('C', 'Received') group by r.po_id
) as red on red.po_id = pi.po_id
where p.status in ('Order Placed') {whdid} {whereCause}
order by p.po_date desc";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            if (directorateId > 0) cmd.Parameters.AddWithValue("@DirectorateId", directorateId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new BalanceStatusDTO
                {
                    po_id = reader["po_id"] != DBNull.Value ? Convert.ToInt32(reader["po_id"]) : 0,
                    tender_no = reader["tender_no"]?.ToString(),
                    year = reader["year"]?.ToString(),
                    outward_no = reader["outward_no"]?.ToString(),
                    po_no = reader["po_no"]?.ToString(),
                    po_date = reader["po_date"]?.ToString(),
                    directorate_id = reader["directorate_id"] != DBNull.Value ? Convert.ToInt32(reader["directorate_id"]) : 0,
                    directorate = reader["directorate"]?.ToString(),
                    authority = reader["authority"]?.ToString(),
                    item_code = reader["item_code"]?.ToString(),
                    item_name = reader["item_name"]?.ToString(),
                    supplier = reader["supplier"]?.ToString(),
                    po_qty = reader["po_qty"] != DBNull.Value ? Convert.ToDecimal(reader["po_qty"]) : 0,
                    supply_qty = reader["supply_qty"] != DBNull.Value ? Convert.ToDecimal(reader["supply_qty"]) : 0,
                    receipt_qty = reader["receipt_qty"] != DBNull.Value ? Convert.ToDecimal(reader["receipt_qty"]) : 0,
                    LastRDate = reader["LastRDate"]?.ToString(),
                    install_qty = reader["install_qty"] != DBNull.Value ? Convert.ToDecimal(reader["install_qty"]) : 0,
                    po_type = reader["po_type"]?.ToString(),
                    balance_qty = reader["balance_qty"] != DBNull.Value ? Convert.ToDecimal(reader["balance_qty"]) : 0
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/pending-install-drilldown-dhs?POID=123
        [HttpGet("pending-install-drilldown-dhs")]
        public async Task<IActionResult> GetPendingInstallDrilldownDHS([FromQuery] int POID, [FromQuery] int poId = 0)
        {
            int targetPoId = POID > 0 ? POID : poId;
            if (targetPoId <= 0) return BadRequest(new { message = "Invalid PO ID" });

            List<PendingInstallDrillDownDHSRowDto> list = new List<PendingInstallDrillDownDHSRowDto>();

            string query = @"
select d.DBStart_Name_En as district, d.DBStart_Name_En, l.location_name,
       m.item_code_as_per_tender as item_code, m.item_code_as_per_tender, m.item_name,
       sp.name as supplier_name, sp.name as supplier, p.po_no, p.po_no as PoNo,
       (case when p.soissueDT is null then convert(varchar, p.po_date, 103) else convert(varchar, p.soissueDT, 103) end) as po_date,
       (case when p.soissueDT is null then convert(varchar, p.po_date, 103) else convert(varchar, p.soissueDT, 103) end) as po_dat,
       sum(pi.quantity) as po_qty, isnull(Supplyqty, 0) as DispatchedQTY,
       isnull(receiptQTY, 0) as receipt_qty, isnull(receiptQTY, 0) as receiptQTY,
       isnull(insqty, 0) as install_qty, isnull(insqty, 0) as insqty,
       isnull(Supplyqty, 0) - isnull(receiptQTY, 0) as pending_receipt,
       isnull(receiptQTY, 0) - isnull(insqty, 0) as pending_install,
       pi.po_id, pi.item_id, l.location_id, d.DP_DistrictID,
       case when (isnull(receiptQTY, 0) > isnull(insqty, 0)) then 'To be installed' else 'To be received' end as remarks
from po_items pi
inner join purchase_order p on p.po_id = pi.po_id
inner join masitems m on m.item_id = pi.item_id
inner join massuppliers sp on sp.supplier_id = p.supplier_id
inner join maslocations l on l.location_id = pi.consignee_id
left outer join Districts d on d.DP_DistrictID = l.DP_DistrictID
left outer join (
    select po_id, isnull(sum(Supplyqty), 0) as Supplyqty, d.location_id, d.Issue_id
    from SupplierDispatch d
    inner join Issue_item_details i on d.Issue_id = i.Issue_id
    inner join maslocations u on u.location_id = d.location_id
    where d.status = 'C'
    group by po_id, d.location_id, d.Issue_id
) as sup on sup.po_id = pi.po_id and sup.location_id = pi.consignee_id
left outer join (
    select sum(receiptQTY) as receiptQTY, sum(insqty) as insqty, po_id, location_id
    from (
        select isnull(r.receipt_qty, 0) as receiptQTY, isnull(insqty, 0) as insqty, r.po_id, r.location_id, r.receipt_id
        from receipts r
        left outer join (
            select sum(ri.received_qty) as insqty, r.po_id, r.location_id, r.receipt_id
            from receipts r
            left outer join receipt_item_details ri on ri.receipt_id = r.receipt_id
            where r.recieved_date is not null and r.status in ('C')
            group by r.po_id, r.location_id, r.receipt_id
        ) as ins on ins.po_id = r.po_id and ins.location_id = r.location_id and ins.receipt_id = r.receipt_id
        where r.recieved_date is not null and r.status in ('C', 'Received')
    ) a group by po_id, location_id
) as re on re.po_id = pi.po_id and re.location_id = l.location_id
where p.po_id = @PoId and p.status in ('Order Placed', 'Completed', 'Partially Received')
group by pi.po_id, pi.item_id, l.location_name, l.location_id, d.DP_DistrictID, d.DBStart_Name_En,
         receiptQTY, insqty, Supplyqty, m.item_code_as_per_tender, m.item_name, sp.name,
         p.soissueDT, p.po_date, p.po_no
having sum(pi.quantity) > isnull(insqty, 0)
order by d.DBStart_Name_En";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@PoId", targetPoId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new PendingInstallDrillDownDHSRowDto
                {
                    district = reader["district"]?.ToString(),
                    DBStart_Name_En = reader["DBStart_Name_En"]?.ToString(),
                    location_name = reader["location_name"]?.ToString(),
                    item_code = reader["item_code"]?.ToString(),
                    item_code_as_per_tender = reader["item_code_as_per_tender"]?.ToString(),
                    item_name = reader["item_name"]?.ToString(),
                    supplier_name = reader["supplier_name"]?.ToString(),
                    supplier = reader["supplier"]?.ToString(),
                    po_no = reader["po_no"]?.ToString(),
                    PoNo = reader["PoNo"]?.ToString(),
                    po_date = reader["po_date"]?.ToString(),
                    po_dat = reader["po_dat"]?.ToString(),
                    po_qty = reader["po_qty"] != DBNull.Value ? Convert.ToDecimal(reader["po_qty"]) : 0,
                    DispatchedQTY = reader["DispatchedQTY"] != DBNull.Value ? Convert.ToDecimal(reader["DispatchedQTY"]) : 0,
                    receipt_qty = reader["receipt_qty"] != DBNull.Value ? Convert.ToDecimal(reader["receipt_qty"]) : 0,
                    receiptQTY = reader["receiptQTY"] != DBNull.Value ? Convert.ToDecimal(reader["receiptQTY"]) : 0,
                    install_qty = reader["install_qty"] != DBNull.Value ? Convert.ToDecimal(reader["install_qty"]) : 0,
                    insqty = reader["insqty"] != DBNull.Value ? Convert.ToDecimal(reader["insqty"]) : 0,
                    pending_receipt = reader["pending_receipt"] != DBNull.Value ? Convert.ToDecimal(reader["pending_receipt"]) : 0,
                    pending_install = reader["pending_install"] != DBNull.Value ? Convert.ToDecimal(reader["pending_install"]) : 0,
                    po_id = reader["po_id"] != DBNull.Value ? Convert.ToInt32(reader["po_id"]) : 0,
                    item_id = reader["item_id"] != DBNull.Value ? Convert.ToInt32(reader["item_id"]) : 0,
                    location_id = reader["location_id"] != DBNull.Value ? Convert.ToInt32(reader["location_id"]) : 0,
                    DP_DistrictID = reader["DP_DistrictID"] != DBNull.Value ? Convert.ToInt32(reader["DP_DistrictID"]) : (int?)null,
                    remarks = reader["remarks"]?.ToString()
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/pending-install-drilldown-pocell?POID=123
        [HttpGet("pending-install-drilldown-pocell")]
        public async Task<IActionResult> GetPendingInstallDrilldownPOCell([FromQuery] int POID, [FromQuery] int poId = 0)
        {
            return await GetPendingInstallDrilldownDHS(POID, poId);
        }

        // GET: api/Reports/rdlc-dhs-pending?directorateId=5
        [HttpGet("rdlc-dhs-pending")]
        public async Task<IActionResult> GetRdlcDHSPending([FromQuery] int directorateId = 5)
        {
            List<RdlcDHSPendingRowDto> list = new List<RdlcDHSPendingRowDto>();

            string query = @"
select d.DBStart_Name_En as district, d.DBStart_Name_En, l.location_name,
       m.item_code_as_per_tender as item_code, m.item_code_as_per_tender, m.item_name,
       sp.name as supplier_name, sp.name as supplier, p.po_no, p.po_no as PoNo,
       (case when p.soissueDT is null then convert(varchar, p.po_date, 103) else convert(varchar, p.soissueDT, 103) end) as po_date,
       (case when p.soissueDT is null then convert(varchar, p.po_date, 103) else convert(varchar, p.soissueDT, 103) end) as po_dat,
       sum(pi.quantity) as po_qty, isnull(Supplyqty, 0) as DispatchedQTY,
       isnull(receiptQTY, 0) as receipt_qty, isnull(receiptQTY, 0) as receiptQTY,
       isnull(insqty, 0) as install_qty, isnull(insqty, 0) as insqty,
       isnull(Supplyqty, 0) - isnull(receiptQTY, 0) as pending_receipt,
       isnull(receiptQTY, 0) - isnull(insqty, 0) as pending_install,
       dir.facility_aut_name as directorate_name, dir.facility_aut_name as directorate,
       pi.po_id, pi.item_id, l.location_id, d.DP_DistrictID,
       case when (isnull(receiptQTY, 0) > isnull(insqty, 0)) then 'To be installed' else 'To be received' end as remarks
from po_items pi
inner join purchase_order p on p.po_id = pi.po_id
inner join masitems m on m.item_id = pi.item_id
inner join massuppliers sp on sp.supplier_id = p.supplier_id
inner join maslocations l on l.location_id = pi.consignee_id
left outer join facility_aut dir on dir.facility_aut_id = l.authority
left outer join Districts d on d.DP_DistrictID = l.DP_DistrictID
left outer join (
    select po_id, isnull(sum(Supplyqty), 0) as Supplyqty, d.location_id
    from SupplierDispatch d
    inner join Issue_item_details i on d.Issue_id = i.Issue_id
    inner join maslocations u on u.location_id = d.location_id
    where d.status = 'C'
    group by po_id, d.location_id
) as sup on sup.po_id = pi.po_id and sup.location_id = pi.consignee_id
left outer join (
    select sum(receiptQTY) as receiptQTY, sum(insqty) as insqty, po_id, location_id
    from (
        select isnull(r.receipt_qty, 0) as receiptQTY, isnull(insqty, 0) as insqty, r.po_id, r.location_id, r.receipt_id
        from receipts r
        left outer join (
            select sum(ri.received_qty) as insqty, r.po_id, r.location_id, r.receipt_id
            from receipts r
            left outer join receipt_item_details ri on ri.receipt_id = r.receipt_id
            where r.recieved_date is not null and r.status in ('C')
            group by r.po_id, r.location_id, r.receipt_id
        ) as ins on ins.po_id = r.po_id and ins.location_id = r.location_id and ins.receipt_id = r.receipt_id
        where r.recieved_date is not null and r.status in ('C', 'Received')
    ) a group by po_id, location_id
) as re on re.po_id = pi.po_id and re.location_id = l.location_id
where l.authority = @DirectorateId and p.status in ('Order Placed', 'Completed', 'Partially Received')
  and isnull(Supplyqty, 0) > 0
group by pi.po_id, pi.item_id, l.location_name, l.location_id, d.DP_DistrictID, d.DBStart_Name_En,
         receiptQTY, insqty, Supplyqty, m.item_code_as_per_tender, m.item_name, sp.name,
         p.soissueDT, p.po_date, p.po_no, dir.facility_aut_name
having sum(pi.quantity) > isnull(insqty, 0)
order by d.DBStart_Name_En";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@DirectorateId", directorateId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new RdlcDHSPendingRowDto
                {
                    district = reader["district"]?.ToString(),
                    DBStart_Name_En = reader["DBStart_Name_En"]?.ToString(),
                    location_name = reader["location_name"]?.ToString(),
                    item_code = reader["item_code"]?.ToString(),
                    item_code_as_per_tender = reader["item_code_as_per_tender"]?.ToString(),
                    item_name = reader["item_name"]?.ToString(),
                    supplier_name = reader["supplier_name"]?.ToString(),
                    supplier = reader["supplier"]?.ToString(),
                    po_no = reader["po_no"]?.ToString(),
                    PoNo = reader["PoNo"]?.ToString(),
                    po_date = reader["po_date"]?.ToString(),
                    po_dat = reader["po_dat"]?.ToString(),
                    po_qty = reader["po_qty"] != DBNull.Value ? Convert.ToDecimal(reader["po_qty"]) : 0,
                    DispatchedQTY = reader["DispatchedQTY"] != DBNull.Value ? Convert.ToDecimal(reader["DispatchedQTY"]) : 0,
                    receipt_qty = reader["receipt_qty"] != DBNull.Value ? Convert.ToDecimal(reader["receipt_qty"]) : 0,
                    receiptQTY = reader["receiptQTY"] != DBNull.Value ? Convert.ToDecimal(reader["receiptQTY"]) : 0,
                    install_qty = reader["install_qty"] != DBNull.Value ? Convert.ToDecimal(reader["install_qty"]) : 0,
                    insqty = reader["insqty"] != DBNull.Value ? Convert.ToDecimal(reader["insqty"]) : 0,
                    pending_receipt = reader["pending_receipt"] != DBNull.Value ? Convert.ToDecimal(reader["pending_receipt"]) : 0,
                    pending_install = reader["pending_install"] != DBNull.Value ? Convert.ToDecimal(reader["pending_install"]) : 0,
                    directorate_name = reader["directorate_name"]?.ToString(),
                    directorate = reader["directorate"]?.ToString(),
                    po_id = reader["po_id"] != DBNull.Value ? Convert.ToInt32(reader["po_id"]) : 0,
                    item_id = reader["item_id"] != DBNull.Value ? Convert.ToInt32(reader["item_id"]) : 0,
                    location_id = reader["location_id"] != DBNull.Value ? Convert.ToInt32(reader["location_id"]) : 0,
                    DP_DistrictID = reader["DP_DistrictID"] != DBNull.Value ? Convert.ToInt32(reader["DP_DistrictID"]) : (int?)null,
                    remarks = reader["remarks"]?.ToString()
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/indentpotenderstatus?directorateId=5&financialYearId=19&districtId=0&searchBy=
        [HttpGet("indentpotenderstatus")]
        public async Task<IActionResult> GetIndentPoTenderStatus(
            [FromQuery] int? directorateId,
            [FromQuery] int? financialYearId,
            [FromQuery] int? districtId,
            [FromQuery] string? searchBy)
        {
            List<IndentPoTenderStatusDTO> list = new List<IndentPoTenderStatusDTO>();

            int dirId = directorateId ?? 5;
            int finYrId = financialYearId ?? 0;
            int distId = districtId ?? 0;
            string search = searchBy?.Trim().ToUpperInvariant() ?? "";

            string searchCondition = "";
            if (search == "IN") searchCondition = " and Ins.installedQTY is not null ";
            else if (search == "NR") searchCondition = " and sup.dispatchQTY > 0 and Ins.installedQTY is null ";

            string distFilter = distId > 0 ? " and ml.DP_DistrictID = @DistrictId " : "";
            string yearFilter = finYrId > 0 ? " and id.financial_year_id = @FinancialYearId " : "";

            string query = $@"
select ml.DP_DistrictID as user_id, isnull(u.DBStart_Name_En, ml.location_name) as user_name,
       mif.year, convert(varchar, id.consolidated_date, 103) as consolidated_date,
       id.indent_con_no, id.description, m.item_code_as_per_tender, m.item_name,
       f.facility_aut_code, ml.location_name, sum(i.indent_quantity) as indentQTY,
       pi.year as POYear, pi.po_date, pi.po_no, isnull(pi.POQTY, 0) as POQTY,
       cast(pi.totalprice as bigint) as POValueWithTax,
       sum(i.indent_quantity) - isnull(pi.POQTY, 0) as BalancePO,
       ind.facility_id, rc.name as supplier_name, rc.tender_no, rc.basic_rate, rc.percentage,
       f.facility_aut_id, m.item_id,
       isnull(sup.dispatchQTY, 0) as dispatchQTY, isnull(Ins.installedQTY, 0) as installedQTY
from indent_items i
inner join masitems m on m.item_id = i.item_id
inner join indent ind on ind.indent_id = i.indent_id
inner join indent_cons_items ci on ci.indent_cons_items_id = ind.indent_cons_items_id
inner join indent_consolidation id on id.indent_consolidation_id = ci.indent_consolidated_id
inner join mas_financial_year mif on mif.financial_year_id = id.financial_year_id
inner join maslocations ml on ml.location_id = ind.facility_id
left outer join Districts u on u.DP_DistrictID = ml.DP_DistrictID
inner join facility_aut f on f.facility_aut_id = ml.authority
left outer join (
    select convert(varchar, p.po_date, 103) as po_date, p.po_no, mf.year, consignee_id,
           sum(pi.quantity) as POQTY, pi.item_id, pi.INDENT_CONSOLIDATION_ID,
           pi.directorate_id, pi.indent_id, pi.indent_item_id, pi.totalbasicPrice, pi.totalprice, pi.po_id
    from po_items pi
    inner join maslocations l on l.location_id = pi.consignee_id
    inner join purchase_order p on p.po_id = pi.po_id
    inner join mas_financial_year mf on mf.financial_year_id = pi.financial_year_id
    where p.status not in ('Incomplete', 'Waiting For Approval', 'Cancelled')
    group by consignee_id, pi.item_id, pi.INDENT_CONSOLIDATION_ID, pi.directorate_id, pi.indent_id, pi.indent_item_id, mf.year, p.po_date, p.po_no, pi.totalbasicPrice, pi.totalprice, pi.po_id
) pi on pi.item_id = m.item_id and pi.consignee_id = ind.facility_id and pi.directorate_id = id.directorate_id and pi.indent_id = ind.indent_id and pi.indent_item_id = i.indent_item_id
left outer join (
    select po_id, sum(Supplyqty) as dispatchQTY from SupplierDispatch d
    inner join Issue_item_details i on d.Issue_id = i.Issue_id
    inner join maslocations u on u.location_id = d.location_id
    where d.status = 'C' group by po_id
) as sup on sup.po_id = pi.po_id
left outer join (
    select r.po_id, sum(ri.received_qty) as installedQTY from receipts r
    left outer join receipt_item_details ri on ri.receipt_id = r.receipt_id
    inner join maslocations l on l.location_id = r.location_id
    where ri.installation_date is not null group by r.po_id
) as Ins on Ins.po_id = pi.po_id
left outer join (
    select ci.item_id, ci.basic_rate, ci.percentage, s.name, t.tender_no from contract_items ci
    inner join award_of_contract ac on ac.award_of_contract_id = ci.award_of_contract_id
    inner join massuppliers s on s.supplier_id = ac.supplier_id
    inner join tenders t on t.tender_id = ac.tender_id
    where getdate() between ac.contract_date and ac.contract_end_date
) rc on rc.item_id = m.item_id
where (f.facility_aut_id = @DirectorateId or @DirectorateId = 0) {yearFilter} {distFilter} {searchCondition}
group by i.indent_id, i.indent_item_id, ind.facility_id, m.item_code_as_per_tender, m.item_id, pi.POQTY,
         ml.location_name, m.item_name, rc.name, rc.tender_no, rc.basic_rate, rc.percentage, f.facility_aut_code,
         f.facility_aut_id, id.consolidated_date, mif.year, pi.year, pi.po_date, pi.po_no, id.indent_con_no,
         id.description, ml.DP_DistrictID, u.DBStart_Name_En, pi.totalprice, sup.dispatchQTY, Ins.installedQTY
order by ml.DP_DistrictID";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@DirectorateId", dirId);
            if (finYrId > 0) cmd.Parameters.AddWithValue("@FinancialYearId", finYrId);
            if (distId > 0) cmd.Parameters.AddWithValue("@DistrictId", distId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new IndentPoTenderStatusDTO
                {
                    user_id = reader["user_id"] != DBNull.Value ? Convert.ToInt32(reader["user_id"]) : (int?)null,
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
                    facility_id = reader["facility_id"] != DBNull.Value ? Convert.ToInt32(reader["facility_id"]) : (int?)null,
                    supplier_name = reader["supplier_name"]?.ToString(),
                    tender_no = reader["tender_no"]?.ToString(),
                    basic_rate = reader["basic_rate"] != DBNull.Value ? Convert.ToDecimal(reader["basic_rate"]) : 0,
                    percentage = reader["percentage"] != DBNull.Value ? Convert.ToDecimal(reader["percentage"]) : 0,
                    facility_aut_id = reader["facility_aut_id"] != DBNull.Value ? Convert.ToInt32(reader["facility_aut_id"]) : (int?)null,
                    item_id = reader["item_id"] != DBNull.Value ? Convert.ToInt32(reader["item_id"]) : (int?)null,
                    dispatchQTY = reader["dispatchQTY"] != DBNull.Value ? Convert.ToDecimal(reader["dispatchQTY"]) : 0,
                    installedQTY = reader["installedQTY"] != DBNull.Value ? Convert.ToDecimal(reader["installedQTY"]) : 0
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/indentpotenderstatussummary?directorateId=5&financialYearId=19&districtId=0
        [HttpGet("indentpotenderstatussummary")]
        public async Task<IActionResult> GetIndentPoTenderStatusSummary(
            [FromQuery] int? directorateId,
            [FromQuery] int? financialYearId,
            [FromQuery] int? districtId)
        {
            List<IndentPoTenderStatusSummaryDTO> list = new List<IndentPoTenderStatusSummaryDTO>();

            int dirId = directorateId ?? 5;
            int finYrId = financialYearId ?? 0;

            string yearFilter = finYrId > 0 ? " and id.financial_year_id = @FinancialYearId " : "";

            string query = $@"
select aa.year as Indent_Year, aa.indent_consolidation_id, aa.facility_aut_id, aa.year,
       aa.description, aa.consolidated_date,
       count(distinct aa.item_code_as_per_tender) as totalIndentitems,
       sum(aa.indentQTY) as indentqty, sum(aa.POQTY) as poqty, sum(aa.BalancePO) as BalancePO
from (
    select mif.year, convert(varchar, id.consolidated_date, 103) as consolidated_date, id.indent_con_no,
           case when id.description is not null then id.description else 'Letterno' end as description,
           m.item_code_as_per_tender, m.item_name, f.facility_aut_code, sum(i.indent_quantity) as indentQTY,
           pi.year as POYear, pi.po_date, pi.po_no, isnull(pi.POQTY, 0) as POQTY,
           cast(pi.totalprice as bigint) as POValueWithTax, sum(i.indent_quantity) - isnull(pi.POQTY, 0) as BalancePO,
           ind.facility_id, rc.name, rc.tender_no, rc.basic_rate, rc.percentage, f.facility_aut_id, m.item_id,
           isnull(sup.dispatchQTY, 0) as dispatchQTY, isnull(Ins.installedQTY, 0) as installedQTY, id.indent_consolidation_id
    from indent_items i
    inner join masitems m on m.item_id = i.item_id
    inner join indent ind on ind.indent_id = i.indent_id
    inner join indent_cons_items ci on ci.indent_cons_items_id = ind.indent_cons_items_id
    inner join indent_consolidation id on id.indent_consolidation_id = ci.indent_consolidated_id
    inner join mas_financial_year mif on mif.financial_year_id = id.financial_year_id
    inner join facility_aut f on f.facility_aut_id = id.directorate_id
    left outer join (
        select convert(varchar, p.po_date, 103) as po_date, p.po_no, mf.year, consignee_id, sum(pi.quantity) as POQTY,
               pi.item_id, pi.INDENT_CONSOLIDATION_ID, pi.directorate_id, pi.indent_id, pi.indent_item_id,
               pi.totalbasicPrice, pi.totalprice, pi.po_id
        from po_items pi
        inner join maslocations l on l.location_id = pi.consignee_id
        inner join purchase_order p on p.po_id = pi.po_id
        inner join mas_financial_year mf on mf.financial_year_id = pi.financial_year_id
        where p.status not in ('Incomplete', 'Waiting For Approval', 'Cancelled')
        group by consignee_id, pi.item_id, pi.INDENT_CONSOLIDATION_ID, pi.directorate_id, pi.indent_id, pi.indent_item_id, mf.year, p.po_date, p.po_no, pi.totalbasicPrice, pi.totalprice, pi.po_id
    ) pi on pi.item_id = m.item_id and pi.directorate_id = id.directorate_id and pi.indent_id = ind.indent_id and pi.indent_item_id = i.indent_item_id
    left outer join (
        select po_id, sum(Supplyqty) as dispatchQTY from SupplierDispatch d
        inner join Issue_item_details i on d.Issue_id = i.Issue_id
        inner join maslocations u on u.location_id = d.location_id
        where d.status = 'C' group by po_id
    ) as sup on sup.po_id = pi.po_id
    left outer join (
        select r.po_id, sum(ri.received_qty) as installedQTY from receipts r
        left outer join receipt_item_details ri on ri.receipt_id = r.receipt_id
        inner join maslocations u on u.location_id = r.location_id
        where ri.installation_date is not null group by r.po_id
    ) as Ins on Ins.po_id = pi.po_id
    left outer join (
        select ci.item_id, ci.basic_rate, ci.percentage, s.name, t.tender_no from contract_items ci
        inner join award_of_contract ac on ac.award_of_contract_id = ci.award_of_contract_id
        inner join massuppliers s on s.supplier_id = ac.supplier_id
        inner join tenders t on t.tender_id = ac.tender_id
        where getdate() between ac.contract_date and ac.contract_end_date
    ) rc on rc.item_id = m.item_id
    where (f.facility_aut_id = @DirectorateId or @DirectorateId = 0) {yearFilter}
    group by i.indent_id, i.indent_item_id, ind.facility_id, m.item_code_as_per_tender, m.item_id, pi.POQTY,
             m.item_name, rc.name, rc.tender_no, rc.basic_rate, rc.percentage, f.facility_aut_code, f.facility_aut_id,
             id.consolidated_date, mif.year, pi.year, pi.po_date, pi.po_no, id.indent_con_no, id.description,
             pi.totalprice, sup.dispatchQTY, Ins.installedQTY, id.indent_consolidation_id
) aa
group by aa.year, aa.description, aa.consolidated_date, aa.facility_aut_id, aa.indent_consolidation_id
order by aa.year desc";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@DirectorateId", dirId);
            if (finYrId > 0) cmd.Parameters.AddWithValue("@FinancialYearId", finYrId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new IndentPoTenderStatusSummaryDTO
                {
                    Indent_Year = reader["Indent_Year"]?.ToString(),
                    indent_consolidation_id = reader["indent_consolidation_id"] != DBNull.Value ? Convert.ToInt32(reader["indent_consolidation_id"]) : (int?)null,
                    facility_aut_id = reader["facility_aut_id"] != DBNull.Value ? Convert.ToInt32(reader["facility_aut_id"]) : (int?)null,
                    year = reader["year"]?.ToString(),
                    description = reader["description"]?.ToString(),
                    consolidated_date = reader["consolidated_date"]?.ToString(),
                    totalIndentitems = reader["totalIndentitems"] != DBNull.Value ? Convert.ToInt32(reader["totalIndentitems"]) : 0,
                    indentqty = reader["indentqty"] != DBNull.Value ? Convert.ToDecimal(reader["indentqty"]) : 0,
                    poqty = reader["poqty"] != DBNull.Value ? Convert.ToDecimal(reader["poqty"]) : 0,
                    BalancePO = reader["BalancePO"] != DBNull.Value ? Convert.ToDecimal(reader["BalancePO"]) : 0
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/indentpotenderstatussummarydrilldown?userId=0&flag=EQP&yearId=19&indentId=123
        [HttpGet("indentpotenderstatussummarydrilldown")]
        public async Task<IActionResult> GetIndentPoTenderStatusSummaryDrilldown(
            [FromQuery] int? userId,
            [FromQuery] string? flag,
            [FromQuery] int? yearId,
            [FromQuery] int? indentId)
        {
            List<IndentPoTenderStatusDrillDownDTO> list = new List<IndentPoTenderStatusDrillDownDTO>();

            int yrId = yearId ?? 0;
            int indId = indentId ?? 0;

            string yearFilter = yrId > 0 ? " and id.financial_year_id = @FinancialYearId " : "";
            string indentFilter = indId > 0 ? " and id.indent_consolidation_id = @IndentId " : "";

            string query = $@"
select u.user_id, u.user_name, mif.year, convert(varchar, id.consolidated_date, 103) as consolidated_date,
       id.indent_con_no, id.description, m.item_code_as_per_tender, m.item_name, f.facility_aut_code,
       ml.location_name, sum(i.indent_quantity) as indentQTY, pi.year as POYear, pi.po_date, pi.po_no,
       isnull(pi.POQTY, 0) as POQTY, cast(pi.totalprice as bigint) as POValueWithTax,
       sum(i.indent_quantity) - isnull(pi.POQTY, 0) as BalancePO, ind.facility_id,
       rc.name as supplier_name, rc.basic_rate, rc.percentage, f.facility_aut_id, m.item_id,
       isnull(sup.dispatchQTY, 0) as dispatchQTY, isnull(Ins.installedQTY, 0) as installedQTY,
       ci.remarks, rc.contract_end_date,
       case when rc.contract_end_date is not null then '' else convert(varchar, t.tenderDT, 103) end as tenderDT,
       case when rc.contract_end_date is not null then '' else t.tender_no end as tender_no_drill,
       case when rc.contract_end_date is not null then '' else t.finalstatus end as finalstatus
from indent_items i
inner join masitems m on m.item_id = i.item_id
inner join indent ind on ind.indent_id = i.indent_id
inner join indent_cons_items ci on ci.indent_cons_items_id = ind.indent_cons_items_id
inner join indent_consolidation id on id.indent_consolidation_id = ci.indent_consolidated_id
inner join mas_financial_year mif on mif.financial_year_id = id.financial_year_id
inner join maslocations ml on ml.location_id = ind.facility_id
left outer join users u on u.user_id = ml.user_id
inner join facility_aut f on f.facility_aut_id = ml.authority
left outer join (
    select convert(varchar, p.po_date, 103) as po_date, p.po_no, mf.year, consignee_id, sum(pi.quantity) as POQTY,
           pi.item_id, pi.INDENT_CONSOLIDATION_ID, pi.directorate_id, pi.indent_id, pi.indent_item_id,
           pi.totalbasicPrice, pi.totalprice, pi.po_id
    from po_items pi
    inner join purchase_order p on p.po_id = pi.po_id
    inner join mas_financial_year mf on mf.financial_year_id = pi.financial_year_id
    where p.status not in ('Incomplete', 'Waiting For Approval', 'Cancelled')
    group by consignee_id, pi.item_id, pi.INDENT_CONSOLIDATION_ID, pi.directorate_id, pi.indent_id, pi.indent_item_id, mf.year, p.po_date, p.po_no, pi.totalbasicPrice, pi.totalprice, pi.po_id
) pi on pi.item_id = m.item_id and pi.consignee_id = ind.facility_id and pi.directorate_id = id.directorate_id and pi.indent_id = ind.indent_id and pi.indent_item_id = i.indent_item_id
left outer join (
    select po_id, sum(Supplyqty) as dispatchQTY from SupplierDispatch d
    inner join Issue_item_details i on d.Issue_id = i.Issue_id
    inner join maslocations u on u.location_id = d.location_id
    where d.status = 'C' group by po_id
) as sup on sup.po_id = pi.po_id
left outer join (
    select r.po_id, sum(ri.received_qty) as installedQTY from receipts r
    left outer join receipt_item_details ri on ri.receipt_id = r.receipt_id
    inner join maslocations u on u.location_id = r.location_id
    where ri.installation_date is not null group by r.po_id
) as Ins on Ins.po_id = pi.po_id
left outer join (
    select ci.item_id, ci.basic_rate, ci.percentage, s.name, t.tender_no, convert(varchar, ac.contract_end_date, 103) as contract_end_date
    from contract_items ci
    inner join award_of_contract ac on ac.award_of_contract_id = ci.award_of_contract_id
    inner join massuppliers s on s.supplier_id = ac.supplier_id
    inner join tenders t on t.tender_id = ac.tender_id
    where getdate() between ac.contract_date and ac.contract_end_date
) rc on rc.item_id = m.item_id
left outer join tenders t on t.tender_id = ci.tender_id
where 1=1 {yearFilter} {indentFilter}
group by i.indent_id, i.indent_item_id, ind.facility_id, m.item_code_as_per_tender, m.item_id, pi.POQTY,
         ml.location_name, m.item_name, rc.name, rc.basic_rate, rc.percentage, f.facility_aut_code, f.facility_aut_id,
         id.consolidated_date, mif.year, pi.year, pi.po_date, pi.po_no, id.indent_con_no, id.description,
         u.user_name, u.user_id, pi.totalprice, sup.dispatchQTY, Ins.installedQTY, ci.remarks,
         rc.contract_end_date, t.tenderDT, t.tender_no, t.finalstatus
order by m.item_name";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            if (yrId > 0) cmd.Parameters.AddWithValue("@FinancialYearId", yrId);
            if (indId > 0) cmd.Parameters.AddWithValue("@IndentId", indId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new IndentPoTenderStatusDrillDownDTO
                {
                    user_id = reader["user_id"] != DBNull.Value ? Convert.ToInt32(reader["user_id"]) : (int?)null,
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
                    facility_id = reader["facility_id"] != DBNull.Value ? Convert.ToInt32(reader["facility_id"]) : (int?)null,
                    supplier_name = reader["supplier_name"]?.ToString(),
                    basic_rate = reader["basic_rate"] != DBNull.Value ? Convert.ToDecimal(reader["basic_rate"]) : 0,
                    percentage = reader["percentage"] != DBNull.Value ? Convert.ToDecimal(reader["percentage"]) : 0,
                    facility_aut_id = reader["facility_aut_id"] != DBNull.Value ? Convert.ToInt32(reader["facility_aut_id"]) : (int?)null,
                    item_id = reader["item_id"] != DBNull.Value ? Convert.ToInt32(reader["item_id"]) : (int?)null,
                    dispatchQTY = reader["dispatchQTY"] != DBNull.Value ? Convert.ToDecimal(reader["dispatchQTY"]) : 0,
                    installedQTY = reader["installedQTY"] != DBNull.Value ? Convert.ToDecimal(reader["installedQTY"]) : 0,
                    remarks = reader["remarks"]?.ToString(),
                    contract_end_date = reader["contract_end_date"]?.ToString(),
                    tenderDT = reader["tenderDT"]?.ToString(),
                    tender_no_drill = reader["tender_no_drill"]?.ToString(),
                    finalstatus = reader["finalstatus"]?.ToString()
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/indentpotendersummary?financialYearId=19
        [HttpGet("indentpotendersummary")]
        public async Task<IActionResult> GetIndentPoTenderSummary([FromQuery] int? financialYearId)
        {
            List<IndentPoTenderSummaryDTO> list = new List<IndentPoTenderSummaryDTO>();

            int finYrId = financialYearId ?? 0;
            string yearFilter = finYrId > 0 ? " and id.financial_year_id = @FinancialYearId " : "";

            string query = $@"
select b.user_name, b.user_id, sum(nosdistinctnoscount) as noscountItem,
       sum(indentQTY) as NoofEqIndent, sum(POQTY) as NoofEqPO, sum(BalancePO) as NoofEqBal,
       sum(netvalue) as netvalue, sum(grossvalue) as grossvalue, sum(tenderstatus) as NoEQLivInTender
from (
    select a.item_id, a.user_id, a.user_name, a.year, a.item_code_as_per_tender, a.item_name,
           count(distinct a.item_code_as_per_tender) as nosdistinctnoscount,
           sum(indentQTY) as indentQTY, sum(POQTY) as POQTY, sum(BalancePO) as BalancePO,
           a.basic_rate, sum(a.netvalue) as netvalue, sum(a.grossvalue) as grossvalue,
           case when a.tender_no is not null then 1 else 0 end as tenderstatus
    from (
        select u.user_id, u.user_name, mif.year, convert(varchar, id.consolidated_date, 103) as consolidated_date,
               id.indent_con_no, id.description, m.item_code_as_per_tender, m.item_name, f.facility_aut_code,
               sum(i.indent_quantity) as indentQTY, isnull(pi.POQTY, 0) as POQTY,
               sum(i.indent_quantity) - isnull(pi.POQTY, 0) as BalancePO,
               f.facility_aut_id, m.item_id, pi.year as POYear, pi.grossvalue, pi.netvalue, rc.basic_rate, rc.tender_no
        from indent_items i
        inner join masitems m on m.item_id = i.item_id
        inner join indent ind on ind.indent_id = i.indent_id
        inner join indent_cons_items ci on ci.indent_cons_items_id = ind.indent_cons_items_id
        inner join indent_consolidation id on id.indent_consolidation_id = ci.indent_consolidated_id
        inner join mas_financial_year mif on mif.financial_year_id = id.financial_year_id
        inner join maslocations ml on ml.location_id = ind.facility_id
        inner join users u on u.user_id = ml.user_id
        inner join facility_aut f on f.facility_aut_id = ml.authority
        left outer join (
            select convert(varchar, p.po_date, 103) as po_date, p.po_no, mf.year, consignee_id, sum(pi.quantity) as POQTY,
                   pi.item_id, pi.INDENT_CONSOLIDATION_ID, pi.directorate_id, pi.indent_id, pi.indent_item_id,
                   sum(pi.totalprice) as grossvalue, sum(pi.totalbasicPrice) as netvalue, l.user_id
            from po_items pi
            inner join purchase_order p on p.po_id = pi.po_id
            inner join mas_financial_year mf on mf.financial_year_id = pi.financial_year_id
            inner join maslocations l on l.location_id = pi.consignee_id
            where p.status not in ('Incomplete', 'Waiting For Approval', 'Cancelled')
            group by consignee_id, pi.item_id, pi.INDENT_CONSOLIDATION_ID, pi.directorate_id, pi.indent_id, pi.indent_item_id,
                     mf.year, p.po_date, p.po_no, l.user_id
        ) pi on pi.item_id = m.item_id and pi.consignee_id = ind.facility_id and pi.directorate_id = id.directorate_id and
                pi.indent_id = ind.indent_id and pi.indent_item_id = i.indent_item_id and pi.INDENT_CONSOLIDATION_ID = id.indent_consolidation_id
        left outer join (
            select ci.item_id, ci.basic_rate, ci.percentage, s.name, t.tender_no from contract_items ci
            inner join award_of_contract ac on ac.award_of_contract_id = ci.award_of_contract_id
            inner join massuppliers s on s.supplier_id = ac.supplier_id
            inner join tenders t on t.tender_id = ac.tender_id
            where getdate() between ac.contract_date and ac.contract_end_date
        ) rc on rc.item_id = m.item_id
        where 1=1 {yearFilter}
        group by m.item_code_as_per_tender, m.item_id, pi.POQTY, m.item_name, pi.grossvalue, pi.netvalue,
                 rc.name, rc.tender_no, rc.basic_rate, rc.percentage, f.facility_aut_code, f.facility_aut_id,
                 id.consolidated_date, mif.year, pi.year, id.indent_con_no, id.description, u.user_name, u.user_id
    ) a
    group by a.item_id, a.user_name, a.user_id, a.year, a.item_code_as_per_tender, a.item_name, a.basic_rate, a.tender_no
) b
group by b.user_name, b.user_id
order by user_name";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            if (finYrId > 0) cmd.Parameters.AddWithValue("@FinancialYearId", finYrId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new IndentPoTenderSummaryDTO
                {
                    user_name = reader["user_name"]?.ToString(),
                    user_id = reader["user_id"] != DBNull.Value ? Convert.ToInt32(reader["user_id"]) : (int?)null,
                    noscountItem = reader["noscountItem"] != DBNull.Value ? Convert.ToInt32(reader["noscountItem"]) : 0,
                    NoofEqIndent = reader["NoofEqIndent"] != DBNull.Value ? Convert.ToInt32(reader["NoofEqIndent"]) : 0,
                    NoofEqPO = reader["NoofEqPO"] != DBNull.Value ? Convert.ToInt32(reader["NoofEqPO"]) : 0,
                    NoofEqBal = reader["NoofEqBal"] != DBNull.Value ? Convert.ToInt32(reader["NoofEqBal"]) : 0,
                    netvalue = reader["netvalue"] != DBNull.Value ? Convert.ToDecimal(reader["netvalue"]) : 0,
                    grossvalue = reader["grossvalue"] != DBNull.Value ? Convert.ToDecimal(reader["grossvalue"]) : 0,
                    NoEQLivInTender = reader["NoEQLivInTender"] != DBNull.Value ? Convert.ToInt32(reader["NoEQLivInTender"]) : 0
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/indentpotendersummarydrilldown?userId=123&flag=EQP&yearId=19
        [HttpGet("indentpotendersummarydrilldown")]
        public async Task<IActionResult> GetIndentPoTenderSummaryDrilldown(
            [FromQuery] int? userId,
            [FromQuery] string? flag,
            [FromQuery] int? yearId)
        {
            List<IndentPoTenderSummaryDrillDownDTO> list = new List<IndentPoTenderSummaryDrillDownDTO>();

            int usrId = userId ?? 0;
            int finYrId = yearId ?? 0;

            string yearFilter = finYrId > 0 ? " and id.financial_year_id = @FinancialYearId " : "";
            string userFilter = usrId > 0 ? " where a.user_id = @UserId " : "";

            string query = $@"
select a.item_id, a.user_name, a.user_id, a.year, a.item_code_as_per_tender, a.item_name,
       a.basic_rate, a.tender_no, sum(indentQTY) as indentQTY, sum(POQTY) as POQTY, sum(BalancePO) as BalancePO,
       sum(a.netvalue) as netvalue, sum(a.grossvalue) as grossvalue,
       case when a.tender_no is not null then 'Live In Tender' else 'Pending' end as finalstatus
from (
    select u.user_id, u.user_name, mif.year, convert(varchar, id.consolidated_date, 103) as consolidated_date,
           id.indent_con_no, id.description, m.item_code_as_per_tender, m.item_name, f.facility_aut_code,
           sum(i.indent_quantity) as indentQTY, isnull(pi.POQTY, 0) as POQTY,
           sum(i.indent_quantity) - isnull(pi.POQTY, 0) as BalancePO,
           f.facility_aut_id, m.item_id, pi.year as POYear, pi.grossvalue, pi.netvalue, rc.basic_rate, rc.tender_no
    from indent_items i
    inner join masitems m on m.item_id = i.item_id
    inner join indent ind on ind.indent_id = i.indent_id
    inner join indent_cons_items ci on ci.indent_cons_items_id = ind.indent_cons_items_id
    inner join indent_consolidation id on id.indent_consolidation_id = ci.indent_consolidated_id
    inner join mas_financial_year mif on mif.financial_year_id = id.financial_year_id
    inner join maslocations ml on ml.location_id = ind.facility_id
    inner join users u on u.user_id = ml.user_id
    inner join facility_aut f on f.facility_aut_id = ml.authority
    left outer join (
        select convert(varchar, p.po_date, 103) as po_date, p.po_no, mf.year, consignee_id, sum(pi.quantity) as POQTY,
               pi.item_id, pi.INDENT_CONSOLIDATION_ID, pi.directorate_id, pi.indent_id, pi.indent_item_id,
               sum(pi.totalprice) as grossvalue, sum(pi.totalbasicPrice) as netvalue, l.user_id
        from po_items pi
        inner join purchase_order p on p.po_id = pi.po_id
        inner join mas_financial_year mf on mf.financial_year_id = pi.financial_year_id
        inner join maslocations l on l.location_id = pi.consignee_id
        where p.status not in ('Incomplete', 'Waiting For Approval', 'Cancelled')
        group by consignee_id, pi.item_id, pi.INDENT_CONSOLIDATION_ID, pi.directorate_id, pi.indent_id, pi.indent_item_id,
                 mf.year, p.po_date, p.po_no, l.user_id
    ) pi on pi.item_id = m.item_id and pi.consignee_id = ind.facility_id and pi.directorate_id = id.directorate_id and
            pi.indent_id = ind.indent_id and pi.indent_item_id = i.indent_item_id and pi.INDENT_CONSOLIDATION_ID = id.indent_consolidation_id
    left outer join (
        select ci.item_id, ci.basic_rate, ci.percentage, s.name, t.tender_no from contract_items ci
        inner join award_of_contract ac on ac.award_of_contract_id = ci.award_of_contract_id
        inner join massuppliers s on s.supplier_id = ac.supplier_id
        inner join tenders t on t.tender_id = ac.tender_id
        where getdate() between ac.contract_date and ac.contract_end_date
    ) rc on rc.item_id = m.item_id
    where 1=1 {yearFilter}
    group by m.item_code_as_per_tender, m.item_id, pi.POQTY, m.item_name, pi.grossvalue, pi.netvalue,
             rc.name, rc.tender_no, rc.basic_rate, rc.percentage, f.facility_aut_code, f.facility_aut_id,
             id.consolidated_date, mif.year, pi.year, id.indent_con_no, id.description, u.user_name, u.user_id
) a
{userFilter}
group by a.item_id, a.user_name, a.user_id, a.year, a.item_code_as_per_tender, a.item_name, a.basic_rate, a.tender_no
order by a.item_name";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            if (finYrId > 0) cmd.Parameters.AddWithValue("@FinancialYearId", finYrId);
            if (usrId > 0) cmd.Parameters.AddWithValue("@UserId", usrId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new IndentPoTenderSummaryDrillDownDTO
                {
                    item_id = reader["item_id"] != DBNull.Value ? Convert.ToInt32(reader["item_id"]) : (int?)null,
                    user_name = reader["user_name"]?.ToString(),
                    user_id = reader["user_id"] != DBNull.Value ? Convert.ToInt32(reader["user_id"]) : (int?)null,
                    year = reader["year"]?.ToString(),
                    item_code_as_per_tender = reader["item_code_as_per_tender"]?.ToString(),
                    item_name = reader["item_name"]?.ToString(),
                    basic_rate = reader["basic_rate"] != DBNull.Value ? Convert.ToDecimal(reader["basic_rate"]) : 0,
                    tender_no = reader["tender_no"]?.ToString(),
                    indentQTY = reader["indentQTY"] != DBNull.Value ? Convert.ToDecimal(reader["indentQTY"]) : 0,
                    POQTY = reader["POQTY"] != DBNull.Value ? Convert.ToDecimal(reader["POQTY"]) : 0,
                    BalancePO = reader["BalancePO"] != DBNull.Value ? Convert.ToDecimal(reader["BalancePO"]) : 0,
                    finalstatus = reader["finalstatus"]?.ToString()
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/pending-po-supplier-wise?supplierId=123
        [HttpGet("pending-po-supplier-wise")]
        public async Task<IActionResult> GetPendingPoSupplierWise([FromQuery] int supplierId)
        {
            List<PendingPoSupWiseDto> list = new List<PendingPoSupWiseDto>();
            string query = @"
select m.item_code_as_per_tender, m.item_name, sp.name, b.outward_no + '-' + b.po_no as PONO,
       convert(varchar, b.po_date, 103) as PODate, sum(quantity) as POQTY, isnull(Supplyqty, 0) as Supplyqty,
       isnull(receiptQTY, 0) as receiptQTY, isnull(insqty, 0) as insqty,
       case when sum(quantity) = isnull(insqty, 0) and sum(quantity) = isnull(Supplyqty, 0) then 'Installation Completed'
            else case when isnull(Supplyqty, 0) = 0 or sum(quantity) > isnull(Supplyqty, 0) then 'Dispatch Pending'
            else 'Pending For Receipt/Installation' end end as status,
       case when m.pid is not null then 'COVID Related' else 'Normal' end as PID,
       sp.supplier_id, f.year, b.po_id
from po_items a
inner join masitems m on m.item_id = a.item_id
inner join purchase_order b on (a.po_id = b.po_id)
inner join mas_financial_year f on f.financial_year_id = b.financial_year_id
inner join massuppliers sp on sp.supplier_id = b.supplier_id
left outer join (
    select po_id, isnull(sum(Supplyqty), 0) as Supplyqty from SupplierDispatch d
    inner join Issue_item_details i on d.Issue_id = i.Issue_id
    where d.status = 'C' group by po_id
) as sup on sup.po_id = a.po_id
left outer join (
    select isnull(sum(r.receipt_qty), 0) as receiptQTY, r.po_id from receipts r
    where r.recieved_date is not null and r.status in ('C', 'Received') group by po_id
) as re on re.po_id = a.po_id
left outer join (
    select sum(ri.received_qty) as insqty, r.po_id from receipts r
    left outer join receipt_item_details ri on ri.receipt_id = r.receipt_id
    where r.recieved_date is not null and r.status in ('C') group by r.po_id
) as ins on ins.po_id = a.po_id
where (b.supplier_id = @SupplierId or @SupplierId = 0)
  and b.status in ('Order Placed', 'Partially Received', 'Completed')
group by b.po_id, isnull(Supplyqty, 0), isnull(receiptQTY, 0), isnull(insqty, 0), b.po_no, b.po_date,
         b.outward_no, m.item_code_as_per_tender, m.item_name, sp.name, sp.supplier_id, m.pid, f.year
having (case when sum(quantity) = isnull(insqty, 0) and sum(quantity) = isnull(Supplyqty, 0) then 'Installation Completed'
             else case when isnull(Supplyqty, 0) = 0 or sum(quantity) > isnull(Supplyqty, 0) then 'Dispatch Pending'
             else 'Pending For Receipt/Installation' end end) in ('Dispatch Pending', 'Pending For Receipt/Installation')
order by b.po_date desc";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@SupplierId", supplierId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new PendingPoSupWiseDto
                {
                    item_code_as_per_tender = reader["item_code_as_per_tender"]?.ToString(),
                    item_name = reader["item_name"]?.ToString(),
                    name = reader["name"]?.ToString(),
                    PONO = reader["PONO"]?.ToString(),
                    PODate = reader["PODate"]?.ToString(),
                    POQTY = reader["POQTY"] != DBNull.Value ? Convert.ToDecimal(reader["POQTY"]) : 0,
                    Supplyqty = reader["Supplyqty"] != DBNull.Value ? Convert.ToDecimal(reader["Supplyqty"]) : 0,
                    receiptQTY = reader["receiptQTY"] != DBNull.Value ? Convert.ToDecimal(reader["receiptQTY"]) : 0,
                    insqty = reader["insqty"] != DBNull.Value ? Convert.ToDecimal(reader["insqty"]) : 0,
                    status = reader["status"]?.ToString(),
                    PID = reader["PID"]?.ToString(),
                    supplier_id = reader["supplier_id"] != DBNull.Value ? Convert.ToInt32(reader["supplier_id"]) : (int?)null,
                    year = reader["year"]?.ToString(),
                    po_id = reader["po_id"] != DBNull.Value ? Convert.ToInt32(reader["po_id"]) : (int?)null
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/balance-supplierwise
        [HttpGet("balance-supplierwise")]
        public async Task<IActionResult> GetBalanceSupplierwise()
        {
            List<BalanceSupplierWiseDto> list = new List<BalanceSupplierWiseDto>();
            string query = @"
select sp.supplier_id, sp.name as supplier_name,
       count(distinct p.po_id) as total_pos,
       count(distinct pi.item_id) as total_items,
       sum(pi.quantity) as po_qty,
       sum(isnull(sup.Supplyqty, 0)) as dispatched_qty,
       sum(isnull(re.receiptQTY, 0)) as receipt_qty,
       sum(isnull(ins.insqty, 0)) as installed_qty,
       sum(pi.quantity) - sum(isnull(sup.Supplyqty, 0)) as balance_dispatch,
       sum(isnull(sup.Supplyqty, 0)) - sum(isnull(re.receiptQTY, 0)) as balance_receipt,
       sum(isnull(re.receiptQTY, 0)) - sum(isnull(ins.insqty, 0)) as balance_install
from purchase_order p
inner join massuppliers sp on sp.supplier_id = p.supplier_id
inner join po_items pi on pi.po_id = p.po_id
left outer join (
    select po_id, isnull(sum(Supplyqty), 0) as Supplyqty from SupplierDispatch d
    inner join Issue_item_details i on d.Issue_id = i.Issue_id
    where d.status = 'C' group by po_id
) as sup on sup.po_id = pi.po_id
left outer join (
    select isnull(sum(r.receipt_qty), 0) as receiptQTY, r.po_id from receipts r
    where r.recieved_date is not null and r.status in ('C', 'Received') group by po_id
) as re on re.po_id = pi.po_id
left outer join (
    select sum(ri.received_qty) as insqty, r.po_id from receipts r
    left outer join receipt_item_details ri on ri.receipt_id = r.receipt_id
    where r.recieved_date is not null and r.status in ('C') group by r.po_id
) as ins on ins.po_id = pi.po_id
where p.status in ('Order Placed', 'Partially Received', 'Completed')
group by sp.supplier_id, sp.name
order by sp.name";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new BalanceSupplierWiseDto
                {
                    supplier_id = reader["supplier_id"] != DBNull.Value ? Convert.ToInt32(reader["supplier_id"]) : (int?)null,
                    supplier_name = reader["supplier_name"]?.ToString(),
                    total_pos = reader["total_pos"] != DBNull.Value ? Convert.ToInt32(reader["total_pos"]) : 0,
                    total_items = reader["total_items"] != DBNull.Value ? Convert.ToInt32(reader["total_items"]) : 0,
                    po_qty = reader["po_qty"] != DBNull.Value ? Convert.ToDecimal(reader["po_qty"]) : 0,
                    dispatched_qty = reader["dispatched_qty"] != DBNull.Value ? Convert.ToDecimal(reader["dispatched_qty"]) : 0,
                    receipt_qty = reader["receipt_qty"] != DBNull.Value ? Convert.ToDecimal(reader["receipt_qty"]) : 0,
                    installed_qty = reader["installed_qty"] != DBNull.Value ? Convert.ToDecimal(reader["installed_qty"]) : 0,
                    balance_dispatch = reader["balance_dispatch"] != DBNull.Value ? Convert.ToDecimal(reader["balance_dispatch"]) : 0,
                    balance_receipt = reader["balance_receipt"] != DBNull.Value ? Convert.ToDecimal(reader["balance_receipt"]) : 0,
                    balance_install = reader["balance_install"] != DBNull.Value ? Convert.ToDecimal(reader["balance_install"]) : 0
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/balance-status-supplier?balanceType=I
        [HttpGet("balance-status-supplier")]
        public async Task<IActionResult> GetBalanceStatusSupplier([FromQuery] string balanceType = "I", [FromQuery] int supplierId = 0)
        {
            return await GetBalanceStatus(balanceType, 0);
        }

        // GET: api/Reports/payment-report?poType=NP
        [HttpGet("payment-report")]
        public async Task<IActionResult> GetPaymentReport([FromQuery] string? poType)
        {
            List<PaymentReportDto> list = new List<PaymentReportDto>();
            string poFilter = !string.IsNullOrEmpty(poType) ? " and isnull(p.potype, 'NP') = @PoType " : "";

            string query = $@"
select p.po_id, p.po_no, convert(varchar, p.po_date, 103) as po_date, t.tender_no, sp.name as supplier_name,
       m.item_name, sum(pi.totalprice) as totalprice,
       isnull(sum(pay.PAIDAMT), 0) as paid_amount,
       sum(pi.totalprice) - isnull(sum(pay.PAIDAMT), 0) as balance_amount,
       case when isnull(sum(pay.PAIDAMT), 0) = 0 then 'Unpaid'
            when isnull(sum(pay.PAIDAMT), 0) >= sum(pi.totalprice) then 'Paid'
            else 'Partially Paid' end as payment_status
from purchase_order p
inner join massuppliers sp on sp.supplier_id = p.supplier_id
inner join po_items pi on pi.po_id = p.po_id
inner join masitems m on m.item_id = pi.item_id
inner join tenders t on t.tender_id = p.tender_id
left outer join BLPSANCTIONS s on s.po_id = p.po_id
left outer join BLPPAYMENTS pay on pay.paymentid = s.paymentid and pay.status = 'P'
where p.status in ('Order Placed', 'Partially Received', 'Completed') {poFilter}
group by p.po_id, p.po_no, p.po_date, t.tender_no, sp.name, m.item_name
order by p.po_date desc";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            if (!string.IsNullOrEmpty(poType)) cmd.Parameters.AddWithValue("@PoType", poType);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new PaymentReportDto
                {
                    po_id = reader["po_id"] != DBNull.Value ? Convert.ToInt32(reader["po_id"]) : (int?)null,
                    po_no = reader["po_no"]?.ToString(),
                    po_date = reader["po_date"]?.ToString(),
                    tender_no = reader["tender_no"]?.ToString(),
                    supplier_name = reader["supplier_name"]?.ToString(),
                    item_name = reader["item_name"]?.ToString(),
                    totalprice = reader["totalprice"] != DBNull.Value ? Convert.ToDecimal(reader["totalprice"]) : 0,
                    paid_amount = reader["paid_amount"] != DBNull.Value ? Convert.ToDecimal(reader["paid_amount"]) : 0,
                    balance_amount = reader["balance_amount"] != DBNull.Value ? Convert.ToDecimal(reader["balance_amount"]) : 0,
                    payment_status = reader["payment_status"]?.ToString()
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/opening-stock-summary?directorateId=12
        [HttpGet("opening-stock-summary")]
        public async Task<IActionResult> GetOpeningStockSummary([FromQuery] int directorateId = 12)
        {
            List<OpeningStockSummaryDto> list = new List<OpeningStockSummaryDto>();
            string query = "";

            if (directorateId == 12) // DME
            {
                query = @"
select u.user_name, u.user_id, count(e.existing_item_id) as nos from users u
left outer join maslocations l on l.user_id = u.user_id
left outer join existing_item e on e.location_id = l.location_id
where u.authority = 12 and u.user_id not in (12)
group by u.user_name, u.user_id
order by u.user_name";
            }
            else // DHS and others
            {
                query = @"
select d.DBStart_Name_En as user_name, d.DP_DistrictID as user_id, count(e.existing_item_id) as nos
from maslocations l
left outer join existing_item e on e.location_id = l.location_id
left outer join Districts d on d.DP_DistrictID = l.DP_DistrictID
where l.authority = @DirectorateId and d.District_Name is not null
group by d.DBStart_Name_En, d.DP_DistrictID
order by d.DBStart_Name_En";
            }

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@DirectorateId", directorateId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new OpeningStockSummaryDto
                {
                    user_name = reader["user_name"]?.ToString(),
                    user_id = reader["user_id"] != DBNull.Value ? Convert.ToInt32(reader["user_id"]) : (int?)null,
                    nos = reader["nos"] != DBNull.Value ? Convert.ToInt32(reader["nos"]) : 0
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/opening-stock-detail?userId=123&directorateId=12
        [HttpGet("opening-stock-detail")]
        public async Task<IActionResult> GetOpeningStockDetail([FromQuery] int userId, [FromQuery] int directorateId = 12)
        {
            List<OpeningStockDetailDto> list = new List<OpeningStockDetailDto>();
            string filter = directorateId == 12 ? " where u.user_id = @UserId " : " where l.DP_DistrictID = @UserId ";

            string query = $@"
select e.existing_item_id, m.item_name, e.serial_no, l.location_name, u.user_name,
       convert(varchar, e.entry_date, 103) as entry_date,
       case when e.working_status = 'W' then 'Working' else 'Not Working' end as working_status
from existing_item e
inner join masitems m on m.item_id = e.item_id
inner join maslocations l on l.location_id = e.location_id
left outer join users u on u.user_id = l.user_id
{filter}
order by m.item_name";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@UserId", userId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new OpeningStockDetailDto
                {
                    existing_item_id = reader["existing_item_id"] != DBNull.Value ? Convert.ToInt32(reader["existing_item_id"]) : (int?)null,
                    item_name = reader["item_name"]?.ToString(),
                    serial_no = reader["serial_no"]?.ToString(),
                    location_name = reader["location_name"]?.ToString(),
                    user_name = reader["user_name"]?.ToString(),
                    entry_date = reader["entry_date"]?.ToString(),
                    working_status = reader["working_status"]?.ToString()
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/po-summary?financialYearId=19&directorateId=5
        [HttpGet("po-summary")]
        public async Task<IActionResult> GetPOSummary([FromQuery] int financialYearId, [FromQuery] int directorateId = 5)
        {
            List<POSummaryDirectorateDto> list = new List<POSummaryDirectorateDto>();
            string query = @"
select CODE, ITEM_NAME, sum(quantity) as quantity, basic_rate, percentage, single_unit_price,
       sum(totalPOvalue) as totalPOvalue
from (
    select R.ITEM_CODE_AS_PER_TENDER as CODE, R.item_name as ITEM_NAME, pi.quantity,
           c.basic_rate, c.percentage, c.single_unit_price,
           c.single_unit_price * pi.quantity as totalPOvalue
    from po_items pi
    inner join MASITEMS R on R.ITEM_ID = pi.item_id
    inner join maslocations m on m.location_id = pi.consignee_id
    inner join purchase_order p on pi.po_id = p.po_id
    inner join MASSUPPLIERS b on p.supplier_id = b.SUPPLIER_ID
    inner join MAS_FINANCIAL_YEAR E on E.FINANCIAL_YEAR_ID = p.FINANCIAL_YEAR_ID
    inner join facility_aut aut on aut.facility_aut_id = p.directorate_id
    left outer join (
        select a.supplier_id, a.tender_id, ci.item_id, ci.basic_rate, ci.percentage,
               ci.single_unit_price, t.tender_no, t.tender_date
        from award_of_contract a
        inner join contract_items ci on ci.award_of_contract_id = a.award_of_contract_id
        inner join tenders t on t.tender_id = a.tender_id
    ) c on c.item_id = pi.item_id and c.tender_id = p.tender_id and c.supplier_id = p.supplier_id
    where (p.financial_year_id = @FinancialYearId or @FinancialYearId = 0)
      and (p.directorate_id = @DirectorateId or @DirectorateId = 0)
      and p.status not in ('Incomplete', 'Waiting For Approval', 'Cancelled')
) a
group by CODE, ITEM_NAME, basic_rate, percentage, single_unit_price
order by ITEM_NAME";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId);
            cmd.Parameters.AddWithValue("@DirectorateId", directorateId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new POSummaryDirectorateDto
                {
                    CODE = reader["CODE"]?.ToString(),
                    ITEM_NAME = reader["ITEM_NAME"]?.ToString(),
                    quantity = reader["quantity"] != DBNull.Value ? Convert.ToDecimal(reader["quantity"]) : 0,
                    basic_rate = reader["basic_rate"] != DBNull.Value ? Convert.ToDecimal(reader["basic_rate"]) : 0,
                    percentage = reader["percentage"] != DBNull.Value ? Convert.ToDecimal(reader["percentage"]) : 0,
                    single_unit_price = reader["single_unit_price"] != DBNull.Value ? Convert.ToDecimal(reader["single_unit_price"]) : 0,
                    totalPOvalue = reader["totalPOvalue"] != DBNull.Value ? Convert.ToDecimal(reader["totalPOvalue"]) : 0
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/po-summary-detail?finYrId=19&itemCode=E01&directorateId=5&flag=
        [HttpGet("po-summary-detail")]
        public async Task<IActionResult> GetPOSummaryDetail(
            [FromQuery] int finYrId,
            [FromQuery] string? itemCode,
            [FromQuery] int directorateId = 5,
            [FromQuery] string? flag = "")
        {
            List<POSummaryDirectorateDrillDownDto> list = new List<POSummaryDirectorateDrillDownDto>();
            string itemFilter = !string.IsNullOrEmpty(itemCode) ? " and R.ITEM_CODE_AS_PER_TENDER = @ItemCode " : "";

            string query = $@"
select m.location_id, m.location_name, m.DP_DistrictID, m.user_id, u.user_name, u.user_type, u.designation,
       R.ITEM_CODE_AS_PER_TENDER as CODE, R.item_name as ITEM_NAME, p.OUTWARD_NO,
       convert(varchar, p.po_date, 103) as po_date, pi.quantity, c.basic_rate, c.percentage,
       c.single_unit_price, c.single_unit_price * pi.quantity as totalPOvalue,
       b.NAME as SUPPLIER_NAME, b.mobile_no, c.TENDER_NO, convert(varchar, c.TENDER_DATE, 103) as TENDER_DATE,
       p.STATUS, p.REMARKS, pi.item_id, p.FINANCIAL_YEAR_ID, E.YEAR, p.TENDER_ID, p.PO_NO,
       p.SUPPLIER_ID, p.directorate_id, p.indent_fund_id, p.PO_ID, d.DBStart_Name_En
from po_items pi
inner join MASITEMS R on R.ITEM_ID = pi.item_id
inner join maslocations m on m.location_id = pi.consignee_id
inner join purchase_order p on pi.po_id = p.po_id
inner join MASSUPPLIERS b on p.supplier_id = b.SUPPLIER_ID
inner join MAS_FINANCIAL_YEAR E on E.FINANCIAL_YEAR_ID = p.FINANCIAL_YEAR_ID
left outer join (
    select a.supplier_id, a.tender_id, ci.item_id, ci.basic_rate, ci.percentage,
           ci.single_unit_price, t.tender_no, t.tender_date
    from award_of_contract a
    inner join contract_items ci on ci.award_of_contract_id = a.award_of_contract_id
    inner join tenders t on t.tender_id = a.tender_id
) c on c.item_id = pi.item_id and c.tender_id = p.tender_id and c.supplier_id = p.supplier_id
left outer join users u on u.user_id = m.user_id
left outer join Districts d on d.DP_DistrictID = m.DP_DistrictID
where (p.financial_year_id = @FinYrId or @FinYrId = 0)
  and (p.directorate_id = @DirectorateId or @DirectorateId = 0)
  and p.status not in ('Incomplete', 'Waiting For Approval', 'Cancelled')
  {itemFilter}
order by m.location_name";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@FinYrId", finYrId);
            cmd.Parameters.AddWithValue("@DirectorateId", directorateId);
            if (!string.IsNullOrEmpty(itemCode)) cmd.Parameters.AddWithValue("@ItemCode", itemCode);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new POSummaryDirectorateDrillDownDto
                {
                    location_id = reader["location_id"] != DBNull.Value ? Convert.ToInt32(reader["location_id"]) : (int?)null,
                    location_name = reader["location_name"]?.ToString(),
                    DP_DistrictID = reader["DP_DistrictID"] != DBNull.Value ? Convert.ToInt32(reader["DP_DistrictID"]) : (int?)null,
                    user_id = reader["user_id"] != DBNull.Value ? Convert.ToInt32(reader["user_id"]) : (int?)null,
                    user_name = reader["user_name"]?.ToString(),
                    user_type = reader["user_type"]?.ToString(),
                    designation = reader["designation"]?.ToString(),
                    CODE = reader["CODE"]?.ToString(),
                    ITEM_NAME = reader["ITEM_NAME"]?.ToString(),
                    OUTWARD_NO = reader["OUTWARD_NO"]?.ToString(),
                    po_date = reader["po_date"]?.ToString(),
                    quantity = reader["quantity"] != DBNull.Value ? Convert.ToDecimal(reader["quantity"]) : 0,
                    basic_rate = reader["basic_rate"] != DBNull.Value ? Convert.ToDecimal(reader["basic_rate"]) : 0,
                    percentage = reader["percentage"] != DBNull.Value ? Convert.ToDecimal(reader["percentage"]) : 0,
                    single_unit_price = reader["single_unit_price"] != DBNull.Value ? Convert.ToDecimal(reader["single_unit_price"]) : 0,
                    totalPOvalue = reader["totalPOvalue"] != DBNull.Value ? Convert.ToDecimal(reader["totalPOvalue"]) : 0,
                    SUPPLIER_NAME = reader["SUPPLIER_NAME"]?.ToString(),
                    mobile_no = reader["mobile_no"]?.ToString(),
                    TENDER_NO = reader["TENDER_NO"]?.ToString(),
                    TENDER_DATE = reader["TENDER_DATE"]?.ToString(),
                    STATUS = reader["STATUS"]?.ToString(),
                    REMARKS = reader["REMARKS"]?.ToString(),
                    item_id = reader["item_id"] != DBNull.Value ? Convert.ToInt32(reader["item_id"]) : (int?)null,
                    FINANCIAL_YEAR_ID = reader["FINANCIAL_YEAR_ID"] != DBNull.Value ? Convert.ToInt32(reader["FINANCIAL_YEAR_ID"]) : (int?)null,
                    YEAR = reader["YEAR"]?.ToString(),
                    TENDER_ID = reader["TENDER_ID"] != DBNull.Value ? Convert.ToInt32(reader["TENDER_ID"]) : (int?)null,
                    PO_NO = reader["PO_NO"]?.ToString(),
                    SUPPLIER_ID = reader["SUPPLIER_ID"] != DBNull.Value ? Convert.ToInt32(reader["SUPPLIER_ID"]) : (int?)null,
                    directorate_id = reader["directorate_id"] != DBNull.Value ? Convert.ToInt32(reader["directorate_id"]) : (int?)null,
                    indent_fund_id = reader["indent_fund_id"] != DBNull.Value ? Convert.ToInt32(reader["indent_fund_id"]) : (int?)null,
                    PO_ID = reader["PO_ID"] != DBNull.Value ? Convert.ToInt32(reader["PO_ID"]) : (int?)null,
                    DBStart_Name_En = reader["DBStart_Name_En"]?.ToString()
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/po-summary-consignee-ho?financialYearId=19&directorateId=5
        [HttpGet("po-summary-consignee-ho")]
        public async Task<IActionResult> GetPOSummaryConsigneeHO([FromQuery] int financialYearId, [FromQuery] int directorateId = 0)
        {
            List<POSummaryConsigneeHoDto> list = new List<POSummaryConsigneeHoDto>();
            string dirFilter = directorateId > 0 ? " and p.directorate_id = @DirectorateId " : "";

            string query = $@"
select distinct p.outward_no, case when isnull(p.potype, 'NP') = 'NP' then 'Normal PO' else 'Covid Po' end as potype,
       p.po_no, convert(varchar, p.po_date, 103) as podate, ms.name, m.item_code_as_per_tender, m.item_name,
       dis.DBStart_Name_En, pi.consignee_id, ml.location_name, pi.quantity as po_qty,
       isnull(sdd.supply_qty, 0) as supply_qty, isnull(re.receiptQTY, 0) as received_qty,
       isnull(ins.insqty, 0) as Install_qty, p.po_id,
       case when m.categoryId = 2 then 'Reagent' else 'Equipment' end as Eqp_Type,
       m.categoryId, p.po_date, pi.totalprice, ci.basic_rate, ci.percentage, ci.single_unit_price
from purchase_order p
inner join po_items pi on pi.po_id = p.po_id
inner join contract_items ci on ci.contract_item_id = pi.contract_item_id
inner join masitems m on m.item_id = pi.item_id
inner join maslocations ml on ml.location_id = pi.consignee_id
inner join Districts dis on dis.DP_DistrictID = ml.DP_DistrictID
inner join massuppliers ms on ms.supplier_id = p.supplier_id
left outer join (
    select sd.po_id, sum(id.supplyqty) as supply_qty, sd.location_id
    from SupplierDispatch sd
    inner join Issue_item_details id on id.Issue_id = sd.Issue_id
    where sd.status = 'C' group by sd.po_id, sd.location_id
) sdd on sdd.po_id = pi.po_id and sdd.location_id = pi.consignee_id
left outer join (
    select isnull(sum(r.receipt_qty), 0) as receiptQTY, r.po_id, r.location_id from receipts r
    where r.recieved_date is not null and r.status in ('C', 'Received') group by po_id, location_id
) as re on re.po_id = pi.po_id and re.location_id = pi.consignee_id
left outer join (
    select sum(ri.received_qty) as insqty, r.po_id, r.location_id from receipts r
    left outer join receipt_item_details ri on ri.receipt_id = r.receipt_id
    where r.recieved_date is not null and r.status in ('C') group by r.po_id, r.location_id
) as ins on ins.po_id = pi.po_id and ins.location_id = pi.consignee_id
where (p.financial_year_id = @FinancialYearId or @FinancialYearId = 0) {dirFilter}
order by p.po_date desc";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId);
            if (directorateId > 0) cmd.Parameters.AddWithValue("@DirectorateId", directorateId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new POSummaryConsigneeHoDto
                {
                    outward_no = reader["outward_no"]?.ToString(),
                    potype = reader["potype"]?.ToString(),
                    po_no = reader["po_no"]?.ToString(),
                    podate = reader["podate"]?.ToString(),
                    name = reader["name"]?.ToString(),
                    item_code_as_per_tender = reader["item_code_as_per_tender"]?.ToString(),
                    item_name = reader["item_name"]?.ToString(),
                    DBStart_Name_En = reader["DBStart_Name_En"]?.ToString(),
                    consignee_id = reader["consignee_id"] != DBNull.Value ? Convert.ToInt32(reader["consignee_id"]) : (int?)null,
                    location_name = reader["location_name"]?.ToString(),
                    po_qty = reader["po_qty"] != DBNull.Value ? Convert.ToDecimal(reader["po_qty"]) : 0,
                    supply_qty = reader["supply_qty"] != DBNull.Value ? Convert.ToDecimal(reader["supply_qty"]) : 0,
                    received_qty = reader["received_qty"] != DBNull.Value ? Convert.ToDecimal(reader["received_qty"]) : 0,
                    Install_qty = reader["Install_qty"] != DBNull.Value ? Convert.ToDecimal(reader["Install_qty"]) : 0,
                    po_id = reader["po_id"] != DBNull.Value ? Convert.ToInt32(reader["po_id"]) : (int?)null,
                    Eqp_Type = reader["Eqp_Type"]?.ToString(),
                    categoryId = reader["categoryId"] != DBNull.Value ? Convert.ToInt32(reader["categoryId"]) : (int?)null,
                    totalprice = reader["totalprice"] != DBNull.Value ? Convert.ToDecimal(reader["totalprice"]) : 0,
                    basic_rate = reader["basic_rate"] != DBNull.Value ? Convert.ToDecimal(reader["basic_rate"]) : 0,
                    percentage = reader["percentage"] != DBNull.Value ? Convert.ToDecimal(reader["percentage"]) : 0,
                    single_unit_price = reader["single_unit_price"] != DBNull.Value ? Convert.ToDecimal(reader["single_unit_price"]) : 0
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/po-receipt-summary
        [HttpGet("po-receipt-summary")]
        public async Task<IActionResult> GetPOReceiptSummary([FromQuery] int? financialYearId, [FromQuery] int? directorateId, [FromQuery] bool? nonReceiptOnly)
        {
            List<POReceiptSummaryDto> list = new List<POReceiptSummaryDto>();
            string whNonReceipt = nonReceiptOnly == true ? " and pi.quantity > isnull(re.receiptQTY, 0) " : "";
            string finFilter = financialYearId.HasValue && financialYearId > 0 ? " and p.financial_year_id = @FinancialYearId " : "";
            string dirFilter = directorateId.HasValue && directorateId > 0 ? " and p.directorate_id = @DirectorateId " : "";

            string query = $@"
select p.po_id, t.tender_no, f.year, p.outward_no, p.po_no, p.outward_no + '/' + p.po_no as pono,
       convert(varchar, p.po_date, 103) as po_date, dir.facility_aut_name,
       case when m.categoryId = 2 then 'Reagent' else 'Equipment' end as EQPTyp,
       m.item_code_as_per_tender, m.item_name, sp.name as Supplier, pi.quantity as POQTY,
       isnull(Supplyqty, 0) as Supplyqty, isnull(re.receiptQTY, 0) as receiptQTY,
       convert(varchar, red.LastRDate, 103) as LastRDate,
       isnull(ins.insqty, 0) as insqty,
       case when isnull(p.potype, 'NP') = 'NP' then 'Normal PO' else 'Covid Po' end as potype,
       t.cancellationdays, datediff(day, p.po_date, red.LastRDate) as DaystakentoSupply,
       convert(varchar, dateadd(dd, 120, p.po_date), 103) as lstsupplydt,
       datediff(day, p.po_date, getdate()) as todays
from purchase_order p
inner join massuppliers sp on sp.supplier_id = p.supplier_id
inner join mas_financial_year f on f.financial_year_id = p.financial_year_id
left outer join (
    select sum(pi.quantity) as quantity, pi.po_id, pi.item_id from po_items pi group by pi.po_id, pi.item_id
) pi on pi.po_id = p.po_id
inner join tenders t on t.tender_id = p.tender_id
inner join masitems m on m.item_id = pi.item_id
inner join facility_aut dir on dir.facility_aut_id = p.directorate_id
left outer join (
    select po_id, isnull(sum(Supplyqty), 0) as Supplyqty from SupplierDispatch d
    inner join Issue_item_details i on d.Issue_id = i.Issue_id
    where d.status = 'C' group by po_id
) as sup on sup.po_id = pi.po_id
left outer join (
    select isnull(sum(r.receipt_qty), 0) as receiptQTY, r.po_id from receipts r
    where r.recieved_date is not null and r.status in ('C', 'Received') group by po_id
) as re on re.po_id = pi.po_id
left outer join (
    select sum(ri.received_qty) as insqty, r.po_id from receipts r
    left outer join receipt_item_details ri on ri.receipt_id = r.receipt_id
    where r.recieved_date is not null and r.status in ('C') group by r.po_id
) as ins on ins.po_id = pi.po_id
left outer join (
    select max(r.recieved_date) as LastRDate, r.po_id from receipts r
    left outer join receipt_item_details ri on ri.receipt_id = r.receipt_id
    where r.recieved_date is not null and r.status in ('C', 'Received') group by r.po_id
) as red on red.po_id = pi.po_id
where p.status in ('Order Placed', 'Partially Received', 'Completed') {finFilter} {dirFilter} {whNonReceipt}
order by p.po_date desc";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            if (financialYearId.HasValue && financialYearId > 0) cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId.Value);
            if (directorateId.HasValue && directorateId > 0) cmd.Parameters.AddWithValue("@DirectorateId", directorateId.Value);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new POReceiptSummaryDto
                {
                    po_id = reader["po_id"] != DBNull.Value ? Convert.ToInt32(reader["po_id"]) : (int?)null,
                    tender_no = reader["tender_no"]?.ToString(),
                    year = reader["year"]?.ToString(),
                    outward_no = reader["outward_no"]?.ToString(),
                    po_no = reader["po_no"]?.ToString(),
                    pono = reader["pono"]?.ToString(),
                    po_date = reader["po_date"]?.ToString(),
                    facility_aut_name = reader["facility_aut_name"]?.ToString(),
                    EQPTyp = reader["EQPTyp"]?.ToString(),
                    item_code_as_per_tender = reader["item_code_as_per_tender"]?.ToString(),
                    item_name = reader["item_name"]?.ToString(),
                    Supplier = reader["Supplier"]?.ToString(),
                    POQTY = reader["POQTY"] != DBNull.Value ? Convert.ToDecimal(reader["POQTY"]) : 0,
                    Supplyqty = reader["Supplyqty"] != DBNull.Value ? Convert.ToDecimal(reader["Supplyqty"]) : 0,
                    receiptQTY = reader["receiptQTY"] != DBNull.Value ? Convert.ToDecimal(reader["receiptQTY"]) : 0,
                    LastRDate = reader["LastRDate"]?.ToString(),
                    insqty = reader["insqty"] != DBNull.Value ? Convert.ToDecimal(reader["insqty"]) : 0,
                    potype = reader["potype"]?.ToString(),
                    cancellationdays = reader["cancellationdays"] != DBNull.Value ? Convert.ToInt32(reader["cancellationdays"]) : (int?)null,
                    DaystakentoSupply = reader["DaystakentoSupply"] != DBNull.Value ? Convert.ToInt32(reader["DaystakentoSupply"]) : (int?)null,
                    lstsupplydt = reader["lstsupplydt"]?.ToString(),
                    todays = reader["todays"] != DBNull.Value ? Convert.ToInt32(reader["todays"]) : (int?)null
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/fac-stock-covid?districtId=1&facilityId=2&itemId=3&status=W
        [HttpGet("fac-stock-covid")]
        public async Task<IActionResult> GetFacStockCovid(
            [FromQuery] int districtId = 0,
            [FromQuery] int facilityId = 0,
            [FromQuery] int itemId = 0,
            [FromQuery] string? status = "")
        {
            List<FacStockCovidDto> list = new List<FacStockCovidDto>();
            string query = @"
select l.location_id as facility_id, l.location_name as facility_name, d.DBStart_Name_En as district_name,
       m.item_name, count(e.existing_item_id) as in_stock,
       sum(case when e.working_status = 'W' then 1 else 0 end) as working,
       sum(case when e.working_status != 'W' then 1 else 0 end) as not_working,
       count(e.existing_item_id) as installed
from existing_item e
inner join masitems m on m.item_id = e.item_id
inner join maslocations l on l.location_id = e.location_id
left outer join Districts d on d.DP_DistrictID = l.DP_DistrictID
where (@DistrictId = 0 or l.DP_DistrictID = @DistrictId)
  and (@FacilityId = 0 or l.location_id = @FacilityId)
  and (@ItemId = 0 or e.item_id = @ItemId)
group by l.location_id, l.location_name, d.DBStart_Name_En, m.item_name
order by d.DBStart_Name_En, l.location_name";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@DistrictId", districtId);
            cmd.Parameters.AddWithValue("@FacilityId", facilityId);
            cmd.Parameters.AddWithValue("@ItemId", itemId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new FacStockCovidDto
                {
                    facility_id = reader["facility_id"] != DBNull.Value ? Convert.ToInt32(reader["facility_id"]) : (int?)null,
                    facility_name = reader["facility_name"]?.ToString(),
                    district_name = reader["district_name"]?.ToString(),
                    item_name = reader["item_name"]?.ToString(),
                    in_stock = reader["in_stock"] != DBNull.Value ? Convert.ToDecimal(reader["in_stock"]) : 0,
                    installed = reader["installed"] != DBNull.Value ? Convert.ToDecimal(reader["installed"]) : 0,
                    working = reader["working"] != DBNull.Value ? Convert.ToDecimal(reader["working"]) : 0,
                    not_working = reader["not_working"] != DBNull.Value ? Convert.ToDecimal(reader["not_working"]) : 0
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/dispatch-detail?directorateId=5&userId=0
        [HttpGet("dispatch-detail")]
        public async Task<IActionResult> GetDispatchDetail([FromQuery] int directorateId = 5, [FromQuery] int userId = 0)
        {
            List<DispatchDetailDto> list = new List<DispatchDetailDto>();
            string query = @"
select sd.Issue_id as dispatch_id, p.po_no, convert(varchar, p.po_date, 103) as po_date,
       sp.name as supplier_name, m.item_name, sd.Supplyqty as dispatched_qty,
       convert(varchar, sd.DispatchedDate, 103) as dispatch_date, sd.ChallanNo as chalan_no,
       l.location_name
from SupplierDispatch sd
inner join purchase_order p on p.po_id = sd.po_id
inner join massuppliers sp on sp.supplier_id = p.supplier_id
inner join maslocations l on l.location_id = sd.location_id
inner join po_items pi on pi.po_id = p.po_id and pi.consignee_id = sd.location_id
inner join masitems m on m.item_id = pi.item_id
where (@DirectorateId = 0 or p.directorate_id = @DirectorateId)
  and (@UserId = 0 or l.user_id = @UserId)
order by sd.DispatchedDate desc";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@DirectorateId", directorateId);
            cmd.Parameters.AddWithValue("@UserId", userId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new DispatchDetailDto
                {
                    dispatch_id = reader["dispatch_id"] != DBNull.Value ? Convert.ToInt32(reader["dispatch_id"]) : (int?)null,
                    po_no = reader["po_no"]?.ToString(),
                    po_date = reader["po_date"]?.ToString(),
                    supplier_name = reader["supplier_name"]?.ToString(),
                    item_name = reader["item_name"]?.ToString(),
                    dispatched_qty = reader["dispatched_qty"] != DBNull.Value ? Convert.ToDecimal(reader["dispatched_qty"]) : 0,
                    dispatch_date = reader["dispatch_date"]?.ToString(),
                    chalan_no = reader["chalan_no"]?.ToString(),
                    location_name = reader["location_name"]?.ToString()
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/report-indent-po-details?financialYearId=19&directorateId=5&itemCode=E01
        [HttpGet("report-indent-po-details")]
        public async Task<IActionResult> GetReportIndentPODetails(
            [FromQuery] int financialYearId = 0,
            [FromQuery] int directorateId = 5,
            [FromQuery] string? itemCode = "")
        {
            List<ReportIndentPoDetailDto> list = new List<ReportIndentPoDetailDto>();
            string itemFilter = !string.IsNullOrEmpty(itemCode) ? " and m.item_code_as_per_tender = @ItemCode " : "";

            string query = $@"
select i.indent_id, id.indent_con_no as indent_no, convert(varchar, id.consolidated_date, 103) as indent_date,
       m.item_name, sum(i.indent_quantity) as indent_qty, p.po_no, sum(pi.quantity) as po_qty,
       dir.facility_aut_name as directorate_name, d.DBStart_Name_En as district_name
from indent_items i
inner join masitems m on m.item_id = i.item_id
inner join indent ind on ind.indent_id = i.indent_id
inner join indent_cons_items ci on ci.indent_cons_items_id = ind.indent_cons_items_id
inner join indent_consolidation id on id.indent_consolidation_id = ci.indent_consolidated_id
inner join facility_aut dir on dir.facility_aut_id = id.directorate_id
inner join maslocations l on l.location_id = ind.facility_id
left outer join Districts d on d.DP_DistrictID = l.DP_DistrictID
left outer join po_items pi on pi.indent_id = ind.indent_id and pi.item_id = m.item_id
left outer join purchase_order p on p.po_id = pi.po_id
where (@FinancialYearId = 0 or id.financial_year_id = @FinancialYearId)
  and (@DirectorateId = 0 or id.directorate_id = @DirectorateId)
  {itemFilter}
group by i.indent_id, id.indent_con_no, id.consolidated_date, m.item_name, p.po_no,
         dir.facility_aut_name, d.DBStart_Name_En
order by id.consolidated_date desc";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId);
            cmd.Parameters.AddWithValue("@DirectorateId", directorateId);
            if (!string.IsNullOrEmpty(itemCode)) cmd.Parameters.AddWithValue("@ItemCode", itemCode);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new ReportIndentPoDetailDto
                {
                    indent_id = reader["indent_id"] != DBNull.Value ? Convert.ToInt32(reader["indent_id"]) : (int?)null,
                    indent_no = reader["indent_no"]?.ToString(),
                    indent_date = reader["indent_date"]?.ToString(),
                    item_name = reader["item_name"]?.ToString(),
                    indent_qty = reader["indent_qty"] != DBNull.Value ? Convert.ToDecimal(reader["indent_qty"]) : 0,
                    po_no = reader["po_no"]?.ToString(),
                    po_qty = reader["po_qty"] != DBNull.Value ? Convert.ToDecimal(reader["po_qty"]) : 0,
                    directorate_name = reader["directorate_name"]?.ToString(),
                    district_name = reader["district_name"]?.ToString()
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/tender-live-status
        [HttpGet("tender-live-status")]
        public async Task<IActionResult> GetTenderLiveStatus()
        {
            List<TenderLiveStatusDto> list = new List<TenderLiveStatusDto>();
            string query = @"
select count(distinct t.tender_no) as nostender, ci.CStatus, ci.CSID, count(distinct m.item_id) as items
from tender_items ti
inner join tenders t on ti.tender_id = t.tender_id
inner join MasCoverStatus ci on ci.CSID = t.csid
inner join masitems m on m.item_id = ti.item_id
where ci.CSID not in (5, 6) and ti.item_id not in (select ci2.item_id from contract_items ci2)
group by ci.CStatus, ci.CSID
order by ci.CSID";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new TenderLiveStatusDto
                {
                    nostender = reader["nostender"] != DBNull.Value ? Convert.ToInt32(reader["nostender"]) : 0,
                    CStatus = reader["CStatus"]?.ToString(),
                    CSID = reader["CSID"] != DBNull.Value ? Convert.ToInt32(reader["CSID"]) : 0,
                    items = reader["items"] != DBNull.Value ? Convert.ToInt32(reader["items"]) : 0
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/tender-live-status-drilldown?csid=1
        [HttpGet("tender-live-status-drilldown")]
        public async Task<IActionResult> GetTenderLiveStatusDrilldown([FromQuery] int csid)
        {
            List<TenderLiveStatusDrilldownDto> list = new List<TenderLiveStatusDrilldownDto>();
            string query = @"
select t.tender_id, t.tender_no, convert(varchar, t.tender_date, 103) as tender_date,
       ci.CStatus, ci.CSID, m.item_name, m.item_code_as_per_tender
from tender_items ti
inner join tenders t on ti.tender_id = t.tender_id
inner join MasCoverStatus ci on ci.CSID = t.csid
inner join masitems m on m.item_id = ti.item_id
where (@Csid = 0 or ci.CSID = @Csid)
order by t.tender_no";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Csid", csid);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new TenderLiveStatusDrilldownDto
                {
                    tender_id = reader["tender_id"] != DBNull.Value ? Convert.ToInt32(reader["tender_id"]) : (int?)null,
                    tender_no = reader["tender_no"]?.ToString(),
                    tender_date = reader["tender_date"]?.ToString(),
                    CStatus = reader["CStatus"]?.ToString(),
                    CSID = reader["CSID"] != DBNull.Value ? Convert.ToInt32(reader["CSID"]) : (int?)null,
                    item_name = reader["item_name"]?.ToString(),
                    item_code_as_per_tender = reader["item_code_as_per_tender"]?.ToString()
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/tender-status?yearId=19&statusId=0
        [HttpGet("tender-status")]
        public async Task<IActionResult> GetTenderStatus([FromQuery] int yearId = 0, [FromQuery] int statusId = 0)
        {
            List<TenderStatusDto> list = new List<TenderStatusDto>();
            string query = @"
select t.tender_id, t.tender_no, convert(varchar, t.tender_date, 103) as tender_date,
       t.finalstatus, f.year, count(distinct ti.item_id) as total_items
from tenders t
inner join mas_financial_year f on f.financial_year_id = t.financial_year_id
left outer join tender_items ti on ti.tender_id = t.tender_id
where (@YearId = 0 or t.financial_year_id = @YearId)
group by t.tender_id, t.tender_no, t.tender_date, t.finalstatus, f.year
order by t.tender_date desc";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@YearId", yearId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new TenderStatusDto
                {
                    tender_id = reader["tender_id"] != DBNull.Value ? Convert.ToInt32(reader["tender_id"]) : (int?)null,
                    tender_no = reader["tender_no"]?.ToString(),
                    tender_date = reader["tender_date"]?.ToString(),
                    finalstatus = reader["finalstatus"]?.ToString(),
                    year = reader["year"]?.ToString(),
                    total_items = reader["total_items"] != DBNull.Value ? Convert.ToInt32(reader["total_items"]) : 0
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/tender-status-item-wise?itemId=E01
        [HttpGet("tender-status-item-wise")]
        public async Task<IActionResult> GetTenderStatusItemWise([FromQuery] string? itemId)
        {
            List<TenderStatusItemWiseDto> list = new List<TenderStatusItemWiseDto>();
            string filter = !string.IsNullOrEmpty(itemId) ? " where m.item_code_as_per_tender like '%' + @ItemId + '%' or m.item_name like '%' + @ItemId + '%' " : "";

            string query = $@"
select t.tender_no, convert(varchar, t.tender_date, 103) as tender_date,
       m.item_code_as_per_tender as item_code, m.item_name, sp.name as supplier_name,
       ci.basic_rate, ci.percentage, convert(varchar, ac.contract_end_date, 103) as contract_end_date
from contract_items ci
inner join award_of_contract ac on ac.award_of_contract_id = ci.award_of_contract_id
inner join massuppliers sp on sp.supplier_id = ac.supplier_id
inner join tenders t on t.tender_id = ac.tender_id
inner join masitems m on m.item_id = ci.item_id
{filter}
order by m.item_name";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            if (!string.IsNullOrEmpty(itemId)) cmd.Parameters.AddWithValue("@ItemId", itemId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new TenderStatusItemWiseDto
                {
                    tender_no = reader["tender_no"]?.ToString(),
                    tender_date = reader["tender_date"]?.ToString(),
                    item_code = reader["item_code"]?.ToString(),
                    item_name = reader["item_name"]?.ToString(),
                    supplier_name = reader["supplier_name"]?.ToString(),
                    basic_rate = reader["basic_rate"] != DBNull.Value ? Convert.ToDecimal(reader["basic_rate"]) : 0,
                    percentage = reader["percentage"] != DBNull.Value ? Convert.ToDecimal(reader["percentage"]) : 0,
                    contract_end_date = reader["contract_end_date"]?.ToString()
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/cover-a-items-reports?financialYearId=19
        [HttpGet("cover-a-items-reports")]
        public async Task<IActionResult> GetCoverAItemsReports([FromQuery] int financialYearId = 0)
        {
            List<CoverAItemsReportDto> list = new List<CoverAItemsReportDto>();
            string query = @"
select t.tender_id, t.tender_no, m.item_code_as_per_tender as item_code, m.item_name,
       sp.name as supplier_name,
       case when ca.eligibility = 'Y' then 'Eligible' when ca.eligibility = 'N' then 'Ineligible' else 'Pending' end as status
from tender_items ti
inner join tenders t on t.tender_id = ti.tender_id
inner join masitems m on m.item_id = ti.item_id
left outer join tender_cover_a ca on ca.tender_id = t.tender_id
left outer join massuppliers sp on sp.supplier_id = ca.supplier_id
where (@FinancialYearId = 0 or t.financial_year_id = @FinancialYearId)
order by t.tender_no, m.item_name";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new CoverAItemsReportDto
                {
                    tender_id = reader["tender_id"] != DBNull.Value ? Convert.ToInt32(reader["tender_id"]) : (int?)null,
                    tender_no = reader["tender_no"]?.ToString(),
                    item_code = reader["item_code"]?.ToString(),
                    item_name = reader["item_name"]?.ToString(),
                    supplier_name = reader["supplier_name"]?.ToString(),
                    status = reader["status"]?.ToString()
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/emd-refund-report
        [HttpGet("emd-refund-report")]
        public async Task<IActionResult> GetEmdRefundReport()
        {
            List<EmdRefundReportDto> list = new List<EmdRefundReportDto>();
            string query = @"
select t.tender_no, sp.name as supplier_name, isnull(ef.amount, 0) as emd_amount,
       case when ef.status = 'A' then 'Approved' when ef.status = 'R' then 'Rejected' else 'Pending' end as refund_status,
       convert(varchar, ef.action_date, 103) as refund_date
from emd_file ef
inner join tenders t on t.tender_id = ef.tender_id
inner join massuppliers sp on sp.supplier_id = ef.supplier_id
order by ef.action_date desc";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new EmdRefundReportDto
                {
                    tender_no = reader["tender_no"]?.ToString(),
                    supplier_name = reader["supplier_name"]?.ToString(),
                    emd_amount = reader["emd_amount"] != DBNull.Value ? Convert.ToDecimal(reader["emd_amount"]) : 0,
                    refund_status = reader["refund_status"]?.ToString(),
                    refund_date = reader["refund_date"]?.ToString()
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/eel-suggestion-report
        [HttpGet("eel-suggestion-report")]
        public async Task<IActionResult> GetEelSuggestionReport()
        {
            List<EelSuggestionReportDto> list = new List<EelSuggestionReportDto>();
            string query = @"
select s.specification_id, m.item_name, s.suggested_by, s.suggestion,
       convert(varchar, s.created_date, 103) as created_date
from eel_specifications s
inner join masitems m on m.item_id = s.item_id
order by s.created_date desc";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new EelSuggestionReportDto
                {
                    specification_id = reader["specification_id"] != DBNull.Value ? Convert.ToInt32(reader["specification_id"]) : (int?)null,
                    item_name = reader["item_name"]?.ToString(),
                    suggested_by = reader["suggested_by"]?.ToString(),
                    suggestion = reader["suggestion"]?.ToString(),
                    created_date = reader["created_date"]?.ToString()
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/indent-report-pocell?indentConsolidationId=123
        [HttpGet("indent-report-pocell")]
        public async Task<IActionResult> GetIndentReportPOCell([FromQuery] int indentConsolidationId)
        {
            List<IndentReportPocellDto> list = new List<IndentReportPocellDto>();
            string query = @"
select id.indent_consolidation_id, m.item_code_as_per_tender, m.item_name,
       sum(i.indent_quantity) as indent_qty, isnull(sum(pi.quantity), 0) as po_qty,
       sum(i.indent_quantity) - isnull(sum(pi.quantity), 0) as balance_qty
from indent_items i
inner join masitems m on m.item_id = i.item_id
inner join indent ind on ind.indent_id = i.indent_id
inner join indent_cons_items ci on ci.indent_cons_items_id = ind.indent_cons_items_id
inner join indent_consolidation id on id.indent_consolidation_id = ci.indent_consolidated_id
left outer join po_items pi on pi.indent_id = ind.indent_id and pi.item_id = m.item_id
where id.indent_consolidation_id = @IndentConsolidationId
group by id.indent_consolidation_id, m.item_code_as_per_tender, m.item_name
order by m.item_name";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@IndentConsolidationId", indentConsolidationId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new IndentReportPocellDto
                {
                    indent_consolidation_id = reader["indent_consolidation_id"] != DBNull.Value ? Convert.ToInt32(reader["indent_consolidation_id"]) : (int?)null,
                    item_code_as_per_tender = reader["item_code_as_per_tender"]?.ToString(),
                    item_name = reader["item_name"]?.ToString(),
                    indent_qty = reader["indent_qty"] != DBNull.Value ? Convert.ToDecimal(reader["indent_qty"]) : 0,
                    po_qty = reader["po_qty"] != DBNull.Value ? Convert.ToDecimal(reader["po_qty"]) : 0,
                    balance_qty = reader["balance_qty"] != DBNull.Value ? Convert.ToDecimal(reader["balance_qty"]) : 0
                });
            }

            return Ok(list);
        }

        // ==========================================
        // Helper Lookup Endpoints for Reports
        // ==========================================

        // GET: api/Reports/GetFinancialYear
        [HttpGet("GetFinancialYear")]
        public async Task<IActionResult> GetFinancialYear()
        {
            List<FinancialYearDto> list = new List<FinancialYearDto>();
            string query = "select financial_year_id, year from mas_financial_year where financial_year_id not in (11, 12, 13, 5) order by orderdp desc";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new FinancialYearDto
                {
                    financial_year_id = Convert.ToInt32(reader["financial_year_id"]),
                    year = reader["year"]?.ToString()
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/GetUsers?directorateId=5
        [HttpGet("GetUsers")]
        public async Task<IActionResult> GetUsers([FromQuery] int directorateId = 0)
        {
            List<UserLookupDto> list = new List<UserLookupDto>();
            string query = @"
select distinct u.user_id, u.user_name, u.designation from maslocations l
inner join users u on u.user_id = l.user_id
where (@DirectorateId = 0 or l.authority = @DirectorateId)
order by u.user_name";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@DirectorateId", directorateId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new UserLookupDto
                {
                    user_id = Convert.ToInt32(reader["user_id"]),
                    user_name = reader["user_name"]?.ToString(),
                    designation = reader["designation"]?.ToString()
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/GetItems
        [HttpGet("GetItems")]
        public async Task<IActionResult> GetItems()
        {
            List<ItemDTO> list = new List<ItemDTO>();
            string query = "select item_id, item_name from masitems where status = 'A' order by item_name";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new ItemDTO
                {
                    item_id = Convert.ToInt32(reader["item_id"]),
                    item_name = reader["item_name"]?.ToString() ?? ""
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/GetFacilities?districtId=1
        [HttpGet("GetFacilities")]
        public async Task<IActionResult> GetFacilities([FromQuery] int districtId = 0)
        {
            List<FacilityLookupDto> list = new List<FacilityLookupDto>();
            string query = "select location_id as facility_id, location_name as facility_name from maslocations where (@DistrictId = 0 or DP_DistrictID = @DistrictId) order by location_name";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@DistrictId", districtId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new FacilityLookupDto
                {
                    facility_id = Convert.ToInt32(reader["facility_id"]),
                    facility_name = reader["facility_name"]?.ToString() ?? ""
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/GetSuppliers
        [HttpGet("GetSuppliers")]
        public async Task<IActionResult> GetSuppliers()
        {
            List<SupplierLookupDto> list = new List<SupplierLookupDto>();
            string query = "select supplier_id, name as supplier_name from massuppliers where status = 'A' order by name";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new SupplierLookupDto
                {
                    supplier_id = Convert.ToInt32(reader["supplier_id"]),
                    supplier_name = reader["supplier_name"]?.ToString() ?? ""
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/GetTenders
        [HttpGet("GetTenders")]
        public async Task<IActionResult> GetTenders()
        {
            List<TenderLookupDto> list = new List<TenderLookupDto>();
            string query = "select distinct tender_id, tender_no from tenders where tender_no is not null order by tender_no";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new TenderLookupDto
                {
                    tender_id = Convert.ToInt32(reader["tender_id"]),
                    tender_no = reader["tender_no"]?.ToString() ?? ""
                });
            }

            return Ok(list);
        }

        // GET: api/Reports/GetTendersBySupplier?supplierId=123
        [HttpGet("GetTendersBySupplier")]
        public async Task<IActionResult> GetTendersBySupplier([FromQuery] int supplierId)
        {
            List<TenderLookupDto> list = new List<TenderLookupDto>();
            string query = @"
select distinct t.tender_id, t.tender_no
from contract_items c
inner join award_of_contract ac on ac.award_of_contract_id = c.award_of_contract_id
inner join massuppliers s on s.supplier_id = ac.supplier_id
inner join tenders t on t.tender_id = ac.tender_id
where (@SupplierId = 0 or ac.supplier_id = @SupplierId)
order by t.tender_no";

            using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@SupplierId", supplierId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new TenderLookupDto
                {
                    tender_id = Convert.ToInt32(reader["tender_id"]),
                    tender_no = reader["tender_no"]?.ToString() ?? ""
                });
            }

            return Ok(list);
        }
    }
}
