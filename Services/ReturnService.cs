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
    public class ReturnService
    {
        private readonly CrmDbContext _context;
        public ReturnService(CrmDbContext context) => _context = context;

        // In ReturnService.cs

        // 1. FETCH PURCHASE RETURNS (DEBIT NOTES) BY PO ID
        public async Task<IEnumerable<PurchaseReturn>> GetPurchaseReturnsByPoIdAsync(int poId)
        {
            using var db = _context.CreateConnection();
            const string sql = @"
        SELECT pr.*, v.CompanyName AS VendorName, po.PoNumber
        FROM PurchaseReturns pr
        LEFT JOIN Vendors v ON pr.VendorId = v.VendorId
        LEFT JOIN PurchaseOrders po ON pr.PurchaseOrderId = po.PurchaseOrderId
        WHERE pr.PurchaseOrderId = @poId
        ORDER BY pr.PurchaseReturnId DESC;";

            var returns = (await db.QueryAsync<PurchaseReturn>(sql, new { poId })).ToList();
            return returns;
        }

        // 2. FETCH SALES RETURNS (CREDIT NOTES) BY ORDER ID
        public async Task<IEnumerable<SalesReturn>> GetSalesReturnsByOrderIdAsync(int orderId)
        {
            using var db = _context.CreateConnection();
            const string sql = @"
        SELECT sr.*, 
               COALESCE(l.CustomerName, l.CompanyName, 'Direct Customer') AS CustomerName,
               o.OrderId
        FROM SalesReturns sr
        LEFT JOIN Leads l ON sr.CustomerId = l.LeadId
        LEFT JOIN Orders o ON sr.OrderId = o.OrderId
        WHERE sr.OrderId = @orderId
        ORDER BY sr.SalesReturnId DESC;";

            var returns = (await db.QueryAsync<SalesReturn>(sql, new { orderId })).ToList();
            return returns;
        }

        // 1. CREATE PURCHASE RETURN (DEBIT NOTE)
        public async Task<int> CreatePurchaseReturnAsync(PurchaseReturn prHeader, List<PurchaseReturnDetail> lines)
        {
            using var db = _context.CreateConnection();
            if (db.State == ConnectionState.Closed) db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                const string insertHeaderSql = @"
                    INSERT INTO PurchaseReturns (
                        ReturnDebitNo, VendorId, PurchaseOrderId, ReturnDate, TotalAmount, TaxAmount, Reason, Status, CreatedBy, CreatedAt
                    ) VALUES (
                        @ReturnDebitNo, @VendorId, @PurchaseOrderId, @ReturnDate, @TotalAmount, @TaxAmount, @Reason, @Status, @CreatedBy, NOW()
                    );
                    SELECT LAST_INSERT_ID();";

                int returnId = await db.ExecuteScalarAsync<int>(insertHeaderSql, prHeader, tx);

                const string insertLineSql = @"
                    INSERT INTO PurchaseReturnDetails (
                        PurchaseReturnId, ProductId, BatchNumber, Quantity, UnitPrice, TaxPercent, TaxAmount, TotalAmount
                    ) VALUES (
                        @PurchaseReturnId, @ProductId, @BatchNumber, @Quantity, @UnitPrice, @TaxPercent, @TaxAmount, @TotalAmount
                    );";

                const string deductProductStockSql = @"
                    UPDATE Products 
                    SET RemainingStock = GREATEST(0, RemainingStock - @Quantity)
                    WHERE ProductId = @ProductId;";

                const string deductBatchStockSql = @"
                    UPDATE ProductBatches 
                    SET CurrentStock = GREATEST(0, CurrentStock - @Quantity)
                    WHERE ProductId = @ProductId AND BatchNumber = @BatchNumber;";

                foreach (var line in lines.Where(l => l.Quantity > 0))
                {
                    line.PurchaseReturnId = returnId;
                    await db.ExecuteAsync(insertLineSql, line, tx);
                    await db.ExecuteAsync(deductProductStockSql, new { line.Quantity, line.ProductId }, tx);

                    if (!string.IsNullOrWhiteSpace(line.BatchNumber))
                    {
                        await db.ExecuteAsync(deductBatchStockSql, new { line.Quantity, line.ProductId, line.BatchNumber }, tx);
                    }
                }

                tx.Commit();
                return returnId;
            }
            catch (Exception ex)
            {
                tx.Rollback();
                System.Diagnostics.Debug.WriteLine($"[PURCHASE RETURN ERROR] {ex.Message}");
                throw;
            }
        }

        // 2. CREATE SALES RETURN (CREDIT NOTE)
        public async Task<int> CreateSalesReturnAsync(SalesReturn srHeader, List<SalesReturnDetail> lines)
        {
            using var db = _context.CreateConnection();
            if (db.State == ConnectionState.Closed) db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                const string insertHeaderSql = @"
                    INSERT INTO SalesReturns (
                        CreditNoteNo, CustomerId, OrderId, ReturnDate, TotalAmount, TaxAmount, Reason, Status, CreatedBy, CreatedAt
                    ) VALUES (
                        @CreditNoteNo, @CustomerId, @OrderId, @ReturnDate, @TotalAmount, @TaxAmount, @Reason, @Status, @CreatedBy, NOW()
                    );
                    SELECT LAST_INSERT_ID();";

                int returnId = await db.ExecuteScalarAsync<int>(insertHeaderSql, srHeader, tx);

                const string insertLineSql = @"
                    INSERT INTO SalesReturnDetails (
                        SalesReturnId, ProductId, BatchNumber, Quantity, UnitPrice, TaxPercent, TaxAmount, TotalAmount
                    ) VALUES (
                        @SalesReturnId, @ProductId, @BatchNumber, @Quantity, @UnitPrice, @TaxPercent, @TaxAmount, @TotalAmount
                    );";

                const string addProductStockSql = @"
                    UPDATE Products 
                    SET RemainingStock = RemainingStock + @Quantity
                    WHERE ProductId = @ProductId;";

                const string addBatchStockSql = @"
                    UPDATE ProductBatches 
                    SET CurrentStock = CurrentStock + @Quantity
                    WHERE ProductId = @ProductId AND BatchNumber = @BatchNumber;";

                foreach (var line in lines.Where(l => l.Quantity > 0))
                {
                    line.SalesReturnId = returnId;
                    await db.ExecuteAsync(insertLineSql, line, tx);
                    await db.ExecuteAsync(addProductStockSql, new { line.Quantity, line.ProductId }, tx);

                    if (!string.IsNullOrWhiteSpace(line.BatchNumber))
                    {
                        await db.ExecuteAsync(addBatchStockSql, new { line.Quantity, line.ProductId, line.BatchNumber }, tx);
                    }
                }

                tx.Commit();
                return returnId;
            }
            catch (Exception ex)
            {
                tx.Rollback();
                System.Diagnostics.Debug.WriteLine($"[SALES RETURN ERROR] {ex.Message}");
                throw;
            }
        }
    }
}
