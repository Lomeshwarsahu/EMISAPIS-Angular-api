using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EMISAPIS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EMDRefundController : ControllerBase
    {
        private readonly IConfiguration _config;

        public EMDRefundController(IConfiguration config)
        {
            _config = config;
        }

        private string ConnStr() => _config.GetConnectionString("DefaultConnection")!;

        #region Common Lookups

        [HttpGet("suppliers")]
        public async Task<IActionResult> GetSuppliers()
        {
            try
            {
                var list = new List<EmdSupplierOptionDto>();
                using var conn = new SqlConnection(ConnStr());
                await conn.OpenAsync();
                var sql = "SELECT supplier_id, name FROM massuppliers ORDER BY name";
                using var cmd = new SqlCommand(sql, conn);
                using var dr = await cmd.ExecuteReaderAsync();
                while (await dr.ReadAsync())
                {
                    list.Add(new EmdSupplierOptionDto
                    {
                        SupplierId = Convert.ToInt32(dr["supplier_id"]),
                        SupplierName = dr["name"]?.ToString() ?? string.Empty
                    });
                }
                return Ok(list);
            }
            catch (Exception ex)
            {
                return Ok(new List<EmdSupplierOptionDto>());
            }
        }

        [HttpGet("tenders")]
        public async Task<IActionResult> GetTenders()
        {
            try
            {
                var list = new List<EmdTenderOptionDto>();
                using var conn = new SqlConnection(ConnStr());
                await conn.OpenAsync();
                var sql = "SELECT tender_id, tender_no FROM tenders ORDER BY tender_date DESC";
                using var cmd = new SqlCommand(sql, conn);
                using var dr = await cmd.ExecuteReaderAsync();
                while (await dr.ReadAsync())
                {
                    list.Add(new EmdTenderOptionDto
                    {
                        TenderId = Convert.ToInt32(dr["tender_id"]),
                        TenderNo = dr["tender_no"]?.ToString() ?? string.Empty
                    });
                }
                return Ok(list);
            }
            catch (Exception ex)
            {
                return Ok(new List<EmdTenderOptionDto>());
            }
        }

        #endregion

        #region EMD Refund Tenderwise

        [HttpGet("pending-emd")]
        public async Task<IActionResult> GetPendingEmd(
            [FromQuery] int supplierId = 0,
            [FromQuery] int tenderId = 0)
        {
            try
            {
                var list = new List<EmdRefundPendingDto>();
                using var conn = new SqlConnection(ConnStr());
                await conn.OpenAsync();

                var where = " WHERE 1=1";
                if (supplierId > 0)
                {
                    where += " AND ed.SupId = @supplierId";
                }
                if (tenderId > 0)
                {
                    where += " AND (ed.TenderNo = CAST(@tenderId AS varchar(50)) OR t.tender_id = @tenderId)";
                }

                var sql = $@"
                    SELECT ed.id, ed.SupId, s.name AS SupplierName,
                           CASE WHEN ISNULL(t.tender_no, '') != '' THEN t.tender_no ELSE ed.OtherTenderNo END AS TenderNo,
                           ISNULL(ed.EMDAmt, 0) AS EMDAmt, ISNULL(d.dtypename, 'EMD') AS EMDType,
                           ISNULL(ed.EMDDocumentNo, '') AS EMDDocumentNo, ISNULL(ed.EMDDocument, '') AS EMDDocument,
                           CONVERT(varchar, ed.EMDDepositeDt, 103) AS EMDDepositeDt,
                           CONVERT(varchar, ed.EntryDate, 103) AS EntryDate,
                           ISNULL(fm.BMEApproval, 'N') AS BMEApproval
                    FROM EMDDepositeDetail ed
                    INNER JOIN massuppliers s ON s.supplier_id = ed.SupId
                    LEFT JOIN EMDFileMovement fm ON fm.FileID = ed.Id AND fm.SupplierID = ed.SupId
                    LEFT JOIN tenders t ON (CAST(t.tender_id AS varchar(50)) = ed.TenderNo OR t.tender_no = ed.TenderNo)
                    LEFT JOIN MASDOCUMENTTYPE d ON CAST(d.dtypeid AS varchar(50)) = CAST(ed.EMDType AS varchar(50))
                    {where}
                    ORDER BY ed.EntryDate DESC";

                using var cmd = new SqlCommand(sql, conn);
                if (supplierId > 0) cmd.Parameters.AddWithValue("@supplierId", supplierId);
                if (tenderId > 0) cmd.Parameters.AddWithValue("@tenderId", tenderId);

                using var dr = await cmd.ExecuteReaderAsync();
                while (await dr.ReadAsync())
                {
                    var doc = dr["EMDDocument"]?.ToString() ?? string.Empty;
                    var app = dr["BMEApproval"]?.ToString() ?? "N";
                    list.Add(new EmdRefundPendingDto
                    {
                        Id = Convert.ToInt32(dr["id"]),
                        SupId = Convert.ToInt32(dr["SupId"]),
                        SupplierName = dr["SupplierName"]?.ToString() ?? string.Empty,
                        TenderNo = dr["TenderNo"]?.ToString() ?? string.Empty,
                        EmdAmt = dr["EMDAmt"] != DBNull.Value ? Convert.ToDecimal(dr["EMDAmt"]) : 0,
                        EmdType = dr["EMDType"]?.ToString() ?? string.Empty,
                        EmdDocumentNo = dr["EMDDocumentNo"]?.ToString() ?? string.Empty,
                        EmdDocument = doc,
                        EmdDepositDate = dr["EMDDepositeDt"]?.ToString() ?? string.Empty,
                        EntryDate = dr["EntryDate"]?.ToString() ?? string.Empty,
                        HasFile = !string.IsNullOrWhiteSpace(doc),
                        Status = app == "Y" ? "Approved & Sent to GM(T)" : "Pending Approval"
                    });
                }
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error querying pending EMD: " + ex.Message });
            }
        }

        [HttpPost("approve-emd")]
        public async Task<IActionResult> ApproveEmd([FromBody] EmdApproveRequestDto req)
        {
            if (req.Items == null || !req.Items.Any())
            {
                return BadRequest(new { message = "No EMD items selected for approval." });
            }

            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();
            using var trans = conn.BeginTransaction();

            try
            {
                var sql = @"
                    INSERT INTO EMDFileMovement (FileID, SupplierID, BMEApproval, BMEApprovalDate, FileStatus)
                    VALUES (@fileId, @supplierId, 'Y', GETDATE(), 'Pending')";

                foreach (var item in req.Items)
                {
                    using var cmd = new SqlCommand(sql, conn, trans);
                    cmd.Parameters.AddWithValue("@fileId", item.Id);
                    cmd.Parameters.AddWithValue("@supplierId", item.SupId);
                    await cmd.ExecuteNonQueryAsync();
                }

                await trans.CommitAsync();
                return Ok(new { message = "Selected EMD files successfully approved & sent to GM(T)." });
            }
            catch (Exception ex)
            {
                await trans.RollbackAsync();
                return StatusCode(500, new { message = "Failed to approve EMD files: " + ex.Message });
            }
        }

        #endregion

        #region SD Release Finance

        [HttpGet("sd-suppliers")]
        public async Task<IActionResult> GetSdSuppliers([FromQuery] int tenderId = 0)
        {
            try
            {
                var list = new List<EmdSupplierOptionDto>();
                using var conn = new SqlConnection(ConnStr());
                await conn.OpenAsync();

                var sql = @"
                    SELECT DISTINCT ms.supplier_id, ms.name
                    FROM purchase_order p
                    INNER JOIN massuppliers ms ON ms.supplier_id = p.supplier_id
                    WHERE (@tenderId = 0 OR p.tender_id = @tenderId)
                    ORDER BY ms.name";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@tenderId", tenderId);

                using var dr = await cmd.ExecuteReaderAsync();
                while (await dr.ReadAsync())
                {
                    list.Add(new EmdSupplierOptionDto
                    {
                        SupplierId = Convert.ToInt32(dr["supplier_id"]),
                        SupplierName = dr["name"]?.ToString() ?? string.Empty
                    });
                }
                return Ok(list);
            }
            catch (Exception ex)
            {
                return Ok(new List<EmdSupplierOptionDto>());
            }
        }

        [HttpGet("pending-sd")]
        public async Task<IActionResult> GetPendingSd(
            [FromQuery] int supplierId = 0,
            [FromQuery] int tenderId = 0)
        {
            try
            {
                var list = new List<SdReleasePendingDto>();
                using var conn = new SqlConnection(ConnStr());
                await conn.OpenAsync();

                var where = " WHERE 1=1";
                if (supplierId > 0)
                {
                    where += " AND ms.supplier_id = @supplierId";
                }
                if (tenderId > 0)
                {
                    where += " AND t.tender_id = @tenderId";
                }

                var sql = $@"
                    SELECT p.po_id, t.tender_no AS Tender_NO, ISNULL(p.outward_no + '/', '') + p.po_no AS PONO, 
                           CONVERT(varchar, ISNULL(p.soissueDT, p.po_date), 103) AS PO_Date,
                           ms.name AS Supplier_Name, ISNULL(ps.SDAmount, 0) AS SDAmount, ISNULL(msd.SDNAME, 'SD') AS SDType,
                           CONVERT(varchar, ps.IssueDT, 103) AS SDIssueDate,
                           CONVERT(varchar, ps.MaturityDT, 103) AS SDMaturityDate,
                           CONVERT(varchar, ps.entryDT, 103) AS SDEntryDate, ISNULL(ps.SDDetailsID, 0) AS SDDetailsID
                    FROM PO_SDDetails ps
                    INNER JOIN purchase_order p ON p.po_id = ps.po_id
                    INNER JOIN massuppliers ms ON ms.supplier_id = p.supplier_id
                    LEFT JOIN MasSD msd ON CAST(msd.SDMode AS varchar(50)) = CAST(ps.SDMode AS varchar(50))
                    LEFT JOIN tenders t ON t.tender_id = p.tender_id
                    {where}
                    ORDER BY ps.entryDT DESC";

                using var cmd = new SqlCommand(sql, conn);
                if (supplierId > 0) cmd.Parameters.AddWithValue("@supplierId", supplierId);
                if (tenderId > 0) cmd.Parameters.AddWithValue("@tenderId", tenderId);

                using var dr = await cmd.ExecuteReaderAsync();
                while (await dr.ReadAsync())
                {
                    list.Add(new SdReleasePendingDto
                    {
                        PoId = Convert.ToInt32(dr["po_id"]),
                        TenderNo = dr["Tender_NO"]?.ToString() ?? string.Empty,
                        PoNo = dr["PONO"]?.ToString() ?? string.Empty,
                        PoDate = dr["PO_Date"]?.ToString() ?? string.Empty,
                        SupplierName = dr["Supplier_Name"]?.ToString() ?? string.Empty,
                        SdAmount = Convert.ToDecimal(dr["SDAmount"]),
                        SdType = dr["SDType"]?.ToString() ?? string.Empty,
                        SdIssueDate = dr["SDIssueDate"]?.ToString() ?? string.Empty,
                        SdMaturityDate = dr["SDMaturityDate"]?.ToString() ?? string.Empty,
                        SdEntryDate = dr["SDEntryDate"]?.ToString() ?? string.Empty,
                        SdDetailsId = Convert.ToInt32(dr["SDDetailsID"])
                    });
                }
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error querying pending SD: " + ex.Message });
            }
        }

        [HttpPost("release-sd")]
        public async Task<IActionResult> ReleaseSd([FromBody] SdReleaseSaveRequestDto req)
        {
            if (req.PoIds == null || !req.PoIds.Any())
            {
                return BadRequest(new { message = "No purchase orders selected for SD release." });
            }

            using var conn = new SqlConnection(ConnStr());
            await conn.OpenAsync();
            using var trans = conn.BeginTransaction();

            try
            {
                var sql = @"
                    UPDATE PO_SDDetails
                    SET isreleased = 'Y',
                        releaseddate = @refundDate,
                        documentno = @chequeNo,
                        updatedt = GETDATE(),
                        releasedamt = @releaseAmount
                    WHERE po_id = @poId";

                foreach (var poId in req.PoIds)
                {
                    using var cmd = new SqlCommand(sql, conn, trans);
                    cmd.Parameters.AddWithValue("@poId", poId);
                    cmd.Parameters.AddWithValue("@refundDate", string.IsNullOrWhiteSpace(req.RefundDate) ? DBNull.Value : req.RefundDate.Trim());
                    cmd.Parameters.AddWithValue("@chequeNo", string.IsNullOrWhiteSpace(req.ChequeNo) ? DBNull.Value : req.ChequeNo.Trim());
                    cmd.Parameters.AddWithValue("@releaseAmount", req.ReleaseAmount);
                    await cmd.ExecuteNonQueryAsync();
                }

                await trans.CommitAsync();
                return Ok(new { message = "Selected SD records successfully released." });
            }
            catch (Exception ex)
            {
                await trans.RollbackAsync();
                return StatusCode(500, new { message = "Failed to release SD: " + ex.Message });
            }
        }

        #endregion
    }
}
