using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

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
    }
}
