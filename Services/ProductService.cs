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
    public class ProductService
    {
        private readonly CrmDbContext _context;
        public ProductService(CrmDbContext context) => _context = context;

        public async Task<IEnumerable<Product>> GetAllProductsAsync()
        {
            using var db = _context.CreateConnection();
            string sql = @"
            SELECT p.*, c.CategoryName 
            FROM Products p
            LEFT JOIN Categories c ON p.CategoryId = c.Id
            ORDER BY p.Name ASC";
            return await db.QueryAsync<Product>(sql);
        }

        public async Task<bool> UpsertProductAsync(Product product)
        {
            using var db = _context.CreateConnection();
            string sql;

            if (product.ProductId == 0)
            {
                sql = @"INSERT INTO Products (Name, ShortName, SKU, Unit, CategoryId, Manufacturer, Packaging, InitialStock, RemainingStock, MRP, CostPrice, SellingPrice, GSTPercent, TotalCost, TrackCost) 
                    VALUES (@Name, @ShortName, @SKU, @Unit, @CategoryId, @Manufacturer, @Packaging, @InitialStock, @RemainingStock, @MRP, @CostPrice, @SellingPrice, @GSTPercent, @TotalCost, @TrackCost)";
            }
            else
            {
                sql = @"UPDATE Products SET Name=@Name, ShortName=@ShortName, SKU=@SKU, CategoryId=@CategoryId, Manufacturer=@Manufacturer, 
                    MRP=@MRP, InitialStock=@InitialStock, CostPrice=@CostPrice, SellingPrice=@SellingPrice, GSTPercent=@GSTPercent, TotalCost=@TotalCost, TrackCost=@TrackCost 
                    WHERE ProductId=@ProductId";
            }
            return await db.ExecuteAsync(sql, product) > 0;
        }

        public async Task<bool> DeleteProductAsync(int id)
        {
            using var db = _context.CreateConnection();
            return await db.ExecuteAsync("DELETE FROM Products WHERE ProductId = @id", new { id }) > 0;
        }
    }
}
