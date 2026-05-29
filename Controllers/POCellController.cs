using EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver.Core.Configuration;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Text.Json.Serialization;
using static System.Runtime.InteropServices.JavaScript.JSType;
namespace EMISAPIS.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class POCellController : ControllerBase
    {
        private readonly IConfiguration _config;

        public POCellController(IConfiguration config, IWebHostEnvironment env)
        {
            _config = config;
            _env = env;
        }

        private readonly IWebHostEnvironment _env;
        [HttpGet("GetFacilityUserList")]
        public async Task<IActionResult> GetFacilityUserList()
        {
            var reportList = new List<FacilityUserListDto>();
            string connString = _config.GetConnectionString("DefaultConnection");

            // Aapki exact SQL query bina kisi format loss ke parameterized layout me
            string sql = @"
        SELECT f.facility_type_id, 
               COUNT(*) AS no_of_consignee, 
               fm.facility_type_name, 
               ISNULL(U.userid, 0) AS No_of_user, 
               f.authority 
        FROM maslocations f
        INNER JOIN facility_type ft ON ft.facility_type_id = f.facility_type_id
        LEFT OUTER JOIN 
        (
            SELECT f.facility_type_id, f.facility_type_name FROM facility_type f
        ) fm ON fm.facility_type_id = f.facility_type_id
        LEFT OUTER JOIN 
        (
            SELECT COUNT(*) AS userid, l.facility_type_id 
            FROM users u 
            INNER JOIN maslocations l ON l.location_id = u.location_id
            INNER JOIN facility_type ft ON ft.facility_type_id = l.facility_type_id
            GROUP BY l.facility_type_id
        ) U ON U.facility_type_id = f.facility_type_id
        WHERE f.facility_type_id IS NOT NULL AND fm.facility_type_name IS NOT NULL
        GROUP BY f.facility_type_id, fm.facility_type_name, U.userid, f.authority
        ORDER BY f.facility_type_id";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        await conn.OpenAsync();

                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            while (await dr.ReadAsync())
                            {
                                var row = new FacilityUserListDto
                                {
                                    FacilityTypeId = Convert.ToInt32(dr["facility_type_id"]),

                                    // Aggregate Counts Parsing with Null checking safely
                                    NoOfConsignee = dr["no_of_consignee"] != DBNull.Value ? Convert.ToInt32(dr["no_of_consignee"]) : 0,
                                    NoOfUser = dr["No_of_user"] != DBNull.Value ? Convert.ToInt32(dr["No_of_user"]) : 0,

                                    // String handling mapping
                                    FacilityTypeName = dr["facility_type_name"]?.ToString() ?? string.Empty,

                                    // Int Column handles
                                    Authority = dr["authority"] != DBNull.Value ? Convert.ToInt32(dr["authority"]) : 0
                                };

                                reportList.Add(row);
                            }
                        }
                    }
                }

                // Return HTTP 200 OK with the array list data json
                return Ok(reportList);
            }
            catch (Exception ex)
            {
                // 500 Internal Server error framework alert protection handler
                return StatusCode(500, new { message = "Error pulling facility consignee report data.", error = ex.Message });
            }
        }


        [HttpGet("GetProgramFacilityList")]
        public async Task<IActionResult> GetProgramFacilityList()
        {
            var resultList = new List<ProgramFacilityReportDto>();
            string connString = _config.GetConnectionString("DefaultConnection");

            // Exact parameterized structure select query
            string sql = @"
        SELECT ProgramName, 
               f.facility_aut_code, 
               CONVERT(VARCHAR(10), m.CreatedOn, 103) AS Createdon 
        FROM MasProgram m
        LEFT OUTER JOIN facility_aut f ON f.facility_aut_id = m.directorateid";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        await conn.OpenAsync();

                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            while (await dr.ReadAsync())
                            {
                                var row = new ProgramFacilityReportDto
                                {
                                    ProgramName = dr["ProgramName"] != DBNull.Value ? dr["ProgramName"].ToString()! : string.Empty,
                                    FacilityAutCode = dr["facility_aut_code"] != DBNull.Value ? dr["facility_aut_code"].ToString()! : string.Empty,
                                    CreatedOn = dr["Createdon"] != DBNull.Value ? dr["Createdon"].ToString()! : string.Empty
                                };

                                resultList.Add(row);
                            }
                        }
                    }
                }

                // Return array payload inside HTTP 200 OK wrapper
                return Ok(resultList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error pulling program facility mapping report.", error = ex.Message });
            }
        }

     

        [HttpPost("SaveProgram")]
        public async Task<IActionResult> SaveProgram([FromBody] SaveProgramRequestDto request)
        {
            // 1. Validation Logic (Agar DTO ke [Required] tags automatically trigger na ho toh ye backup check hai)
            if (string.IsNullOrWhiteSpace(request.ProgramName))
            {
                return BadRequest(new { message = "Please fill Program Name" });
            }

            if (string.IsNullOrWhiteSpace(request.DirectorateId) || request.DirectorateId == "0")
            {
                return BadRequest(new { message = "Please Select Directorate Name" });
            }

            string connString = _config.GetConnectionString("DefaultConnection");

            // 2. SQL Query WITH Parameters (Anti-SQL Injection Security Fix)
            string sql = @"INSERT INTO MasProgram (ProgramName, CreatedOn, Directorateid) 
                       VALUES (@ProgramName, GETDATE(), @DirectorateId)";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        // Parameters bind kar rahe hain jo dynamic data handle karenge
                        cmd.Parameters.AddWithValue("@ProgramName", request.ProgramName.Trim());
                        cmd.Parameters.AddWithValue("@DirectorateId", request.DirectorateId);

                        await conn.OpenAsync();
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            // Web Forms ka: "Record Successfully Inserted" logic
                            return Ok(new { message = "Record Successfully Inserted" });
                        }
                        else
                        {
                            return BadRequest(new { message = "Failed to insert record." });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Internal System Exception safety handle
                return StatusCode(500, new { message = "Error occurred while saving program details.", error = ex.Message });
            }
        }

        // Dynamic dynamic route parameter pass hoga: api/Tenders/GetTenderList/ValidRC
        [HttpGet("GetTenderList/{rcTender}")]
        public async Task<IActionResult> GetTenderList(string rcTender)
        {
            var tenderList = new List<TenderDropdownDto>();
            string connString = _config.GetConnectionString("DefaultConnection");
            string sql = string.Empty;

            // WebForms framework conditions implementation logic checks
            if (string.Equals(rcTender, "RC", StringComparison.OrdinalIgnoreCase))
            {
                sql = @"SELECT DISTINCT t.tender_no, t.tender_id, t.tender_date 
                    FROM tenders t
                    INNER JOIN award_of_contract r ON r.tender_id = t.tender_id
                    ORDER BY t.tender_date DESC";
            }
            else if (string.Equals(rcTender, "ValidRC", StringComparison.OrdinalIgnoreCase))
            {
                // FIX: Puraane code me table alias 'ac' block crash thaa, use 'r' me resolve kiya hai
                sql = @"SELECT DISTINCT t.tender_no, t.tender_id, t.tender_date 
                    FROM tenders t
                    INNER JOIN award_of_contract r ON r.tender_id = t.tender_id
                    WHERE GETDATE() BETWEEN r.contract_date AND r.contract_end_date 
                    ORDER BY t.tender_date DESC";
            }
            else
            {
                sql = @"SELECT DISTINCT t.tender_no, t.tender_id, t.tender_date 
                    FROM tenders t 
                    ORDER BY t.tender_date DESC";
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        await conn.OpenAsync();

                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            while (await dr.ReadAsync())
                            {
                                tenderList.Add(new TenderDropdownDto
                                {
                                    TenderNo = dr["tender_no"] != DBNull.Value ? dr["tender_no"].ToString()! : string.Empty,
                                    TenderId = Convert.ToInt32(dr["tender_id"]),
                                    TenderDate = dr["tender_date"] != DBNull.Value ? Convert.ToDateTime(dr["tender_date"]) : null
                                });
                            }
                        }
                    }
                }

                return Ok(tenderList); // Returns structured list JSON code
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error pulling tenders dropdown lookup data grid.", error = ex.Message });
            }
        }

        [HttpPost("GetPoDashboardActual")]
        public async Task<IActionResult> GetPoDashboardActual([FromBody] PoDashboardRequestDto request)
        {
            var reportData = new List<PoDashboardResponseDto>();
            string connString = _config.GetConnectionString("DefaultConnection");

            // --- 1. DYNAMIC JOIN CLAUSE CONDITION LOGIC ---
            string joinDetails = " INNER JOIN ";
            if (request.StatusId == "InComplete" || request.StatusId == "0")
            {
                joinDetails = " LEFT OUTER JOIN ";
            }

            // --- 2. DYNAMIC FILTERS & PARAMETERS LOGIC ---
            string whClause = "";
            using (SqlConnection conn = new SqlConnection(connString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    if (request.YearId != "0" && !string.IsNullOrEmpty(request.YearId))
                    {
                        whClause += " AND a.FINANCIAL_YEAR_ID = @YearId ";
                        cmd.Parameters.AddWithValue("@YearId", Convert.ToInt32(request.YearId));
                    }

                    if (request.TenderId != "0" && !string.IsNullOrEmpty(request.TenderId))
                    {
                        whClause += " AND a.TENDER_ID = @TenderId ";
                        cmd.Parameters.AddWithValue("@TenderId", Convert.ToInt32(request.TenderId));
                    }

                    if (!string.IsNullOrEmpty(request.SupplierId) && request.SupplierId != "0")
                    {
                        // FIX: Puraane code ka bug "and  and a.supplier_id =" yahan theek kiya gaya hai
                        whClause += " AND a.supplier_id = @SupplierId ";
                        cmd.Parameters.AddWithValue("@SupplierId", Convert.ToInt32(request.SupplierId));
                    }

                    if (request.StatusId != "0" && !string.IsNullOrEmpty(request.StatusId))
                    {
                        whClause += " AND a.STATUS = @StatusId ";
                        cmd.Parameters.AddWithValue("@StatusId", request.StatusId);
                    }

                    // --- 3. CORE DYNAMIC MONOLITHIC SQL QUERY STRING ---
                    string sqlQuery = $@"
                    SELECT E.YEAR, a.OUTWARD_NO, a.PO_NO,
                           CONVERT(VARCHAR(10), (CASE WHEN a.soissueDT IS NULL THEN a.po_date ELSE a.soissueDT END), 103) AS po_date,
                           ISNULL(pi.po_value, 0) AS po_value, 
                           ISNULL(pi.total_po_value, 0) AS total_po_value, 
                           ISNULL(POQTY, 0) AS POQTY, 
                           ISNULL(nosConsignee, 0) AS po_items_key,
                           a.STATUS, a.REMARKS,
                           b.NAME AS SUPPLIER_NAME, b.mobile_no, B.email_id,
                           CASE WHEN ISNULL(a.Potype, 'NP') = 'NP' THEN 'Normal PO' ELSE 'Covid Po' END AS Potype,
                           ltp.filePathAccessories, ltp.filePathReagent,
                           CASE WHEN a.Ispayment = 'Y' THEN 'Paid' ELSE 'Not Paid' END AS Ispayment,
                           R.item_name AS ITEM_NAME, R.ITEM_CODE_AS_PER_TENDER AS CODE,
                           c.TENDER_NO,
                           a.TENDER_ID, a.SUPPLIER_ID, A.PO_ID, a.directorate_id, a.FINANCIAL_YEAR_ID, R.item_id
                    FROM PURCHASE_ORDER a
                    {joinDetails} MASSUPPLIERS b ON (a.SUPPLIER_ID = b.SUPPLIER_ID)
                    {joinDetails} TENDERS c ON (c.TENDER_ID = a.TENDER_ID)
                    {joinDetails} MAS_FINANCIAL_YEAR E ON (E.FINANCIAL_YEAR_ID = a.FINANCIAL_YEAR_ID)	
                    LEFT OUTER JOIN 
                    (
                        SELECT p.po_id, 
                               CAST(SUM(quantity) AS BIGINT) AS POQTY, 
                               CAST(SUM(totalprice) AS BIGINT) AS total_po_value, 
                               SUM(totalprice) AS po_value, 
                               p.item_id,
                               COUNT(p.consignee_id) AS nosConsignee
                        FROM po_items p
                        GROUP BY p.item_id, p.po_id
                    ) pi ON pi.po_id = a.po_id
                    {joinDetails} MASITEMS R ON (R.ITEM_ID = pi.ITEM_ID)
                    LEFT OUTER JOIN
                    (
                        SELECT tender_id, item_id, ti.tender_item_id FROM tender_items ti 
                    ) ti ON ti.tender_id = c.tender_id AND ti.item_id = r.item_id
                    LEFT OUTER JOIN 
                    (
                        SELECT ltp.filePathAccessories, ltp.filePathReagent, supplier_id, ltp.tender_item_id FROM live_tender_price ltp 
                    ) ltp ON ltp.supplier_id = a.supplier_id AND ltp.tender_item_id = ti.tender_item_id
                    WHERE 1=1 {whClause}
                    ORDER BY a.po_date DESC";

                    cmd.CommandText = sqlQuery;
                    cmd.Connection = conn;

                    await conn.OpenAsync();

                    using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                    {
                        while (await dr.ReadAsync())
                        {
                            reportData.Add(new PoDashboardResponseDto
                            {
                                Year = dr["YEAR"]?.ToString() ?? string.Empty,
                                OutwardNo = dr["OUTWARD_NO"]?.ToString() ?? string.Empty,
                                PoNo = dr["PO_NO"]?.ToString() ?? string.Empty,
                                PoDate = dr["po_date"]?.ToString() ?? string.Empty,

                                PoValue = dr["po_value"] != DBNull.Value ? Convert.ToDecimal(dr["po_value"]) : 0m,
                                TotalPoValue = dr["total_po_value"] != DBNull.Value ? Convert.ToInt64(dr["total_po_value"]) : 0,
                                PoQty = dr["POQTY"] != DBNull.Value ? Convert.ToInt64(dr["POQTY"]) : 0,
                                PoItemsKey = dr["po_items_key"] != DBNull.Value ? Convert.ToInt32(dr["po_items_key"]) : 0,

                                Status = dr["STATUS"]?.ToString() ?? string.Empty,
                                Remarks = dr["REMARKS"]?.ToString() ?? string.Empty,
                                SupplierName = dr["SUPPLIER_NAME"]?.ToString() ?? string.Empty,
                                MobileNo = dr["mobile_no"]?.ToString() ?? string.Empty,
                                EmailId = dr["email_id"]?.ToString() ?? string.Empty,
                                PoType = dr["Potype"]?.ToString() ?? string.Empty,
                                FilePathAccessories = dr["filePathAccessories"]?.ToString() ?? string.Empty,
                                FilePathReagent = dr["filePathReagent"]?.ToString() ?? string.Empty,
                                IsPayment = dr["Ispayment"]?.ToString() ?? string.Empty,
                                ItemName = dr["ITEM_NAME"]?.ToString() ?? string.Empty,
                                Code = dr["CODE"]?.ToString() ?? string.Empty,
                                TenderNo = dr["TENDER_NO"]?.ToString() ?? string.Empty,

                                TenderId = dr["TENDER_ID"] != DBNull.Value ? Convert.ToInt32(dr["TENDER_ID"]) : 0,
                                SupplierId = dr["SUPPLIER_ID"] != DBNull.Value ? Convert.ToInt32(dr["SUPPLIER_ID"]) : 0,
                                PoId = dr["PO_ID"] != DBNull.Value ? Convert.ToInt32(dr["PO_ID"]) : 0,
                                DirectorateId = dr["directorate_id"] != DBNull.Value ? Convert.ToInt32(dr["directorate_id"]) : 0,
                                FinancialYearId = dr["FINANCIAL_YEAR_ID"] != DBNull.Value ? Convert.ToInt32(dr["FINANCIAL_YEAR_ID"]) : 0,
                                ItemId = dr["item_id"] != DBNull.Value ? Convert.ToInt32(dr["item_id"]) : 0
                            });
                        }
                    }
                }
            }

            return Ok(reportData); // Angular compatible standard JSON payload return array
        }

        [HttpGet("GetActiveContractItemsReport")]
        public async Task<IActionResult> GetActiveContractItemsReport()
        {
            var activeContractsList = new List<ContractItemReportDto>();
            string connString = _config.GetConnectionString("DefaultConnection");

            // Parametrized query pattern preserve layout logic
            string sql = @"
            SELECT ci.item_id, 
                   m.item_name + '-' + m.item_code_as_per_tender + ' S-' + s.name + ' T-' + t.tender_no AS concatenated_item_name,
                   ci.single_unit_price, 
                   t.tender_no, 
                   t.tender_date, 
                   t.tender_id, 
                   ci.basic_rate, 
                   ci.percentage, 
                   m.item_code_as_per_tender, 
                   m.item_name AS core_item_name,
                   s.name AS supplier_name, 
                   s.email_id, 
                   s.mobile_no, 
                   ci.is_extended, 
                   CASE 
                       WHEN ci.is_extended = 'Y' THEN DATEADD(DAY, 1, ci.contract_new_end_date) 
                       ELSE DATEADD(DAY, 1, a.contract_end_date) 
                   END AS next_day_after_expiry
            FROM award_of_contract a
            INNER JOIN contract_items ci ON ci.award_of_contract_id = a.award_of_contract_id
            INNER JOIN masitems m ON m.item_id = ci.item_id
            INNER JOIN tenders t ON t.tender_id = a.tender_id
            INNER JOIN massuppliers s ON s.supplier_id = a.supplier_id
            WHERE GETDATE() BETWEEN a.contract_date AND 
                  (CASE 
                       WHEN ci.is_extended = 'Y' THEN DATEADD(DAY, 1, ci.contract_new_end_date) 
                       ELSE DATEADD(DAY, 1, a.contract_end_date) 
                   END)
              AND a.isfreezed IS NULL
            ORDER BY m.item_name";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        await conn.OpenAsync();

                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            while (await dr.ReadAsync())
                            {
                                activeContractsList.Add(new ContractItemReportDto
                                {
                                    ItemId = Convert.ToInt32(dr["item_id"]),
                                    ItemName = dr["concatenated_item_name"]?.ToString() ?? string.Empty,
                                    SingleUnitPrice = dr["single_unit_price"] != DBNull.Value ? Convert.ToDecimal(dr["single_unit_price"]) : 0m,
                                    TenderNo = dr["tender_no"]?.ToString() ?? string.Empty,
                                    TenderDate = dr["tender_date"] != DBNull.Value ? Convert.ToDateTime(dr["tender_date"]) : null,
                                    TenderId = Convert.ToInt32(dr["tender_id"]),
                                    BasicRate = dr["basic_rate"] != DBNull.Value ? Convert.ToDecimal(dr["basic_rate"]) : 0m,
                                    Percentage = dr["percentage"] != DBNull.Value ? Convert.ToDecimal(dr["percentage"]) : 0m,
                                    ItemCodeAsPerTender = dr["item_code_as_per_tender"]?.ToString() ?? string.Empty,
                                    CoreItemName = dr["core_item_name"]?.ToString() ?? string.Empty,
                                    SupplierName = dr["supplier_name"]?.ToString() ?? string.Empty,
                                    EmailId = dr["email_id"]?.ToString() ?? string.Empty,
                                    MobileNo = dr["mobile_no"]?.ToString() ?? string.Empty,
                                    IsExtended = dr["is_extended"]?.ToString() ?? string.Empty,
                                    NextDayAfterExpiry = dr["next_day_after_expiry"] != DBNull.Value ? Convert.ToDateTime(dr["next_day_after_expiry"]) : null
                                });
                            }
                        }
                    }
                }

                return Ok(activeContractsList); // Output returning array layout
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error pulling active contracts mapping matrices.", error = ex.Message });
            }
        }


        [HttpPost("GeneratePurchaseOrderActual")]
        public async Task<IActionResult> GeneratePurchaseOrderActual([FromBody] GeneratePoRequestDto request)
        {
            // --- 1. SEVER SIDE CONDITIONAL VALIDATIONS ENGINE ---
            int strFundId = 0;
            int dmeUserId = 0;

            if (request.AuthorityValue != "12")
            {
                strFundId = request.FundSourceValue;
                if (request.FundSourceSelectedIndex == 0)
                    return BadRequest(new { message = "Please Select Fund Source" });
            }
            else
            {
                strFundId = request.FundSourceValue;
                if (request.MedicalHospitalSelectedIndex == 0)
                    return BadRequest(new { message = "Please Select Medical College/Hospital" });

                dmeUserId = request.MedicalHospitalValue;
            }

            if (request.CovidPoSelectedIndex == 0)
                return BadRequest(new { message = "Please Select PO type" });

            string connString = _config.GetConnectionString("DefaultConnection");

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    await conn.OpenAsync();

                    // --- CONDITION A: IF EXIST PO_ID IS PASSED (FETCH STRATEGY) ---
                    if (request.PoId > 0)
                    {
                        string existingPoNo = string.Empty;
                        string fetchPoSql = "SELECT TOP(1) PO_NO FROM PURCHASE_ORDER WHERE PO_ID = @PoId";

                        using (SqlCommand fetchCmd = new SqlCommand(fetchPoSql, conn))
                        {
                            fetchCmd.Parameters.AddWithValue("@PoId", request.PoId);
                            var result = await fetchCmd.ExecuteScalarAsync();
                            existingPoNo = result != null ? result.ToString()! : "N/A";
                        }

                        return Ok(new
                        {
                            actionState = "FETCHED",
                            message = "Existing purchase order tracked successfully.",
                            poId = request.PoId,
                            poNo = existingPoNo,
                            directorateId = request.DirectorateId,
                            rcItemsValue = request.RcItemsSelectedValue,
                            dmeUserId = dmeUserId
                        });
                    }

                    // --- CONDITION B: GENERATE NEW PURCHASE ORDER TRANSACTION ---
                    if (request.SupplyDaysSelectedIndex != 0 && request.AuthoritySelectedIndex != 0 && request.FundSourceSelectedIndex != 0)
                    {
                        if (request.CovidPoValue.ToString() != "0")
                        {
                            if (!DateTime.TryParse(request.PoDateStr, out DateTime parsedPoDate))
                            {
                                return BadRequest(new { message = "Invalid Purchase Order Date structure format." });
                            }

                            // WebForms layout dynamic helper: genPO(year) emulation setup
                            int temporarySequenceNo = 101;
                            string fetchSeqSql = "SELECT ISNULL(MAX(PO_ID), 0) + 1 FROM PURCHASE_ORDER WHERE FINANCIAL_YEAR_ID = @FinId";
                            using (SqlCommand seqCmd = new SqlCommand(fetchSeqSql, conn))
                            {
                                seqCmd.Parameters.AddWithValue("@FinId", request.FinancialYearId);
                                temporarySequenceNo = Convert.ToInt32(await seqCmd.ExecuteScalarAsync());
                            }

                            // Generate precise string layout tracking rule: EQP/Sequence/TextYear
                            string generatedPoNo = $"EQP/{temporarySequenceNo}/{request.FinancialYearText}";

                            // Safe SQL Parameterized command instantiation block
                            string insertSql = @"INSERT INTO PURCHASE_ORDER 
                                            (TENDER_ID, PO_DATE, SUPPLIER_ID, STATUS, FINANCIAL_YEAR_ID, PO_NO, can_flag, directorate_id, Potype, fund_id, DMEUserid, programid, GeMBidno) 
                                             VALUES 
                                            (@TENDER_ID, @PO_DATE, @SUPPLIER_ID, @STATUS, @FINANCIAL_YEAR_ID, @PO_NO, 'F', @Dir, @Ptype, @fundid, @DMEUserid, @Programid, @GemPO);
                                             SELECT SCOPE_IDENTITY();";

                            using (SqlCommand cmd = new SqlCommand(insertSql, conn))
                            {
                                cmd.Parameters.AddWithValue("@TENDER_ID", request.TenderId);
                                cmd.Parameters.AddWithValue("@PO_DATE", parsedPoDate);
                                cmd.Parameters.AddWithValue("@SUPPLIER_ID", request.SupplierId);
                                cmd.Parameters.AddWithValue("@STATUS", "InComplete");
                                cmd.Parameters.AddWithValue("@FINANCIAL_YEAR_ID", request.FinancialYearId);
                                cmd.Parameters.AddWithValue("@PO_NO", generatedPoNo);
                                cmd.Parameters.AddWithValue("@Dir", request.DirectorateId);
                                cmd.Parameters.AddWithValue("@Ptype", request.CovidPoValue.ToString());
                                cmd.Parameters.AddWithValue("@fundid", strFundId);
                                cmd.Parameters.AddWithValue("@DMEUserid", dmeUserId);
                                cmd.Parameters.AddWithValue("@Programid", request.ProgramId);

                                if (!string.IsNullOrWhiteSpace(request.GemPoText))
                                    cmd.Parameters.AddWithValue("@GemPO", request.GemPoText.Trim());
                                else
                                    cmd.Parameters.Add("@GemPO", SqlDbType.VarChar).Value = DBNull.Value;

                                int newGeneratedPoId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                                return Ok(new
                                {
                                    actionState = "GENERATED",
                                    message = "Purchase Order generated and saved successfully.",
                                    poId = newGeneratedPoId,
                                    poNo = generatedPoNo,
                                    directorateId = request.DirectorateId,
                                    rcItemsValue = request.RcItemsSelectedValue,
                                    dmeUserId = dmeUserId
                                });
                            }
                        }
                        else
                        {
                            return BadRequest(new { message = "Please select Po Type" });
                        }
                    }
                    else
                    {
                        return BadRequest(new { message = "Please select Authorities/Supply Days/Fund Source" });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error processing purchase order validation engine loops.", error = ex.Message });
            }
        }

        [HttpGet("GetProgramsByDirectorate/{dirId}")]
        public async Task<IActionResult> GetProgramsByDirectorate(int dirId)
        {
            // Fail-safe validation check parameters boundaries
            //if (dirId <= 0)
            //{
            //    return BadRequest(new { message = "Invalid Directorate Identity Context ID." });
            //}

            var programList = new List<ProgramDropdownDto>();
            string connString = _config.GetConnectionString("DefaultConnection");

            // FIX: Structural dynamic JOIN parameters applied instead of raw concatenation strings
            string sql = @" select distinct ProgramID, ProgramName,f.facility_aut_code from MasProgram m left outer join facility_aut f on f.facility_aut_id =@DirId";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        // Adding strong typed parameter bindings anti-injection
                        cmd.Parameters.AddWithValue("@DirId", dirId);

                        await conn.OpenAsync();

                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            while (await dr.ReadAsync())
                            {
                                programList.Add(new ProgramDropdownDto
                                {
                                    ProgramId = Convert.ToInt32(dr["ProgramID"]),
                                    ProgramName = dr["ProgramName"] != DBNull.Value ? dr["ProgramName"].ToString()! : string.Empty,
                                    FacilityAutCode = dr["facility_aut_code"] != DBNull.Value ? dr["facility_aut_code"].ToString()! : string.Empty
                                });
                            }
                        }
                    }
                }

                return Ok(programList); // Returns safe array structured payload
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error pulling program metadata lists lookup fields.", error = ex.Message });
            }
        }
        // Dynamic Route Parameter: api/ContractSupply/GetSupplyDaysReport/2441
        [HttpGet("GetSupplyDaysReport/{itemId}")]
        public async Task<IActionResult> GetSupplyDaysReport(int itemId)
        {
            // Fail-safe validation check parameters boundaries
            if (itemId <= 0)
            {
                return BadRequest(new { message = "Invalid Item Identity Context ID." });
            }

            var resultList = new List<SupplyDaysReportDto>();
            string connString = _config.GetConnectionString("DefaultConnection");

            // Exact parameterized structure select query with UNION ALL
            string sql = @"
            SELECT t.domestic_days AS daystaken, 
                   ci.is_extended, 
                   CASE WHEN ci.is_extended = 'Y' THEN DATEADD(DAY, 1, ci.contract_new_end_date) ELSE DATEADD(DAY, 1, a.contract_end_date) END AS calculated_end_date
            FROM award_of_contract a
            INNER JOIN contract_items ci ON ci.award_of_contract_id = a.award_of_contract_id
            INNER JOIN masitems m ON m.item_id = ci.item_id
            INNER JOIN tenders t ON t.tender_id = a.tender_id
            INNER JOIN massuppliers s ON s.supplier_id = a.supplier_id
            WHERE GETDATE() BETWEEN a.contract_date AND (CASE WHEN ci.is_extended = 'Y' THEN DATEADD(DAY, 1, ci.contract_new_end_date) ELSE DATEADD(DAY, 1, a.contract_end_date) END)
              AND ci.item_id = @ItemId

            UNION ALL

            SELECT t.import_days AS daystaken, 
                   ci.is_extended, 
                   CASE WHEN ci.is_extended = 'Y' THEN DATEADD(DAY, 1, ci.contract_new_end_date) ELSE DATEADD(DAY, 1, a.contract_end_date) END AS calculated_end_date
            FROM award_of_contract a
            INNER JOIN contract_items ci ON ci.award_of_contract_id = a.award_of_contract_id
            INNER JOIN masitems m ON m.item_id = ci.item_id
            INNER JOIN tenders t ON t.tender_id = a.tender_id
            INNER JOIN massuppliers s ON s.supplier_id = a.supplier_id
            WHERE GETDATE() BETWEEN a.contract_date AND (CASE WHEN ci.is_extended = 'Y' THEN DATEADD(DAY, 1, ci.contract_new_end_date) ELSE DATEADD(DAY, 1, a.contract_end_date) END)
              AND ci.item_id = @ItemId";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        // Adding strong typed parameter bindings anti-injection
                        cmd.Parameters.AddWithValue("@ItemId", itemId);

                        await conn.OpenAsync();

                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            while (await dr.ReadAsync())
                            {
                                resultList.Add(new SupplyDaysReportDto
                                {
                                    DaysTaken = dr["daystaken"] != DBNull.Value ? Convert.ToInt32(dr["daystaken"]) : 0,
                                    IsExtended = dr["is_extended"]?.ToString() ?? "N",
                                    CalculatedEndDate = dr["calculated_end_date"] != DBNull.Value ? Convert.ToDateTime(dr["calculated_end_date"]) : null
                                });
                            }
                        }
                    }
                }

                return Ok(resultList); // Returns safe array structured payload
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error pulling supply days report lists.", error = ex.Message });
            }
        }
        [HttpPost("ShowMappedFunds")]
        public async Task<IActionResult> ShowMappedFunds([FromBody] MappedFundsRequestDto request)
        {
            var mappedFundsList = new List<MappedFundsResponseDto>();
            string connString = _config.GetConnectionString("DefaultConnection");

            // --- DYNAMIC FILTERS PARSING SAFETY ---
            string whFacilityAuthId = "";

            using (SqlConnection conn = new SqlConnection(connString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    // WebForms dynamic overrides execution criteria blocks mirroring
                    if (!string.IsNullOrEmpty(request.DmeUserId) && request.DmeUserId != "0")
                    {
                        if (!string.IsNullOrEmpty(request.FacilityAutId))
                        {
                            whFacilityAuthId = " AND f.facility_aut_id = @FacilityAutId AND m.DMEUserid = @DmeUserId ";
                            cmd.Parameters.AddWithValue("@FacilityAutId", Convert.ToInt32(request.FacilityAutId));
                            cmd.Parameters.AddWithValue("@DmeUserId", Convert.ToInt32(request.DmeUserId));
                        }
                    }
                    else if (!string.IsNullOrEmpty(request.FacilityAutId))
                    {
                        whFacilityAuthId = " AND f.facility_aut_id = @FacilityAutId ";
                        cmd.Parameters.AddWithValue("@FacilityAutId", Convert.ToInt32(request.FacilityAutId));
                    }

                    // Core parameterized monolithic raw string query string layout
                    string sqlQuery = $@"
                    SELECT f.facility_aut_name, 
                           b.BUDGETNAME, 
                           m.MapId, 
                           b.BUDGETID, 
                           f.facility_aut_id, 
                           m.DMEUserid,
                           ISNULL(u.user_name, '-') AS DMEUserName 
                    FROM MasBudgetMap m
                    INNER JOIN MASBUDGET b ON b.BUDGETID = m.budgetid
                    INNER JOIN facility_aut f ON f.facility_aut_id = m.facility_aut_id
                    LEFT OUTER JOIN users u ON u.user_id = m.DMEUserid
                    WHERE 1=1 {whFacilityAuthId} 
                    ORDER BY b.BUDGETNAME";

                    cmd.CommandText = sqlQuery;
                    cmd.Connection = conn;

                    await conn.OpenAsync();

                    using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                    {
                        while (await dr.ReadAsync())
                        {
                            mappedFundsList.Add(new MappedFundsResponseDto
                            {
                                FacilityAutName = dr["facility_aut_name"]?.ToString() ?? string.Empty,
                                BudgetName = dr["BUDGETNAME"]?.ToString() ?? string.Empty,
                                MapId = Convert.ToInt32(dr["MapId"]),
                                BudgetId = Convert.ToInt32(dr["BUDGETID"]),
                                FacilityAutId = Convert.ToInt32(dr["facility_aut_id"]),

                                // Handling optional reference foreign key indices safely
                                DmeUserId = dr["DMEUserid"] != DBNull.Value ? Convert.ToInt32(dr["DMEUserid"]) : null,
                                DmeUserName = dr["DMEUserName"]?.ToString() ?? string.Empty
                            });
                        }
                    }
                }
            }

            return Ok(mappedFundsList); // Returns safe array JSON data mapping framework models
        }

        // Dynamic Route Parameter: api/AwardOfContract/GetContractsByTender/169
        [HttpGet("GetContractsByTender/{tenderId}")]
        public async Task<IActionResult> GetContractsByTender(int tenderId)
        {
            // Fail-safe validation parameter validation boundaries
            if (tenderId <= 0)
            {
                return BadRequest(new { message = "Invalid Tender Identity Context ID." });
            }

            var contractList = new List<AwardContractDropdownDto>();
            string connString = _config.GetConnectionString("DefaultConnection");

            // Exact parameterized structure select query (Anti-SQL Injection security)
            string sql = @"SELECT ac.award_of_contract_id, 
                              s.name + ' (' + ac.contract_number + ')' AS concatenated_contract_number
                       FROM award_of_contract ac 
                       INNER JOIN massuppliers s ON s.supplier_id = ac.supplier_id
                       WHERE ac.tender_id = @TenderId
                       ORDER BY s.name";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        // Adding strong typed parameter bindings anti-injection
                        cmd.Parameters.AddWithValue("@TenderId", tenderId);

                        await conn.OpenAsync();

                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            while (await dr.ReadAsync())
                            {
                                contractList.Add(new AwardContractDropdownDto
                                {
                                    AwardOfContractId = Convert.ToInt32(dr["award_of_contract_id"]),
                                    ContractNumber = dr["concatenated_contract_number"] != DBNull.Value
                                        ? dr["concatenated_contract_number"].ToString()!
                                        : string.Empty
                                });
                            }
                        }
                    }
                }

                return Ok(contractList); // Returns safe array JSON data mapping models
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error pulling award of contract dropdown lookup fields.", error = ex.Message });
            }
        }


        [HttpGet("GetContractGridDetail")]
        public async Task<IActionResult> GetContractGridDetail([FromQuery] int tenderId, [FromQuery] int awardOfContractId)
        {
            if (tenderId <= 0 || awardOfContractId <= 0)
            {
                return BadRequest(new { message = "Invalid input query parameters mapped contexts." });
            }

            var gridList = new List<ContractGridDetailDto>();
            string connString = _config.GetConnectionString("DefaultConnection");

            string sql = @"
            SELECT ac.award_of_contract_id, c.contract_item_id, m.item_id, 
                   m.item_code_as_per_tender AS item_codeE, m.item_name AS item_nameE,
                   c.basic_rate, c.percentage, c.single_unit_price, c.model, 
                   CONVERT(VARCHAR, ac.contract_date, 103) AS contract_date, 
                   ac.contract_duration, 
                   CONVERT(VARCHAR, ac.contract_end_date, 103) AS contract_end_date,
                   s.name AS supplier_name, t.tender_no, t.tender_id, 
                   CONVERT(VARCHAR, c.contract_new_end_date, 105) AS contract_new_end_date, 
                   c.remark
            FROM contract_items c
            INNER JOIN masitems m ON m.item_id = c.item_id
            INNER JOIN award_of_contract ac ON ac.award_of_contract_id = c.award_of_contract_id
            INNER JOIN massuppliers s ON s.supplier_id = ac.supplier_id
            INNER JOIN tenders t ON t.tender_id = ac.tender_id
            WHERE ac.status = 'C' 
              AND t.tender_id = @TenderId 
              AND ac.award_of_contract_id = @AwardOfContractId 
            ORDER BY ac.contract_date DESC";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@TenderId", tenderId);
                        cmd.Parameters.AddWithValue("@AwardOfContractId", awardOfContractId);

                        await conn.OpenAsync();
                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            while (await dr.ReadAsync())
                            {
                                gridList.Add(new ContractGridDetailDto
                                {
                                    AwardOfContractId = Convert.ToInt32(dr["award_of_contract_id"]),
                                    ContractItemId = Convert.ToInt32(dr["contract_item_id"]),
                                    ItemId = Convert.ToInt32(dr["item_id"]),
                                    ItemCodeE = dr["item_codeE"]?.ToString() ?? string.Empty,
                                    ItemNameE = dr["item_nameE"]?.ToString() ?? string.Empty,
                                    BasicRate = dr["basic_rate"] != DBNull.Value ? Convert.ToDecimal(dr["basic_rate"]) : 0m,
                                    Percentage = dr["percentage"] != DBNull.Value ? Convert.ToDecimal(dr["percentage"]) : 0m,
                                    SingleUnitPrice = dr["single_unit_price"] != DBNull.Value ? Convert.ToDecimal(dr["single_unit_price"]) : 0m,
                                    Model = dr["model"]?.ToString() ?? string.Empty,
                                    ContractDate = dr["contract_date"]?.ToString() ?? string.Empty,
                                    ContractDuration = dr["contract_duration"]?.ToString() ?? string.Empty,
                                    ContractEndDate = dr["contract_end_date"]?.ToString() ?? string.Empty,
                                    SupplierName = dr["supplier_name"]?.ToString() ?? string.Empty,
                                    TenderNo = dr["tender_no"]?.ToString() ?? string.Empty,
                                    TenderId = Convert.ToInt32(dr["tender_id"]),
                                    ContractNewEndDate = dr["contract_new_end_date"]?.ToString() ?? string.Empty,
                                    Remark = dr["remark"]?.ToString() ?? string.Empty
                                });
                            }
                        }
                    }
                }

                // WebForms rowCount pattern: Return 200 OK along with records array data lists
                return Ok(gridList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading contract details report grid.", error = ex.Message });
            }
        }

        // ==========================================
        // 2. REPLACEMENT FOR: isContractAlreadyExtended()
        // ==========================================
        // Route: GET api/ContractExtension/CheckIfAlreadyExtended/45
        [HttpGet("CheckIfAlreadyExtended/{awardOfContractId}")]
        public async Task<IActionResult> CheckIfAlreadyExtended(int awardOfContractId)
        {
            bool isContractExtend = false;
            string connString = _config.GetConnectionString("DefaultConnection");
            string sql = "SELECT COUNT(1) FROM award_of_contract WHERE is_extended = 'Y' AND award_of_contract_id = @AocId";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@AocId", awardOfContractId);
                        await conn.OpenAsync();

                        int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        if (count > 0)
                        {
                            isContractExtend = true;
                        }
                    }
                }

                return Ok(new { awardOfContractId = awardOfContractId, isExtended = isContractExtend });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error validation contract check status loop.", error = ex.Message });
            }
        }

        // ==========================================
        // 3. REPLACEMENT FOR: populatePnlContractExtend()
        // ==========================================
        // Route: GET api/ContractExtension/GetPanelExtensionInfo/45
        [HttpGet("GetPanelExtensionInfo/{awardOfContractId}")]
        public async Task<IActionResult> GetPanelExtensionInfo(int awardOfContractId)
        {
            string connString = _config.GetConnectionString("DefaultConnection");
            string sql = @"SELECT CONVERT(VARCHAR, aoc.contract_end_date, 105) AS contract_end_date, aoc.remark 
                       FROM award_of_contract aoc
                       WHERE aoc.award_of_contract_id = @AocId";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@AocId", awardOfContractId);
                        await conn.OpenAsync();

                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            if (await dr.ReadAsync())
                            {
                                var info = new ContractPanelInfoDto
                                {
                                    ContractEndDate = dr["contract_end_date"] != DBNull.Value ? dr["contract_end_date"].ToString()!.Trim() : string.Empty,
                                    Remark = dr["remark"] != DBNull.Value ? dr["remark"].ToString()!.Trim() : string.Empty
                                };

                                return Ok(info); // Returns structural single matching JSON record
                            }
                        }
                    }
                }

                return NotFound(new { message = "No matching award of contract record tracking indices found." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error pulling contract panels extension metrics.", error = ex.Message });
            }
        }



        [HttpPost("ExtendContractTransaction")]
        public async Task<IActionResult> ExtendContractTransaction([FromBody] ContractExtendRequestDto request)
        {
            // 1. Basic Server Side Validation Checks
            if (request.AwardOfContractId <= 0 || string.IsNullOrWhiteSpace(request.ContractNewEndDate))
            {
                return BadRequest(new { message = "Invalid Contract Identity or New End Date." });
            }

            string connString = _config.GetConnectionString("DefaultConnection");

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    await conn.OpenAsync();

                    // 2. Fetch Previous End Date Logic: getContractEndDate(id)
                    string contractPreviousEndDate = string.Empty;
                    string fetchPreviousDateSql = "SELECT CONVERT(VARCHAR, contract_end_date, 103) FROM award_of_contract WHERE award_of_contract_id = @AocId";

                    using (SqlCommand fetchCmd = new SqlCommand(fetchPreviousDateSql, conn))
                    {
                        fetchCmd.Parameters.AddWithValue("@AocId", request.AwardOfContractId);
                        var result = await fetchCmd.ExecuteScalarAsync();

                        if (result == null || result == DBNull.Value)
                        {
                            return BadRequest(new { message = "Contract Previous End Date Not Found !!!" });
                        }
                        contractPreviousEndDate = result.ToString()!;
                    }

                    // 3. Start SQL Transaction for Database Atomicity Safety (All or Nothing)
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // --- QUERY 1: Update previous end date context ---
                            string updatePrvEndDtSql = @"UPDATE award_of_contract 
                                                     SET contract_previous_end_date = CONVERT(DATE, @PrvEndDate, 103) 
                                                     WHERE award_of_contract_id = @AocId";

                            using (SqlCommand cmd1 = new SqlCommand(updatePrvEndDtSql, conn, transaction))
                            {
                                cmd1.Parameters.AddWithValue("@PrvEndDate", contractPreviousEndDate);
                                cmd1.Parameters.AddWithValue("@AocId", request.AwardOfContractId);
                                await cmd1.ExecuteNonQueryAsync();
                            }

                            // --- QUERY 2: Update current contract tracking fields ---
                            string updateQrySql = @"UPDATE award_of_contract 
                                                SET contract_end_date = CONVERT(DATE, @NewEndDate, 103), 
                                                    remark = @Remark, 
                                                    is_extended = 'Y' 
                                                WHERE award_of_contract_id = @AocId";

                            using (SqlCommand cmd2 = new SqlCommand(updateQrySql, conn, transaction))
                            {
                                cmd2.Parameters.AddWithValue("@NewEndDate", request.ContractNewEndDate.Trim());
                                cmd2.Parameters.AddWithValue("@Remark", request.Remark?.Trim() ?? (object)DBNull.Value);
                                cmd2.Parameters.AddWithValue("@AocId", request.AwardOfContractId);
                                await cmd2.ExecuteNonQueryAsync();
                            }

                            // --- QUERY 3: Cascade update downstream contract items row maps ---
                            string updateCISql = @"UPDATE contract_items 
                                               SET contract_new_end_date = CONVERT(DATE, @NewEndDate, 103), 
                                                   remark = @Remark, 
                                                   is_extended = 'Y' 
                                               WHERE award_of_contract_id = @AocId";

                            using (SqlCommand cmd3 = new SqlCommand(updateCISql, conn, transaction))
                            {
                                cmd3.Parameters.AddWithValue("@NewEndDate", request.ContractNewEndDate.Trim());
                                cmd3.Parameters.AddWithValue("@Remark", request.Remark?.Trim() ?? (object)DBNull.Value);
                                cmd3.Parameters.AddWithValue("@AocId", request.AwardOfContractId);
                                await cmd3.ExecuteNonQueryAsync();
                            }

                            // Commit all 3 updates securely if no execution blocks crash
                            await transaction.CommitAsync();

                            return Ok(new { message = "Contract Successfully Extended." });
                        }
                        catch (Exception txEx)
                        {
                            // Rollback transaction state to avoid data corruption if any query fails
                            await transaction.RollbackAsync();
                            throw new Exception("Transaction execution failed inside update blocks.", txEx);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error occurred while executing contract extension transaction.", error = ex.Message });
            }
        }

        // Target Endpoint Route URL: GET api/PoExtension/GetExtensionReportList/applied
        [HttpGet("GetExtensionReportList/{statusType}")]
        public async Task<IActionResult> GetExtensionReportList(string statusType)
        {
            var resultList = new List<PoExtensionReportDto>();
            string connString = _config.GetConnectionString("DefaultConnection");

            // --- 1. PARSE RADIO BUTTON STATUS CODES TO INTERNAL IDs ---
            var statusIds = new List<string>();
            if (statusType.Equals("applied", StringComparison.OrdinalIgnoreCase))
            {
                statusIds.Add("P");
            }
            else if (statusType.Equals("approved", StringComparison.OrdinalIgnoreCase))
            {
                statusIds.Add("A");
                statusIds.Add("E");
            }
            else if (statusType.Equals("rejected", StringComparison.OrdinalIgnoreCase))
            {
                statusIds.Add("R");
            }
            else
            {
                return BadRequest(new { message = "Invalid Radio Filter Tab criteria status selection type." });
            }

            // --- 2. DYNAMIC PARAMETERIZED IN CLAUSE BUILDER ---
            var paramNames = new List<string>();
            using (SqlConnection conn = new SqlConnection(connString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    for (int i = 0; i < statusIds.Count; i++)
                    {
                        string paramName = $"@StatusId{i}";
                        cmd.Parameters.AddWithValue(paramName, statusIds[i]);
                        paramNames.Add(paramName);
                    }

                    // Inject dynamic secure parameter collection tags inside SQL statement structure
                    string inClauseCondition = string.Join(",", paramNames);

                    // Monolithic secure nested database select report script expression
                    string sqlQuery = $@"
                    SELECT SUPPLIER_ID, SUPPLIER_NAME, posu.item_id, posu.PO_ID, posu.CODE,
                           posu.ITEM_NAME, posu.OUTWARD_NO, CONVERT(VARCHAR, posu.po_date, 103) AS po_date, posu.PO_NO, posu.quantity, posu.no_of_consignee,
                           posu.basic_rate, posu.percentage, posu.single_unit_price, posu.totalPOvalue, posu.tender_no, posu.status, posu.SD,
                           pDet.SubmissionStatus, tranche_days, CONVERT(VARCHAR, DATEADD(DAY, tranche_days, po_date), 103) AS po_end_date,
                           ltr.id, ltr.extensionId, ltr.remark, ltr.days, ltr.extended_date,
                           ltr.last_po_end_date, ltr.path, ltr.letter_date, ltr.letter_no,
                           ltr.sys_gen_apply_date, ltr.letterStatus
                    FROM
                    (
                        SELECT a.item_id, PO_ID, CODE, ITEM_NAME, OUTWARD_NO, po_date, PO_NO, 
                               SUM(quantity) AS quantity, COUNT(location_id) AS no_of_consignee,
                               basic_rate, percentage, single_unit_price, SUM(totalPOvalue) AS totalPOvalue, 
                               tender_no, status, CASE WHEN sdname IS NOT NULL THEN sdname ELSE 'Not Submitted' END AS SD,
                               tranche_days, SUPPLIER_NAME, SUPPLIER_ID
                        FROM 
                        (
                            SELECT m.location_id, m.location_name, m.DP_DistrictID, R.ITEM_CODE_AS_PER_TENDER AS CODE, R.item_name AS ITEM_NAME, p.OUTWARD_NO, 
                                   p.po_date, pi.quantity, c.basic_rate, c.percentage, c.single_unit_price, (c.single_unit_price * pi.quantity) AS totalPOvalue,
                                   b.NAME AS SUPPLIER_NAME, b.mobile_no, c.tender_no, CONVERT(VARCHAR, c.tender_date, 103) AS tender_date, p.STATUS,
                                   p.REMARKS, pi.item_id, p.FINANCIAL_YEAR_ID, E.YEAR, p.APPROVED_BY, p.total_po_value, p.po_value, p.TENDER_ID, p.PO_NO, p.SUPPLIER_ID,
                                   p.directorate_id, p.indent_fund_id, p.PO_ID, sd.sdname, PT.tranche_days
                            FROM po_items pi 
                            INNER JOIN MASITEMS R ON (R.ITEM_ID = pi.item_id)
                            INNER JOIN maslocations m ON m.location_id = pi.consignee_id
                            INNER JOIN purchase_order p ON pi.po_id = p.po_id
                            INNER JOIN MASSUPPLIERS b ON (p.supplier_id = b.SUPPLIER_ID)
                            INNER JOIN MAS_FINANCIAL_YEAR E ON (E.FINANCIAL_YEAR_ID = p.FINANCIAL_YEAR_ID)
                            INNER JOIN po_tranche PT ON PT.po_id = p.po_id
                            LEFT OUTER JOIN 
                            (
                                SELECT a.supplier_id, a.tender_id, ci.item_id, ci.basic_rate, ci.percentage, ci.single_unit_price, t.tender_no, t.tender_date
                                FROM award_of_contract a 
                                INNER JOIN contract_items ci ON ci.award_of_contract_id = a.award_of_contract_id
                                INNER JOIN tenders t ON t.tender_id = a.tender_id
                            ) c ON c.item_id = pi.item_id AND c.tender_id = p.tender_id AND c.supplier_id = p.supplier_id
                            LEFT OUTER JOIN 
                            (
                                SELECT s.sdname, ps.po_id 
                                FROM PO_Sddetails ps
                                INNER JOIN massd s ON s.SDMode = ps.SDMode
                                WHERE SubmissionStatus = 'Y'
                            ) sd ON sd.po_id = p.po_id    
                            LEFT OUTER JOIN users u ON u.user_id = m.user_id
                            WHERE p.status IN ('Order Placed', 'Partially Received', 'Completed')
                        ) a 
                        GROUP BY sdname, PO_ID, ITEM_NAME, OUTWARD_NO, po_date, PO_NO, CODE, basic_rate, percentage, single_unit_price, tender_no, status, item_id, tranche_days, SUPPLIER_NAME, SUPPLIER_ID
                    ) posu
                    LEFT OUTER JOIN PO_SDDetails pDet ON pDet.po_id = posu.po_id 
                    INNER JOIN 
                    (
                        SELECT s.id, ped.extensionId, ped.po_id, ped.remark, ped.days, CONVERT(VARCHAR, ped.extended_date, 105) AS extended_date,
                               CONVERT(VARCHAR, ped.po_end_date, 105) AS last_po_end_date, ped.path, ped.letter_date, letter_no,
                               CONVERT(VARCHAR, ped.sys_gen_apply_date, 105) AS sys_gen_apply_date, s.status AS letterStatus
                        FROM PO_extension_detail ped 
                        INNER JOIN master_po_extension_detail_status s ON ped.status = s.id
                    ) ltr ON ltr.po_id = posu.po_id
                    WHERE ltr.id IN ({inClauseCondition})
                    ORDER BY posu.po_date DESC";

                    cmd.CommandText = sqlQuery;
                    cmd.Connection = conn;

                    await conn.OpenAsync();
                    using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                    {
                        while (await dr.ReadAsync())
                        {
                            resultList.Add(new PoExtensionReportDto
                            {
                                SupplierId = Convert.ToInt32(dr["SUPPLIER_ID"]),
                                SupplierName = dr["SUPPLIER_NAME"]?.ToString() ?? string.Empty,
                                ItemId = Convert.ToInt32(dr["item_id"]),
                                PoId = Convert.ToInt32(dr["PO_ID"]),
                                Code = dr["CODE"]?.ToString() ?? string.Empty,
                                ItemName = dr["ITEM_NAME"]?.ToString() ?? string.Empty,
                                OutwardNo = dr["OUTWARD_NO"]?.ToString() ?? string.Empty,
                                PoDate = dr["po_date"]?.ToString() ?? string.Empty,
                                PoNo = dr["PO_NO"]?.ToString() ?? string.Empty,
                                Quantity = dr["quantity"] != DBNull.Value ? Convert.ToDecimal(dr["quantity"]) : 0m,
                                NoOfConsignee = Convert.ToInt32(dr["no_of_consignee"]),
                                BasicRate = dr["basic_rate"] != DBNull.Value ? Convert.ToDecimal(dr["basic_rate"]) : 0m,
                                Percentage = dr["percentage"] != DBNull.Value ? Convert.ToDecimal(dr["percentage"]) : 0m,
                                SingleUnitPrice = dr["single_unit_price"] != DBNull.Value ? Convert.ToDecimal(dr["single_unit_price"]) : 0m,
                                TotalPoValue = dr["totalPOvalue"] != DBNull.Value ? Convert.ToDecimal(dr["totalPOvalue"]) : 0m,
                                TenderNo = dr["tender_no"]?.ToString() ?? string.Empty,
                                Status = dr["status"]?.ToString() ?? string.Empty,
                                Sd = dr["SD"]?.ToString() ?? string.Empty,
                                SubmissionStatus = dr["SubmissionStatus"]?.ToString() ?? string.Empty,
                                TrancheDays = dr["tranche_days"] != DBNull.Value ? Convert.ToInt32(dr["tranche_days"]) : 0,
                                PoEndDate = dr["po_end_date"]?.ToString() ?? string.Empty,
                                LetterId = dr["id"] != DBNull.Value ? dr["id"].ToString()!.Trim() : string.Empty,
                                //LetterId = Convert.ToInt32(dr["id"]),
                                ExtensionId = Convert.ToInt32(dr["extensionId"]),
                                Remark = dr["remark"]?.ToString() ?? string.Empty,
                                Days = dr["days"] != DBNull.Value ? Convert.ToInt32(dr["days"]) : 0,
                                ExtendedDate = dr["extended_date"]?.ToString() ?? string.Empty,
                                LastPoEndDate = dr["last_po_end_date"]?.ToString() ?? string.Empty,
                                Path = dr["path"]?.ToString() ?? string.Empty,
                                LetterDate = dr["letter_date"]?.ToString() ?? string.Empty,
                                LetterNo = dr["letter_no"]?.ToString() ?? string.Empty,
                                SysGenApplyDate = dr["sys_gen_apply_date"]?.ToString() ?? string.Empty,
                                LetterStatus = dr["letterStatus"]?.ToString() ?? string.Empty
                            });
                        }
                    }
                }
            }

            return Ok(resultList);
        }
        // Dynamic Route Parameter Route Context: GET api/Indent/GetConsolidationReport?yearId=19&directorateId=12
        [HttpGet("GetConsolidationReport")]
        public async Task<IActionResult> GetConsolidationReport([FromQuery] int yearId, [FromQuery] int directorateId)
        {
            // Fail-safe basic validation check boundary filters
            if (yearId <= 0 || directorateId <= 0)
            {
                return BadRequest(new { message = "Please provide valid Financial Year ID and Directorate ID parameters." });
            }

            var consolidationList = new List<IndentConsolidationReportDto>();
            string connString = _config.GetConnectionString("DefaultConnection");

            // Exact secure parameterized query execution layout
            string sql = @"
            SELECT COUNT(B.item_id) AS Equipmentcount, 
                   A.INDENT_CONSOLIDATION_ID, 
                   A.description, 
                   A.USER_ID, 
                   A.DIRECTORATE_ID, 
                   A.FINANCIAL_YEAR_ID, 
                   ISNULL(SUM(B.PROPOSED_QTY), 0) AS PROPOSED_QTY, 
                   A.indent_con_no,
                   CONVERT(VARCHAR(10), A.CONSOLIDATED_DATE, 103) AS CONSOLIDATED_DATE, 
                   ISNULL(SUM(B.FINAL_QTY), 0) AS FINAL_QTY,
                   (CASE WHEN A.STATUS = 'I' THEN 'Incomplete' WHEN A.STATUS = 'C' THEN 'Completed' ELSE '' END) AS EStatus,
                   (SELECT (CASE WHEN FileName IS NULL THEN 'Not Uploaded' ELSE 'Uploaded' END) 
                    FROM INDENT_CONSOLIDATION 
                    WHERE indent_consolidation_id = A.INDENT_CONSOLIDATION_ID) AS uploadStatus,
                   A.CreatedOn
            FROM INDENT_CONSOLIDATION A 
            LEFT OUTER JOIN INDENT_CONS_ITEMS B ON (B.INDENT_CONSOLIDATED_ID = A.INDENT_CONSOLIDATION_ID)			
            WHERE A.financial_year_id = @YearId AND A.directorate_id = @DirectorateId
            GROUP BY A.INDENT_CONSOLIDATION_ID, A.USER_ID, A.DIRECTORATE_ID, A.FINANCIAL_YEAR_ID, 
                     A.STATUS, A.CONSOLIDATED_DATE, A.indent_con_no, A.CreatedOn, A.description
            ORDER BY A.CreatedOn DESC";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        // Adding strong typed parameter bindings to avoid SQL Injection completely
                        cmd.Parameters.AddWithValue("@YearId", yearId);
                        cmd.Parameters.AddWithValue("@DirectorateId", directorateId);

                        await conn.OpenAsync();

                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            while (await dr.ReadAsync())
                            {
                                consolidationList.Add(new IndentConsolidationReportDto
                                {
                                    EquipmentCount = Convert.ToInt32(dr["Equipmentcount"]),
                                    IndentConsolidationId = Convert.ToInt32(dr["INDENT_CONSOLIDATION_ID"]),
                                    Description = dr["description"]?.ToString() ?? string.Empty,
                                    UserId = Convert.ToInt32(dr["USER_ID"]),
                                    DirectorateId = Convert.ToInt32(dr["DIRECTORATE_ID"]),
                                    FinancialYearId = Convert.ToInt32(dr["FINANCIAL_YEAR_ID"]),
                                    ProposedQty = dr["PROPOSED_QTY"] != DBNull.Value ? Convert.ToDecimal(dr["PROPOSED_QTY"]) : 0m,
                                    IndentConNo = dr["indent_con_no"]?.ToString() ?? string.Empty,
                                    ConsolidatedDate = dr["CONSOLIDATED_DATE"]?.ToString() ?? string.Empty,
                                    FinalQty = dr["FINAL_QTY"] != DBNull.Value ? Convert.ToDecimal(dr["FINAL_QTY"]) : 0m,
                                    EStatus = dr["EStatus"]?.ToString() ?? string.Empty,
                                    UploadStatus = dr["uploadStatus"]?.ToString() ?? string.Empty,
                                    CreatedOn = dr["CreatedOn"] != DBNull.Value ? Convert.ToDateTime(dr["CreatedOn"]) : null
                                });
                            }
                        }
                    }
                }

                return Ok(consolidationList); // Returns safe array JSON data mapping framework models
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error pulling indent consolidation reporting streams.", error = ex.Message });
            }
        }
        [HttpPost("SaveIndentConsolidation")]
        public async Task<IActionResult> SaveIndentConsolidation([FromBody] SaveIndentConsolidationRequestDto request)
        {
            // --- 1. SERVER-SIDE CONDITIONAL VALIDATIONS ---
            if (request.FinancialYearId <= 0)
                return BadRequest(new { message = "Select Financial Year" });

            if (request.DirectorateId <= 0)
                return BadRequest(new { message = "Select Directorate" });

            if (request.FundId <= 0)
                return BadRequest(new { message = "Select Fund Head" });

            if (string.IsNullOrWhiteSpace(request.IndentDescription))
                return BadRequest(new { message = "Enter Letter No" });

            if (string.IsNullOrWhiteSpace(request.IndentDateStr))
                return BadRequest(new { message = "Enter Indent Date" });

            // Parse date input
            if (!DateTime.TryParse(request.IndentDateStr, out DateTime receivedDT))
            {
                return BadRequest(new { message = "Invalid Indent Date format." });
            }

            // Condition check: Indent date cannot be greater than today
            if (receivedDT.Date > DateTime.Now.Date)
            {
                return BadRequest(new { message = "Indent Date cannot be greater than Today" });
            }

            string connString = _config.GetConnectionString("DefaultConnection");

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    await conn.OpenAsync();

                    // --- 2. EMULATE legacy helper: ind.CheckDatainYear ---
                    // Financial year ranges validation engine check fallback (Example validation layout)
                    string checkYearSql = "SELECT COUNT(1) FROM MAS_FINANCIAL_YEAR WHERE FINANCIAL_YEAR_ID = @YearId AND @IndentDate BETWEEN START_DATE AND END_DATE";
                    using (SqlCommand checkCmd = new SqlCommand(checkYearSql, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@YearId", request.FinancialYearId);
                        checkCmd.Parameters.AddWithValue("@IndentDate", receivedDT);

                        int isValidYear = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                        if (isValidYear == 0)
                        {
                            return BadRequest(new { message = "Indent Date is Not valid for the Selected Year" });
                        }
                    }

                    // --- 3. DATABASE TRANSACTION BLOCK FOR DOUBLE ENGINES UPDATES ---
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // QUERY A: Insert statement execution pipeline + Fetch explicit output Identity ID
                            string insertSql = @"INSERT INTO INDENT_CONSOLIDATION 
                                            (IndentFundID, description, CONSOLIDATED_DATE, USER_ID, DIRECTORATE_ID, FINANCIAL_YEAR_ID, STATUS, CreatedOn)
                                             VALUES
                                            (@FundId, @Desc, @ConsolidatedDate, @UserId, @DirId, @FinYearId, 'I', GETDATE());
                                             SELECT SCOPE_IDENTITY();";

                            int generatedId = 0;
                            using (SqlCommand insertCmd = new SqlCommand(insertSql, conn, transaction))
                            {
                                insertCmd.Parameters.AddWithValue("@FundId", request.FundId);
                                insertCmd.Parameters.AddWithValue("@Desc", request.IndentDescription.Trim());
                                insertCmd.Parameters.AddWithValue("@ConsolidatedDate", receivedDT);
                                insertCmd.Parameters.AddWithValue("@UserId", request.UserId);
                                insertCmd.Parameters.AddWithValue("@DirId", request.DirectorateId);
                                insertCmd.Parameters.AddWithValue("@FinYearId", request.FinancialYearId);

                                generatedId = Convert.ToInt32(await insertCmd.ExecuteScalarAsync());
                            }

                            // --- 4. EXPLICIT DYNAMIC CODE GENERATOR (getindent_consolidation_id emulation) ---
                            // Generates format schema: ID + Day + Month + ShortYear + / + GeneratedRowID
                            // E.g., ID28526/1045
                            string indentCode = $"ID{DateTime.Now.Day}{DateTime.Now.Month}{DateTime.Now.ToString("yy")}/{generatedId}";

                            // QUERY B: Update tracking serial mapping code back to row parameters
                            string updateSql = "UPDATE INDENT_CONSOLIDATION SET INDENT_CON_NO = @IndentCode WHERE INDENT_CONSOLIDATION_ID = @Id";
                            using (SqlCommand updateCmd = new SqlCommand(updateSql, conn, transaction))
                            {
                                updateCmd.Parameters.AddWithValue("@IndentCode", indentCode);
                                updateCmd.Parameters.AddWithValue("@Id", generatedId);

                                await updateCmd.ExecuteNonQueryAsync();
                            }

                            // Save transaction pipeline changes cleanly
                            await transaction.CommitAsync();

                            return Ok(new
                            {
                                message = "Indent Consolidation Saved Successfully.",
                                indentConsolidationId = generatedId,
                                indentConNo = indentCode
                            });
                        }
                        catch (Exception txEx)
                        {
                            await transaction.RollbackAsync();
                            throw new Exception("Transaction crashed inside update consolidation loops.", txEx);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error processing data persistence engine loops.", error = ex.Message });
            }
        }
    }
}
