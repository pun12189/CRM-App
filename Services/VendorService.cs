using Tijori.Data;
using Tijori.Models;
using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Services
{
    public class VendorService
    {
        private readonly CrmDbContext _context;

        public VendorService(CrmDbContext context) => _context = context;

        // READ ALL VENDORS
        public async Task<IEnumerable<Vendor>> GetAllVendorsAsync()
        {
            try
            {
                using var db = _context.CreateConnection();
                var result = await db.QueryAsync<Vendor>("SELECT * FROM Vendors ORDER BY VendorId DESC;");
                return result.ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VENDOR SERVICE ERROR] GetAllVendorsAsync failed: {ex.Message}");
                return Enumerable.Empty<Vendor>();
            }
        }

        // READ BY ID
        public async Task<Vendor?> GetVendorByIdAsync(int vendorId)
        {
            try
            {
                using var db = _context.CreateConnection();
                const string sql = "SELECT * FROM Vendors WHERE VendorId = @VendorId;";
                return await db.QueryFirstOrDefaultAsync<Vendor>(sql, new { VendorId = vendorId });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VENDOR SERVICE ERROR] GetVendorByIdAsync failed: {ex.Message}");
                return null;
            }
        }

        // CREATE VENDOR
        public async Task<int> SaveVendorAsync(Vendor vendor)
        {
            try
            {
                using var db = _context.CreateConnection();
                const string sql = @"
                    INSERT INTO Vendors (CompanyName, ContactPerson, Phone, Email, GstNumber, Address, Status, CreatedAt)
                    VALUES (@CompanyName, @ContactPerson, @Phone, @Email, @GstNumber, @Address, @Status, NOW());
                    SELECT LAST_INSERT_ID();";

                return await db.ExecuteScalarAsync<int>(sql, vendor);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VENDOR SERVICE ERROR] SaveVendorAsync failed: {ex.Message}");
                return 0;
            }
        }

        // UPDATE VENDOR
        public async Task<bool> UpdateVendorAsync(Vendor vendor)
        {
            try
            {
                using var db = _context.CreateConnection();
                const string sql = @"
                    UPDATE Vendors 
                    SET CompanyName = @CompanyName,
                        ContactPerson = @ContactPerson,
                        Phone = @Phone,
                        Email = @Email,
                        GstNumber = @GstNumber,
                        Address = @Address,
                        Status = @Status
                    WHERE VendorId = @VendorId;";

                int rowsAffected = await db.ExecuteAsync(sql, vendor);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VENDOR SERVICE ERROR] UpdateVendorAsync failed: {ex.Message}");
                return false;
            }
        }

        // DELETE VENDOR
        public async Task<bool> DeleteVendorAsync(int vendorId)
        {
            try
            {
                using var db = _context.CreateConnection();
                const string sql = "DELETE FROM Vendors WHERE VendorId = @VendorId;";

                int rowsAffected = await db.ExecuteAsync(sql, new { VendorId = vendorId });
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VENDOR SERVICE ERROR] DeleteVendorAsync failed: {ex.Message}");
                return false;
            }
        }

        // FETCH ALL PRODUCTS LINKED TO A SPECIFIC VENDOR
        public async Task<IEnumerable<VendorProductLinkDisplay>> GetProductsByVendorIdAsync(int vendorId)
        {
            try
            {
                using var db = _context.CreateConnection();
                const string sql = @"
            SELECT 
                vpl.VendorId,
                vpl.ProductId,
                p.Name AS ProductName,
                COALESCE(c.CategoryName, 'General') AS CategoryName,
                COALESCE(vpl.SupplierSku, p.ShortName) AS SupplierSku,
                vpl.PurchasePrice,
                p.RemainingStock AS CurrentStock,
                COALESCE(vpl.IsPreferredVendor, 1) AS IsPreferredVendor,
                COALESCE(vpl.VendorPriority, 1) AS VendorPriority,
                COALESCE(vpl.LeadTimeDays, 3) AS LeadTimeDays
            FROM VendorProductLinks vpl
            INNER JOIN Products p ON vpl.ProductId = p.ProductId
            LEFT JOIN Categories c ON p.CategoryId = c.Id
            WHERE vpl.VendorId = @vendorId
            ORDER BY vpl.IsPreferredVendor DESC, vpl.VendorPriority ASC, p.Name ASC;";

                var result = await db.QueryAsync<VendorProductLinkDisplay>(sql, new { vendorId });
                return result.ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VENDOR SERVICE ERROR] GetProductsByVendorIdAsync failed: {ex.Message}");
                return Enumerable.Empty<VendorProductLinkDisplay>();
            }
        }

        // LINK OR UPDATE PRODUCT FOR A VENDOR
        public async Task<bool> SaveVendorProductLinkAsync(
    int vendorId,
    int productId,
    string supplierSku,
    decimal purchasePrice,
    int leadTimeDays,
    int vendorPriority,
    bool isPreferredVendor)
        {
            try
            {
                using var db = _context.CreateConnection();

                // If this vendor is marked as preferred for this product,
                // clear the preferred flag for other vendors supplying this item
                if (isPreferredVendor)
                {
                    const string demoteSql = @"
                UPDATE VendorProductLinks 
                SET IsPreferredVendor = 0 
                WHERE ProductId = @productId AND VendorId != @vendorId;";
                    await db.ExecuteAsync(demoteSql, new { productId, vendorId });
                }

                const string sql = @"
            INSERT INTO VendorProductLinks (
                VendorId, ProductId, SupplierSku, PurchasePrice, 
                LeadTimeDays, VendorPriority, IsPreferredVendor
            ) VALUES (
                @vendorId, @productId, @supplierSku, @purchasePrice,
                @leadTimeDays, @vendorPriority, @isPreferredVendor
            )
            ON DUPLICATE KEY UPDATE 
                SupplierSku = @supplierSku, 
                PurchasePrice = @purchasePrice,
                LeadTimeDays = @leadTimeDays,
                VendorPriority = @vendorPriority,
                IsPreferredVendor = @isPreferredVendor;";

                int rows = await db.ExecuteAsync(sql, new
                {
                    vendorId,
                    productId,
                    supplierSku,
                    purchasePrice,
                    leadTimeDays,
                    vendorPriority,
                    isPreferredVendor = isPreferredVendor ? 1 : 0
                });

                // Sync SKU back to Products table if empty
                if (!string.IsNullOrWhiteSpace(supplierSku))
                {
                    await db.ExecuteAsync(@"
                UPDATE Products 
                SET ShortName = @supplierSku 
                WHERE ProductId = @productId AND (ShortName IS NULL OR ShortName = '');",
                        new { supplierSku, productId });
                }

                return rows > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VENDOR SERVICE ERROR] SaveVendorProductLinkAsync failed: {ex.Message}");
                return false;
            }
        }
    }
}
