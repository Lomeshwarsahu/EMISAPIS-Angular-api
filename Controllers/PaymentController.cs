using EMISAPIS.DTOS;
using EMISAPIS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Diagnostics;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace EMISAPIS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly IConfiguration _config;

        public PaymentController(IConfiguration config)
        {
            _config = config;
        }

        [HttpPost("fit-payment-list")]
        public async Task<IActionResult> GetFitPaymentList(FitPaymentRequestDTO request)
        {
            string connectionString = _config.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(connectionString);

            string whereClause = "";
            string fitClause = "";
            string myDeskClause = "";

            // PO TYPE FILTER
            if (request.Potype == "NP")
                whereClause = " AND ISNULL(p.potype,'NP')='NP' ";

            if (request.Potype == "CP")
                whereClause = " AND ISNULL(p.potype,'NP')='CP' ";

            if (request.Potype == "All")
                whereClause = " ";

            // FIT / UNFIT
            if (request.FitUnfit == "FP")
                fitClause = " AND fitunfit='Fit for Payment' ";

            if (request.FitUnfit == "NFP")
                fitClause = " AND fitunfit='Not Fit For Payment' ";

            if (request.FitUnfit == "All")
                fitClause = " ";

            // MY DESK
            if (request.MyDeskFile)
                myDeskClause = $" AND ISNULL(pres.user_id,383)={request.UserId} ";
            string query = $@"

SELECT fitunfit,po_id,tender_no,po_no,Supplier,po_date,facility_aut_name,
item_code_as_per_tender,item_name,POQTY,cast(POValue as bigint) as POValue,
Supplyqty,instalationQty,receiptQTY,LastRDate1,potype,
fileno,filedt,PresentFile,presentuserid,TOUSERID,penaltypercent,
reasonid,ReasonName,issolved,dbo.GetSiteNotReady(po_id) as SiteStatus,
Rowno,todate,entDT,remarks

from (

select t.penaltypercent, p.po_id,t.tender_no,p.outward_no,p.po_no,(case when p.soissueDT is null then p.po_date else p.soissueDT end) as  po_date,p.directorate_id,dir.facility_aut_name,m.item_code_as_per_tender,m.item_name,sp.name as Supplier,pi.quantity as POQTY,pi.totalprice,Supplyqty,re.receiptQTY,
red.LastRDate ,case when  isnull(rpo.reasonid,0) =13 then red.LastRDate  else case when isnull(rpo.reasonid,0) !=0 then getdate()  else red.LastRDate end end as LastRDaterow
,ins.insqty,den.deniedqty,DenR.DenRec,case when isnull(p.potype,'NP')='NP' then 'Normal PO' else 'Covid Po' end potype
,p.fileno,p.filedt,rpo.ReasonName,rpo.issolved AS issolved,reasonid,rpo.remarks
from  purchase_order p 
 inner join massuppliers sp on sp.supplier_id=p.supplier_id
 left outer join 
 (
select sum(pi.quantity) as quantity,pi.po_id,pi.item_id,sum(pi.totalprice) as totalprice  from po_items pi 
group by pi.po_id,pi.item_id
) pi on pi.po_id=p.po_id
 inner join tenders t on t.tender_id=p.tender_id
 inner join masitems m on m.item_id=pi.item_id
 inner join facility_aut dir on dir.facility_aut_id=p.directorate_id
  left outer join 
 (  
 select po_id,isnull(sum(Supplyqty),0) as Supplyqty  from SupplierDispatch d
inner join Issue_item_details i on d.Issue_id=i.Issue_id
inner  join maslocations u on u.location_id= d.location_id
where d.status='C'  
group by po_id
 ) as sup on sup.po_id=pi.po_id 
 
 left outer join 
 (
 select isnull(sum(r.receipt_qty),0) as receiptQTY ,r.po_id
 from receipts r
where r.recieved_date is not null and r.status in ('C','Received') 
group by po_id
 )as re on re.po_id=pi.po_id 

  left outer join 
 (
 select sum(ri.received_qty) as insqty,r.po_id from receipts r
left outer join receipt_item_details ri on ri.receipt_id=r.receipt_id
where r.recieved_date is not null and r.status in ('C') 
group by r.po_id
 )as ins on ins.po_id=pi.po_id 

   left outer join 
 (
 
select sum (deniedqty) as deniedqty ,ponoid from descrepency where issolved is null 
group by ponoid
 )as Den on Den.ponoid=pi.po_id

    left outer join 
 (
 
 select sum (deniedqty) as DenRec,ponoid from descrepency where receiptid is null and issolved is null
 group by ponoid
 )as DenR on DenR.ponoid=pi.po_id 

 
  left outer join 
 (
 select max(r.recieved_date)LastRDate,r.po_id from receipts r
left outer join receipt_item_details ri on ri.receipt_id=r.receipt_id
where r.recieved_date is not null and r.status in ('C','Received') 
group by r.po_id
 )as red on red.po_id=pi.po_id 
 

 left outer join 
   (
   select Max(SORID) SRID,r.PONOID as   rpo_id
   from SOORDERREASON r 
   inner join ReasonMaster rm on rm.reasonid=r.reasonid 
   where  r.reasonid not in (1) 
   group by r.PONOID
   ) rpo1 on rpo1.rpo_id=p.po_id

   left outer join 
   (
    select  r.SORID,r.reasonid,rm.ReasonName,remarks, case when r.reasonid=13 then 'Y' else 'N' end as issolved
   from SOORDERREASON r 
   inner join ReasonMaster rm on rm.reasonid=r.reasonid 
   where  r.reasonid not in (1) 
   ) rpo  on rpo.SORID=rpo1.SRID

   
 where 1=1 --and p.po_id in (2393) 
and  p.status in ('Completed','Order Placed','Partially Received') and p.Ispayment is null and Supplyqty>0 and LastRDate is not null  -- whereclause 
 ) a

  left outer join 
 (
 select u.user_name,m.PONOID,u.user_id,m.TOUSERID,convert(varchar, m.todate,103) as todate,convert(varchar, m.ENTRYDT, 100) as entDT from users u 
inner join masfilemovement m on m.TOUSERID=u.user_id and m.PRESENTFILEFLAG='Y'
 ) pres on pres.PONOID=a.po_id 
where 1=1 -- whFileMyDesk 
 group by entDT,todate, outward_no,pres.user_name,pres.user_id,potype,po_no,po_date,facility_aut_name,item_code_as_per_tender,item_name,LastRDate,LastRDaterow,Supplier,po_id,tender_no,fileno,filedt,pres.TOUSERID,penaltypercent,ReasonName,issolved ,a.reasonid,a.REMARKS
 )b

WHERE 1=1
{fitClause}

