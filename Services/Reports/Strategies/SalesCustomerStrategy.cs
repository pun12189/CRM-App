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
    public class SalesCustomerStrategy : IE2EReportStrategy
    {
        public string GetStrategyKey(E2EMainFilter main, E2EComparisonTarget target)
        {
            // ONLY claim the key when the specific Sales X Customer intersection is being evaluated
            if (main == E2EMainFilter.Sales && target == E2EComparisonTarget.Customer)
            {
                return "Sales_Customer";
            }

            // Return empty for the other 39 combinations so the loop skips it safely
            return string.Empty;
        }

        public async Task<DataTable> RunQueryAsync(IDbConnection db, DateTime from, DateTime to)
        {
            string sql = @"
                SELECT 
                    l.CustomerName AS `Customer Name`,
                    l.CompanyName AS `Company Name`,
                    l.Phone AS `Contact`,
                    
                    -- Order Aggregations (Sourced via structural child row sums)
                    IFNULL(ord.TotalOrders, 0) AS `Total Orders`,
                    FORMAT(IFNULL(ord.GrossBillings, 0), 2, 'en_IN') AS `Net Sales (₹)`,                    
                    
                    -- Payment Metrics
                    FORMAT(IFNULL(pay.TotalPaid, 0), 2, 'en_IN') AS `Amount Received (₹)`,
                    FORMAT(IFNULL(ord.GrossBillings, 0) - IFNULL(pay.TotalPaid, 0), 2, 'en_IN') AS `Outstanding Balance (₹)`
                    
                FROM Leads l
                
                -- SUBQUERY A: Extracts orders data and breaks down line item GST amounts
                LEFT JOIN (
                    SELECT 
                        o.LeadId,
                        COUNT(DISTINCT o.OrderId) AS TotalOrders,
                        SUM(o.TotalAmount) AS GrossBillings,
                        -- Explicitly sum up item level GstAmount flags per active order record
                        SUM((
                            SELECT IFNULL(SUM(oi.GstAmount), 0) 
                            FROM OrderItems oi 
                            WHERE oi.OrderId = o.OrderId
                        )) AS TotalGst
                    FROM Orders o
                    WHERE o.OrderDate >= @From AND o.OrderDate <= @To
                    GROUP BY o.LeadId
                ) ord ON l.LeadId = ord.LeadId
                
                -- SUBQUERY B: Combines payment rows without creating structural row duplicates
                LEFT JOIN (
                    SELECT 
                        p.LeadId,
                        SUM(p.AmountReceived) AS TotalPaid
                    FROM Payments p
                    WHERE p.PaymentDate >= @From AND p.PaymentDate <= @To
                    GROUP BY p.LeadId
                ) pay ON l.LeadId = pay.LeadId
                
                -- SCHEMA CRITERIA RULES
                WHERE l.Status = 'Matured'
                  AND (ord.TotalOrders > 0 OR pay.TotalPaid > 0)
                ORDER BY ord.GrossBillings DESC;";

            var reader = await db.ExecuteReaderAsync(sql, new { From = from, To = to });
            var dt = new DataTable(); dt.Load(reader); return dt;
        }
    }
}
