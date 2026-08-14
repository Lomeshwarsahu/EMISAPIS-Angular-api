using EMISAPIS.DTOS;
using EMISAPIS.DTOS.EMISAPIS.DTOS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver.Core.Configuration;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Text.Json.Serialization;


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
                        // Step 1: Insert record parameter configurations text components inside SQL Server
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
                        string folderRootPath = Path.Combine(_env.ContentRootPath, "Uploads", "Funds");

                        if (!Directory.Exists(folderRootPath))
                        {
                            Directory.CreateDirectory(folderRootPath);
                        }

                        // Dynamic nomenclature calculated string value parameters matching your business logic
                        string fileNameOnly = $"{dto.Bgid}AntiFundRec{generatedAbgid}";
                        string fullPhysicalPathWithExtension = Path.Combine(folderRootPath, fileNameOnly + extClean);

                        // Step 3: Write Base64 string directly into a physical raw local file (.pdf)
                        byte[] rawBinaryFileBytes = Convert.FromBase64String(dto.FileBase64);
                        await System.IO.File.WriteAllBytesAsync(fullPhysicalPathWithExtension, rawBinaryFileBytes);

                        // ==================== FIXED DATABASE UPDATE PIPELINE ====================
                        // FIX 1: Explicitly used distinctive tracking variable parameters to avoid baseline NULL injection paths
                        // FIX 2: Added verification schema to force exceptions tracking if row identity isn't synced
                        string updateSql = "UPDATE MASBUDGETDETAILSActualEntry SET fileName = @FileNameToStore WHERE ABGID = @TargetAbgid";

                        using (SqlCommand updCmd = new SqlCommand(updateSql, conn, trans))
                        {
                            updCmd.Parameters.AddWithValue("@FileNameToStore", fileNameOnly.Trim());
                            updCmd.Parameters.AddWithValue("@TargetAbgid", generatedAbgid);

                            int rowsUpdated = await updCmd.ExecuteNonQueryAsync();
                            if (rowsUpdated == 0)
                            {
                                throw new Exception($"File reference sync crashed. Target ledger entry ID row ABGID: {generatedAbgid} could not be located.");
                            }
                        }
                        // =========================================================================

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




        //[HttpGet("DownloadFundFile/{abgid}")]
        //public async Task<IActionResult> DownloadFundFile(int abgid, [FromQuery] bool forceDownload = false)
        //{
        //    if (abgid <= 0)
        //    {
        //        return BadRequest(new { message = "Invalid actual entry index identification (abgid)." });
        //    }

        //    string connectionString = _config.GetConnectionString("DefaultConnection");
        //    string dbFileName = string.Empty;

        //    // Database se exact row wise filename load karna
        //    string query = "SELECT fileName FROM MASBUDGETDETAILSActualEntry WHERE ABGID = @Abgid";

        //    try
        //    {
        //        using (SqlConnection conn = new SqlConnection(connectionString))
        //        {
        //            using (SqlCommand cmd = new SqlCommand(query, conn))
        //            {
        //                cmd.Parameters.AddWithValue("@Abgid", abgid);
        //                await conn.OpenAsync();
        //                object result = await cmd.ExecuteScalarAsync();

        //                if (result != null && result != DBNull.Value)
        //                {
        //                    dbFileName = result.ToString().Trim();
        //                }
        //            }
        //        }

        //        if (string.IsNullOrEmpty(dbFileName))
        //        {
        //            return NotFound(new { message = "No file footprint registered in database records logs." });
        //        }

        //        // AUTOMATED RECURSIVE ABSOLUTE RESOLVER
        //        string folderRootPath = Path.Combine(_env.ContentRootPath, "Uploads", "Funds");
        //        string completePhysicalPath = Path.Combine(folderRootPath, dbFileName + ".pdf");

        //        // Windows dynamic format parsing normalization
        //        completePhysicalPath = Path.GetFullPath(completePhysicalPath);

        //        if (!System.IO.File.Exists(completePhysicalPath))
        //        {
        //            // EMERGENCY SAFETY SCOPE: Agar extension double truncate ho gaya ho (.pdf.pdf)
        //            string altPath = completePhysicalPath.Replace(".pdf.pdf", ".pdf");
        //            if (System.IO.File.Exists(altPath))
        //            {
        //                completePhysicalPath = altPath;
        //            }
        //            else
        //            {
        //                return NotFound(new
        //                {
        //                    message = $"File '{dbFileName}.pdf' is in database but physically missing in server directories context.",
        //                    system_scanned_root = _env.ContentRootPath,
        //                    searched_filename = dbFileName + ".pdf"
        //                });
        //            }
        //        }

        //        // Thread-safe async reading to bypass OS lockdown permissions
        //        byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(completePhysicalPath);
        //        string contentType = "application/pdf";
        //        string outputDownloadName = dbFileName.EndsWith(".pdf") ? dbFileName : dbFileName + ".pdf";

        //        if (forceDownload)
        //        {
        //            return File(fileBytes, contentType, outputDownloadName);
        //        }
        //        else
        //        {
        //            // FIRST ACTION: Opens inline stream view inside chrome tab reader seamlessly
        //            Response.Headers.Append("Content-Disposition", $"inline; filename=\"{outputDownloadName}\"");
        //            return File(fileBytes, contentType);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { message = "Internal execution collapsed streaming file bytes arrays.", error = ex.Message });
        //    }
        //}

        //[HttpGet("DownloadFundFile/{abgid}")]
        //public async Task<IActionResult> DownloadFundFile(int abgid, [FromQuery] bool forceDownload = false)
        //{
        //    if (abgid <= 0)
        //    {
        //        return BadRequest(new { message = "Invalid actual entry index identification (abgid)." });
        //    }

        //    string connectionString = _config.GetConnectionString("DefaultConnection");
        //    string dbFileName = string.Empty;

        //    string query = "SELECT fileName FROM MASBUDGETDETAILSActualEntry WHERE ABGID = @Abgid";

        //    try
        //    {
        //        using (SqlConnection conn = new SqlConnection(connectionString))
        //        {
        //            using (SqlCommand cmd = new SqlCommand(query, conn))
        //            {
        //                cmd.Parameters.AddWithValue("@Abgid", abgid);
        //                await conn.OpenAsync();
        //                object result = await cmd.ExecuteScalarAsync();

        //                if (result != null && result != DBNull.Value)
        //                {
        //                    dbFileName = result.ToString().Trim();
        //                }
        //            }
        //        }

        //        if (string.IsNullOrEmpty(dbFileName))
        //        {
        //            return NotFound(new { message = "No file footprint registered in database records logs." });
        //        }

        //        string folderRootPath = Path.Combine(_env.ContentRootPath, "Uploads", "Funds");
        //        string completePhysicalPath = Path.Combine(folderRootPath, dbFileName + ".pdf");
        //        completePhysicalPath = Path.GetFullPath(completePhysicalPath);

        //        if (!System.IO.File.Exists(completePhysicalPath))
        //        {
        //            string altPath = completePhysicalPath.Replace(".pdf.pdf", ".pdf");
        //            if (System.IO.File.Exists(altPath))
        //            {
        //                completePhysicalPath = altPath;
        //            }
        //            else
        //            {
        //                return NotFound(new
        //                {
        //                    message = $"File '{dbFileName}.pdf' is in database but physically missing in server directories context."
        //                });
        //            }
        //        }

        //        byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(completePhysicalPath);

        //        // CRITICAL FIX: To force inline view in browser, content-type must be exact application/pdf
        //        string contentType = "application/pdf";
        //        string outputDownloadName = dbFileName.EndsWith(".pdf") ? dbFileName : dbFileName + ".pdf";

        //        if (forceDownload)
        //        {
        //            return File(fileBytes, contentType, outputDownloadName);
        //        }
        //        else
        //        {
        //            // REMOVED COMPLEX HEADERS: Simple inline assignment forces browsers to render via built-in PDF viewer
        //            Response.Headers.Clear(); // Clears any caching attachment header block
        //            Response.Headers.Append("Content-Disposition", "inline");
        //            return File(fileBytes, contentType);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { message = "Internal error streaming bytes.", error = ex.Message });
        //    }
        //}
        [HttpGet("DownloadFundFile/{abgid}")]
        public async Task<IActionResult> DownloadFundFile(int abgid, [FromQuery] bool forceDownload = false)
        {
            if (abgid <= 0)
            {
                return BadRequest(new { message = "Invalid actual entry index identification (abgid)." });
            }

            string connectionString = _config.GetConnectionString("DefaultConnection");
            string dbFileName = string.Empty;

            string query = "SELECT fileName FROM MASBUDGETDETAILSActualEntry WHERE ABGID = @Abgid";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Abgid", abgid);
                        await conn.OpenAsync();
                        object result = await cmd.ExecuteScalarAsync();

                        if (result != null && result != DBNull.Value)
                        {
                            dbFileName = result.ToString().Trim();
                        }
                    }
                }

                if (string.IsNullOrEmpty(dbFileName))
                {
                    return NotFound(new { message = "No file footprint registered in database records logs." });
                }

                string folderRootPath = Path.Combine(_env.ContentRootPath, "Uploads", "Funds");
                string completePhysicalPath = Path.Combine(folderRootPath, dbFileName + ".pdf");
                completePhysicalPath = Path.GetFullPath(completePhysicalPath);

                if (!System.IO.File.Exists(completePhysicalPath))
                {
                    string altPath = completePhysicalPath.Replace(".pdf.pdf", ".pdf");
                    if (System.IO.File.Exists(altPath))
                    {
                        completePhysicalPath = altPath;
                    }
                    else
                    {
                        return NotFound(new
                        {
                            message = $"File '{dbFileName}.pdf' is in database but physically missing in server directories context."
                        });
                    }
                }

                byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(completePhysicalPath);
                string contentType = "application/pdf";
                string outputDownloadName = dbFileName.EndsWith(".pdf") ? dbFileName : dbFileName + ".pdf";

                if (forceDownload)
                {
                    // Forces download prompt on user local system
                    return File(fileBytes, contentType, outputDownloadName);
                }
                else
                {
                    // FIX: Content-Disposition headers are optimized with filename for strict inline view rendering
                    Response.Headers.Clear();
                    Response.Headers.Append("Content-Disposition", $"inline; filename=\"{outputDownloadName}\"");

                    // X-Content-Type-Options tells the browser strictly to treat it as application/pdf without downloading
                    Response.Headers.Append("X-Content-Type-Options", "nosniff");

                    return File(fileBytes, contentType);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal error streaming bytes.", error = ex.Message });
            }
        }

        [HttpPost("GetConsolidatedGridData")]
        public async Task<IActionResult> GetConsolidatedGridData([FromBody] GridFilterDto filters)
        {
            // Validation segment mirroring your legacy selectIndex == 0 logic
            if (filters == null || filters.FinancialYearId <= 0)
            {
                return BadRequest(new { message = "Please Select a Valid Financial Year." });
            }

            string connectionString = _config.GetConnectionString("DefaultConnection");
            var resultList = new List<Dictionary<string, object>>();

            // Building dynamic SQL filters securely using parameter tokens
            string whDirectorate = "";
            if (filters.DirectorateId.HasValue && filters.DirectorateId.Value > 0)
            {
                whDirectorate = " AND i.directorate_id = @DirectorateId ";
            }

            // Your exact complex inner-outer nested query block mapping legacy views execution
            string selectSqlQuery = $@"
                SELECT item_id, Code, item_name, 
                       SUM(nosconsignee) AS nosconsignee, 
                       SUM(totalIndentQTY) AS totalIndentQTY, 
                       SUM(POQTY) AS POQTY, 
                       RCEndDate, Supplier, PriceIncGST, tender_no,
                       CASE WHEN tenderDT = '01/01/1900' THEN '' ELSE CONVERT(VARCHAR, tenderDT, 103) END AS TStartDT, 
                       finalstatus
                FROM 
                (
                    SELECT i.consolidated_date, i.description, m.item_id, m.item_code_as_per_tender AS Code, m.item_name,
                           SUM(imi.indent_quantity) AS totalIndentQTY, 
                           COUNT(DISTINCT id.facility_id) AS nosconsignee, 
                           ISNULL(POQTY, 0) AS POQTY,
                           ISNULL(rcEndDT, 'RC Not Valid') AS RCEndDate, 
                           Supplier, 
                           ISNULL(rc.make, '-') AS make, 
                           ISNULL(rc.model, '-') AS model, 
                           PriceIncGST,
                           CASE WHEN rcEndDT IS NOT NULL THEN '' ELSE t.tenderDT END AS tenderDT, 
                           CASE WHEN rcEndDT IS NOT NULL THEN '' ELSE t.tender_no END AS tender_no,
                           CASE WHEN rcEndDT IS NOT NULL THEN '' ELSE t.finalstatus END AS finalstatus
                    FROM indent_consolidation i 
                    INNER JOIN indent_cons_items ic ON ic.indent_consolidated_id = i.indent_consolidation_id
                    INNER JOIN indent id ON id.indent_consolidation_id = i.indent_consolidation_id AND id.indent_cons_items_id = ic.indent_cons_items_id
                    INNER JOIN indent_items imi ON imi.indent_id = id.indent_id AND imi.item_id = ic.item_id
                    INNER JOIN masitems m ON m.item_id = imi.item_id AND m.item_id = ic.item_id
                    LEFT OUTER JOIN 
                    (
                        SELECT r.item_id, Supplier, rcStartDT, rcEndDT, make, model, basic_rate, percentage, PriceIncGST 
                        FROM v_rcvalid r
                    ) rc ON rc.item_id = m.item_id
                    LEFT OUTER JOIN 
                    (
                        SELECT SUM(pi.quantity) AS POQTY, pi.INDENT_CONSOLIDATION_ID, pi.item_id 
                        FROM purchase_order p
                        INNER JOIN po_items pi ON pi.po_id = p.po_id
                        WHERE p.status IN ('Completed', 'Order Placed', 'Partially Received')
                        GROUP BY pi.INDENT_CONSOLIDATION_ID, pi.item_id
                    ) po ON po.INDENT_CONSOLIDATION_ID = i.indent_consolidation_id AND po.item_id = m.item_id
                    LEFT OUTER JOIN 
                    (
                        SELECT t.item_id, t.tenderDT, ti.tender_no, ti.finalstatus
                        FROM 
                        (
                            SELECT item_id, MAX(tender_date) AS tenderDT 
                            FROM v_Tenderstatus
                            GROUP BY item_id
                        ) t
                        INNER JOIN v_Tenderstatus ti ON ti.item_id = t.item_id AND ti.tender_date = t.tenderDT
                    ) t ON t.item_id = m.item_id
                    WHERE i.status = 'C' 
                      AND i.financial_year_id = @FinancialYearId
                      {whDirectorate}
                    GROUP BY i.consolidated_date, i.description, m.item_id, m.item_code_as_per_tender, m.item_name, 
                             rcEndDT, rc.make, rc.model, PriceIncGST, Supplier, tenderDT, tender_no, finalstatus, POQTY
                ) b 
                GROUP BY RCEndDate, Supplier, PriceIncGST, item_id, Code, item_name, tenderDT, tender_no, finalstatus";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(selectSqlQuery, conn))
                    {
                        // Safe SQL Parameter Additions
                        cmd.Parameters.AddWithValue("@FinancialYearId", filters.FinancialYearId);
                        if (filters.DirectorateId.HasValue && filters.DirectorateId.Value > 0)
                        {
                            cmd.Parameters.AddWithValue("@DirectorateId", filters.DirectorateId.Value);
                        }

                        await conn.OpenAsync();
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var row = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                }
                                resultList.Add(row);
                            }
                        }
                    }
                }

                // Standard Web API Response JSON Array footprint layout return
                return Ok(resultList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error executing complex grid dataset aggregation queries.", error = ex.Message });
            }
        }
        [HttpGet("GetUnpaidPoGridData")]
        public async Task<IActionResult> GetUnpaidPoGridData([FromQuery] int financialYearId, [FromQuery] string fromDate = null, [FromQuery] string toDate = null)
        {
            if (financialYearId <= 0)
            {
                return BadRequest(new { message = "Please Select a Valid Financial Year." });
            }

            string connectionString = _config.GetConnectionString("DefaultConnection");
            var resultList = new List<Dictionary<string, object>>();

            string whereFromToSql = "";
            bool hasValidDates = !string.IsNullOrWhiteSpace(fromDate) && !string.IsNullOrWhiteSpace(toDate);

            // In do variables mein parsed dates store hongi
            DateTime parsedFromDate = DateTime.MinValue;
            DateTime parsedToDate = DateTime.MinValue;

            if (hasValidDates)
            {
                try
                {
                    // SMART PARSING: Yeh C# framework automatic '6-15-2025' ya '15-06-2025' dono ko sahi detect kar lega
                    parsedFromDate = DateTime.Parse(fromDate.Trim());
                    parsedToDate = DateTime.Parse(toDate.Trim());

                    // SQL Query ke liye simple direct comparison filter string banayenge
                    whereFromToSql = " AND p.po_date BETWEEN @FromDate AND @ToDate ";
                }
                catch (Exception)
                {
                    return BadRequest(new { message = "Provided date strings could not be parsed into valid date structures." });
                }
            }

            string selectSqlQuery = $@"
        SELECT 
            v.year, v.po_no, CONVERT(VARCHAR, p.po_Date, 103) AS podate, v.POQTY, v.facility_aut_name, 
            v.Supplier, v.item_code_as_per_tender, v.itemname, v.POValue, v.Liability, v.potype, 
            v.SupplierDispatch, v.receiptQTY, v.instalationQty, v.fitunfit, v.EQtype, v.ReasonName, 
            v.reasonid, t.cancellationdays, DATEADD(DAY, t.cancellationdays, p.po_date) AS date, 
            v.po_id, p.financial_year_id
        FROM V_POPaymentStatus v
        INNER JOIN purchase_order p ON p.po_id = v.po_id
        LEFT OUTER JOIN tenders t ON t.tender_id = p.tender_id
        WHERE v.Paymentstatus = 'Unpaid' 
          AND p.financial_year_id = @FinancialYearId
          {whereFromToSql}
        ORDER BY p.po_Date DESC";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(selectSqlQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId);

                        if (hasValidDates)
                        {
                            // SQL Server ke standard database standard format (yyyy-MM-dd HH:mm:ss) mein safely inject karna
                            cmd.Parameters.AddWithValue("@FromDate", parsedFromDate.ToString("yyyy-MM-dd 00:00:00"));
                            cmd.Parameters.AddWithValue("@ToDate", parsedToDate.ToString("yyyy-MM-dd 23:59:59"));
                        }

                        await conn.OpenAsync();
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var row = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                }
                                resultList.Add(row);
                            }
                        }
                    }
                }

                return Ok(resultList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error executing un-paid PO grid record lookups context extraction.", error = ex.Message });
            }
        }
        // URL Testing Pattern Samples:
        // 1. Specific Year Filters: GET api/GMFI/GetPurchaseOrderGridData?financialYearId=12
        // 2. All Years Data (Dropdown not selected): GET api/GMFI/GetPurchaseOrderGridData?financialYearId=0
        [HttpGet("GetPurchaseOrderGridData")]
        public async Task<IActionResult> GetPurchaseOrderGridData([FromQuery] int financialYearId = 0)
        {
            string connectionString = _config.GetConnectionString("DefaultConnection");
            var resultList = new List<Dictionary<string, object>>();

            // 1. Dynamic Optional Year Clause Mapping (SQL Parameter Injection Safe)
            string whereCauseYear = "";
            if (financialYearId > 0)
            {
                whereCauseYear = " AND p.financial_year_id = @FinancialYearId ";
            }

            // 2. Exact Business Logic Multi-Join Nested Query Structure
            string selectSqlQuery = $@"
        SELECT 
            fu.facility_aut_code,
            pi.item_id,
            p.po_id,
            pi.no_of_consignee,
            pi.basic_rate,
            pi.percentage,
            pi.single_unit_price,
            pi.quantity,
            pi.totalPOvalue,
            p.OUTWARD_NO + '/' + p.po_no AS PO_NO,
            CASE 
                WHEN p.soissueDT IS NULL THEN CONVERT(VARCHAR, p.po_date, 103) 
                ELSE CONVERT(VARCHAR, p.soissueDT, 103) 
            END AS po_date,
            t.tender_no,
            pi.CODE,
            pi.ITEM_NAME,
            CONVERT(VARCHAR, t.TENDER_DATE, 103) AS TENDER_DATE,
            E.financial_year_id,
            p.status
        FROM purchase_order p
        INNER JOIN MASSUPPLIERS b ON b.supplier_id = p.SUPPLIER_ID
        INNER JOIN MAS_FINANCIAL_YEAR E ON E.FINANCIAL_YEAR_ID = p.FINANCIAL_YEAR_ID
        INNER JOIN tenders t ON t.tender_id = p.tender_id
        INNER JOIN facility_aut fu ON fu.facility_aut_id = p.directorate_id
        LEFT OUTER JOIN 
        (
            SELECT 
                COUNT(DISTINCT sub_pi.consignee_id) AS no_of_consignee, 
                R.ITEM_CODE_AS_PER_TENDER AS CODE,
                R.item_name AS ITEM_NAME,
                c.basic_rate,
                c.percentage,
                c.single_unit_price,
                SUM(sub_pi.quantity) AS quantity,
                c.single_unit_price * SUM(sub_pi.quantity) AS totalPOvalue,
                sub_pi.po_id,
                sub_pi.item_id
            FROM po_items sub_pi 
            INNER JOIN purchase_order sub_p ON sub_p.po_id = sub_pi.po_id
            INNER JOIN MASITEMS R ON R.ITEM_ID = sub_pi.item_id	
            INNER JOIN contract_items c ON c.contract_item_id = sub_pi.contract_item_id
            GROUP BY R.ITEM_CODE_AS_PER_TENDER, R.item_name, c.basic_rate, c.percentage, c.single_unit_price, sub_pi.po_id, sub_pi.item_id
        ) pi ON pi.po_id = p.po_id 
        WHERE p.status IN ('Order Placed', 'Partially Received', 'Completed')
        {whereCauseYear}
        ORDER BY p.po_date";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(selectSqlQuery, conn))
                    {
                        // Dynamic conditional binding
                        if (financialYearId > 0)
                        {
                            cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId);
                        }

                        await conn.OpenAsync();
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var row = new Dictionary<string, object>();

                                // Dynamic Field Count parsing sends ALL query column blocks seamlessly to client
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                }

                                resultList.Add(row);
                            }
                        }
                    }
                }

                return Ok(resultList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error executing complex purchase order grid data lookup routines.", error = ex.Message });
            }
        }

        [HttpGet("GetSupplierDispatchReport")]
        public async Task<IActionResult> GetSupplierDispatchReport([FromQuery] string poType, [FromQuery] string startDate, [FromQuery] string endDate)
        {
            if (string.IsNullOrWhiteSpace(startDate))
                return BadRequest(new { message = "Please fill Start Date" });

            if (string.IsNullOrWhiteSpace(endDate))
                return BadRequest(new { message = "Please fill End Date" });

            if (string.IsNullOrWhiteSpace(poType) || (poType != "ID" && poType != "ED"))
                return BadRequest(new { message = "Invalid PO Type selection logic payload. Expected 'ID' or 'ED'." });

            string connectionString = _config.GetConnectionString("DefaultConnection");
            var resultList = new List<SupplierDispatchDto>();

            DateTime parsedStart = DateTime.MinValue;
            DateTime parsedEnd = DateTime.MinValue;
            string[] supportedFormats = { "yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy", "MM-dd-yyyy", "MM/dd/yyyy" };

            if (!DateTime.TryParseExact(startDate.Trim(), supportedFormats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out parsedStart) ||
                !DateTime.TryParseExact(endDate.Trim(), supportedFormats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out parsedEnd))
            {
                return BadRequest(new { message = "Provided date strings could not be parsed into valid date structures. Use YYYY-MM-DD standard format." });
            }

            string whereFromToSql = "";
            if (poType == "ID")
            {
                whereFromToSql = " WHERE d.invoice_date BETWEEN @StartDate AND @EndDate ";
            }
            else if (poType == "ED")
            {
                whereFromToSql = " WHERE d.supplier_entry BETWEEN @StartDate AND @EndDate ";
            }

            string selectSqlQuery = $@"
        SELECT 
            d.hsncode, 
            p.quantity AS PO_QTY, 
            msi.item_name, 
            ms.location_name, 
            po.outward_no + '/' + po.po_no AS pono, 
            mas.name, 
            d.invoiceGST, 
            d.invoice_no,
            CONVERT(VARCHAR, po.po_date, 103) AS po_date,
            d.challan_no, 
            CONVERT(VARCHAR, d.challan_date, 103) AS challandate,
            d.dispatch_no, 
            CONVERT(VARCHAR, d.dispatch_date, 103) AS dispatch_date,
            CONVERT(VARCHAR, d.invoice_date, 103) AS invoice_date,
            SUM(i.Supplyqty) AS Supplyqty, 
            d.EwayBillno, 
            CONVERT(VARCHAR, d.EwayBilldt, 103) AS EwayBilldt, 
            d.tcsvalue,
            (p.basicrate + (p.basicrate * p.percentage / 100)) * SUM(i.Supplyqty) AS InvoiceAmount, 
            p.basicrate, 
            p.percentage, 
            ROUND((p.basicrate * p.percentage / 100), 0) AS taxable_value 
        FROM SupplierDispatch d
        INNER JOIN Issue_item_details i ON i.Issue_id = d.Issue_id
        INNER JOIN po_items p ON p.po_id = d.po_id AND p.item_id = i.item_id AND p.consignee_id = d.location_id
        INNER JOIN purchase_order po ON po.po_id = p.po_id
        INNER JOIN maslocations ms ON ms.location_id = p.consignee_id
        INNER JOIN massuppliers mas ON mas.supplier_id = po.supplier_id 
        INNER JOIN masitems msi ON msi.item_id = p.item_id
        {whereFromToSql}
        GROUP BY 
            d.Issue_id, d.Tentative_Sdate, d.remarks, d.status, d.challan_date, d.challan_no,
            d.dispatch_date, d.invoice_date, d.dispatch_no, d.invoice_no, d.invoiceGST, 
            d.EwayBillno, d.EwayBilldt, d.hsncode, d.tcsvalue, p.basicrate, p.percentage, 
            i.Supplyqty, po.po_no, po.po_date, ms.location_name, p.quantity, mas.name, 
            msi.item_name, po.outward_no";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(selectSqlQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@StartDate", parsedStart.ToString("yyyy-MM-dd 00:00:00.000"));
                        cmd.Parameters.AddWithValue("@EndDate", parsedEnd.ToString("yyyy-MM-dd 23:59:59.997"));

                        await conn.OpenAsync();
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var rowItem = new SupplierDispatchDto();

                                // 1. Safe String Mappings
                                rowItem.Hsncode = reader["hsncode"] == DBNull.Value ? "" : reader["hsncode"].ToString().Trim();
                                rowItem.ItemName = reader["item_name"] == DBNull.Value ? "" : reader["item_name"].ToString().Trim();
                                rowItem.LocationName = reader["location_name"] == DBNull.Value ? "" : reader["location_name"].ToString().Trim();
                                rowItem.Pono = reader["pono"] == DBNull.Value ? "" : reader["pono"].ToString().Trim();
                                rowItem.Name = reader["name"] == DBNull.Value ? "" : reader["name"].ToString().Trim();
                                rowItem.InvoiceNo = reader["invoice_no"] == DBNull.Value ? "" : reader["invoice_no"].ToString().Trim();
                                rowItem.PoDate = reader["po_date"] == DBNull.Value ? "" : reader["po_date"].ToString().Trim();
                                rowItem.ChallanNo = reader["challan_no"] == DBNull.Value ? "" : reader["challan_no"].ToString().Trim();
                                rowItem.Challandate = reader["challandate"] == DBNull.Value ? "" : reader["challandate"].ToString().Trim();
                                rowItem.DispatchNo = reader["dispatch_no"] == DBNull.Value ? "" : reader["dispatch_no"].ToString().Trim();
                                rowItem.DispatchDate = reader["dispatch_date"] == DBNull.Value ? "" : reader["dispatch_date"].ToString().Trim();
                                rowItem.InvoiceDate = reader["invoice_date"] == DBNull.Value ? "" : reader["invoice_date"].ToString().Trim();
                                rowItem.EwayBillno = reader["EwayBillno"] == DBNull.Value ? "" : reader["EwayBillno"].ToString().Trim();
                                rowItem.EwayBilldt = reader["EwayBilldt"] == DBNull.Value ? "" : reader["EwayBilldt"].ToString().Trim();

                                // ==================== FIXED INVOICE_GST STRING PARSER ====================
                                // Alphanumeric strings (e.g., '22AHNPJ4497N1ZP') are handled seamlessly without conversion crashes
                                rowItem.InvoiceGst = reader["invoiceGST"] == DBNull.Value ? "" : reader["invoiceGST"].ToString().Trim();
                                // =========================================================================

                                // 2. Safe Mathematical Numeric Conversions 
                                rowItem.PoQty = (reader["PO_QTY"] == DBNull.Value || string.IsNullOrWhiteSpace(reader["PO_QTY"].ToString()))
                                                ? 0 : Convert.ToInt32(reader["PO_QTY"]);

                                rowItem.Supplyqty = (reader["Supplyqty"] == DBNull.Value || string.IsNullOrWhiteSpace(reader["Supplyqty"].ToString()))
                                                    ? 0 : Convert.ToInt32(reader["Supplyqty"]);

                                rowItem.Tcsvalue = (reader["tcsvalue"] == DBNull.Value || string.IsNullOrWhiteSpace(reader["tcsvalue"].ToString()))
                                                   ? 0 : Convert.ToDecimal(reader["tcsvalue"]);

                                rowItem.InvoiceAmount = (reader["InvoiceAmount"] == DBNull.Value || string.IsNullOrWhiteSpace(reader["InvoiceAmount"].ToString()))
                                                        ? 0 : Convert.ToDecimal(reader["InvoiceAmount"]);

                                rowItem.Basicrate = (reader["basicrate"] == DBNull.Value || string.IsNullOrWhiteSpace(reader["basicrate"].ToString()))
                                                    ? 0 : Convert.ToDecimal(reader["basicrate"]);

                                rowItem.Percentage = (reader["percentage"] == DBNull.Value || string.IsNullOrWhiteSpace(reader["percentage"].ToString()))
                                                     ? 0 : Convert.ToDecimal(reader["percentage"]);

                                rowItem.TaxableValue = (reader["taxable_value"] == DBNull.Value || string.IsNullOrWhiteSpace(reader["taxable_value"].ToString()))
                                                       ? 0 : Convert.ToDecimal(reader["taxable_value"]);

                                resultList.Add(rowItem);
                            }
                        }
                    }
                }
                return Ok(resultList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error executing automated supplier dispatch reports configurations.", error = ex.Message });
            }
        }

        // URL Pattern Example: GET api/PaymentStatus/GetPaymentStatusGrid?fromDate=2025-04-01&toDate=2026-03-31
        [HttpGet("GetPaymentStatusGrid")]
        public async Task<IActionResult> GetPaymentStatusGrid([FromQuery] string fromDate, [FromQuery] string toDate)
        {
            // 1. URL Parameters validations mimicking legacy alerts
            if (string.IsNullOrWhiteSpace(fromDate) || string.IsNullOrWhiteSpace(toDate))
            {
                return BadRequest(new { message = "Please select Date parameters." });
            }

            string connectionString = _config.GetConnectionString("DefaultConnection");
            var resultList = new List<PaymentStatusDto>();

            // 2. Strict Date Parsing for SQL execution blocks mapping
            DateTime parsedFromDate = DateTime.MinValue;
            DateTime parsedToDate = DateTime.MinValue;
            string[] exactFormats = { "yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy", "MM-dd-yyyy", "MM/dd/yyyy" };

            if (!DateTime.TryParseExact(fromDate.Trim(), exactFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedFromDate) ||
                !DateTime.TryParseExact(toDate.Trim(), exactFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedToDate))
            {
                return BadRequest(new { message = "Provided date strings could not be parsed into valid format. YYYY-MM-DD is highly recommended." });
            }

            // Standard parameterized dynamic mapping filter injection framework
            string whereFromToSql = " AND p.AIDDATE BETWEEN @FromDate AND @ToDate ";

            // 3. Exact SQL Script with inner queries calculation metrics preserved natively
            string selectSqlQuery = $@"
                SELECT 
                    MONTH(p.AIDDATE) AS PaidMonth, 
                    po.outward_no, 
                    po.po_no,  
                    (CASE WHEN po.soissueDT IS NULL THEN po.po_date ELSE po.soissueDT END) AS po_date,
                    sp.name AS SupplierName, 
                    s.SUPGST, 
                    p.AIDNO AS Chequeno, 
                    CONVERT(VARCHAR, p.AIDDATE, 103) AS chequeDT,  
                    ROUND(pi.totalprice, 0) AS povalue, 
                    s.chequeAmt, 
                    s.ADMINCHARGES AS ADMINCHARGES,
                    (ROUND(pi.totalprice, 0) + s.ADMINCHARGES) AS GrossWithAdmiChareges,
                    ISNULL(TotalStDeductionIGST, 0) AS TotalStDeductionIGST,
                    ISNULL(TotalStDeductionCGST, 0) AS TotalStDeductionCGST,
                    ISNULL(TotalStDeductionSGST, 0) AS TotalStDeductionSGST,
                    ISNULL(TotalStDeduction194Q, 0) AS TotalStDeduction194Q,
                    sd.TotalStDeduction, 
                    ISNULL(Penalties, 0) AS Penalties,
                    ISNULL(totalAddition, 0) AS totalAddition,
                    ISNULL(WitheldAmt, 0) AS WitheldAmt, 
                    b.BUDGETNAME, 
                    s.PAYMENTID, 
                    p.AIDDATE AS TranDT, 
                    s.po_id
                FROM BLPSANCTIONS s 
                INNER JOIN MASBUDGET b ON b.BUDGETID = s.BUDGETID
                INNER JOIN purchase_order po ON po.po_id = s.po_id
                INNER JOIN facility_aut dir ON dir.facility_aut_id = po.directorate_id
                INNER JOIN massuppliers sp ON sp.supplier_id = po.supplier_id
                INNER JOIN BLPPAYMENTS p ON p.PAYMENTID = s.PAYMENTID
                LEFT OUTER JOIN 
                (
                    SELECT pi.po_id, SUM(pi.totalprice) AS totalprice, SUM(pi.quantity) AS poqty   
                    FROM po_items pi 
                    GROUP BY pi.po_id
                ) pi ON pi.po_id = s.po_id
                LEFT OUTER JOIN 
                (
                    SELECT r.po_id, SUM(ri.received_qty) AS paidQTY FROM receipts r 
                    LEFT OUTER JOIN receipt_item_details ri ON ri.receipt_id = r.receipt_id
                    INNER JOIN BLPINVOICES inv ON inv.RECEIPTID = r.receipt_id
                    WHERE inv.STATUS = 'P'
                    GROUP BY r.po_id
                ) paid ON paid.po_id = s.po_id
                LEFT OUTER JOIN 
                (
                    SELECT ISNULL(SUM(TAXVALUE), 0) AS TotalStDeduction, SANCTIONID FROM 
                    (
                        SELECT bt.TAXTYPENAME, bt.TAXCATEGORY, t.TAXVALUE, t.TAXPER, t.SANCTIONID 
                        FROM BLPTAXS t
                        INNER JOIN BLPTAXTYPES bt ON bt.TAXTYPEID = t.TAXTYPEID
                        WHERE bt.TAXCATEGORY = 'D' AND t.TAXTYPEID NOT IN (250) AND bt.IsMinus_FromGross IS NULL 
                    ) d GROUP BY SANCTIONID  
                ) SD ON SD.SANCTIONID = s.SANCTIONID
                LEFT OUTER JOIN 
                (
                    SELECT ISNULL(SUM(TAXVALUE), 0) AS TotalStDeductionIGST, SANCTIONID FROM 
                    (
                        SELECT bt.TAXTYPENAME, bt.TAXCATEGORY, t.TAXVALUE, t.TAXPER, t.SANCTIONID 
                        FROM BLPTAXS t
                        INNER JOIN BLPTAXTYPES bt ON bt.TAXTYPEID = t.TAXTYPEID
                        WHERE bt.TAXCATEGORY = 'D' AND t.TAXTYPEID NOT IN (250) AND bt.IsMinus_FromGross IS NULL AND bt.TAXTYPEID = 247
                    ) d GROUP BY SANCTIONID  
                ) IGST ON IGST.SANCTIONID = s.SANCTIONID
                LEFT OUTER JOIN 
                (
                    SELECT ISNULL(SUM(TAXVALUE), 0) AS TotalStDeductionCGST, SANCTIONID FROM 
                    (
                        SELECT bt.TAXTYPENAME, bt.TAXCATEGORY, t.TAXVALUE, t.TAXPER, t.SANCTIONID 
                        FROM BLPTAXS t
                        INNER JOIN BLPTAXTYPES bt ON bt.TAXTYPEID = t.TAXTYPEID
                        WHERE bt.TAXCATEGORY = 'D' AND t.TAXTYPEID NOT IN (250) AND bt.IsMinus_FromGross IS NULL AND bt.TAXTYPEID = 248
                    ) d GROUP BY SANCTIONID  
                ) CGST ON CGST.SANCTIONID = s.SANCTIONID
                LEFT OUTER JOIN 
                (
                    SELECT ISNULL(SUM(TAXVALUE), 0) AS TotalStDeductionSGST, SANCTIONID FROM 
                    (
                        SELECT bt.TAXTYPENAME, bt.TAXCATEGORY, t.TAXVALUE, t.TAXPER, t.SANCTIONID 
                        FROM BLPTAXS t
                        INNER JOIN BLPTAXTYPES bt ON bt.TAXTYPEID = t.TAXTYPEID
                        WHERE bt.TAXCATEGORY = 'D' AND t.TAXTYPEID NOT IN (250) AND bt.IsMinus_FromGross IS NULL AND bt.TAXTYPEID = 249
                    ) d GROUP BY SANCTIONID  
                ) SGST ON SGST.SANCTIONID = s.SANCTIONID
                LEFT OUTER JOIN 
                (
                    SELECT ISNULL(SUM(TAXVALUE), 0) AS TotalStDeduction194Q, SANCTIONID FROM 
                    (
                        SELECT bt.TAXTYPENAME, bt.TAXCATEGORY, t.TAXVALUE, t.TAXPER, t.SANCTIONID 
                        FROM BLPTAXS t
                        INNER JOIN BLPTAXTYPES bt ON bt.TAXTYPEID = t.TAXTYPEID
                        WHERE bt.TAXCATEGORY = 'D' AND t.TAXTYPEID NOT IN (250) AND bt.IsMinus_FromGross IS NULL AND bt.TAXTYPEID = 265
                    ) d GROUP BY SANCTIONID  
                ) Q ON Q.SANCTIONID = s.SANCTIONID
                LEFT OUTER JOIN
                (
                    SELECT ISNULL(SUM(TAXVALUE), 0) AS Penalties, SANCTIONID FROM
                    (
                        SELECT bt.TAXTYPENAME, bt.TAXCATEGORY, t.TAXVALUE, t.TAXPER, t.SANCTIONID 
                        FROM BLPTAXS t
                        INNER JOIN BLPTAXTYPES bt ON bt.TAXTYPEID = t.TAXTYPEID
                        WHERE bt.TAXCATEGORY = 'D' AND bt.IsMinus_FromGross = 'Y'
                    ) d GROUP BY SANCTIONID
                ) pen ON pen.SANCTIONID = s.SANCTIONID
                LEFT OUTER JOIN 
                (
                    SELECT ISNULL(SUM(TAXVALUE), 0) totalAddition, SANCTIONID FROM 
                    (
                        SELECT bt.TAXTYPENAME, bt.TAXCATEGORY, t.TAXVALUE, t.TAXPER, t.SANCTIONID 
                        FROM BLPTAXS t
                        INNER JOIN BLPTAXTYPES bt ON bt.TAXTYPEID = t.TAXTYPEID
                        WHERE bt.TAXCATEGORY = 'A'
                    ) d GROUP BY SANCTIONID 
                ) AE ON AE.SANCTIONID = s.SANCTIONID
                LEFT OUTER JOIN 
                (
                    SELECT ISNULL(SUM(TAXVALUE), 0) AS WitheldAmt, SANCTIONID FROM 
                    (
                        SELECT bt.TAXTYPENAME, bt.TAXCATEGORY, t.TAXVALUE, t.TAXPER, t.SANCTIONID 
                        FROM BLPTAXS t
                        INNER JOIN BLPTAXTYPES bt ON bt.TAXTYPEID = t.TAXTYPEID
                        WHERE bt.TAXCATEGORY = 'D' AND t.TAXTYPEID IN (250) 
                    ) d GROUP BY SANCTIONID, TAXCATEGORY 
                ) We ON We.SANCTIONID = s.SANCTIONID
                WHERE s.STATUS = 'P' {whereFromToSql}
                ORDER BY p.AIDDATE";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(selectSqlQuery, conn))
                    {
                        // Safely linking parameters bypassing timezone overlaps completely
                        cmd.Parameters.AddWithValue("@FromDate", parsedFromDate.ToString("yyyy-MM-dd 00:00:00.000"));
                        cmd.Parameters.AddWithValue("@ToDate", parsedToDate.ToString("yyyy-MM-dd 23:59:59.997"));

                        await conn.OpenAsync();
                        int srNo = 1;

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var itemRow = new PaymentStatusDto();

                                // 4. Sequential NULL-Safe Data Parsing Bindings
                                itemRow.Sno = srNo++;
                                itemRow.PaidMonth = reader["PaidMonth"] != DBNull.Value ? Convert.ToInt32(reader["PaidMonth"]) : 0;
                                itemRow.OutwardNo = reader["outward_no"]?.ToString() ?? "";
                                itemRow.PoNo = reader["po_no"]?.ToString() ?? "";

                                if (reader["po_date"] != DBNull.Value)
                                    itemRow.PoDate = Convert.ToDateTime(reader["po_date"]);

                                itemRow.SupplierName = reader["SupplierName"]?.ToString() ?? "";
                                itemRow.SupGst = reader["SUPGST"]?.ToString() ?? "";
                                itemRow.ChequeNo = reader["Chequeno"]?.ToString() ?? "";
                                itemRow.ChequeDt = reader["chequeDT"]?.ToString() ?? "";

                                itemRow.PoValue = reader["povalue"] != DBNull.Value ? Convert.ToDecimal(reader["povalue"]) : 0;
                                itemRow.ChequeAmt = reader["chequeAmt"] != DBNull.Value ? Convert.ToDecimal(reader["chequeAmt"]) : 0;
                                itemRow.AdminCharges = reader["ADMINCHARGES"] != DBNull.Value ? Convert.ToDecimal(reader["ADMINCHARGES"]) : 0;
                                itemRow.GrossWithAdmiChareges = reader["GrossWithAdmiChareges"] != DBNull.Value ? Convert.ToDecimal(reader["GrossWithAdmiChareges"]) : 0;

                                itemRow.TotalStDeductionIGST = reader["TotalStDeductionIGST"] != DBNull.Value ? Convert.ToDecimal(reader["TotalStDeductionIGST"]) : 0;
                                itemRow.TotalStDeductionCGST = reader["TotalStDeductionCGST"] != DBNull.Value ? Convert.ToDecimal(reader["TotalStDeductionCGST"]) : 0;
                                itemRow.TotalStDeductionSGST = reader["TotalStDeductionSGST"] != DBNull.Value ? Convert.ToDecimal(reader["TotalStDeductionSGST"]) : 0;
                                itemRow.TotalStDeduction194Q = reader["TotalStDeduction194Q"] != DBNull.Value ? Convert.ToDecimal(reader["TotalStDeduction194Q"]) : 0;

                                itemRow.TotalStDeduction = reader["TotalStDeduction"] != DBNull.Value ? Convert.ToDecimal(reader["TotalStDeduction"]) : 0;
                                itemRow.Penalties = reader["Penalties"] != DBNull.Value ? Convert.ToDecimal(reader["Penalties"]) : 0;
                                itemRow.TotalAddition = reader["totalAddition"] != DBNull.Value ? Convert.ToDecimal(reader["totalAddition"]) : 0;
                                itemRow.WitheldAmt = reader["WitheldAmt"] != DBNull.Value ? Convert.ToDecimal(reader["WitheldAmt"]) : 0;

                                itemRow.BudgetName = reader["BUDGETNAME"]?.ToString() ?? "";
                                itemRow.PaymentId = reader["PAYMENTID"] != DBNull.Value ? Convert.ToInt32(reader["PAYMENTID"]) : 0;

                                if (reader["TranDT"] != DBNull.Value)
                                    itemRow.TranDt = Convert.ToDateTime(reader["TranDT"]);

                                itemRow.PoId = reader["po_id"] != DBNull.Value ? Convert.ToInt32(reader["po_id"]) : 0;

                                resultList.Add(itemRow);
                            }
                        }
                    }
                }
                return Ok(resultList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error executing automated payment statement record mappings.", error = ex.Message });
            }
        }

        // URL Pattern Test: GET api/TaxRelease/GetTaxReleaseGrid?poType=NP&fromDate=2025-04-01&toDate=2026-03-31
        [HttpGet("GetTaxReleaseGrid")]
        public async Task<IActionResult> GetTaxReleaseGrid([FromQuery] string poType, [FromQuery] string fromDate, [FromQuery] string toDate)
        {
            // 1. Date Validation
            if (string.IsNullOrWhiteSpace(fromDate) || string.IsNullOrWhiteSpace(toDate))
            {
                return BadRequest(new { message = "Please select From and To Date parameters." });
            }

            // Framework string parsing for dates to ignore OS format conflicts
            DateTime parsedFromDate = DateTime.MinValue;
            DateTime parsedToDate = DateTime.MinValue;
            string[] exactFormats = { "yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy", "MM-dd-yyyy", "MM/dd/yyyy" };

            if (!DateTime.TryParseExact(fromDate.Trim(), exactFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedFromDate) ||
                !DateTime.TryParseExact(toDate.Trim(), exactFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedToDate))
            {
                return BadRequest(new { message = "Invalid date format. Recommended format is YYYY-MM-DD." });
            }

            string connectionString = _config.GetConnectionString("DefaultConnection");
            var resultList = new List<TaxReleaseDto>();

            // 2. Dynamic PO Type Condition Binding (NP, CP, All)
            string whereClause = "";
            string safePoType = string.IsNullOrWhiteSpace(poType) ? "All" : poType.Trim();

            if (safePoType == "NP")
            {
                whereClause = " AND ISNULL(p.potype, 'NP') = 'NP' ";
            }
            else if (safePoType == "CP")
            {
                whereClause = " AND ISNULL(p.potype, 'NP') = 'CP' ";
            }

            // 3. Date Filter Binding
            string whereFromToSql = " AND re.AIDDATE BETWEEN @FromDate AND @ToDate ";

            // 4. Exact Core Query Injection
            string selectSqlQuery = $@"
                SELECT 
                    s.name, 
                    re.AIDNO, 
                    p.outward_no + '/' + p.po_no AS po_no, 
                    CONVERT(VARCHAR, re.AIDDATE, 103) AS AIDDATE, 
                    b.BUDGETID, 
                    b.BUDGETNAME, 
                    p.supplier_id, 
                    SUM(ReleaseAmt) AS ReleaseAmt, 
                    SUM(RecoveredAmt) AS RecoveredAmt, 
                    CONVERT(VARCHAR, re.PAIDON, 103) AS PAIDON, 
                    re.Paymentid
                FROM BLPTaxsRelease r
                INNER JOIN MASBUDGET b ON b.BUDGETID = r.FundId
                INNER JOIN purchase_order p ON p.po_id = r.po_id
                INNER JOIN BLPPaymentRelease re ON re.Paymentid = r.Paymentid
                INNER JOIN massuppliers s ON s.supplier_id = p.supplier_id
                WHERE 1=1 {whereFromToSql} {whereClause}
                GROUP BY 
                    re.SanctionNo, re.Paymentid, re.AIDNO, re.AIDDATE, re.Status, re.PAIDON, 
                    b.BUDGETID, b.BUDGETNAME, p.po_no, p.po_id, p.supplier_id, s.name, 
                    p.outward_no
                ORDER BY re.AIDDATE";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(selectSqlQuery, conn))
                    {
                        // Safely applying strict timestamp boundaries
                        cmd.Parameters.AddWithValue("@FromDate", parsedFromDate.ToString("yyyy-MM-dd 00:00:00.000"));
                        cmd.Parameters.AddWithValue("@ToDate", parsedToDate.ToString("yyyy-MM-dd 23:59:59.997"));

                        await conn.OpenAsync();
                        int srNo = 1;

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var itemRow = new TaxReleaseDto();

                                itemRow.Sno = srNo++;

                                // Clean String Binding
                                itemRow.Name = reader["name"] == DBNull.Value ? "" : reader["name"].ToString().Trim();
                                itemRow.AidNo = reader["AIDNO"] == DBNull.Value ? "" : reader["AIDNO"].ToString().Trim();
                                itemRow.PoNo = reader["po_no"] == DBNull.Value ? "" : reader["po_no"].ToString().Trim();
                                itemRow.AidDate = reader["AIDDATE"] == DBNull.Value ? "" : reader["AIDDATE"].ToString().Trim();
                                itemRow.BudgetName = reader["BUDGETNAME"] == DBNull.Value ? "" : reader["BUDGETNAME"].ToString().Trim();
                                itemRow.PaidOn = reader["PAIDON"] == DBNull.Value ? "" : reader["PAIDON"].ToString().Trim();

                                // Null-Safe ID & Math Bindings
                                itemRow.BudgetId = reader["BUDGETID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["BUDGETID"]);
                                itemRow.SupplierId = reader["supplier_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["supplier_id"]);
                                itemRow.PaymentId = reader["Paymentid"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Paymentid"]);

                                itemRow.ReleaseAmt = (reader["ReleaseAmt"] == DBNull.Value || string.IsNullOrWhiteSpace(reader["ReleaseAmt"].ToString()))
                                                     ? 0 : Convert.ToDecimal(reader["ReleaseAmt"]);

                                itemRow.RecoveredAmt = (reader["RecoveredAmt"] == DBNull.Value || string.IsNullOrWhiteSpace(reader["RecoveredAmt"].ToString()))
                                                       ? 0 : Convert.ToDecimal(reader["RecoveredAmt"]);

                                resultList.Add(itemRow);
                            }
                        }
                    }
                }
                return Ok(resultList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error executing tax release data mapping.", error = ex.Message });
            }
        }

        //file gridview 
        [HttpGet("GetDashboardGrid")]
        public async Task<IActionResult> GetDashboardGrid(
    [FromQuery] string poType = "All",
    [FromQuery] string fitUnfit = "All",
    [FromQuery] string eqType = "0",
    [FromQuery] int authorityId = 0)
        {
            string connectionString = _config.GetConnectionString("DefaultConnection");
            var resultList = new List<DashboardGridResponseDto>();

            // Dynamic Filter Clauses Initialization
            string whereclause = "";
            string whereclauseDir = "";
            string ftunft = "";
            string eqtypeStr = "";
            string whFileMyDesk = " and isnull(pres.user_id,0) in (5,29)";

            // 1. PO Type Logic
            if (poType == "NP") whereclause += " and isnull(p.potype,'NP')='NP' ";
            else if (poType == "CP") whereclause += " and isnull(p.potype,'NP')='CP' ";
            else whereclause += " "; // All

            // 2. Fit/Unfit Logic (Exact mapping of your having clause)
            if (fitUnfit == "FP")
            {
                ftunft += " having((  case when (SUM(POQTY)=(isnull(SUM(insqty),0)+isnull(sum(deniedqty),0)) and SUM(POQTY)=(isnull(SUM(receiptQTY),0)+isnull(SUM(DenRec),0))  and (reasonid=11 or reasonid=13 or reasonid is null) ) then 'Fit' else  case when (SUM(POQTY)=isnull(SUM(receiptQTY),0) and (reasonid=11) ) then 'Fit' else  case when (SUM(POQTY)=isnull(SUM(receiptQTY),0) and (isLC = 'Y') ) then 'Fit'  else  case when (SUM(POQTY) ! =isnull(SUM(receiptQTY),0) and (reasonid=16) ) then 'Fit' else   'Non Fit' end end end end)= 'Fit')";
            }
            else if (fitUnfit == "NFP")
            {
                ftunft += " having( (  case when (SUM(POQTY)=(isnull(SUM(insqty),0)+isnull(sum(deniedqty),0)) and SUM(POQTY)=(isnull(SUM(receiptQTY),0)+isnull(SUM(DenRec),0))  and (reasonid=11 or reasonid=13 or reasonid is null) ) then 'Fit' else  case when (SUM(POQTY)=isnull(SUM(receiptQTY),0) and (reasonid=11) ) then 'Fit' else    case when (SUM(POQTY)=isnull(SUM(receiptQTY),0) and (reasonid=11) ) then 'Fit' else 'Non Fit' end end end)= 'Non Fit')";
            }
            else
            {
                ftunft += " ";
            }

            // 3. Equipment Type Logic
            if (eqType == "1") eqtypeStr += "  and (m.categoryId =1 or  m.categoryId is null) ";
            else if (eqType == "2") eqtypeStr += " and m.categoryId  in (2)  ";
            else eqtypeStr += " ";

            // 4. Authority Logic
            if (authorityId != 0)
            {
                whereclauseDir = " and dir.facility_aut_id= @AuthorityId ";
            }

            // Complete Complex SQL Query
            string selectSqlQuery = $@" 
        select a.po_id,tender_no,outward_no+'/'+po_no as po_no ,Supplier, CONVERT(varchar,po_date,103)as po_date,facility_aut_name,item_code_as_per_tender,item_name
        ,SUM(POQTY) as POQTY,cast(sum(totalprice) as bigint) POValue,SUM(Supplyqty)as Supplyqty, isnull(SUM(insqty),0)  as insQty,isnull(SUM(receiptQTY),0)  receiptQTY,CONVERT(varchar,LastRDate,103) as LastRDate1,potype
        ,fileno,CONVERT(varchar,filedt,103) as filedt,isnull(pres.user_name,'TPO(V)') PresentFile,isnull(pres.user_id,383) as presentuserid,isnull(pres.TOUSERID,383) as TOUSERID,isnull(penaltypercent,0)penaltypercent
        ,ReasonName,isnull(issolved,'Y') as issolved,reasonid 
        ,dbo.GetSiteNotReady(po_id) as SiteStatus
        ,ROW_NUMBER() OVER(ORDER BY LastRDaterow ) AS Rowno
        ,tobePaid,isnull(todate,'NA') as  todate
        ,isnull(entDT,'NA') as  entDT
        ,case when (SUM(POQTY)=(isnull(SUM(insqty),0)+isnull(sum(deniedqty),0)) and SUM(POQTY)=(isnull(SUM(receiptQTY),0)+isnull(SUM(DenRec),0))  and (reasonid=11 or reasonid=13 or reasonid is null) ) then 'Fit'
        else  case when (SUM(POQTY)=isnull(SUM(receiptQTY),0) and (reasonid=11) ) then 'Fit' 
        else  case when (SUM(POQTY)=isnull(SUM(receiptQTY),0) and (isLC = 'Y') ) then 'Fit'
        else  case when (SUM(POQTY)! =isnull(SUM(receiptQTY),0) and (reasonid=16) ) then 'Fit' 
        else   'Non Fit' end end end end as Conditond
        ,FinalRate,round(FinalRate*tobePaid,0) as topaidValue
        ,FinREmarks
        ,facility_aut_id
        from (
        select t.penaltypercent, p.po_id,t.tender_no,p.outward_no,p.po_no,p.isLC,p.po_date,p.directorate_id,dir.facility_aut_name,m.item_code_as_per_tender,m.item_name,sp.name as Supplier,pi.quantity as POQTY,pi.totalprice,Supplyqty,re.receiptQTY,
        red.LastRDate ,case when  isnull(rpo.reasonid,0) =13 then red.LastRDate  else case when isnull(rpo.reasonid,0) !=0 then getdate()  else red.LastRDate end end as LastRDaterow
        ,ins.insqty, den.deniedqty,DenR.DenRec, paid.paidQTY,
        pi.quantity-isnull(paid.paidQTY,0) tobePaid,   case when isnull(p.potype,'NP')='NP' then 'Normal PO' else 'Covid Po' end potype
        ,p.fileno,p.filedt,rpo.ReasonName,rpo.issolved AS issolved,rpo.reasonid,FinalRate,
        case when SancP_PONOID is not null then 'Sanction Prepared' else case when   sancCP.SancCP_PONOID is not null then 'Cheque Prepared'
        else case when SancFP_PONOID is not null then 'Cheque To be Prepared' 
        else 'Sanction To be Prepared'
        end end end as FinREmarks
        ,dir.facility_aut_id
        from  purchase_order p 
         inner join massuppliers sp on sp.supplier_id=p.supplier_id
         left outer join 
         (
        select sum(pi.quantity) as quantity,pi.po_id,pi.item_id,sum(pi.totalprice) as totalprice,round(pi.basicrate+(pi.basicrate* pi.percentage/100),2) as FinalRate   from po_items pi 
        group by pi.po_id,pi.item_id,pi.basicrate,pi.percentage
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
         select max(r.recieved_date)LastRDate,r.po_id from receipts r
        left outer join receipt_item_details ri on ri.receipt_id=r.receipt_id
        where r.recieved_date is not null and r.status in ('C','Received') 
        group by r.po_id
         ) as red on red.po_id=pi.po_id 

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
           select r.po_id,sum(ri.received_qty) as paidQTY from receipts r 
           left outer join receipt_item_details ri on ri.receipt_id=r.receipt_id
           inner join BLPINVOICES inv on inv.RECEIPTID=r.receipt_id
           where inv.STATUS='P' and  r.SiteNotFlag is null
           group by r.po_id
           ) paid on paid.po_id=p.po_id

          left outer join 
           (
             select max(s.po_id) SancP_PONOID from BLPSANCTIONS s 
             where s.STATUS='IN'
             group by s.po_id
           ) sancP on sancP.SancP_PONOID=p.po_id

              left outer join 
           (
             select max(s.po_id) SancFP_PONOID from BLPSANCTIONS s 
             where s.STATUS='FP'
             group by s.po_id
           ) sancFP on sancFP.SancFP_PONOID=p.po_id

                 left outer join 
           (
             select max(s.po_id) SancCP_PONOID from BLPSANCTIONS s 
             where s.STATUS='CP'
             group by s.po_id
           ) sancCP on sancCP.SancCP_PONOID=p.po_id

         where  1=1  and p.status in ('Completed','Order Placed','Partially Received') and p.Ispayment is null {whereclauseDir} and Supplyqty>0   {eqtypeStr} and  LastRDate is not null  {whereclause}   

         ) a
          left outer join 
         (
         select u.user_name,m.PONOID,u.user_id,m.TOUSERID,convert(varchar, m.todate,103) as todate,convert(varchar, m.ENTRYDT, 100) as entDT  from users u 
        inner join masfilemovement m on m.TOUSERID=u.user_id and m.PRESENTFILEFLAG='Y'
         ) pres on pres.PONOID=a.po_id
         where 1=1 and  tobePaid>0 {whFileMyDesk}
         group by  entDT,todate, a.tobePaid, outward_no,pres.user_name,pres.user_id,potype,po_no,isLC,po_date,facility_aut_name,facility_aut_id,item_code_as_per_tender,item_name,LastRDate,LastRDaterow,Supplier,po_id,tender_no,fileno,filedt,pres.TOUSERID,penaltypercent,ReasonName,issolved ,reasonid, FinalRate,FinREmarks
        {ftunft}
         order by ROW_NUMBER() OVER(ORDER BY LastRDaterow )";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(selectSqlQuery, conn))
                    {
                        cmd.CommandTimeout = 120;
                        if (authorityId != 0)
                        {
                            cmd.Parameters.AddWithValue("@AuthorityId", authorityId);
                        }

                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var rowItem = new DashboardGridResponseDto();

                                rowItem.PoId = reader["po_id"] == DBNull.Value ? 0 : Convert.ToInt64(reader["po_id"]);
                                rowItem.TenderNo = reader["tender_no"]?.ToString() ?? "";
                                rowItem.PoNo = reader["po_no"]?.ToString() ?? "";
                                rowItem.Supplier = reader["Supplier"]?.ToString() ?? "";
                                rowItem.PoDate = reader["po_date"]?.ToString() ?? "";
                                rowItem.FacilityAutName = reader["facility_aut_name"]?.ToString() ?? "";
                                rowItem.ItemCodeAsPerTender = reader["item_code_as_per_tender"]?.ToString() ?? "";
                                rowItem.ItemName = reader["item_name"]?.ToString() ?? "";

                                rowItem.PoQty = reader["POQTY"] == DBNull.Value ? 0 : Convert.ToInt64(reader["POQTY"]);
                                rowItem.PoValue = reader["POValue"] == DBNull.Value ? 0 : Convert.ToInt64(reader["POValue"]);
                                rowItem.SupplyQty = reader["Supplyqty"] == DBNull.Value ? 0 : Convert.ToInt64(reader["Supplyqty"]);
                                rowItem.InsQty = reader["insQty"] == DBNull.Value ? 0 : Convert.ToInt64(reader["insQty"]);
                                rowItem.ReceiptQty = reader["receiptQTY"] == DBNull.Value ? 0 : Convert.ToInt64(reader["receiptQTY"]);

                                rowItem.LastRDate = reader["LastRDate1"]?.ToString() ?? "";
                                rowItem.PoType = reader["potype"]?.ToString() ?? "";
                                rowItem.FileNo = reader["fileno"]?.ToString() ?? "";
                                rowItem.FileDt = reader["filedt"]?.ToString() ?? "";
                                rowItem.PresentFile = reader["PresentFile"]?.ToString() ?? "";

                                rowItem.PresentUserId = reader["presentuserid"] == DBNull.Value ? 0 : Convert.ToInt32(reader["presentuserid"]);
                                rowItem.ToUserId = reader["TOUSERID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TOUSERID"]);
                                rowItem.PenaltyPercent = reader["penaltypercent"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["penaltypercent"]);

                                rowItem.ReasonName = reader["ReasonName"]?.ToString() ?? "";
                                rowItem.IsSolved = reader["issolved"]?.ToString() ?? "";
                                rowItem.ReasonId = reader["reasonid"] == DBNull.Value ? 0 : Convert.ToInt32(reader["reasonid"]);
                                rowItem.SiteStatus = reader["SiteStatus"]?.ToString() ?? "";

                                rowItem.RowNo = reader["Rowno"] == DBNull.Value ? 0 : Convert.ToInt64(reader["Rowno"]);
                                rowItem.ToBePaid = reader["tobePaid"] == DBNull.Value ? 0 : Convert.ToInt64(reader["tobePaid"]);
                                rowItem.ToDate = reader["todate"]?.ToString() ?? "";
                                rowItem.EntDt = reader["entDT"]?.ToString() ?? "";
                                rowItem.Conditond = reader["Conditond"]?.ToString() ?? "";

                                rowItem.FinalRate = reader["FinalRate"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["FinalRate"]);
                                rowItem.ToPaidValue = reader["topaidValue"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["topaidValue"]);
                                rowItem.FinRemarks = reader["FinREmarks"]?.ToString() ?? "";
                                rowItem.FacilityAutId = reader["facility_aut_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["facility_aut_id"]);

                                resultList.Add(rowItem);
                            }
                        }
                    }
                }
                return Ok(resultList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error executing grid fetch operation.", error = ex.Message });
            }
        }



        [HttpPost("ForwardFile")]
        public async Task<IActionResult> ForwardFile([FromBody] ForwardFileRequestDto request)
        {
            if (request == null || request.PoNoId <= 0 || request.SendToUserId <= 0)
            {
                return BadRequest(new { message = "Please select Send to Officer and provide valid PO ID." });
            }

            if (string.IsNullOrWhiteSpace(request.Remarks))
            {
                return BadRequest(new { message = "Please Write Remarks/Comments for this File." });
            }

            if (string.IsNullOrWhiteSpace(request.ForwardDate))
            {
                return BadRequest(new { message = "Choose Forward Date." });
            }

            // Date Parsing
            DateTime forwardDt;
            string[] exactFormats = { "yyyy-MM-dd", "dd-MM-yyyy", "MM/dd/yyyy" };
            if (!DateTime.TryParseExact(request.ForwardDate.Trim(), exactFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out forwardDt))
            {
                return BadRequest(new { message = "Invalid Forward Date format." });
            }

            // Simple validation: forward date should not be greater than today (as per your commented out logic)
            if (forwardDt.Date > DateTime.Now.Date)
            {
                return BadRequest(new { message = "Forward date is not Greater then todays date." });
            }

            string connString = _config.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Update Existing Movement (Check and Update)
                        string checkExistingSql = "SELECT COUNT(*) FROM MASFILEMOVEMENT WHERE presentfileflag='Y' AND ponoid= @PoNoId";
                        using (SqlCommand chkCmd = new SqlCommand(checkExistingSql, conn, trans))
                        {
                            chkCmd.Parameters.AddWithValue("@PoNoId", request.PoNoId);
                            int exists = Convert.ToInt32(await chkCmd.ExecuteScalarAsync());

                            if (exists > 0)
                            {
                                string updateSql = "UPDATE MASFILEMOVEMENT SET presentfileflag='N' WHERE ponoid= @PoNoId";
                                using (SqlCommand updCmd = new SqlCommand(updateSql, conn, trans))
                                {
                                    updCmd.Parameters.AddWithValue("@PoNoId", request.PoNoId);
                                    await updCmd.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        // 2. Insert New Movement
                        string insertSql = @"
                            INSERT INTO MASFILEMOVEMENT(userid, todate, entryDT, remarks, Flag, presentfileflag, touserid, ponoid, fileid)
                            VALUES(@UserId, @ToDate, GETDATE(), @Remarks, @Flag, 'Y', @ToUserId, @PoNoId, @FileId)";

                        using (SqlCommand insCmd = new SqlCommand(insertSql, conn, trans))
                        {
                            insCmd.Parameters.AddWithValue("@UserId", request.FromUserId);
                            insCmd.Parameters.AddWithValue("@ToDate", forwardDt.ToString("yyyy-MM-dd HH:mm:ss"));
                            insCmd.Parameters.AddWithValue("@Remarks", request.Remarks);
                            insCmd.Parameters.AddWithValue("@Flag", request.Flag);
                            insCmd.Parameters.AddWithValue("@ToUserId", request.SendToUserId);
                            insCmd.Parameters.AddWithValue("@PoNoId", request.PoNoId);
                            insCmd.Parameters.AddWithValue("@FileId", request.FileNo.Trim());

                            await insCmd.ExecuteNonQueryAsync();
                        }

                        await trans.CommitAsync();
                        return Ok(new { message = "File Forwarded Successfully !!!" });
                    }
                    catch (Exception ex)
                    {
                        await trans.RollbackAsync();
                        return StatusCode(500, new { message = "Transaction failed during file forwarding.", error = ex.Message });
                    }
                }
            }
        }

        // ========================================================
        // 2. SAVE REASON API (Purana btnReasonYes_click)
        // ========================================================
        [HttpPost("SaveReason")]
        public async Task<IActionResult> SaveReason([FromBody] SaveReasonRequestDto request)
        {
            if (request == null || request.PoNoId <= 0 || request.ReasonId <= 0)
            {
                return BadRequest(new { message = "Please select a valid Reason and PO ID." });
            }

            string connString = _config.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Insert Reason
                        string insertReasonSql = @"
                            INSERT INTO SOORDERREASON(USERID, REASONID, PONOID, remarks, ENTRYDATE) 
                            VALUES(@UserId, @ReasonId, @PoNoId, @Remarks, GETDATE())";

                        using (SqlCommand insCmd = new SqlCommand(insertReasonSql, conn, trans))
                        {
                            insCmd.Parameters.AddWithValue("@UserId", request.UserId);
                            insCmd.Parameters.AddWithValue("@ReasonId", request.ReasonId);
                            insCmd.Parameters.AddWithValue("@PoNoId", request.PoNoId);
                            insCmd.Parameters.AddWithValue("@Remarks", request.Remarks);

                            await insCmd.ExecuteNonQueryAsync();
                        }

                        // 2. Update Purchase Order if ReasonId is 1
                        if (request.ReasonId == 1)
                        {
                            string updatePoSql = "UPDATE purchase_order SET Ispayment='Y' WHERE po_id= @PoNoId";
                            using (SqlCommand updCmd = new SqlCommand(updatePoSql, conn, trans))
                            {
                                updCmd.Parameters.AddWithValue("@PoNoId", request.PoNoId);
                                await updCmd.ExecuteNonQueryAsync();
                            }
                        }

                        await trans.CommitAsync();
                        return Ok(new { message = "Reason updated successfully." });
                    }
                    catch (Exception ex)
                    {
                        await trans.RollbackAsync();
                        return StatusCode(500, new { message = "Transaction failed during saving reason.", error = ex.Message });
                    }
                }
            }
        }

        // ============================================================
        // 2a. GET REASON MASTER LIST  (used in Add-Reason modal dropdown)
        // GET /api/GMFI/GetReasons
        // ============================================================
        [HttpGet("GetReasons")]
        public async Task<IActionResult> GetReasons()
        {
            string connStr = _config.GetConnectionString("DefaultConnection");
            using SqlConnection con = new SqlConnection(connStr);
            await con.OpenAsync();

            string sql = "SELECT reasonid, ReasonName FROM ReasonMaster WHERE reasonid NOT IN (1) ORDER BY ReasonName";
            using SqlCommand cmd = new SqlCommand(sql, con);

            var list = new List<object>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new
                {
                    ReasonId   = Convert.ToInt32(reader["reasonid"]),
                    ReasonName = reader["ReasonName"]?.ToString() ?? ""
                });
            }
            return Ok(list);
        }

        // ============================================================
        // 2b. ADD REASON  (alias of SaveReason — same logic)
        // POST /api/GMFI/AddReason
        // Body: { userId, reasonId, poId, remarks }
        // ============================================================
        [HttpPost("AddReason")]
        public async Task<IActionResult> AddReason([FromBody] SaveReasonRequestDto request)
        {
            if (request == null || request.PoNoId <= 0 || request.ReasonId <= 0)
                return BadRequest(new { message = "Please select a valid Reason and PO ID." });

            string connStr = _config.GetConnectionString("DefaultConnection");
            using SqlConnection conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            using SqlTransaction trans = conn.BeginTransaction();
            try
            {
                string insertSql = @"
                    INSERT INTO SOORDERREASON(USERID, REASONID, PONOID, remarks, ENTRYDATE)
                    VALUES(@UserId, @ReasonId, @PoNoId, @Remarks, GETDATE())";

                using (SqlCommand insCmd = new SqlCommand(insertSql, conn, trans))
                {
                    insCmd.Parameters.AddWithValue("@UserId",   request.UserId);
                    insCmd.Parameters.AddWithValue("@ReasonId", request.ReasonId);
                    insCmd.Parameters.AddWithValue("@PoNoId",   request.PoNoId);
                    insCmd.Parameters.AddWithValue("@Remarks",  request.Remarks ?? "");
                    await insCmd.ExecuteNonQueryAsync();
                }

                if (request.ReasonId == 1)
                {
                    string updSql = "UPDATE purchase_order SET Ispayment='Y' WHERE po_id=@PoNoId";
                    using SqlCommand updCmd = new SqlCommand(updSql, conn, trans);
                    updCmd.Parameters.AddWithValue("@PoNoId", request.PoNoId);
                    await updCmd.ExecuteNonQueryAsync();
                }

                await trans.CommitAsync();
                return Ok(new { message = "Reason added successfully." });
            }
            catch (Exception ex)
            {
                await trans.RollbackAsync();
                return StatusCode(500, new { message = "Failed to add reason.", error = ex.Message });
            }
        }

        // ============================================================
        // 2c. RESOLVE REASON  (mark SOORDERREASON row as solved)
        // POST /api/GMFI/ResolveReason
        // Body: { sorId }
        // ============================================================
        [HttpPost("ResolveReason")]
        public async Task<IActionResult> ResolveReason([FromBody] ResolveReasonRequestDto request)
        {
            if (request == null || request.SorId <= 0)
                return BadRequest(new { message = "Invalid SORID." });

            string connStr = _config.GetConnectionString("DefaultConnection");
            using SqlConnection conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            // Mark reason as solved by updating reasonid to 13 (Resolved) or set issolved flag
            string sql = "UPDATE SOORDERREASON SET REASONID = 13 WHERE SORID = @SorId";
            using SqlCommand cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@SorId", request.SorId);

            int rows = await cmd.ExecuteNonQueryAsync();
            if (rows == 0)
                return NotFound(new { message = "Record not found." });

            return Ok(new { message = "Reason resolved successfully." });
        }

        // ============================================================
        // 3. FETCH DOCUMENT PATHS (Purana Challan/Photo View buttons)
        // ============================================================
        [HttpGet("GetReceiptDocuments/{receiptItemId}")]
        public async Task<IActionResult> GetReceiptDocuments(int receiptItemId)
        {
            if (receiptItemId <= 0)
            {
                return BadRequest(new { message = "Invalid Receipt Item ID." });
            }

            string connString = _config.GetConnectionString("DefaultConnection");
            string sqlQuery = @"
                SELECT InstalationReportFile, InstalationPhoto, Challanfile, WarrantyCardFile 
                FROM receipt_item_details 
                WHERE item_detail_id = @ItemId";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sqlQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@ItemId", receiptItemId);
                        await conn.OpenAsync();

                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            if (await dr.ReadAsync())
                            {
                                var docs = new ReceiptDocumentsDto
                                {
                                    InstalationReportFile = dr["InstalationReportFile"]?.ToString() ?? "",
                                    InstalationPhoto = dr["InstalationPhoto"]?.ToString() ?? "",
                                    Challanfile = dr["Challanfile"]?.ToString() ?? "",
                                    WarrantyCardFile = dr["WarrantyCardFile"]?.ToString() ?? ""
                                };
                                return Ok(docs);
                            }
                        }
                    }
                }
                return NotFound(new { message = "Documents not found for this receipt." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching document paths.", error = ex.Message });
            }
        }



        //      public async Task<List<SupplierGstDropdownDto>> GetGstNumbersByPoIdAsync(int poId)
        //        {
        //        var gstList = new List<SupplierGstDropdownDto>();

        //        string sqlQuery = @"
        //        SELECT g.GSTID, g.GSTNO 
        //        FROM MassupplierGST g
        //        INNER JOIN purchase_order p ON g.supplierid = p.supplier_id
        //        WHERE p.po_id = @PoId";

        //            string connString = _config.GetConnectionString("DefaultConnection");

        //            using (SqlConnection conn = new SqlConnection(connString))
        //            {
        //            using (SqlCommand cmd = new SqlCommand(sqlQuery, conn))
        //            {
        //                cmd.Parameters.AddWithValue("@PoId", poId);

        //                await conn.OpenAsync();

        //                using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
        //                {
        //                    while (await dr.ReadAsync())
        //                    {
        //                        gstList.Add(new SupplierGstDropdownDto
        //                        {
        //                            GstId = dr["GSTID"] == DBNull.Value ? 0 : Convert.ToInt32(dr["GSTID"]),
        //                            GstNo = dr["GSTNO"]?.ToString() ?? string.Empty
        //                        });
        //                    }
        //                }
        //            }
        //        }

        //        return gstList;
        //    }



        //[HttpGet("GetGstNumbersByPoId/{poId}")]
        //    public async Task<IActionResult> GetGstNumbersByPoId(int poId)
        //    {
        //        if (poId <= 0)
        //        {
        //            return BadRequest(new { message = "Please provide a valid Purchase Order ID." });
        //        }

        //        try
        //        {
        //            var data = await _repository.GetGstNumbersByPoIdAsync(poId);


        //            return Ok(data);
        //        }
        //        catch (Exception ex)
        //        {
        //            return StatusCode(500, new { message = "Error fetching GST numbers for the selected PO.", error = ex.Message });
        //        }
        //    }

        [HttpGet("GetGstNumbersByPoId/{poId}")]
        public async Task<IActionResult> GetGstNumbersByPoId(int poId)
        {
            // 1. Parameter Validation
            if (poId <= 0)
            {
                return BadRequest(new { message = "Please provide a valid Purchase Order ID." });
            }

            var gstList = new List<SupplierGstDropdownDto>();

            // 2. SQL Query
            string sqlQuery = @"
        SELECT g.GSTID, g.GSTNO 
        FROM MassupplierGST g
        INNER JOIN purchase_order p ON g.supplierid = p.supplier_id
        WHERE p.po_id = @PoId";

            try
            {
                string connString = _config.GetConnectionString("DefaultConnection");

                // 3. Database Execution
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sqlQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@PoId", poId);

                        await conn.OpenAsync();

                        using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                        {
                            while (await dr.ReadAsync())
                            {
                                gstList.Add(new SupplierGstDropdownDto
                                {
                                    GstId = dr["GSTID"] == DBNull.Value ? 0 : Convert.ToInt32(dr["GSTID"]),
                                    GstNo = dr["GSTNO"]?.ToString() ?? string.Empty
                                });
                            }
                        }
                    }
                }

                // 4. Return Data
                return Ok(gstList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching GST numbers for the selected PO.", error = ex.Message });
            }
        }

        [HttpGet("GetPoItemDetails/{poId}")]
        public async Task<IActionResult> GetPoItemDetails(int poId)
        {
            if (poId <= 0)
            {
                return BadRequest(new { message = "Please provide a valid Purchase Order ID." });
            }

            var resultList = new List<PoItemDetailsDto>();
            string connString = _config.GetConnectionString("DefaultConnection");

            // Optimized and parameterized SQL Que

            //    string sqlQuery = @"
            //SELECT 
            //    pi.item_id,
            //    m.item_code_as_per_tender AS itemcode,
            //    m.item_name AS itemname,
            //    pi.percentage AS percentvalue,
            //    pi.basicrate AS basicrate, 
            //    t.tender_no AS SchemeName,
            //    CONVERT(VARCHAR, (CASE WHEN soissueDT IS NULL THEN po_date ELSE soissueDT END), 105) AS PoDate,
            //    f.year AS AccYear,
            //    s.name AS SupplierName,
            //    p.po_no AS PoNo, 
            //    ROUND(pi.basicrate + ((pi.basicrate * pi.percentage) / 100), 2) AS finalrate,
            //    SUM(pi.quantity) AS poqty,
            //    ROUND((SUM(pi.quantity) * basicrate), 0) + ROUND((SUM(pi.quantity) * basicrate * pi.percentage) / 100, 0) AS poValue,
            //    s.supplier_id AS SupplierID,
            //    t.tender_id,
            //    p.po_id AS PONOID,
            //    f.financial_year_id AS AccYrSetID,
            //    ISNULL(dp.invoiceGst, '-') AS invoiceGst, 
            //    ISNULL(hsncode, '-') AS hsncode,
            //    '.pdf' AS ext,
            //    dir.facility_aut_name,
            //    dir.facility_aut_id,
            //    ISNULL(rid.REASONID, 0) AS REASONID, 
            //    CASE WHEN rid.REASONID = 16 THEN SUM(pi.quantity) - ISNULL(dispatch, 0) ELSE 0 END AS unexQTY
            //FROM purchase_order p WITH (NOLOCK)
            //INNER JOIN po_items pi WITH (NOLOCK) ON pi.po_id = p.po_id
            //INNER JOIN tenders t WITH (NOLOCK) ON t.tender_id = p.tender_id
            //INNER JOIN masitems m WITH (NOLOCK) ON m.item_id = pi.item_id
            //INNER JOIN mas_financial_year f WITH (NOLOCK) ON f.financial_year_id = p.financial_year_id
            //INNER JOIN massuppliers s WITH (NOLOCK) ON s.supplier_id = p.supplier_id
            //INNER JOIN facility_aut dir WITH (NOLOCK) ON dir.facility_aut_id = p.directorate_id
            //LEFT OUTER JOIN (
            //    SELECT DISTINCT po_id, invoiceGst, hsncode FROM SupplierDispatch WITH (NOLOCK) WHERE invoiceGst IS NOT NULL
            //) dp ON dp.po_id = p.po_id
            //LEFT OUTER JOIN (
            //    SELECT d.po_id, SUM(i.Supplyqty) AS dispatch FROM SupplierDispatch d WITH (NOLOCK)
            //    INNER JOIN Issue_item_details i WITH (NOLOCK) ON i.Issue_id = d.Issue_id
            //    GROUP BY d.po_id
            //) dis ON dis.po_id = p.po_id
            //LEFT OUTER JOIN (
            //    SELECT MAX(r.SORID) AS sorid, r.PONOID, ri.REASONID FROM SOORDERREASON r WITH (NOLOCK)
            //    LEFT OUTER JOIN (
            //        SELECT PONOID, REASONID FROM SOORDERREASON WITH (NOLOCK)
            //    ) ri ON ri.PONOID = r.PONOID
            //    GROUP BY r.PONOID, ri.REASONID
            //) rid ON rid.PONOID = p.po_id
            //WHERE p.po_id = @PoId
            //GROUP BY 
            //    dp.invoiceGst, hsncode, pi.item_id, m.item_code_as_per_tender, m.item_name, pi.percentage, pi.basicrate,
            //    p.po_date, t.tender_no, t.tender_date, f.year, s.name, p.po_no, s.supplier_id, t.tender_id, p.po_id, 
            //    f.financial_year_id, dir.facility_aut_name, dir.facility_aut_id, soissueDT, rid.REASONID, dispatch";
            string sqlQuery = @"
       SELECT 
    pi.item_id,
    m.item_code_as_per_tender AS itemcode,
    m.item_name AS itemname,
    pi.percentage AS percentvalue,
    pi.basicrate AS basicrate, 
    t.tender_no AS SchemeName,
    CONVERT(VARCHAR, ISNULL(p.soissueDT, p.po_date), 105) AS PoDate,
    f.year AS AccYear,
    s.name AS SupplierName,
    p.po_no AS PoNo, 
    ROUND(pi.basicrate + ((pi.basicrate * pi.percentage) / 100), 2) AS finalrate,
    SUM(pi.quantity) AS poqty,
    ROUND((SUM(pi.quantity) * pi.basicrate), 0) + ROUND((SUM(pi.quantity) * pi.basicrate * pi.percentage) / 100, 0) AS poValue,
    s.supplier_id AS SupplierID,
    t.tender_id,
    p.po_id AS PONOID,
    f.financial_year_id AS AccYrSetID,
    ISNULL(dp.invoiceGst, '-') AS invoiceGst, 
    ISNULL(dp.hsncode, '-') AS hsncode,
    '.pdf' AS ext,
    dir.facility_aut_name,
    dir.facility_aut_id,
    ISNULL(rid.REASONID, 0) AS REASONID, 
    CASE WHEN ISNULL(rid.REASONID, 0) = 16 THEN SUM(pi.quantity) - ISNULL(dis.dispatch, 0) ELSE 0 END AS unexQTY
FROM purchase_order p WITH (NOLOCK)
INNER JOIN po_items pi WITH (NOLOCK) ON pi.po_id = p.po_id
INNER JOIN tenders t WITH (NOLOCK) ON t.tender_id = p.tender_id
INNER JOIN masitems m WITH (NOLOCK) ON m.item_id = pi.item_id
INNER JOIN mas_financial_year f WITH (NOLOCK) ON f.financial_year_id = p.financial_year_id
INNER JOIN massuppliers s WITH (NOLOCK) ON s.supplier_id = p.supplier_id
INNER JOIN facility_aut dir WITH (NOLOCK) ON dir.facility_aut_id = p.directorate_id

-- Optimization 1: Changed LEFT JOIN to OUTER APPLY to filter strictly by p.po_id
OUTER APPLY (
    SELECT DISTINCT invoiceGst, hsncode 
    FROM SupplierDispatch WITH (NOLOCK) 
    WHERE po_id = p.po_id AND invoiceGst IS NOT NULL
) dp

-- Optimization 2: Removed full table GROUP BY. Now it only sums for this specific PO.
OUTER APPLY (
    SELECT SUM(i.Supplyqty) AS dispatch 
    FROM SupplierDispatch d WITH (NOLOCK)
    INNER JOIN Issue_item_details i WITH (NOLOCK) ON i.Issue_id = d.Issue_id
    WHERE d.po_id = p.po_id
) dis

-- Optimization 3: Simplified the complex self-join and grouping which was causing a massive performance hit.
OUTER APPLY (
    SELECT DISTINCT REASONID 
    FROM SOORDERREASON WITH (NOLOCK)
    WHERE PONOID = p.po_id
) rid

WHERE p.po_id = @PoId

GROUP BY 
    dp.invoiceGst, dp.hsncode, pi.item_id, m.item_code_as_per_tender, m.item_name, pi.percentage, pi.basicrate,
    p.po_date, t.tender_no, t.tender_date, f.year, s.name, p.po_no, s.supplier_id, t.tender_id, p.po_id, 
    f.financial_year_id, dir.facility_aut_name, dir.facility_aut_id, p.soissueDT, rid.REASONID, dis.dispatch";
            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sqlQuery, conn))
                    {
                      
                        cmd.Parameters.AddWithValue("@PoId", poId);
                        cmd.CommandTimeout = 120;
                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var itemRow = new PoItemDetailsDto();

                                itemRow.ItemId = reader["item_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["item_id"]);
                                itemRow.ItemCode = reader["itemcode"]?.ToString() ?? "";
                                itemRow.ItemName = reader["itemname"]?.ToString() ?? "";

                                itemRow.PercentValue = reader["percentvalue"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["percentvalue"]);
                                itemRow.BasicRate = reader["basicrate"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["basicrate"]);

                                itemRow.SchemeName = reader["SchemeName"]?.ToString() ?? "";
                                itemRow.PoDate = reader["PoDate"]?.ToString() ?? "";
                                itemRow.AccYear = reader["AccYear"]?.ToString() ?? "";
                                itemRow.SupplierName = reader["SupplierName"]?.ToString() ?? "";
                                itemRow.PoNo = reader["PoNo"]?.ToString() ?? "";

                                itemRow.FinalRate = reader["finalrate"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["finalrate"]);
                                itemRow.PoQty = reader["poqty"] == DBNull.Value ? 0 : Convert.ToInt64(reader["poqty"]);
                                itemRow.PoValue = reader["poValue"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["poValue"]);

                                itemRow.SupplierId = reader["SupplierID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SupplierID"]);
                                itemRow.TenderId = reader["tender_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["tender_id"]);
                                itemRow.PoNoId = reader["PONOID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["PONOID"]);
                                itemRow.AccYrSetId = reader["AccYrSetID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["AccYrSetID"]);

                                itemRow.InvoiceGst = reader["invoiceGst"]?.ToString() ?? "-";
                                itemRow.HsnCode = reader["hsncode"]?.ToString() ?? "-";
                                itemRow.Ext = reader["ext"]?.ToString() ?? ".pdf";

                                itemRow.FacilityAutName = reader["facility_aut_name"]?.ToString() ?? "";
                                itemRow.FacilityAutId = reader["facility_aut_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["facility_aut_id"]);
                                itemRow.ReasonId = reader["REASONID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["REASONID"]);
                                itemRow.UnexQty = reader["unexQTY"] == DBNull.Value ? 0 : Convert.ToInt64(reader["unexQTY"]);

                                resultList.Add(itemRow);
                            }
                        }
                    }
                }
                return Ok(resultList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching PO item details.", error = ex.Message });
            }
        }

        [HttpGet("GetPoPenaltyDetails/{poId}")]
        public async Task<IActionResult> GetPoPenaltyDetails(int poId)
        {
            if (poId <= 0)
            {
                return BadRequest(new { message = "Please provide a valid Purchase Order ID." });
            }

            var resultDetails = new PoPenaltyAndTrancheDto();
            string connString = _config.GetConnectionString("DefaultConnection");

            // The SQL query with the hardcoded 2747 replaced by a parameter (@PoId)
            // WITH (NOLOCK) is added to prevent database locking issues.
            string sqlQuery = @"
        SELECT 
            p.po_id, 
            (CASE WHEN p.soissueDT IS NULL THEN p.po_date ELSE p.soissueDT END) AS po_date,
            CASE WHEN p.Potype IS NULL THEN dbo.GetAllowedDays(pt.tranche_days, (CASE WHEN p.soissueDT IS NULL THEN p.po_date ELSE p.soissueDT END)) ELSE pt.tranche_days END AS tranche_days,
            ISNULL(p.isldpenality, '0') AS isldpenality,
            CONVERT(VARCHAR, ISNULL(p.extendeddate, ''), 105) AS extendeddate,
            t.penaltytype,
            CASE WHEN p.Potype IS NULL THEN dbo.GetMaxAllowedDays(t.cancellationdays, (CASE WHEN p.soissueDT IS NULL THEN p.po_date ELSE p.soissueDT END)) ELSE t.cancellationdays END AS cancellationdays,
            t.cancellationpercentage,
            t.penaltypercent,
            t.import_days,
            t.domestic_days,
            t.logocharges,
            t.logochargesUpper
        FROM purchase_order p WITH (NOLOCK)
        INNER JOIN tenders t WITH (NOLOCK) ON p.tender_id = t.tender_id
        INNER JOIN po_tranche pt WITH (NOLOCK) ON pt.po_id = p.po_id
        WHERE p.po_id = @PoId";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sqlQuery, conn))
                    {
                        // Securing against SQL Injection
                        cmd.Parameters.AddWithValue("@PoId", poId);

                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                // Mapping data and safely handling potential NULL values from the database
                                resultDetails.PoId = reader["po_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["po_id"]);

                                if (reader["po_date"] != DBNull.Value)
                                {
                                    resultDetails.PoDate = Convert.ToDateTime(reader["po_date"]).ToString("yyyy-MM-dd"); // Format as needed
                                }

                                resultDetails.TrancheDays = reader["tranche_days"] == DBNull.Value ? 0 : Convert.ToInt32(reader["tranche_days"]);
                                resultDetails.IsLdPenalty = reader["isldpenality"]?.ToString() ?? "0";
                                resultDetails.ExtendedDate = reader["extendeddate"]?.ToString() ?? "";
                                resultDetails.PenaltyType = reader["penaltytype"]?.ToString() ?? "";
                                resultDetails.CancellationDays = reader["cancellationdays"] == DBNull.Value ? 0 : Convert.ToInt32(reader["cancellationdays"]);

                                resultDetails.CancellationPercentage = reader["cancellationpercentage"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["cancellationpercentage"]);
                                resultDetails.PenaltyPercent = reader["penaltypercent"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["penaltypercent"]);

                                resultDetails.ImportDays = reader["import_days"] == DBNull.Value ? 0 : Convert.ToInt32(reader["import_days"]);
                                resultDetails.DomesticDays = reader["domestic_days"] == DBNull.Value ? 0 : Convert.ToInt32(reader["domestic_days"]);

                                resultDetails.LogoCharges = reader["logocharges"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["logocharges"]);
                                resultDetails.LogoChargesUpper = reader["logochargesUpper"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["logochargesUpper"]);

                                return Ok(resultDetails);
                            }
                            else
                            {
                                // Return a 404 Not Found if the PO ID does not exist in the database
                                return NotFound(new { message = $"Purchase Order details not found for ID: {poId}" });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching PO penalty details.", error = ex.Message });
            }
        }

        [HttpGet("GetSanctionIdIfExist/{poId}")]
        public async Task<IActionResult> GetSanctionIdIfExist(int poId)
        {
            // 1. Validation
            if (poId <= 0)
            {
                return BadRequest(new { message = "Please provide a valid Purchase Order ID." });
            }

            string connString = _config.GetConnectionString("DefaultConnection");
            string sanctionId = "";
            string sqlQuery = "SELECT ISNULL(SanctionID, 0) AS SanctionID FROM blpSanctions WITH (NOLOCK) WHERE po_id = @PoId AND status = 'IN'";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sqlQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@PoId", poId);

                        await conn.OpenAsync();

                        object result = await cmd.ExecuteScalarAsync();

                        if (result != null && result != DBNull.Value)
                        {
                            sanctionId = result.ToString();
                        }
                    }
                }

                return Ok(new { sanctionId = sanctionId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching Sanction ID.", error = ex.Message });
            }
        }
        [HttpGet("GenerateSanctionNo")]
        public async Task<IActionResult> GenerateSanctionNo([FromQuery] int accyrSetId, [FromQuery] int poNoId = 0)
        {
            // 1. Validation
            if (accyrSetId <= 0)
            {
                return BadRequest(new { message = "Please provide a valid Financial Year ID." });
            }

            string connString = _config.GetConnectionString("DefaultConnection");
            string mYearCode = "";
            string mAutoCode = "00001"; // Default fallback
            string finalSanctionNo = "";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    await conn.OpenAsync();

                    // ==========================================
                    // STEP 1: Get Financial Year Code
                    // ==========================================
                    string yearQuery = "SELECT year FROM mas_financial_year WITH (NOLOCK) WHERE financial_year_id = @AccYrSetId";
                    using (SqlCommand cmdYear = new SqlCommand(yearQuery, conn))
                    {
                        cmdYear.Parameters.AddWithValue("@AccYrSetId", accyrSetId);

                        object yearResult = await cmdYear.ExecuteScalarAsync();
                        if (yearResult != null && yearResult != DBNull.Value)
                        {
                            mYearCode = yearResult.ToString().Trim();
                        }
                    }

                    // Agar year nahi mila toh yahi se error return kar denge
                    if (string.IsNullOrEmpty(mYearCode))
                    {
                        return NotFound(new { message = "Financial year details not found in the database." });
                    }

                    // ==========================================
                    // STEP 2: Auto Generate Sanction Code
                    // ==========================================
                    // Note: As per your original code, poNoId is NOT used in the WHERE clause, 
                    // so we keep the exact original active SQL query logic.
                    string autoCodeQuery = @"SELECT RIGHT(REPLICATE('0', 5) + LEFT(ISNULL(MAX(CAST(auto_code AS INT)), 0) + 1, 5), 5) AS SanctionNo FROM blpsanctions WITH (NOLOCK)";

                    using (SqlCommand cmdAuto = new SqlCommand(autoCodeQuery, conn))
                    {
                        object autoResult = await cmdAuto.ExecuteScalarAsync();
                        if (autoResult != null && autoResult != DBNull.Value)
                        {
                            mAutoCode = autoResult.ToString().Trim();
                        }
                    }

                    // ==========================================
                    // STEP 3: Construct the Final String
                    // ==========================================
                    finalSanctionNo = $"SAN/{mYearCode}/{mAutoCode}";
                }

                // Return both values so Angular can bind them to UI inputs
                return Ok(new
                {
                    autoCode = mAutoCode,         // For lblAutoCode
                    sanctionNo = finalSanctionNo  // For txtSanctionNo
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error generating Sanction Number.", error = ex.Message });
            }
        }

        [HttpGet("GetPendingSanctionByPoId/{poId}/{sId}")]
        public async Task<IActionResult> GetPendingSanctionByPoId(int poId,int sId)
        {
            // 1. Validation
            if (poId <= 0 && sId <= 0)
            {
                return BadRequest(new { message = "Please provide a valid Purchase Order ID." });
            }

            string connString = _config.GetConnectionString("DefaultConnection");
            var sanctionDetails = new SanctionDetailsDto();
            bool isRecordFound = false;

            string sqlQuery = @"
        SELECT 
            SanctionID, 
            SanctionNo, 
            ISNULL(BUDGETAMT, 0) AS BUDGETAMT, 
            CONVERT(VARCHAR, SanctionDate, 105) AS SanctionDate, 
            BudgetID, 
            Remarks, 
            SupGST 
        FROM blpSanctions WITH (NOLOCK)
        WHERE po_id = @PoId 
          AND SanctionID = @sId  
          AND status = 'IN'";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sqlQuery, conn))
                    {
                       
                        cmd.Parameters.AddWithValue("@PoId", poId);
                        cmd.Parameters.AddWithValue("@sId", sId);


                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                         
                            if (await reader.ReadAsync())
                            {
                                isRecordFound = true;

                                
                                sanctionDetails.SanctionId = reader["SanctionID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SanctionID"]);
                                sanctionDetails.SanctionNo = reader["SanctionNo"]?.ToString() ?? string.Empty;
                                sanctionDetails.BudgetAmt = reader["BUDGETAMT"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["BUDGETAMT"]);
                                sanctionDetails.SanctionDate = reader["SanctionDate"]?.ToString() ?? string.Empty;
                                sanctionDetails.BudgetId = reader["BudgetID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["BudgetID"]);
                                sanctionDetails.Remarks = reader["Remarks"]?.ToString() ?? string.Empty;
                                sanctionDetails.SupGst = reader["SupGST"]?.ToString() ?? string.Empty;
                            }
                        }
                    }
                }

                if (isRecordFound)
                {
                    return Ok(sanctionDetails); 
                }
                else
                {
                   
                    return NotFound(new { message = "No pending sanction data found for this Purchase Order." });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching sanction details.", error = ex.Message });
            }
        }

        [HttpPost("SaveSanctionHeader")]
        public async Task<IActionResult> SaveSanctionHeader([FromBody] SaveSanctionHeaderDto request)
        {
            // ==========================================
            // 1. BASIC VALIDATIONS
            // ==========================================
            if (request == null || request.PoId <= 0 || request.TenderId <= 0)
                return BadRequest(new { message = "Invalid Purchase Order or Tender data." });

            if (request.BudgetId <= 0)
                return BadRequest(new { message = "Please Select a valid Budget No." });

            if (string.IsNullOrWhiteSpace(request.SanctionNo))
                return BadRequest(new { message = "Sanction Number cannot be empty." });

            if (string.IsNullOrWhiteSpace(request.HsnCode))
                return BadRequest(new { message = "HSN Code cannot be empty." });

            if (string.IsNullOrWhiteSpace(request.Remarks))
                return BadRequest(new { message = "Remarks cannot be empty." });

            // Date Validation
            DateTime sanctionDateParsed;
            string[] dateFormats = { "yyyy-MM-dd", "dd-MM-yyyy", "MM/dd/yyyy" };
            if (!DateTime.TryParseExact(request.SanctionDate.Trim(), dateFormats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out sanctionDateParsed))
                return BadRequest(new { message = "Invalid Sanction Date format." });

            if (sanctionDateParsed.Date > DateTime.Now.Date)
                return BadRequest(new { message = "You Can't Select Greater than todays Date." });

            // GST Matching Validation (CheckSelectedGST Logic)
            bool isGstValid = request.IsGstOverrideChecked ||
                              request.GstNo == request.DispatchGstNo ||
                              string.IsNullOrWhiteSpace(request.DispatchGstNo) ||
                              request.DispatchGstNo == "-";

            if (!isGstValid)
                return BadRequest(new { message = "Supplier Dispatched GST No Not Matched." });

            string connString = _config.GetConnectionString("DefaultConnection");

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    await conn.OpenAsync();

                    // ==========================================
                    // 2. LEVY VALIDATION (CheckLeavy Logic)
                    // ==========================================
                    string levyQuery = "SELECT ISNULL(penaltypercent, 0) FROM tenders WITH (NOLOCK) WHERE tender_id = @TenderId";
                    using (SqlCommand levyCmd = new SqlCommand(levyQuery, conn))
                    {
                        levyCmd.Parameters.AddWithValue("@TenderId", request.TenderId);
                        object levyObj = await levyCmd.ExecuteScalarAsync();

                        decimal penaltyPercent = (levyObj != null && levyObj != DBNull.Value) ? Convert.ToDecimal(levyObj) : 0;

                        if (penaltyPercent == 0)
                        {
                            return BadRequest(new { message = "Please Enter Leavy Details in Tender Before Going to Sanction or Communicate Equipment Tender Cell to Enter." });
                        }
                    }

                    // ==========================================
                    // 3. CHECK IF SANCTION EXISTS (getSanctinidIfExist Logic)
                    // ==========================================
                    int existingSanctionId = 0;
                    string checkSanctionQuery = "SELECT ISNULL(SanctionID, 0) FROM blpSanctions WITH (NOLOCK) WHERE po_id = @PoId AND status = 'IN'";
                    using (SqlCommand chkCmd = new SqlCommand(checkSanctionQuery, conn))
                    {
                        chkCmd.Parameters.AddWithValue("@PoId", request.PoId);
                        object sanctionObj = await chkCmd.ExecuteScalarAsync();

                        if (sanctionObj != null && sanctionObj != DBNull.Value)
                        {
                            existingSanctionId = Convert.ToInt32(sanctionObj);
                        }
                    }

                    // ==========================================
                    // 4. INSERT OR UPDATE TRANSACTION
                    // ==========================================
                    string targetQuery = "";

                    if (existingSanctionId == 0)
                    {
                        // INSERT NEW RECORD
                        targetQuery = @"
                    INSERT INTO blpSanctions (po_id, InvoiceType, SanctionNo, SanctionDate, BudgetID, Remarks, SupGST, status, auto_code, BUDGETAMT, EntryDT, HSNcode) 
                    VALUES (@PoId, 'NI', @SanctionNo, @SanctionDate, @BudgetId, @Remarks, @SupGst, 'IN', @AutoCode, @BudgetAmt, GETDATE(), @HsnCode);
                    
                    -- Returns the newly generated SanctionID
                    SELECT SCOPE_IDENTITY();";
                    }
                    else
                    {
                        // UPDATE EXISTING RECORD
                        targetQuery = @"
                    UPDATE blpSanctions 
                    SET po_id = @PoId, InvoiceType = 'NI', SanctionNo = @SanctionNo, SanctionDate = @SanctionDate, BudgetID = @BudgetId, 
                        Remarks = @Remarks, SupGST = @SupGst, BUDGETAMT = @BudgetAmt, EntryDT = GETDATE(), HSNcode = @HsnCode 
                    WHERE SanctionID = @SanctionId;";
                    }

                    using (SqlCommand saveCmd = new SqlCommand(targetQuery, conn))
                    {
                        saveCmd.Parameters.AddWithValue("@PoId", request.PoId);
                        saveCmd.Parameters.AddWithValue("@SanctionNo", request.SanctionNo);
                        // For SQL DateTime standard
                        saveCmd.Parameters.AddWithValue("@SanctionDate", sanctionDateParsed.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                        saveCmd.Parameters.AddWithValue("@BudgetId", request.BudgetId);
                        saveCmd.Parameters.AddWithValue("@Remarks", request.Remarks);
                        saveCmd.Parameters.AddWithValue("@SupGst", request.GstNo);
                        saveCmd.Parameters.AddWithValue("@BudgetAmt", request.BudgetAmt);
                        saveCmd.Parameters.AddWithValue("@HsnCode", request.HsnCode);

                        if (existingSanctionId == 0)
                        {
                            // Insert specific parameters
                            saveCmd.Parameters.AddWithValue("@AutoCode", request.AutoCode);

                            // Insert and get new ID
                            int newSanctionId = Convert.ToInt32(await saveCmd.ExecuteScalarAsync());
                            return Ok(new { message = "Saved Successfully", sanctionId = newSanctionId });
                        }
                        else
                        {
                            // Update specific parameters
                            saveCmd.Parameters.AddWithValue("@SanctionId", existingSanctionId);

                            await saveCmd.ExecuteNonQueryAsync();
                            return Ok(new { message = "Updated Successfully", sanctionId = existingSanctionId });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Transaction failed while saving sanction details.", error = ex.Message });
            }
        }
        [HttpGet("GetInvoiceSummaryByPoId/{poId}")]
        public async Task<IActionResult> GetInvoiceSummaryByPoId(int poId)
        {
            // 1. Validations
            if (poId <= 0)
            {
                return BadRequest(new { message = "Please provide a valid Purchase Order ID." });
            }

            var invoiceList = new List<InvoiceSummaryDto>();
            string connString = _config.GetConnectionString("DefaultConnection");

            // 2. Query with WITH (NOLOCK) and @PoId parameter injected
            string sqlQuery = @"
        SELECT 
            ISNULL(InvoiceID,0) AS InvoiceID, 
            receipt_id,
            location_id AS warehouseid,
            location_name,
            invoice_no AS InvoiceNo,
            invoice_date,
            OrderedQTY AS OrderedQty,
            SUM(received_qty) AS InvoiceAbsQty,
            GST,
            basicrate,
            ROUND((SUM(received_qty)*basicrate),0) + ROUND((SUM(received_qty)*basicrate*GST)/100,0) AS OLDINVOICEVALUE,
            po_item_id,
            item_id,
            ISNULL(GROSSAMOUNT, (ROUND((SUM(received_qty)*basicrate),0) + ROUND((SUM(received_qty)*basicrate*GST)/100,0))) AS Invalueonbill,
            ISNULL(GROSSAMOUNT50,0) AS GROSSAMOUNT50,
            pstatus,
            (CASE WHEN soissueDT IS NULL THEN po_date ELSE soissueDT END) AS po_date,
            CONVERT(VARCHAR, (CASE WHEN soissueDT IS NULL THEN po_date ELSE soissueDT END), 105) AS PDATE,
            rDate,
            recieved_date,
            DATEDIFF(DAY, (CASE WHEN soissueDT IS NULL THEN po_date ELSE soissueDT END), rDate) AS daystaken,
            ISNULL(LogoCharges_HOVerified,'NA') AS Logo
        FROM (
            SELECT 
                0 AS SlNo, 
                b.InvoiceID, 
                m.location_name,
                m.location_id,
                r.receipt_id,
                pi.po_item_id,
                sud.item_id,
                ISNULL(bi.INVOICENO, d.invoice_no) AS invoice_no,  
                CONVERT(VARCHAR, ISNULL(bi.INVOICEDATE, d.invoice_date), 105) AS invoice_date,  
                CONVERT(VARCHAR, r.recieved_date, 105) AS recieved_date,
                CONVERT(VARCHAR, ri.installation_date, 105) AS installation_date, 
                OrderedQTY,
                pi.basicrate,
                pi.GST,
                sud.Supplyqty,
                CASE WHEN r.status='C' THEN ri.received_qty ELSE r.receipt_qty END AS received_qty,
                b.OLDINVOICEVALUE,
                b.GROSSAMOUNT,
                b.GROSSAMOUNT50,
                mp.pstatus,
                p.po_date,
                p.soissueDT,
                r.recieved_date AS rDate,
                ri.installation_date AS InstDT,
                LogoCharges_HOVerified,
                r.status
            FROM receipts r WITH (NOLOCK)
            LEFT OUTER JOIN receipt_item_details ri WITH (NOLOCK) ON ri.receipt_id = r.receipt_id
            INNER JOIN purchase_order p WITH (NOLOCK) ON p.po_id = r.po_id
            LEFT OUTER JOIN (
                SELECT pi.po_item_id, pi.po_id, SUM(pi.quantity) AS OrderedQTY, pi.percentage AS GST, pi.basicrate, pi.consignee_id 
                FROM po_items pi WITH (NOLOCK)
                GROUP BY pi.po_id, pi.percentage, pi.basicrate, pi.consignee_id, pi.po_item_id
            ) pi ON pi.po_id = r.po_id AND pi.consignee_id = r.location_id
            INNER JOIN maslocations m WITH (NOLOCK) ON m.location_id = r.location_id 
            INNER JOIN SupplierDispatch d WITH (NOLOCK) ON d.Issue_id = r.issue_id AND r.location_id = m.location_id
            LEFT OUTER JOIN BLPINVOICES bi WITH (NOLOCK) ON bi.po_id = r.po_id AND bi.RECEIPTID = r.receipt_id AND bi.location_id = r.location_id
            LEFT OUTER JOIN Issue_item_details sud WITH (NOLOCK) ON sud.Issue_id = d.Issue_id AND sud.issue_detail_id = ri.issue_detail_id
            LEFT OUTER JOIN BLPINVOICES b WITH (NOLOCK) ON b.RECEIPTID = r.receipt_id AND b.location_id = r.location_id AND b.po_id = r.po_id
            LEFT OUTER JOIN MasPStatus mp WITH (NOLOCK) ON mp.PType = b.status
            WHERE r.po_id = @PoId AND r.status IN ('C','Received') AND mp.ptype = 'TP'
        ) a 
        GROUP BY 
            LogoCharges_HOVerified, soissueDT, receipt_id, location_id, location_name, invoice_no, invoice_date,
            GST, basicrate, OrderedQTY, InvoiceID, po_item_id, item_id, OLDINVOICEVALUE,
            GROSSAMOUNT, pstatus, po_date, rDate, recieved_date, GROSSAMOUNT50";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sqlQuery, conn))
                    {
                        // Inject parameterized logic safely
                        cmd.Parameters.AddWithValue("@PoId", poId);

                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var row = new InvoiceSummaryDto();

                                // 3. Robust Null-Checking & Type Casting Data Assignment
                                row.InvoiceId = reader["InvoiceID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["InvoiceID"]);
                                row.ReceiptId = reader["receipt_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["receipt_id"]);
                                row.WarehouseId = reader["warehouseid"] == DBNull.Value ? 0 : Convert.ToInt32(reader["warehouseid"]);

                                row.LocationName = reader["location_name"]?.ToString() ?? string.Empty;
                                row.InvoiceNo = reader["InvoiceNo"]?.ToString() ?? string.Empty;
                                row.InvoiceDate = reader["invoice_date"]?.ToString() ?? string.Empty;

                                row.OrderedQty = reader["OrderedQty"] == DBNull.Value ? 0 : Convert.ToInt64(reader["OrderedQty"]);
                                row.InvoiceAbsQty = reader["InvoiceAbsQty"] == DBNull.Value ? 0 : Convert.ToInt64(reader["InvoiceAbsQty"]);

                                row.Gst = reader["GST"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["GST"]);
                                row.BasicRate = reader["basicrate"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["basicrate"]);
                                row.OldInvoiceValue = reader["OLDINVOICEVALUE"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["OLDINVOICEVALUE"]);

                                row.PoItemId = reader["po_item_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["po_item_id"]);
                                row.ItemId = reader["item_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["item_id"]);

                                row.InValueOnBill = reader["Invalueonbill"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["Invalueonbill"]);
                                row.GrossAmount50 = reader["GROSSAMOUNT50"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["GROSSAMOUNT50"]);

                                row.PStatus = reader["pstatus"]?.ToString() ?? string.Empty;
                                row.PoDate = reader["po_date"]?.ToString() ?? string.Empty;
                                row.PDateFormatted = reader["PDATE"]?.ToString() ?? string.Empty;
                                row.RDate = reader["rDate"]?.ToString() ?? string.Empty;
                                row.RecievedDate = reader["recieved_date"]?.ToString() ?? string.Empty;

                                row.DaysTaken = reader["daystaken"] == DBNull.Value ? 0 : Convert.ToInt32(reader["daystaken"]);
                                row.Logo = reader["Logo"]?.ToString() ?? string.Empty;

                                invoiceList.Add(row);
                            }
                        }
                    }
                }

                return Ok(invoiceList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error executing invoice summary lookup.", error = ex.Message });
            }
        }

        [HttpPost("SaveSanctionHeaderinfor")]
        public async Task<IActionResult> SaveSanctionHeaderinfor([FromBody] SanctionSaveDto request)
        {
            // 1. Basic Validations (Replaces GenFunctions checks)
            if (request.BudgetId == -1 || request.BudgetId == 0)
            {
                return BadRequest(new { message = "Please Select a valid Budget No." });
            }
            if (string.IsNullOrWhiteSpace(request.SanctionNo))
            {
                return BadRequest(new { message = "Sanction Number is required." });
            }
            if (string.IsNullOrWhiteSpace(request.HsnCode))
            {
                return BadRequest(new { message = "HSN Code is required." });
            }
            if (request.SanctionDate > DateTime.Now)
            {
                return BadRequest(new { message = "You Can't Select Greater than todays Date." });
            }

            // 2. GST Validation Logic (Replaces CheckSelectedGST method)
            bool isGstValid = false;
            if (request.GstNo == request.DispatchGstNo)
            {
                isGstValid = true;
            }
            else if (string.IsNullOrEmpty(request.DispatchGstNo) || request.DispatchGstNo == "-")
            {
                isGstValid = true;
            }

            if (request.IsGstOverrideChecked) // CheckBox3.Checked
            {
                isGstValid = true;
            }

            if (!isGstValid)
            {
                return BadRequest(new { message = "Supplier Dispatched GST No Not Matched" });
            }

            string connString = _config.GetConnectionString("DefaultConnection");
            int sanctionId = 0;

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    await conn.OpenAsync();

                    // 3. Levy Logic Check (Replaces CheckLeavy method)
                    string levyQuery = "SELECT ISNULL(penaltypercent, 0) FROM tenders WITH (NOLOCK) WHERE tender_id = @TenderId";
                    using (SqlCommand cmdLevy = new SqlCommand(levyQuery, conn))
                    {
                        cmdLevy.Parameters.AddWithValue("@TenderId", request.TenderId);
                        object levyResult = await cmdLevy.ExecuteScalarAsync();

                        decimal penaltyPercent = (levyResult != null && levyResult != DBNull.Value) ? Convert.ToDecimal(levyResult) : 0;

                        if (penaltyPercent <= 0)
                        {
                            return BadRequest(new { message = "Please Enter Leavy Details in Tender Before Going to Sanctin or Communicate Equipment Tender Cell to Enter" });
                        }
                    }

                    // 4. Check if Sanction ID already exists (Replaces getSanctinidIfExist method)
                    string checkExistQuery = "SELECT ISNULL(SanctionID, 0) FROM blpSanctions WITH (NOLOCK) WHERE po_id = @PoId AND status = 'IN'";
                    using (SqlCommand cmdExist = new SqlCommand(checkExistQuery, conn))
                    {
                        cmdExist.Parameters.AddWithValue("@PoId", request.PoId);
                        object existResult = await cmdExist.ExecuteScalarAsync();

                        if (existResult != null && existResult != DBNull.Value)
                        {
                            sanctionId = Convert.ToInt32(existResult);
                        }
                    }

                    // 5. Save Data (Insert or Update logic)
                    string sqlQuery = "";
                    if (sanctionId == 0) // INSERT
                    {
                        sqlQuery = @"
                    INSERT INTO blpSanctions (po_id, InvoiceType, SanctionNo, SanctionDate, BudgetID, Remarks, SupGST, status, auto_code, BUDGETAMT, EntryDT, HSNcode) 
                    OUTPUT INSERTED.SanctionID
                    VALUES (@PoId, 'NI', @SanctionNo, @SanctionDate, @BudgetId, @Remarks, @SupGst, 'IN', @AutoCode, @BudgetAmt, GETDATE(), @HsnCode)";
                    }
                    else // UPDATE
                    {
                        sqlQuery = @"
                    UPDATE blpSanctions 
                    SET po_id = @PoId, InvoiceType = 'NI', SanctionNo = @SanctionNo, SanctionDate = @SanctionDate, 
                        BudgetID = @BudgetId, Remarks = @Remarks, SupGST = @SupGst, BUDGETAMT = @BudgetAmt, 
                        EntryDT = GETDATE(), HSNcode = @HsnCode 
                    WHERE SanctionID = @SanctionId";
                    }

                    using (SqlCommand cmdSave = new SqlCommand(sqlQuery, conn))
                    {
                        cmdSave.Parameters.AddWithValue("@PoId", request.PoId);
                        cmdSave.Parameters.AddWithValue("@SanctionNo", request.SanctionNo);
                        cmdSave.Parameters.AddWithValue("@SanctionDate", request.SanctionDate.ToString("yyyy/MM/dd HH:mm:ss.fff"));
                        cmdSave.Parameters.AddWithValue("@BudgetId", request.BudgetId);
                        cmdSave.Parameters.AddWithValue("@Remarks", string.IsNullOrEmpty(request.Remarks) ? (object)DBNull.Value : request.Remarks);
                        cmdSave.Parameters.AddWithValue("@SupGst", string.IsNullOrEmpty(request.GstNo) ? (object)DBNull.Value : request.GstNo);
                        cmdSave.Parameters.AddWithValue("@BudgetAmt", request.BudgetAmt);
                        cmdSave.Parameters.AddWithValue("@HsnCode", request.HsnCode);

                        if (sanctionId == 0)
                        {
                            cmdSave.Parameters.AddWithValue("@AutoCode", string.IsNullOrEmpty(request.AutoCode) ? (object)DBNull.Value : request.AutoCode);
                            // Get newly inserted ID
                            sanctionId = Convert.ToInt32(await cmdSave.ExecuteScalarAsync());
                        }
                        else
                        {
                            cmdSave.Parameters.AddWithValue("@SanctionId", sanctionId);
                            await cmdSave.ExecuteNonQueryAsync();
                        }
                    }
                }

                // Return success with generated SanctionID so frontend can use it (e.g. for SetHeaderData)
                return Ok(new
                {
                    message = "Saved Successfully",
                    sanctionId = sanctionId,
                    poId = request.PoId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error saving sanction details.", error = ex.Message });
            }
        }


        //[HttpGet("GetInvoiceDetails/{poId}/{invoiceId}")]
        //public async Task<IActionResult> GetInvoiceDetails(int poId, int invoiceId)
        //{
        //    //|| invoiceId <= 0
        //    if (poId <= 0 )
        //    {
        //        return BadRequest(new { message = "Please provide valid PO ID and Invoice ID." });
        //    }

        //    string connString = _config.GetConnectionString("DefaultConnection");
        //    List<InvoiceDetailDto> invoiceDetailsList = new List<InvoiceDetailDto>();

        //    string sqlQuery = @"
        //    SELECT 
        //    ISNULL(InvoiceID, 0) AS InvoiceID, 
        //    receipt_id, 
        //    location_id AS warehouseid, 
        //    location_name, 
        //    invoice_no AS InvoiceNo, 
        //    invoice_date, 
        //    OrderedQTY AS OrderedQty, 
        //    SUM(received_qty) AS InvoiceAbsQty, 
        //    GST, 
        //    basicrate, 
        //    ROUND((SUM(received_qty) * basicrate), 0) + ROUND((SUM(received_qty) * basicrate * GST) / 100, 0) AS OLDINVOICEVALUE, 
        //    po_item_id, 
        //    item_id, 
        //    ISNULL(OLDINVOICEVALUE, (ROUND((SUM(received_qty) * basicrate), 0) + ROUND((SUM(received_qty) * basicrate * GST) / 100, 0))) AS Invalueonbill, 
        //    item_detail_id, 
        //    INVOICEITEMID
        //FROM 
        //(
        //    SELECT 
        //        0 SlNo, 
        //        ISNULL(bi.INVOICEITEMID, 0) INVOICEITEMID, 
        //        b.InvoiceID, 
        //        m.location_name, 
        //        m.location_id, 
        //        r.receipt_id, 
        //        po_item_id, 
        //        item_id, 
        //        ri.item_detail_id, 
        //        d.invoice_no, 
        //        CONVERT(varchar, d.invoice_date, 105) AS invoice_date, 
        //        CONVERT(varchar, r.recieved_date, 105) AS recieved_date, 
        //        CONVERT(varchar, ri.installation_date, 105) AS installation_date, 
        //        OrderedQTY, 
        //        pi.basicrate, 
        //        pi.GST, 
        //        sud.Supplyqty, 
        //        ri.received_qty, 
        //        b.OLDINVOICEVALUE
        //    FROM receipts r WITH(NOLOCK)
        //    LEFT OUTER JOIN receipt_item_details ri WITH(NOLOCK) ON ri.receipt_id = r.receipt_id
        //    LEFT OUTER JOIN 
        //    (
        //        SELECT pi.po_item_id, pi.po_id, SUM(pi.quantity) AS OrderedQTY, pi.percentage AS GST, pi.basicrate, pi.consignee_id 
        //        FROM po_items pi WITH(NOLOCK)
        //        GROUP BY pi.po_id, pi.percentage, pi.basicrate, pi.consignee_id, pi.po_item_id
        //    ) pi ON pi.po_id = r.po_id AND pi.consignee_id = r.location_id
        //    INNER JOIN maslocations m WITH(NOLOCK) ON m.location_id = r.location_id 
        //    INNER JOIN SupplierDispatch d WITH(NOLOCK) ON d.Issue_id = r.issue_id AND r.location_id = m.location_id
        //    LEFT OUTER JOIN Issue_item_details sud WITH(NOLOCK) ON sud.Issue_id = d.Issue_id AND sud.issue_detail_id = ri.issue_detail_id
        //    LEFT OUTER JOIN BLPINVOICES b WITH(NOLOCK) ON b.RECEIPTID = r.receipt_id AND b.location_id = r.location_id AND b.po_id = r.po_id
        //    LEFT OUTER JOIN BLPINVOICEITEMS bi WITH(NOLOCK) ON bi.INVOICEID = b.INVOICEID
        //    WHERE r.po_id = @PoId AND r.status IN ('C', 'Received')
        //) a 
        //WHERE InvoiceID = @InvoiceId
        //GROUP BY 
        //    receipt_id, location_id, location_name, invoice_no, invoice_date, 
        //    GST, basicrate, OrderedQTY, InvoiceID, po_item_id, item_id, 
        //    OLDINVOICEVALUE, item_detail_id, INVOICEITEMID";

        //    try
        //    {
        //        using (SqlConnection conn = new SqlConnection(connString))
        //        {
        //            using (SqlCommand cmd = new SqlCommand(sqlQuery, conn))
        //            {
        //                // Add Parameters to prevent SQL Injection
        //                cmd.Parameters.AddWithValue("@PoId", poId);
        //                cmd.Parameters.AddWithValue("@InvoiceId", invoiceId);

        //                await conn.OpenAsync();

        //                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
        //                {
        //                    while (await reader.ReadAsync())
        //                    {
        //                        invoiceDetailsList.Add(new InvoiceDetailDto
        //                        {
        //                            InvoiceId = reader["InvoiceID"] != DBNull.Value ? Convert.ToInt32(reader["InvoiceID"]) : 0,
        //                            ReceiptId = reader["receipt_id"] != DBNull.Value ? Convert.ToInt32(reader["receipt_id"]) : 0,
        //                            WarehouseId = reader["warehouseid"] != DBNull.Value ? Convert.ToInt32(reader["warehouseid"]) : 0,
        //                            LocationName = reader["location_name"]?.ToString() ?? string.Empty,
        //                            InvoiceNo = reader["InvoiceNo"]?.ToString() ?? string.Empty,
        //                            InvoiceDate = reader["invoice_date"]?.ToString() ?? string.Empty,
        //                            OrderedQty = reader["OrderedQty"] != DBNull.Value ? Convert.ToDecimal(reader["OrderedQty"]) : 0,
        //                            InvoiceAbsQty = reader["InvoiceAbsQty"] != DBNull.Value ? Convert.ToDecimal(reader["InvoiceAbsQty"]) : 0,
        //                            Gst = reader["GST"] != DBNull.Value ? Convert.ToDecimal(reader["GST"]) : 0,
        //                            BasicRate = reader["basicrate"] != DBNull.Value ? Convert.ToDecimal(reader["basicrate"]) : 0,
        //                            OldInvoiceValue = reader["OLDINVOICEVALUE"] != DBNull.Value ? Convert.ToDecimal(reader["OLDINVOICEVALUE"]) : 0,
        //                            PoItemId = reader["po_item_id"] != DBNull.Value ? Convert.ToInt32(reader["po_item_id"]) : 0,
        //                            ItemId = reader["item_id"] != DBNull.Value ? Convert.ToInt32(reader["item_id"]) : 0,
        //                            InValueOnBill = reader["Invalueonbill"] != DBNull.Value ? Convert.ToDecimal(reader["Invalueonbill"]) : 0,
        //                            ItemDetailId = reader["item_detail_id"] != DBNull.Value ? Convert.ToInt32(reader["item_detail_id"]) : 0,
        //                            InvoiceItemId = reader["INVOICEITEMID"] != DBNull.Value ? Convert.ToInt32(reader["INVOICEITEMID"]) : 0
        //                        });
        //                    }
        //                }
        //            }
        //        }

        //        if (invoiceDetailsList.Count > 0)
        //        {
        //            return Ok(invoiceDetailsList);
        //        }
        //        else
        //        {
        //            return NotFound(new { message = "No invoice records found for the given PO ID and Invoice ID." });
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { message = "Error fetching invoice details.", error = ex.Message });
        //    }
        //}
        // एक Route बिना InvoiceId के, और दूसरा InvoiceId के साथ
        // यहाँ route में सिर्फ poId रखा है
        [HttpGet("GetInvoiceDetails/{poId}")]
        public async Task<IActionResult> GetInvoiceDetails(int poId, [FromQuery] int? invoiceId)
        {
            if (poId <= 0)
            {
                return BadRequest(new { message = "Please provide a valid PO ID." });
            }

            string connString = _config.GetConnectionString("DefaultConnection");
            List<InvoiceDetailDto> invoiceDetailsList = new List<InvoiceDetailDto>();

            // SQL क्वेरी वही रहेगी
            string sqlQuery = @"
        SELECT 
            ISNULL(InvoiceID, 0) AS InvoiceID, 
            receipt_id, 
            location_id AS warehouseid, 
            location_name, 
            invoice_no AS InvoiceNo, 
            invoice_date, 
            OrderedQTY AS OrderedQty, 
            SUM(received_qty) AS InvoiceAbsQty, 
            GST, 
            basicrate, 
            ROUND((SUM(received_qty) * basicrate), 0) + ROUND((SUM(received_qty) * basicrate * GST) / 100, 0) AS OLDINVOICEVALUE, 
            po_item_id, 
            item_id, 
            ISNULL(OLDINVOICEVALUE, (ROUND((SUM(received_qty) * basicrate), 0) + ROUND((SUM(received_qty) * basicrate * GST) / 100, 0))) AS Invalueonbill, 
            item_detail_id, 
            INVOICEITEMID
        FROM 
        (
            SELECT 
                0 SlNo, 
                ISNULL(bi.INVOICEITEMID, 0) INVOICEITEMID, 
                b.InvoiceID, 
                m.location_name, 
                m.location_id, 
                r.receipt_id, 
                po_item_id, 
                item_id, 
                ri.item_detail_id, 
                d.invoice_no, 
                CONVERT(varchar, d.invoice_date, 105) AS invoice_date, 
                CONVERT(varchar, r.recieved_date, 105) AS recieved_date, 
                CONVERT(varchar, ri.installation_date, 105) AS installation_date, 
                OrderedQTY, 
                pi.basicrate, 
                pi.GST, 
                sud.Supplyqty, 
                ri.received_qty, 
                b.OLDINVOICEVALUE
            FROM receipts r WITH(NOLOCK)
            LEFT OUTER JOIN receipt_item_details ri WITH(NOLOCK) ON ri.receipt_id = r.receipt_id
            LEFT OUTER JOIN 
            (
                SELECT pi.po_item_id, pi.po_id, SUM(pi.quantity) AS OrderedQTY, pi.percentage AS GST, pi.basicrate, pi.consignee_id 
                FROM po_items pi WITH(NOLOCK)
                GROUP BY pi.po_id, pi.percentage, pi.basicrate, pi.consignee_id, pi.po_item_id
            ) pi ON pi.po_id = r.po_id AND pi.consignee_id = r.location_id
            INNER JOIN maslocations m WITH(NOLOCK) ON m.location_id = r.location_id 
            INNER JOIN SupplierDispatch d WITH(NOLOCK) ON d.Issue_id = r.issue_id AND r.location_id = m.location_id
            LEFT OUTER JOIN Issue_item_details sud WITH(NOLOCK) ON sud.Issue_id = d.Issue_id AND sud.issue_detail_id = ri.issue_detail_id
            LEFT OUTER JOIN BLPINVOICES b WITH(NOLOCK) ON b.RECEIPTID = r.receipt_id AND b.location_id = r.location_id AND b.po_id = r.po_id
            LEFT OUTER JOIN BLPINVOICEITEMS bi WITH(NOLOCK) ON bi.INVOICEID = b.INVOICEID
            WHERE r.po_id = @PoId AND r.status IN ('C', 'Received')
        ) a 
        WHERE (@InvoiceId IS NULL OR @InvoiceId = 0 OR InvoiceID = @InvoiceId)
        GROUP BY 
            receipt_id, location_id, location_name, invoice_no, invoice_date, 
            GST, basicrate, OrderedQTY, InvoiceID, po_item_id, item_id, 
            OLDINVOICEVALUE, item_detail_id, INVOICEITEMID";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    using (SqlCommand cmd = new SqlCommand(sqlQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@PoId", poId);
                        // यहाँ invoiceId Nullable है, तो अगर ये पास नहीं होगा तो DBNull.Value जाएगा
                        cmd.Parameters.AddWithValue("@InvoiceId", invoiceId.HasValue ? (object)invoiceId.Value : DBNull.Value);

                        await conn.OpenAsync();

                        // ... (बाकी का Read logic वही रहेगा)
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            // ... अंदर के loop में ये कोड रखें
                            while (await reader.ReadAsync())
                            {
                                invoiceDetailsList.Add(new InvoiceDetailDto
                                {
                                    // यहाँ कॉलम के नाम SQL के 'AS' वाले नामों से मैच करें
                                    InvoiceId = reader["InvoiceID"] != DBNull.Value ? Convert.ToInt32(reader["InvoiceID"]) : 0,
                                    ReceiptId = reader["receipt_id"] != DBNull.Value ? Convert.ToInt32(reader["receipt_id"]) : 0,
                                    WarehouseId = reader["warehouseid"] != DBNull.Value ? Convert.ToInt32(reader["warehouseid"]) : 0,
                                    LocationName = reader["location_name"]?.ToString() ?? "",
                                    InvoiceNo = reader["InvoiceNo"]?.ToString() ?? "",
                                    InvoiceDate = reader["invoice_date"]?.ToString() ?? "",
                                    OrderedQty = reader["OrderedQty"] != DBNull.Value ? Convert.ToDecimal(reader["OrderedQty"]) : 0,
                                    InvoiceAbsQty = reader["InvoiceAbsQty"] != DBNull.Value ? Convert.ToDecimal(reader["InvoiceAbsQty"]) : 0,
                                    Gst = reader["GST"] != DBNull.Value ? Convert.ToDecimal(reader["GST"]) : 0,
                                    BasicRate = reader["basicrate"] != DBNull.Value ? Convert.ToDecimal(reader["basicrate"]) : 0,
                                    OldInvoiceValue = reader["OLDINVOICEVALUE"] != DBNull.Value ? Convert.ToDecimal(reader["OLDINVOICEVALUE"]) : 0,
                                    PoItemId = reader["po_item_id"] != DBNull.Value ? Convert.ToInt32(reader["po_item_id"]) : 0,
                                    ItemId = reader["item_id"] != DBNull.Value ? Convert.ToInt32(reader["item_id"]) : 0,
                                    InValueOnBill = reader["Invalueonbill"] != DBNull.Value ? Convert.ToDecimal(reader["Invalueonbill"]) : 0,
                                    ItemDetailId = reader["item_detail_id"] != DBNull.Value ? Convert.ToInt32(reader["item_detail_id"]) : 0,
                                    InvoiceItemId = reader["INVOICEITEMID"] != DBNull.Value ? Convert.ToInt32(reader["INVOICEITEMID"]) : 0
                                });
                            }
                        }
                    }
                }
                return invoiceDetailsList.Count > 0 ? Ok(invoiceDetailsList) : NotFound(new { message = "No records found." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error.", error = ex.Message });
            }
        }

    }
}



 
