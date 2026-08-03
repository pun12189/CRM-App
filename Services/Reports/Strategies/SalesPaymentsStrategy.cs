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
    public class SalesPaymentsStrategy : IE2EReportStrategy
    {
        public string StrategyKey => "Sales_Ledgers";

        public string GetStrategyKey(E2EMainFilter main, E2EComparisonTarget target)
        {
            if (main == E2EMainFilter.Sales && target == E2EComparisonTarget.Ledgers)
            {
                return StrategyKey;
            }
            return string.Empty;
        }

        public async Task<DataTable> RunQueryAsync(IDbConnection db, DateTime from, DateTime to)
        {
            string sql = @"
                SELECT 
                        l.CustomerName AS `Customer Name`,
                        l.CompanyName AS `Company Name`,
    
                        -- 1. Sales Billings Matrix                        
                        FORMAT(IFNULL(ord.GrossSales, 0), 2, 'en_IN') AS `Total Sales (₹)`,                        
    
                        -- 2. Payments Collection Matrix
                        IFNULL(pay.PaymentCount, 0) AS `Payments Count`,
                        FORMAT(IFNULL(pay.TotalCollected, 0), 2, 'en_IN') AS `Payments Received (₹)`,
                        
    
                        CASE 
                            -- Advance collection processing case
                            WHEN IFNULL(ord.GrossSales, 0) < IFNULL(pay.TotalCollected, 0) 
                                THEN FORMAT(IFNULL(pay.TotalCollected, 0) - IFNULL(ord.GrossSales, 0), 2, 'en_IN') + ' (Advance)'
                            -- Standard outstanding liabilities
                            ELSE FORMAT(IFNULL(ord.GrossSales, 0) - IFNULL(pay.TotalCollected, 0), 2, 'en_IN')
                        END AS `Outstanding Balance (₹)`
    
                    FROM Leads l

                    -- SUBQUERY A: Aggregate sales metrics cleanly over the selected date range
                    LEFT JOIN (
                        SELECT 
                            o.LeadId,
                            COUNT(DISTINCT o.OrderId) AS InvoiceCount,
                            SUM(o.TotalAmount) AS GrossSales,
                            SUM((
                                SELECT IFNULL(SUM(oi.GstAmount), 0) 
                                FROM OrderItems oi 
                                WHERE oi.OrderId = o.OrderId
                            )) AS TotalTax
                        FROM Orders o
                        WHERE o.OrderDate >= @From AND o.OrderDate <= @To
                        GROUP BY o.LeadId
                    ) ord ON l.LeadId = ord.LeadId

                    -- SUBQUERY B: Aggregate payment collection metrics over the selected date range
                    LEFT JOIN (
                        SELECT 
                            p.LeadId,
                            COUNT(DISTINCT p.PaymentId) AS PaymentCount,
                            SUM(p.AmountReceived) AS TotalCollected
                        FROM Payments p
                        WHERE p.PaymentDate >= @From AND p.PaymentDate <= @To
                        GROUP BY p.LeadId
                    ) pay ON l.LeadId = pay.LeadId

                    -- CORE STRUCTURAL FILTERS
                    WHERE l.Status = 'Matured'
                      AND (ord.InvoiceCount > 0 OR pay.PaymentCount > 0)
                    ORDER BY ord.GrossSales DESC, pay.TotalCollected DESC;";

            var reader = await db.ExecuteReaderAsync(sql, new { From = from, To = to });
            var dataTable = new DataTable();
            dataTable.Load(reader);
            return dataTable;
        }
    }
}
