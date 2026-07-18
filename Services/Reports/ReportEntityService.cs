using CallMan.Data;
using CallMan.Models;
using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Services.Reports
{
    public class ReportEntityService
    {
        private readonly CrmDbContext _context;

        public ReportEntityService(CrmDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Dynamically loads selectable items based on the dropdown selection context.
        /// </summary>
        public async Task<IEnumerable<SelectableReportEntity>> GetEntitiesByParameterAsync(string parameterName)
        {
            using var db = _context.CreateConnection();
            if (db.State == ConnectionState.Closed) db.Open();

            // Map UI text inputs cleanly to your production MySQL schemas
            string query = parameterName switch
            {
                "Customer" => "SELECT LeadId AS Id, CustomerName AS DisplayName FROM Leads WHERE Status = 'Matured' ORDER BY CustomerName;",
                "Vendor" => "SELECT VendorId AS Id, CompanyName AS DisplayName FROM Vendors ORDER BY CompanyName;",
                "Items" => "SELECT ProductId AS Id, Name AS DisplayName FROM Products ORDER BY Name;",
                "Staff" => "SELECT UserId AS Id, FullName AS DisplayName FROM Users WHERE IsActive = 1 ORDER BY FullName;",                
                "Divisions" => "SELECT Id, Name AS DisplayName FROM Divisions ORDER BY Name;",
                "Sales" => "SELECT OrderId AS Id, CONCAT(IFNULL(OrderId, 'No Order'), ' (', IFNULL(ProcessedBy, 'Unknown'), ') - ₹', FORMAT(TotalAmount, 2)) AS DisplayName FROM Orders ORDER BY OrderDate DESC;",
                "Purchase" => "SELECT po.PurchaseOrderId AS Id, CONCAT(IFNULL(po.PoNumber, 'No PO'), ' (', IFNULL(v.CompanyName, 'No Vendor'), ') - ₹', FORMAT(po.TotalAmount, 2)) AS DisplayName FROM PurchaseOrders po LEFT JOIN Vendors v ON po.VendorId = v.VendorId ORDER BY po.OrderDate DESC;",
                "Ledger" => "SELECT p.PaymentId AS Id, CONCAT(IFNULL(l.CustomerName, 'Unknown Payer'), ' - ', IFNULL(p.PaymentMethod, 'Direct'), ' (₹', FORMAT(p.AmountReceived, 2), ')') AS DisplayName FROM Payments p LEFT JOIN Leads l ON p.LeadId = l.LeadId ORDER BY p.PaymentDate DESC;",
                // Fallbacks for abstract metrics like Sales, Purchases, or P&L that aggregate as full series
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(query))
                return new List<SelectableReportEntity>();

            return await db.QueryAsync<SelectableReportEntity>(query);
        }
    }
}
