using System.Data;
using System.Net;
using System.Text;

namespace EMISAPIS.Helpers
{
    /// <summary>
    /// Printable HTML replacement for legacy RDLC reports (ReportViewer is not available on .NET 6).
    /// The browser opens the page and the user prints or saves it as PDF. All values are HTML-encoded.
    /// </summary>
    public class HtmlReportBuilder
    {
        private readonly StringBuilder _body = new();
        private readonly string _title;

        public HtmlReportBuilder(string title)
        {
            _title = title;
        }

        public static string Encode(object? value) =>
            WebUtility.HtmlEncode(value == null || value == DBNull.Value ? string.Empty : value.ToString());

        public HtmlReportBuilder Heading(string text)
        {
            _body.Append("<h3>").Append(Encode(text)).Append("</h3>");
            return this;
        }

        public HtmlReportBuilder InfoGrid(IEnumerable<(string Label, object? Value)> items)
        {
            _body.Append("<div class=\"info\">");
            foreach (var (label, value) in items)
                _body.Append("<div><span>").Append(Encode(label)).Append(":</span> <strong>")
                     .Append(Encode(value)).Append("</strong></div>");
            _body.Append("</div>");
            return this;
        }

        /// <summary>Table from rows; each column maps a header to a value selector.</summary>
        public HtmlReportBuilder Table(IEnumerable<DataRow> rows, params (string Header, Func<DataRow, object?> Value)[] columns)
        {
            _body.Append("<table><thead><tr><th>S.No</th>");
            foreach (var c in columns) _body.Append("<th>").Append(Encode(c.Header)).Append("</th>");
            _body.Append("</tr></thead><tbody>");
            int i = 0;
            foreach (var row in rows)
            {
                _body.Append("<tr><td>").Append(++i).Append("</td>");
                foreach (var c in columns) _body.Append("<td>").Append(Encode(c.Value(row))).Append("</td>");
                _body.Append("</tr>");
            }
            if (i == 0)
                _body.Append("<tr><td colspan=\"").Append(columns.Length + 1).Append("\" class=\"empty\">No records.</td></tr>");
            _body.Append("</tbody></table>");
            return this;
        }

        public HtmlReportBuilder Paragraph(string text)
        {
            _body.Append("<p>").Append(Encode(text)).Append("</p>");
            return this;
        }

        public HtmlReportBuilder Signatures(params string[] names)
        {
            _body.Append("<div class=\"sign\">");
            foreach (var n in names) _body.Append("<div>").Append(Encode(n)).Append("</div>");
            _body.Append("</div>");
            return this;
        }

        public string Build()
        {
            return "<!doctype html><html><head><meta charset=\"utf-8\"><title>" + Encode(_title) + "</title><style>"
                + "body{font-family:Arial,sans-serif;font-size:12px;color:#111;margin:24px}"
                + ".org{text-align:center;font-weight:700;font-size:15px}h2{text-align:center;margin:4px 0 14px;font-size:14px}"
                + ".info{display:grid;grid-template-columns:repeat(2,1fr);gap:4px 24px;margin-bottom:12px}.info span{color:#555}"
                + "h3{font-size:13px;margin:16px 0 6px}table{width:100%;border-collapse:collapse;margin-bottom:10px}"
                + "th,td{border:1px solid #999;padding:4px 6px;text-align:left;vertical-align:top}th{background:#eee}"
                + "td.empty{text-align:center;color:#666}.sign{display:flex;justify-content:space-between;margin-top:48px;font-weight:600}"
                + ".foot{margin-top:16px;color:#555;font-size:11px}@media print{body{margin:10mm}}"
                + "</style></head><body onload=\"window.print()\">"
                + "<div class=\"org\">CHHATTISGARH MEDICAL SERVICES CORPORATION LTD</div>"
                + "<h2>" + Encode(_title) + "</h2>"
                + _body
                + "<div class=\"foot\">Print DT &amp; Time: " + DateTime.Now.ToString("dd/MM/yyyy hh:mm tt") + " — System generated from EMIS</div>"
                + "</body></html>";
        }
    }
}
