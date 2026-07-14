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
    public class VendorService
    {
        private readonly CrmDbContext _context;
        public VendorService(CrmDbContext context) => _context = context;

        public async Task<IEnumerable<Vendor>> GetAllVendorsAsync()
        {
            try
            {
                using var db = _context.CreateConnection();

                // Await the query result completely within the active connection lifetime scope
                var result = await db.QueryAsync<Vendor>("SELECT * FROM Vendors;");

                // Convert to a concrete list right here before the 'db' variable is disposed
                return result.ToList();
            }
            catch (Exception ex)
            {
                // Log the exception parameters to your debug console channel
                System.Diagnostics.Debug.WriteLine($"[VENDOR SERVICE ERROR] GetAllVendorsAsync failed: {ex.Message}");

                // Return an empty list instance fallback so that your UI data-binding collections don't break
                return Enumerable.Empty<Vendor>();
            }
        }

        public async Task<int> SaveVendorAsync(Vendor vendor)
        {
            using var db = _context.CreateConnection();
            const string sql = @"
                INSERT INTO Vendors (CompanyName, ContactPerson, Phone, Email, GstNumber, Address, Status)
                VALUES (@CompanyName, @ContactPerson, @Phone, @Email, @GstNumber, @Address, @Status);
                SELECT LAST_INSERT_ID();";
            return await db.ExecuteScalarAsync<int>(sql, vendor);
        }

        public async Task LinkProductAsync(int vendorId, int productId, string sku, decimal price)
        {
            using var db = _context.CreateConnection();
            const string sql = @"
                INSERT INTO VendorProductLinks (VendorId, ProductId, SupplierSku, PurchasePrice)
                VALUES (@VendorId, @ProductId, @sku, @price)
                ON DUPLICATE KEY UPDATE SupplierSku = @sku, PurchasePrice = @price;";
            await db.ExecuteAsync(sql, new { vendorId, productId, sku, price });
        }
    }
}
