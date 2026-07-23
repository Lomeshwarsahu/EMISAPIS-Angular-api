using EMISAPIS.DTOS;
using Microsoft.Data.SqlClient;

namespace EMISAPIS.Helpers
{
    /// <summary>Shared loader for rdlcPoReport.aspx / supplier &amp; DME PO print JSON.</summary>
    public static class PoPrintReportLoader
    {
        public static async Task<SupplierPoPrintDto?> LoadAsync(SqlConnection con, int poId)
        {
            const string itemsSql = @"
SELECT dbo.purchase_order.outward_no,
       CASE WHEN dbo.purchase_order.outward_no IS NOT NULL
            THEN CONVERT(VARCHAR(10), dbo.purchase_order.po_date, 103) ELSE '' END AS po_date,
       dbo.purchase_order.po_no,
       dbo.purchase_order.po_id,
       dbo.masitems.item_name,
       dbo.massuppliers.name,
       dbo.massuppliers.address,
       dbo.massuppliers.mobile_no,
       dbo.massuppliers.email_id,
       dbo.tenders.tender_no,
       CONVERT(VARCHAR(10), dbo.tenders.tender_date, 103) AS tender_date,
       dbo.masitems.item_code_as_per_tender AS item_code,
       dbo.masitems.item_desc,
       dbo.contract_items.model,
       dbo.contract_items.single_unit_price,
       dbo.contract_items.basic_rate,
       SUM(dbo.po_items.quantity) AS quantity,
       dbo.purchase_order.total_po_valuew AS Expr2,
       dbo.purchase_order.po_valuew AS Expr3,
       dbo.purchase_order.total_po_value AS totalsum,
       dbo.purchase_order.po_value,
       dbo.contract_items.percentage,
       dbo.po_tranche.tranche_days,
       ISNULL(LTP.CMC1, 0) AS CMC1,
       ISNULL(LTP.cmc2, 0) AS CMC2,
       ISNULL(LTP.cmc3, 0) AS CMC3,
       ISNULL(LTP.cmc4, 0) AS CMC4,
       ISNULL(LTP.cmc5, 0) AS CMC5,
       pia.Prev_SoissueNo,
       CONVERT(VARCHAR(10), pia.Prev_SoissueDT, 103) AS Prev_SoissueDT,
       ISNULL(amencount, 0) AS amencount,
       CASE WHEN dbo.tenders.isGemTender = 'Y' THEN dbo.purchase_order.GeMBidno ELSE 'NA' END AS GemPO
FROM dbo.purchase_order
INNER JOIN dbo.po_items ON dbo.purchase_order.po_id = dbo.po_items.po_id
INNER JOIN dbo.po_tranche ON dbo.po_tranche.po_id = dbo.purchase_order.po_id
INNER JOIN dbo.masitems ON dbo.po_items.item_id = dbo.masitems.item_id
INNER JOIN dbo.massuppliers ON dbo.purchase_order.supplier_id = dbo.massuppliers.supplier_id
INNER JOIN dbo.tenders ON dbo.purchase_order.tender_id = dbo.tenders.tender_id
LEFT OUTER JOIN DBO.tender_items TI ON TI.tender_id = DBO.tenders.tender_id AND TI.item_id = dbo.po_items.item_id
LEFT OUTER JOIN live_tender_price LTP ON LTP.tender_item_id = TI.tender_item_id AND ltp.isaccept = 'Y'
INNER JOIN dbo.maslocations ON dbo.po_items.consignee_id = dbo.maslocations.location_id
INNER JOIN dbo.facility_aut ON dbo.facility_aut.facility_aut_id = dbo.maslocations.authority
INNER JOIN dbo.contract_items ON dbo.contract_items.contract_item_id = dbo.po_items.contract_item_id
INNER JOIN dbo.award_of_contract ON dbo.award_of_contract.award_of_contract_id = dbo.contract_items.award_of_contract_id
LEFT OUTER JOIN (
    SELECT po_id, MAX(po_ammdid) AS po_ammdid FROM PODTAmendment GROUP BY po_id
) mAmd ON mAmd.po_id = dbo.purchase_order.po_id
LEFT OUTER JOIN PODTAmendment pia ON pia.po_ammdid = mAmd.po_ammdid
LEFT OUTER JOIN (
    SELECT po_id, COUNT(po_ammdid) AS amencount FROM PODTAmendment GROUP BY po_id
) mamtcount ON mamtcount.po_id = dbo.purchase_order.po_id
WHERE dbo.purchase_order.po_id = @PoId
GROUP BY dbo.purchase_order.po_no,
         dbo.purchase_order.po_id,
         dbo.masitems.item_name,
         dbo.massuppliers.name,
         dbo.massuppliers.address,
         dbo.massuppliers.mobile_no,
         dbo.massuppliers.email_id,
         dbo.tenders.tender_no,
         dbo.tenders.tender_date,
         dbo.masitems.item_code_as_per_tender,
         dbo.masitems.item_desc,
         dbo.contract_items.model,
         dbo.contract_items.single_unit_price,
         dbo.contract_items.basic_rate,
         dbo.purchase_order.outward_no,
         dbo.purchase_order.po_date,
         dbo.purchase_order.total_po_valuew,
         dbo.purchase_order.po_valuew,
         dbo.purchase_order.total_po_value,
         dbo.purchase_order.po_value,
         dbo.contract_items.percentage,
         dbo.po_tranche.tranche_days,
         LTP.cmc1, LTP.cmc2, LTP.cmc3, LTP.cmc4, LTP.cmc5,
         pia.Prev_SoissueNo, pia.Prev_SoissueDT, amencount,
         dbo.purchase_order.GeMBidno, dbo.tenders.isGemTender";

            const string termsSql = @"
SELECT term_condition_id, term_condition
FROM terms_conditions t
INNER JOIN purchase_order p ON p.tender_id = t.tender_id
WHERE p.po_id = @PoId";

            const string consigneeSql = @"
SELECT DISTINCT CONVERT(VARCHAR, icd.consolidated_date, 103) AS consolidated_date,
       ml.location_name,
       i.quantity
FROM po_items i
INNER JOIN PURCHASE_ORDER p ON i.po_id = p.po_id
INNER JOIN award_of_contract awc ON awc.tender_id = p.tender_id AND awc.supplier_id = p.supplier_id
INNER JOIN maslocations ml ON ml.location_id = i.consignee_id
INNER JOIN indent_items ii ON ii.item_id = i.item_id AND ii.indent_item_id = i.indent_item_id AND ii.indent_id = i.indent_id
INNER JOIN indent id ON id.indent_id = ii.indent_id AND id.facility_id = ml.location_id
INNER JOIN indent_cons_items ic ON ic.indent_cons_items_id = id.indent_cons_items_id AND ic.item_id = i.item_id
INNER JOIN indent_consolidation icd ON icd.indent_consolidation_id = ic.indent_consolidated_id
    AND icd.directorate_id = i.directorate_id AND icd.indent_consolidation_id = i.INDENT_CONSOLIDATION_ID
WHERE p.po_id = @PoId";

            var report = new SupplierPoPrintDto { PoId = poId };
            bool hasItems = false;

            await using (var itemsCmd = new SqlCommand(itemsSql, con))
            {
                itemsCmd.Parameters.AddWithValue("@PoId", poId);
                await using var reader = await itemsCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!hasItems)
                    {
                        hasItems = true;
                        report.OutwardNo = ReadString(reader, "outward_no");
                        report.PoDate = ReadString(reader, "po_date");
                        report.PoNo = ReadString(reader, "po_no");
                        report.SupplierName = ReadString(reader, "name");
                        report.SupplierAddress = ReadString(reader, "address");
                        report.MobileNo = ReadString(reader, "mobile_no");
                        report.EmailId = ReadString(reader, "email_id");
                        report.TenderNo = ReadString(reader, "tender_no");
                        report.TenderDate = ReadString(reader, "tender_date");
                        report.TotalPoValueWords = ReadString(reader, "Expr2");
                        report.BasicRate = ReadDecimal(reader, "basic_rate");
                        report.GstPercent = ReadDecimal(reader, "percentage");
                        report.TrancheDays = (int)ReadDecimal(reader, "tranche_days");
                        report.GemPo = ReadString(reader, "GemPO");
                        report.Cmc1 = FormatCmc(ReadString(reader, "CMC1"));
                        report.Cmc2 = FormatCmc(ReadString(reader, "CMC2"));
                        report.Cmc3 = FormatCmc(ReadString(reader, "CMC3"));
                        report.Cmc4 = FormatCmc(ReadString(reader, "CMC4"));
                        report.Cmc5 = FormatCmc(ReadString(reader, "CMC5"));
                        report.AmendNo = ReadString(reader, "amencount");
                        report.PreviousOutwardNo = ReadString(reader, "Prev_SoissueNo");
                        report.PreviousPoDate = ReadString(reader, "Prev_SoissueDT");
                    }

                    decimal quantity = ReadDecimal(reader, "quantity");
                    decimal unitPrice = ReadDecimal(reader, "single_unit_price");
                    report.Items.Add(new SupplierPoPrintItemDto
                    {
                        ItemCode = ReadString(reader, "item_code"),
                        ItemName = ReadString(reader, "item_name"),
                        Model = ReadString(reader, "model"),
                        Quantity = quantity,
                        SingleUnitPrice = unitPrice,
                        LineAmount = quantity * unitPrice,
                    });
                    report.ItemsTotal += quantity * unitPrice;
                }
            }

