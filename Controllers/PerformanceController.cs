using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using EMISAPIS.DTOS;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace EMISAPIS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PerformanceController : ControllerBase
    {
        private readonly IConfiguration _config;

        public PerformanceController(IConfiguration config)
        {
            _config = config;
        }

        private string ConnStr => _config.GetConnectionString("DefaultConnection");

        [HttpGet("GetPerformanceGrid")]
        public IActionResult GetPerformanceGrid([FromQuery] int userId)
        {
            var list = new List<PerformanceGridDto>();
            using var conn = new SqlConnection(ConnStr);
            conn.Open();

            string whuser, joinwh, whponoid, pflag;
            if (userId == 1)
            {
                whuser = " and m.TOUSERID in (1)";
                joinwh = " inner join ";
                pflag = " and PRESENTFILEFLAG = 'Y'";
                whponoid = "";
            }
            else
            {
                joinwh = " left outer join ";
                whuser = " and m.TOUSERID in (5,29)";
                whponoid = " and pres.PONOID is null ";
                pflag = "";
            }

            string sql = @" select distinct
 p.outward_no+'/'+p.po_no as pono,
 (case when p.soissueDT is null then convert(varchar,p.po_date,103) else convert(varchar,p.soissueDT,103) end) as po_date,
 pi.quantity as POQTY,inst.instQTY
 ,convert(varchar,LastInstDT,103) as LastInstDT1,LastInstDT
 ,sp.name
 ,te.tender_no
 ,case when te.performacereq='Y' then 'Yes' else case when te.performacereq='N' then 'No' else 'NA' end end as PRequired
 ,mp.PStatus,sp.supplier_id
 ,p.Potype
 ,p.po_id
 ,te.releasetype,te.releasevalue,ms.item_code_as_per_tender,ms.item_name
 ,isnull(cpp.status,'No Online Complain') as CStatus
 ,case when rtrim(ltrim(te.releasetype))='W' then Convert(varchar,(DATEADD(WEEK, te.releasevalue,red.LastInstDT)),103)
  else case when rtrim(ltrim(te.releasetype))='M' then Convert(varchar,(DATEADD(MONTH, te.releasevalue,red.LastInstDT)),103) else 'NA' end end as LimitDT
 ,case when rec20by is not null then 'Forward' else '' end as PresentFile,p.fileno,Convert(varchar,p.filedt,103) as filedt
 ,case when rec20by is not null then 'Download' else '' end as DownloadPerf
 ,te.tender_id
 from BLPTAXS t
 inner join BLPSANCTIONS s on s.SANCTIONID=t.SANCTIONID
 inner join MASBUDGET b on b.BUDGETID=s.BUDGETID
 inner join purchase_order p on p.po_id=s.po_id
 INNER JOIN tenders te on te.tender_id = p.tender_id
 LEFT OUTER JOIN
 (
   SELECT sum(pi.quantity) AS quantity, pi.po_id, pi.item_id, sum(pi.totalprice) AS totalprice
   FROM po_items pi GROUP BY pi.po_id, pi.item_id
 ) pi ON pi.po_id = p.po_id
 LEFT OUTER JOIN
 (
   SELECT max(ri.installation_date) LastInstDT, r.po_id
   FROM receipts r inner join receipt_item_details ri ON ri.receipt_id = r.receipt_id
   WHERE r.recieved_date IS NOT NULL AND r.status IN ('C')
   GROUP BY r.po_id
 ) AS red ON red.po_id = pi.po_id
 LEFT OUTER JOIN
 (
   SELECT sum(ri.received_qty) as instQTY,r.po_id
   FROM receipts r inner join receipt_item_details ri ON ri.receipt_id = r.receipt_id
   WHERE r.recieved_date IS NOT NULL AND r.status IN ('C')
   GROUP BY r.po_id
 ) AS inst ON inst.po_id = pi.po_id
 inner join masitems ms on ms.item_id = pi.item_id
 inner join massuppliers sp on sp.supplier_id=p.supplier_id
 inner join BLPPAYMENTS py on py.paymentid=s.paymentid
 inner join MasCGMSCAccNos mcn on mcn.bankid = py.CGMSCPAIDBANKID
 left outer join MasPStatus mp on mp.PType=py.status
 left outer join
 (
   select cp.status,cp.location_id,cp.item_id,cp.item_detail_id,r.po_id from complaints cp
   inner join receipt_item_details ric on ric.item_detail_id = cp.item_detail_id
   inner join receipts r on r.receipt_id=ric.receipt_id and r.location_id=cp.location_id
 ) cpp on cpp.po_id = pi.po_id
 " + joinwh + @"
 (
   select Max(m.PONOID) as PONOID from users u
   inner join masfilemovementother m on m.TOUSERID=u.user_id " + pflag + " and Ptype=2 " + whuser + @"
   group by m.PONOID
 ) pres on pres.PONOID=p.po_id and pres.PONOID = pi.po_id
 where py.STATUS='P' and t.TAXTYPEID=250 and isnull(t.TAXVALUE,0)>0 " + whponoid + @" and isnull(ISRELEASE,'N')='N'
 order by LastInstDT ";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var limitDT = reader["LimitDT"]?.ToString() ?? "";
                var cstatus = reader["CStatus"]?.ToString() ?? "";

                bool isEligible = false;
                if (limitDT != "NA" && limitDT != "")
                {
                    if (DateTime.TryParseExact(limitDT, "dd/MM/yyyy",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out DateTime dtLimit))
                    {
                        isEligible = DateTime.Now > dtLimit;
                    }
                }
                else if (limitDT == "NA")
                {
                    isEligible = true;
                }

                string rowColor = "white";
                bool canRelease = true;

                if (cstatus == "Booked" || !isEligible)
                    rowColor = "LightPink";
                else if (cstatus != "Booked" && !isEligible)
                    rowColor = "LightBlue";
                else
                    rowColor = "LightGreen";

                if (cstatus == "Booked") canRelease = false;
                if (!isEligible) canRelease = false;

                list.Add(new PerformanceGridDto
                {
                    PoId = reader["po_id"] != DBNull.Value ? Convert.ToInt32(reader["po_id"]) : 0,
                    Pono = reader["pono"]?.ToString() ?? "",
                    PoDate = reader["po_date"]?.ToString() ?? "",
                    POQTY = reader["POQTY"] != DBNull.Value ? Convert.ToDecimal(reader["POQTY"]) : 0,
                    InstQTY = reader["instQTY"] != DBNull.Value ? Convert.ToDecimal(reader["instQTY"]) : 0,
                    LastInstDT1 = reader["LastInstDT1"]?.ToString() ?? "",
                    Name = reader["name"]?.ToString() ?? "",
                    TenderNo = reader["tender_no"]?.ToString() ?? "",
                    PRequired = reader["PRequired"]?.ToString() ?? "",
                    PStatus = reader["PStatus"]?.ToString() ?? "",
                    SupplierId = reader["supplier_id"] != DBNull.Value ? Convert.ToInt32(reader["supplier_id"]) : 0,
                    Potype = reader["Potype"]?.ToString() ?? "",
                    ReleaseType = reader["releasetype"]?.ToString() ?? "",
                    ReleaseValue = reader["releasevalue"] != DBNull.Value ? Convert.ToInt32(reader["releasevalue"]) : 0,
                    ItemCode = reader["item_code_as_per_tender"]?.ToString() ?? "",
                    ItemName = reader["item_name"]?.ToString() ?? "",
                    CStatus = cstatus,
                    LimitDT = limitDT,
                    PresentFile = reader["PresentFile"]?.ToString() ?? "",
                    FileNo = reader["fileno"]?.ToString() ?? "",
                    Filedt = reader["filedt"]?.ToString() ?? "",
                    DownloadPerf = reader["DownloadPerf"]?.ToString() ?? "",
                    TenderId = reader["tender_id"] != DBNull.Value ? Convert.ToInt32(reader["tender_id"]) : 0,
                    RowColor = rowColor,
                    CanRelease = canRelease
                });
            }
            return Ok(list);
        }

        [HttpGet("GetTenderLeavy")]
        public IActionResult GetTenderLeavy([FromQuery] int tenderId)
        {
            using var conn = new SqlConnection(ConnStr);
            conn.Open();
            string sql = @"SELECT A.TENDER_NO,
              convert(varchar(10), A.TENDER_DATE, 103) as TENDER_DATE,
              A.TENDER_ID, a.releasetype, a.performacereq, a.releasevalue,
              CONVERT(varchar(10),A.performanceentrydt,103) as performanceentrydt
              FROM TENDERS A
              WHERE A.TENDER_ID=@tid";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@tid", tenderId);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return Ok(new TenderLeavyDto
                {
                    TenderId = Convert.ToInt32(reader["TENDER_ID"]),
                    TenderNo = reader["TENDER_NO"]?.ToString() ?? "",
                    TenderDate = reader["TENDER_DATE"]?.ToString() ?? "",
                    ReleaseType = reader["releasetype"]?.ToString() ?? "M",
                    Performacereq = reader["performacereq"]?.ToString() ?? "Y",
                    ReleaseValue = reader["releasevalue"] != DBNull.Value ? Convert.ToInt32(reader["releasevalue"]) : 0,
                    PerformanceEntryDt = reader["performanceentrydt"]?.ToString() ?? ""
                });
            }
            return Ok(new TenderLeavyDto());
        }

        [HttpPost("UpdateTenderLeavy")]
        public IActionResult UpdateTenderLeavy([FromBody] UpdateTenderLeavyDto dto)
        {
            using var conn = new SqlConnection(ConnStr);
            conn.Open();

            string errMsg = "";
            if ((dto.ReleaseType == "M" || dto.ReleaseType == "W") && (!dto.ReleaseValue.HasValue || dto.ReleaseValue == 0))
                errMsg = "Please Enter Release Duration (No.of (Week/Month))";
            if ((dto.ReleaseType == "M" || dto.ReleaseType == "W") && dto.Performacereq == "NA")
                errMsg = "Performance Certificate will be Yes or No";
            if (dto.ReleaseType == "NA" && dto.ReleaseValue.HasValue && dto.ReleaseValue > 0)
                errMsg = "Release Duration should be Blank in case of Not Required";
            if (dto.ReleaseType == "NA" && dto.Performacereq == "Y")
                errMsg = "Performance Certificate will not be Required";

            if (!string.IsNullOrEmpty(errMsg))
                return BadRequest(new { message = errMsg });

            string sql = @"UPDATE TENDERS SET releasetype=@rt, performacereq=@pr, releasevalue=@rv,
              performanceentrydt=GETDATE(), PReleaseOTP=@otp WHERE TENDER_ID=@tid";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@rt", dto.ReleaseType);
            cmd.Parameters.AddWithValue("@pr", dto.Performacereq);
            cmd.Parameters.AddWithValue("@rv", (object)dto.ReleaseValue ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@otp", dto.Otp ?? "");
            cmd.Parameters.AddWithValue("@tid", dto.TenderId);
            cmd.ExecuteNonQuery();

            return Ok(new { message = "Performance Release Clause Successfully Updated In Tender" });
        }

        [HttpGet("GetPerformanceHeader")]
        public IActionResult GetPerformanceHeader([FromQuery] int poId)
        {
            using var conn = new SqlConnection(ConnStr);
            conn.Open();
            string sql = @" select A.PO_ID,
              convert(varchar,case when a.soissueDT is null then a.po_date else a.soissueDT end,103) as po_date,
              a.TENDER_ID, a.PO_NO,nosc,POQTY,isnull(dis,0) as dis,isnull(receipt_qty,0) as receipt_qty,isnull(insQTY,0) as insQTY,
              make,model,percentage,basic_rate,item_code_as_per_tender,item_name,b.item_id,
              case when te.releasetype='W' then 'Week' else 'Month' end as releasetype,te.releasevalue
              from purchase_order a
              INNER JOIN tenders te on te.tender_id = a.tender_id
              left outer join
              (
                select count(distinct consignee_id) as nosc, sum(quantity) POQTY,sum(dis) as dis,sum(receipt_qty) as receipt_qty,sum(insQTY) insQTY,
                a.po_id, a.make,a.model,a.percentage,a.basic_rate,item_code_as_per_tender,item_name,a.item_id
                from
                (
                  select pi.consignee_id,pi.quantity,d.dis,r.receipt_qty,i.insQTY,pi.po_id,ci.make,ci.model,ci.percentage,ci.basic_rate,
                  m.item_code_as_per_tender,m.item_name,m.item_id
                  from po_items pi
                  inner join contract_items ci on ci.contract_item_id=pi.contract_item_id
                  inner join maslocations l on l.location_id=pi.consignee_id
                  inner join masitems m on m.item_id=pi.item_id
                  left outer join
                  (select r.location_id,sum(r.receipt_qty) as receipt_qty,r.po_id from receipts r group by r.location_id,r.po_id) r
                    on r.location_id=pi.consignee_id and r.po_id=pi.po_id
                  left outer join
                  (select r.location_id,sum(ri.received_qty) as insQTY,r.po_id from receipts r
                   inner join receipt_item_details ri on ri.receipt_id=r.receipt_id group by r.location_id,r.po_id) i
                    on i.location_id=pi.consignee_id and i.po_id=pi.po_id
                  left outer join
                  (select sum(si.Supplyqty) dis,s.po_id,s.location_id from SupplierDispatch s
                   inner join Issue_item_details si on si.Issue_id=s.Issue_id group by s.location_id,s.po_id) d
                    on d.location_id=pi.consignee_id and d.po_id=pi.po_id
                  where pi.po_id=@poId
                ) a
                group by a.po_id,a.make,a.model,a.percentage,a.basic_rate,item_code_as_per_tender,item_name,item_id
              ) b on b.po_id=a.po_id
              where a.po_id=@poId";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@poId", poId);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return Ok(new PerformanceHeaderDto
                {
                    PoId = Convert.ToInt32(reader["PO_ID"]),
                    PoDate = reader["po_date"]?.ToString() ?? "",
                    TenderId = reader["TENDER_ID"] != DBNull.Value ? Convert.ToInt32(reader["TENDER_ID"]) : 0,
                    PoNo = reader["PO_NO"]?.ToString() ?? "",
                    NoOfConsignee = reader["nosc"] != DBNull.Value ? Convert.ToInt32(reader["nosc"]) : 0,
                    POQTY = reader["POQTY"] != DBNull.Value ? Convert.ToDecimal(reader["POQTY"]) : 0,
                    DispatchQty = reader["dis"] != DBNull.Value ? Convert.ToDecimal(reader["dis"]) : 0,
                    ReceiptQty = reader["receipt_qty"] != DBNull.Value ? Convert.ToDecimal(reader["receipt_qty"]) : 0,
                    InsQTY = reader["insQTY"] != DBNull.Value ? Convert.ToDecimal(reader["insQTY"]) : 0,
                    ItemCode = reader["item_code_as_per_tender"]?.ToString() ?? "",
                    ItemName = reader["item_name"]?.ToString() ?? "",
                    Make = reader["make"]?.ToString() ?? "",
                    Model = reader["model"]?.ToString() ?? "",
                    Percentage = reader["percentage"] != DBNull.Value ? Convert.ToDecimal(reader["percentage"]) : 0,
                    BasicRate = reader["basic_rate"] != DBNull.Value ? Convert.ToDecimal(reader["basic_rate"]) : 0,
                    ReleaseType = reader["releasetype"]?.ToString() ?? "",
                    ReleaseValue = reader["releasevalue"] != DBNull.Value ? Convert.ToInt32(reader["releasevalue"]) : 0
                });
            }
            return Ok(new PerformanceHeaderDto());
        }

        [HttpGet("GetConsigneeInstallation")]
        public IActionResult GetConsigneeInstallation([FromQuery] int poId)
        {
            var list = new List<ConsigneeInstallationDto>();
            using var conn = new SqlConnection(ConnStr);
            conn.Open();
            string sql = @" select 0 as SlNo, l.location_name, pi.consignee_id,
              sum(pi.quantity) POQTY,isnull(dis,0) as dis,isnull(receiptQTY,0) as receiptQTY,isnull(insqty,0)insqty,
              '.pdf' as ext,pi.po_id,'' as InstalationReportFile, ins.installation_date
              from po_items pi
              inner join maslocations l on l.location_id=pi.consignee_id
              left outer join
              (select sum(si.Supplyqty) dis,s.po_id,s.location_id from SupplierDispatch s
               inner join Issue_item_details si on si.Issue_id=s.Issue_id
               where s.status='C' group by s.location_id,s.po_id) d on d.location_id=pi.consignee_id and d.po_id=pi.po_id
              left outer join
              (select isnull(sum(r.receipt_qty),0) as receiptQTY,r.po_id,r.location_id
               from receipts r where r.recieved_date is not null and r.status in ('C','Received')
               group by po_id,r.location_id) as re on re.po_id=pi.po_id and re.location_id=pi.consignee_id
              left outer join
              (select sum(ri.received_qty) as insqty,r.po_id,r.location_id,convert(varchar,Max(installation_date),103) as installation_date
               from receipts r left outer join receipt_item_details ri on ri.receipt_id=r.receipt_id
               where r.recieved_date is not null and r.status in ('C')
               group by r.po_id,r.location_id) as ins on ins.po_id=pi.po_id and ins.location_id=pi.consignee_id
              where pi.po_id=@poId
              group by pi.consignee_id,pi.po_id,l.location_name,dis,receiptQTY,insqty,ins.installation_date";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@poId", poId);
            using var reader = cmd.ExecuteReader();
            int sno = 0;
            while (reader.Read())
            {
                sno++;
                list.Add(new ConsigneeInstallationDto
                {
                    SlNo = sno,
                    LocationName = reader["location_name"]?.ToString() ?? "",
                    ConsigneeId = reader["consignee_id"] != DBNull.Value ? Convert.ToInt32(reader["consignee_id"]) : 0,
                    POQTY = reader["POQTY"] != DBNull.Value ? Convert.ToDecimal(reader["POQTY"]) : 0,
                    Dis = reader["dis"] != DBNull.Value ? Convert.ToDecimal(reader["dis"]) : 0,
                    ReceiptQTY = reader["receiptQTY"] != DBNull.Value ? Convert.ToDecimal(reader["receiptQTY"]) : 0,
                    Insqty = reader["insqty"] != DBNull.Value ? Convert.ToDecimal(reader["insqty"]) : 0,
                    InstallationDate = reader["installation_date"]?.ToString() ?? ""
                });
            }
            return Ok(list);
        }

        [HttpPost("SavePerformanceRelease")]
        public IActionResult SavePerformanceRelease([FromBody] SavePerformanceReleaseDto dto)
        {
            using var conn = new SqlConnection(ConnStr);
            conn.Open();

            string sql1 = @" Update receipt_item_details set Recom20='Y',RecomDT=getdate()
              where receipt_id in (select receipt_id from receipts where po_id=@poId)";
            using (var cmd1 = new SqlCommand(sql1, conn))
            {
                cmd1.Parameters.AddWithValue("@poId", dto.PoId);
                cmd1.ExecuteNonQuery();
            }

            string sql2 = @" update purchase_order set Rec20by=@userId, Rec20DT=GETDATE() where po_id=@poId";
            using (var cmd2 = new SqlCommand(sql2, conn))
            {
                cmd2.Parameters.AddWithValue("@userId", dto.UserId);
                cmd2.Parameters.AddWithValue("@poId", dto.PoId);
                cmd2.ExecuteNonQuery();
            }

            return Ok(new { message = "Performance Release Successfully Updated for this PO, Please Take Forward Action" });
        }

        [HttpGet("GetSendToUsers")]
        public IActionResult GetSendToUsers([FromQuery] int userId, [FromQuery] string flag)
        {
            var list = new List<SendToUserDto>();
            string whereClause = "";
            if (flag == "S")
            {
                if (userId == 12 || userId == 11)
                    whereClause = " and roleid in (16,14) ";
                else if (userId == 14)
                    whereClause = " and roleid in (12,16) ";
                else if (userId == 16)
                    whereClause = " and roleid in (17,18,19) ";
                else if (userId == 17)
                    whereClause = " and user_id in (16,18,19) ";
                else if (userId == 18)
                    whereClause = " and user_id in (16,17,19) ";
                else if (userId == 19)
                    whereClause = " and user_id in (16,17,18) ";
                else
                    whereClause = " and user_id in (1) ";
            }
            else
            {
                if (userId == 29)
                    whereClause = " and user_id in (1) ";
                else if (userId == 5)
                    whereClause = " and user_id in (1) ";
                else if (userId == 1)
                    whereClause = " and user_id in (21) ";
            }

            using var conn = new SqlConnection(ConnStr);
            conn.Open();
            string sql = "select user_id, user_name from users where 1=1 " + whereClause;
            using var cmd = new SqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new SendToUserDto
                {
                    UserId = Convert.ToInt32(reader["user_id"]),
                    UserName = reader["user_name"]?.ToString() ?? ""
                });
            }
            return Ok(list);
        }

        [HttpPost("ForwardPerformanceFile")]
        public IActionResult ForwardPerformanceFile([FromBody] ForwardFilePerformanceDto dto)
        {
            using var conn = new SqlConnection(ConnStr);
            conn.Open();

            string checkSql = @"select 1 from masfilemovementother where presentfileflag='Y' and ponoid=@poId";
            using (var cmd = new SqlCommand(checkSql, conn))
            {
                cmd.Parameters.AddWithValue("@poId", dto.PonoId);
                var exists = cmd.ExecuteScalar();
                if (exists != null)
                {
                    string updSql = @"update masfilemovementother set presentfileflag='N' where ponoid=@poId";
                    using var updCmd = new SqlCommand(updSql, conn);
                    updCmd.Parameters.AddWithValue("@poId", dto.PonoId);
                    updCmd.ExecuteNonQuery();
                }
            }

            DateTime fwdDate = DateTime.TryParseExact(dto.ForwardDate, "dd/MM/yyyy",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateTime parsed) ? parsed : DateTime.Now;

            string insSql = @"insert into masfilemovementother(userid,todate,entryDT,remarks,Flag,presentfileflag,touserid,ponoid,Ptype)
              values(@userId, @fwdDate, GETDATE(), @remarks, @flag, 'Y', @toUserId, @poId, 2)";
            using var insCmd = new SqlCommand(insSql, conn);
            insCmd.Parameters.AddWithValue("@userId", dto.UserId);
            insCmd.Parameters.AddWithValue("@fwdDate", fwdDate.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            insCmd.Parameters.AddWithValue("@remarks", dto.Remarks ?? "");
            insCmd.Parameters.AddWithValue("@flag", dto.Flag ?? "S");
            insCmd.Parameters.AddWithValue("@toUserId", dto.ToUserId);
            insCmd.Parameters.AddWithValue("@poId", dto.PonoId);
            insCmd.ExecuteNonQuery();

            return Ok(new { message = "File Forwarded Successfully!" });
        }

        [HttpGet("generate-perf20-pdf")]
        public IActionResult GeneratePerf20Pdf([FromQuery] int poNoId)
        {
            // TODO: Generate PDF via RDLC or equivalent and return file
            byte[] pdfBytes = System.Text.Encoding.UTF8.GetBytes($"Perf20 PDF placeholder for PONOID={poNoId}");
            return File(pdfBytes, "application/pdf", $"Perf20_{poNoId}.pdf");
        }

        [HttpGet("get-cheque-prep-grid")]
        public IActionResult GetChequePrepGrid([FromQuery] string? status)
        {
            var list = new List<ChequePrepGridDto>();
            try
            {
                using var conn = new SqlConnection(ConnStr);
                conn.Open();

                string whereClause = "";
                if (!string.IsNullOrEmpty(status))
                {
                    whereClause = status == "Complete"
                        ? " AND ISNULL(cp.cheque_no, '') != '' "
                        : " AND (cp.cheque_no IS NULL OR cp.cheque_no = '') ";
                }

                string sql = @"SELECT cp.payment_id, cp.payment_no, cp.po_no, cp.fund,
                    cp.no_of_supplier, cp.no_of_pos, cp.to_be_released_amt,
                    cp.withheld_recovered_amt, cp.status,
                    cp.cgmsc_account_no, cp.cheque_no,
                    CONVERT(varchar, cp.cheque_dt, 103) AS cheque_dt,
                    CONVERT(varchar, cp.paid_on, 103) AS paid_on
                    FROM Payment20ChequePrep cp WHERE 1=1 " + whereClause;

                using var cmd = new SqlCommand(sql, conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new ChequePrepGridDto
                    {
                        PaymentId = Convert.ToInt32(reader["payment_id"]),
                        PaymentNo = reader["payment_no"]?.ToString() ?? "",
                        PoNo = reader["po_no"]?.ToString() ?? "",
                        Fund = reader["fund"]?.ToString() ?? "",
                        NoOfSupplier = reader["no_of_supplier"] != DBNull.Value ? Convert.ToInt32(reader["no_of_supplier"]) : 0,
                        NoOfPos = reader["no_of_pos"] != DBNull.Value ? Convert.ToInt32(reader["no_of_pos"]) : 0,
                        ToBeReleasedAmt = reader["to_be_released_amt"] != DBNull.Value ? Convert.ToDecimal(reader["to_be_released_amt"]) : 0,
                        WithheldRecoveredAmt = reader["withheld_recovered_amt"] != DBNull.Value ? Convert.ToDecimal(reader["withheld_recovered_amt"]) : 0,
                        Status = reader["status"]?.ToString() ?? "",
                        CgmscAccountNo = reader["cgmsc_account_no"]?.ToString() ?? "",
                        ChequeNo = reader["cheque_no"]?.ToString() ?? "",
                        ChequeDt = reader["cheque_dt"]?.ToString() ?? "",
                        PaidOn = reader["paid_on"]?.ToString() ?? ""
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PerformanceController] GetChequePrepGrid handled exception: {ex.Message}");
            }
            return Ok(list);
        }

        [HttpPost("update-cheque-info")]
        public IActionResult UpdateChequeInfo([FromBody] UpdateChequeInfoDto dto)
        {
            try
            {
                using var conn = new SqlConnection(ConnStr);
                conn.Open();
                string sql = @"UPDATE Payment20ChequePrep SET cheque_no=@cn, cheque_dt=@cd, paid_on=@po WHERE payment_id=@pid";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@cn", dto.ChequeNo ?? "");
                cmd.Parameters.AddWithValue("@cd", (object)dto.ChequeDt ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@po", (object)dto.PaidOn ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@pid", dto.PaymentId);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PerformanceController] UpdateChequeInfo handled exception: {ex.Message}");
            }
            return Ok(new { message = "Cheque info updated successfully" });
        }

        [HttpGet("generate-sanction-notesheet")]
        public IActionResult GenerateSanctionNotesheet([FromQuery] int paymentid)
        {
            // TODO: Generate Word document via RDLC or equivalent and return file
            byte[] docBytes = System.Text.Encoding.UTF8.GetBytes($"Sanction Notesheet placeholder for PaymentID={paymentid}");
            return File(docBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"SanctionNotesheet_{paymentid}.docx");
        }

        // ── Performace20 Consignee ──────────────────────────────────────

        [HttpGet("get-consignee-detail")]
        public IActionResult GetConsigneeDetail([FromQuery] int poId)
        {
            var header = new ConsigneeDetailHeaderDto();
            var grid = new List<ConsigneeDetailGridDto>();

            using var conn = new SqlConnection(ConnStr);
            conn.Open();

            string headerSql = @"select a.po_id,
              convert(varchar,case when a.soissueDT is null then a.po_date else a.soissueDT end,103) as po_date,
              a.po_no, b.item_code_as_per_tender, b.item_name,
              b.percentage, b.basic_rate, b.nosc, b.model, b.make,
              b.POQTY, b.dis, b.receipt_qty, b.insQTY, te.releasetype, te.releasevalue
              from purchase_order a
              inner join (
                select pi.po_id, max(ci.make) as make, max(ci.model) as model, max(ci.percentage) as percentage,
                  max(ci.basic_rate) as basic_rate, max(m.item_code_as_per_tender) as item_code_as_per_tender,
                  max(m.item_name) as item_name, count(distinct pi.consignee_id) as nosc,
                  sum(pi.quantity) as POQTY, isnull(sum(d.dis),0) as dis,
                  isnull(sum(r.receipt_qty),0) as receipt_qty, isnull(sum(i.insQTY),0) as insQTY
                from po_items pi
                inner join contract_items ci on ci.contract_item_id = pi.contract_item_id
                inner join masitems m on m.item_id = pi.item_id
                left outer join (select r.location_id, sum(r.receipt_qty) as receipt_qty, r.po_id
                  from receipts r group by r.location_id, r.po_id) r
                  on r.location_id = pi.consignee_id and r.po_id = pi.po_id
                left outer join (select r.location_id, sum(ri.received_qty) as insQTY, r.po_id
                  from receipts r inner join receipt_item_details ri on ri.receipt_id = r.receipt_id
                  group by r.location_id, r.po_id) i
                  on i.location_id = pi.consignee_id and i.po_id = pi.po_id
                left outer join (select sum(si.Supplyqty) as dis, s.po_id, s.location_id
                  from SupplierDispatch s inner join Issue_item_details si on si.Issue_id = s.Issue_id
                  group by s.location_id, s.po_id) d
                  on d.location_id = pi.consignee_id and d.po_id = pi.po_id
                where pi.po_id = @poId
                group by pi.po_id
              ) b on b.po_id = a.po_id
              inner join tenders te on te.tender_id = a.tender_id
              where a.po_id = @poId";

            using (var cmd = new SqlCommand(headerSql, conn))
            {
                cmd.Parameters.AddWithValue("@poId", poId);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    header.PoId = Convert.ToInt32(reader["po_id"]);
                    header.PoDate = reader["po_date"]?.ToString() ?? "";
                    header.PoNo = reader["po_no"]?.ToString() ?? "";
                    header.ItemCode = reader["item_code_as_per_tender"]?.ToString() ?? "";
                    header.ItemName = reader["item_name"]?.ToString() ?? "";
                    header.Percentage = reader["percentage"] != DBNull.Value ? Convert.ToDecimal(reader["percentage"]) : 0;
                    header.BasicRate = reader["basic_rate"] != DBNull.Value ? Convert.ToDecimal(reader["basic_rate"]) : 0;
                    header.NoOfConsignee = reader["nosc"] != DBNull.Value ? Convert.ToInt32(reader["nosc"]) : 0;
                    header.Model = reader["model"]?.ToString() ?? "";
                    header.Make = reader["make"]?.ToString() ?? "";
                    header.POQTY = reader["POQTY"] != DBNull.Value ? Convert.ToDecimal(reader["POQTY"]) : 0;
                    header.DispatchQty = reader["dis"] != DBNull.Value ? Convert.ToDecimal(reader["dis"]) : 0;
                    header.ReceiptQty = reader["receipt_qty"] != DBNull.Value ? Convert.ToDecimal(reader["receipt_qty"]) : 0;
                    header.InsQTY = reader["insQTY"] != DBNull.Value ? Convert.ToDecimal(reader["insQTY"]) : 0;
                    var rt = reader["releasetype"]?.ToString() ?? "";
                    var rv = reader["releasevalue"] != DBNull.Value ? Convert.ToInt32(reader["releasevalue"]) : 0;
                    header.ReleaseType = rt == "W" ? "Week" : rt == "M" ? "Month" : rt;
                    header.ReleaseDur = rv > 0 ? rv.ToString() : "";
                }
            }

            string gridSql = @"select l.location_name,
              pi.quantity as po_qty, isnull(d.dis,0) as dispatched_qty,
              isnull(r.receipt_qty,0) as received_qty, isnull(i.insQTY,0) as installed_qty,
              i.installation_date
              from po_items pi
              inner join maslocations l on l.location_id = pi.consignee_id
              left outer join (select r.location_id, sum(r.receipt_qty) as receipt_qty, r.po_id
                from receipts r group by r.location_id, r.po_id) r
                on r.location_id = pi.consignee_id and r.po_id = pi.po_id
              left outer join (select r.location_id, sum(ri.received_qty) as insQTY, r.po_id,
                  convert(varchar, max(ri.installation_date), 103) as installation_date
                from receipts r inner join receipt_item_details ri on ri.receipt_id = r.receipt_id
                where r.recieved_date is not null and r.status in ('C')
                group by r.location_id, r.po_id) i
                on i.location_id = pi.consignee_id and i.po_id = pi.po_id
              left outer join (select sum(si.Supplyqty) as dis, s.po_id, s.location_id
                from SupplierDispatch s inner join Issue_item_details si on si.Issue_id = s.Issue_id
                where s.status = 'C' group by s.location_id, s.po_id) d
                on d.location_id = pi.consignee_id and d.po_id = pi.po_id
              where pi.po_id = @poId
              order by l.location_name";

            using (var cmd = new SqlCommand(gridSql, conn))
            {
                cmd.Parameters.AddWithValue("@poId", poId);
                using var reader = cmd.ExecuteReader();
                int sno = 0;
                while (reader.Read())
                {
                    sno++;
                    grid.Add(new ConsigneeDetailGridDto
                    {
                        Sno = sno,
                        LocationName = reader["location_name"]?.ToString() ?? "",
                        PoQty = reader["po_qty"] != DBNull.Value ? Convert.ToDecimal(reader["po_qty"]) : 0,
                        DispatchedQty = reader["dispatched_qty"] != DBNull.Value ? Convert.ToDecimal(reader["dispatched_qty"]) : 0,
                        ReceivedQty = reader["received_qty"] != DBNull.Value ? Convert.ToDecimal(reader["received_qty"]) : 0,
                        InstalledQty = reader["installed_qty"] != DBNull.Value ? Convert.ToDecimal(reader["installed_qty"]) : 0,
                        InstallationDate = reader["installation_date"]?.ToString() ?? ""
                    });
                }
            }

            return Ok(new ConsigneeDetailResponseDto { Header = header, Grid = grid });
        }

        [HttpPost("upload-consignee-performance")]
        public IActionResult UploadConsigneePerformance([FromBody] UploadConsigneePerformanceDto dto)
        {
            using var conn = new SqlConnection(ConnStr);
            conn.Open();
            string sql = @"update purchase_order set
              PerfCertType = @perfCertType, Rec20by = @userId, Rec20DT = GETDATE()
              where po_id = @poId";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@poId", dto.PoId);
            cmd.Parameters.AddWithValue("@perfCertType", dto.PerfCertType);
            cmd.Parameters.AddWithValue("@userId", dto.UserId);
            cmd.ExecuteNonQuery();
            return Ok(new { message = "Consignee performance uploaded successfully" });
        }

        // ── FIN Release ────────────────────────────────────────────────

        [HttpGet("get-release-years")]
        public IActionResult GetReleaseYears()
        {
            var list = new List<ReleaseYearDto>();
            try
            {
                using var conn = new SqlConnection(ConnStr);
                conn.Open();
                string sql = "select release_year_id, release_year_name from mas_release_year order by release_year_name";
                using var cmd = new SqlCommand(sql, conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new ReleaseYearDto
                    {
                        ReleaseYearId = Convert.ToInt32(reader["release_year_id"]),
                        ReleaseYearName = reader["release_year_name"]?.ToString() ?? ""
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PerformanceController] GetReleaseYears handled exception: {ex.Message}");
                try
                {
                    using var conn = new SqlConnection(ConnStr);
                    conn.Open();
                    string sql = "select yearid as release_year_id, financialyear as release_year_name from masfinancialyears order by financialyear desc";
                    using var cmd = new SqlCommand(sql, conn);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        list.Add(new ReleaseYearDto
                        {
                            ReleaseYearId = Convert.ToInt32(reader["release_year_id"]),
                            ReleaseYearName = reader["release_year_name"]?.ToString() ?? ""
                        });
                    }
                }
                catch { }
            }
            return Ok(list);
        }

        [HttpGet("get-funds")]
        public IActionResult GetFunds()
        {
            var list = new List<FundDto>();
            try
            {
                using var conn = new SqlConnection(ConnStr);
                conn.Open();
                string sql = "select fund_id, fund_name from mas_fund order by fund_name";
                using var cmd = new SqlCommand(sql, conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new FundDto
                    {
                        FundId = Convert.ToInt32(reader["fund_id"]),
                        FundName = reader["fund_name"]?.ToString() ?? ""
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PerformanceController] GetFunds handled exception: {ex.Message}");
                try
                {
                    using var conn = new SqlConnection(ConnStr);
                    conn.Open();
                    string sql = "select fundid as fund_id, fundname as fund_name from masfunds order by fundname";
                    using var cmd = new SqlCommand(sql, conn);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        list.Add(new FundDto
                        {
                            FundId = Convert.ToInt32(reader["fund_id"]),
                            FundName = reader["fund_name"]?.ToString() ?? ""
                        });
                    }
                }
                catch { }
            }
            return Ok(list);
        }

        [HttpGet("get-fin-release-grid")]
        public IActionResult GetFinReleaseGrid([FromQuery] int releaseYearId, [FromQuery] int fundId)
        {
            var list = new List<FinReleaseGridDto>();
            try
            {
                using var conn = new SqlConnection(ConnStr);
                conn.Open();
                string sql = @"select distinct
                  p.po_id, p.po_no, sp.name as supplier_name, p.fileno as nasti_no,
                  pi.quantity as installed_qty,
                  convert(varchar, red.LastInstDT, 103) as last_installed_date,
                  s.withheld_amt, s.to_be_released_amt, s.recovered_amount,
                  s.remarks, s.cheque_dt, s.paid_from, s.paid_to,
                  te.tender_no, isnull(cp.status, 'No Online Complain') as complaint_status,
                  te.performacereq as performance_required,
                  case when te.releasetype = 'W' then convert(varchar, dateadd(week, te.releasevalue, red.LastInstDT), 103)
                       when te.releasetype = 'M' then convert(varchar, dateadd(month, te.releasevalue, red.LastInstDT), 103)
                       else 'NA' end as LimitDT
                  from purchase_order p
                  inner join po_items pi on pi.po_id = p.po_id
                  inner join massuppliers sp on sp.supplier_id = p.supplier_id
                  inner join tenders te on te.tender_id = p.tender_id
                  left outer join (
                    select max(ri.installation_date) as LastInstDT, r.po_id
                    from receipts r inner join receipt_item_details ri on ri.receipt_id = r.receipt_id
                    where r.recieved_date is not null and r.status in ('C')
                    group by r.po_id
                  ) red on red.po_id = p.po_id
                  left outer join (
                    select cp.status, r.po_id from complaints cp
                    inner join receipt_item_details ric on ric.item_detail_id = cp.item_detail_id
                    inner join receipts r on r.receipt_id = ric.receipt_id
                  ) cp on cp.po_id = p.po_id
                  left outer join (
                    select po_id, sum(withheld_amt) as withheld_amt, sum(to_be_released_amt) as to_be_released_amt,
                      sum(recovered_amount) as recovered_amount, max(remarks) as remarks,
                      max(cheque_dt) as cheque_dt, max(paid_from) as paid_from, max(paid_to) as paid_to
                    from payment_release where release_year_id = @releaseYearId and fund_id = @fundId
                    group by po_id
                  ) s on s.po_id = p.po_id
                  where 1=1
                  order by sp.name";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@releaseYearId", releaseYearId);
                cmd.Parameters.AddWithValue("@fundId", fundId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var limitDT = reader["LimitDT"]?.ToString() ?? "";
                    var cstatus = reader["complaint_status"]?.ToString() ?? "";
                    bool isEligible = false;
                    if (limitDT != "NA" && limitDT != "")
                    {
                        if (DateTime.TryParseExact(limitDT, "dd/MM/yyyy",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out DateTime dtLimit))
                        {
                            isEligible = DateTime.Now > dtLimit;
                        }
                    }
                    else if (limitDT == "NA") isEligible = true;

                    list.Add(new FinReleaseGridDto
                    {
                        PoId = reader["po_id"] != DBNull.Value ? Convert.ToInt32(reader["po_id"]) : 0,
                        PoNo = reader["po_no"]?.ToString() ?? "",
                        SupplierName = reader["supplier_name"]?.ToString() ?? "",
                        NastiNo = reader["nasti_no"]?.ToString() ?? "",
                        InstalledQty = reader["installed_qty"] != DBNull.Value ? Convert.ToDecimal(reader["installed_qty"]) : 0,
                        LastInstalledDate = reader["last_installed_date"]?.ToString() ?? "",
                        WithheldAmt = reader["withheld_amt"] != DBNull.Value ? Convert.ToDecimal(reader["withheld_amt"]) : 0,
                        ToBeReleasedAmt = reader["to_be_released_amt"] != DBNull.Value ? Convert.ToDecimal(reader["to_be_released_amt"]) : 0,
                        RecoveredAmount = reader["recovered_amount"] != DBNull.Value ? Convert.ToDecimal(reader["recovered_amount"]) : 0,
                        Remarks = reader["remarks"]?.ToString() ?? "",
                        ChequeDt = reader["cheque_dt"]?.ToString() ?? "",
                        PaidFrom = reader["paid_from"]?.ToString() ?? "",
                        PaidTo = reader["paid_to"]?.ToString() ?? "",
                        TenderNo = reader["tender_no"]?.ToString() ?? "",
                        PerformanceRequired = reader["performance_required"]?.ToString() ?? "",
                        ComplaintStatus = cstatus,
                        IsEligible = isEligible,
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PerformanceController] GetFinReleaseGrid handled exception: {ex.Message}");
            }
            return Ok(list);
        }

        [HttpPost("update-release-data")]
        public IActionResult UpdateReleaseData([FromBody] UpdateReleaseDataDto dto)
        {
            using var conn = new SqlConnection(ConnStr);
            conn.Open();
            string sql = @"update payment_release set
              recovered_amount = @recoveredAmount, remarks = @remarks, updated_by = @userId, updated_dt = GETDATE()
              where po_id = @poId";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@poId", dto.PoId);
            cmd.Parameters.AddWithValue("@recoveredAmount", (object)dto.RecoveredAmount ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@remarks", dto.Remarks ?? "");
            cmd.Parameters.AddWithValue("@userId", dto.UserId);
            cmd.ExecuteNonQuery();
            return Ok(new { message = "Release data updated successfully" });
        }

        [HttpPost("go-for-cheque-preparation")]
        public IActionResult GoForChequePreparation([FromBody] GoForChequePreparationDto dto)
        {
            using var conn = new SqlConnection(ConnStr);
            conn.Open();
            foreach (var poId in dto.PoIds)
            {
                string sql = @"insert into cheque_preparation_batch(po_id, created_by, created_dt, status)
                  values(@poId, @userId, GETDATE(), 'Pending')";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@poId", poId);
                cmd.Parameters.AddWithValue("@userId", dto.UserId);
                cmd.ExecuteNonQuery();
            }
            return Ok(new { message = "Cheque preparation batch created successfully" });
        }

        [HttpGet("get-forward-users")]
        public IActionResult GetForwardUsers([FromQuery] int userId, [FromQuery] string flag)
        {
            var list = new List<ForwardUserDto>();
            string whereClause = "";
            if (flag == "S")
            {
                if (userId == 12 || userId == 11)
                    whereClause = " and roleid in (16,14) ";
                else if (userId == 14)
                    whereClause = " and roleid in (12,16) ";
                else if (userId == 16)
                    whereClause = " and roleid in (17,18,19) ";
                else if (userId == 17)
                    whereClause = " and user_id in (16,18,19) ";
                else if (userId == 18)
                    whereClause = " and user_id in (16,17,19) ";
                else if (userId == 19)
                    whereClause = " and user_id in (16,17,18) ";
                else
                    whereClause = " and user_id in (1) ";
            }
            else
            {
                if (userId == 29) whereClause = " and user_id in (1) ";
                else if (userId == 5) whereClause = " and user_id in (1) ";
                else if (userId == 1) whereClause = " and user_id in (21) ";
            }

            using var conn = new SqlConnection(ConnStr);
            conn.Open();
            string sql = "select user_id, user_name from users where 1=1 " + whereClause;
            using var cmd = new SqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ForwardUserDto
                {
                    UserId = Convert.ToInt32(reader["user_id"]),
                    UserName = reader["user_name"]?.ToString() ?? ""
                });
            }
            return Ok(list);
        }

        [HttpPost("forward-file")]
        public IActionResult ForwardFile([FromBody] ForwardFilePerformanceDto dto)
        {
            using var conn = new SqlConnection(ConnStr);
            conn.Open();

            string checkSql = "select 1 from masfilemovementother where presentfileflag='Y' and ponoid=@poId";
            using (var cmd = new SqlCommand(checkSql, conn))
            {
                cmd.Parameters.AddWithValue("@poId", dto.PonoId);
                var exists = cmd.ExecuteScalar();
                if (exists != null)
                {
                    string updSql = "update masfilemovementother set presentfileflag='N' where ponoid=@poId";
                    using var updCmd = new SqlCommand(updSql, conn);
                    updCmd.Parameters.AddWithValue("@poId", dto.PonoId);
                    updCmd.ExecuteNonQuery();
                }
            }

            DateTime fwdDate = DateTime.TryParseExact(dto.ForwardDate, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateTime parsed) ? parsed : DateTime.Now;

            string insSql = @"insert into masfilemovementother(userid,todate,entryDT,remarks,Flag,presentfileflag,touserid,ponoid,Ptype)
              values(@userId, @fwdDate, GETDATE(), @remarks, @flag, 'Y', @toUserId, @poId, 2)";
            using var insCmd = new SqlCommand(insSql, conn);
            insCmd.Parameters.AddWithValue("@userId", dto.UserId);
            insCmd.Parameters.AddWithValue("@fwdDate", fwdDate.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            insCmd.Parameters.AddWithValue("@remarks", dto.Remarks ?? "");
            insCmd.Parameters.AddWithValue("@flag", dto.Flag ?? "S");
            insCmd.Parameters.AddWithValue("@toUserId", dto.ToUserId);
            insCmd.Parameters.AddWithValue("@poId", dto.PonoId);
            insCmd.ExecuteNonQuery();

            return Ok(new { message = "File Forwarded Successfully!" });
        }
    }
}
