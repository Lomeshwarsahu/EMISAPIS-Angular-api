using Microsoft.Data.SqlClient;
using System.Data;

//namespace EMISAPIS.Helpers
//{
//    public class Tender
//    {
//    }
//}


using System;
using Microsoft.Extensions.Configuration;

public class Tender
{
    private readonly string _connString;

    public Tender(IConfiguration configuration)
    {
        _connString = configuration.GetConnectionString("DefaultConnection");
    }

    public DataTable Get_TendersStatus(string stid, string undOBClaim)
    {
        string whUnderObjectClaim = "";
        string strcsid = "";

        if (undOBClaim == "Under Prep")
        {
            strcsid = " and csid in (2)";
            whUnderObjectClaim = " and sc.ISCovTechEli is not null ";
        }
        else if (undOBClaim == "Under Obj")
        {
            strcsid = " and csid in (2,7)";
            whUnderObjectClaim = " and getdate() > ObjCEndDT ";
        }
        else if (undOBClaim == "COVERB")
        {
            strcsid = " and csid in (3,4)";
            whUnderObjectClaim = " ";
        }
        else if (undOBClaim == "COVERC")
        {
            string strSQL_CoverC = @"
                SELECT 
                    tender_id,
                    ISNULL(tender_no, 'NA') + ' ,OpenDT-' + ISNULL(CONVERT(VARCHAR, cover_a, 103), 'NA') AS name   
                FROM tenders WITH (NOLOCK)
                WHERE 1=1 
                  AND financial_year_id >= 15 
                  AND (
                        (csid IN (3, 4, 5) AND cover_a IS NOT NULL)
                        OR 
                        (csid IN (5) AND (ISNULL(isgemTender, 'N') = 'Y' OR ISNULL(isDirectPurchase, 'N') = 'Y'))
                      )
                ORDER BY cover_a DESC";

            return ExecuteDataTable(strSQL_CoverC);
        }
        else if (undOBClaim == "GEM")
        {
            strcsid = " and csid in (2) and isgemTender = 'Y'";
            whUnderObjectClaim = " ";
        }
        else
        {
            strcsid = " and csid =" + stid;
        }

        if (undOBClaim == "Under Prep")
        {
            string strSQL = @"
                SELECT tender_id, tender_no + ' ,OpenDT-' + CONVERT(VARCHAR, cover_a, 103) AS name   
                FROM tenders t WITH (NOLOCK)
                INNER JOIN 
                (
                    SELECT sc.SCHEMEID, COUNT(DISTINCT sc.SUPPLIERID) AS nossupplierADA 
                    FROM masschemesstatusdetails sc WITH (NOLOCK)
                    INNER JOIN SCHEMESTATUSDETAILSCHILD sch WITH (NOLOCK) ON sch.SCHSTATUSDID = sc.SCHSTATUSDID
                    INNER JOIN tenders t WITH (NOLOCK) ON t.tender_id = sc.SCHEMEID
                    WHERE 1=1 " + whUnderObjectClaim + @"
                    GROUP BY sc.SCHEMEID
                ) scADA ON scADA.SCHEMEID = t.tender_id
                WHERE 1=1 AND csid in (2) AND financial_year_id >= 15 
                  AND cover_a IS NOT NULL 
                ORDER BY cover_a DESC";

            return ExecuteDataTable(strSQL);
        }
        else
        {
            string strSQL = @"
                SELECT tender_id, tender_no + ' ,OpenDT-' + CONVERT(VARCHAR, cover_a, 103) AS name   
                FROM tenders WITH (NOLOCK)
                WHERE 1=1 " + strcsid + " AND financial_year_id >= 15 AND cover_a IS NOT NULL " + whUnderObjectClaim + " ORDER BY cover_a DESC";

            return ExecuteDataTable(strSQL);
        }
    }

    // Helper method to execute query and return DataTable using ADO.NET
    private DataTable ExecuteDataTable(string query)
    {
        DataTable dt = new DataTable();
        using (SqlConnection conn = new SqlConnection(_connString))
        {
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    adapter.Fill(dt);
                }
            }
        }
        return dt;
    }
}