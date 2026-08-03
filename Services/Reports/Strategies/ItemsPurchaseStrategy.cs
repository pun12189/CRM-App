using Tijori.Interfaces.Reports;
using Tijori.Models.Enums;
using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Services.Reports.Strategies
{
    public class ItemsPurchaseStrategy : IE2EReportStrategy
    {
        public string GetStrategyKey(E2EMainFilter main, E2EComparisonTarget target)
        {
            if (main == E2EMainFilter.Items && target == E2EComparisonTarget.Vendors)
            {
                return "Items_Vendors";
            }
            return string.Empty;
        }

        public async Task<DataTable> RunQueryAsync(IDbConnection db, DateTime from, DateTime to)
        {
            string sql = @"
                SELECT 
                    i.ItemName AS `Inventory Component`,
                    i.FrameSize AS `Frame Specification`,
                    v.VendorName AS `Primary Supplier Source`,
                    SUM(pi.Quantity) AS `Total Volume Procured`,
                    FORMAT(AVG(pi.UnitPrice), 2, 'en_IN') AS `Weighted Avg Cost (₹)`,
                    FORMAT(SUM(pi.TotalPrice), 2, 'en_IN') AS `Total Procurement Expenditure (₹)`
                FROM Items i
                INNER JOIN PurchaseItems pi ON i.ItemId = pi.ItemId
                INNER JOIN Purchases p ON pi.PurchaseId = p.PurchaseId
                INNER JOIN Vendors v ON p.VendorId = v.VendorId
                WHERE p.PurchaseDate >= @From AND p.PurchaseDate <= @To
                GROUP BY i.ItemId, i.ItemName, i.FrameSize, v.VendorName
                ORDER BY SUM(pi.TotalPrice) DESC;";

            var reader = await db.ExecuteReaderAsync(sql, new { From = from, To = to });
            var dt = new DataTable(); dt.Load(reader); return dt;
        }
    }
}
