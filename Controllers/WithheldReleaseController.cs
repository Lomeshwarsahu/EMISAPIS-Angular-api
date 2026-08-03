using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using EMISAPIS.DTOS;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

namespace EMISAPIS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WithheldReleaseController : ControllerBase
    {
        private readonly IConfiguration _config;

        public WithheldReleaseController(IConfiguration config)
        {
            _config = config;
        }

        private string ConnStr => _config.GetConnectionString("DefaultConnection");

        /// <summary>Payment20Per.aspx fillgrid — pending 20% withheld releases (TaxTypeid=250, ISRELEASE null).</summary>
        [HttpGet("rows")]
        public async Task<IActionResult> GetRows()
        {
            const string sql = @"
select sp.name, convert(varchar, s.SANCTIONDATE, 103) as SANCTIONDATE, s.SANCTIONEDAMOUNT as GrossAmt,
       s.chequeAmt as ChequeAmt, py.AIDNO, convert(varchar, py.AIDDATE, 103) as chequedate,
       t.TAXVALUE as Witheld20, rec.nos as WithledQTY,
       case when p.Potype is null then 'Normal PO' else 'COVID PO' end as POType,
       b.BUDGETID, b.BUDGETNAME, s.SANCTIONID, p.po_id, sp.supplier_id,
       isnull(py.PAYMENTID, 0) PAYMENTID, convert(varchar, py.AIDDATE, 103) as AIDDATE, isnull(py.AIDNO, '-') AIDNO,
       isnull(t.RELEASEAMT, 0) RELEASEAMT,
       p.outward_no + '-' + p.po_no as pono,
       convert(varchar, (case when p.soissueDT is null then p.po_date else p.soissueDT end), 103) as po_date
from BLPTAXS t
inner join BLPSANCTIONS s on s.SANCTIONID = t.SANCTIONID
inner join BLPPAYMENTS py on py.PAYMENTID = s.PAYMENTID
inner join purchase_order p on s.po_id = p.po_id
inner join massuppliers sp on sp.supplier_id = p.supplier_id
inner join MASBUDGET b on b.BUDGETID = s.BUDGETID
left outer join (
    select count(ri.item_detail_id) as nos, i.SANCTIONID
    from BLPINVOICES i
    inner join BLPSANCTIONS s on s.SANCTIONID = i.SANCTIONID
    inner join BLPTAXS t on t.SANCTIONID = i.SANCTIONID
    inner join receipts r on r.receipt_id = i.RECEIPTID and i.po_id = r.po_id
    inner join receipt_item_details ri on ri.receipt_id = r.receipt_id and ri.receipt_id = i.RECEIPTID
    where i.STATUS = 'P' and s.status = 'P' and (ri.ISUserRecom = 'N' or ri.RecDate > s.SANCTIONDATE) and t.TAXTYPEID = 250
    group by i.SANCTIONID
) rec on rec.SANCTIONID = t.SANCTIONID
where t.TAXTYPEID = 250 and t.ISRELEASE is null and s.status = 'P'";

            try
            {
                var list = new List<WithheldReleaseRowDto>();
                await using var conn = new SqlConnection(ConnStr);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new WithheldReleaseRowDto
                    {
                        SupplierName = reader["name"]?.ToString() ?? string.Empty,
                        SanctionDate = reader["SANCTIONDATE"]?.ToString() ?? string.Empty,
                        GrossAmt = GetDecimal(reader["GrossAmt"]),
                        ChequeAmt = GetDecimal(reader["ChequeAmt"]),
                        AidNo = reader["AIDNO"]?.ToString() ?? string.Empty,
                        ChequeDate = reader["chequedate"]?.ToString() ?? string.Empty,
                        Witheld20 = GetDecimal(reader["Witheld20"]),
                        WithledQty = reader["WithledQTY"] == DBNull.Value ? 0 : Convert.ToInt32(reader["WithledQTY"]),
                        PoType = reader["POType"]?.ToString() ?? string.Empty,
                        BudgetId = reader["BUDGETID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["BUDGETID"]),
                        BudgetName = reader["BUDGETNAME"]?.ToString() ?? string.Empty,
                        SanctionId = reader["SANCTIONID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SANCTIONID"]),
                        PoId = reader["po_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["po_id"]),
                        SupplierId = reader["supplier_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["supplier_id"]),
                        PaymentId = reader["PAYMENTID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["PAYMENTID"]),
                        AidDate = reader["AIDDATE"]?.ToString() ?? string.Empty,
                        ReleaseAmt = GetDecimal(reader["RELEASEAMT"]),
                        PoNo = reader["pono"]?.ToString() ?? string.Empty,
                        PoDate = reader["po_date"]?.ToString() ?? string.Empty,
                    });
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading withheld release rows.", detail = ex.Message });
            }
        }

        /// <summary>FillCGMSCBankAccounts — active CGMSC debit accounts.</summary>
        [HttpGet("cgmsc-banks")]
        public async Task<IActionResult> GetCgmscBanks()
        {
            const string sql = @"Select bankid as BankAccountID, AccountNo + '-' + accountname as AccountNo
from MasCGMSCAccNos where isactive = 'Y'";
            try
            {
                var list = new List<WithheldBankOptionDto>();
                await using var conn = new SqlConnection(ConnStr);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new WithheldBankOptionDto
                    {
                        BankAccountId = reader["BankAccountID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["BankAccountID"]),
                        AccountNo = reader["AccountNo"]?.ToString() ?? string.Empty,
                    });
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading CGMSC bank accounts.", detail = ex.Message });
            }
        }

        /// <summary>FillSupplierBankAccounts — supplier registered accounts.</summary>
        [HttpGet("supplier-banks")]
        public async Task<IActionResult> GetSupplierBanks([FromQuery] int supplierId)
        {
            if (supplierId <= 0)
                return BadRequest(new { message = "supplierId is required." });

            const string sql = @"Select BankAccountID, AccountNo as AccountNo
from MasSupplierAccNos Where SUPPLIERID = @SupplierId order by DEFAULTACC desc";
            try
            {
                var list = new List<WithheldBankOptionDto>();
                await using var conn = new SqlConnection(ConnStr);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@SupplierId", supplierId);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new WithheldBankOptionDto
                    {
                        BankAccountId = reader["BankAccountID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["BankAccountID"]),
                        AccountNo = reader["AccountNo"]?.ToString() ?? string.Empty,
                    });
                }
                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading supplier bank accounts.", detail = ex.Message });
            }
        }

        /// <summary>ddlAccountNos_SelectedIndexChanged — supplier account detail.</summary>
        [HttpGet("supplier-bank-detail")]
        public async Task<IActionResult> GetSupplierBankDetail([FromQuery] int bankAccountId)
        {
            if (bankAccountId <= 0)
                return BadRequest(new { message = "bankAccountId is required." });

            const string sql = @"Select AccountName, ifsccode, branch from MasSupplierAccNos Where bankAccountid = @BankAccountId";
            try
            {
                await using var conn = new SqlConnection(ConnStr);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@BankAccountId", bankAccountId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound(new { message = "Supplier does not have any registered accounts." });

                return Ok(new WithheldBankDetailDto
                {
                    AccountName = reader["AccountName"]?.ToString() ?? string.Empty,
                    IfscCode = reader["ifsccode"]?.ToString() ?? string.Empty,
                    Branch = reader["branch"]?.ToString() ?? string.Empty,
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading supplier bank detail.", detail = ex.Message });
            }
        }

        /// <summary>BtnSanctionSave_Click — validate selected sanctions (same supplier + budget, bank account exists).</summary>
        [HttpPost("validate-selection")]
        public async Task<IActionResult> ValidateSelection([FromBody] List<int> sanctionIds)
        {
            if (sanctionIds == null || sanctionIds.Count == 0)
                return BadRequest(new { message = "Please Select Checkbox" });

            string ids = string.Join(",", sanctionIds);
            try
            {
                await using var conn = new SqlConnection(ConnStr);
                await conn.OpenAsync();

                // Same supplier + budget check
                string budgetSql = @"select distinct p.supplier_id, BUDGETID from BLPSANCTIONS s
inner join purchase_order p on p.po_id = s.po_id
where s.status in ('P') and SANCTIONID in (" + ids + ")";
                var supplierId = 0;
                int budgetId = 0;
                string supplierName = string.Empty;
                await using (var cmd = new SqlCommand(budgetSql, conn))
                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    var distinct = new List<(int SupplierId, int BudgetId)>();
                    while (await reader.ReadAsync())
                    {
                        int sid = reader["supplier_id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["supplier_id"]);
                        int bid = reader["BUDGETID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["BUDGETID"]);
                        distinct.Add((sid, bid));
                    }
                    if (distinct.Count != 1)
                        return Ok(new WithheldSelectionResultDto
                        {
                            Valid = false,
                            Message = "You can not Select Different Supplier or Different Budget Head for One Cheque",
                        });
                    supplierId = distinct[0].SupplierId;
                    budgetId = distinct[0].BudgetId;
                }

                // Sum of Witheld20 for selected sanctions
                string amtSql = @"select isnull(sum(t.TAXVALUE), 0) as withld from BLPTAXS t
where t.TAXTYPEID = 250 and t.SANCTIONID in (" + ids + ")";
                decimal paidAmount = 0;
                await using (var cmd = new SqlCommand(amtSql, conn))
                {
                    var result = await cmd.ExecuteScalarAsync();
                    paidAmount = result == null || result == DBNull.Value ? 0 : Convert.ToDecimal(result);
                }

                // Supplier bank account existence + detail
                string acctSql = @"select distinct p.supplier_id, isnull(sup.ACCOUNTNO, 0) as acno,
isnull(sup.ACCOUNTNAME, '-') as ACCOUNTNAME, sup.IFSCCODE, sup.BRANCH, sp.name
from BLPSANCTIONS s
inner join purchase_order p on p.po_id = s.po_id
inner join massuppliers sp on sp.supplier_id = p.supplier_id
left outer join MASSUPPLIERACCNOS sup on sup.SUPPLIERID = p.supplier_id
where s.status in ('P') and SANCTIONID in (" + ids + ")";
                await using (var cmd = new SqlCommand(acctSql, conn))
                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                        return Ok(new WithheldSelectionResultDto { Valid = false, Message = "Supplier details not found." });

                    supplierName = reader["name"]?.ToString() ?? string.Empty;
                    object acno = reader["acno"];
                    if (acno == DBNull.Value || Convert.ToString(acno) == "0")
                        return Ok(new WithheldSelectionResultDto
                        {
                            Valid = false,
                            Message = "Please Add Supplier Bank Account No Before Cheque Preparation",
                        });

                    return Ok(new WithheldSelectionResultDto
                    {
                        Valid = true,
                        SupplierId = supplierId,
                        SupplierName = supplierName,
                        PaidAmount = paidAmount,
                        AccountName = reader["ACCOUNTNAME"]?.ToString() ?? string.Empty,
                        IfscCode = reader["IFSCCODE"]?.ToString() ?? string.Empty,
                        Branch = reader["BRANCH"]?.ToString() ?? string.Empty,
                        PaymentId = await GetExistingPaymentId(conn, ids),
                    });
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error validating selection.", detail = ex.Message });
            }
        }

        /// <summary>lbtnUpdateHeaderInfo_Click — insert/update BlpPayments + update BLPTAXS.</summary>
        [HttpPost("save")]
        public async Task<IActionResult> Save([FromBody] WithheldReleaseSaveRequest req)
        {
            // Validation mirroring legacy GenFunctions checks
            if (req.SupplierBankAccountId <= 0)
                return BadRequest(new { message = "Supplier Bank Account is required." });
            if (req.PayMode <= 0)
                return BadRequest(new { message = "Payment Mode is required." });
            if (string.IsNullOrWhiteSpace(req.PayDocumentNo))
                return BadRequest(new { message = "Cheque No is required." });
            if (string.IsNullOrWhiteSpace(req.PayDocumentDate))
                return BadRequest(new { message = "Cheque Date is required." });
            if (!DateTime.TryParse(req.PayDocumentDate, out DateTime chequeDt))
                return BadRequest(new { message = "Invalid cheque date." });
            if (chequeDt > DateTime.Now)
                return BadRequest(new { message = "You Can`t Select Cheque Date Greater than todays Date" });
            if (req.SanctionIds == null || req.SanctionIds.Count == 0)
                return BadRequest(new { message = "Please Select Checkbox" });

            string ids = string.Join(",", req.SanctionIds);
            try
            {
                await using var conn = new SqlConnection(ConnStr);
                await conn.OpenAsync();

                int existingPaymentId = await GetExistingPaymentId(conn, ids);
                string remarks = req.Remarks.Trim().Replace("'", "''");
                int paymentId;

                if (existingPaymentId == 0)
                {
                    string insertSql = $@"Insert into BlpPayments (PayModeId, AidNo, AidDate, AmountPaid, Remarks, SUPPAIDBANKID, CGMSCPAIDBANKID, SUPPLIERID)
values ({req.PayMode}, {req.PayDocumentNo.Trim()}, '{chequeDt:yyyy-MM-dd HH:mm:ss}', {req.AmountPaid}, '{remarks}',
{req.SupplierBankAccountId}, {req.CgmscBankAccountId}, {req.SupplierId})";
                    await using (var cmd = new SqlCommand(insertSql, conn))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }

                    paymentId = await GetPaymentByCheque(conn, req.PayMode, req.PayDocumentNo.Trim(), req.AmountPaid);

                    string updateTaxSql = $@"Update BLPTAXS Set paymentid = {paymentId}, STATUS = 'CP', RELEASEAMT = {req.AmountPaid}
Where TaxTypeid = 250 and SanctionID in ({ids})";
                    await using (var cmd = new SqlCommand(updateTaxSql, conn))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                else
                {
                    string updateSql = $@"Update BlpPayments set PayModeId = {req.PayMode}, AidNo = {req.PayDocumentNo.Trim()},
AidDate = '{chequeDt:yyyy-MM-dd HH:mm:ss}', AmountPaid = {req.AmountPaid}, Remarks = '{remarks}',
SUPPAIDBANKID = {req.SupplierBankAccountId}, CGMSCPAIDBANKID = {req.CgmscBankAccountId}, SUPPLIERID = {req.SupplierId}
Where PaymentId = {existingPaymentId}";
                    await using (var cmd = new SqlCommand(updateSql, conn))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }

                    paymentId = await GetPaymentByCheque(conn, req.PayMode, req.PayDocumentNo.Trim(), req.AmountPaid);

                    string updateTaxSql = $@"Update BLPTAXS Set PaymentId = {paymentId}, STATUS = 'CP' Where SanctionID in ({ids})";
                    await using (var cmd = new SqlCommand(updateTaxSql, conn))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                return Ok(new WithheldSelectionResultDto
                {
                    Valid = true,
                    Message = existingPaymentId == 0 ? "Saved Successfully" : "Updated Successfully",
                    PaymentId = paymentId,
                    PaidAmount = req.AmountPaid,
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error saving withheld release.", detail = ex.Message });
            }
        }

        /// <summary>btnComplete_Click — mark payment made (BLPPAYMENTS status='P', BLPTAXS ISRELEASE='Y').</summary>
        [HttpPost("complete")]
        public async Task<IActionResult> Complete([FromBody] WithheldReleaseCompleteRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.PayDocumentNo) || req.AmountPaid <= 0)
                return BadRequest(new { message = "Cheque No and amount are required." });
            if (string.IsNullOrWhiteSpace(req.PaidOn) || !DateTime.TryParse(req.PaidOn, out DateTime payDt))
                return BadRequest(new { message = "Invalid pay date." });
            if (payDt > DateTime.Now)
                return BadRequest(new { message = "You Can`t Select Pay Date Greater than todays Date" });
            if (!string.IsNullOrWhiteSpace(req.PayDocumentDate))
            {
                DateTime.TryParse(req.PayDocumentDate, out DateTime chequeDt);
                if (payDt < chequeDt)
                    return BadRequest(new { message = "You Can`t Select Pay Date Less than Cheque Date" });
            }

            try
            {
                await using var conn = new SqlConnection(ConnStr);
                await conn.OpenAsync();

                int paymentId = await GetPaymentByCheque(conn, req.PayMode, req.PayDocumentNo.Trim(), req.AmountPaid);
                if (paymentId == 0)
                    return BadRequest(new { message = "Payment record not found." });

                string updateSql = $@"update BLPPAYMENTS set STATUS = 'P', PAIDON = '{payDt:yyyy-MM-dd HH:mm:ss}', EntryDT = getdate()
where PAYMENTID = {paymentId}";
                await using (var cmd = new SqlCommand(updateSql, conn))
                {
                    await cmd.ExecuteNonQueryAsync();
                }

                string sanctSql = $@"update BLPTAXS set ISRELEASE = 'Y', STATUS = 'P' where PAYMENTID = {paymentId}";
                await using (var cmd = new SqlCommand(sanctSql, conn))
                {
                    await cmd.ExecuteNonQueryAsync();
                }

                return Ok(new { message = "Payment Made Successfully for 20% Witheld, Now you cant Edit the Entry" });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error completing payment.", detail = ex.Message });
            }
        }

        /// <summary>PaymentLetter.aspx PaymentLetterRDLC — bank letter data for HTML print view.</summary>
        [HttpGet("letter")]
        public async Task<IActionResult> GetLetter([FromQuery] int paymentId, [FromQuery] string? is20)
        {
            if (paymentId <= 0)
                return BadRequest(new { message = "Paymentid is required." });

            bool is20Per = string.Equals(is20, "Y", StringComparison.OrdinalIgnoreCase);
            try
            {
                await using var conn = new SqlConnection(ConnStr);
                await conn.OpenAsync();

                var letter = new List<PaymentLetterRowDto>();
                if (is20Per)
                {
                    const string sql20 = @"select SUPBankId as Bankaccountid, b.accountno, B.Accountname,
B.Bankname, B.Branch, B.Ifsccode, Sup.supplier_id as Supplierid, Sup.name as Suppliername,
cast(SUM(RELEASEAMT) as bigint) as Amountpaid, ri.Paymentid
from BLPTaxsRelease ri
Inner Join Massupplieraccnos B On (B.Bankaccountid = ri.SUPBankId)
Inner Join Massuppliers Sup On (Sup.supplier_id = b.SUPPLIERID)
where ri.Paymentid = @PaymentId
group by Paymentid, CGMSCBankId, SUPBankId, b.accountno, B.Accountname,
B.Bankname, B.Branch, B.Ifsccode, Sup.name, sup.supplier_id";
                    await using (var cmd = new SqlCommand(sql20, conn))
                    {
                        cmd.Parameters.AddWithValue("@PaymentId", paymentId);
                        await using var reader = await cmd.ExecuteReaderAsync();
                        while (await reader.ReadAsync())
                        {
                            letter.Add(ReadLetterRow(reader));
                        }
                    }
                }
                else
                {
                    const string sqlNormal = @"select b.BANKACCOUNTID as Bankaccountid, b.accountno, B.Accountname,
B.Bankname, B.Branch, B.Ifsccode, Sup.supplier_id as Supplierid, Sup.name as Suppliername,
SUM(chequeAmt) as Amountpaid, p.PAYMENTID
from BLPSANCTIONS S
inner join BLPPAYMENTS p on p.PAYMENTID = S.paymentid
Inner Join Massupplieraccnos B On (B.Bankaccountid = p.SUPPAIDBANKID)
Inner Join Massuppliers Sup On (Sup.supplier_id = p.SUPPLIERID)
where p.paymentid = @PaymentId
group by b.BANKACCOUNTID, b.accountno, B.Accountname, B.Bankname, B.Branch, B.Ifsccode, Sup.name, Sup.supplier_id, p.PAYMENTID
union all
select b.bankid as Bankaccountid, b.accountno, B.Accountname, B.Bankname, B.Branch, B.Ifsccode,
0 as Supplierid, 'CGMSCL' as Suppliername, a.ADMINCHARGES, a.paymentid
from MasCGMSCAccNos b
left outer join (
    select paymentid, SUM(ADMINCHARGES) as ADMINCHARGES from BLPSANCTIONS where paymentid = @PaymentId group by paymentid
) a on 1 = 1
where b.ISADMINCHARGES = 'Y'";
                    await using (var cmd = new SqlCommand(sqlNormal, conn))
                    {
                        cmd.Parameters.AddWithValue("@PaymentId", paymentId);
                        await using var reader = await cmd.ExecuteReaderAsync();
                        while (await reader.ReadAsync())
                        {
                            letter.Add(ReadLetterRow(reader));
                        }
                    }
                }

                // Bank info (dtBankinfo)
                var bankInfo = new List<PaymentLetterBankDto>();
                string bankSql = is20Per
                    ? @"select accountno, accountname, bankname, branch, ifsccode,
b.Aidno, convert(varchar, b.Aiddate, 103) Aiddate
from MasCGMSCAccNos m
inner join BLPPaymentRelease b on b.CGMSCPAIDBANKID = m.bankid where b.PAYMENTID = @PaymentId"
                    : @"select accountno, accountname, bankname, branch, ifsccode,
b.Aidno, convert(varchar, b.Aiddate, 103) Aiddate, isnull(b.REMARKS, '') as REMARKS
from MasCGMSCAccNos m
inner join Blppayments b on b.CGMSCPAIDBANKID = m.bankid where b.PAYMENTID = @PaymentId";
                await using (var cmd = new SqlCommand(bankSql, conn))
                {
                    cmd.Parameters.AddWithValue("@PaymentId", paymentId);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        bankInfo.Add(new PaymentLetterBankDto
                        {
                            Accountno = reader["accountno"]?.ToString() ?? string.Empty,
                            Accountname = reader["accountname"]?.ToString() ?? string.Empty,
                            Bankname = reader["bankname"]?.ToString() ?? string.Empty,
                            Branch = reader["branch"]?.ToString() ?? string.Empty,
                            Ifsccode = reader["ifsccode"]?.ToString() ?? string.Empty,
                            Aidno = reader["Aidno"]?.ToString() ?? string.Empty,
                            Aiddate = reader["Aiddate"]?.ToString() ?? string.Empty,
                            Remarks = reader["REMARKS"]?.ToString() ?? string.Empty,
                        });
                    }
                }

                decimal total = 0;
                foreach (var r in letter) total += r.AmountPaid;
                total = Math.Round(total, 0);
                string words = NumberToWords(total);
                string supplierName = letter.Count > 0 ? letter[0].SupplierName : string.Empty;
                string aidNo = bankInfo.Count > 0 ? bankInfo[0].Aidno : string.Empty;

                return Ok(new PaymentLetterDataDto
                {
                    BankLetter = letter,
                    BankInfo = bankInfo,
                    Words = words,
                    SupplierName = supplierName,
                    AidNo = aidNo,
                    TotalAmount = total,
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Error loading payment letter.", detail = ex.Message });
            }
        }

        private static PaymentLetterRowDto ReadLetterRow(SqlDataReader reader)
        {
            return new PaymentLetterRowDto
            {
                Bankaccountid = reader["Bankaccountid"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Bankaccountid"]),
                Accountno = reader["accountno"]?.ToString() ?? string.Empty,
                Accountname = reader["Accountname"]?.ToString() ?? string.Empty,
                Bankname = reader["Bankname"]?.ToString() ?? string.Empty,
                Branch = reader["Branch"]?.ToString() ?? string.Empty,
                Ifsccode = reader["Ifsccode"]?.ToString() ?? string.Empty,
                SupplierId = reader["Supplierid"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Supplierid"]),
                SupplierName = reader["Suppliername"]?.ToString() ?? string.Empty,
                AmountPaid = reader["Amountpaid"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["Amountpaid"]),
                PaymentId = reader["Paymentid"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Paymentid"]),
            };
        }

        private static async Task<int> GetExistingPaymentId(SqlConnection conn, string ids)
        {
            const string sql = @"Select top 1 PaymentId from BLPTAXS where SanctionId in (SELECT value FROM STRING_SPLIT(@Ids, ','))";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Ids", ids);
            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        private static async Task<int> GetPaymentByCheque(SqlConnection conn, int payMode, string payDocumentNo, decimal amountPaid)
        {
            const string sql = @"Select top 1 PaymentId from BlpPayments where PayModeId = @PayMode
and AidNo = @AidNo and AmountPaid = @AmountPaid";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@PayMode", payMode);
            cmd.Parameters.AddWithValue("@AidNo", payDocumentNo);
            cmd.Parameters.AddWithValue("@AmountPaid", amountPaid);
            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        private static decimal GetDecimal(object value)
        {
            return value == DBNull.Value ? 0 : Convert.ToDecimal(value);
        }

        private static string NumberToWords(decimal amount)
        {
            long whole = (long)Math.Truncate(amount);
            int paise = (int)Math.Round((amount - whole) * 100);

            string words = WholeToWords(whole);
            if (paise > 0)
                words += " and " + WholeToWords(paise) + " paise";
            return words + " only";
        }

        private static string WholeToWords(long number)
        {
            if (number == 0) return "Zero";
            if (number < 0) return "Minus " + WholeToWords(-number);

            var ones = new[] { "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
            var tens = new[] { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

            string words = "";
            if (number / 10000000 > 0)
            {
                words += WholeToWords(number / 10000000) + " Crore ";
                number %= 10000000;
            }
            if (number / 100000 > 0)
            {
                words += WholeToWords(number / 100000) + " Lakh ";
                number %= 100000;
            }
            if (number / 1000 > 0)
            {
                words += WholeToWords(number / 1000) + " Thousand ";
                number %= 1000;
            }
            if (number / 100 > 0)
            {
                words += WholeToWords(number / 100) + " Hundred ";
                number %= 100;
            }
            if (number > 0)
            {
                if (words != "") words += "And ";
                if (number < 20)
                    words += ones[number];
                else
                    words += tens[number / 10] + (number % 10 > 0 ? " " + ones[number % 10] : "");
            }
            return words.Trim();
        }
    }
}
