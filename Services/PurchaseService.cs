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
                    FROM purchaseorders po
                    INNER JOIN vendors v ON po.VendorId = v.VendorId
                    ORDER BY po.PurchaseOrderId DESC;";
                return await db.QueryAsync<PurchaseOrder>(sql);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PURCHASE SERVICE ERROR] GetAllOrdersAsync failed: {ex.Message}");
                return Enumerable.Empty<PurchaseOrder>();
            }
        }

        // FETCH ORDERS BY VENDOR ID
        public async Task<IEnumerable<PurchaseOrder>> GetOrdersByVendorIdAsync(int vendorId)
        {
            try
            {
                using var db = _context.CreateConnection();
                const string sql = @"
                    SELECT po.*, v.CompanyName AS VendorName 
                    FROM purchaseorders po
                    INNER JOIN vendors v ON po.VendorId = v.VendorId
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

        // FETCH PURCHASE ORDER HEADER WITH VENDOR
        public async Task<(PurchaseOrder? Order, Vendor? VendorDetails)> GetPurchaseOrderWithVendorAsync(int purchaseOrderId)
        {
            try
            {
                using var db = _context.CreateConnection();

                const string poSql = @"
                    SELECT 
                        po.PurchaseOrderId, po.PoNumber, po.VendorId, po.OrderDate, po.InvoiceDate,
                        po.ExpectedDeliveryDate, po.ActualDeliveryDate, 
                        po.TaxableAmount, po.DiscountAmount, po.TaxAmount, po.RoundOff, po.TotalAmount, 
                        po.OrderStatus, po.CreatedBy,
                        v.CompanyName AS VendorName
                    FROM purchaseorders po
                    LEFT JOIN vendors v ON po.VendorId = v.VendorId
                    WHERE po.PurchaseOrderId = @purchaseOrderId;";

                var po = await db.QueryFirstOrDefaultAsync<PurchaseOrder>(poSql, new { purchaseOrderId });

                Vendor? vendor = null;
                if (po != null && po.VendorId > 0)
                {
                    const string vendorSql = @"
                        SELECT VendorId, CompanyName, ContactPerson, Phone, Email, GstNumber, Address, Status, CreatedAt
                        FROM vendors
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

        // FETCH LINE ITEMS
        public async Task<IEnumerable<PurchaseOrderDetail>> GetPurchaseOrderDetailsAsync(int purchaseOrderId)
        {
            try
            {
                using var db = _context.CreateConnection();
                const string sql = @"
                    SELECT 
                        pod.PoDetailId, pod.PurchaseOrderId, pod.ProductId, pod.BatchNumber,
                        pod.Quantity, pod.FreeQuantity, pod.UnitPrice, pod.MRP,
                        pod.DiscountPercent, pod.TaxPercent, pod.TaxAmount, pod.TotalAmount,
                        p.ShortName AS SupplierSku,
                        COALESCE(p.Name, CONCAT('Product #', pod.ProductId)) AS ProductName
                    FROM purchaseorderdetails pod
                    LEFT JOIN products p ON pod.ProductId = p.ProductId
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

        // CREATE PURCHASE ORDER WITH FULL FINANCIALS
        public async Task<int> CreatePurchaseOrderAsync(PurchaseOrder poHeader, List<PurchaseOrderDetail> poLines)
        {
            using var db = _context.CreateConnection();
            if (db.State == ConnectionState.Closed) db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                const string insertHeaderSql = @"
                    INSERT INTO purchaseorders (
                        PoNumber, VendorId, OrderDate, InvoiceDate, ExpectedDeliveryDate,
                        TaxableAmount, DiscountAmount, TaxAmount, RoundOff, TotalAmount,
                        OrderStatus, CreatedBy
                    ) VALUES (
                        @PoNumber, @VendorId, @OrderDate, @InvoiceDate, @ExpectedDeliveryDate,
                        @TaxableAmount, @DiscountAmount, @TaxAmount, @RoundOff, @TotalAmount,
                        @OrderStatus, @CreatedBy
                    );
                    SELECT LAST_INSERT_ID();";

                int generatedPoId = await db.ExecuteScalarAsync<int>(insertHeaderSql, poHeader, tx);

                const string insertLineSql = @"
                    INSERT INTO purchaseorderdetails (
                        PurchaseOrderId, ProductId, BatchNumber, Quantity, FreeQuantity,
                        UnitPrice, MRP, DiscountPercent, TaxPercent, TaxAmount, TotalAmount
                    ) VALUES (
                        @PurchaseOrderId, @ProductId, @BatchNumber, @Quantity, @FreeQuantity,
                        @UnitPrice, @MRP, @DiscountPercent, @TaxPercent, @TaxAmount, @TotalAmount
                    );";

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

        // PROCESS STOCK RECEIPT / GRN
        public async Task ProcessStockReceiptAsync(int poId)
        {
            using var db = _context.CreateConnection();
            if (db.State == ConnectionState.Closed) db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                const string fetchLinesSql = @"
                    SELECT pod.*, po.OrderDate
                    FROM purchaseorderdetails pod
                    INNER JOIN purchaseorders po ON pod.PurchaseOrderId = po.PurchaseOrderId
                    WHERE pod.PurchaseOrderId = @poId;";

                var lines = (await db.QueryAsync<PurchaseOrderDetail>(fetchLinesSql, new { poId }, tx)).ToList();

                foreach (var line in lines)
                {
                    int totalInwardUnits = line.Quantity + line.FreeQuantity;
                    string batchNum = !string.IsNullOrWhiteSpace(line.BatchNumber)
                        ? line.BatchNumber
                        : $"BAT-PO{poId}-{DateTime.Today:yyyyMM}-{Guid.NewGuid().ToString()[..4].ToUpper()}";

                    // 1. Upsert Batch
                    const string upsertBatchSql = @"
                        INSERT INTO productbatches (
                            ProductId, BatchNumber, MfgDate, ExpiryDate, 
                            QuantityReceived, CurrentStock, MinimumSellingPrice, CreatedAt
                        ) VALUES (
                            @ProductId, @BatchNumber, NOW(), DATE_ADD(NOW(), INTERVAL 2 YEAR), 
                            @TotalUnits, @TotalUnits, @MRP, NOW()
                        )
                        ON DUPLICATE KEY UPDATE 
                            CurrentStock = CurrentStock + @TotalUnits,
                            QuantityReceived = QuantityReceived + @TotalUnits;";

                    await db.ExecuteAsync(upsertBatchSql, new
                    {
                        line.ProductId,
                        BatchNumber = batchNum,
                        TotalUnits = totalInwardUnits,
                        MRP = line.MRP > 0 ? line.MRP : (line.UnitPrice * 1.25m)
                    }, tx);

                    // 2. Update Master Product Stock & Pricing
                    const string updateProductStockSql = @"
                        UPDATE products 
                        SET RemainingStock = RemainingStock + @TotalUnits,
                            CostPrice = CASE WHEN @UnitPrice > 0 THEN @UnitPrice ELSE CostPrice END,
                            MRP = CASE WHEN @MRP > 0 THEN @MRP ELSE MRP END
                        WHERE ProductId = @ProductId;";

                    await db.ExecuteAsync(updateProductStockSql, new
                    {
                        TotalUnits = totalInwardUnits,
                        line.UnitPrice,
                        line.MRP,
                        line.ProductId
                    }, tx);
                }

                // 3. Mark PO Received
                const string updatePoStatusSql = @"
                    UPDATE purchaseorders 
                    SET OrderStatus = 'Received', 
                        ActualDeliveryDate = NOW() 
                    WHERE PurchaseOrderId = @poId;";

                await db.ExecuteAsync(updatePoStatusSql, new { poId }, tx);

                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.Rollback();
                System.Diagnostics.Debug.WriteLine($"[PO RECEIVE ERROR] {ex.Message}");
                throw;
            }
        }

        // FETCH CHARGES
        public async Task<IEnumerable<PurchaseCharge>> GetChargesByPurchaseOrderIdAsync(int purchaseOrderId)
        {
            try
            {
                using var db = _context.CreateConnection();
                const string sql = @"
                    SELECT * 
                    FROM purchase_charges 
                    WHERE PurchaseOrderId = @purchaseOrderId
                    ORDER BY ChargeId ASC;";

                var result = await db.QueryAsync<PurchaseCharge>(sql, new { purchaseOrderId });
                return result.ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PURCHASE SERVICE ERROR] GetChargesByPurchaseOrderIdAsync failed: {ex.Message}");
                return Enumerable.Empty<PurchaseCharge>();
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
