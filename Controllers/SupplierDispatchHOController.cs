using EMISAPIS.DTOS;
using EMISAPIS.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EMISAPIS.Controllers
{
    /// <summary>
    /// HO view of supplier dispatches: Order/SupplierDispatchPending_HO.aspx, SupplierDispatchPendingCons_HO.aspx,
    /// SupplierDispatchPendingInsta_HO.aspx and POCell/InCompleteDispatch.aspx.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class SupplierDispatchHOController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly MongoService _mongoService;

        public SupplierDispatchHOController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection missing.");
            _mongoService = new MongoService();
        }

        // Shared inner query of SupplierDispatchPending_HO: PO lines dated before 01/01/2021 with completed dispatch qty.
        private const string PendingPoLinesSql = @"
select b.supplier_id, b.NAME as SUPPLIER_NAME, b.mobile_no, m.location_id,
       R.ITEM_CODE_AS_PER_TENDER as CODE, R.item_name as ITEM_NAME, p.OUTWARD_NO,
       convert(varchar, p.po_date, 103) as po_date, p.po_date as po_date_raw,
       pi.quantity, c.basic_rate, c.percentage, c.single_unit_price, c.single_unit_price * pi.quantity as totalPOvalue,
       c.TENDER_NO, p.STATUS, p.PO_NO, p.PO_ID, isnull(di.Squantity, 0) as Squantity
