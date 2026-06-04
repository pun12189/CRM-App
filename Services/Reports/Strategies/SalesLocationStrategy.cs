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
    public class SalesLocationStrategy : IE2EReportStrategy
    {
        public string StrategyKey => "Sales_Areas";

        public string GetStrategyKey(E2EMainFilter main, E2EComparisonTarget target)
        {
            if (main == E2EMainFilter.Sales && target == E2EComparisonTarget.Areas)
            {
                return StrategyKey;
            }
            return string.Empty;
        }

        public async Task<DataTable> RunQueryAsync(IDbConnection db, DateTime from, DateTime to)
        {
            // Aggregates total order value, collected GST, and client density by location parameters
            string sql = @"
                SELECT 
                    -- Fallback to 'Unknown Region' if the City or Area column is unpopulated
                    IF(SystemicLocation.CityOrArea IS NULL OR SystemicLocation.CityOrArea = '', 'Unknown Region', SystemicLocation.CityOrArea) AS `Location`,
    
                    -- Penetration Analytics: Count unique converted customer profiles inside this region
                    COUNT(DISTINCT l.LeadId) AS `Customers Count`,
    
                    -- Financial Sales Metrics aggregated across this location
                    IFNULL(SUM(SystemicLocation.InvoiceCount), 0) AS `Total Orders`,
                    FORMAT(IFNULL(SUM(SystemicLocation.GrossRevenue), 0), 2, 'en_IN') AS `Total Business (₹)`,
                    FORMAT(IFNULL(SUM(SystemicLocation.GrossRevenue) - IFNULL(SUM(SystemicLocation.LocationGst), 0), 0), 2, 'en_IN') AS `Net Sales (₹)`
    
                FROM Leads l

                -- INNER JOIN to an aggregated orders subquery containing location strings
                INNER JOIN (
                    SELECT 
                        o.LeadId,
                        -- Clean and trim the location source column (Swapping l.City or your designated Area string here)
                        TRIM(UPPER(cl.District)) AS CityOrArea, 
                        COUNT(o.OrderId) AS InvoiceCount,
                        SUM(o.TotalAmount) AS GrossRevenue,
                        SUM((
                            SELECT IFNULL(SUM(oi.GstAmount), 0) 
                            FROM OrderItems oi 
                            WHERE oi.OrderId = o.OrderId
                        )) AS LocationGst
                    FROM Orders o
                    INNER JOIN Leads cl ON o.LeadId = cl.LeadId
                    WHERE o.OrderDate >= @From AND o.OrderDate <= @To
                    GROUP BY o.LeadId, TRIM(UPPER(cl.City))
                ) SystemicLocation ON l.LeadId = SystemicLocation.LeadId

                -- CORE STRUCTURAL CRITERIA
                WHERE l.Status = 'Matured'
                GROUP BY SystemicLocation.CityOrArea
                ORDER BY SUM(SystemicLocation.GrossRevenue) DESC;";

            var reader = await db.ExecuteReaderAsync(sql, new { From = from, To = to });
            var dataTable = new DataTable();
            dataTable.Load(reader);
            return dataTable;
        }
    }
}