            if (!hasItems)
                return null;

            await using (var termsCmd = new SqlCommand(termsSql, con))
            {
                termsCmd.Parameters.AddWithValue("@PoId", poId);
                await using var reader = await termsCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    report.Terms.Add(new SupplierPoPrintTermDto
                    {
                        TermConditionId = (int)ReadDecimal(reader, "term_condition_id"),
                        TermCondition = ReadString(reader, "term_condition"),
                    });
                }
            }

            await using (var consigneeCmd = new SqlCommand(consigneeSql, con))
            {
                consigneeCmd.Parameters.AddWithValue("@PoId", poId);
                await using var reader = await consigneeCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    report.Consignees.Add(new SupplierPoPrintConsigneeDto
                    {
                        ConsolidatedDate = ReadString(reader, "consolidated_date"),
                        LocationName = ReadString(reader, "location_name"),
                        Quantity = ReadDecimal(reader, "quantity"),
                    });
                }
            }

            string? copyToSql = await BuildCopyToSqlAsync(con, poId);
            if (!string.IsNullOrWhiteSpace(copyToSql))
            {
                await using var copyCmd = new SqlCommand(copyToSql, con);
                copyCmd.Parameters.AddWithValue("@PoId", poId);
                await using var reader = await copyCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    report.CopyTo.Add(new SupplierPoPrintCopyToDto
                    {
                        Designation = ReadString(reader, "desig"),
                        Office = ReadString(reader, "office"),
                    });
                }
            }

            return report;
        }

        private static async Task<string?> BuildCopyToSqlAsync(SqlConnection con, int poId)
        {
            const string directorateSql = "SELECT directorate_id FROM purchase_order WHERE po_id = @PoId";
            string directorateId = string.Empty;

            await using (var directorateCmd = new SqlCommand(directorateSql, con))
            {
                directorateCmd.Parameters.AddWithValue("@PoId", poId);
                object? result = await directorateCmd.ExecuteScalarAsync();
                directorateId = result == null || result == DBNull.Value
                    ? string.Empty
                    : Convert.ToString(result) ?? string.Empty;
            }

            return directorateId switch
            {
                "12" => @"
SELECT nameconsignee AS desig, ', ' + facility_aut_name AS office
FROM facility_aut WHERE facility_aut_id = 12
UNION ALL
SELECT DISTINCT u.designation AS desig, ', ' + u.user_name AS office
FROM po_items pi
INNER JOIN purchase_order p ON p.po_id = pi.po_id
INNER JOIN maslocations l ON l.location_id = pi.consignee_id
INNER JOIN users u ON u.user_id = l.user_id
LEFT OUTER JOIN facility_type ft ON ft.facility_type_id = l.facility_type_id
WHERE pi.po_id = @PoId
UNION ALL
SELECT 'GM Finance' AS desig, ', CGMSC' AS office FROM facility_aut WHERE facility_aut_id = 12",
                "5" => @"
SELECT nameconsignee AS desig, ',' + facility_aut_name AS office
FROM facility_aut WHERE facility_aut_id = 5
UNION ALL
SELECT DISTINCT 'CMHO' AS desig, ', ' + d.DBStart_Name_En AS office
FROM po_items pi
INNER JOIN purchase_order p ON p.po_id = pi.po_id
INNER JOIN maslocations l ON l.location_id = pi.consignee_id
INNER JOIN Districts d ON d.DP_DistrictID = l.DP_DistrictID
LEFT OUTER JOIN facility_type ft ON ft.facility_type_id = l.facility_type_id
WHERE pi.po_id = @PoId
UNION ALL
SELECT DISTINCT ft.designation, ',' + l.location_name AS office
FROM po_items pi
INNER JOIN purchase_order p ON p.po_id = pi.po_id
INNER JOIN maslocations l ON l.location_id = pi.consignee_id
LEFT OUTER JOIN facility_type ft ON ft.facility_type_id = l.facility_type_id
WHERE pi.po_id = @PoId
UNION ALL
SELECT 'GM Finance' AS desig, ',CGMSC' AS office FROM facility_aut WHERE facility_aut_id = 12",
                "9" or "11" or "1" or "7" or "6" or "13" => $@"
SELECT nameconsignee AS desig, ', ' + facility_aut_name AS office
FROM facility_aut WHERE facility_aut_id = {directorateId}
UNION ALL
SELECT DISTINCT u.designation AS desig, ', ' + u.user_name AS office
FROM po_items pi
INNER JOIN purchase_order p ON p.po_id = pi.po_id
INNER JOIN maslocations l ON l.location_id = pi.consignee_id
INNER JOIN users u ON u.user_id = l.user_id
LEFT OUTER JOIN facility_type ft ON ft.facility_type_id = l.facility_type_id
WHERE pi.po_id = @PoId
UNION ALL
SELECT 'GM Finance' AS desig, ', CGMSC' AS office FROM facility_aut WHERE facility_aut_id = 12",
                _ => null,
            };
        }

        private static string FormatCmc(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "NA";
            return decimal.TryParse(value, out decimal parsed) && parsed == 0 ? "NA" : value;
        }

        private static string ReadString(SqlDataReader reader, string columnName)
        {
            try
            {
                int ordinal = reader.GetOrdinal(columnName);
                if (!reader.IsDBNull(ordinal))
                    return reader.GetValue(ordinal)?.ToString() ?? string.Empty;
            }
            catch (IndexOutOfRangeException)
            {
            }

            return string.Empty;
        }

        private static decimal ReadDecimal(SqlDataReader reader, string columnName)
        {
            try
            {
                int ordinal = reader.GetOrdinal(columnName);
                if (!reader.IsDBNull(ordinal))
                    return Convert.ToDecimal(reader.GetValue(ordinal));
            }
            catch (IndexOutOfRangeException)
            {
            }

            return 0;
        }
    }
}
