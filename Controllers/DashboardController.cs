using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EMISAPIS.Controllers
{
    // Home page dashboard: KPI cards + charts for the logged-in department (user_type / role name).
    // Every number is its own query; a query that fails returns null ("—" on the page) instead of breaking the
    // dashboard. Conditions are copied from the pages each card links to, so the numbers match those pages.
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly IConfiguration _config;

        public DashboardController(IConfiguration config)
        {
            _config = config;
        }

        private string ConnStr() => _config.GetConnectionString("DefaultConnection")!;

        // POs counted everywhere except drafts / cancelled (DME po-dashboard filter)
        private const string LivePo = "p.status NOT IN ('Incomplete', 'Waiting For Approval', 'Cancelled')";

        // Per-PO balances used by the supplier balance-status pages (dispatch D / receipt R / installation I)
        private const string PoBalanceSql = @"
FROM purchase_order p
LEFT OUTER JOIN (SELECT SUM(quantity) AS quantity, po_id FROM po_items GROUP BY po_id) pi ON pi.po_id = p.po_id
LEFT OUTER JOIN (
    SELECT po_id, ISNULL(SUM(Supplyqty), 0) AS Supplyqty
    FROM SupplierDispatch d INNER JOIN Issue_item_details i ON d.Issue_id = i.Issue_id
    WHERE d.status = 'C' GROUP BY po_id
) sup ON sup.po_id = p.po_id
LEFT OUTER JOIN (
    SELECT ISNULL(SUM(r.receipt_qty), 0) AS receiptQTY, r.po_id FROM receipts r
    WHERE r.recieved_date IS NOT NULL AND r.status IN ('C', 'Received') GROUP BY r.po_id
) re ON re.po_id = p.po_id
LEFT OUTER JOIN (
    SELECT SUM(ri.received_qty) AS insqty, r.po_id FROM receipts r
    LEFT OUTER JOIN receipt_item_details ri ON ri.receipt_id = r.receipt_id
    WHERE r.recieved_date IS NOT NULL AND r.status IN ('C') GROUP BY r.po_id
) ins ON ins.po_id = p.po_id
WHERE p.status IN ('Order Placed')";

        public class DashboardCard
        {
            public string Key { get; set; } = "";
            public string Label { get; set; } = "";
            public decimal? Value { get; set; }
            public string Format { get; set; } = "count"; // count | crore | rupees
            public string Route { get; set; } = "";
            public Dictionary<string, string>? Query { get; set; }
            public string Tone { get; set; } = "neutral"; // neutral | warning | success | danger | info
            public string Hint { get; set; } = "";
        }

        public class DashboardChart
        {
            public string Key { get; set; } = "";
            public string Title { get; set; } = "";
            public string Type { get; set; } = "bar"; // bar | donut
            public List<string> Labels { get; set; } = new();
            public List<decimal> Values { get; set; } = new();
            public string Unit { get; set; } = "";
        }

        public class DashboardResponse
        {
            public string Department { get; set; } = "";
            public string FinancialYear { get; set; } = "";
            public List<DashboardCard> Cards { get; set; } = new();
            public List<DashboardChart> Charts { get; set; } = new();
        }

        private static readonly string[] TenderRoles = { "TPO", "TPOT", "BME" };
        private static readonly string[] PoRoles = { "AUPO" };
        private static readonly string[] FinanceRoles = { "AUGMF", "FIN" };
        private static readonly string[] SupplierRoles = { "SUP" };
        private static readonly string[] FacilityRoles = { "DME", "FU", "PRINCIPAL", "FDA", "Store", "Facility", "HealthFacility", "AYUSH" };
        private static readonly string[] ItRoles = { "SEC1" };
        // AD, AU, GM, IGM, DHS, CMHO and anything else: organisation overview

        // GET api/Dashboard/home?role=TPO&userId=12
        [HttpGet("home")]
        public async Task<IActionResult> GetHomeDashboard([FromQuery] string? role, [FromQuery] int userId = 0)
        {
            string r = (role ?? "").Trim();
            bool Is(string[] set) => set.Any(x => string.Equals(x, r, StringComparison.OrdinalIgnoreCase));

            try
            {
                await using var conn = new SqlConnection(ConnStr());
                await conn.OpenAsync();

                var (fyId, fyName) = await CurrentFinancialYearAsync(conn);
                var res = new DashboardResponse { FinancialYear = fyName };

                if (Is(TenderRoles))
                {
                    res.Department = "Tender Cell";
                    await AddTenderAsync(conn, res, fyId, full: true);
                }
                else if (Is(PoRoles))
                {
                    res.Department = "PO Cell";
                    await AddPoAsync(conn, res, fyId, full: true);
                }
                else if (Is(FinanceRoles))
                {
                    res.Department = "Finance";
                    await AddFinanceAsync(conn, res, full: true);
                }
                else if (Is(SupplierRoles))
                {
                    res.Department = "Supplier";
                    await AddSupplierAsync(conn, res, userId);
                }
                else if (Is(FacilityRoles))
                {
                    res.Department = "Facility / Consignee";
                    await AddFacilityAsync(conn, res, userId);
                }
                else if (Is(ItRoles))
                {
                    res.Department = "IT Module";
                    await AddItAsync(conn, res);
                }
                else
                {
                    res.Department = "Overview";
                    await AddTenderAsync(conn, res, fyId, full: false);
                    await AddPoAsync(conn, res, fyId, full: false);
                    await AddFinanceAsync(conn, res, full: false);
                }

                return Ok(res);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error loading dashboard", error = ex.Message });
            }
        }

        // Legacy: mas_financial_year where getdate() between from_date and to_date
        private static async Task<(int id, string name)> CurrentFinancialYearAsync(SqlConnection conn)
        {
            try
            {
                await using var cmd = new SqlCommand(
                    "SELECT TOP 1 financial_year_id, year FROM mas_financial_year WHERE GETDATE() BETWEEN from_date AND to_date", conn);
                await using var dr = await cmd.ExecuteReaderAsync();
                if (await dr.ReadAsync())
                    return (Convert.ToInt32(dr["financial_year_id"]), dr["year"]?.ToString() ?? "");
            }
            catch (SqlException)
            {
                // fall through: year-bound cards use all years
            }
            return (0, "");
        }

        private static async Task<decimal?> ScalarAsync(SqlConnection conn, string sql, params (string name, object value)[] ps)
        {
            try
            {
                await using var cmd = new SqlCommand(sql, conn);
                foreach (var (name, value) in ps) cmd.Parameters.AddWithValue(name, value);
                var v = await cmd.ExecuteScalarAsync();
                return v == null || v == DBNull.Value ? 0 : Convert.ToDecimal(v);
            }
            catch (SqlException)
            {
                return null;
            }
        }

        private static async Task<DashboardChart?> ChartAsync(SqlConnection conn, string key, string title, string type, string unit,
            string sql, params (string name, object value)[] ps)
        {
            try
            {
                var chart = new DashboardChart { Key = key, Title = title, Type = type, Unit = unit };
                await using var cmd = new SqlCommand(sql, conn);
                foreach (var (name, value) in ps) cmd.Parameters.AddWithValue(name, value);
                await using var dr = await cmd.ExecuteReaderAsync();
                while (await dr.ReadAsync())
                {
                    chart.Labels.Add(dr[0]?.ToString() ?? "");
                    chart.Values.Add(dr[1] == DBNull.Value ? 0 : Convert.ToDecimal(dr[1]));
                }
                return chart;
            }
            catch (SqlException)
            {
                return null;
            }
        }

        private static void Card(DashboardResponse res, string key, string label, decimal? value, string route,
            Dictionary<string, string>? query = null, string tone = "neutral", string format = "count", string hint = "")
        {
            res.Cards.Add(new DashboardCard
            {
                Key = key, Label = label, Value = value, Route = route, Query = query, Tone = tone, Format = format, Hint = hint,
            });
        }

        private static void AddChart(DashboardResponse res, DashboardChart? chart)
        {
            if (chart != null && chart.Labels.Count > 0) res.Charts.Add(chart);
        }

        // ---- Tender Cell (Plan a Tender / Cover A evaluation) ----
        private static async Task AddTenderAsync(SqlConnection conn, DashboardResponse res, int fyId, bool full)
        {
            string byCsid = "SELECT COUNT(*) FROM tenders WHERE csid = @Csid AND financial_year_id >= 15";
            Card(res, "tenderLive", "Live Tenders", await ScalarAsync(conn, byCsid, ("@Csid", 1)),
                "/tender/plan", new() { ["status"] = "1" }, "info");
            Card(res, "tenderCoverA", "Cover A Opened", await ScalarAsync(conn, byCsid, ("@Csid", 2)),
                "/tender/plan", new() { ["status"] = "2" });
            // Cover A tenders where some bidder has no technical or no financial evaluation yet
            Card(res, "tenderCoverAPending", "Cover A Evaluation Pending", await ScalarAsync(conn, @"
SELECT COUNT(DISTINCT t.tender_id) FROM tenders t
INNER JOIN masschemesstatusdetails sc ON sc.SCHEMEID = t.tender_id
WHERE t.csid = 2 AND (sc.ISCovTechEli IS NULL OR sc.IsCOVFinEli IS NULL)"),
                "/TenderCoverA", null, "warning");

            if (full)
            {
                Card(res, "tenderCoverB", "Cover B Opened", await ScalarAsync(conn, byCsid, ("@Csid", 3)),
                    "/tender/plan", new() { ["status"] = "3" });
                Card(res, "tenderDemo", "Under Demo", await ScalarAsync(conn, byCsid, ("@Csid", 4)),
                    "/tender/plan", new() { ["status"] = "4" });
                Card(res, "tenderPrice", "Price Opened", await ScalarAsync(conn, byCsid, ("@Csid", 5)),
                    "/tender/plan", new() { ["status"] = "5" }, "success");
                // Legacy row colour "Link Items in Tender": tender without items
                Card(res, "tenderNoItems", "Tenders Without Items", await ScalarAsync(conn, @"
SELECT COUNT(*) FROM tenders t
WHERE t.csid <> 6 AND t.financial_year_id >= 15
  AND NOT EXISTS (SELECT 1 FROM tender_items ti WHERE ti.tender_id = t.tender_id)"),
                    "/tender/plan", null, "danger");
            }

            AddChart(res, await ChartAsync(conn, "tenderStatus",
                fyId > 0 ? "Tenders by Status (this year)" : "Tenders by Status", "donut", "tenders", @"
SELECT c.CStatus, COUNT(*) FROM tenders t
INNER JOIN MasCoverStatus c ON c.CSID = t.csid
WHERE (@Fy = 0 AND t.financial_year_id >= 15) OR t.financial_year_id = @Fy
GROUP BY c.CStatus, c.CSID ORDER BY c.CSID", ("@Fy", fyId)));

            if (full)
            {
                AddChart(res, await ChartAsync(conn, "tenderByYear", "Tenders per Year", "bar", "tenders", @"
SELECT f.year, COUNT(*) FROM tenders t
INNER JOIN mas_financial_year f ON f.financial_year_id = t.financial_year_id
WHERE t.financial_year_id >= 15
GROUP BY f.year, f.financial_year_id ORDER BY f.financial_year_id"));
            }
        }

        // ---- PO Cell ----
        private static async Task AddPoAsync(SqlConnection conn, DashboardResponse res, int fyId, bool full)
        {
            Card(res, "poThisYear", fyId > 0 ? "POs This Year" : "POs", await ScalarAsync(conn,
                $"SELECT COUNT(*) FROM purchase_order p WHERE {LivePo} AND (@Fy = 0 OR p.financial_year_id = @Fy)", ("@Fy", fyId)),
                "/EMSPODashboard", null, "info");
            Card(res, "poPendingSupply", "Pending Supply (POs)", await ScalarAsync(conn,
                $"SELECT COUNT(DISTINCT p.po_id) {PoBalanceSql} AND pi.quantity > ISNULL(sup.Supplyqty, 0)"),
                "/reports/pending-receipt-installation", null, "warning");
            Card(res, "poPendingInstall", "Pending Installation (POs)", await ScalarAsync(conn,
                $"SELECT COUNT(DISTINCT p.po_id) {PoBalanceSql} AND re.receiptQTY > ins.insqty"),
                "/reports/pending-receipt-installation", null, "warning");


            AddChart(res, await ChartAsync(conn, "poByMonth", fyId > 0 ? "POs by Month (this year)" : "POs by Month", "bar", "POs",
                $@"
SELECT LEFT(DATENAME(MONTH, p.po_date), 3) + ' ' + RIGHT(CONVERT(VARCHAR, YEAR(p.po_date)), 2), COUNT(*)
FROM purchase_order p
WHERE {LivePo} AND p.po_date IS NOT NULL
  AND ((@Fy = 0 AND p.po_date >= DATEADD(MONTH, -11, GETDATE())) OR p.financial_year_id = @Fy)
GROUP BY YEAR(p.po_date), MONTH(p.po_date), DATENAME(MONTH, p.po_date)
ORDER BY YEAR(p.po_date), MONTH(p.po_date)", ("@Fy", fyId)));

            if (full)
            {
                AddChart(res, await ChartAsync(conn, "poByStatus", "POs by Status", "donut", "POs", @"
SELECT p.status, COUNT(*) FROM purchase_order p
WHERE p.status IS NOT NULL AND p.status NOT IN ('Incomplete', 'Cancelled')
GROUP BY p.status ORDER BY COUNT(*) DESC"));
            }
        }

        // ---- Finance (GM Finance / FIN) ----
        private static async Task AddFinanceAsync(SqlConnection conn, DashboardResponse res, bool full)
        {
            // GMFI/GetPendingSanctions kind=TP / kind=P (same invoice filters)
            string sanctions(string pType, string extra) => $@"
SELECT COUNT(DISTINCT p.po_id) FROM purchase_order p
INNER JOIN BLPINVOICES b ON b.po_id = p.po_id
WHERE p.status IN ('Completed', 'Order Placed', 'Partially Received')
  AND b.status = '{pType}' AND b.SANCTIONID IS NOT NULL{extra}";
            Card(res, "finSanctionTP", "Pending Sanctions", await ScalarAsync(conn, sanctions("TP", "")),
                "/finance/pending-sanctions", null, "warning");
            if (full)
            {
                Card(res, "finSanctionP", "Pending Sanctions (Site Not Ready)",
                    await ScalarAsync(conn, sanctions("P", " AND b.SiteNotFlag = 'Y'")),
                    "/finance/pending-sanctions-site-not-ready", null, "warning");
            }
            // EMDRefund/pending-emd and pending-sd (no supplier / tender filter)
            Card(res, "finEmdPending", "EMD Refunds Pending", await ScalarAsync(conn, @"
SELECT COUNT(*) FROM EMDDepositeDetail ed
LEFT JOIN EMDFileMovement fm ON fm.FileID = ed.Id AND fm.SupplierID = ed.SupId
WHERE fm.BMEApproval IS NULL OR fm.BMEApproval <> 'Y'"),
                "/emd-refund/tenderwise", null, "info");
            if (full)
            {
                Card(res, "finSdPending", "SD Release Pending", await ScalarAsync(conn, @"
SELECT COUNT(*) FROM PO_SDDetails ps
INNER JOIN purchase_order p ON p.po_id = ps.po_id
WHERE ps.isreleased IS NULL OR ps.isreleased <> 'Y'"),
                    "/emd-refund/sd-release-finance", null, "info");

                AddChart(res, await ChartAsync(conn, "finSanctionByDir", "Pending Sanction Value by Directorate", "bar", "₹ Cr", @"
SELECT dir.facility_aut_name, CAST(SUM(b.grossamount) / 10000000.0 AS DECIMAL(18, 2))
FROM purchase_order p
INNER JOIN BLPINVOICES b ON b.po_id = p.po_id
INNER JOIN facility_aut dir ON dir.facility_aut_id = p.directorate_id
WHERE p.status IN ('Completed', 'Order Placed', 'Partially Received') AND b.status = 'TP' AND b.SANCTIONID IS NOT NULL
GROUP BY dir.facility_aut_name ORDER BY 2 DESC"));
            }
        }

        // ---- Supplier (own POs only; users.supplier_id) ----
        private static async Task AddSupplierAsync(SqlConnection conn, DashboardResponse res, int userId)
        {
            var sid = await ScalarAsync(conn, "SELECT supplier_id FROM users WHERE user_id = @U AND user_type = 'SUP'", ("@U", userId));
            if (sid == null || sid == 0) return;
            var p = ("@S", (object)(int)sid.Value);

            Card(res, "supOpenPo", "Open Purchase Orders", await ScalarAsync(conn,
                "SELECT COUNT(*) FROM purchase_order p WHERE p.status IN ('Order Placed') AND p.supplier_id = @S", p),
                "/orders/po-supply", null, "info");
            Card(res, "supPendingDispatch", "Pending Dispatch", await ScalarAsync(conn,
                $"SELECT COUNT(DISTINCT p.po_id) {PoBalanceSql} AND p.supplier_id = @S AND pi.quantity > ISNULL(sup.Supplyqty, 0)", p),
                "/reports/pending-receipt-installation", new() { ["type"] = "D" }, "warning");
            Card(res, "supPendingReceipt", "Pending Receipt", await ScalarAsync(conn,
                $"SELECT COUNT(DISTINCT p.po_id) {PoBalanceSql} AND p.supplier_id = @S AND sup.Supplyqty > re.receiptQTY", p),
                "/reports/pending-receipt-installation", new() { ["type"] = "R" }, "warning");
            Card(res, "supPendingInstall", "Pending Installation", await ScalarAsync(conn,
                $"SELECT COUNT(DISTINCT p.po_id) {PoBalanceSql} AND p.supplier_id = @S AND re.receiptQTY > ins.insqty", p),
                "/reports/pending-receipt-installation", new() { ["type"] = "I" }, "warning");
            Card(res, "supEmdPending", "EMD Refund Requests Pending", await ScalarAsync(conn, @"
SELECT COUNT(*) FROM EMDDepositeDetail ed
LEFT JOIN EMDFileMovement fm ON fm.FileID = ed.Id AND fm.SupplierID = ed.SupId
WHERE ed.SupId = @S AND (fm.BMEApproval IS NULL OR fm.BMEApproval <> 'Y')", p),
                "/emd-refund/emd-deposit", null, "neutral");

            AddChart(res, await ChartAsync(conn, "supPoByYear", "My POs per Year", "bar", "POs", $@"
SELECT f.year, COUNT(*) FROM purchase_order p
INNER JOIN mas_financial_year f ON f.financial_year_id = p.financial_year_id
WHERE {LivePo} AND p.supplier_id = @S
GROUP BY f.year, f.financial_year_id ORDER BY f.financial_year_id", p));
        }

        // ---- Facility / consignee users (DME po-dashboard: consignee location belongs to the user) ----
        private static async Task AddFacilityAsync(SqlConnection conn, DashboardResponse res, int userId)
        {
            if (userId <= 0) return;
            var u = ("@U", (object)userId);
            const string myPoItems = @"
FROM purchase_order p
INNER JOIN po_items pi ON pi.po_id = p.po_id
INNER JOIN maslocations l ON l.location_id = pi.consignee_id
WHERE p.status NOT IN ('Incomplete', 'Waiting For Approval', 'Cancelled') AND l.user_id = @U";

            Card(res, "facPo", "Purchase Orders", await ScalarAsync(conn, "SELECT COUNT(DISTINCT p.po_id) " + myPoItems, u),
                "/orders/purchase-order-dashboard", null, "info");
            // ordered quantity for my locations vs quantity received at my locations
            Card(res, "facPendingReceipt", "POs Pending Receipt", await ScalarAsync(conn, @"
SELECT COUNT(*) FROM (
    SELECT p.po_id, SUM(pi.quantity) AS ordered
    FROM purchase_order p
    INNER JOIN po_items pi ON pi.po_id = p.po_id
    INNER JOIN maslocations l ON l.location_id = pi.consignee_id
    WHERE p.status IN ('Order Placed') AND l.user_id = @U
    GROUP BY p.po_id
) o
LEFT OUTER JOIN (
    SELECT r.po_id, SUM(r.receipt_qty) AS received
    FROM receipts r INNER JOIN maslocations l ON l.location_id = r.location_id
    WHERE r.recieved_date IS NOT NULL AND r.status IN ('C', 'Received') AND l.user_id = @U
    GROUP BY r.po_id
) rc ON rc.po_id = o.po_id
WHERE o.ordered > ISNULL(rc.received, 0)", u),
                "/orders/purchase-order-receipts", null, "warning");
            Card(res, "facIndents", "Indents Raised", await ScalarAsync(conn,
                "SELECT COUNT(*) FROM mas_indentfacility a WHERE a.directorate_id = 12 AND a.location_id = @U", u),
                "/indents/annual-indent-report", null, "neutral");

            AddChart(res, await ChartAsync(conn, "facPoByYear", "My POs per Year", "bar", "POs", @"
SELECT f.year, COUNT(DISTINCT p.po_id)
FROM purchase_order p
INNER JOIN po_items pi ON pi.po_id = p.po_id
INNER JOIN maslocations l ON l.location_id = pi.consignee_id
INNER JOIN mas_financial_year f ON f.financial_year_id = p.financial_year_id
WHERE p.status NOT IN ('Incomplete', 'Waiting For Approval', 'Cancelled') AND l.user_id = @U
GROUP BY f.year, f.financial_year_id ORDER BY f.financial_year_id", u));
        }

        // ---- IT module (menus / roles / users) ----
        private static async Task AddItAsync(SqlConnection conn, DashboardResponse res)
        {
            Card(res, "itMenus", "Menus", await ScalarAsync(conn, "SELECT COUNT(*) FROM masmenu"), "/IT/add-sub-menu", null, "info");
            Card(res, "itSubMenus", "Sub Menus", await ScalarAsync(conn, "SELECT COUNT(*) FROM massubmenu"), "/IT/add-sub-menu");
            Card(res, "itRoles", "Roles", await ScalarAsync(conn, "SELECT COUNT(*) FROM masrole"), "/IT/add-role-in-screen");
            Card(res, "itUsers", "Users", await ScalarAsync(conn, "SELECT COUNT(*) FROM users"), "/IT/add-role-in-screen");

            AddChart(res, await ChartAsync(conn, "itUsersByType", "Users by Type", "bar", "users", @"
SELECT ISNULL(NULLIF(user_type, ''), 'Other'), COUNT(*) FROM users
GROUP BY ISNULL(NULLIF(user_type, ''), 'Other') ORDER BY COUNT(*) DESC"));
        }
    }
}
