using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tijori.Data;
using Tijori.Models;

namespace Tijori.Services
{
    public class StockLedgerService
    {
        private readonly CrmDbContext _context;

        public StockLedgerService(CrmDbContext context) => _context = context;

        public async Task<IEnumerable<StockLedger>> GetLedgerHistoryByProductAsync(int productId)
        {
            using var conn = _context.CreateConnection();
            const string sql = @"
                SELECT sl.*, p.Name AS ProductName, p.ShortName AS ProductCode
                FROM stock_ledgers sl
                INNER JOIN products p ON sl.ProductId = p.ProductId
                WHERE sl.ProductId = @ProductId
                ORDER BY sl.LedgerId DESC;";
            return await conn.QueryAsync<StockLedger>(sql, new { ProductId = productId });
        }

        public async Task<IEnumerable<StockLedger>> GetAllStockMovementsAsync(string? movementType = null)
        {
            using var conn = _context.CreateConnection();
            const string sql = @"
                SELECT sl.*, p.Name AS ProductName, p.ShortName AS ProductCode
                FROM stock_ledgers sl
                INNER JOIN products p ON sl.ProductId = p.ProductId
                WHERE (@MovementType IS NULL OR @MovementType = 'All' OR sl.MovementType = @MovementType)
                ORDER BY sl.LedgerId DESC;";
            return await conn.QueryAsync<StockLedger>(sql, new { MovementType = movementType });
        }

        public async Task<int> RecordStockMovementAsync(StockLedger entry, IDbConnection conn, IDbTransaction transaction)
        {
            const string insertSql = @"
        INSERT INTO stock_ledgers (
            ProductId, BatchNumber, MovementType, Quantity, Unit, ReferenceDocument, Notes, CreatedDate
        ) VALUES (
            @ProductId, @BatchNumber, @MovementType, @Quantity, @Unit, @ReferenceDocument, @Notes, NOW()
        );
        SELECT LAST_INSERT_ID();";

            // 🌟 Updates your existing 'RemainingStock' column
            const string updateProductStockSql = @"
        UPDATE products 
        SET RemainingStock = RemainingStock + @Quantity 
        WHERE ProductId = @ProductId;";

            await conn.ExecuteAsync(updateProductStockSql, new { Quantity = entry.Quantity, ProductId = entry.ProductId }, transaction);
            return await conn.ExecuteScalarAsync<int>(insertSql, entry, transaction);
        }
    }
}
