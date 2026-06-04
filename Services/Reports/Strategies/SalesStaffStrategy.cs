using CallMan.Interfaces.Reports;
using CallMan.Models.Enums;
using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Services.Reports.Strategies
{
    public class SalesStaffStrategy : IE2EReportStrategy
    {
        public string StrategyKey => "Sales_LeadHolder";

        public string GetStrategyKey(E2EMainFilter main, E2EComparisonTarget target)
        {
            if (main == E2EMainFilter.Sales && (target == E2EComparisonTarget.LeadHolder || target == E2EComparisonTarget.LeadHolder))
            {
                return StrategyKey;
            }
            return string.Empty;
        }

        public async Task<DataTable> RunQueryAsync(IDbConnection db, DateTime from, DateTime to)
        {
            string sql = @"
                SELECT 
                    u.FullName AS `Staff Name`,
                    u.Role AS `Staff Role`,
                    
                    -- 1. Base Target Pull (Multiplied dynamically by the number of months in the chosen date range)
                    CASE 
                        WHEN IFNULL(u.MonthlyTarget, 0) = 0 THEN 'Not Entered'
                        ELSE FORMAT(u.MonthlyTarget * GREATEST(1, PERIOD_DIFF(EXTRACT(YEAR_MONTH FROM @To), EXTRACT(YEAR_MONTH FROM @From)) + 1), 2, 'en_IN')
                    END AS `Assigned Target (₹)`,
                    
                    -- 2. Realized Productivity Metrics
                    COUNT(o.OrderId) AS `Invoices Count`,
                    FORMAT(IFNULL(SUM(o.TotalAmount), 0), 2, 'en_IN') AS `Achieved Business (₹)`,
                    
                    -- 3. Dynamic Conditional Performance Analysis
                    CASE 
                        -- Condition A: Target wasn't entered -> Just count total business (No performance percentage calculation)
                        WHEN IFNULL(u.MonthlyTarget, 0) = 0 THEN 'N/A (Counting Gross Business)'
                        
                        -- Condition B: Target is present -> Calculate absolute achievement ratio
                        ELSE CONCAT(ROUND((IFNULL(SUM(o.TotalAmount), 0) / (u.MonthlyTarget * GREATEST(1, PERIOD_DIFF(EXTRACT(YEAR_MONTH FROM @To), EXTRACT(YEAR_MONTH FROM @From)) + 1))) * 100, 2), '%')
                    END AS `Achievement Status`,
                    
                    -- 4. Shortfall / Surplus Variance Mapping
                    CASE 
                        -- 1. Target wasn't entered
                        WHEN IFNULL(u.MonthlyTarget, 0) = 0 THEN 'N/A'
    
                        -- 2. Target met or exceeded (Surplus)
                        WHEN IFNULL(SUM(o.TotalAmount), 0) >= (u.MonthlyTarget * GREATEST(1, PERIOD_DIFF(EXTRACT(YEAR_MONTH FROM @To), EXTRACT(YEAR_MONTH FROM @From)) + 1))
                            THEN CONCAT(
                                FORMAT(IFNULL(SUM(o.TotalAmount), 0) - (u.MonthlyTarget * GREATEST(1, PERIOD_DIFF(EXTRACT(YEAR_MONTH FROM @To), EXTRACT(YEAR_MONTH FROM @From)) + 1)), 2, 'en_IN'),
                                ' (Surplus)'
                            )
        
                        -- 3. Target missed (Shortfall)
                        ELSE CONCAT(
                            FORMAT((u.MonthlyTarget * GREATEST(1, PERIOD_DIFF(EXTRACT(YEAR_MONTH FROM @To), EXTRACT(YEAR_MONTH FROM @From)) + 1)) - IFNULL(SUM(o.TotalAmount), 0), 2, 'en_IN'),
                            ' (Shortfall)'
                        )
                    END AS `Variance Balance (₹)`,
                    
                    FORMAT(IFNULL(AVG(o.TotalAmount), 0), 2, 'en_IN') AS `Avg Ticket Size (₹)`
                    
                FROM Users u
                
                -- LEFT JOIN lets us see active staff members even if they haven't closed an order in this timeframe
                LEFT JOIN Orders o ON u.FullName = o.ProcessedBy AND o.OrderDate >= @From AND o.OrderDate <= @To
                
                WHERE u.IsActive = 1
                GROUP BY u.UserId, u.FullName, u.Role, u.MonthlyTarget
                ORDER BY IFNULL(SUM(o.GrandTotal), 0) DESC;";

            var reader = await db.ExecuteReaderAsync(sql, new { From = from, To = to });
            var dataTable = new DataTable();
            dataTable.Load(reader);
            return dataTable;
        }
    }
}