from po_items pi
inner join MASITEMS R on R.ITEM_ID = pi.item_id
inner join maslocations m on m.location_id = pi.consignee_id
inner join purchase_order p on pi.po_id = p.po_id
inner join MASSUPPLIERS b on p.supplier_id = b.SUPPLIER_ID
inner join MAS_FINANCIAL_YEAR E on E.FINANCIAL_YEAR_ID = p.FINANCIAL_YEAR_ID
left outer join (
    select sum(i.Supplyqty) Squantity, d.po_id
    from SupplierDispatch d
    inner join Issue_item_details i on d.Issue_id = i.Issue_id
    where d.status = 'C'
    group by d.po_id
) di on di.po_id = pi.po_id
left outer join (
    select a.supplier_id, a.tender_id, ci.item_id, ci.basic_rate, ci.percentage, ci.single_unit_price, t.tender_no
    from award_of_contract a
    inner join contract_items ci on ci.award_of_contract_id = a.award_of_contract_id
    inner join tenders t on t.tender_id = a.tender_id
) c on c.item_id = pi.item_id and c.tender_id = p.tender_id and c.supplier_id = p.supplier_id
where p.po_date < '2021-01-01' and p.status in ('Order Placed', 'Partially Received', 'Completed')";

        /// <summary>SupplierDispatchPending_HO fillYear — suppliers with pending dispatch ("name/PO count Mob:...").</summary>
        [HttpGet("pending-suppliers")]
        public async Task<IActionResult> GetPendingSuppliers()
        {
            string sql = $@"
select d.supplier_id,
       d.SUPPLIER_NAME + '/' + cast(count(distinct d.PO_NO) as varchar)
         + ' Mob:' + isnull(cast(cast(d.mobile_no as bigint) as varchar), '') as label
from (
    select supplier_id, SUPPLIER_NAME, mobile_no, PO_NO
    from ({PendingPoLinesSql}) a
    group by supplier_id, SUPPLIER_NAME, mobile_no, ITEM_NAME, OUTWARD_NO, po_date, PO_NO, CODE, TENDER_NO, STATUS, PO_ID, Squantity
    having sum(quantity) > Squantity
) d
group by d.supplier_id, d.SUPPLIER_NAME, d.mobile_no
order by count(distinct d.PO_NO), d.SUPPLIER_NAME";

            try
            {
                var list = new List<DispatchPendingSupplierDto>();
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new DispatchPendingSupplierDto
                    {
                        SupplierId = Convert.ToInt32(reader["supplier_id"]),
                        Label = reader["label"]?.ToString() ?? string.Empty,
                    });
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading suppliers.", detail = ex.Message });
            }
        }

        /// <summary>SupplierDispatchPending_HO fillgrid — pending POs of one supplier.</summary>
        [HttpGet("pending-pos")]
        public async Task<IActionResult> GetPendingPos([FromQuery] int supplierId)
        {
            if (supplierId <= 0)
                return BadRequest(new { message = "Select supplier." });

            string sql = $@"
select CODE, ITEM_NAME, OUTWARD_NO, po_date, PO_NO, sum(quantity) as quantity, count(location_id) as no_of_consignee,
       basic_rate, percentage, sum(totalPOvalue) as totalPOvalue, STATUS, PO_ID, Squantity,
       case when sum(quantity) = Squantity then 'Complete Supplied'
            when Squantity > 0 and sum(quantity) != Squantity then 'Partial Supplied'
            else 'Not Supplied' end as SupplyStatus
from ({PendingPoLinesSql} and b.supplier_id = @SupplierId) a
group by ITEM_NAME, OUTWARD_NO, po_date, po_date_raw, PO_NO, CODE, basic_rate, percentage, single_unit_price, TENDER_NO, STATUS, PO_ID, Squantity
having sum(quantity) > Squantity
order by po_date_raw";

            try
            {
                var list = new List<DispatchPendingPoDto>();
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@SupplierId", supplierId);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new DispatchPendingPoDto
                    {
                        PoId = Convert.ToInt32(reader["PO_ID"]),
                        OutwardNo = reader["OUTWARD_NO"]?.ToString() ?? string.Empty,
                        PoNo = reader["PO_NO"]?.ToString() ?? string.Empty,
                        PoDate = reader["po_date"]?.ToString() ?? string.Empty,
                        ItemCode = reader["CODE"]?.ToString() ?? string.Empty,
                        ItemName = reader["ITEM_NAME"]?.ToString() ?? string.Empty,
                        BasicRate = reader["basic_rate"] == DBNull.Value ? null : Convert.ToDecimal(reader["basic_rate"]),
                        Gst = reader["percentage"] == DBNull.Value ? null : Convert.ToDecimal(reader["percentage"]),
                        NoOfConsignee = Convert.ToInt32(reader["no_of_consignee"]),
                        Quantity = reader["quantity"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["quantity"]),
                        TotalPoValue = reader["totalPOvalue"] == DBNull.Value ? null : Convert.ToDecimal(reader["totalPOvalue"]),
                        DispatchedQty = reader["Squantity"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["Squantity"]),
                        Status = reader["STATUS"]?.ToString() ?? string.Empty,
                        SupplyStatus = reader["SupplyStatus"]?.ToString() ?? string.Empty,
                    });
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading pending purchase orders.", detail = ex.Message });
            }
        }

        /// <summary>
        /// SupplierDispatchPendingCons_HO / SupplierDispatchPendingInsta_HO — consignee rows of a PO with their dispatch batches.
        /// The installation view also returns installation status and whether an invoice document was uploaded.
        /// </summary>
        [HttpGet("po-consignees/{poId:int}")]
        public async Task<IActionResult> GetPoConsignees(int poId)
        {
            if (poId <= 0)
                return BadRequest(new { message = "poId is required." });

            const string rowsSql = @"
select a.po_id, a.po_item_id, a.quantity, a.consignee_id,
       c.item_name, c.item_code_as_per_tender as item_code, x.single_unit_price, c1.location_name,
       x.single_unit_price * a.quantity as Total_Price,
       convert(varchar, b.po_date, 103) as po_date, b.PO_NO
from po_items a
inner join purchase_order b on a.po_id = b.po_id
left outer join maslocations c1 on c1.location_id = a.consignee_id
left outer join masitems c on a.item_id = c.item_id
left outer join (
    select D.ITEM_ID, F.TENDER_ID, F.SUPPLIER_ID, D.single_unit_price
    from AWARD_OF_CONTRACT F
    inner join CONTRACT_ITEMS D on D.AWARD_OF_CONTRACT_ID = F.AWARD_OF_CONTRACT_ID
) x on x.item_id = a.ITEM_ID and x.TENDER_ID = b.TENDER_ID and x.SUPPLIER_ID = b.SUPPLIER_ID
where a.po_id = @PoId
order by c1.location_name";

            const string batchesSql = @"
select d.Issue_id, convert(varchar, d.Tentative_Sdate, 103) as Tentative_Sdate,
       convert(varchar, d.dispatch_date, 103) as dispatch_date, d.status, d.dispatch_no,
       sum(i.Supplyqty) as quantity, d.po_id, d.location_id, re.recieved_date,
       isnull(ins.status, 'I') as ins_status,
       max(case when len(ltrim(rtrim(isnull(d.invoicedocpath, '')))) > 0 then 1 else 0 end) as has_doc
from SupplierDispatch d
left outer join Issue_item_details i on d.Issue_id = i.Issue_id
left outer join (
    select distinct r.issue_id, convert(varchar, r.recieved_date, 103) as recieved_date, r.po_id
    from receipts r
    inner join maslocations l on l.location_id = r.location_id
) re on re.issue_id = d.Issue_id and re.po_id = d.po_id
left outer join (
    select status, issue_id, locationID from Supplierinstallation where po_id = @PoId
) ins on ins.issue_id = d.Issue_id and ins.locationID = d.location_id
where d.po_id = @PoId
group by d.Issue_id, d.dispatch_date, d.Tentative_Sdate, d.status, d.dispatch_no, d.po_id, d.location_id, re.recieved_date, ins.status
order by d.Issue_id";

            try
            {
                var view = new DispatchConsigneeViewDto { PoId = poId };
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                await using (var cmd = new SqlCommand(rowsSql, conn))
                {
                    cmd.Parameters.AddWithValue("@PoId", poId);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        view.PoNo = reader["PO_NO"]?.ToString() ?? string.Empty;
                        view.PoDate = reader["po_date"]?.ToString() ?? string.Empty;
                        view.Rows.Add(new DispatchConsigneeRowDto
                        {
                            PoId = poId,
                            PoItemId = Convert.ToInt32(reader["po_item_id"]),
                            ConsigneeId = reader["consignee_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["consignee_id"]),
                            ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                            ItemCode = reader["item_code"]?.ToString() ?? string.Empty,
                            LocationName = reader["location_name"]?.ToString() ?? string.Empty,
                            UnitPrice = reader["single_unit_price"] == DBNull.Value ? null : Convert.ToDecimal(reader["single_unit_price"]),
                            Quantity = reader["quantity"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["quantity"]),
                            TotalPrice = reader["Total_Price"] == DBNull.Value ? null : Convert.ToDecimal(reader["Total_Price"]),
                        });
                    }
                }

                if (view.Rows.Count == 0)
                    return NotFound(new { message = "Purchase order not found." });

                var batches = new List<DispatchConsigneeBatchDto>();
                await using (var cmd = new SqlCommand(batchesSql, conn))
                {
                    cmd.Parameters.AddWithValue("@PoId", poId);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        batches.Add(new DispatchConsigneeBatchDto
                        {
                            IssueId = Convert.ToInt32(reader["Issue_id"]),
                            PoId = poId,
                            LocationId = Convert.ToInt32(reader["location_id"]),
                            TentativeDate = reader["Tentative_Sdate"]?.ToString() ?? string.Empty,
                            DispatchDate = reader["dispatch_date"]?.ToString() ?? string.Empty,
                            DispatchNo = reader["dispatch_no"]?.ToString() ?? string.Empty,
                            Quantity = reader["quantity"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["quantity"]),
                            ReceivedDate = reader["recieved_date"] == DBNull.Value ? "Not Receipt" : reader["recieved_date"].ToString()!,
                            SupplyStatus = reader["status"]?.ToString() == "I" ? "Incomplete" : "Complete",
                            InstallationStatus = reader["ins_status"]?.ToString() == "I" ? "Incomplete" : "Complete",
                            HasInvoiceDoc = Convert.ToInt32(reader["has_doc"]) == 1,
                        });
                    }
                }

                foreach (var row in view.Rows)
                {
                    row.Batches = batches.Where(b => b.LocationId == row.ConsigneeId).ToList();
                    row.DispatchedQty = row.Batches.Sum(b => b.Quantity);
                }

                return Ok(view);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading consignee dispatch details.", detail = ex.Message });
            }
        }

        /// <summary>InCompleteDispatch — PO lookup (legacy page was opened from the PO dashboard with PoNoID).</summary>
        [HttpGet("incomplete-dispatch/search-po")]
        public async Task<IActionResult> SearchPo([FromQuery] string? term)
        {
            term = InputSanitizer.Sanitize(term, 100);
            if (string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2)
                return Ok(new List<IncompleteDispatchPoOptionDto>());

            const string sql = @"
select top 20 p.po_id, p.po_no, convert(varchar, p.po_date, 103) as po_date, s.name
from purchase_order p
inner join massuppliers s on s.supplier_id = p.supplier_id
where p.po_no like @Term
  and exists (select 1 from SupplierDispatch d where d.po_id = p.po_id)
order by p.po_date desc";

            try
            {
                var list = new List<IncompleteDispatchPoOptionDto>();
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Term", "%" + term.Trim() + "%");
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new IncompleteDispatchPoOptionDto
                    {
                        PoId = Convert.ToInt32(reader["po_id"]),
                        PoNo = reader["po_no"]?.ToString() ?? string.Empty,
                        PoDate = reader["po_date"]?.ToString() ?? string.Empty,
                        SupplierName = reader["name"]?.ToString() ?? string.Empty,
                    });
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error searching purchase orders.", detail = ex.Message });
            }
        }

        /// <summary>InCompleteDispatch PopulateHeaderByPoNo.</summary>
        [HttpGet("incomplete-dispatch/header")]
        public async Task<IActionResult> GetIncompleteDispatchHeader([FromQuery] int poId)
        {
            if (poId <= 0)
                return BadRequest(new { message = "poId is required." });

            const string sql = @"
select p.po_id, p.po_no, convert(varchar, p.po_date, 103) as po_date, s.name as SupplierName,
       max(m.item_name) as itemname, max(m.item_code_as_per_tender) as itemcode, sum(pi.quantity) as poqty,
       dir.facility_aut_id, dir.facility_aut_name
from purchase_order p
inner join po_items pi on pi.po_id = p.po_id
inner join masitems m on m.item_id = pi.item_id
inner join massuppliers s on s.supplier_id = p.supplier_id
inner join facility_aut dir on dir.facility_aut_id = p.directorate_id
where p.po_id = @PoId
group by p.po_id, p.po_no, p.po_date, s.name, dir.facility_aut_id, dir.facility_aut_name";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@PoId", poId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound(new { message = "PO not found." });

                return Ok(new IncompleteDispatchHeaderDto
                {
                    PoId = poId,
                    PoNo = reader["po_no"]?.ToString() ?? string.Empty,
                    PoDate = reader["po_date"]?.ToString() ?? string.Empty,
                    SupplierName = reader["SupplierName"]?.ToString() ?? string.Empty,
                    ItemName = reader["itemname"]?.ToString() ?? string.Empty,
                    ItemCode = reader["itemcode"]?.ToString() ?? string.Empty,
                    TotalPoQty = reader["poqty"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["poqty"]),
                    DirectorateId = Convert.ToInt32(reader["facility_aut_id"]),
                    DirectorateName = reader["facility_aut_name"]?.ToString() ?? string.Empty,
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading PO header.", detail = ex.Message });
            }
        }

        // Dispatch batches of a PO with the consignee receipt date (only receipts that have received quantities count).
        private const string IncompleteDispatchRowsSql = @"
select d.Issue_id, convert(varchar, d.Tentative_Sdate, 103) as Tentative_Sdate,
       convert(varchar, d.dispatch_date, 103) as dispatch_date, d.status, d.dispatch_no,
       sum(i.Supplyqty) as quantity, d.po_id, d.location_id, l.location_name, re.recieved_date
from SupplierDispatch d
inner join maslocations l on l.location_id = d.location_id
left outer join Issue_item_details i on d.Issue_id = i.Issue_id
left outer join (
    select distinct r.issue_id, convert(varchar, r.recieved_date, 103) as recieved_date, r.po_id
    from receipts r
    inner join maslocations l on l.location_id = r.location_id
    left outer join receipt_item_details ri on ri.receipt_id = r.receipt_id
    where ri.received_qty is not null
) re on re.issue_id = d.Issue_id and re.po_id = d.po_id
where d.po_id = @PoId
group by d.Issue_id, d.dispatch_date, d.Tentative_Sdate, d.status, d.dispatch_no, d.po_id, d.location_id, re.recieved_date, l.location_name";

        /// <summary>InCompleteDispatch fillgrid — dispatch batches; only not-yet-received ones can be marked incomplete.</summary>
        [HttpGet("incomplete-dispatch/rows")]
        public async Task<IActionResult> GetIncompleteDispatchRows([FromQuery] int poId)
        {
            if (poId <= 0)
                return BadRequest(new { message = "poId is required." });

            try
            {
                var list = new List<DispatchConsigneeBatchDto>();
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(IncompleteDispatchRowsSql + " order by l.location_name, d.Issue_id", conn);
                cmd.Parameters.AddWithValue("@PoId", poId);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new DispatchConsigneeBatchDto
                    {
                        IssueId = Convert.ToInt32(reader["Issue_id"]),
                        PoId = poId,
                        LocationId = Convert.ToInt32(reader["location_id"]),
                        LocationName = reader["location_name"]?.ToString() ?? string.Empty,
                        TentativeDate = reader["Tentative_Sdate"]?.ToString() ?? string.Empty,
                        DispatchDate = reader["dispatch_date"]?.ToString() ?? string.Empty,
                        DispatchNo = reader["dispatch_no"]?.ToString() ?? string.Empty,
                        Quantity = reader["quantity"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["quantity"]),
                        ReceivedDate = reader["recieved_date"] == DBNull.Value ? "Not Receipt" : reader["recieved_date"].ToString()!,
                        SupplyStatus = reader["status"]?.ToString() == "I" ? "Incomplete" : "Complete",
                    });
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading dispatch rows.", detail = ex.Message });
            }
        }

        /// <summary>InCompleteDispatch fillgridHistryData — all 'In Complete Dispatch' entries (legacy shows every PO).</summary>
        [HttpGet("incomplete-dispatch/history")]
        public async Task<IActionResult> GetIncompleteDispatchHistory()
        {
            const string sql = @"
select p.po_no, convert(varchar, p.po_date, 103) as podate, s.name, upL.location_name as newLocation,
       convert(varchar, pa.entryDT, 103) as entryDT, pa.remark, pa.doc_id
from PO_Reallocation pa
inner join purchase_order p on p.po_id = pa.po_id
inner join massuppliers s on s.supplier_id = p.supplier_id
inner join maslocations upL on upL.location_id = pa.UpLocationid
where pa.UpdateCat = 'In Complete Dispatch'
order by pa.entryDT desc";

            try
            {
                var list = new List<IncompleteDispatchHistoryDto>();
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new IncompleteDispatchHistoryDto
                    {
                        PoNo = reader["po_no"]?.ToString() ?? string.Empty,
                        PoDate = reader["podate"]?.ToString() ?? string.Empty,
                        SupplierName = reader["name"]?.ToString() ?? string.Empty,
                        Location = reader["newLocation"]?.ToString() ?? string.Empty,
                        EntryDate = reader["entryDT"]?.ToString() ?? string.Empty,
                        Remark = reader["remark"]?.ToString() ?? string.Empty,
                        DocId = int.TryParse(reader["doc_id"]?.ToString(), out int docId) ? docId : 0,
                    });
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading history.", detail = ex.Message });
            }
        }

        /// <summary>
        /// InCompleteDispatch BtnReallocate_Click — sets checked, not-yet-received dispatches back to Incomplete ('I'),
        /// logs them in PO_Reallocation and stores the approval PDF in Mongo (same collection as PO reallocation).
        /// </summary>
        [HttpPost("incomplete-dispatch")]
        [RequestSizeLimit(FileValidationHelper.MaxPdfSizeBytes + 1024)]
        public async Task<IActionResult> SaveIncompleteDispatch(
            [FromForm] int poId,
            [FromForm] string? remark,
            [FromForm] string items,
            IFormFile? file)
        {
            if (poId <= 0)
                return BadRequest(new { message = "poId is required." });
            if (!FileValidationHelper.ValidatePdf(file, out var fileErr, maxSizeBytes: FileValidationHelper.MaxPdfSizeBytes))
                return BadRequest(new { message = string.IsNullOrEmpty(fileErr) ? "Upload Mail/Approval Copy/Notesheet in PDF Format." : fileErr });

            List<IncompleteDispatchItemDto>? selected;
            try
            {
                selected = System.Text.Json.JsonSerializer.Deserialize<List<IncompleteDispatchItemDto>>(items ?? string.Empty);
            }
            catch (System.Text.Json.JsonException)
            {
                return BadRequest(new { message = "Invalid items payload." });
            }
            if (selected == null || selected.Count == 0)
                return BadRequest(new { message = "No Checkbox is selected." });

            remark = InputSanitizer.Sanitize(remark, 1000);

            try
            {
                byte[] fileBytes;
                await using (var ms = new MemoryStream())
                {
                    await file!.CopyToAsync(ms);
                    fileBytes = ms.ToArray();
                }

                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Re-check receipt status server-side; a received dispatch must be reversed from receipt first.
                var receivedIssues = new HashSet<int>();
                await using (var cmd = new SqlCommand(IncompleteDispatchRowsSql, conn))
                {
                    cmd.Parameters.AddWithValue("@PoId", poId);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    var validIssues = new HashSet<(int, int)>();
                    while (await reader.ReadAsync())
                    {
                        int issueId = Convert.ToInt32(reader["Issue_id"]);
                        validIssues.Add((issueId, Convert.ToInt32(reader["location_id"])));
                        if (reader["recieved_date"] != DBNull.Value)
                            receivedIssues.Add(issueId);
                    }
                    if (selected.Any(s => !validIssues.Contains((s.IssueId, s.LocationId))))
                        return BadRequest(new { message = "Selected dispatch does not belong to this PO." });
                }
                if (selected.Any(s => receivedIssues.Contains(s.IssueId)))
                    return BadRequest(new { message = "Please Incomplete Receipt/Installation First" });

                await using (var tx = (SqlTransaction)await conn.BeginTransactionAsync())
                {
                    foreach (var item in selected)
                    {
                        const string updateDispatch = @"
update SupplierDispatch set status = 'I' where location_id = @LocationId and po_id = @PoId and Issue_id = @IssueId;
update Issue_item_details set status = 'I' where Issue_id = @IssueId;
insert into PO_Reallocation (po_id, remark, PrevLocationid, UpLocationid, EntryDT, UpdateCat)
values (@PoId, @Remark, @LocationId, @LocationId, getdate(), 'In Complete Dispatch');";
                        await using var cmd = new SqlCommand(updateDispatch, conn, tx);
                        cmd.Parameters.AddWithValue("@LocationId", item.LocationId);
                        cmd.Parameters.AddWithValue("@PoId", poId);
                        cmd.Parameters.AddWithValue("@IssueId", item.IssueId);
                        cmd.Parameters.AddWithValue("@Remark", remark ?? string.Empty);
                        await cmd.ExecuteNonQueryAsync();
                    }
                    await tx.CommitAsync();
                }

                // Legacy doc id = PO id followed by (reallocation count + 1), e.g. PO 1234 with 3 rows -> 12344.
                int relocCount;
                await using (var countCmd = new SqlCommand("select count(RelocID) from PO_Reallocation where po_id = @PoId", conn))
                {
                    countCmd.Parameters.AddWithValue("@PoId", poId);
                    relocCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync() ?? 0) + 1;
                }
                if (!int.TryParse($"{poId}{relocCount}", out int docId))
                    return StatusCode(500, new { message = "Could not generate document id." });

                await _mongoService.UpsertReallocationFile(docId, fileBytes, ".pdf");

                const string updateDoc = @"
update PO_Reallocation set FileName = @DocId, doc_id = @DocId, doc_Name = @DocName
where po_id = @PoId and FileName is null";
                await using (var docCmd = new SqlCommand(updateDoc, conn))
                {
                    docCmd.Parameters.AddWithValue("@DocId", docId);
                    docCmd.Parameters.AddWithValue("@DocName", $"{poId}_{relocCount}IncSD");
                    docCmd.Parameters.AddWithValue("@PoId", poId);
                    await docCmd.ExecuteNonQueryAsync();
                }

                return Ok(new { message = "Supplier Dispatch has been Incompleted Successfully" });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error saving incomplete dispatch.", detail = ex.Message });
            }
        }
    }
}
