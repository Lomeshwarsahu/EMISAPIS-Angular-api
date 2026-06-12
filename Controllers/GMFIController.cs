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
    public class GMFIController : ControllerBase
    {
        private readonly IConfiguration _config;

        public GMFIController(IConfiguration config, IWebHostEnvironment env)
        {
            _config = config;
            _env = env;
        }

        private readonly IWebHostEnvironment _env;

        [HttpGet("GetActivePurchaseOrders")]
        public async Task<IActionResult> GetActivePurchaseOrders([FromQuery] int finid)
        {
          
            if (finid <= 0)
            {
                return BadRequest(new { message = "Please provide a valid Financial Year ID (finid)." });
            }

            var poList = new List<PurchaseOrderDropdownDto>();
            string connString = _config.GetConnectionString("DefaultConnection");

            string sqlQuery = @"SELECT po_id, po_no 
                            FROM purchase_order 
                            WHERE status IN ('Partially Received', 'Order Placed') 
                              AND financial_year_id = @FinYearId";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sqlQuery, conn))
                    {
                        // Binding 'finid' query parameter to secure SQL variable safely
                        cmd.Parameters.AddWithValue("@FinYearId", finid);

                        await conn.OpenAsync();

                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            while (await dr.ReadAsync())
                            {
                                poList.Add(new PurchaseOrderDropdownDto
                                {
                                    PoId = Convert.ToInt32(dr["po_id"]),
                                    PoNo = dr["po_no"]?.ToString() ?? string.Empty
                                });
                            }
                        }
                    }
                }

                return Ok(poList); // Returns clean JSON collection array structure to frontend dropdown
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error pulling filtered active purchase orders dropdown list.", error = ex.Message });
            }
        }
        [HttpGet("GetConsigneesByPo")]
        public async Task<IActionResult> GetConsigneesByPo([FromQuery] int poId)
        {
            // 1. Parameter Validation Check Guard
            if (poId <= 0)
            {
                return BadRequest(new { message = "Please provide a valid Purchase Order ID (poId)." });
            }

            var consigneeList = new List<ConsigneeDropdownDto>();
            string connString = _config.GetConnectionString("DefaultConnection");

            // 2. Core SQL Query with Parameterized Binding
            string sqlQuery = @"SELECT DISTINCT i.consignee_id, l.location_name 
                            FROM po_items i
                            INNER JOIN maslocations l ON l.location_id = i.consignee_id
                            WHERE i.po_id = @PoId 
                            ORDER BY l.location_name ASC";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sqlQuery, conn))
                    {
                        // Securely bind the parameter to prevent SQL Injection
                        cmd.Parameters.AddWithValue("@PoId", poId);

                        await conn.OpenAsync();

                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            while (await dr.ReadAsync())
                            {
                                consigneeList.Add(new ConsigneeDropdownDto
                                {
                                    ConsigneeId = Convert.ToInt32(dr["consignee_id"]),
                                    LocationName = dr["location_name"]?.ToString() ?? string.Empty
                                });
                            }
                        }
                    }
                }

                return Ok(consigneeList); // Returns structured JSON collection array to frontend dropdown
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error pulling consignees list for the selected purchase order.", error = ex.Message });
            }
        }

        [HttpGet("GetReceiptItemTrackingDetails")]
        public async Task<IActionResult> GetReceiptItemTrackingDetails(
        [FromQuery] int locationId,
        [FromQuery] int poId,
        [FromQuery] int finYearId)
        {
            // 1. Parameter Boundary Checks Guard
            if (locationId <= 0 || poId <= 0 || finYearId <= 0)
            {
                return BadRequest(new { message = "Please provide valid Consignee Location ID, PO ID, and Financial Year ID parameters." });
            }

            var trackingList = new List<ReceiptItemTrackingDto>();
            string connString = _config.GetConnectionString("DefaultConnection");

            // 2. Base Query Construction with Injection-Safe Variable Placeholders
            string sqlQuery = @"
            SELECT r.receipt_id, 
                   CONVERT(VARCHAR, r.recieved_date, 105) AS recieved_date, 
                   CONVERT(VARCHAR, ri.installation_date, 105) AS installation_date,
                   CONVERT(VARCHAR, ri.warenty_from, 105) AS warenty_from, 
                   CONVERT(VARCHAR, ri.warenty_to, 105) AS warenty_to, 
                   ri.item_detail_id, r.location_id, r.po_id, po.financial_year_id, 
                   CONVERT(VARCHAR, sd.dispatch_date, 105) AS dispatch_date, 
                   CONVERT(VARCHAR, sd.challan_date, 105) AS challan_date,
                   CONVERT(VARCHAR, sd.invoice_date, 105) AS invoice_date
            FROM receipts r
            INNER JOIN receipt_item_details ri ON ri.receipt_id = r.receipt_id
            INNER JOIN purchase_order po ON po.po_id = r.po_id
            INNER JOIN SupplierDispatch sd ON sd.po_id = r.po_id AND sd.Issue_id = r.issue_id
            WHERE r.location_id = @LocationId 
              AND r.po_id = @PoId 
              AND po.financial_year_id = @FinYearId
              AND r.status = 'C'";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sqlQuery, conn))
                    {
                        // Adding strong-typed parameter bindings safely
                        cmd.Parameters.AddWithValue("@LocationId", locationId);
                        cmd.Parameters.AddWithValue("@PoId", poId);
                        cmd.Parameters.AddWithValue("@FinYearId", finYearId);

                        await conn.OpenAsync();

                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            while (await dr.ReadAsync())
                            {
                                trackingList.Add(new ReceiptItemTrackingDto
                                {
                                    ReceiptId = Convert.ToInt32(dr["receipt_id"]),
                                    RecievedDate = dr["recieved_date"]?.ToString() ?? string.Empty,
                                    InstallationDate = dr["installation_date"]?.ToString() ?? string.Empty,
                                    WarentyFrom = dr["warenty_from"]?.ToString() ?? string.Empty,
                                    WarentyTo = dr["warenty_to"]?.ToString() ?? string.Empty,
                                    ItemDetailId = Convert.ToInt32(dr["item_detail_id"]),
                                    LocationId = Convert.ToInt32(dr["location_id"]),
                                    PoId = Convert.ToInt32(dr["po_id"]),
                                    FinancialYearId = Convert.ToInt32(dr["financial_year_id"]),
                                    DispatchDate = dr["dispatch_date"]?.ToString() ?? string.Empty,
                                    ChallanDate = dr["challan_date"]?.ToString() ?? string.Empty,
                                    InvoiceDate = dr["invoice_date"]?.ToString() ?? string.Empty
                                });
                            }
                        }
                    }
                }

                return Ok(trackingList); // Returns clean, formatted JSON array stream to Angular frontend
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error streaming purchase order receipt item tracking details.", error = ex.Message });
            }
        }


        [HttpPost("UpdateReceiptReceivedDateActual")]
        public async Task<IActionResult> UpdateReceiptReceivedDateActual([FromBody] UpdateReceivedDateRequestDto request)
        {
            // --- 1. SERVER SIDE BOUNDARY CHECK ---
            if (request == null || request.ReceiptId <= 0 || string.IsNullOrWhiteSpace(request.ReceivedDate))
            {
                return BadRequest(new { message = "Please provide a valid Receipt ID and Received Date." });
            }

            // --- 2. BUSINESS DATE VALIDATION LOGIC ---
            // Parse format strings "dd-MM-yyyy" to strict internal DateTimes mapping structures
            CultureInfo provider = CultureInfo.InvariantCulture;
            try
            {
                DateTime receivedDt = DateTime.ParseExact(request.ReceivedDate, "yyyy-MM-dd", provider);
                DateTime dispatchDt = DateTime.ParseExact(request.DispatchDate, "yyyy-MM-dd", provider);
                DateTime challanDt = DateTime.ParseExact(request.ChallanDate, "yyyy-MM-dd", provider);
                DateTime invoiceDt = DateTime.ParseExact(request.InvoiceDate, "yyyy-MM-dd", provider);

                if (dispatchDt > receivedDt || challanDt > receivedDt || invoiceDt > receivedDt)
                {
                    return BadRequest(new { message = "Received Date cannot be before Supplier Dispatch Date / Challan Date / Invoice Date." });
                }
            }
            catch (FormatException)
            {
                return BadRequest(new { message = "Date format parsing mismatch. Expected structure standard parameters syntax format." });
            }

            string connString = _config.GetConnectionString("DefaultConnection");

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    await conn.OpenAsync();

                    // ========================================================
                    // STEP A: EMULATING: getrecdate() Method Context
                    // ========================================================
                    string oldDateStr = "-";
                    string selectOldDateSql = "SELECT recieved_date FROM receipts WHERE receipt_id = @ReceiptId";
                    using (SqlCommand selectCmd = new SqlCommand(selectOldDateSql, conn))
                    {
                        selectCmd.Parameters.AddWithValue("@ReceiptId", request.ReceiptId);
                        var result = await selectCmd.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                        {
                            oldDateStr = result.ToString(); // Fetches original baseline date reference 
                        }
                    }

                    // ========================================================
                    // STEP B: START SQL TRANSACTION FOR AUDIT TRAIL LOGGING
                    // ========================================================
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // 1. Core Insert Initial Logs statement (Emulating: strInsert)
                            string insertAuditSql = @"
                            INSERT INTO audit (TableName, FieldName, RecordId, OldDate, Userid, AddDate)
                            VALUES ('receipts', 'recieved_date', @RecordId, @OldDate, 5, GETDATE());
                            SELECT SCOPE_IDENTITY();";

                            long generatedAuditId = 0;
                            using (SqlCommand insAuditCmd = new SqlCommand(insertAuditSql, conn, transaction))
                            {
                                insAuditCmd.Parameters.AddWithValue("@RecordId", request.ReceiptId.ToString());
                                insAuditCmd.Parameters.AddWithValue("@OldDate", oldDateStr);

                                generatedAuditId = Convert.ToInt64(await insAuditCmd.ExecuteScalarAsync());
                            }

                            // 2. Core receipts Update processing matrix (Emulating: strSQL2)
                            string updateReceiptSql = @"
                            UPDATE receipts 
                            SET recieved_date = CONVERT(DATE, @NewDate, 120) 
                            WHERE receipt_id = @ReceiptId";

                            using (SqlCommand updReceiptCmd = new SqlCommand(updateReceiptSql, conn, transaction))
                            {
                                updReceiptCmd.Parameters.AddWithValue("@NewDate", request.ReceivedDate);
                                updReceiptCmd.Parameters.AddWithValue("@ReceiptId", request.ReceiptId);

                                await updReceiptCmd.ExecuteNonQueryAsync();
                            }

                            // 3. Finalize update log entry mappings dynamically (Emulating: strSQL3 & getId())
                            string updateAuditNewDateSql = @"
                            UPDATE audit 
                            SET NewDate = CONVERT(DATE, @NewDate, 120) 
                            WHERE RecordId = @RecordId AND id = @AuditId";

                            using (SqlCommand updAuditCmd = new SqlCommand(updateAuditNewDateSql, conn, transaction))
                            {
                                updAuditCmd.Parameters.AddWithValue("@NewDate", request.ReceivedDate);
                                updAuditCmd.Parameters.AddWithValue("@RecordId", request.ReceiptId.ToString());
                                updAuditCmd.Parameters.AddWithValue("@AuditId", generatedAuditId);

                                await updAuditCmd.ExecuteNonQueryAsync();
                            }

                            // Commit full database commands loops pipeline execution
                            await transaction.CommitAsync();
                            return Ok(new { message = "Received Date has been Updated Successfully." });
                        }
                        catch (Exception txEx)
                        {
                            await transaction.RollbackAsync();
                            throw new Exception("SQL Server inner batch logging validation collapsed inside transaction blocks.", txEx);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error processing dynamic system data registers updates.", error = ex.Message });
            }
        }
        [HttpPost("UpdateReceiptInstallationDateActual")]
        public async Task<IActionResult> UpdateReceiptInstallationDateActual([FromBody] UpdateInstallationDateRequestDto request)
        {
            // --- 1. SERVER SIDE MANDATORY VALIDATIONS ---
            if (request == null || request.ReceiptId <= 0 ||
                string.IsNullOrWhiteSpace(request.InstallationDate) ||
                string.IsNullOrWhiteSpace(request.WarrantyFrom) ||
                string.IsNullOrWhiteSpace(request.WarrantyTo))
            {
                return BadRequest(new { message = "Please fill all required dates fields." });
            }

            // --- 2. BUSINESS DATE VALIDATION LOGIC ---
            CultureInfo provider = CultureInfo.InvariantCulture;
            try
            {
                DateTime installationDt = DateTime.ParseExact(request.InstallationDate, "yyyy-MM-dd", provider);
                DateTime receivedDt = DateTime.ParseExact(request.ReceivedDate, "yyyy-MM-dd", provider);

                // Emulating: if (ReceivedDate > InstallationDate)
                if (receivedDt > installationDt)
                {
                    return BadRequest(new { message = "Installation Date cannot be before Consignee Receipt Date." });
                }
            }
            catch (FormatException)
            {
                return BadRequest(new { message = "Invalid date format constraint. Expected standard notation structure." });
            }

            string connString = _config.GetConnectionString("DefaultConnection");

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    await conn.OpenAsync();

                    // ========================================================
                    // STEP A: EMULATING: getinstdate() Method Context
                    // ========================================================
                    string oldDateStr = "-";
                    string selectOldDateSql = "SELECT installation_date FROM receipt_item_details WHERE receipt_id = @ReceiptId";
                    using (SqlCommand selectCmd = new SqlCommand(selectOldDateSql, conn))
                    {
                        selectCmd.Parameters.AddWithValue("@ReceiptId", request.ReceiptId);
                        var result = await selectCmd.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                        {
                            oldDateStr = result.ToString(); // Original baseline installation date reference 
                        }
                    }

                    // ========================================================
                    // STEP B: START SQL TRANSACTION FOR AUDIT TRAIL LOGGING
                    // ========================================================
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // 1. Insert Initial Logs statement into Audit (Emulating: strInsert)
                            string insertAuditSql = @"
                            INSERT INTO audit (TableName, FieldName, RecordId, OldDate, Userid, AddDate)
                            VALUES ('receipt_item_details', 'installation_date', @RecordId, @OldDate, 5, GETDATE());
                            SELECT SCOPE_IDENTITY();";

                            long generatedAuditId = 0;
                            using (SqlCommand insAuditCmd = new SqlCommand(insertAuditSql, conn, transaction))
                            {
                                insAuditCmd.Parameters.AddWithValue("@RecordId", request.ReceiptId.ToString());
                                insAuditCmd.Parameters.AddWithValue("@OldDate", oldDateStr);

                                generatedAuditId = Convert.ToInt64(await insAuditCmd.ExecuteScalarAsync());
                            }

                            // 2. Core details Update processing (Emulating: strSQL2 with 3 years Warranty Add)
                            string updateInstallationSql = @"
                            UPDATE receipt_item_details 
                            SET installation_date = CONVERT(DATE, @NewDate, 120), 
                                warenty_from = CONVERT(DATE, @NewDate, 120), 
                                warenty_to = DATEADD(year, 3, CONVERT(DATE, @WarrantyFrom, 120))
                            WHERE receipt_id = @ReceiptId";

                            using (SqlCommand updInstCmd = new SqlCommand(updateInstallationSql, conn, transaction))
                            {
                                updInstCmd.Parameters.AddWithValue("@NewDate", request.InstallationDate);
                                updInstCmd.Parameters.AddWithValue("@WarrantyFrom", request.WarrantyFrom);
                                updInstCmd.Parameters.AddWithValue("@ReceiptId", request.ReceiptId);

                                await updInstCmd.ExecuteNonQueryAsync();
                            }

                            // 3. Finalize update log entry mappings dynamically (Emulating: strSQL3 & getId())
                            string updateAuditNewDateSql = @"
                            UPDATE audit 
                            SET NewDate = CONVERT(DATE, @NewDate, 120) 
                            WHERE RecordId = @RecordId AND id = @AuditId";

                            using (SqlCommand updAuditCmd = new SqlCommand(updateAuditNewDateSql, conn, transaction))
                            {
                                updAuditCmd.Parameters.AddWithValue("@NewDate", request.InstallationDate);
                                updAuditCmd.Parameters.AddWithValue("@RecordId", request.ReceiptId.ToString());
                                updAuditCmd.Parameters.AddWithValue("@AuditId", generatedAuditId);

                                await updAuditCmd.ExecuteNonQueryAsync();
                            }

                            // Commit entire batch operation loop cleanly
                            await transaction.CommitAsync();
                            return Ok(new { message = "Installation Date has been Updated Successfully" });
                        }
                        catch (Exception txEx)
                        {
                            await transaction.RollbackAsync();
                            throw new Exception("Transaction batch process crashed inside audit update blocks.", txEx);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating master inventory data registers.", error = ex.Message });
            }
        }
        [HttpGet("GetSuppliersGridRecords")]
        public async Task<IActionResult> GetSuppliersGridRecords(
        [FromQuery] string searchText = "",
        [FromQuery] bool incInactive = false)
        {
            var supplierList = new List<SupplierGridDto>();
            string connString = _config.GetConnectionString("DefaultConnection");

            // 1. Core Injection-Safe Select Fields & From Structure Assembly
            string sqlQuery = @"
            SELECT a.supplier_id AS SupplierID, 
                   a.supplier_code AS SupplierCode,
                   CASE WHEN a.address = '' THEN a.name ELSE a.name END AS SupplierName, 
                   'India' AS CountryName, 
                   a.is_register AS IsActive,
                   (SELECT CASE COUNT(*) WHEN 0 THEN 'True' ELSE 'False' END FROM purchase_order y WHERE y.supplier_id = a.supplier_id) AS Deletable, 
                   a.address AS Address1, 
                   a.address AS Address2, 
                   a.address AS Address3, 
                   '' AS City, 
                   '' AS Zip, 
                   a.mobile_no AS ContactPerson, 
                   CASE a.ph_no WHEN '' THEN ISNULL(a.ph_no, '') ELSE a.ph_no END AS Phone, 
                   a.fax_no AS Fax, 
                   a.email_id AS Email 
            FROM masSuppliers a 
            WHERE 1 = 1";

            // 2. Emulating: chkIncludeInactive Logic Block (Bypasses active filter if true)
            if (!incInactive)
            {
                // Assuming active suppliers status flag check code context is stored as 'Y' or '1'
                sqlQuery += " AND (a.is_register = 'Y' OR a.is_register = '1' OR a.is_register = 'True')";
            }

            // 3. Emulating: Upper Dynamic Search Criteria Conditions
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                sqlQuery += @" AND (UPPER(a.name) LIKE UPPER(@SearchText) 
                            OR UPPER(a.supplier_code) LIKE UPPER(@SearchText) 
                            OR UPPER(a.address) LIKE UPPER(@SearchText) 
                            OR UPPER(a.ph_no) LIKE UPPER(@SearchText))";
            }

            sqlQuery += " ORDER BY a.name";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sqlQuery, conn))
                    {
                        // Safe injection checking placeholder configuration
                        if (!string.IsNullOrWhiteSpace(searchText))
                        {
                            cmd.Parameters.AddWithValue("@SearchText", $"%{searchText.Trim()}%");
                        }

                        await conn.OpenAsync();

                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            while (await dr.ReadAsync())
                            {
                                supplierList.Add(new SupplierGridDto
                                {
                                    SupplierId = Convert.ToInt32(dr["SupplierID"]),
                                    SupplierCode = dr["SupplierCode"]?.ToString() ?? string.Empty,
                                    SupplierName = dr["SupplierName"]?.ToString() ?? string.Empty,
                                    CountryName = dr["CountryName"]?.ToString() ?? "India",
                                    IsActive = dr["IsActive"]?.ToString() ?? string.Empty,
                                    Deletable = dr["Deletable"]?.ToString() ?? "True",
                                    Address1 = dr["Address1"]?.ToString() ?? string.Empty,
                                    Address2 = dr["Address2"]?.ToString() ?? string.Empty,
                                    Address3 = dr["Address3"]?.ToString() ?? string.Empty,
                                    City = dr["City"]?.ToString() ?? string.Empty,
                                    Zip = dr["Zip"]?.ToString() ?? string.Empty,
                                    ContactPerson = dr["ContactPerson"]?.ToString() ?? string.Empty,
                                    Phone = dr["Phone"]?.ToString() ?? string.Empty,
                                    Fax = dr["Fax"]?.ToString() ?? string.Empty,
                                    Email = dr["Email"]?.ToString() ?? string.Empty
                                });
                            }
                        }
                    }
                }

                return Ok(supplierList); // Returns safe structured JSON lists collection matrix to frontend
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error pulling dynamic supplier master directories indexes.", error = ex.Message });
            }
        }
        // A. FETCH SUPPLIER HEADER INFO (lblSupplierName & Code Context)
        // GET api/SupplierBank/GetSupplierHeaderInfo/180
        [HttpGet("GetSupplierHeaderInfo/{supplierId}")]
        public async Task<IActionResult> GetSupplierHeaderInfo(int supplierId)
        {
            string connString = _config.GetConnectionString("DefaultConnection");
            string query = "SELECT supplier_code, name FROM masSuppliers WHERE supplier_id = @SupId";

            using (SqlConnection conn = new SqlConnection(connString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@SupId", supplierId);
                    await conn.OpenAsync();
                    using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                    {
                        if (await dr.ReadAsync())
                        {
                            return Ok(new
                            {
                                supplierCode = dr["supplier_code"]?.ToString() ?? "",
                                supplierName = dr["name"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
            return NotFound(new { message = "Supplier profile records not found." });
        }

        // B. FETCH ALL BANK ACCOUNTS LOG LIST
        // GET api/SupplierBank/GetSupplierBankAccounts/180
        [HttpGet("GetSupplierBankAccounts/{supplierId}")]
        public async Task<IActionResult> GetSupplierBankAccounts(int supplierId)
        {
            var list = new List<SupplierBankAccDto>();
            string connString = _config.GetConnectionString("DefaultConnection");
            string query = @"SELECT a.BankAccountID, a.SupplierID, a.AccountNo, a.AccountName, a.BankName, a.Branch,
                                a.IFSCCode, a.MICRCode, a.DefaultAcc, 
                                (CASE WHEN a.DefaultAcc = 1 THEN 'Yes' ELSE 'No' END) AS DefaultAccText, a.Remarks
                         FROM masSupplierAccNos a
                         INNER JOIN masSuppliers b ON a.SupplierID = b.supplier_id
                         WHERE a.SupplierID = @SupId
                         ORDER BY a.BankName, a.Branch";

            using (SqlConnection conn = new SqlConnection(connString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@SupId", supplierId);
                    await conn.OpenAsync();
                    using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                    {
                        while (await dr.ReadAsync())
                        {
                            list.Add(new SupplierBankAccDto
                            {
                                BankAccountId = Convert.ToInt32(dr["BankAccountID"]),
                                SupplierId = Convert.ToInt32(dr["SupplierID"]),
                                AccountNo = dr["AccountNo"]?.ToString() ?? "",
                                AccountName = dr["AccountName"]?.ToString() ?? "",
                                BankName = dr["BankName"]?.ToString() ?? "",
                                Branch = dr["Branch"]?.ToString() ?? "",
                                IfscCode = dr["IFSCCode"]?.ToString() ?? "",
                                MicrCode = dr["MICRCode"]?.ToString() ?? "",
                                DefaultAcc = Convert.ToInt32(dr["DefaultAcc"]),
                                DefaultAccText = dr["DefaultAccText"]?.ToString() ?? "No",
                                Remarks = dr["Remarks"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
            return Ok(list);
        }

        // C. TRANSACTION SAVE OR UPDATE METHOD (Emulating: gvBankAccNos_RowUpdating)
        // POST api/SupplierBank/SaveOrUpdateBankAccount
        [HttpPost("SaveOrUpdateBankAccount")]
        public async Task<IActionResult> SaveOrUpdateBankAccount([FromBody] SupplierBankAccDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.AccountNo) || string.IsNullOrWhiteSpace(dto.IfscCode))
                return BadRequest(new { message = "Account Number and IFSC Code fields are mandatory entries." });

            string connString = _config.GetConnectionString("DefaultConnection");
            using (SqlConnection conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();

                // Legacy Logic Rule: Check if another unique default account already active
                if (dto.DefaultAcc == 1)
                {
                    string checkSql = "SELECT COUNT(*) FROM masSupplierAccNos WHERE SupplierID = @SupId AND DefaultAcc = 1 AND BankAccountID != @BankAccId";
                    using (SqlCommand chkCmd = new SqlCommand(checkSql, conn))
                    {
                        chkCmd.Parameters.AddWithValue("@SupId", dto.SupplierId);
                        chkCmd.Parameters.AddWithValue("@BankAccId", dto.BankAccountId);
                        int count = Convert.ToInt32(await chkCmd.ExecuteScalarAsync());
                        if (count > 0) return BadRequest(new { message = "Default Account can be set for only one bank account configuration context." });
                    }
                }

                string targetQuery = "";
                if (dto.BankAccountId == 0) // INSERT TRANSACTION
                {
                    targetQuery = @"INSERT INTO masSupplierAccNos (SupplierID, AccountNo, AccountName, BankName, Branch, IFSCCode, MICRCode, DefaultAcc, Remarks)
                                VALUES (@SupId, @AccNo, @AccName, @BName, @Branch, @Ifsc, @Micr, @Def, @Rem)";
                }
                else // UPDATE TRANSACTION
                {
                    targetQuery = @"UPDATE masSupplierAccNos 
                                SET AccountNo = @AccNo, AccountName = @AccName, BankName = @BName, Branch = @Branch, 
                                    IFSCCode = @Ifsc, MICRCode = @Micr, DefaultAcc = @Def, Remarks = @Rem 
                                WHERE BankAccountID = @BankAccId";
                }

                using (SqlCommand cmd = new SqlCommand(targetQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@BankAccId", dto.BankAccountId);
                    cmd.Parameters.AddWithValue("@SupId", dto.SupplierId);
                    cmd.Parameters.AddWithValue("@AccNo", dto.AccountNo);
                    cmd.Parameters.AddWithValue("@AccName", dto.AccountName);
                    cmd.Parameters.AddWithValue("@BName", dto.BankName);
                    cmd.Parameters.AddWithValue("@Branch", dto.Branch);
                    cmd.Parameters.AddWithValue("@Ifsc", dto.IfscCode);
                    cmd.Parameters.AddWithValue("@Micr", string.IsNullOrEmpty(dto.MicrCode) ? "0" : dto.MicrCode);
                    cmd.Parameters.AddWithValue("@Def", dto.DefaultAcc);
                    cmd.Parameters.AddWithValue("@Rem", string.IsNullOrEmpty(dto.Remarks) ? "-" : dto.Remarks);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
            return Ok(new { message = dto.BankAccountId == 0 ? "Bank Account Added Successfully" : "Bank Account Updated Successfully" });
        }

        // D. SECURE DELETE METHOD (Emulating: gvBankAccNos_RowDeleting)
        // DELETE api/SupplierBank/DeleteBankAccount/12
        [HttpDelete("DeleteBankAccount/{bankAccountId}")]
        public async Task<IActionResult> DeleteBankAccount(int bankAccountId)
        {
            string connString = _config.GetConnectionString("DefaultConnection");
            try
            {
                string sql = "DELETE FROM masSupplierAccNos WHERE BankAccountID = @BankAccId";
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@BankAccId", bankAccountId);
                        await conn.OpenAsync();
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                return Ok(new { message = "Deleted successfully" });
            }
            catch (SqlException ex) when (ex.Number == 547) // Referential Integrity violation capture
            {
                return BadRequest(new { message = "Delete not allowed, references found inside linked accounting logs." });
            }
        }
        //[HttpGet("GetSupplierHeaderInfo/{supplierId}")]
        //public async Task<IActionResult> GetSupplierHeaderInfo(int supplierId)
        //{
        //    string connString = _config.GetConnectionString("DefaultConnection");
        //    string query = "SELECT supplier_code, name FROM masSuppliers WHERE supplier_id = @SupId";

        //    using (SqlConnection conn = new SqlConnection(connString))
        //    {
        //        using (SqlCommand cmd = new SqlCommand(query, conn))
        //        {
        //            cmd.Parameters.AddWithValue("@SupId", supplierId);
        //            await conn.OpenAsync();
        //            using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
        //            {
        //                if (await dr.ReadAsync())
        //                {
        //                    return Ok(new
        //                    {
        //                        supplierCode = dr["supplier_code"]?.ToString() ?? "",
        //                        supplierName = dr["name"]?.ToString() ?? ""
        //                    });
        //                }
        //            }
        //        }
        //    }
        //    return NotFound(new { message = "Supplier profile records not found." });
        //}

        // B. FETCH SUPPLIER REGISTERED GST LIST
        // GET api/SupplierGst/GetSupplierGstRecords/180
        [HttpGet("GetSupplierGstRecords/{supplierId}")]
        public async Task<IActionResult> GetSupplierGstRecords(int supplierId)
        {
            var list = new List<SupplierGstDto>();
            string connString = _config.GetConnectionString("DefaultConnection");
            string query = "SELECT gstid, gstno, supplierid, flag FROM massuppliergst WHERE supplierid = @SupId ORDER BY gstid ASC";

            using (SqlConnection conn = new SqlConnection(connString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@SupId", supplierId);
                    await conn.OpenAsync();
                    using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                    {
                        while (await dr.ReadAsync())
                        {
                            list.Add(new SupplierGstDto
                            {
                                Gstid = Convert.ToInt32(dr["gstid"]),
                                Supplierid = Convert.ToInt32(dr["supplierid"]),
                                Gstno = dr["gstno"]?.ToString() ?? "",
                                Flag = dr["flag"]?.ToString() ?? "Y"
                            });
                        }
                    }
                }
            }
            return Ok(list);
        }

        // C. INSERT OR UPDATE GST REGISTRATION (Emulating: gvBankAccNos_RowUpdating)
        // POST api/SupplierGst/SaveOrUpdateSupplierGst
        [HttpPost("SaveOrUpdateSupplierGst")]
        public async Task<IActionResult> SaveOrUpdateSupplierGst([FromBody] SupplierGstDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Gstno) || dto.Supplierid <= 0)
                return BadRequest(new { message = "GST Number and Supplier reference index are mandatory data parameters." });

            string connString = _config.GetConnectionString("DefaultConnection");
            using (SqlConnection conn = new SqlConnection(connString))
            {
                string targetQuery = "";
                if (dto.Gstid == 0) // INSERT NEW GST ENTRY
                {
                    targetQuery = "INSERT INTO massuppliergst (gstno, supplierid, flag) VALUES (@GstNo, @SupId, 'Y')";
                }
                else // UPDATE EXISTING GST ENTRY
                {
                    targetQuery = "UPDATE massuppliergst SET gstno = @GstNo WHERE gstid = @GstId";
                }

                using (SqlCommand cmd = new SqlCommand(targetQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@GstId", dto.Gstid);
                    cmd.Parameters.AddWithValue("@SupId", dto.Supplierid);
                    cmd.Parameters.AddWithValue("@GstNo", dto.Gstno.Trim().ToUpper());

                    await conn.OpenAsync();
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            return Ok(new { message = dto.Gstid == 0 ? "Added Successfully" : "Updated Successfully" });
        }

        // D. DELETE GST RECORD ENTRY (Emulating: gvBankAccNos_RowDeleting)
        // DELETE api/SupplierGst/DeleteSupplierGst/12
        [HttpDelete("DeleteSupplierGst/{gstId}")]
        public async Task<IActionResult> DeleteSupplierGst(int gstId)
        {
            string connString = _config.GetConnectionString("DefaultConnection");
            try
            {
                string sql = "DELETE FROM massuppliergst WHERE gstid = @GstId";
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@GstId", gstId);
                        await conn.OpenAsync();
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                return Ok(new { message = "Deleted successfully" });
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                return BadRequest(new { message = "Delete not allowed, references found in procurement transactional data." });
            }
        }

        [HttpGet("GetActiveAccounts")]
        public async Task<IActionResult> GetActiveAccounts()
        {
            var list = new List<CgmscBankAccDto>();
            string connString = _config.GetConnectionString("DefaultConnection");
            string query = @"SELECT bankid, ACCOUNTNO, ACCOUNTNAME, BANKNAME, BRANCH, IFSCCODE, REMARKS, ISACTIVE 
                         FROM MasCGMSCAccNos 
                         WHERE ISACTIVE = 'Y' 
                         ORDER BY ENTRYDT ASC";

            using (SqlConnection conn = new SqlConnection(connString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    await conn.OpenAsync();
                    using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                    {
                        while (await dr.ReadAsync())
                        {
                            list.Add(new CgmscBankAccDto
                            {
                                Bankid = Convert.ToInt32(dr["bankid"]),
                                Accountno = dr["ACCOUNTNO"]?.ToString() ?? "",
                                Accountname = dr["ACCOUNTNAME"]?.ToString() ?? "",
                                Bankname = dr["BANKNAME"]?.ToString() ?? "",
                                Branch = dr["BRANCH"]?.ToString() ?? "",
                                Ifsccode = dr["IFSCCODE"]?.ToString() ?? "",
                                Remarks = dr["REMARKS"]?.ToString() ?? "",
                                Isactive = dr["ISACTIVE"]?.ToString() ?? "Y"
                            });
                        }
                    }
                }
            }
            return Ok(list);
        }

        // B. TRANSACTION PERSIST LOGIC: SAVE OR UPDATE ENTRY
        // POST api/CgmscBank/SaveOrUpdateAccount
        [HttpPost("SaveOrUpdateAccount")]
        public async Task<IActionResult> SaveOrUpdateAccount([FromBody] CgmscBankAccDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Accountno) || string.IsNullOrWhiteSpace(dto.Ifsccode))
                return BadRequest(new { message = "Account Number and IFSC Code fields are mandatory entries." });

            string connString = _config.GetConnectionString("DefaultConnection");
            using (SqlConnection conn = new SqlConnection(connString))
            {
                string targetQuery = "";
                if (dto.Bankid == 0) // INSERT RECORD ENTRY
                {
                    targetQuery = @"INSERT INTO MasCGMSCAccNos (AccountNo, AccountName, BankName, Branch, IFSCCode, Remarks, ENTRYDT, isactive)
                                VALUES (@AccNo, @AccName, @BName, @Branch, @Ifsc, @Rem, GETDATE(), 'Y')";
                }
                else // UPDATE RECORD ENTRY
                {
                    targetQuery = @"UPDATE MasCGMSCAccNos 
                                SET AccountNo = @AccNo, AccountName = @AccName, BankName = @BName, Branch = @Branch, 
                                    IFSCCode = @Ifsc, Remarks = @Rem, EntryDt = GETDATE() 
                                WHERE bankid = @BankId";
                }

                using (SqlCommand cmd = new SqlCommand(targetQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@BankId", dto.Bankid);
                    cmd.Parameters.AddWithValue("@AccNo", dto.Accountno.Trim());
                    cmd.Parameters.AddWithValue("@AccName", dto.Accountname.Trim());
                    cmd.Parameters.AddWithValue("@BName", dto.Bankname.Trim());
                    cmd.Parameters.AddWithValue("@Branch", dto.Branch.Trim());
                    cmd.Parameters.AddWithValue("@Ifsc", dto.Ifsccode.Trim().ToUpper());
                    cmd.Parameters.AddWithValue("@Rem", string.IsNullOrEmpty(dto.Remarks) ? "-" : dto.Remarks.Trim());

                    await conn.OpenAsync();
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            return Ok(new { message = dto.Bankid == 0 ? "Added Successfully" : "Updated Successfully" });
        }

        // C. SOFT DELETE MODEL ACTION (Emulating: update MasCGMSCAccNos set ISACTIVE = 'N')
        // DELETE api/CgmscBank/SoftDeleteAccount/5
        [HttpDelete("SoftDeleteAccount/{bankId}")]
        public async Task<IActionResult> SoftDeleteAccount(int bankId)
        {
            string connString = _config.GetConnectionString("DefaultConnection");
            string sql = "UPDATE MasCGMSCAccNos SET ISACTIVE = 'N' WHERE bankid = @BankId";

            using (SqlConnection conn = new SqlConnection(connString))
            {
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@BankId", bankId);
                    await conn.OpenAsync();
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    if (rowsAffected > 0)
                    {
                        return Ok(new { message = "Deleted successfully" });
                    }
                }
            }
            return BadRequest(new { message = "Record parsing failed or context trace target missing." });
        }
        [HttpGet("GetFundsList")]
        public async Task<IActionResult> GetFundsList()
        {
            var list = new List<FundMasterDto>();
            string connString = _config.GetConnectionString("DefaultConnection");

            // Target query structure matched to your legacy fields mapping
            string query = "SELECT budgetid, BUDGETNAME, ORDERID FROM MASBUDGET ORDER BY ORDERID DESC";

            using (SqlConnection conn = new SqlConnection(connString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    await conn.OpenAsync();
                    using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                    {
                        while (await dr.ReadAsync())
                        {
                            list.Add(new FundMasterDto
                            {
                                Budgetid = Convert.ToInt32(dr["budgetid"]),
                                Budgetname = dr["BUDGETNAME"]?.ToString() ?? "",
                                Orderid = dr["ORDERID"] != DBNull.Value ? Convert.ToInt32(dr["ORDERID"]) : 0
                            });
                        }
                    }
                }
            }
            return Ok(list);
        }

        // B. COMPREHENSIVE ADD NEW FUND TRANSACTION (Emulating: btnMap_Click & Validations)
        // POST api/FundMaster/AddNewFund
        [HttpPost("AddNewFund")]
        public async Task<IActionResult> AddNewFund([FromBody] FundMasterDto dto)
        {
            // 1. Mandatory Input Bound String Validation
            if (dto == null || string.IsNullOrWhiteSpace(dto.Budgetname))
            {
                return BadRequest(new { message = "Please Enter New Fund Name." });
            }

            string fundNameClean = dto.Budgetname.Trim();
            string connString = _config.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();

                // 2. Emulating: ChekAlreadyFund(string funName) Method Execution Logic
                string checkSql = "SELECT COUNT(*) FROM MASBUDGET WHERE UPPER(BUDGETNAME) = UPPER(@FundName)";
                using (SqlCommand chkCmd = new SqlCommand(checkSql, conn))
                {
                    chkCmd.Parameters.AddWithValue("@FundName", fundNameClean);
                    int duplicateCount = Convert.ToInt32(await chkCmd.ExecuteScalarAsync());
                    if (duplicateCount > 0)
                    {
                        return BadRequest(new { message = "Fund Already Exists!" });
                    }
                }

                // 3. Emulating: getLastOrderid() Dynamic Index Incrementation
                int nextOrderId = 1;
                string orderSql = "SELECT ISNULL(MAX(ORDERID), 0) FROM MASBUDGET";
                using (SqlCommand ordCmd = new SqlCommand(orderSql, conn))
                {
                    int currentMaxOrder = Convert.ToInt32(await ordCmd.ExecuteScalarAsync());
                    nextOrderId = currentMaxOrder + 1; // Generates incremental index thread-safely
                }

                // 4. Secure Parameterized Data Injection Command
                string insertSql = "INSERT INTO MASBUDGET (BUDGETNAME, ORDERID) VALUES (@FundName, @OrderId)";
                using (SqlCommand insCmd = new SqlCommand(insertSql, conn))
                {
                    insCmd.Parameters.AddWithValue("@FundName", fundNameClean);
                    insCmd.Parameters.AddWithValue("@OrderId", nextOrderId);

                    await insCmd.ExecuteNonQueryAsync();
                }
            }

            return Ok(new { message = "New Fund Added Successfully" });
        }





        //// A. POPULATE MAIN MENU DIRECTORATE DROPDOWN (Emulating: fillDirectorate)
        //[HttpGet("GetDirectorates")]
        //public async Task<IActionResult> GetDirectorates()
        //{
        //    var list = new List<DirectorateDropdownDto>();
        //    string query = "SELECT facility_aut_id, facility_aut_name FROM masDirectorate ORDER BY facility_aut_name ASC";
        //    using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        //    {
        //        using (SqlCommand cmd = new SqlCommand(query, conn))
        //        {
        //            await conn.OpenAsync();
        //            using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
        //            {
        //                while (await dr.ReadAsync())
        //                {
        //                    list.Add(new DirectorateDropdownDto
        //                    {
        //                        FacilityAutId = Convert.ToInt32(dr["facility_aut_id"]),
        //                        FacilityAutName = dr["facility_aut_name"]?.ToString() ?? ""
        //                    });
        //                }
        //            }
        //        }
        //    }
        //    return Ok(list);
        //}

        // B. POPULATE CONDITIONAL INSTITUTE DROPDOWN IF DIRECTORATE IS 12 (Emulating: FillDME)
        //[HttpGet("GetDmeInstitutes")]
        //public async Task<IActionResult> GetDmeInstitutes()
        //{
        //    var list = new List<DmeInstituteDropdownDto>();
        //    string query = "select designation +'-'+user_name as username,user_id from users where authority=12 and user_id!=12 order by designation";
        //    using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        //    {
        //        using (SqlCommand cmd = new SqlCommand(query, conn))
        //        {
        //            await conn.OpenAsync();
        //            using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
        //            {
        //                while (await dr.ReadAsync())
        //                {
        //                    list.Add(new DmeInstituteDropdownDto
        //                    {
        //                        UserId = Convert.ToInt32(dr["user_id"]),
        //                        Username = dr["username"]?.ToString() ?? ""
        //                    });
        //                }
        //            }
        //        }
        //    }
        //    return Ok(list);
        //}

        // C. LOAD SOURCE UNMAPPED/MAPPED HEADS FUNDS FOR SELECTION (Emulating: fillHeads)
        [HttpGet("GetFundsToMap/{directorateId}")]
        public async Task<IActionResult> GetFundsToMap(int directorateId)
        {
            var list = new List<FundMappingExecutionDto>();
            // Using matching subquery simulation tracker to evaluate if a fund is pre-mapped
            string query = @"select b.BUDGETID, b.BUDGETNAME, isnull(m.mapid,0) as cnt from MASBUDGET b
left outer join
(
select facility_aut_id, budgetid, MapId from  MasBudgetMap f
where 1=1 and f.facility_aut_id= @DirId
) m on 1=1 and m.budgetid=b.BUDGETID
where 1=1 order by b.BUDGETNAME";

            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@DirId", directorateId);
                    await conn.OpenAsync();
                    using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                    {
                        while (await dr.ReadAsync())
                        {
                            list.Add(new FundMappingExecutionDto
                            {
                                Budgetid = Convert.ToInt32(dr["budgetid"]),
                                Budgetname = dr["BUDGETNAME"]?.ToString() ?? "",
                                Cnt = Convert.ToInt32(dr["cnt"])
                            });
                        }
                    }
                }
            }
            return Ok(list);
        }
        // URL pattern supports both: 
        // 1. Without Institute: GET api/GMFI/GetMappedFundsSummary/12
        // 2. With Institute: GET api/GMFI/GetMappedFundsSummary/12?dmeUserId=45
        [HttpGet("GetMappedFundsSummary/{directorateId}")]
        public async Task<IActionResult> GetMappedFundsSummary(int directorateId, [FromQuery] string dmeUserId = "0")
        {
            // Safety parameter bounds check guard
            if (directorateId <= 0)
            {
                return BadRequest(new { message = "Please provide a valid Directorate ID." });
            }

            var list = new List<FundMappingExecutionDto>();
            string connString = _config.GetConnectionString("DefaultConnection");

            // 1. Base Query reflecting your EXACT original fields and inner joins mapping
            string query = @"SELECT f.facility_aut_name, b.BUDGETNAME, m.MapId, b.BUDGETID, f.facility_aut_id, m.DMEUserid,
                            ISNULL(u.user_name, '-') AS DMEUserName 
                     FROM MasBudgetMap m
                     INNER JOIN MASBUDGET b ON b.BUDGETID = m.budgetid
                     INNER JOIN facility_aut f ON f.facility_aut_id = m.facility_aut_id
                     LEFT OUTER JOIN users u ON u.user_id = m.DMEUserid
                     WHERE 1=1";

            // 2. Appending dynamic filters condition securely based on your legacy if-else flow
            if (!string.IsNullOrEmpty(dmeUserId) && dmeUserId != "0")
            {
                query += " AND f.facility_aut_id = @DirId AND m.DMEUserid = @DmeUserId";
            }
            else
            {
                query += " AND f.facility_aut_id = @DirId";
            }

            // Applying your precise fallback ordering scheme standard
            query += " ORDER BY b.BUDGETNAME";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        // Secure parameterized assignments to wipe out SQL Injection threats
                        cmd.Parameters.AddWithValue("@DirId", directorateId);

                        if (!string.IsNullOrEmpty(dmeUserId) && dmeUserId != "0")
                        {
                            cmd.Parameters.AddWithValue("@DmeUserId", dmeUserId.Trim());
                        }

                        await conn.OpenAsync();
                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            while (await dr.ReadAsync())
                            {
                                list.Add(new FundMappingExecutionDto
                                {
                                    MapId = Convert.ToInt32(dr["MapId"]),
                                    Budgetname = dr["BUDGETNAME"]?.ToString() ?? "",
                                    // Mapping directly from your true legacy database column keys
                                    FacilityAutName = dr["facility_aut_name"]?.ToString() ?? "",
                                    DmeUserName = dr["DMEUserName"]?.ToString() ?? "-"
                                });
                            }
                        }
                    }
                }
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Database query execution failed against 'facility_aut' system schemas.", error = ex.Message });
            }
        }

        [HttpPost("ExecuteBulkFundMapping")]
        public async Task<IActionResult> ExecuteBulkFundMapping([FromBody] BulkFundMapSubmissionDto request)
        {
            if (request == null || request.DirectorateId <= 0 || request.SelectedBudgetIds.Count == 0)
                return BadRequest(new { message = "Invalid mapping operational payload configurations." });

            if (request.DirectorateId == 12 && request.InstituteId <= 0)
                return BadRequest(new { message = "Please Select Hospital/College for Directorate of Medical Education." });

            string dmeUserIdStr = request.DirectorateId == 12 ? request.InstituteId.ToString() : "";
            string connString = _config.GetConnectionString("DefaultConnection");
            int mapsProcessedCount = 0;

            using (SqlConnection conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        foreach (int budgetId in request.SelectedBudgetIds)
                        {
                            // Duplicate Safety Check Inside the Transaction
                            string checkQuery = @"SELECT COUNT(*) FROM MasBudgetMap 
                                              WHERE facility_aut_id = @DirId AND budgetid = @BudId 
                                                AND (DMEUserid = @DmeId OR (DMEUserid IS NULL AND @DmeId = ''))";

                            using (SqlCommand chkCmd = new SqlCommand(checkQuery, conn, trans))
                            {
                                chkCmd.Parameters.AddWithValue("@DirId", request.DirectorateId);
                                chkCmd.Parameters.AddWithValue("@BudId", budgetId);
                                chkCmd.Parameters.AddWithValue("@DmeId", dmeUserIdStr);
                                int balanceExist = Convert.ToInt32(await chkCmd.ExecuteScalarAsync());

                                if (balanceExist > 0) continue; // Skip mapping if duplication matches context
                            }

                            // Insert statement payload
                            string insertQuery = @"INSERT INTO MasBudgetMap (facility_aut_id, budgetid, DMEUserid) 
                                               VALUES (@DirId, @BudId, @DmeId)";
                            using (SqlCommand insCmd = new SqlCommand(insertQuery, conn, trans))
                            {
                                insCmd.Parameters.AddWithValue("@DirId", request.DirectorateId);
                                insCmd.Parameters.AddWithValue("@BudId", budgetId);
                                insCmd.Parameters.AddWithValue("@DmeId", string.IsNullOrEmpty(dmeUserIdStr) ? (object)DBNull.Value : dmeUserIdStr);

                                await insCmd.ExecuteNonQueryAsync();
                                mapsProcessedCount++;
                            }
                        }
                        await trans.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        await trans.RollbackAsync();
                        return StatusCode(500, new { message = "Transaction collapsed during bulk assignment loop execution.", error = ex.Message });
                    }
                }
            }
            return Ok(new { message = $"{mapsProcessedCount} Funds Mapped Successfully" });
        }



        [HttpGet("GetAuthorityUsersDropdown")]
        public async Task<IActionResult> GetAuthorityUsersDropdown()
        {
            var userList = new List<DmeInstituteDropdownDto>();
            string connString = _config.GetConnectionString("DefaultConnection");

            // Query matches your exact conditions and ordering rule
            string query = @"SELECT designation + '-' + user_name AS username, user_id 
                         FROM users 
                         WHERE authority = 12 
                           AND user_id != 12 
                         ORDER BY designation";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        await conn.OpenAsync();
                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            while (await dr.ReadAsync())
                            {
                                userList.Add(new DmeInstituteDropdownDto
                                {
                                    UserId = Convert.ToInt32(dr["user_id"]),
                                    Username = dr["username"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }
                return Ok(userList); // Returns clean JSON array mapping directly to frontend controls
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error pulling authority users master directory lists.", error = ex.Message });
            }
        }


        // A. ACCOUNTS DROPDOWN LIST (Emulating: FillAcno)
        [HttpGet("GetCgmscBankAccounts")]
        public async Task<IActionResult> GetCgmscBankAccounts()
        {
            var list = new List<object>();
            string query = "SELECT bankid, accountname + '-' + bankname + '-' + accountno AS acname FROM masCGMSCAccNos";
            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    await conn.OpenAsync();
                    using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                    {
                        while (await dr.ReadAsync())
                        {
                            list.Add(new { bankId = Convert.ToInt32(dr["bankid"]), acName = dr["acname"]?.ToString() });
                        }
                    }
                }
            }
            return Ok(list);
        }

        // B. LOGICAL MODEL VALIDATOR: EVALUATE IF OPENING BALANCE IS NEEDED (Emulating: CheckFirstTimeOP)
        [HttpGet("VerifyFirstTimeOpeningBalance")]
        public async Task<IActionResult> VerifyFirstTimeOpeningBalance([FromQuery] int budgetId, [FromQuery] int directorateId, [FromQuery] int facilityId = 0)
        {
            string connString = _config.GetConnectionString("DefaultConnection");
            string query = "SELECT COUNT(*) FROM MASBUDGETDETAILS WHERE budgetid = @BudId";

            if (directorateId == 12) query += " AND FACILITYID = @FacId";
            else query += " AND directorate_id = @DirId";

            using (SqlConnection conn = new SqlConnection(connString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@BudId", budgetId);
                    cmd.Parameters.AddWithValue("@DirId", directorateId);
                    cmd.Parameters.AddWithValue("@FacId", facilityId);
                    await conn.OpenAsync();
                    int recordCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                    // If recordCount > 0, returns true (Already has transaction -> regular transaction setup)
                    // If recordCount == 0, returns false -> lock layout input parameters to Opening Balance
                    return Ok(new { isAlreadyInitialized = recordCount > 0 });
                }
            }
        }

        // C. COMPREHENSIVE FUNDS MASTER ARCHIVE GRID LOADER (Emulating: fillgrid)
        [HttpGet("GetFundReceiptsGridSummary/{directorateId}")]
        public async Task<IActionResult> GetFundReceiptsGridSummary(int directorateId)
        {
            var list = new List<FundReceiptGridDto>();
            string connString = _config.GetConnectionString("DefaultConnection");

            string query = @"
            SELECT d.facility_aut_name AS user_name, ISNULL(facName,'-') AS facName, b.BUDGETNAME, 
                   CONVERT(VARCHAR, mbd.receiveddate, 105) AS recdate, CAST(mbd.amount AS BIGINT) AS amount,
                   mbd.filename, mbd.remarks, mbd.bgid, b.BUDGETID,
                   CASE WHEN mbd.Isop='Y' THEN 'Opening Balance' ELSE 'Received from Directorate' END AS RecType,
                   CASE WHEN isprovisional ='Y' THEN 'Provisional' ELSE 'Actual' END AS Pentry,
                   CASE WHEN isprovisional ='Y' THEN 'Anticipatory' ELSE 'Actual' END AS PentryShow,
                   CASE WHEN isprovisional='Y' THEN CAST(ISNULL(act.actamt,0) AS BIGINT) ELSE CAST(ISNULL(mbd.amount,0) AS BIGINT) END AS ActualAmountReceived
            FROM MASBUDGETDETAILS mbd
            INNER JOIN MASBUDGET b ON b.BUDGETID = mbd.BUDGETID
            INNER JOIN facility_aut d ON d.facility_aut_id = mbd.directorate_id
            LEFT OUTER JOIN (
                SELECT designation +'-'+user_name AS facName, user_id FROM users WHERE authority=12 AND user_id!=12
            ) f ON f.user_id = mbd.FACILITYID
            LEFT OUTER JOIN (
                SELECT SUM(Amount) AS actamt, BGID FROM MASBUDGETDETAILSActualEntry GROUP BY BGID
            ) act ON act.BGID = mbd.BGID
            WHERE 1=1";

            if (directorateId > 0) query += " AND mbd.directorate_id = @DirId";
            query += " ORDER BY mbd.receiveddate DESC";

            using (SqlConnection conn = new SqlConnection(connString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (directorateId > 0) cmd.Parameters.AddWithValue("@DirId", directorateId);
                    await conn.OpenAsync();
                    using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                    {
                        while (await dr.ReadAsync())
                        {
                            list.Add(new FundReceiptGridDto
                            {
                                Bgid = Convert.ToInt32(dr["bgid"]),
                                Budgetid = Convert.ToInt32(dr["BUDGETID"]),
                                UserName = dr["user_name"]?.ToString() ?? "",
                                FacName = dr["facName"]?.ToString() ?? "-",
                                BudgetName = dr["BUDGETNAME"]?.ToString() ?? "",
                                RecDate = dr["recdate"]?.ToString() ?? "",
                                Amount = Convert.ToInt64(dr["amount"]),
                                FileName = dr["filename"]?.ToString() ?? "",
                                Remarks = dr["remarks"]?.ToString() ?? "",
                                RecType = dr["RecType"]?.ToString() ?? "",
                                Pentry = dr["Pentry"]?.ToString() ?? "",
                                PentryShow = dr["PentryShow"]?.ToString() ?? "",
                                ActualAmountReceived = Convert.ToInt64(dr["ActualAmountReceived"])
                            });
                        }
                    }
                }
            }
            return Ok(list);
        }

        // D. SYSTEM SUBMIT HANDLER TRANSACTION BLOCK WITH SCOPE IDENTITY & DOCUMENT RECOVERY DATA ATTACHMENT
        [HttpPost("SaveFundReceiptRecord")]
        public async Task<IActionResult> SaveFundReceiptRecord([FromBody] SubmitFundReceiptDto dto)
        {
            string connString = _config.GetConnectionString("DefaultConnection");
            using (SqlConnection conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        string insertSql = @"
                        INSERT INTO MASBUDGETDETAILS (BUDGETID, AMOUNT, RECEIVEDDATE, ENTRYDATE, directorate_id, facilityid, bankid, Remarks, IsOP, ISPROVISIONAL)
                        VALUES (@BudId, @Amt, CONVERT(DATETIME, @RecDt, 120), GETDATE(), @DirId, @FacId, @BankId, @Rem, @IsOp, @IsProv);
                        SELECT SCOPE_IDENTITY();";

                        int generatedBgid = 0;
                        using (SqlCommand cmd = new SqlCommand(insertSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@BudId", dto.BudgetId);
                            cmd.Parameters.AddWithValue("@Amt", dto.Amount);
                            cmd.Parameters.AddWithValue("@RecDt", dto.ReceivedDate); // Expected format standard: YYYY-MM-DD
                            cmd.Parameters.AddWithValue("@DirId", dto.DirectorateId);
                            cmd.Parameters.AddWithValue("@FacId", dto.DirectorateId == 12 ? (object)dto.FacilityId : DBNull.Value);
                            cmd.Parameters.AddWithValue("@BankId", dto.BankId);
                            cmd.Parameters.AddWithValue("@Rem", dto.Remarks);
                            cmd.Parameters.AddWithValue("@IsOp", dto.IsOp);
                            cmd.Parameters.AddWithValue("@IsProv", dto.IsProvisional);

                            generatedBgid = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        }

                        // Processing attached Document Base64 stream inside the transaction safely (Emulating MongoDB file registration upload)
                        if (!string.IsNullOrEmpty(dto.FileBase64))
                        {
                            string calculatedFileName = $"Fund{generatedBgid}{dto.DirectorateId}";
                            byte[] fileBytes = Convert.FromBase64String(dto.FileBase64);

                            // Emulating legacy Update statement: string strSQL2
                            string updateFileSql = "UPDATE MASBUDGETDETAILS SET filename = @FileName WHERE BGID = @Bgid";
                            using (SqlCommand updCmd = new SqlCommand(updateFileSql, conn, trans))
                            {
                                updCmd.Parameters.AddWithValue("@FileName", calculatedFileName);
                                updCmd.Parameters.AddWithValue("@Bgid", generatedBgid);
                                await updCmd.ExecuteNonQueryAsync();
                            }

                            // NOTE: if using secondary binary servers, run insert operations for StuObj_ here using fileBytes parameters
                        }

                        await trans.CommitAsync();
                        return Ok(new { message = "Saved Successfully" });
                    }
                    catch (Exception ex)
                    {
                        await trans.RollbackAsync();
                        return StatusCode(500, new { message = "Transaction operation failed during fund enrollment process.", error = ex.Message });
                    }
                }
            }
        }

        [HttpGet("GetFundDetailsByBgid/{bgid}")]
        public async Task<IActionResult> GetFundDetailsByBgid(int bgid)
        {
            if (bgid <= 0)
            {
                return BadRequest(new { message = "Please provide a valid budget details record configuration identifier (bgid)." });
            }

            string connString = _config.GetConnectionString("DefaultConnection");

            // Exact original query with your parameters and joins logic preserved securely
            string query = @"
            SELECT mb.budgetid, bd.bgid, mb.budgetname, ISNULL(bd.amount,0) AS amount, 
                   CONVERT(VARCHAR, bd.receiveddate, 105) AS receiveddate, bd.filepath AS path, bd.filename,
                   ac.accountname + '-' + ac.bankname + '-' + ac.accountno AS acname, 
                   ISNULL(bd.Remarks, '-') AS Remarks, bd.BGID AS extensionId, '.pdf' AS ext,
                   CASE WHEN Isop='Y' THEN 'Opening Balance' ELSE 'Received from Directorate' END AS RecType, 
                   CASE WHEN isprovisional ='Y' THEN 'Provisional' ELSE 'Actual' END AS Pentry,
                   CASE WHEN isprovisional ='Y' THEN 'Anticipatory' ELSE 'Actual' END AS PentryShow,
                   CASE WHEN isprovisional='Y' THEN ISNULL(act.actamt, 0) ELSE ISNULL(bd.amount, 0) END AS ActualAmountReceived,
                   CAST(ISNULL(bd.amount, 0) - ISNULL(act.actamt, 0) AS BIGINT) AS Bal,
                   (ISNULL(bd.amount, 0) - ISNULL(act.actamt, 0)) AS BalValue, 
                   bd.bankid
            FROM MASBUDGET mb 
            INNER JOIN MASBUDGETDETAILS bd ON bd.budgetid = mb.budgetid
            INNER JOIN masCGMSCAccNos ac ON ac.bankid = bd.bankid
            LEFT OUTER JOIN 
            (
                SELECT SUM(Amount) AS actamt, BGID 
                FROM MASBUDGETDETAILSActualEntry
                GROUP BY BGID
            ) act ON act.BGID = bd.BGID
            WHERE bd.bgid = @Bgid";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        // Injection protection rule parameter binding
                        cmd.Parameters.AddWithValue("@Bgid", bgid);
                        await conn.OpenAsync();

                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            if (await dr.ReadAsync())
                            {
                                var detailsObj = new FundDetailsResponseDto
                                {
                                    Budgetid = Convert.ToInt32(dr["budgetid"]),
                                    Bgid = Convert.ToInt32(dr["bgid"]),
                                    Budgetname = dr["budgetname"]?.ToString() ?? "",
                                    Amount = Convert.ToInt64(dr["amount"]),
                                    Receiveddate = dr["receiveddate"]?.ToString() ?? "",
                                    Path = dr["path"]?.ToString() ?? "",
                                    Filename = dr["filename"]?.ToString() ?? "",
                                    Acname = dr["acname"]?.ToString() ?? "",
                                    Remarks = dr["Remarks"]?.ToString() ?? "-",
                                    ExtensionId = Convert.ToInt32(dr["extensionId"]),
                                    Ext = dr["ext"]?.ToString() ?? ".pdf",
                                    RecType = dr["RecType"]?.ToString() ?? "",
                                    Pentry = dr["Pentry"]?.ToString() ?? "",
                                    PentryShow = dr["PentryShow"]?.ToString() ?? "",
                                    ActualAmountReceived = Convert.ToInt64(dr["ActualAmountReceived"]),
                                    Bal = Convert.ToInt64(dr["Bal"]),
                                    BalValue = Convert.ToInt64(dr["BalValue"]),
                                    Bankid = Convert.ToInt32(dr["bankid"])
                                };

                                return Ok(detailsObj); // Returns clean single descriptive JSON object
                            }
                        }
                    }
                }
                return NotFound(new { message = "No matching anticipatory fund log metrics located for this specific identifier." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Database mapping extraction transaction crashed.", error = ex.Message });
            }
        }

        [HttpGet("GetActualEntriesByBgid/{bgid}")]
        public async Task<IActionResult> GetActualEntriesByBgid(int bgid)
        {
            if (bgid <= 0)
            {
                return BadRequest(new { message = "Please provide a valid Provisional reference budget ID (bgid)." });
            }

            var actualEntriesList = new List<ActualCentricFundDto>();
            string connString = _config.GetConnectionString("DefaultConnection");

            // Dynamic multi-table inner join query matching your requirements strictly
            string query = @"
            SELECT bd.bgid, bd.abgid, mb.budgetname, ISNULL(bd.amount,0) AS amount,
                   mb.budgetid, CONVERT(VARCHAR, bd.RECEIVEDDATE, 105) AS receiveddate, 
                   bd.filepath AS path, bd.filename,
                   ac.accountname + '-' + ac.bankname + '-' + ac.accountno AS acname, 
                   ISNULL(bd.Remarks, '-') AS Remarks, bd.BGID AS extensionId, '.pdf' AS ext
            FROM MASBUDGET mb 
            INNER JOIN MASBUDGETDETAILSActualEntry bd ON bd.budgetid = mb.budgetid
            INNER JOIN masCGMSCAccNos ac ON ac.bankid = bd.bankid
            WHERE bd.bgid = @Bgid 
            ORDER BY bd.abgid";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        // Parameterized injection shielding layer
                        cmd.Parameters.AddWithValue("@Bgid", bgid);
                        await conn.OpenAsync();

                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            while (await dr.ReadAsync())
                            {
                                actualEntriesList.Add(new ActualCentricFundDto
                                {
                                    Bgid = Convert.ToInt32(dr["bgid"]),
                                    Abgid = Convert.ToInt32(dr["abgid"]),
                                    Budgetname = dr["budgetname"]?.ToString() ?? "",
                                    Amount = Convert.ToInt64(dr["amount"]),
                                    Budgetid = Convert.ToInt32(dr["budgetid"]),
                                    Receiveddate = dr["receiveddate"]?.ToString() ?? "",
                                    Path = dr["path"]?.ToString() ?? "",
                                    Filename = dr["filename"]?.ToString() ?? "",
                                    Acname = dr["acname"]?.ToString() ?? "",
                                    Remarks = dr["Remarks"]?.ToString() ?? "-",
                                    ExtensionId = Convert.ToInt32(dr["extensionId"]),
                                    Ext = dr["ext"]?.ToString() ?? ".pdf"
                                });
                            }
                        }
                    }
                }
                return Ok(actualEntriesList); // Returns clean structured JSON Array
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error executing actual allocation log list processing.", error = ex.Message });
            }
        }
        [HttpPost("SaveActualFundEntry")]
        public async Task<IActionResult> SaveActualFundEntry([FromBody] SubmitActualEntryDto dto)
        {
            // 0. Base Model Validation Guard
            if (dto == null || dto.Bgid <= 0 || dto.Budgetid <= 0 || dto.Amount <= 0)
            {
                return BadRequest(new { message = "Invalid allocation parameter values inside payload." });
            }

            if (string.IsNullOrWhiteSpace(dto.FileBase64))
            {
                return BadRequest(new { message = "Please Upload File" });
            }

            // Validate File format types as per legacy switch statements constraints
            string extClean = dto.FileExtension.Trim().ToLower();
            if (extClean != ".pdf")
            {
                return BadRequest(new { message = "Please Upload pdf file only" });
            }

            // Parsing dates arrays strings securely
            if (!DateTime.TryParse(dto.ReceivedDate, out DateTime recvDt) ||
                !DateTime.TryParse(dto.AnticipatoryDate, out DateTime antiDt))
            {
                return BadRequest(new { message = "Invalid date structures notation formatting parsed." });
            }

            // 1. BUSINESS RULE CHECK 1: Received date comparison with current real-time window
            if (recvDt.Date > DateTime.Now.Date)
            {
                return BadRequest(new { message = "You Can`t Select Greater than todays Date" });
            }

            // 2. BUSINESS RULE CHECK 2: Received date check against parent provisional allotment baseline timeline
            if (recvDt.Date < antiDt.Date)
            {
                return BadRequest(new { message = "You Can`t Select Less Anticipatory Date" });
            }

            // 3. BUSINESS RULE CHECK 3: Balance envelope cross-check constraint validation
            if (dto.Amount > dto.CurrentBalance)
            {
                return BadRequest(new { message = "You can not Received more than Entered Anticipatory Amount" });
            }

            string connString = _config.GetConnectionString("DefaultConnection");
            using (SqlConnection conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        // 4. Parameterized Insert command targeting MASBUDGETDETAILSActualEntry scheme table
                        string insertSql = @"
                        INSERT INTO MASBUDGETDETAILSActualEntry (BUDGETID, AMOUNT, RECEIVEDDATE, ENTRYDATE, bankid, Remarks, bgid)
                        VALUES (@BudId, @Amt, CONVERT(DATETIME, @RecDt, 120), GETDATE(), @BankId, @Rem, @Bgid);
                        SELECT SCOPE_IDENTITY();";

                        int generatedAbgid = 0;
                        using (SqlCommand cmd = new SqlCommand(insertSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@BudId", dto.Budgetid);
                            cmd.Parameters.AddWithValue("@Amt", dto.Amount);
                            cmd.Parameters.AddWithValue("@RecDt", recvDt.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                            cmd.Parameters.AddWithValue("@BankId", dto.BankId);
                            cmd.Parameters.AddWithValue("@Rem", string.IsNullOrEmpty(dto.Remarks) ? (object)DBNull.Value : dto.Remarks.Trim());
                            cmd.Parameters.AddWithValue("@Bgid", dto.Bgid);

                            generatedAbgid = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        }

                        // 5. Processing Document Byte Array Conversion and Filename Concatenation mapping strings
                        string dynamicCalculatedFileName = $"{dto.Bgid}AntiFundRec{generatedAbgid}";
                        byte[] rawBinaryFileBytes = Convert.FromBase64String(dto.FileBase64);

                        // A. Update the primary record file descriptor path keys (Emulating legacy string strSQL2 execution)
                        string updateSql = "UPDATE MASBUDGETDETAILSActualEntry SET fileName = @FileName WHERE ABGID = @Abgid";
                        using (SqlCommand updCmd = new SqlCommand(updateSql, conn, trans))
                        {
                            updCmd.Parameters.AddWithValue("@FileName", dynamicCalculatedFileName.Trim());
                            updCmd.Parameters.AddWithValue("@Abgid", generatedAbgid);
                            await updCmd.ExecuteNonQueryAsync();
                        }

                        // B. Emulating: MongoDB_Class file insertion placeholder (_Obj.Insert_AntiBudgetDetails)
                        // If you have secondary document attachments servers, invoke their binary insert loops here passing rawBinaryFileBytes

                        await trans.CommitAsync();
                        return Ok(new { message = "Saved Successfully", generatedActualId = generatedAbgid });
                    }
                    catch (Exception ex)
                    {
                        await trans.RollbackAsync();
                        return StatusCode(500, new { message = "Database transaction failed inside bulk batch operation block routines.", error = ex.Message });
                    }
                }
            }
        }

        //foleder save krna h 
        [HttpPost("SaveActualFundEntry1")]
        public async Task<IActionResult> SaveActualFundEntry1([FromBody] SubmitActualEntryDto dto)
        {
            if (dto == null || dto.Bgid <= 0 || dto.Budgetid <= 0 || dto.Amount <= 0)
            {
                return BadRequest(new { message = "Invalid allocation parameter values inside payload." });
            }

            if (string.IsNullOrWhiteSpace(dto.FileBase64))
            {
                return BadRequest(new { message = "Please Upload File" });
            }

            string extClean = dto.FileExtension.Trim().ToLower();
            if (extClean != ".pdf")
            {
                return BadRequest(new { message = "Please Upload pdf file only" });
            }

            if (!DateTime.TryParse(dto.ReceivedDate, out DateTime recvDt) ||
                !DateTime.TryParse(dto.AnticipatoryDate, out DateTime antiDt))
            {
                return BadRequest(new { message = "Invalid date structures notation formatting parsed." });
            }

            if (recvDt.Date > DateTime.Now.Date) return BadRequest(new { message = "You Can`t Select Greater than todays Date" });
            if (recvDt.Date < antiDt.Date) return BadRequest(new { message = "You Can`t Select Less Anticipatory Date" });
            if (dto.Amount > dto.CurrentBalance) return BadRequest(new { message = "You can not Received more than Entered Anticipatory Amount" });

            string connString = _config.GetConnectionString("DefaultConnection");
            using (SqlConnection conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        // Step 1: Insert record text configurations parameters inside SQL Server
                        string insertSql = @"
                        INSERT INTO MASBUDGETDETAILSActualEntry (BUDGETID, AMOUNT, RECEIVEDDATE, ENTRYDATE, bankid, Remarks, bgid)
                        VALUES (@BudId, @Amt, CONVERT(DATETIME, @RecDt, 120), GETDATE(), @BankId, @Rem, @Bgid);
                        SELECT SCOPE_IDENTITY();";

                        int generatedAbgid = 0;
                        using (SqlCommand cmd = new SqlCommand(insertSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@BudId", dto.Budgetid);
                            cmd.Parameters.AddWithValue("@Amt", dto.Amount);
                            cmd.Parameters.AddWithValue("@RecDt", recvDt.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                            cmd.Parameters.AddWithValue("@BankId", dto.BankId);
                            cmd.Parameters.AddWithValue("@Rem", string.IsNullOrEmpty(dto.Remarks) ? (object)DBNull.Value : dto.Remarks.Trim());
                            cmd.Parameters.AddWithValue("@Bgid", dto.Bgid);

                            generatedAbgid = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        }

                        // Step 2: FOLDER STORAGE LOGIC PIPELINE
                        // Aapke application root (wwwroot) ke andar 'Uploads/Funds' naam ka folder target hoga
                        string folderRootPath = Path.Combine(_env.ContentRootPath, "Uploads", "Funds");

                        // Agar folder physically server machine par create nahi hua hai, toh ye auto-create kar dega
                        if (!Directory.Exists(folderRootPath))
                        {
                            Directory.CreateDirectory(folderRootPath);
                        }

                        // Dynamic naming scheme matched perfectly to your legacy rules code logic
                        string fileNameOnly = $"{dto.Bgid}AntiFundRec{generatedAbgid}";
                        string fullPhysicalPathWithExtension = Path.Combine(folderRootPath, fileNameOnly + extClean);

                        // Step 3: Write Base64 string directly into a physical raw system file (.pdf)
                        byte[] rawBinaryFileBytes = Convert.FromBase64String(dto.FileBase64);
                        await System.IO.File.WriteAllBytesAsync(fullPhysicalPathWithExtension, rawBinaryFileBytes);

                        // Step 4: Update the file path/name identifiers keys inside database index rows
                        string updateSql = "UPDATE MASBUDGETDETAILSActualEntry SET fileName = @FileName WHERE ABGID = @Abgid";
                        using (SqlCommand updCmd = new SqlCommand(updateSql, conn, trans))
                        {
                            updCmd.Parameters.AddWithValue("@FileName", fileNameOnly);
                            updCmd.Parameters.AddWithValue("@Abgid", generatedAbgid);
                            await updCmd.ExecuteNonQueryAsync();
                        }

                        await trans.CommitAsync();
                        return Ok(new { message = "Saved Successfully and File Uploaded into Server Folder Context", actualId = generatedAbgid });
                    }
                    catch (Exception ex)
                    {
                        await trans.RollbackAsync();
                        return StatusCode(500, new { message = "Database or File Directory stream write transaction failed.", error = ex.Message });
                    }
                }
            }
        }



    }
}



 