and po_id in 
(
select distinct po_id 
from receipts r 
where SiteNotFlag='Y'
)

and po_id not in 
(
select distinct p.po_id  
FROM receipts r  
left outer join receipt_item_details ri 
on ri.receipt_id = r.receipt_id
inner join purchase_order p on p.po_id=r.po_id 
inner join maslocations m on m.location_id=r.location_id  
inner join BLPINVOICES b on b.RECEIPTID=r.receipt_id 
and b.location_id=r.location_id 
and b.po_id=r.po_id
inner join BLPSANCTIONS s on s.SANCTIONID=b.SANCTIONIDP
where r.po_id=2170 
and r.status in ('C','Received')  
and r.SiteNotFlag='Y' 
and s.STATUS in ('P'))ORDER BY Rowno";





            SqlCommand cmd = new SqlCommand(query, con);

            await con.OpenAsync();

            SqlDataReader reader = await cmd.ExecuteReaderAsync();

            List<FitPaymentDTO> list = new List<FitPaymentDTO>();

            while (await reader.ReadAsync())
            {
                list.Add(new FitPaymentDTO
                {
                    PoId = Convert.ToInt32(reader["po_id"]),
                    TenderNo = reader["tender_no"].ToString(),
                    PoNo = reader["po_no"].ToString(),
                    Supplier = reader["Supplier"].ToString(),
                    PoDate = reader["po_date"].ToString(),
                    ItemName = reader["item_name"].ToString(),
                    POQty = Convert.ToInt32(reader["POQTY"]),
                    SupplyQty = Convert.ToInt32(reader["Supplyqty"]),
                    ReceiptQty = Convert.ToInt32(reader["receiptQTY"]),
                    PresentFile = reader["PresentFile"]?.ToString()
                });
            }

            return Ok(list);
        }

        [HttpGet("GetFitPaymentList")]
        public async Task<IActionResult> GetFitPaymentListt([FromQuery] FitPaymentRequestDTO request)
        {
            string connectionString = _config.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(connectionString);
            string whereclause = "";
            string whFileMyDesk = "";
            string ftunft = "";

            if (request.Potype == "NP")
                whereclause += " and  isnull(p.potype,'NP')='NP' ";

            if (request.Potype == "CP")
                whereclause += " and  isnull(p.potype,'NP')='CP' ";
            if (request.Potype == "SP")
                whereclause += " and  isnull(p.potype,'NP')='SP' ";

            if (request.Potype == "All")
                whereclause += "";

            if (request.MyDeskFile)
                whFileMyDesk = " and isnull(pres.user_id,383) =" + request.UserId;

            if (request.FitUnfit == "FP")
                ftunft += " and fitunfit = 'Fit For Payment'";

            else if (request.FitUnfit == "NFP")
                ftunft += " and fitunfit = 'Not Fit For Payment'";

            else
                ftunft += "";

            string query = "";

            if (request.Potype == "SP")
            {
                query = @"select fitunfit,po_id,tender_no, po_no ,Supplier, po_date,facility_aut_name,item_code_as_per_tender,item_name , POQTY,cast(POValue as bigint) as POValue, Supplyqty, instalationQty, receiptQTY, LastRDate1,potype ,fileno, filedt, PresentFile, presentuserid, TOUSERID, penaltypercent, reasonid ,ReasonName,issolved ,dbo.GetSiteNotReady(po_id) as SiteStatus , Rowno , todate , entDT, remarks,Extstatus from ( select case when (SUM(POQTY)=(isnull(SUM(insqty),0)+isnull(sum(deniedqty),0)) and SUM(POQTY)=(isnull(SUM(receiptQTY),0)+isnull(SUM(DenRec),0)) and (reasonid=11 or reasonid=13 or reasonid is null) ) then 'Fit for Payment' else case when (SUM(POQTY)=isnull(SUM(receiptQTY),0) and (reasonid=11 OR reasonid=15 ) ) then 'Fit for Payment' else case when (SUM(POQTY)! =isnull(SUM(receiptQTY),0) and (reasonid=16 ) ) then 'Fit for Payment' else 'Not Fit For Payment' end end end as fitunfit, dbo.GetExtStatus(po_id) as Extstatus, a.po_id,tender_no,outward_no+'/'+po_no as po_no ,Supplier, CONVERT(varchar,po_date,103)as po_date,facility_aut_name,item_code_as_per_tender,item_name ,SUM(POQTY) as POQTY,sum(totalprice) POValue,SUM(Supplyqty)as Supplyqty, isnull(SUM(insqty),0) as instalationQty,isnull(SUM(receiptQTY),0) receiptQTY,CONVERT(varchar,LastRDate,103) as LastRDate1,potype ,fileno,CONVERT(varchar,filedt,103) as filedt,isnull(pres.user_name,'TPO(V)') PresentFile,isnull(pres.user_id,383) as presentuserid,isnull(pres.TOUSERID,383) as TOUSERID,isnull(penaltypercent,0) penaltypercent, reasonid ,ReasonName,issolved ,dbo.GetSiteNotReady(po_id) as SiteStatus ,ROW_NUMBER() OVER(ORDER BY LastRDaterow ) AS Rowno ,isnull(todate,'NA') as todate ,isnull(entDT,'NA') as entDT, remarks from ( select t.penaltypercent, p.po_id,t.tender_no,p.outward_no,p.po_no,(case when p.soissueDT is null then p.po_date else p.soissueDT end) as po_date,p.directorate_id,dir.facility_aut_name,m.item_code_as_per_tender,m.item_name,sp.name as Supplier,pi.quantity as POQTY,pi.totalprice,Supplyqty,re.receiptQTY, red.LastRDate ,case when isnull(rpo.reasonid,0) =13 then red.LastRDate else case when isnull(rpo.reasonid,0) !=0 then getdate() else red.LastRDate end end as LastRDaterow ,ins.insqty,den.deniedqty,DenR.DenRec,case when isnull(p.potype,'NP')='NP' then 'Normal PO' else 'Covid Po' end potype ,p.fileno,p.filedt,rpo.ReasonName,rpo.issolved AS issolved,reasonid,rpo.remarks from purchase_order p inner join massuppliers sp on sp.supplier_id=p.supplier_id left outer join ( select sum(pi.quantity) as quantity,pi.po_id,pi.item_id,sum(pi.totalprice) as totalprice from po_items pi group by pi.po_id,pi.item_id ) pi on pi.po_id=p.po_id inner join tenders t on t.tender_id=p.tender_id inner join masitems m on m.item_id=pi.item_id inner join facility_aut dir on dir.facility_aut_id=p.directorate_id left outer join ( select po_id,isnull(sum(Supplyqty),0) as Supplyqty from SupplierDispatch d inner join Issue_item_details i on d.Issue_id=i.Issue_id inner join maslocations u on u.location_id= d.location_id where d.status='C' group by po_id ) as sup on sup.po_id=pi.po_id left outer join ( select isnull(sum(r.receipt_qty),0) as receiptQTY ,r.po_id from receipts r where r.recieved_date is not null and r.status in ('C','Received') group by po_id )as re on re.po_id=pi.po_id left outer join ( select sum(ri.received_qty) as insqty,r.po_id from receipts r left outer join receipt_item_details ri on ri.receipt_id=r.receipt_id where r.recieved_date is not null and r.status in ('C') group by r.po_id )as ins on ins.po_id=pi.po_id left outer join ( select sum (deniedqty) as deniedqty ,ponoid from descrepency where issolved is null group by ponoid )as Den on Den.ponoid=pi.po_id left outer join ( select sum (deniedqty) as DenRec,ponoid from descrepency where receiptid is null and issolved is null group by ponoid )as DenR on DenR.ponoid=pi.po_id left outer join ( select max(r.recieved_date)LastRDate,r.po_id from receipts r left outer join receipt_item_details ri on ri.receipt_id=r.receipt_id where r.recieved_date is not null and r.status in ('C','Received') group by r.po_id )as red on red.po_id=pi.po_id left outer join ( select Max(SORID) SRID,r.PONOID as rpo_id from SOORDERREASON r inner join ReasonMaster rm on rm.reasonid=r.reasonid where r.reasonid not in (1) group by r.PONOID ) rpo1 on rpo1.rpo_id=p.po_id left outer join ( select r.SORID,r.reasonid,rm.ReasonName,remarks, case when r.reasonid=13 then 'Y' else 'N' end as issolved from SOORDERREASON r inner join ReasonMaster rm on rm.reasonid=r.reasonid where r.reasonid not in (1) ) rpo on rpo.SORID=rpo1.SRID where 1=1 --and p.po_id in (2393) and p.status in ('Completed','Order Placed','Partially Received') and p.Ispayment is null and Supplyqty>0 and LastRDate is not null
        " + whereclause + @"
        ) a
        left outer join 
        (
        select u.user_name,m.PONOID,u.user_id,m.TOUSERID,
        convert(varchar, m.todate,103) as todate,
        convert(varchar, m.ENTRYDT, 100) as entDT
        from users u 
        inner join masfilemovement m on m.TOUSERID=u.user_id and m.PRESENTFILEFLAG='Y'
        ) pres on pres.PONOID=a.po_id   
        where 1=1 " + whFileMyDesk + @"
        group by entDT,todate, outward_no,pres.user_name,pres.user_id,potype,po_no,
        po_date,facility_aut_name,item_code_as_per_tender,item_name,LastRDate,
        LastRDaterow,Supplier,po_id,tender_no,fileno,filedt,pres.TOUSERID,
        penaltypercent,ReasonName,issolved ,a.reasonid,a.REMARKS
        )b
        where 1=1 
        " + ftunft + @"
        and po_id in (select distinct po_id from receipts r where SiteNotFlag='Y')
        order by ROW_NUMBER() OVER(ORDER BY Rowno )";
            }

            else
            {
                query = @"select fitunfit,po_id,tender_no, po_no ,Supplier, po_date,facility_aut_name,item_code_as_per_tender,item_name , POQTY,cast(POValue as bigint) as POValue, Supplyqty, instalationQty, receiptQTY, LastRDate1,potype ,fileno, filedt, PresentFile, presentuserid, TOUSERID, penaltypercent, reasonid ,ReasonName,issolved ,dbo.GetSiteNotReady(po_id) as SiteStatus , Rowno , todate , entDT, remarks,Extstatus from ( select case when (SUM(POQTY)=(isnull(SUM(insqty),0)+isnull(sum(deniedqty),0)) and SUM(POQTY)=(isnull(SUM(receiptQTY),0)+isnull(SUM(DenRec),0)) and (reasonid=11 or reasonid=13 or reasonid is null) ) then 'Fit for Payment' else case when (SUM(POQTY)=isnull(SUM(receiptQTY),0) and (reasonid=11 ) ) then 'Fit for Payment' else case when (SUM(POQTY)=isnull(SUM(receiptQTY),0) and (isLC= 'Y') ) then 'Fit for Payment' else case when (SUM(POQTY)! =isnull(SUM(receiptQTY),0) and (reasonid=16 ) ) then 'Fit for Payment' else 'Not Fit For Payment' end end end end as fitunfit, dbo.GetExtStatus(po_id) as Extstatus, a.po_id,tender_no,outward_no+'/'+po_no as po_no ,Supplier, CONVERT(varchar,po_date,103)as po_date,facility_aut_name,item_code_as_per_tender,item_name ,SUM(POQTY) as POQTY,sum(totalprice) POValue,SUM(Supplyqty)as Supplyqty, isnull(SUM(insqty),0) as instalationQty,isnull(SUM(receiptQTY),0) receiptQTY,CONVERT(varchar,LastRDate,103) as LastRDate1,potype ,fileno,CONVERT(varchar,filedt,103) as filedt,isnull(pres.user_name,'TPO(V)') PresentFile,isnull(pres.user_id,383) as presentuserid,isnull(pres.TOUSERID,383) as TOUSERID,isnull(penaltypercent,0) penaltypercent, reasonid ,ReasonName,issolved ,dbo.GetSiteNotReady(po_id) as SiteStatus ,ROW_NUMBER() OVER(ORDER BY LastRDaterow ) AS Rowno ,isnull(todate,'NA') as todate ,isnull(entDT,'NA') as entDT, remarks from ( select t.penaltypercent, p.po_id,t.tender_no,p.outward_no,p.po_no,p.isLC,(case when p.soissueDT is null then p.po_date else p.soissueDT end) as po_date,p.directorate_id,dir.facility_aut_name,m.item_code_as_per_tender,m.item_name,sp.name as Supplier,pi.quantity as POQTY,pi.totalprice,Supplyqty,re.receiptQTY, red.LastRDate ,case when isnull(rpo.reasonid,0) =13 then red.LastRDate else case when isnull(rpo.reasonid,0) !=0 then getdate() else red.LastRDate end end as LastRDaterow ,ins.insqty,den.deniedqty,DenR.DenRec,case when isnull(p.potype,'NP')='NP' then 'Normal PO' else 'Covid Po' end potype ,p.fileno,p.filedt,rpo.ReasonName,rpo.issolved AS issolved,reasonid,rpo.remarks from purchase_order p inner join massuppliers sp on sp.supplier_id=p.supplier_id left outer join ( select sum(pi.quantity) as quantity,pi.po_id,pi.item_id,sum(pi.totalprice) as totalprice from po_items pi group by pi.po_id,pi.item_id ) pi on pi.po_id=p.po_id inner join tenders t on t.tender_id=p.tender_id inner join masitems m on m.item_id=pi.item_id inner join facility_aut dir on dir.facility_aut_id=p.directorate_id left outer join ( select po_id,isnull(sum(Supplyqty),0) as Supplyqty from SupplierDispatch d inner join Issue_item_details i on d.Issue_id=i.Issue_id inner join maslocations u on u.location_id= d.location_id where d.status='C' group by po_id ) as sup on sup.po_id=pi.po_id left outer join ( select isnull(sum(r.receipt_qty),0) as receiptQTY ,r.po_id from receipts r where r.recieved_date is not null and r.status in ('C','Received') group by po_id )as re on re.po_id=pi.po_id left outer join ( select sum(ri.received_qty) as insqty,r.po_id from receipts r left outer join receipt_item_details ri on ri.receipt_id=r.receipt_id where r.recieved_date is not null and r.status in ('C') group by r.po_id )as ins on ins.po_id=pi.po_id left outer join ( select sum (deniedqty) as deniedqty ,ponoid from descrepency where issolved is null group by ponoid )as Den on Den.ponoid=pi.po_id left outer join ( select sum (deniedqty) as DenRec,ponoid from descrepency where receiptid is null and issolved is null group by ponoid )as DenR on DenR.ponoid=pi.po_id left outer join ( select max(r.recieved_date)LastRDate,r.po_id from receipts r left outer join receipt_item_details ri on ri.receipt_id=r.receipt_id where r.recieved_date is not null and r.status in ('C','Received') group by r.po_id )as red on red.po_id=pi.po_id left outer join ( select Max(SORID) SRID,r.PONOID as rpo_id from SOORDERREASON r inner join ReasonMaster rm on rm.reasonid=r.reasonid where r.reasonid not in (1) group by r.PONOID ) rpo1 on rpo1.rpo_id=p.po_id left outer join ( select r.SORID,r.reasonid,rm.ReasonName,remarks, case when r.reasonid=13 then 'Y' else 'N' end as issolved from SOORDERREASON r inner join ReasonMaster rm on rm.reasonid=r.reasonid where r.reasonid not in (1) ) rpo on rpo.SORID=rpo1.SRID where 1=1 --and p.po_id in (3062) and p.status in ('Completed','Order Placed','Partially Received') and p.Ispayment is null and Supplyqty>0 and LastRDate is not null " + whereclause + @"
        ) a
        left outer join 
        (
        select u.user_name,m.PONOID,u.user_id,m.TOUSERID,
        convert(varchar, m.todate,103) as todate,
        convert(varchar, m.ENTRYDT, 100) as entDT
        from users u 
        inner join masfilemovement m on m.TOUSERID=u.user_id and m.PRESENTFILEFLAG='Y'
        ) pres on pres.PONOID=a.po_id 
        where 1=1 " + whFileMyDesk + @"
        group by entDT,todate, outward_no,pres.user_name,pres.user_id,potype,
        po_no,isLC,po_date,facility_aut_name,item_code_as_per_tender,item_name,
        LastRDate,LastRDaterow,Supplier,po_id,tender_no,fileno,filedt,
        pres.TOUSERID,penaltypercent,ReasonName,issolved ,a.reasonid,a.REMARKS
        )b
        where 1=1 
        " + ftunft + @"
        order by ROW_NUMBER() OVER(ORDER BY Rowno )";
            }
            Console.WriteLine(query);
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.CommandTimeout = 300; // 5 minute
            await con.OpenAsync();

            SqlDataReader reader = await cmd.ExecuteReaderAsync();

            List<FitPaymentDTO> list = new List<FitPaymentDTO>();

            //while (await reader.ReadAsync())
            //{
            //    list.Add(new FitPaymentDTO
            //    {
            //        //PoId = Convert.ToInt32(reader["po_id"]),
            //        PoId = reader["po_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["po_id"]),
            //        TenderNo = reader["tender_no"]?.ToString(),
            //        PoNo = reader["po_no"]?.ToString(),
            //        Supplier = reader["Supplier"]?.ToString(),
            //        PoDate = reader["po_date"]?.ToString(),
            //        ItemName = reader["item_name"]?.ToString(),

            //        POQty = reader["POQTY"] == DBNull.Value ? 0 : Convert.ToInt32(reader["POQTY"]),
            //        SupplyQty = reader["Supplyqty"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Supplyqty"]),
            //        ReceiptQty = reader["receiptQTY"] == DBNull.Value ? 0 : Convert.ToInt32(reader["receiptQTY"]),

            //        PresentFile = reader["PresentFile"]?.ToString()
            //    });
            //}
            while (await reader.ReadAsync())
            {
                list.Add(new FitPaymentDTO
                {
                    FitUnfit = reader["fitunfit"]?.ToString(),

                    PoId = reader["po_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["po_id"]),
                    TenderNo = reader["tender_no"]?.ToString(),
                    PoNo = reader["po_no"]?.ToString(),
                    Supplier = reader["Supplier"]?.ToString(),
                    PoDate = reader["po_date"]?.ToString(),

                    FacilityAutName = reader["facility_aut_name"]?.ToString(),
                    ItemCode = reader["item_code_as_per_tender"]?.ToString(),
                    ItemName = reader["item_name"]?.ToString(),

                    POQty = reader["POQTY"] == DBNull.Value ? 0 : Convert.ToInt32(reader["POQTY"]),
                    POValue = reader["POValue"] == DBNull.Value ? 0 : Convert.ToInt64(reader["POValue"]),

                    SupplyQty = reader["Supplyqty"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Supplyqty"]),
                    InstallationQty = reader["instalationQty"] == DBNull.Value ? 0 : Convert.ToInt32(reader["instalationQty"]),
                    ReceiptQty = reader["receiptQTY"] == DBNull.Value ? 0 : Convert.ToInt32(reader["receiptQTY"]),

                    LastRDate = reader["LastRDate1"]?.ToString(),
                    PoType = reader["potype"]?.ToString(),

                    FileNo = reader["fileno"]?.ToString(),
                    FileDt = reader["filedt"]?.ToString(),

                    PresentFile = reader["PresentFile"]?.ToString(),
                    PresentUserId = reader["presentuserid"] == DBNull.Value ? 0 : Convert.ToInt32(reader["presentuserid"]),
                    ToUserId = reader["TOUSERID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TOUSERID"]),

                    PenaltyPercent = reader["penaltypercent"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["penaltypercent"]),

                    ReasonId = reader["reasonid"] == DBNull.Value ? 0 : Convert.ToInt32(reader["reasonid"]),
                    ReasonName = reader["ReasonName"]?.ToString(),
                    IsSolved = reader["issolved"]?.ToString(),

                    SiteStatus = reader["SiteStatus"]?.ToString(),

                    RowNo = reader["Rowno"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Rowno"]),

                    ToDate = reader["todate"]?.ToString(),
                    EntDT = reader["entDT"]?.ToString(),

                    Remarks = reader["remarks"]?.ToString(),

                    ExtStatus = reader["Extstatus"]?.ToString()
                });
            }

            return Ok(list);
        }
 



        [HttpGet("GetHeaderPO")]
        public async Task<IActionResult> GetHeaderPO(int poId)
        {
            List<PoHeaderDTO> list = new List<PoHeaderDTO>();
            //string connectionString = _config.GetConnectionString("DefaultConnection");

            //using SqlConnection con = new SqlConnection(connectionString);
            string query = @"
select A.PO_ID, case when a.soissueDT is not null then convert(varchar,a.soissueDT,103) else convert(varchar,a.po_date,103) end as po_date,
convert(varchar,a.po_date,103) as YEAR_ONLY, a.TENDER_ID, a.PO_NO, a.SUPPLIER_ID,
c.TENDER_NO, convert(varchar,c.TENDER_DATE, 103) as TENDER_DATE ,a.STATUS, a.REMARKS,R.item_name as ITEM_NAME,R.ITEM_CODE_AS_PER_TENDER as CODE,
a.FINANCIAL_YEAR_ID,E.YEAR,a.APPROVED_BY,a.OUTWARD_NO					
,pi.POQ,sup.Supplyqty as Dispatched,re.receiptQTY ,ins.insqty
,ci.percentage,ci.basic_rate,pi.contract_item_id,c.warranty_year
,ci.make,ci.model,pt.tranche_days,nosco
from PURCHASE_ORDER a

left outer join
(
select sum(quantity) POQ,pi.item_id,pi.contract_item_id,pi.po_id,count(distinct consignee_id) nosco from  po_items pi 
group by pi.item_id,pi.contract_item_id,pi.po_id 
) pi on pi.po_id =a.po_id

left outer join 
(  
select po_id,isnull(sum(Supplyqty),0) as Supplyqty  from SupplierDispatch d
inner join Issue_item_details i on d.Issue_id=i.Issue_id
inner  join maslocations u on u.location_id= d.location_id
where d.status='C'  
group by po_id
) as sup on sup.po_id=pi.po_id 
 
left outer join 
(
select isnull(sum(r.receipt_qty),0) as receiptQTY ,r.po_id 
from receipts r
where r.recieved_date is not null and r.status in ('C','Received') 
group by po_id
)as re on re.po_id=pi.po_id 

left outer join 
(
select sum(ri.received_qty) as insqty,r.po_id from receipts r
left outer join receipt_item_details ri on ri.receipt_id=r.receipt_id
where r.recieved_date is not null and r.status in ('C') 
group by r.po_id
)as ins on ins.po_id=pi.po_id 

left outer join po_tranche pt on pt.po_id=a.po_id and  pt.po_id=pi.po_id
left outer join contract_items ci on ci.contract_item_id=pi.contract_item_id and ci.item_id=pi.item_id
left outer join MASITEMS R on (R.ITEM_ID =pi.ITEM_ID)
left outer join TENDERS c on (c.TENDER_ID = a.TENDER_ID)		
LEFT OUTER JOIN MAS_FINANCIAL_YEAR E ON (E.FINANCIAL_YEAR_ID=a.FINANCIAL_YEAR_ID)	
where a.po_id=@poId";

            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@poId", poId);

                await conn.OpenAsync();

                SqlDataReader reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    PoHeaderDTO data = new PoHeaderDTO
                    {
                        PoId = reader["PO_ID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["PO_ID"]),
                        PoDate = reader["po_date"]?.ToString(),
                        YearOnly = reader["YEAR_ONLY"]?.ToString(),

                        TenderId = reader["TENDER_ID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TENDER_ID"]),
                        PoNo = reader["PO_NO"]?.ToString(),
                        SupplierId = reader["SUPPLIER_ID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SUPPLIER_ID"]),

                        TenderNo = reader["TENDER_NO"]?.ToString(),
                        TenderDate = reader["TENDER_DATE"]?.ToString(),

                        Status = reader["STATUS"]?.ToString(),
                        Remarks = reader["REMARKS"]?.ToString(),

                        ItemName = reader["ITEM_NAME"]?.ToString(),
                        itemcode = reader["CODE"]?.ToString(),

                        FinancialYearId = reader["FINANCIAL_YEAR_ID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["FINANCIAL_YEAR_ID"]),
                        Year = reader["YEAR"]?.ToString(),

                        ApprovedBy = reader["APPROVED_BY"]?.ToString(),
                        OutwardNo = reader["OUTWARD_NO"]?.ToString(),

                        Poq = reader["POQ"] == DBNull.Value ? 0 : Convert.ToInt32(reader["POQ"]),
                        Dispatched = reader["Dispatched"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Dispatched"]),
                        ReceiptQty = reader["receiptQTY"] == DBNull.Value ? 0 : Convert.ToInt32(reader["receiptQTY"]),
                        InsQty = reader["insqty"] == DBNull.Value ? 0 : Convert.ToInt32(reader["insqty"]),

                        Percentage = reader["percentage"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["percentage"]),
                        BasicRate = reader["basic_rate"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["basic_rate"]),

                        ContractItemId = reader["contract_item_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["contract_item_id"]),

                        WarrantyYear = reader["warranty_year"] == DBNull.Value ? 0 : Convert.ToInt32(reader["warranty_year"]),

                        Make = reader["make"]?.ToString(),
                        Model = reader["model"]?.ToString(),

                        TrancheDays = reader["tranche_days"] == DBNull.Value ? 0 : Convert.ToInt32(reader["tranche_days"]),

                        Nosco = reader["nosco"] == DBNull.Value ? 0 : Convert.ToInt32(reader["nosco"])
                    };

                    list.Add(data);
                }
            }

            return Ok(list);
        }
        [HttpGet("GetCRIDetail")]
        public async Task<IActionResult> GetCRIDetail(int poId)
        {
            List<ReceiptItemDetailsDTO> list = new List<ReceiptItemDetailsDTO>();
            //string connectionString = _config.GetConnectionString("DefaultConnection");

            //using SqlConnection con = new SqlConnection(connectionString);

            string query = @"SELECT 0 SlNo, case when r.BulkInst='Y' then r.receipt_id else   item_detail_id end as item_detail_id,case when r.BulkInst='Y' then 'Bulk/Combined File Upload for the Consingee' else 'Serial No of Document Uploaded' end as UType
      ,model_no
      ,make_no
, convert(varchar,r.recieved_date,103) as recieved_date 
      ,convert(varchar,installation_date,103) as installation_date 
      ,convert(varchar,warenty_from,103) as warenty_from
      ,convert(varchar,warenty_to,103) as warenty_to 
      ,receipt_item_id
      ,ri.status
      ,equpitment_code
      ,make
      ,installation_location
      ,training_satisfactorily
      ,cgmsc_log_printed
      ,manual_provided
      ,opening_manual_provided
      ,calibration_certificate_prov
      ,org_warranty_card_rec
      ,other_statutory
      ,warranty_validity
      ,warranty_certificate_no
      ,inticated_po_are_received
      ,ri.receipt_id
      ,issue_detail_id
      ,received_qty
      ,warranty_card_no
      ,installation_by
,ri.InstalationReportFile,ri.InstalationPhoto,ri.Challanfile,ri.WarrantyCardFile
,'.pdf' as ext
,'.pdf' as  ext1
,isnull(ISmongo,'N') ISmongo,r.po_id,m.location_name
  FROM receipt_item_details ri
  inner join receipts r on r.receipt_id=ri.receipt_id  
  inner join maslocations  m on m.location_id=r.location_id 
   where r.po_id=@poId";

          

            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@poId", poId);

                await conn.OpenAsync();

                SqlDataReader reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    ReceiptItemDetailsDTO data = new ReceiptItemDetailsDTO
                    {
                        SlNo = reader["SlNo"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SlNo"]),
                        ItemDetailId = reader["item_detail_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["item_detail_id"]),
                        UType = reader["UType"]?.ToString(),
                        ModelNo = reader["model_no"]?.ToString(),
                        MakeNo = reader["make_no"]?.ToString(),
                        RecievedDate = reader["recieved_date"]?.ToString(),
                        InstallationDate = reader["installation_date"]?.ToString(),
                        WarentyFrom = reader["warenty_from"]?.ToString(),
                        WarentyTo = reader["warenty_to"]?.ToString(),
                        ReceiptItemId = reader["receipt_item_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["receipt_item_id"]),
                        Status = reader["status"]?.ToString(),
                        EqupitmentCode = reader["equpitment_code"]?.ToString(),
                        Make = reader["make"]?.ToString(),
                        InstallationLocation = reader["installation_location"]?.ToString(),
                        TrainingSatisfactorily = reader["training_satisfactorily"]?.ToString(),
                        CgmscLogPrinted = reader["cgmsc_log_printed"]?.ToString(),
                        ManualProvided = reader["manual_provided"]?.ToString(),
                        OpeningManualProvided = reader["opening_manual_provided"]?.ToString(),
                        CalibrationCertificateProv = reader["calibration_certificate_prov"]?.ToString(),
                        OrgWarrantyCardRec = reader["org_warranty_card_rec"]?.ToString(),
                        OtherStatutory = reader["other_statutory"]?.ToString(),
                        WarrantyValidity = reader["warranty_validity"]?.ToString(),
                        WarrantyCertificateNo = reader["warranty_certificate_no"]?.ToString(),
                        InticatedPoAreReceived = reader["inticated_po_are_received"]?.ToString(),
                        ReceiptId = reader["receipt_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["receipt_id"]),
                        IssueDetailId = reader["issue_detail_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["issue_detail_id"]),
                        ReceivedQty = reader["received_qty"] == DBNull.Value ? 0 : Convert.ToInt32(reader["received_qty"]),
                        WarrantyCardNo = reader["warranty_card_no"]?.ToString(),
                        InstallationBy = reader["installation_by"]?.ToString(),
                        InstalationReportFile = reader["InstalationReportFile"]?.ToString(),
                        InstalationPhoto = reader["InstalationPhoto"]?.ToString(),
                        Challanfile = reader["Challanfile"]?.ToString(),
                        WarrantyCardFile = reader["WarrantyCardFile"]?.ToString(),
                        Ext = reader["ext"]?.ToString(),
                        Ext1 = reader["ext1"]?.ToString(),
                        ISmongo = reader["ISmongo"]?.ToString(),
                        PoId = reader["po_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["po_id"]),
                        LocationName = reader["location_name"]?.ToString()
                    };

                    list.Add(data);
                }
            }

            return Ok(list);
        }



        [HttpGet("FillDeniedDetail")]
        public async Task<IActionResult> FillDeniedDtail(int poId)
        {
            List<DescrepencyDTO> list = new List<DescrepencyDTO>();
            //string connectionString = _config.GetConnectionString("DefaultConnection");

            //using SqlConnection con = new SqlConnection(connectionString);

            string query = @" select 0 as SlNo,deniedqty,ponoid,consigneeid,l.location_name ,d.Filename as DeniedLetter,d.Filename_reccopy as 
ReceiptCopy ,ext ,ext_reccopy,d.decrepencyid from descrepency d inner join maslocations l on l.location_id=d.consigneeid where issolved is null and d.ponoid=@poId";




            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@poId", poId);

                await conn.OpenAsync();

                SqlDataReader reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    DescrepencyDTO data = new DescrepencyDTO
                    {
                        SlNo = reader["SlNo"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SlNo"]),
                        DeniedQty = reader["deniedqty"] == DBNull.Value ? 0 : Convert.ToInt32(reader["deniedqty"]),
                        PoNoId = reader["ponoid"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ponoid"]),
                        ConsigneeId = reader["consigneeid"] == DBNull.Value ? 0 : Convert.ToInt32(reader["consigneeid"]),
                        LocationName = reader["location_name"]?.ToString(),
                        DeniedLetter = reader["DeniedLetter"]?.ToString(),
                        ReceiptCopy = reader["ReceiptCopy"]?.ToString(),
                        Ext = reader["ext"]?.ToString(),
                        ExtRecCopy = reader["ext_reccopy"]?.ToString(),
                        DecrepencyId = reader["decrepencyid"] == DBNull.Value ? 0 : Convert.ToInt32(reader["decrepencyid"])
                    };

                    list.Add(data);
                }
            }

            return Ok(list);
        }

        [HttpGet("sendto/{distId}")]
        public async Task<IActionResult> GetSendTo(string sb, int distId)
        {
            List<UserDTO> list = new List<UserDTO>();
            string where = "";

            //if (distId == 21)
            //    where = "and user_id in (5)";
            //else if (distId == 383)
            //    where = "and user_id in (21)";
            //else if (distId == 5)
            //    where = "and user_id in (29)";
            //else if (distId == 29)
            //    where = "and user_id in (5)";
            if (sb == "S")
            {
                if (distId == 21)
                    where = "and user_id in (5)";

                if (distId == 383)
                    where = "and user_id in (21)";

                if (distId == 5)
                    where = "and user_id in (29)";

                if (distId == 29)
                    where = "and user_id in (5)";
            }
            else
            {
                if (distId == 21)
                    where = "and user_id in (383)";

                if (distId == 383)
                    where = "and user_id in (383)";

                if (distId == 5)
                    where = "and user_id in (21)";

                if (distId == 29)
                    where = "and user_id in (5)";
            }

            string connStr = _config.GetConnectionString("DefaultConnection");

            using (SqlConnection con = new SqlConnection(connStr))
            {
                //string query = $"select user_id,user_name from users where 1=1 {where}";
                string query = $"select user_id,user_name,hodno depmobile,emailid depemail from users where 1 = 1 {where}";
              
                SqlCommand cmd = new SqlCommand(query, con);

                await con.OpenAsync();
                SqlDataReader dr = await cmd.ExecuteReaderAsync();

                while (await dr.ReadAsync())
                {
                    list.Add(new UserDTO
                    {
                        user_id = Convert.ToInt32(dr["user_id"]),
                        user_name = dr["user_name"].ToString()
                    });
                }
            }

            return Ok(list);
        }


        [HttpPost("forward")]
        public async Task<IActionResult> ForwardFile(FileMovementRequestDTO req)
        {
            if (req.ToUserId == 0)
                return BadRequest("Select Send To Officer");

            if (string.IsNullOrEmpty(req.Remarks))
                return BadRequest("Remarks Required");

            string connStr = _config.GetConnectionString("DefaultConnection");

            using (SqlConnection con = new SqlConnection(connStr))
            {
                await con.OpenAsync();

                // check existing movement
                string checkQuery = @"SELECT TOP 1 * 
                              FROM MASFILEMOVEMENT 
                              WHERE ponoid=@ponoid 
                              AND presentfileflag='Y'";

                SqlCommand checkCmd = new SqlCommand(checkQuery, con);
                checkCmd.Parameters.AddWithValue("@ponoid", req.PonoId);

                var reader = await checkCmd.ExecuteReaderAsync();
                bool exists = reader.HasRows;
                reader.Close();

                // update old movement
                if (exists)
                {
                    string updateQuery = @"UPDATE MASFILEMOVEMENT 
                                   SET presentfileflag='N' 
                                   WHERE ponoid=@ponoid";

                    SqlCommand updateCmd = new SqlCommand(updateQuery, con);
                    updateCmd.Parameters.AddWithValue("@ponoid", req.PonoId);
                    await updateCmd.ExecuteNonQueryAsync();
                }

                // insert new movement
                string insertQuery = @"INSERT INTO MASFILEMOVEMENT
                              (userid,todate,entryDT,remarks,Flag,presentfileflag,touserid,ponoid,fileid)
                              VALUES
                              (@userid,@todate,GETDATE(),@remarks,@flag,'Y',@touserid,@ponoid,@fileid)";

                SqlCommand insertCmd = new SqlCommand(insertQuery, con);

                insertCmd.Parameters.AddWithValue("@userid", req.UserId);
                insertCmd.Parameters.AddWithValue("@todate", req.ForwardDate);
                insertCmd.Parameters.AddWithValue("@remarks", req.Remarks);
                insertCmd.Parameters.AddWithValue("@flag", req.Flag);
                insertCmd.Parameters.AddWithValue("@touserid", req.ToUserId);
                insertCmd.Parameters.AddWithValue("@ponoid", req.PonoId);
                insertCmd.Parameters.AddWithValue("@fileid", req.FileId);

                await insertCmd.ExecuteNonQueryAsync();
            }

            return Ok(new { message = "File Forwarded Successfully" });
        }
        //    DataTable dt = new DataTable();

        //    using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
        //    {
        //        using (SqlCommand cmd = new SqlCommand(query, con))
        //        {
        //            await con.OpenAsync();

        //            SqlDataAdapter da = new SqlDataAdapter(cmd);
        //            da.Fill(dt);
        //        }
        //    }

        //    return Ok(dt);
        //}





        //    [HttpGet("fit-payment-list")]
        //        public async Task<IActionResult> GettFitPaymentList([FromQuery] FitPaymentRequestDTO request)
        //        {
        //            string connectionString = _config.GetConnectionString("DefaultConnection");

        //            using SqlConnection con = new SqlConnection(connectionString);

        //            string whereClause = "";
        //            string fitClause = "";
        //            string myDeskClause = "";

        //            // PO TYPE
        //            if (request.PoType == "NP")
        //                whereClause = " AND ISNULL(p.potype,'NP')='NP' ";

        //            if (request.PoType == "CP")
        //                whereClause = " AND ISNULL(p.potype,'NP')='CP' ";

        //            if (request.PoType == "All")
        //                whereClause = " ";

        //            // FIT / UNFIT
        //            if (request.FitUnfit == "FP")
        //                fitClause = " AND fitunfit='Fit for Payment' ";

        //            if (request.FitUnfit == "NFP")
        //                fitClause = " AND fitunfit='Not Fit For Payment' ";

        //            if (request.FitUnfit == "All")
        //                fitClause = " ";

        //            // MY DESK
        //            if (request.MyDeskFile)
        //                myDeskClause = $" AND ISNULL(pres.user_id,383)={request.UserId} ";

        //            string query = @"

        //SELECT fitunfit,po_id,tender_no,po_no,Supplier,po_date,facility_aut_name,
        //item_code_as_per_tender,item_name,POQTY,cast(POValue as bigint) as POValue,
        //Supplyqty,instalationQty,receiptQTY,LastRDate1,potype,
        //fileno,filedt,PresentFile,presentuserid,TOUSERID,penaltypercent,
        //reasonid,ReasonName,issolved,dbo.GetSiteNotReady(po_id) as SiteStatus,
        //Rowno,todate,entDT,remarks

        //from (

        //select t.penaltypercent, p.po_id,t.tender_no,p.outward_no,p.po_no,(case when p.soissueDT is null then p.po_date else p.soissueDT end) as  po_date,p.directorate_id,dir.facility_aut_name,m.item_code_as_per_tender,m.item_name,sp.name as Supplier,pi.quantity as POQTY,pi.totalprice,Supplyqty,re.receiptQTY,
        //red.LastRDate ,case when  isnull(rpo.reasonid,0) =13 then red.LastRDate  else case when isnull(rpo.reasonid,0) !=0 then getdate()  else red.LastRDate end end as LastRDaterow
        //,ins.insqty,den.deniedqty,DenR.DenRec,case when isnull(p.potype,'NP')='NP' then 'Normal PO' else 'Covid Po' end potype
        //,p.fileno,p.filedt,rpo.ReasonName,rpo.issolved AS issolved,reasonid,rpo.remarks
        //from  purchase_order p 
        // inner join massuppliers sp on sp.supplier_id=p.supplier_id
        // left outer join 
        // (
        //select sum(pi.quantity) as quantity,pi.po_id,pi.item_id,sum(pi.totalprice) as totalprice  from po_items pi 
        //group by pi.po_id,pi.item_id
        //) pi on pi.po_id=p.po_id
        // inner join tenders t on t.tender_id=p.tender_id
        // inner join masitems m on m.item_id=pi.item_id
        // inner join facility_aut dir on dir.facility_aut_id=p.directorate_id
        //  left outer join 
        // (  
        // select po_id,isnull(sum(Supplyqty),0) as Supplyqty  from SupplierDispatch d
        //inner join Issue_item_details i on d.Issue_id=i.Issue_id
        //inner  join maslocations u on u.location_id= d.location_id
        //where d.status='C'  
        //group by po_id
        // ) as sup on sup.po_id=pi.po_id 

        // left outer join 
        // (
        // select isnull(sum(r.receipt_qty),0) as receiptQTY ,r.po_id
        // from receipts r
        //where r.recieved_date is not null and r.status in ('C','Received') 
        //group by po_id
        // )as re on re.po_id=pi.po_id 

        //  left outer join 
        // (
        // select sum(ri.received_qty) as insqty,r.po_id from receipts r
        //left outer join receipt_item_details ri on ri.receipt_id=r.receipt_id
        //where r.recieved_date is not null and r.status in ('C') 
        //group by r.po_id
        // )as ins on ins.po_id=pi.po_id 

        //   left outer join 
        // (

        //select sum (deniedqty) as deniedqty ,ponoid from descrepency where issolved is null 
        //group by ponoid
        // )as Den on Den.ponoid=pi.po_id

        //    left outer join 
        // (

        // select sum (deniedqty) as DenRec,ponoid from descrepency where receiptid is null and issolved is null
        // group by ponoid
        // )as DenR on DenR.ponoid=pi.po_id 


        //  left outer join 
        // (
        // select max(r.recieved_date)LastRDate,r.po_id from receipts r
        //left outer join receipt_item_details ri on ri.receipt_id=r.receipt_id
        //where r.recieved_date is not null and r.status in ('C','Received') 
        //group by r.po_id
        // )as red on red.po_id=pi.po_id 


        // left outer join 
        //   (
        //   select Max(SORID) SRID,r.PONOID as   rpo_id
        //   from SOORDERREASON r 
        //   inner join ReasonMaster rm on rm.reasonid=r.reasonid 
        //   where  r.reasonid not in (1) 
        //   group by r.PONOID
        //   ) rpo1 on rpo1.rpo_id=p.po_id

        //   left outer join 
        //   (
        //    select  r.SORID,r.reasonid,rm.ReasonName,remarks, case when r.reasonid=13 then 'Y' else 'N' end as issolved
        //   from SOORDERREASON r 
        //   inner join ReasonMaster rm on rm.reasonid=r.reasonid 
        //   where  r.reasonid not in (1) 
        //   ) rpo  on rpo.SORID=rpo1.SRID


        // where 1=1 --and p.po_id in (2393) 
        //and  p.status in ('Completed','Order Placed','Partially Received') and p.Ispayment is null and Supplyqty>0 and LastRDate is not null  -- whereclause 
        // ) a

        //  left outer join 
        // (
        // select u.user_name,m.PONOID,u.user_id,m.TOUSERID,convert(varchar, m.todate,103) as todate,convert(varchar, m.ENTRYDT, 100) as entDT from users u 
        //inner join masfilemovement m on m.TOUSERID=u.user_id and m.PRESENTFILEFLAG='Y'
        // ) pres on pres.PONOID=a.po_id 
        //where 1=1 -- whFileMyDesk 
        // group by entDT,todate, outward_no,pres.user_name,pres.user_id,potype,po_no,po_date,facility_aut_name,item_code_as_per_tender,item_name,LastRDate,LastRDaterow,Supplier,po_id,tender_no,fileno,filedt,pres.TOUSERID,penaltypercent,ReasonName,issolved ,a.reasonid,a.REMARKS
        // )b

        //WHERE 1=1
        // -- ftunft 

        //and po_id in 
        //(
        //select distinct po_id 
        //from receipts r 
        //where SiteNotFlag='Y'
        //)

        //and po_id not in 
        //(
        //select distinct p.po_id  
        //FROM receipts r  
        //left outer join receipt_item_details ri 
        //on ri.receipt_id = r.receipt_id
        //inner join purchase_order p on p.po_id=r.po_id 
        //inner join maslocations m on m.location_id=r.location_id  
        //inner join BLPINVOICES b on b.RECEIPTID=r.receipt_id 
        //and b.location_id=r.location_id 
        //and b.po_id=r.po_id
        //inner join BLPSANCTIONS s on s.SANCTIONID=b.SANCTIONIDP
        //where r.po_id=2170 
        //and r.status in ('C','Received')  
        //and r.SiteNotFlag='Y' 
        //and s.STATUS in ('P'))ORDER BY Rowno";

        //            // 🔥 IMPORTANT
        //            query = query.Replace("-- whereclause", whereClause);
        //            query = query.Replace("-- whFileMyDesk", myDeskClause);
        //            query = query.Replace("-- ftunft", fitClause);
        //            Console.WriteLine(query);

        //            SqlCommand cmd = new SqlCommand(query, con);
        //            await con.OpenAsync();

        //            SqlDataReader reader = await cmd.ExecuteReaderAsync();

        //            List<FitPaymentDTO> list = new List<FitPaymentDTO>();

        //            while (await reader.ReadAsync())
        //            {
        //                list.Add(new FitPaymentDTO
        //                {
        //                    PoId = Convert.ToInt32(reader["po_id"]),
        //                    TenderNo = reader["tender_no"].ToString(),
        //                    PoNo = reader["po_no"].ToString(),
        //                    Supplier = reader["Supplier"].ToString(),
        //                    PoDate = reader["po_date"].ToString(),
        //                    ItemName = reader["item_name"].ToString(),
        //                    POQty = Convert.ToInt32(reader["POQTY"]),
        //                    SupplyQty = Convert.ToInt32(reader["Supplyqty"]),
        //                    ReceiptQty = Convert.ToInt32(reader["receiptQTY"]),
        //                    PresentFile = reader["PresentFile"]?.ToString()
        //                });
        //            }

        //            return Ok(list);
        //        }

    }

}

