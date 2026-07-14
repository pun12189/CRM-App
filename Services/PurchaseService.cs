using CallMan.Data;
using CallMan.Models;
using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Services
{
    public class PurchaseService
    {
        private readonly CrmDbContext _context;
        public PurchaseService(CrmDbContext context) => _context = context;

        public async Task<IEnumerable<PurchaseOrder>> GetAllOrdersAsync()
        {
            using var db = _context.CreateConnection();
            const string sql = @"
                SELECT po.*, v.CompanyName AS VendorName 
                FROM PurchaseOrders po
                INNER JOIN Vendors v ON po.VendorId = v.VendorId;";
            return await db.QueryAsync<PurchaseOrder>(sql);
        }

        public async Task ProcessStockReceiptAsync(int poId)
        {
            using var db = _context.CreateConnection();
            if (db.State == ConnectionState.Closed) db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                // 1. Fetch complete line details along with a flag indicating if this product's initial stock was already set by this PO
                const string fetchLinesSql = @"
            SELECT pod.ProductId, pod.Quantity, pod.UnitPrice,
                   (p.InitialStock = pod.Quantity AND DATE(p.CreatedAt) = DATE(po.OrderDate)) AS IsNewAdHocProduct
            FROM PurchaseOrderDetails pod
            INNER JOIN PurchaseOrders po ON pod.PurchaseOrderId = po.PurchaseOrderId
            INNER JOIN Products p ON pod.ProductId = p.ProductId
            WHERE pod.PurchaseOrderId = @poId;";

                var lines = (await db.QueryAsync<dynamic>(fetchLinesSql, new { poId }, tx)).ToList();

                foreach (var line in lines)
                {
                    long isNewAdHoc = (long)line.IsNewAdHocProduct;

                    // ====================================================================
                    // CRITICAL FIX FOR AD-HOC CUSTOM ITEMS
                    // ====================================================================
                    if (isNewAdHoc == 1)
                    {
                        // This is a custom ad-hoc item. It already has its initial stock set 
                        // and its initial batch row created by SaveProductAssemblyAsync.
                        // We SKIP batch insertion and SKIP parent stock updates completely!
                        continue;
                    }

                    // Check if a batch record already exists for this PO and Product to avoid double-processing
                    const string checkBatchSql = @"
                SELECT COUNT(1) FROM ProductBatches 
                WHERE ProductId = @ProductId AND BatchNumber LIKE @BatchPattern;";

                    string batchPattern = $"BAT-PO{poId}-%";
                    long batchExists = await db.ExecuteScalarAsync<long>(checkBatchSql, new { ProductId = line.ProductId, BatchPattern = batchPattern }, tx);

                    if (batchExists > 0)
                    {
                        // This batch was already created during the ad-hoc item initialization phase. Skip to avoid double entry!
                        continue;
                    }

                    // 2. Generate a unique traceable Batch Number for standard products arriving now
                    string uniqueBatchNum = $"BAT-PO{poId}-{DateTime.Today:yyyyMM}-{Guid.NewGuid().ToString()[..4].ToUpper()}";

                    // 3. INSERT the child record into ProductBatches tracking this incoming lot
                    const string insertBatchSql = @"
                INSERT INTO ProductBatches (
                    ProductId, DivisionId, BatchNumber, MfgDate, ExpiryDate, 
                    QuantityReceived, CurrentStock, MinimumSellingPrice, CreatedAt
                ) VALUES (
                    @ProductId, @DivisionId, @BatchNumber, NOW(), DATE_ADD(NOW(), INTERVAL 2 YEAR), 
                    @Quantity, @Quantity, (@UnitPrice * 1.15), NOW()
                );";

                    await db.ExecuteAsync(insertBatchSql, new
                    {
                        ProductId = line.ProductId,
                        DivisionId = 1,
                        BatchNumber = uniqueBatchNum,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice
                    }, tx);

                    // 4. CRITICAL FIX: Only update parent stock if it wasn't already initialized during custom item generation!

                    const string updateProductStockSql = @"
                    UPDATE Products 
                    SET RemainingStock = RemainingStock + @Quantity 
                    WHERE ProductId = @ProductId;";

                    await db.ExecuteAsync(updateProductStockSql, new { line.Quantity, line.ProductId }, tx);
                }

                // 5. Finalize the Purchase Order Status
                await db.ExecuteAsync(
                    "UPDATE PurchaseOrders SET OrderStatus = 'Received' WHERE PurchaseOrderId = @poId;",
                    new { poId }, tx);

                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.Rollback();
                System.Diagnostics.Debug.WriteLine($"[PO RECEIVE ERROR] Transaction aborted safely: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Executes an atomic transaction to save a new purchase order header and all its itemized detail lines.
        /// </summary>
        public async Task<int> CreatePurchaseOrderAsync(PurchaseOrder poHeader, List<PurchaseOrderDetail> poLines)
        {
            using var db = _context.CreateConnection();
            if (db.State == ConnectionState.Closed) db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                // 1. Insert the Master Purchase Order Header Record
                const string insertHeaderSql = @"
                    INSERT INTO PurchaseOrders (PoNumber, VendorId, OrderDate, TotalAmount, OrderStatus, CreatedBy)
                    VALUES (@PoNumber, @VendorId, @OrderDate, @TotalAmount, @OrderStatus, @CreatedBy);
                    SELECT LAST_INSERT_ID();";

                int generatedPoId = await db.ExecuteScalarAsync<int>(insertHeaderSql, poHeader, tx);

                // 2. Insert each individual Item Detail Line mapping to the generated Header ID
                const string insertLineSql = @"
                    INSERT INTO PurchaseOrderDetails (PurchaseOrderId, ProductId, Quantity, UnitPrice)
                    VALUES (@PurchaseOrderId, @ProductId, @Quantity, @UnitPrice);";

                foreach (var line in poLines)
                {
                    line.PurchaseOrderId = generatedPoId;
                    await db.ExecuteAsync(insertLineSql, line, tx);
                }

                // Commit the structural adjustments safely if all steps pass execution rules
                tx.Commit();
                return generatedPoId;
            }
            catch (Exception ex)
            {
                // Rollback structural inserts to prevent data mismatch configurations on failure
                tx.Rollback();
                System.Diagnostics.Debug.WriteLine($"[PURCHASE SERVICE ERROR] PO Creation Failed: {ex.Message}");
                throw;
            }
        }
    }
}
