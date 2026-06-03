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


    }
}



 
