using Microsoft.AspNetCore.Mvc;
using EMISAPIS.DTOS;
using EMISAPIS.DTOS.EMISAPIS.DTOS;
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



    }
}