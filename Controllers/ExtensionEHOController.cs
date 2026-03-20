using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EMISAPIS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExtensionEHOController : ControllerBase
    {
        private readonly IConfiguration _config;

        public ExtensionEHOController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet("Supplierlist")]
        public async Task<IActionResult> Supplierlist()
        {
            List<SupplierlistDTO> list = new List<SupplierlistDTO>();

            string query = @"select distinct p.supplier_id ,b.name 
                     from purchase_order p 
                     inner join MASSUPPLIERS b 
                     on (p.supplier_id = b.SUPPLIER_ID)
                     and p.status in ('Order Placed','Partially Received','Completed')
                     order by b.name";

            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                SqlCommand cmd = new SqlCommand(query, conn);

                await conn.OpenAsync();

                SqlDataReader reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    SupplierlistDTO data = new SupplierlistDTO
                    {
                        supplier_id = reader["supplier_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["supplier_id"]),
                        name = reader["name"] == DBNull.Value ? null : reader["name"].ToString()
                    };

                    list.Add(data);
                }
            }

            return Ok(list);
        }




        [HttpGet("ExtensionEHODetails")]
        public async Task<IActionResult> GetExtensionEHODetails(string supplierid)
        {
            List<PurchaseOrderGridDTO> list = new List<PurchaseOrderGridDTO>();

            string whereClause = "";

            if (!string.IsNullOrEmpty(supplierid))
            {
                whereClause = " and p.supplier_id=@supplierid ";
            }

            string connStr = _config.GetConnectionString("DefaultConnection");

            string query = @" select posu.item_id, posu.PO_ID, posu.CODE,
		posu.ITEM_NAME,posu.OUTWARD_NO,posu.po_date,posu.PO_NO,posu.quantity,posu.no_of_consignee,
		posu.basic_rate,posu.percentage,posu.single_unit_price,posu.totalPOvalue,posu.tender_no,posu.status,posu.SD,
		pDet.SubmissionStatus,name
		FROM

       ( select a.item_id,PO_ID, CODE,name,
		ITEM_NAME,OUTWARD_NO,po_date,PO_NO,SUM(quantity) as quantity,COUNT(location_id) no_of_consignee,
		basic_rate,percentage,single_unit_price,SUM(totalPOvalue) as totalPOvalue,tender_no,status,
        case when sdname is not null then sdname else 'Not Submitted' end as SD
		from 
		(
		select m.location_id, m.location_name,m.DP_DistrictID,
        R.ITEM_CODE_AS_PER_TENDER as CODE,R.item_name as ITEM_NAME,p.OUTWARD_NO, 
		convert(varchar,p.po_date, 103) as po_date,
		pi.quantity,c.basic_rate,c.percentage,c.single_unit_price,
        c.single_unit_price*pi.quantity as totalPOvalue,b.name
					 ,b.mobile_no,
					c.TENDER_NO,
        convert(varchar,c.TENDER_DATE, 103) as TENDER_DATE ,p.STATUS,
					 pi.item_id,
					p.FINANCIAL_YEAR_ID,E.YEAR,p.APPROVED_BY
                     ,p.total_po_value,p.po_value,p.TENDER_ID,
                     p.PO_NO, p.SUPPLIER_ID
                    ,p.directorate_id,p.indent_fund_id,p.PO_ID
           ,sd.sdname
                      from po_items pi 
				 inner join MASITEMS R on (R.ITEM_ID =pi.item_id)
				 inner join maslocations m on m.location_id=pi.consignee_id
				 inner join purchase_order p on pi.po_id=p.po_id
				 inner join MASSUPPLIERS b on (p.supplier_id =b.SUPPLIER_ID)
				 inner join MAS_FINANCIAL_YEAR E ON (E.FINANCIAL_YEAR_ID=p.FINANCIAL_YEAR_ID)

				left outer join 
				(
				select a.supplier_id,a.tender_id,ci.item_id,ci.basic_rate,ci.percentage,
                ci.single_unit_price,t.tender_no,t.tender_date
                from award_of_contract a 
                inner join contract_items ci 
                on ci.award_of_contract_id=a.award_of_contract_id
                inner join tenders t 
                on t.tender_id=a.tender_id
			    ) c 
                on c.item_id=pi.item_id 
                and c.tender_id=p.tender_id 
                and c.supplier_id=p.supplier_id

left outer join 
(
 select s.sdname,ps.po_id from PO_Sddetails ps
 inner join massd s on s.SDMode=ps.SDMode
 where SubmissionStatus='Y'
) sd on sd.po_id=p.po_id				

				left outer join users u on u.user_id=m.user_id
				where   1=1 
                and p.status in('Order Placed','Partially Received','Completed')
                " + whereClause + @"  
				) a 
                group by sdname ,PO_ID, ITEM_NAME,OUTWARD_NO,po_date,PO_NO,CODE,
                basic_rate,percentage,single_unit_price,tender_no,status,item_id,name

) posu

LEFT OUTER JOIN PO_SDDetails pDet 
on pDet.po_id = posu.po_id 

order by posu.PO_date desc";

            using (SqlConnection con = new SqlConnection(connStr))
            {
                SqlCommand cmd = new SqlCommand(query, con);

                if (!string.IsNullOrEmpty(supplierid))
                    cmd.Parameters.AddWithValue("@supplierid", supplierid);

                await con.OpenAsync();
                SqlDataReader dr = await cmd.ExecuteReaderAsync();

                while (await dr.ReadAsync())
                {
                    list.Add(new PurchaseOrderGridDTO
                    {
                        item_id = Convert.ToInt32(dr["item_id"]),
                        PO_ID = Convert.ToInt32(dr["PO_ID"]),
                        CODE = dr["CODE"].ToString(),
                        ITEM_NAME = dr["ITEM_NAME"].ToString(),
                        OUTWARD_NO = dr["OUTWARD_NO"].ToString(),
                        po_date = dr["po_date"].ToString(),
                        PO_NO = dr["PO_NO"].ToString(),
                        quantity = Convert.ToInt32(dr["quantity"]),
                        no_of_consignee = Convert.ToInt32(dr["no_of_consignee"]),
                        basic_rate = Convert.ToDecimal(dr["basic_rate"]),
                        percentage = Convert.ToDecimal(dr["percentage"]),
                        single_unit_price = Convert.ToDecimal(dr["single_unit_price"]),
                        totalPOvalue = Convert.ToDecimal(dr["totalPOvalue"]),
                        tender_no = dr["tender_no"].ToString(),
                        status = dr["status"].ToString(),
                        SD = dr["SD"].ToString(),
                        SubmissionStatus = dr["SubmissionStatus"].ToString(),
                        name = dr["name"].ToString()
                    });
                }
            }

            return Ok(list);
        }




        //[HttpGet("header/{poId}")]
        //public async Task<IActionResult> GetHeader(int poId)
        //{
        //    List<PoHeaderforextDTO> list = new List<PoHeaderforextDTO>();

        //    string query = @"  select s.name as SupplierName,
        //       m.item_name as ItemName,
        //       p.po_no as PoNo,
        //       convert(varchar,p.po_date,103) as PoDate,
        //       pt.tranche_days as SupplyDays,
        //       convert(varchar,
        //       DATEADD(DAY, pt.tranche_days, p.po_date),103) as PoEndDate
        //from purchase_order p
        //inner join po_tranche pt on pt.po_id=p.po_id
        //inner join massuppliers s on s.supplier_id=p.supplier_id
        //inner join po_items pi on pi.po_id=p.po_id
        //inner join masitems m on m.item_id=pi.item_id
        //where p.po_id = {0}", poId;

        //    using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        //    {
        //        SqlCommand cmd = new SqlCommand(query, conn);

        //        await conn.OpenAsync();

        //        SqlDataReader reader = await cmd.ExecuteReaderAsync();

        //        while (await reader.ReadAsync())
        //        {
        //            SupplierlistDTO data = new SupplierlistDTO
        //            {
        //                supplier_id = reader["supplier_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["supplier_id"]),
        //                name = reader["name"] == DBNull.Value ? null : reader["name"].ToString()
        //            };

        //            list.Add(data);
        //        }
        //    }

        //    return Ok(list);
        //}


        [HttpGet("header/{poId}")]
        public async Task<IActionResult> GetHeader1(int poId)
        {
            List<PoHeaderforextDTO> list = new List<PoHeaderforextDTO>();

            string query = @"
    select s.name as SupplierName,
           m.item_name as ItemName,
           p.po_no as PoNo,
           case when p.soissueDT is null 
                then CONVERT(varchar,p.po_date,103) 
                else CONVERT(varchar,p.soissueDT,103) end as PoDate,
           pt.tranche_days as SupplyDays,
           case when p2.extendeddate is null then 
                CONVERT(varchar, DATEADD(DAY, pt.tranche_days,
                (case when p.soissueDT is null then p.po_date else p.soissueDT end)),103)
           else CONVERT(varchar,p2.extendeddate,103) end as PoEndDate
    from purchase_order p
    inner join po_tranche pt on pt.po_id=p.po_id
    inner join massuppliers s on s.supplier_id=p.supplier_id
    left join (select distinct item_id,po_id from po_items) pi on pi.po_id=p.po_id
    inner join masitems m on m.item_id=pi.item_id
    left join purchase_order p2 on p2.po_id=p.po_id
    where p.po_id = @poId
    and p.status in ('Order Placed','Partially Received','Completed')";

            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@poId", poId);

                await conn.OpenAsync();

                SqlDataReader reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    PoHeaderforextDTO data = new PoHeaderforextDTO
                    {
                        SupplierName = reader["SupplierName"] == DBNull.Value ? null : reader["SupplierName"].ToString(),
                        ItemName = reader["ItemName"] == DBNull.Value ? null : reader["ItemName"].ToString(),
                        PoNo = reader["PoNo"] == DBNull.Value ? null : reader["PoNo"].ToString(),
                        PoDate = reader["PoDate"] == DBNull.Value ? null : reader["PoDate"].ToString(),
                        SupplyDays = reader["SupplyDays"] == DBNull.Value ? null : reader["SupplyDays"].ToString(),
                        PoEndDate = reader["PoEndDate"] == DBNull.Value ? null : reader["PoEndDate"].ToString()
                    };

                    list.Add(data);
                }
            }

            return Ok(list);
        }


        [HttpGet("list/{poId}")]
        public async Task<IActionResult> GetExtensions(int poId)
        {
            List<ExtensionListDTO> list = new List<ExtensionListDTO>();

            string query = @"
        SELECT ped.extensionId,
               ped.po_id,
               ped.remark,
               ped.days,
               convert(varchar,ped.extended_date,103) as extended_date,
               convert(varchar,ped.po_end_date,103) as po_end_date,
               ped.path,
               convert(varchar,ped.letter_date,103) as letter_date,
               ped.letter_no,
               convert(varchar,ped.sys_gen_apply_date,105) as sys_gen_apply_date,
               s.status,
               case when s.isldpenality='N' 
                    then 'Approved without Penalty' 
                    else 'Approved with Penalty' end as penalty
        FROM PO_extension_detail ped
        inner join purchase_order s on ped.po_id = s.po_id
        where ped.po_id = @poId
        order by ped.extensionId";

            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@poId", poId);

                await conn.OpenAsync();

                SqlDataReader reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    ExtensionListDTO data = new ExtensionListDTO
                    {
                        ExtensionId = reader["extensionId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["extensionId"]),
                        PoId = reader["po_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["po_id"]),
                        Remark = reader["remark"] == DBNull.Value ? null : reader["remark"].ToString(),
                        Days = reader["days"] == DBNull.Value ? 0 : Convert.ToInt32(reader["days"]),
                        ExtendedDate = reader["extended_date"] == DBNull.Value ? null : reader["extended_date"].ToString(),
                        PoEndDate = reader["po_end_date"] == DBNull.Value ? null : reader["po_end_date"].ToString(),
                        Path = reader["path"] == DBNull.Value ? null : reader["path"].ToString(),
                        LetterDate = reader["letter_date"] == DBNull.Value ? null : reader["letter_date"].ToString(),
                        LetterNo = reader["letter_no"] == DBNull.Value ? null : reader["letter_no"].ToString(),
                        SysGenApplyDate = reader["sys_gen_apply_date"] == DBNull.Value ? null : reader["sys_gen_apply_date"].ToString(),
                        Status = reader["status"] == DBNull.Value ? null : reader["status"].ToString(),
                        Penalty = reader["penalty"] == DBNull.Value ? null : reader["penalty"].ToString()
                    };

                    list.Add(data);
                }
            }

            return Ok(list);
        }

        [HttpPost("apply")]
        public async Task<IActionResult> ApplyExtension(CreateExtensionDTO dto)
        {
            if (dto.Days <= 0)
                return BadRequest("Extension Days must be greater than 0");

            if (string.IsNullOrEmpty(dto.Remark))
                return BadRequest("Remark Required");

            if (dto.LetterDate == DateTime.MinValue)
                return BadRequest("Letter Date Required");

            string letterNo = $"{new Random().Next(100, 999)}-{new Random().Next(100, 999)}-{dto.PoId}-Extension";

            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                await conn.OpenAsync();

                using (SqlTransaction tran = conn.BeginTransaction())
                {
                    try
                    {
                        // ✅ 1. Insert into PO_extension_detail
                        string insertQuery = @"
                INSERT INTO PO_extension_detail
                (po_id, remark, days, extended_date, po_end_date, path, letter_date, letter_no, sys_gen_apply_date, status)
                VALUES
                (@PoId, @Remark, @Days, @ExtendedDate, @PoEndDate, 'NA', @LetterDate, @LetterNo, GETDATE(), 'Y')";

                        SqlCommand cmd = new SqlCommand(insertQuery, conn, tran);
                        cmd.Parameters.AddWithValue("@PoId", dto.PoId);
                        cmd.Parameters.AddWithValue("@Remark", dto.Remark);
                        cmd.Parameters.AddWithValue("@Days", dto.Days);
                        cmd.Parameters.AddWithValue("@ExtendedDate", dto.ExtendedDate);
                        cmd.Parameters.AddWithValue("@PoEndDate", dto.PoEndDate);
                        cmd.Parameters.AddWithValue("@LetterDate", dto.LetterDate);
                        cmd.Parameters.AddWithValue("@LetterNo", letterNo);

                        await cmd.ExecuteNonQueryAsync();

                        // ✅ 2. Update purchase_order
                        string updateQuery = @"
                UPDATE purchase_order
                SET extendeddate = @ExtendedDate,
                    isldpenality = @IsPenalty
                WHERE po_id = @PoId";

                        SqlCommand cmd2 = new SqlCommand(updateQuery, conn, tran);
                        cmd2.Parameters.AddWithValue("@ExtendedDate", dto.ExtendedDate);
                        cmd2.Parameters.AddWithValue("@IsPenalty", dto.IsPenalty ?? "N");
                        cmd2.Parameters.AddWithValue("@PoId", dto.PoId);

                        await cmd2.ExecuteNonQueryAsync();

                        tran.Commit();

                        return Ok(new
                        {
                            message = "Extension Applied Successfully",
                            letterNo = letterNo
                        });
                    }
                    catch (Exception ex)
                    {
                        tran.Rollback();
                        return StatusCode(500, ex.Message);
                    }
                }
            }
        }
    }
}
