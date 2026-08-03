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
    public class SalesBusinessStrategy : IE2EReportStrategy
    {
        public string StrategyKey => "Sales_Business";

        public string GetStrategyKey(E2EMainFilter main, E2EComparisonTarget target)
        {
            if (main == E2EMainFilter.Sales && target == E2EComparisonTarget.Business)
            {
                return StrategyKey;
            }
            return string.Empty;
        }

        public async Task<DataTable> RunQueryAsync(IDbConnection db, DateTime from, DateTime to)
        {
            string sql = @"
                SELECT 
                    -- 1. Operational Volume Overview
                    COUNT(DISTINCT o.OrderId) AS `Total Orders Generated`,
                    SUM(IFNULL((SELECT SUM(oi.Quantity) FROM OrderItems oi WHERE oi.OrderId = o.OrderId), 0)) AS `Total Units Sold`,
                    COUNT(DISTINCT o.LeadId) AS `Active Customers`,

                    -- 2. Financial Metrics Breakdowns
                    FORMAT(IFNULL(SUM(o.TotalAmount), 0), 2, 'en_IN') AS `Gross Business (₹)`,
                    FORMAT(IFNULL(SUM((
                        SELECT IFNULL(SUM(oi.GstAmount), 0) 
                        FROM OrderItems oi 
                        WHERE oi.OrderId = o.OrderId
                    )), 0), 2, 'en_IN') AS `GstAmount (₹)`,
                    FORMAT(IFNULL(SUM(o.TotalAmount), 0) - IFNULL(SUM((
                        SELECT IFNULL(SUM(oi.GstAmount), 0) 
                        FROM OrderItems oi 
                        WHERE oi.OrderId = o.OrderId
                    )), 0), 2, 'en_IN') AS `Net Sales (₹)`,

                    -- 3. Average Value Efficiency Analytics
                    FORMAT(IFNULL(AVG(o.TotalAmount), 0), 2, 'en_IN') AS `Average Ticket Size (₹)`,
                    
                    -- 4. Dynamic Cash Liquidity Matrix (Calculated from Payments Ledger basis)
                    FORMAT(IFNULL(pay.ActualCashInflow, 0), 2, 'en_IN') AS `Total Liquidity Received (₹)`,
                    
                    -- 5. Outstanding Receivables Risk Variance Calculation
                    CASE 
                        WHEN IFNULL(SUM(o.TotalAmount), 0) < IFNULL(pay.ActualCashInflow, 0)
                            THEN CONCAT(FORMAT(IFNULL(pay.ActualCashInflow, 0) - IFNULL(SUM(o.TotalAmount), 0), 2, 'en_IN'), ' (Advance Pool)')
                        ELSE FORMAT(IFNULL(SUM(o.TotalAmount), 0) - IFNULL(pay.ActualCashInflow, 0), 2, 'en_IN')
                    END AS `Total Outstanding Receivables (₹)`

                FROM Orders o
                
                -- CROSS JOIN to an isolated single-row payments aggregation summary block
                CROSS JOIN (
                    SELECT SUM(p.AmountReceived) AS ActualCashInflow
                    FROM Payments p
                    WHERE p.PaymentDate >= @From AND p.PaymentDate <= @To
                ) pay
                
                WHERE o.OrderDate >= @From AND o.OrderDate <= @To
                GROUP BY pay.ActualCashInflow;";

            var reader = await db.ExecuteReaderAsync(sql, new { From = from, To = to });
            var dataTable = new DataTable();
            dataTable.Load(reader);
            return dataTable;
        }
    }
}
