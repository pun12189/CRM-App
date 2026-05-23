using CallMan.Data;
using CallMan.Models;
using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Services
{
    public class OccupiedLocationService
    {
        private readonly CrmDbContext _db;
        public OccupiedLocationService(CrmDbContext db) => _db = db;

        public async Task<IEnumerable<OccupiedLocation>> GetOccupiedLocationsAsync(string stateFilter = null)
        {
            using var conn = _db.CreateConnection();
            string stateCondition;

            if (stateFilter == null)
            {
                stateCondition = "AND (@State IS NULL OR l.State = @State)"; // Get All if nothing is selected
            }
            else if (string.IsNullOrEmpty(stateFilter))
            {
                stateCondition = "AND (l.State IS NULL OR l.State = '')"; // No additional filter for "All"
            }
            else
            {
                stateCondition = "AND l.State = @State";
            }

            string sql = $@"
            SELECT 
                l.LeadId AS Id, l.State, l.District, l.City, l.WorkingArea, l.Pincode,
                l.CustomerName, l.CompanyName AS FirmName,
                l.LeadHolder, u1.Phone, u2.FullName AS Senior, d.*,
                (SELECT COUNT(*) FROM Orders WHERE LeadId = l.LeadId) AS TotalOrders,
                (SELECT IFNULL(SUM(TotalOrderValue), 0) FROM Payments WHERE LeadId = l.LeadId) AS TotalPayments
            FROM Leads l
            LEFT JOIN Users u1 ON l.LeadHolder = u1.FullName
            LEFT JOIN Users u2 ON u1.SeniorId = u2.UserId
            LEFT JOIN LeadDivisions ld ON l.LeadId = ld.LeadId 
            LEFT JOIN Divisions d ON ld.DivisionId = d.Id
            WHERE l.Status = 'Matured' 
            {stateCondition} ORDER BY l.State ASC;";

            var locationLookup = new Dictionary<int, OccupiedLocation>();

            var locations = await conn.QueryAsync<OccupiedLocation, Division, OccupiedLocation>(
                sql,
                (location, division) =>
                {
                    if (!locationLookup.TryGetValue(location.Id, out var existingLocation))
                    {
                        existingLocation = location;
                        locationLookup.Add(existingLocation.Id, existingLocation);
                    }

                    // Append the child division instance natively to the ObservableCollection structure
                    if (division != null && !existingLocation.AssignedDivisions.Any(x => x.Id == division.Id))
                    {
                        existingLocation.AssignedDivisions.Add(division);
                    }

                    return existingLocation;
                },
                new { State = stateFilter },
                splitOn: "Id"
            );

            return locationLookup.Values;
        }

        public async Task<IEnumerable<StateStat>> GetStateStatsAsync()
        {
            using var conn = _db.CreateConnection();
            string sql = @"
        SELECT 
            State, 
            CAST(SUM(CASE WHEN Status = 'Matured' THEN 1 ELSE 0 END) AS SIGNED) AS MaturedCount, 
            COUNT(*) AS TotalLeads
        FROM Leads 
        GROUP BY State
        ORDER BY State ASC";

            return await conn.QueryAsync<StateStat>(sql);
        }

        public async Task<CustomerSummaryMetrics> GetSummaryMetricsAsync(int customerId)
        {
            using var conn = _db.CreateConnection();

            // We use a single query with subqueries to get all header data efficiently
            string sql = @"
        SELECT 
            COALESCE(
        (SELECT DATE_FORMAT(MIN(OrderDate), '%d-%m-%Y') FROM Orders WHERE LeadId = l.LeadId),
        'No Orders Yet'
    ) as CustomerSince,
            
            (SELECT IFNULL(SUM(TotalAmount), 0) FROM Orders 
             WHERE LeadId = l.LeadId AND OrderDate >= DATE_SUB(CURDATE(), INTERVAL 3 MONTH)) as Last3MonthsBilling,
             
            (SELECT DATE_FORMAT(MAX(OrderDate), '%d-%M-%Y') FROM Orders
             WHERE LeadId = l.LeadId) as LastOrderDate,
             
            -- Outstanding = Total Order Value - Total Payments Received
            ((SELECT IFNULL(SUM(TotalAmount), 0) FROM Orders WHERE LeadId = l.LeadId) - 
             (SELECT IFNULL(SUM(AmountReceived), 0) FROM Payments WHERE LeadId = l.LeadId)) as OutstandingAmount,
             
            -- Monthly Business (Current Month)
            (SELECT IFNULL(SUM(TotalAmount), 0) FROM Orders 
             WHERE LeadId = l.LeadId AND MONTH(OrderDate) = MONTH(CURDATE()) AND YEAR(OrderDate) = YEAR(CURDATE())) as MonthlyBusiness,
             
            -- Overall Business
            (SELECT IFNULL(SUM(TotalAmount), 0) FROM Orders WHERE LeadId = l.LeadId) as OverallBusiness
            
        FROM Leads l
        WHERE l.LeadId = @Id";

            var metrics = await conn.QueryFirstOrDefaultAsync<CustomerSummaryMetrics>(sql, new { Id = customerId });

            // Calculate Last 3 Months Business (Shared property with billing for this view)
            if (metrics != null)
            {
                metrics.Last3MonthsBusiness = metrics.Last3MonthsBilling;
            }

            return metrics ?? new CustomerSummaryMetrics();
        }

        public async Task<IEnumerable<Product>> GetOrderedProductsAsync(int customerId)
        {
            using var conn = _db.CreateConnection();

            // This query finds unique products ordered by the customer and pulls the latest details
            string sql = @"
        SELECT 
            COUNT(oh.OrderId) as TotalOrders,
            DATE_FORMAT(MAX(oh.OrderDate), '%d-%b-%Y') as LastOrderDate,
            oh.ProductName,
            -- Subquery to get current stock from your Inventory/Products table
            (SELECT StockQuantity FROM Products WHERE Name = oh.ProductName) as Stock,
            -- Get the rate from the most recent order
            (SELECT Rate FROM OrderHistory 
             WHERE CustomerId = @Id AND ProductName = oh.ProductName 
             ORDER BY OrderDate DESC LIMIT 1) as LastRate,
            SUM(oh.Quantity) as TotalQuantity
        FROM OrderHistory oh
        WHERE oh.CustomerId = @Id
        GROUP BY oh.ProductName
        ORDER BY MAX(oh.OrderDate) DESC";

            return await conn.QueryAsync<Product>(sql, new { Id = customerId });
        }
    }
}
