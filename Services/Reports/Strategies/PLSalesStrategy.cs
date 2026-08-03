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
    public class PLSalesStrategy : IE2EReportStrategy
    {
        public string GetStrategyKey(E2EMainFilter main, E2EComparisonTarget target)
        {
            if (main == E2EMainFilter.Sales && target == E2EComparisonTarget.PL)
            {
                return "Sales_PL";
            }
            return string.Empty;
        }

        public async Task<DataTable> RunQueryAsync(IDbConnection db, DateTime from, DateTime to)
        {
            string sql = @"
                SELECT 
                    i.Name AS `Product Name`,
                    SUM(oi.Quantity) AS `Units Sold`,
                    FORMAT(SUM(oi.Total), 2, 'en_IN') AS `Gross Sales Yield (₹)`,
                    FORMAT(SUM(oi.Quantity * i.CostPrice), 2, 'en_IN') AS `Estimated COGS Cost (₹)`,
                    FORMAT(SUM(oi.Total) - SUM(oi.Quantity * i.CostPrice), 2, 'en_IN') AS `Net Margin Profit (₹)`,
                    ROUND(((SUM(oi.Total) - SUM(oi.Quantity * i.CostPrice)) / SUM(oi.Total)) * 100, 2) AS `Profit Margin Ratio %`
                FROM Products i
                INNER JOIN OrderItems oi ON i.ProductId = oi.ProductId
                INNER JOIN Orders o ON oi.OrderId = o.OrderId
                WHERE o.OrderDate >= @From AND o.OrderDate <= @To
                GROUP BY i.ProductId, i.Name
                ORDER BY (SUM(oi.Total) - SUM(oi.Quantity * i.CostPrice)) DESC;";

            var reader = await db.ExecuteReaderAsync(sql, new { From = from, To = to });
            var dt = new DataTable(); dt.Load(reader); return dt;
        }
    }
}
