using Tijori.Data;
using Tijori.Models;
using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Services
{
    public class PurchaseService
    {
        private readonly CrmDbContext _context;
        public PurchaseService(CrmDbContext context) => _context = context;

        // FETCH ALL ORDERS
        public async Task<IEnumerable<PurchaseOrder>> GetAllOrdersAsync()
        {
            try
            {
                using var db = _context.CreateConnection();
                const string sql = @"
                    SELECT po.*, v.CompanyName AS VendorName 
                    FROM PurchaseOrders po
                    INNER JOIN Vendors v ON po.VendorId = v.VendorId
                    ORDER BY po.PurchaseOrderId DESC;";
                return await db.QueryAsync<PurchaseOrder>(sql);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PURCHASE SERVICE ERROR] GetAllOrdersAsync failed: {ex.Message}");
                return Enumerable.Empty<PurchaseOrder>();
            }
        }

        // FETCH ORDERS BY VENDOR ID (Used by Vendor Profile View & Rating Engine)
        public async Task<IEnumerable<PurchaseOrder>> GetOrdersByVendorIdAsync(int vendorId)
        {
            try
            {
                using var db = _context.CreateConnection();
                const string sql = @"
                    SELECT po.*, v.CompanyName AS VendorName 
                    FROM PurchaseOrders po
                    INNER JOIN Vendors v ON po.VendorId = v.VendorId
                    WHERE po.VendorId = @vendorId
                    ORDER BY po.PurchaseOrderId DESC;";

                var result = await db.QueryAsync<PurchaseOrder>(sql, new { vendorId });
                return result.ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PURCHASE SERVICE ERROR] GetOrdersByVendorIdAsync failed: {ex.Message}");
                return Enumerable.Empty<PurchaseOrder>();
            }
        }

        // CREATE PURCHASE ORDER (Saves ExpectedDeliveryDate)
        public async Task<int> CreatePurchaseOrderAsync(PurchaseOrder poHeader, List<PurchaseOrderDetail> poLines)
        {
            using var db = _context.CreateConnection();
            if (db.State == ConnectionState.Closed) db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                // 1. Insert Master Header Record with ExpectedDeliveryDate
                const string insertHeaderSql = @"
                    INSERT INTO PurchaseOrders (
                        PoNumber, VendorId, OrderDate, ExpectedDeliveryDate, TotalAmount, OrderStatus, CreatedBy
                    ) VALUES (
                        @PoNumber, @VendorId, @OrderDate, @ExpectedDeliveryDate, @TotalAmount, @OrderStatus, @CreatedBy
                    );
                    SELECT LAST_INSERT_ID();";

                int generatedPoId = await db.ExecuteScalarAsync<int>(insertHeaderSql, poHeader, tx);

                // 2. Insert Detail Lines
                const string insertLineSql = @"
                    INSERT INTO PurchaseOrderDetails (PurchaseOrderId, ProductId, Quantity, UnitPrice)
                    VALUES (@PurchaseOrderId, @ProductId, @Quantity, @UnitPrice);";

                foreach (var line in poLines)
                {
                    line.PurchaseOrderId = generatedPoId;
                    await db.ExecuteAsync(insertLineSql, line, tx);
                }

                tx.Commit();
                return generatedPoId;
            }
            catch (Exception ex)
            {
                tx.Rollback();
                System.Diagnostics.Debug.WriteLine($"[PURCHASE SERVICE ERROR] PO Creation Failed: {ex.Message}");
                throw;
            }
        }

        // PROCESS STOCK RECEIPT (Sets ActualDeliveryDate = NOW() dynamically)
        public async Task ProcessStockReceiptAsync(int poId)
        {
            using var db = _context.CreateConnection();
            if (db.State == ConnectionState.Closed) db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                // 1. Fetch line details
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

                    if (isNewAdHoc == 1) continue;

                    const string checkBatchSql = @"
                        SELECT COUNT(1) FROM ProductBatches 
                        WHERE ProductId = @ProductId AND BatchNumber LIKE @BatchPattern;";

                    string batchPattern = $"BAT-PO{poId}-%";
                    long batchExists = await db.ExecuteScalarAsync<long>(checkBatchSql, new { ProductId = line.ProductId, BatchPattern = batchPattern }, tx);

                    if (batchExists > 0) continue;

                    string uniqueBatchNum = $"BAT-PO{poId}-{DateTime.Today:yyyyMM}-{Guid.NewGuid().ToString()[..4].ToUpper()}";

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

                    const string updateProductStockSql = @"
                        UPDATE Products 
                        SET RemainingStock = RemainingStock + @Quantity 
                        WHERE ProductId = @ProductId;";

                    await db.ExecuteAsync(updateProductStockSql, new { line.Quantity, line.ProductId }, tx);
                }

                // 2. Finalize Status AND record ActualDeliveryDate as NOW()
                const string updatePoStatusSql = @"
                    UPDATE PurchaseOrders 
                    SET OrderStatus = 'Received', 
                        ActualDeliveryDate = NOW() 
                    WHERE PurchaseOrderId = @poId;";

                await db.ExecuteAsync(updatePoStatusSql, new { poId }, tx);

                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.Rollback();
                System.Diagnostics.Debug.WriteLine($"[PO RECEIVE ERROR] Transaction aborted safely: {ex.Message}");
                throw;
            }
        }

        public async Task<(PurchaseOrder? Order, Vendor? VendorDetails)> GetPurchaseOrderWithVendorAsync(int purchaseOrderId)
        {
            try
            {
                using var db = _context.CreateConnection();

                const string poSql = @"
                    SELECT 
                        po.PurchaseOrderId, po.PoNumber, po.VendorId, po.OrderDate, 
                        po.ExpectedDeliveryDate, po.ActualDeliveryDate, po.TotalAmount, 
                        po.OrderStatus, po.CreatedBy,
                        v.CompanyName AS VendorName
                    FROM PurchaseOrders po
                    LEFT JOIN Vendors v ON po.VendorId = v.VendorId
                    WHERE po.PurchaseOrderId = @purchaseOrderId;";

                var po = await db.QueryFirstOrDefaultAsync<PurchaseOrder>(poSql, new { purchaseOrderId });

                Vendor? vendor = null;
                if (po != null && po.VendorId > 0)
                {
                    const string vendorSql = @"
                        SELECT VendorId, CompanyName, ContactPerson, Phone, Email, GstNumber, Address, Status, CreatedAt
                        FROM Vendors
                        WHERE VendorId = @vendorId;";

                    vendor = await db.QueryFirstOrDefaultAsync<Vendor>(vendorSql, new { vendorId = po.VendorId });
                }

                return (po, vendor);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PO SERVICE ERROR] GetPurchaseOrderWithVendorAsync: {ex.Message}");
                return (null, null);
            }
        }

        // 2. FETCH LINE ITEMS FOR THE PURCHASE ORDER
        public async Task<IEnumerable<PurchaseOrderDetail>> GetPurchaseOrderDetailsAsync(int purchaseOrderId)
        {
            try
            {
                using var db = _context.CreateConnection();
                const string sql = @"
                    SELECT 
                        pod.PoDetailId, pod.PurchaseOrderId, pod.ProductId, 
                        pod.Quantity, pod.UnitPrice, p.ShortName as SupplierSku,
                        COALESCE(p.Name, CONCAT('Product #', pod.ProductId)) AS ProductName
                    FROM PurchaseOrderDetails pod
                    LEFT JOIN Products p ON pod.ProductId = p.ProductId
                    WHERE pod.PurchaseOrderId = @purchaseOrderId;";

                var result = await db.QueryAsync<PurchaseOrderDetail>(sql, new { purchaseOrderId });
                return result.ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PO SERVICE ERROR] GetPurchaseOrderDetailsAsync: {ex.Message}");
                return Enumerable.Empty<PurchaseOrderDetail>();
            }
        }

        public async Task<IEnumerable<StockInwardDto>> GetStockInwardFilteredAsync(int productId, int? vendorId, string? location)
        {
            const string sql = @"
        SELECT 
            po.PoNumber AS BillNo,
            po.OrderDate AS TransactionDate,
            po.VendorId,
            COALESCE(v.CompanyName, 'Cash Vendor') AS VendorName,
            COALESCE(v.Address, '') AS VendorCity,
            pod.ProductId,
            COALESCE(pb.BatchNumber, 'N/A') AS BatchNumber,
            pod.Quantity,
            pod.UnitPrice
        FROM PurchaseOrderDetails pod
        INNER JOIN PurchaseOrders po ON pod.PurchaseOrderId = po.PurchaseOrderId
        LEFT JOIN Vendors v ON po.VendorId = v.VendorId
        LEFT JOIN ProductBatches pb 
               ON pb.ProductId = pod.ProductId 
              AND pb.BatchNumber LIKE CONCAT('BAT-PO', po.PurchaseOrderId, '-%')
        WHERE pod.ProductId = @ProductId
          -- OPTIONAL FILTERS: If parameter is NULL, condition is bypassed
          AND (@VendorId IS NULL OR po.VendorId = @VendorId)
          AND (@Location IS NULL OR @Location = '' OR LOWER(v.Address) LIKE LOWER(CONCAT('%', @Location, '%')))
        ORDER BY po.OrderDate ASC;";

            using var db = _context.CreateConnection();
            if (db.State == ConnectionState.Closed) db.Open();

            return await db.QueryAsync<StockInwardDto>(sql, new
            {
                ProductId = productId,
                VendorId = vendorId,
                Location = location
            });
        }
    }
}
