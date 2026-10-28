using EMISAPIS.DTOS;
using EMISAPIS.DTOS.EMISAPIS.DTOS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Text.Json.Serialization;

namespace EMISAPIS.Controllers
{
    //[Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TendercellController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;
        private readonly Tender _tenderHelper;

        // ✅ एकमात्र सिंगल कंस्ट्रक्टर जिसमें दोनों (IConfiguration और Tender) को इनिशियलाइज किया गया है
        public TendercellController(IConfiguration config, IWebHostEnvironment env)
        {
            _config = config;
            _env = env;
            _tenderHelper = new Tender(config); // Tender helper को यहाँ सही से पास कर दिया गया है
        }

        [HttpGet("GetDmeInstitutes/{directorateId}")]
        public async Task<IActionResult> GetDmeInstitutes(int directorateId)
        {
            if (directorateId <= 0)
            {
                return Ok(new List<DirectorateUserDto>());
            }

            string connString = _config.GetConnectionString("DefaultConnection");
            List<DirectorateUserDto> userList = new List<DirectorateUserDto>();
            string sqlQuery = string.Empty;

            if (directorateId == 12)
            {
                sqlQuery = @"
                    SELECT user_id, e_mail_id, user_name, designation 
                    FROM users WITH (NOLOCK) 
                    WHERE user_id != 12 AND authority = @DirectorateId";
            }
            else if (directorateId == 11)
            {
                sqlQuery = @"
                    SELECT u.user_id, e_mail_id, user_name, designation 
                    FROM users u WITH (NOLOCK)
                    INNER JOIN maslocations l WITH (NOLOCK) ON l.location_id = u.location_id
                    WHERE l.authority = @DirectorateId";
            }
            else
            {
                return Ok(new List<DirectorateUserDto>());
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sqlQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@DirectorateId", directorateId);
                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                userList.Add(new DirectorateUserDto
                                {
                                    UserId = reader["user_id"] != DBNull.Value ? Convert.ToInt32(reader["user_id"]) : 0,
                                    EMailId = reader["e_mail_id"]?.ToString() ?? string.Empty,
                                    UserName = reader["user_name"]?.ToString() ?? string.Empty,
                                    Designation = reader["designation"]?.ToString() ?? string.Empty
                                });
                            }
                        }
                    }
                }

                return Ok(userList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching institute details.", error = ex.Message });
            }
        }

        [HttpGet("GetIndentGridData/{directorateId}")]
        public async Task<IActionResult> GetIndentGridData(int directorateId, [FromQuery] string yearid = null, [FromQuery] string userId = null)
        {
            if (directorateId != 11 && directorateId != 12)
            {
                return BadRequest(new { message = "Please provide a valid Directorate ID (11 or 12)." });
            }

            string connString = _config.GetConnectionString("DefaultConnection");
            List<IndentGridDto> indentList = new List<IndentGridDto>();
            string selectQuery = string.Empty;

            if (directorateId == 12)
            {
                selectQuery = @"
                    SELECT 
                        a.indentno AS description, 
                        A.indentid, 
                        A.location_id, 
                        A.directorate_id, 
                        A.financial_year_id,
                        CONVERT(VARCHAR(10), A.indentdate, 103) AS CONSOLIDATED_DATE, 
                        SUM(B.dirappqty) AS FINAL_QTY, 
                        COUNT(DISTINCT b.itemid) AS nosindentQTY,
                        (CASE WHEN A.STATUS = 'I' THEN 'Incomplete' WHEN A.STATUS = 'C' THEN 'Completed' ELSE '' END) AS EStatus,
                        (CASE WHEN a.path IS NULL THEN 'Not Uploaded' ELSE 'Uploaded' END) AS uploadStatus, 
                        u.user_name,
                        A.USER_ID, 
                        (CASE WHEN a.Isapproved = 'I' THEN 'Incomplete' WHEN a.Isapproved = 'Y' THEN 'Approved' ELSE '' END) AS Dirapproved,
                        a.DispatchNo, 
                        CONVERT(VARCHAR(20), a.DispatchDT, 120) AS DispatchDT, 
                        a.cgmscapp
                    FROM mas_indentfacility a WITH (NOLOCK)
                    LEFT OUTER JOIN mas_item_indent b WITH (NOLOCK) ON b.indentid = a.indentid
                    INNER JOIN users u WITH (NOLOCK) ON u.user_id = a.location_id
                    WHERE 1 = 1 
                      AND A.Isapproved = 'Y' 
                      AND a.status = 'C'  
                      AND a.directorate_id = 12";
            }
            else if (directorateId == 11)
            {
                selectQuery = @"
                    SELECT 
                        a.indentno AS description, 
                        A.indentid, 
                        A.location_id, 
                        A.directorate_id, 
                        A.financial_year_id,
                        CONVERT(VARCHAR(10), A.indentdate, 103) AS CONSOLIDATED_DATE, 
                        SUM(B.dirappqty) AS FINAL_QTY, 
                        COUNT(DISTINCT b.itemid) AS nosindentQTY,
                        (CASE WHEN A.STATUS = 'I' THEN 'Incomplete' WHEN A.STATUS = 'C' THEN 'Completed' ELSE '' END) AS EStatus,
                        (CASE WHEN a.path IS NULL THEN 'Not Uploaded' ELSE 'Uploaded' END) AS uploadStatus, 
                        u.user_name,
                        A.USER_ID, 
                        (CASE WHEN a.Isapproved = 'I' THEN 'Incomplete' WHEN a.Isapproved = 'Y' THEN 'Approved' ELSE '' END) AS Dirapproved,
                        a.DispatchNo, 
                        CONVERT(VARCHAR(20), a.DispatchDT, 120) AS DispatchDT, 
                        a.cgmscapp
                    FROM mas_indentfacility a WITH (NOLOCK)
                    LEFT OUTER JOIN mas_item_indent b WITH (NOLOCK) ON b.indentid = a.indentid
                    INNER JOIN maslocations l WITH (NOLOCK) ON l.location_id = a.location_id
                    INNER JOIN users u WITH (NOLOCK) ON u.location_id = l.location_id
                    WHERE 1 = 1  
                      AND A.Isapproved = 'Y' 
                      AND a.status = 'C'  
                      AND a.directorate_id = 11";
            }

            if (!string.IsNullOrEmpty(yearid))
            {
                selectQuery += " AND a.financial_year_id = @YearId";
            }

            if (!string.IsNullOrEmpty(userId) && userId != "0")
            {
                selectQuery += " AND A.USER_ID = @UserId";
            }

            selectQuery += @"
                GROUP BY A.indentid, A.USER_ID, u.user_name, A.DIRECTORATE_ID, A.FINANCIAL_YEAR_ID, 
                         A.STATUS, A.indentdate, A.indentno, a.path, a.location_id, A.Isapproved,
                         a.DispatchNo, a.DispatchDT, a.cgmscapp
                ORDER BY A.indentdate DESC";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(selectQuery, conn))
                    {
                        if (!string.IsNullOrEmpty(yearid))
                        {
                            cmd.Parameters.AddWithValue("@YearId", Convert.ToInt32(yearid));
                        }

                        if (!string.IsNullOrEmpty(userId) && userId != "0")
                        {
                            cmd.Parameters.AddWithValue("@UserId", userId);
                        }

                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                indentList.Add(new IndentGridDto
                                {
                                    Description = reader["description"]?.ToString() ?? string.Empty,
                                    IndentId = reader["indentid"] != DBNull.Value ? Convert.ToInt32(reader["indentid"]) : 0,
                                    LocationId = reader["location_id"] != DBNull.Value ? Convert.ToInt32(reader["location_id"]) : 0,
                                    DirectorateId = reader["directorate_id"] != DBNull.Value ? Convert.ToInt32(reader["directorate_id"]) : 0,
                                    FinancialYearId = reader["financial_year_id"] != DBNull.Value ? Convert.ToInt32(reader["financial_year_id"]) : 0,
                                    ConsolidatedDate = reader["CONSOLIDATED_DATE"]?.ToString() ?? string.Empty,
                                    FinalQty = reader["FINAL_QTY"] != DBNull.Value ? Convert.ToDecimal(reader["FINAL_QTY"]) : 0,
                                    NosIndentQty = reader["nosindentQTY"] != DBNull.Value ? Convert.ToInt32(reader["nosindentQTY"]) : 0,
                                    EStatus = reader["EStatus"]?.ToString() ?? string.Empty,
                                    UploadStatus = reader["uploadStatus"]?.ToString() ?? string.Empty,
                                    UserName = reader["user_name"]?.ToString() ?? string.Empty,
                                    UserId = reader["USER_ID"] != DBNull.Value ? Convert.ToInt32(reader["USER_ID"]) : 0,
                                    DirApproved = reader["Dirapproved"]?.ToString() ?? string.Empty,
                                    DispatchNo = reader["DispatchNo"]?.ToString() ?? string.Empty,
                                    DispatchDt = reader["DispatchDT"]?.ToString() ?? string.Empty,
                                    CgmscApp = reader["cgmscapp"]?.ToString() ?? string.Empty
                                });
                            }
                        }
                    }
                }

                if (indentList.Count > 0)
                {
                    return Ok(indentList);
                }
                else
                {
                    return NotFound(new { message = "No records found for the selected criteria." });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching grid details.", error = ex.Message });
            }
        }

        [HttpGet("GetTendersStatus")]
        public IActionResult GetTendersStatus([FromQuery] string stid, [FromQuery] string undOBClaim)
        {
            try
            {
                DataTable dt = _tenderHelper.Get_TendersStatus(stid, undOBClaim);

                List<TenderStatusDtos> resultList = new List<TenderStatusDtos>();

                foreach (DataRow row in dt.Rows)
                {
                    resultList.Add(new TenderStatusDtos
                    {
                        TenderId = row["tender_id"] != DBNull.Value ? Convert.ToInt32(row["tender_id"]) : 0,
                        Name = row["name"]?.ToString() ?? string.Empty
                    });
                }

                if (resultList.Count > 0)
                {
                    return Ok(resultList);
                }
                else
                {
                    return NotFound(new { message = "No tender status data found." });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching tender status.", error = ex.Message });
            }
        }


        [HttpGet("GetTenderDetails/{schemeid}")]
        public async Task<IActionResult> GetTenderDetails(int schemeid)
        {
            if (schemeid <= 0)
            {
                return BadRequest(new { message = "Please provide a valid scheme/tender ID." });
            }

            string connString = _config.GetConnectionString("DefaultConnection");
            TenderDetailDto tenderDetail = null;

            string sqlQuery = @"
                SELECT 
                    m.tender_id, 
                    m.tender_no, 
                    m.financial_year_id,
                    (CASE 
                        WHEN ms.CSID = '1' THEN 'Tender Live' 
                        WHEN ms.CSID = '2' THEN 'Cover A Opened' 
                        WHEN ms.CSID = '3' THEN 'Cover B Opened' 
                        WHEN ms.CSID = '4' THEN 'Under Demo' 
                        WHEN ms.CSID = '5' THEN 'Price Bid' 
                        ELSE 'Tender Cancelled' 
                     END) AS Status,
                    CONVERT(VARCHAR(10), m.TENDER_DATE, 103) AS TENDER_DATE, 
                    ms.CStatus,
                    CONVERT(VARCHAR, cover_a, 103) AS covADT,
                    CONVERT(VARCHAR, ObjCStartDT, 103) AS ObjCStartDT,
                    CONVERT(VARCHAR, ObjCEndDT, 103) AS ObjCEndDT
                FROM mascoverstatus ms WITH (NOLOCK)
                INNER JOIN TENDERS m WITH (NOLOCK) ON m.csid = ms.csid
                WHERE m.tender_id = @SchemeId";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sqlQuery, conn))
                    {
                        // SQL Injection से बचने के लिए Parameterized Query
                        cmd.Parameters.AddWithValue("@SchemeId", schemeid);

                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                tenderDetail = new TenderDetailDto
                                {
                                    TenderId = reader["tender_id"] != DBNull.Value ? Convert.ToInt32(reader["tender_id"]) : 0,
                                    TenderNo = reader["tender_no"]?.ToString() ?? string.Empty,
                                    FinancialYearId = reader["financial_year_id"] != DBNull.Value ? Convert.ToInt32(reader["financial_year_id"]) : 0,
                                    Status = reader["Status"]?.ToString() ?? string.Empty,
                                    TenderDate = reader["TENDER_DATE"]?.ToString() ?? string.Empty,
                                    CStatus = reader["CStatus"]?.ToString() ?? string.Empty,
                                    CovAdt = reader["covADT"]?.ToString() ?? string.Empty,
                                    ObjCStartDt = reader["ObjCStartDT"]?.ToString() ?? string.Empty,
                                    ObjCEndDt = reader["ObjCEndDT"]?.ToString() ?? string.Empty
                                };
                            }
                        }
                    }
                }

                if (tenderDetail != null)
                {
                    return Ok(tenderDetail);
                }
                else
                {
                    return NotFound(new { message = "No tender details found for the given ID." });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching tender details.", error = ex.Message });
            }
        }

        [HttpGet("GetSchemeStatusDetails/{schemeid}")]
        public async Task<IActionResult> GetSchemeStatusDetails(int schemeid)
        {
            if (schemeid <= 0)
            {
                return BadRequest(new { message = "Please provide a valid scheme/tender ID." });
            }

            string connString = _config.GetConnectionString("DefaultConnection");
            List<SchemedetailsDto> schemeStatusList = new List<SchemedetailsDto>();

            string sqlQuery = @"
                SELECT 
                    0 AS slno,
                    ms.SCHSTATUSDID,
                    t.tender_id,
                    mas.name,
                    ms.EMD,
                    ISNULL(ReqEMDAMt, 0) AS ReqEMDAMt,
                    ISNULL(submittedEMDAMT, 0) AS submittedEMDAMT,
                    ms.TPAMOUNT,
                    ms.EMDDOCTYPE,
                    ms.EMDPATH,
                    ms.EMDFILENAME,
                    ms.TPFILENAME,
                    ms.TPPATH,
                    ms.EMDDOCNO,
                    mas.supplier_id,
                    ms.REMARK,
                    ISNULL(piitem.cntparticipated, 0) AS pitems,
                    ms.ISCovTechEli,
                    ms.IsCOVFinEli,
                    ms.CovATechRemarksBefore_OBClM,
                    ms.CovAFINRemarksBefore_OBClM,
                    dt.dtypename,
                    tel.Eligiblity AS TechEli,
                    FEl.Eligiblity AS FinElig,
                    IsObjUploadElig,
                    ObjUploadRemrks,
                    OBEl.Eligiblity AS OBJElig,
                    FinalCovrARemarks,
                    ms.ISELIGIBLE_B
                FROM tenders t WITH (NOLOCK)
                INNER JOIN masschemesstatusdetails ms WITH (NOLOCK) ON ms.SCHEMEID = t.tender_id
                INNER JOIN masEligiblity tEl WITH (NOLOCK) ON tEl.ID = ms.ISCovTechEli
                INNER JOIN masEligiblity FEl WITH (NOLOCK) ON FEl.ID = ms.IsCOVFinEli
                INNER JOIN masEligiblity OBEl WITH (NOLOCK) ON OBEl.ID = ms.IsObjUploadElig
                INNER JOIN massuppliers mas WITH (NOLOCK) ON mas.supplier_id = ms.SUPPLIERID
                INNER JOIN MASDOCUMENTTYPE dt WITH (NOLOCK) ON dt.dtypeid = ms.EMDDOCTYPE
                LEFT OUTER JOIN 
                (
                    SELECT COUNT(ch.ITEMID) AS cntparticipated, sc.SCHEMEID, sc.SUPPLIERID 
                    FROM SCHEMESTATUSDETAILSCHILD ch WITH (NOLOCK)
                    INNER JOIN masschemesstatusdetails sc WITH (NOLOCK) ON sc.SCHSTATUSDID = ch.SCHSTATUSDID
                    GROUP BY sc.SCHEMEID, sc.SUPPLIERID
                ) piitem ON piitem.SCHEMEID = t.tender_id AND piitem.SUPPLIERID = mas.supplier_id
                LEFT OUTER JOIN 
                (
                    SELECT SUPPLIERID, SUM(emd_amount) AS ReqEMDAMt, emd AS submittedEMDAMT, SCHEMEID
                    FROM 
                    (
                        SELECT ch.ITEMID, sc.SUPPLIERID, sc.EMD, ti.emd_amount, sc.SCHEMEID 
                        FROM SCHEMESTATUSDETAILSCHILD ch WITH (NOLOCK)
                        INNER JOIN masschemesstatusdetails sc WITH (NOLOCK) ON sc.SCHSTATUSDID = ch.SCHSTATUSDID
                        INNER JOIN tender_items ti WITH (NOLOCK) ON ti.item_id = ch.ITEMID AND ti.tender_id = sc.SCHEMEID
                        WHERE sc.SCHEMEID = @SchemeId
                    ) b 
                    GROUP BY SUPPLIERID, EMD, SCHEMEID
                ) em ON em.SCHEMEID = t.tender_id AND em.SUPPLIERID = ms.SUPPLIERID
                WHERE t.tender_id = @SchemeId
                ORDER BY mas.name";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sqlQuery, conn))
                    {
                        // SQL Injection से बचने के लिए Parameterized Query का उपयोग दोनो स्थानों के लिए
                        cmd.Parameters.AddWithValue("@SchemeId", schemeid);

                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                schemeStatusList.Add(new SchemedetailsDto
                                {
                                    Slno = reader["slno"] != DBNull.Value ? Convert.ToInt32(reader["slno"]) : 0,
                                    SchStatusDid = reader["SCHSTATUSDID"] != DBNull.Value ? Convert.ToInt32(reader["SCHSTATUSDID"]) : 0,
                                    TenderId = reader["tender_id"] != DBNull.Value ? Convert.ToInt32(reader["tender_id"]) : 0,
                                    Name = reader["name"]?.ToString() ?? string.Empty,
                                    Emd = reader["EMD"]?.ToString() ?? string.Empty,
                                    ReqEmdAmt = reader["ReqEMDAMt"] != DBNull.Value ? Convert.ToDecimal(reader["ReqEMDAMt"]) : 0,
                                    SubmittedEmdAmt = reader["submittedEMDAMT"] != DBNull.Value ? Convert.ToDecimal(reader["submittedEMDAMT"]) : 0,
                                    TpAmount = reader["TPAMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["TPAMOUNT"]) : 0,
                                    EmdDocType = reader["EMDDOCTYPE"] != DBNull.Value ? Convert.ToInt32(reader["EMDDOCTYPE"]) : 0,
                                    EmdPath = reader["EMDPATH"]?.ToString() ?? string.Empty,
                                    EmdFileName = reader["EMDFILENAME"]?.ToString() ?? string.Empty,
                                    TpFileName = reader["TPFILENAME"]?.ToString() ?? string.Empty,
                                    TpPath = reader["TPPATH"]?.ToString() ?? string.Empty,
                                    EmdDocNo = reader["EMDDOCNO"]?.ToString() ?? string.Empty,
                                    SupplierId = reader["supplier_id"] != DBNull.Value ? Convert.ToInt32(reader["supplier_id"]) : 0,
                                    Remark = reader["REMARK"]?.ToString() ?? string.Empty,
                                    PItems = reader["pitems"] != DBNull.Value ? Convert.ToInt32(reader["pitems"]) : 0,
                                    IsCovTechEli = reader["ISCovTechEli"]?.ToString() ?? string.Empty,
                                    IsCOVFinEli = reader["IsCOVFinEli"]?.ToString() ?? string.Empty,
                                    CovATechRemarksBeforeObclm = reader["CovATechRemarksBefore_OBClM"]?.ToString() ?? string.Empty,
                                    CovAFinRemarksBeforeObclm = reader["CovAFINRemarksBefore_OBClM"]?.ToString() ?? string.Empty,
                                    DtypeName = reader["dtypename"]?.ToString() ?? string.Empty,
                                    TechEli = reader["TechEli"]?.ToString() ?? string.Empty,
                                    FinElig = reader["FinElig"]?.ToString() ?? string.Empty,
                                    IsObjUploadElig = reader["IsObjUploadElig"]?.ToString() ?? string.Empty,
                                    ObjUploadRemrks = reader["ObjUploadRemrks"]?.ToString() ?? string.Empty,
                                    ObjElig = reader["OBJElig"]?.ToString() ?? string.Empty,
                                    FinalCovrARemarks = reader["FinalCovrARemarks"]?.ToString() ?? string.Empty,
                                    IsEligibleB = reader["ISELIGIBLE_B"]?.ToString() ?? string.Empty
                                });
                            }
                        }
                    }
                }

                if (schemeStatusList.Count > 0)
                {
                    return Ok(schemeStatusList);
                }
                else
                {
                    return NotFound(new { message = "No scheme status records found for the given tender ID." });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching scheme status details.", error = ex.Message });
            }
        }

        [HttpGet("GetParticipatedItems")]
        public async Task<IActionResult> GetParticipatedItems([FromQuery] string tenderid, [FromQuery] string schemestatusid)
        {
            if (string.IsNullOrEmpty(tenderid) || string.IsNullOrEmpty(schemestatusid))
            {
                return BadRequest(new { message = "Please provide both tenderid and schemestatusid." });
            }

            string connString = _config.GetConnectionString("DefaultConnection");
            List<ParticipatedItemDto> participatedItemsList = new List<ParticipatedItemDto>();

            string sqlQuery = @"
                SELECT 
                    sc.ChildID,
                    m.item_code_as_per_tender,
                    m.item_name,
                    sc.itemid,
                    sc.FLAGCOB,
                    s.ISELIGIBLE_B,
                    sc.SCHSTATUSDID,
                    s.SCHEMEID,
                    s.SUPPLIERID,
                    sp.name,
                    sc.RemarksA,
                    sc.DemoRemarksID 
                FROM masschemesstatusdetails s WITH (NOLOCK)
                INNER JOIN schemestatusdetailschild sc WITH (NOLOCK) ON sc.SCHSTATUSDID = s.SCHSTATUSDID
                INNER JOIN masitems m WITH (NOLOCK) ON m.item_id = sc.itemid
                INNER JOIN massuppliers sp WITH (NOLOCK) ON sp.supplier_id = s.SUPPLIERID
                WHERE 1 = 1 
                  AND s.SCHEMEID = @TenderId 
                  AND sc.SCHSTATUSDID = @SchemeStatusDid";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sqlQuery, conn))
                    {
                        // SQL Injection से बचने के लिए Parameterized Query
                        cmd.Parameters.AddWithValue("@TenderId", Convert.ToInt32(tenderid));
                        cmd.Parameters.AddWithValue("@SchemeStatusDid", Convert.ToInt32(schemestatusid));

                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                participatedItemsList.Add(new ParticipatedItemDto
                                {
                                    ChildId = reader["ChildID"] != DBNull.Value ? Convert.ToInt32(reader["ChildID"]) : 0,
                                    ItemCodeAsPerTender = reader["item_code_as_per_tender"]?.ToString() ?? string.Empty,
                                    ItemName = reader["item_name"]?.ToString() ?? string.Empty,
                                    ItemId = reader["itemid"] != DBNull.Value ? Convert.ToInt32(reader["itemid"]) : 0,
                                    FlagCob = reader["FLAGCOB"]?.ToString() ?? string.Empty,
                                    IsEligibleB = reader["ISELIGIBLE_B"]?.ToString() ?? string.Empty,
                                    SchStatusDid = reader["SCHSTATUSDID"] != DBNull.Value ? Convert.ToInt32(reader["SCHSTATUSDID"]) : 0,
                                    SchemeId = reader["SCHEMEID"] != DBNull.Value ? Convert.ToInt32(reader["SCHEMEID"]) : 0,
                                    SupplierId = reader["SUPPLIERID"] != DBNull.Value ? Convert.ToInt32(reader["SUPPLIERID"]) : 0,
                                    Name = reader["name"]?.ToString() ?? string.Empty,
                                    RemarksA = reader["RemarksA"]?.ToString() ?? string.Empty,
                                    DemoRemarksId = reader["DemoRemarksID"] != DBNull.Value ? Convert.ToInt32(reader["DemoRemarksID"]) : 0
                                });
                            }
                        }
                    }
                }

                if (participatedItemsList.Count > 0)
                {
                    return Ok(participatedItemsList);
                }
                else
                {
                    return NotFound(new { message = "No participated items found for the given criteria." });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching participated items.", error = ex.Message });
            }
        }


        [HttpPost("UpdateParticipatedItems")]
        public async Task<IActionResult> UpdateParticipatedItems([FromBody] UpdateParticipatedItemsRequestDto model)
        {
            if (model == null || model.Items == null || model.Items.Count == 0)
            {
                return BadRequest(new { message = "No items provided for update." });
            }

            string connString = _config.GetConnectionString("DefaultConnection");
            int cnElig = 0;
            int cnNElig = 0;
            bool value = false;

            // 1. Validation Logic (समान लॉजिक जो आपके वेबफोर्म्स में था)
            foreach (var item in model.Items)
            {
                bool isChecked = item.IsChecked;
                string reason = item.RemarksA?.Trim() ?? string.Empty;

                if (isChecked && string.IsNullOrEmpty(reason))
                {
                    value = true; // अगर चेकबॉक्स टिक है पर रीज़न खाली है
                }
                else if (!isChecked && string.IsNullOrEmpty(reason))
                {
                    value = true; // यदि टिक नहीं है और रीज़न खाली है
                }
            }

            // यदि वैलिडेशन फेल होता है (Remarks missing)
            if (value)
            {
                return BadRequest(new { status = "ValidationError", message = "Remarks for Items Not Eligibility is required" });
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    await conn.OpenAsync();

                    // 2. Database Update Loop
                    foreach (var item in model.Items)
                    {
                        string status = item.IsChecked ? "Y" : "N";
                        if (item.IsChecked) cnElig++; else cnNElig++;

                        string updateQuery = @"
                            UPDATE schemestatusdetailschild 
                            SET FLAGCOB = @Status, RemarksA = @RemarksA 
                            WHERE SCHSTATUSDID = @SchStatusDid 
                              AND ChildID = @ChildId 
                              AND itemid = @ItemId";

                        using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@Status", status);
                            cmd.Parameters.AddWithValue("@RemarksA", (object)item.RemarksA ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@SchStatusDid", item.SchStatusDid);
                            cmd.Parameters.AddWithValue("@ChildId", item.ChildId);
                            cmd.Parameters.AddWithValue("@ItemId", item.ItemId);

                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }

                if (cnElig + cnNElig > 0)
                {
                    return Ok(new
                    {
                        status = "Success",
                        message = $"{cnElig} nos Items Eligible and {cnNElig} nos Items Not Eligible",
                        eligibleCount = cnElig,
                        notEligibleCount = cnNElig
                    });
                }
                else
                {
                    return BadRequest(new { status = "Failed", message = "Not Saved Successfully" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating items.", error = ex.Message });
            }
        }




        // ---------------- Legacy TenderCoverAObClaim.aspx: Claim-Object Preparation ----------------

        // setItemName + ItemDetails
        [HttpGet("GetObjClaimPrep/{tenderId}")]
        public async Task<IActionResult> GetObjClaimPrep(int tenderId)
        {
            string headerSql = @"SELECT m.tender_id, m.tender_no, CONVERT(varchar(10), m.TENDER_DATE, 103) AS TENDER_DATE,
                    CONVERT(varchar, m.cover_a, 103) AS covADT, m.csid,
                    (CASE WHEN ms.CSID = '1' THEN 'Tender Live' WHEN ms.CSID = '2' THEN 'Cover A Opened' WHEN ms.CSID = '3' THEN 'Cover B Opened'
                          WHEN ms.CSID = '4' THEN 'Under Demo' WHEN ms.CSID = '5' THEN 'Price Bid' ELSE 'Tender Cancelled' END) AS Status
                FROM mascoverstatus ms INNER JOIN TENDERS m ON m.csid = ms.csid
                WHERE m.tender_id = @Tid";
            string gridSql = @"SELECT ms.SCHSTATUSDID, t.tender_id, mas.name, mas.supplier_id, ms.EMD,
                    ISNULL(ReqEMDAMt, 0) AS ReqEMDAMt, ISNULL(submittedEMDAMT, 0) AS submittedEMDAMT, ms.REMARK,
                    ISNULL(piitem.cntparticipated, 0) AS pitems, tel.Eligiblity AS TechEli, FEl.Eligiblity AS FinElig,
                    ms.CovATechRemarksBefore_OBClM, ms.CovAFINRemarksBefore_OBClM, ms.IsObjUploadElig, ms.ObjUploadRemrks
                FROM tenders t
                INNER JOIN masschemesstatusdetails ms ON ms.SCHEMEID = t.tender_id
                INNER JOIN masEligiblity tEl ON tEl.ID = ms.ISCovTechEli
                INNER JOIN masEligiblity FEl ON FEl.ID = ms.IsCOVFinEli
                INNER JOIN massuppliers mas ON mas.supplier_id = ms.SUPPLIERID
                INNER JOIN MASDOCUMENTTYPE dt ON dt.dtypeid = ms.EMDDOCTYPE
                LEFT OUTER JOIN (
                    SELECT COUNT(ch.ITEMID) cntparticipated, sc.SCHEMEID, sc.SUPPLIERID FROM SCHEMESTATUSDETAILSCHILD ch
                    INNER JOIN masschemesstatusdetails sc ON sc.SCHSTATUSDID = ch.SCHSTATUSDID
                    GROUP BY sc.SCHEMEID, sc.SUPPLIERID
                ) piitem ON piitem.SCHEMEID = t.tender_id AND piitem.SUPPLIERID = mas.supplier_id
                LEFT OUTER JOIN (
                    SELECT SUPPLIERID, SUM(emd_amount) ReqEMDAMt, emd AS submittedEMDAMT, SCHEMEID FROM (
                        SELECT ch.ITEMID, sc.SUPPLIERID, sc.EMD, ti.emd_amount, sc.SCHEMEID FROM SCHEMESTATUSDETAILSCHILD ch
                        INNER JOIN masschemesstatusdetails sc ON sc.SCHSTATUSDID = ch.SCHSTATUSDID
                        INNER JOIN tender_items ti ON ti.item_id = ch.ITEMID AND ti.tender_id = sc.SCHEMEID
                        WHERE sc.SCHEMEID = @Tid
                    ) b GROUP BY SUPPLIERID, EMD, SCHEMEID
                ) em ON em.SCHEMEID = t.tender_id AND em.SUPPLIERID = ms.SUPPLIERID
                WHERE t.tender_id = @Tid
                ORDER BY mas.name";
            try
            {
                using SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();

                object? header = null;
                using (SqlCommand h = new SqlCommand(headerSql, conn))
                {
                    h.Parameters.AddWithValue("@Tid", tenderId);
                    using SqlDataReader r = await h.ExecuteReaderAsync();
                    if (await r.ReadAsync())
                    {
                        header = new
                        {
                            TenderId = Convert.ToInt32(r["tender_id"]),
                            TenderNo = r["tender_no"]?.ToString() ?? "",
                            TenderDate = r["TENDER_DATE"]?.ToString() ?? "",
                            CoverA = r["covADT"]?.ToString() ?? "",
                            Status = r["Status"]?.ToString() ?? ""
                        };
                    }
                }

                var bidders = new List<object>();
                using (SqlCommand g = new SqlCommand(gridSql, conn))
                {
                    g.Parameters.AddWithValue("@Tid", tenderId);
                    using SqlDataReader r = await g.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                    {
                        bidders.Add(new
                        {
                            SchStatusDid = Convert.ToInt32(r["SCHSTATUSDID"]),
                            TenderId = Convert.ToInt32(r["tender_id"]),
                            SupplierId = Convert.ToInt32(r["supplier_id"]),
                            SupplierName = r["name"]?.ToString() ?? "",
                            ReqEmd = r["ReqEMDAMt"] == DBNull.Value ? 0m : Convert.ToDecimal(r["ReqEMDAMt"]),
                            SubmittedEmd = r["submittedEMDAMT"] == DBNull.Value ? 0m : Convert.ToDecimal(r["submittedEMDAMT"]),
                            TechEli = r["TechEli"]?.ToString() ?? "",
                            TechRemarks = r["CovATechRemarksBefore_OBClM"]?.ToString() ?? "",
                            FinEli = r["FinElig"]?.ToString() ?? "",
                            FinRemarks = r["CovAFINRemarksBefore_OBClM"]?.ToString() ?? "",
                            PItems = Convert.ToInt32(r["pitems"]),
                            Remark = r["REMARK"]?.ToString() ?? "",
                            ObjEligibility = r["IsObjUploadElig"]?.ToString() ?? "",
                            ObjRemarks = r["ObjUploadRemrks"]?.ToString() ?? ""
                        });
                    }
                }

                int pending = 0;
                using (SqlCommand p = new SqlCommand(@"SELECT COUNT(DISTINCT sc.SUPPLIERID) FROM masschemesstatusdetails sc
                    INNER JOIN tenders t ON t.tender_id = sc.SCHEMEID WHERE t.tender_id = @Tid AND t.csid = 2 AND sc.IsObjUploadElig IS NULL", conn))
                {
                    p.Parameters.AddWithValue("@Tid", tenderId);
                    pending = Convert.ToInt32(await p.ExecuteScalarAsync());
                }

                return Ok(new { Header = header, Bidders = bidders, PendingSuppliers = pending });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching claim objection data", error = ex.Message });
            }
        }

        // btnaccepted_Click: writes the Object Claim Prep eligibility only (not the Cover A tech/finance decision)
        [HttpPost("UpdateObjClaimPrep")]
        public async Task<IActionResult> UpdateObjClaimPrep([FromBody] UpdateObjClaimPrepRequest request)
        {
            string eli = (request.Eligibility ?? "").Trim();
            string remarks = (request.Remarks ?? "").Trim();
            if (eli == "" || eli == "0") return BadRequest(new { message = "Please select Eligibility" });
            if (eli == "N" && remarks == "") return BadRequest(new { message = "Please Enter Rejection Remark for Not Eligible" });
            if (eli == "C" && remarks == "") return BadRequest(new { message = "Please Enter Clarification Remarks" });

            try
            {
                using SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
                using SqlCommand cmd = new SqlCommand(@"UPDATE masschemesstatusdetails SET IsObjUploadElig = @Eli, ObjUploadRemrks = @Rem
                    WHERE SCHSTATUSDID = @Sid AND schemeid = @Tid", conn);
                cmd.Parameters.AddWithValue("@Eli", eli);
                cmd.Parameters.AddWithValue("@Rem", remarks);
                cmd.Parameters.AddWithValue("@Sid", request.SchStatusDid);
                cmd.Parameters.AddWithValue("@Tid", request.TenderId);
                await conn.OpenAsync();
                int rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0
                    ? Ok(new { message = "Object Claim Updated Successfully" })
                    : NotFound(new { message = "Record not found to update." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating object claim", error = ex.Message });
            }
        }

        // btnFreezCovA_Click ("Freeze for Object Claim Upload"): tender goes to csid 7 with the objection window dates
        [HttpPost("FreezeObjClaim")]
        public async Task<IActionResult> FreezeObjClaim([FromBody] FreezeObjClaimRequest request)
        {
            if (request.TenderId <= 0) return BadRequest(new { message = "Select Tender No first" });
            if (!DateTime.TryParse(request.StartDate, out DateTime start)) return BadRequest(new { message = "Enter Objection Claim Start Date" });
            if (!DateTime.TryParse(request.EndDate, out DateTime end)) return BadRequest(new { message = "Enter Objection Claim End Date" });

            try
            {
                using SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();

                using (SqlCommand p = new SqlCommand(@"SELECT COUNT(DISTINCT sc.SUPPLIERID) FROM masschemesstatusdetails sc
                    INNER JOIN tenders t ON t.tender_id = sc.SCHEMEID WHERE t.tender_id = @Tid AND t.csid = 2 AND sc.IsObjUploadElig IS NULL", conn))
                {
                    p.Parameters.AddWithValue("@Tid", request.TenderId);
                    if (Convert.ToInt32(await p.ExecuteScalarAsync()) > 0)
                        return BadRequest(new { message = "Claim Objection Not Completed for all suppliers" });
                }

                DateTime? coverA = null;
                using (SqlCommand c = new SqlCommand("SELECT cover_a FROM tenders WHERE tender_id = @Tid", conn))
                {
                    c.Parameters.AddWithValue("@Tid", request.TenderId);
                    object? v = await c.ExecuteScalarAsync();
                    if (v != null && v != DBNull.Value) coverA = Convert.ToDateTime(v).Date;
                }
                if (coverA == null || start.Date < coverA)
                    return BadRequest(new { message = "Object Claim Start Date Should be Greater/Equal to Tender Cover A Date" });
                if (end.Date <= start.Date)
                    return BadRequest(new { message = "Object Claim End Date Should be Greater to Start Date" });

                using SqlCommand u = new SqlCommand("UPDATE tenders SET csid = 7, ObjCStartDT = @S, ObjCEndDT = @E WHERE tender_id = @Tid", conn);
                u.Parameters.AddWithValue("@S", start.Date);
                u.Parameters.AddWithValue("@E", end.Date);
                u.Parameters.AddWithValue("@Tid", request.TenderId);
                await u.ExecuteNonQueryAsync();
                return Ok(new { message = "Object Claim Uploaded Successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Object Claim Not Saved Successfully", error = ex.Message });
            }
        }

        // ---------------- Legacy TenderCoverAObClaimAfterB.aspx: Finalization of Cover B ----------------

        // Tender.cs GetIncompletedCOVB: first "item-supplier" still waiting for an eligible/not eligible decision
        private async Task<string> GetPendingCoverBAsync(SqlConnection conn, int tenderId)
        {
            string sql = @"SELECT TOP 1 mi.item_name + '-' + s.name AS suppliername
                FROM masschemesstatusdetails ms
                INNER JOIN schemestatusdetailschild msc ON msc.schemeid = ms.schemeid AND msc.supplierId = ms.supplierId
                INNER JOIN massuppliers s ON s.supplier_id = ms.supplierid AND s.supplier_id = msc.supplierid
                INNER JOIN masitems mi ON mi.item_id = msc.itemid
                WHERE ms.schemeid = @Tid AND ms.ISELIGIBLE_B = 'Y' AND msc.FLAGCOB = 'Y' AND msc.flagcoc IS NULL";
            using SqlCommand cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Tid", tenderId);
            object? v = await cmd.ExecuteScalarAsync();
            return v == null || v == DBNull.Value ? "" : v.ToString() ?? "";
        }

        // setItemName + ItemDetails2 + GetIncompletedCOVB
        [HttpGet("GetCoverBShortlisting/{tenderId}")]
        public async Task<IActionResult> GetCoverBShortlisting(int tenderId)
        {
            string headerSql = @"SELECT tender_id, tender_no, CONVERT(varchar, TENDER_DATE, 103) AS tender_date,
                    CONVERT(varchar, cover_b, 103) AS cover_b,
                    (CASE WHEN csid = '1' THEN 'Tender Live' WHEN csid = '2' THEN 'Cover A Opened' WHEN csid = '3' THEN 'Cover B Opened'
                          WHEN csid = '4' THEN 'Under Demonstration' WHEN csid = '5' THEN 'Price Bid Opened' ELSE 'Cancelled' END) AS Status
                FROM tenders WHERE tender_id = @Tid";
            string gridSql = @"SELECT s.supplier_id, COUNT(DISTINCT mi.item_Id) AS nositems, s.name AS suppliername,
                    ms.schemeid AS tender_id, ms.SCHSTATUSDID AS schstatusdid, ISNULL(cntW, 0) AS cntworks, ISNULL(cntN, 0) AS cntN
                FROM masschemesstatusdetails ms
                INNER JOIN schemestatusdetailschild msc ON msc.schemeid = ms.schemeid AND msc.supplierId = ms.supplierId
                INNER JOIN massuppliers s ON s.supplier_id = ms.supplierid AND s.supplier_id = msc.supplierid
                INNER JOIN masitems mi ON mi.item_id = msc.itemid
                LEFT OUTER JOIN (SELECT COUNT(itemid) AS cntW, schemeid, SUPPLIERID FROM schemestatusdetailschild
                                 WHERE schemeid = @Tid AND flagcob = 'Y' AND FLAGCOC = 'Y' GROUP BY schemeid, SUPPLIERID) cF
                    ON cF.schemeid = ms.schemeid AND cF.SUPPLIERID = s.supplier_id
                LEFT OUTER JOIN (SELECT COUNT(itemid) AS cntN, schemeid, SUPPLIERID FROM schemestatusdetailschild
                                 WHERE schemeid = @Tid AND flagcob = 'Y' AND FLAGCOC = 'N' GROUP BY schemeid, SUPPLIERID) cN
                    ON cN.schemeid = ms.schemeid AND cN.SUPPLIERID = s.supplier_id
                WHERE ms.schemeid = @Tid AND ms.ISELIGIBLE_B = 'Y' AND msc.FLAGCOB = 'Y'
                GROUP BY s.name, s.supplier_id, ms.schemeid, ms.SCHSTATUSDID, cF.cntW, cN.cntN
                ORDER BY s.name";
            try
            {
                using SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();

                object? header = null;
                using (SqlCommand h = new SqlCommand(headerSql, conn))
                {
                    h.Parameters.AddWithValue("@Tid", tenderId);
                    using SqlDataReader r = await h.ExecuteReaderAsync();
                    if (await r.ReadAsync())
                    {
                        header = new
                        {
                            TenderId = Convert.ToInt32(r["tender_id"]),
                            TenderNo = r["tender_no"]?.ToString() ?? "",
                            TenderDate = r["tender_date"]?.ToString() ?? "",
                            CoverB = r["cover_b"]?.ToString() ?? "",
                            Status = r["Status"]?.ToString() ?? ""
                        };
                    }
                }

                var bidders = new List<object>();
                using (SqlCommand g = new SqlCommand(gridSql, conn))
                {
                    g.Parameters.AddWithValue("@Tid", tenderId);
                    using SqlDataReader r = await g.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                    {
                        bidders.Add(new
                        {
                            SupplierId = Convert.ToInt32(r["supplier_id"]),
                            SupplierName = r["suppliername"]?.ToString() ?? "",
                            TenderId = Convert.ToInt32(r["tender_id"]),
                            SchStatusDid = Convert.ToInt32(r["schstatusdid"]),
                            EligibleItems = Convert.ToInt32(r["nositems"]),
                            EligibleForC = Convert.ToInt32(r["cntworks"]),
                            NotEligibleForC = Convert.ToInt32(r["cntN"])
                        });
                    }
                }

                string pending = await GetPendingCoverBAsync(conn, tenderId);
                return Ok(new { Header = header, Bidders = bidders, Pending = pending });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching Cover B shortlisting", error = ex.Message });
            }
        }

        // Tender.cs GetParticipatedItemsB (Item Action popup)
        [HttpGet("GetCoverBItems")]
        public async Task<IActionResult> GetCoverBItems([FromQuery] int tenderId, [FromQuery] int schStatusDid)
        {
            string sql = @"SELECT sc.ChildID, m.item_code_as_per_tender, m.item_name, sc.itemid, sc.FLAGCOB, sc.FLAGCOC,
                    s.SUPPLIERID, sp.name, sc.RemarksA, sc.DemoRemarksID
                FROM masschemesstatusdetails s
                INNER JOIN schemestatusdetailschild sc ON sc.SCHSTATUSDID = s.SCHSTATUSDID
                INNER JOIN masitems m ON m.item_id = sc.itemid
                INNER JOIN massuppliers sp ON sp.supplier_id = s.SUPPLIERID
                WHERE sc.FLAGCOB = 'Y' AND s.SCHEMEID = @Tid AND sc.SCHSTATUSDID = @Sid";
            try
            {
                var list = new List<object>();
                using SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
                using SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Tid", tenderId);
                cmd.Parameters.AddWithValue("@Sid", schStatusDid);
                await conn.OpenAsync();
                using SqlDataReader r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add(new
                    {
                        ChildId = Convert.ToInt32(r["ChildID"]),
                        ItemId = Convert.ToInt32(r["itemid"]),
                        SupplierId = Convert.ToInt32(r["SUPPLIERID"]),
                        SupplierName = r["name"]?.ToString() ?? "",
                        ItemCode = r["item_code_as_per_tender"]?.ToString() ?? "",
                        ItemName = r["item_name"]?.ToString() ?? "",
                        // Legacy GVDetail_RowDataBound: checked when FLAGCOC = 'Y'
                        Eligible = (r["FLAGCOC"]?.ToString() ?? "") == "Y",
                        Decided = r["FLAGCOC"] != DBNull.Value,
                        DemoRemarksId = r["DemoRemarksID"] == DBNull.Value ? 0 : Convert.ToInt32(r["DemoRemarksID"]),
                        RemarksA = r["RemarksA"]?.ToString() ?? ""
                    });
                }
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching items", error = ex.Message });
            }
        }

        // BindEligiblity: masDemoEligiblity
        [HttpGet("GetDemoEligibilityList")]
        public async Task<IActionResult> GetDemoEligibilityList()
        {
            try
            {
                var list = new List<object>();
                using SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
                using SqlCommand cmd = new SqlCommand("SELECT ID, COVBRemarks FROM masDemoEligiblity ORDER BY ID", conn);
                await conn.OpenAsync();
                using SqlDataReader r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add(new { Id = Convert.ToInt32(r["ID"]), Remarks = r["COVBRemarks"]?.ToString() ?? "" });
                }
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching remarks", error = ex.Message });
            }
        }

        // btnUpdateItems: checked = eligible for Cover C (flagcoc 'Y'), unchecked = not eligible ('N') with a remark
        [HttpPost("UpdateCoverBItems")]
        public async Task<IActionResult> UpdateCoverBItems([FromBody] UpdateCoverBItemsRequest request)
        {
            if (request == null || request.Items == null || request.Items.Count == 0)
                return BadRequest(new { message = "Not Saved Successfully" });
            if (request.Items.Any(i => !i.Eligible && i.DemoRemarksId <= 0))
                return BadRequest(new { message = "Please Select Remarks for Not eligible" });

            try
            {
                using SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    foreach (var i in request.Items)
                    {
                        using SqlCommand cmd = new SqlCommand(@"UPDATE schemestatusdetailschild SET DemoRemarksID = @Rid, flagcoc = @Flag
                            WHERE supplierid = @Sid AND ChildID = @Cid AND itemid = @Iid", conn, trans);
                        cmd.Parameters.AddWithValue("@Rid", i.Eligible ? (object)"0" : i.DemoRemarksId.ToString());
                        cmd.Parameters.AddWithValue("@Flag", i.Eligible ? "Y" : "N");
                        cmd.Parameters.AddWithValue("@Sid", i.SupplierId);
                        cmd.Parameters.AddWithValue("@Cid", i.ChildId);
                        cmd.Parameters.AddWithValue("@Iid", i.ItemId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                    trans.Commit();
                }

                int eligible = request.Items.Count(i => i.Eligible);
                int notEligible = request.Items.Count - eligible;
                string pending = await GetPendingCoverBAsync(conn, request.TenderId);
                return Ok(new { message = eligible + " nos Items Eligible and " + notEligible + " nos Items Not Eligible", pending });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error saving items", error = ex.Message });
            }
        }

        // btnFreezCovA_Click ("Freeze Cover B Shortlisting"): demo PDF -> Mongo DemoFile, tender to Price Bid (csid 5), cover_c = price bid date
        [HttpPost("FreezeCoverB")]
        public async Task<IActionResult> FreezeCoverB([FromForm] FreezeCoverBRequest request)
        {
            if (request.TenderId <= 0) return BadRequest(new { message = "Select Tender No first" });
            if (request.File == null || request.File.Length == 0) return BadRequest(new { message = "Please Upload Demo for Bulk" });
            if (!string.Equals(Path.GetExtension(request.File.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Please Choose PDF File for DEMO" });
            if (request.File.Length > 1000000) return BadRequest(new { message = "File More than 1 MB." });
            if (!DateTime.TryParse(request.PriceBidDate, out DateTime priceBidDate))
                return BadRequest(new { message = "COVER B Shorlisted Not Saved,Please Check Shortlist Items " });

            try
            {
                using SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();

                string pending = await GetPendingCoverBAsync(conn, request.TenderId);
                if (pending != "") return BadRequest(new { message = "Pending Cover B for item " + pending });

                DateTime? coverB = null;
                using (SqlCommand c = new SqlCommand("SELECT cover_b FROM tenders WHERE tender_id = @Tid", conn))
                {
                    c.Parameters.AddWithValue("@Tid", request.TenderId);
                    object? v = await c.ExecuteScalarAsync();
                    if (v != null && v != DBNull.Value) coverB = Convert.ToDateTime(v).Date;
                }
                if (coverB == null || priceBidDate.Date < coverB)
                    return BadRequest(new { message = "Price Bid Date Should be Greater/Equal to Tender Cover B Date" });

                string fileName = "Bulk_E" + request.TenderId;
                using (var ms = new MemoryStream())
                {
                    await request.File.CopyToAsync(ms);
                    await new EMISAPIS.Helpers.MongoService().InsertDemoFile(request.TenderId, fileName, ms.ToArray(), ".pdf");
                }

                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    using (SqlCommand u = new SqlCommand("UPDATE tenders SET csid = 5, cover_c = @Dt WHERE tender_id = @Tid", conn, trans))
                    {
                        u.Parameters.AddWithValue("@Dt", priceBidDate.Date);
                        u.Parameters.AddWithValue("@Tid", request.TenderId);
                        await u.ExecuteNonQueryAsync();
                    }
                    using (SqlCommand u = new SqlCommand("UPDATE schemestatusdetailschild SET DemoFile = @F WHERE FLAGCOC IS NOT NULL AND SCHEMEID = @Tid", conn, trans))
                    {
                        u.Parameters.AddWithValue("@F", fileName);
                        u.Parameters.AddWithValue("@Tid", request.TenderId);
                        await u.ExecuteNonQueryAsync();
                    }
                    trans.Commit();
                }
                return Ok(new { message = "COVER B Shorlisted Successfully,Please Open Price bid from e-Proc" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error freezing Cover B", error = ex.Message });
            }
        }

    }


}