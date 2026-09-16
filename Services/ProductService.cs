using Dapper;
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Tijori.Data;
using Tijori.Models;
using Tijori.Models.Enums;

namespace Tijori.Services
{
    public class ProductService
    {
        private readonly CrmDbContext _context;
        public ProductService(CrmDbContext context) => _context = context;

        public async Task<IEnumerable<Product>> GetAllProductsAsync()
        {
            using var db = _context.CreateConnection();
            string sql = @"
                SELECT p.*, c.CategoryName, c.CategoryType 
                FROM Products p
                LEFT JOIN Categories c ON p.CategoryId = c.Id
                ORDER BY p.Name ASC";
            return await db.QueryAsync<Product>(sql);
        }

        public async Task<IEnumerable<Product>> GetAllProductsAsync(int divisionId)
        {
            using var db = _context.CreateConnection();

            string sql = @"
                SELECT 
                    p.*, 
                    c.CategoryName AS CategoryName,
                    c.CategoryType,
                    IFNULL(b.AggStock, 0) AS RemainingStock,
                    IFNULL(b.BatchCount, 0) AS TotalBatchesCount,
                    CASE 
                        WHEN IFNULL(b.AggStock, 0) > 0 THEN ROUND(b.TotalValue / b.AggStock, 2)
                        ELSE p.CostPrice 
                    END AS CostPrice
                FROM Products p
                LEFT JOIN Categories c ON p.CategoryId = c.Id
                LEFT JOIN (
                    SELECT 
                        ProductId, 
                        SUM(CurrentStock) AS AggStock,
                        COUNT(BatchId) AS BatchCount,
                        SUM(CurrentStock * MinimumSellingPrice) AS TotalValue
                    FROM ProductBatches                    
                    GROUP BY ProductId
                ) b ON p.ProductId = b.ProductId        
                ORDER BY p.Name ASC;"; /*WHERE DivisionId = @DivId*/

            return await db.QueryAsync<Product>(sql, new { DivId = divisionId });
        }

        public async Task<IEnumerable<Product>> GetProductsWithBatchesAsync(int divisionId)
        {
            const string sql = @"
                SELECT * FROM Products;
                SELECT * FROM ProductBatches;"; /*WHERE DivisionId = @DivId -- both queries are executed in a single round-trip to the database*/

            using var db = _context.CreateConnection();
            using var gridReader = await db.QueryMultipleAsync(sql, new { DivId = divisionId });

            var products = (await gridReader.ReadAsync<Product>()).ToList();
            var batches = (await gridReader.ReadAsync<ProductBatch>()).ToList();

            foreach (var product in products)
            {
                var productSpecificLots = batches.Where(b => b.ProductId == product.ProductId);

                product.InnerBatchesCollection.Clear();
                foreach (var batch in productSpecificLots)
                {
                    product.InnerBatchesCollection.Add(batch);
                }

                product.TotalBatchesCount = product.InnerBatchesCollection.Count;
            }

            return products;
        }

        public async Task<IEnumerable<ProductBatch>> GetAllBatchesAsync()
        {
            const string sql = @"
                SELECT 
                    BatchId, ProductId, DivisionId, BatchNumber, 
                    MfgDate, ExpiryDate, QuantityReceived, CurrentStock, 
                    MinimumSellingPrice, CreatedAt
                FROM ProductBatches
                ORDER BY BatchNumber ASC;";

            using var db = _context.CreateConnection();
            if (db.State == ConnectionState.Closed) db.Open();

            return await db.QueryAsync<ProductBatch>(sql);
        }

        public async Task<IEnumerable<ProductBatch>> GetBatchesByProductIdAsync(int productId, int divisionId)
        {
            const string sql = @"
                SELECT 
                    BatchId, ProductId, DivisionId, BatchNumber, 
                    MfgDate, ExpiryDate, QuantityReceived, CurrentStock, 
                    MinimumSellingPrice, CreatedAt
                FROM ProductBatches
                WHERE ProductId = @ProdId 
                ORDER BY ExpiryDate ASC, BatchId DESC;"; /*AND DivisionId = @DivId*/

            try
            {
                using var db = _context.CreateConnection();
                return await db.QueryAsync<ProductBatch>(sql, new { ProdId = productId, DivId = divisionId });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to retrieve inventory batches for Product ID: {productId}", ex);
            }
        }

        public async Task<bool> UpsertProductWithBatchAsync(Product product, ProductBatch batch)
        {
            using var db = _context.CreateConnection();
            if (db.State == ConnectionState.Closed)
                await ((System.Data.Common.DbConnection)db).OpenAsync();

            using var transaction = db.BeginTransaction();
            try
            {
                int? resolvedDivisionId = (product.DivisionId.HasValue && product.DivisionId.Value > 0) ? product.DivisionId.Value : null;

                product.DivisionId = resolvedDivisionId;
                batch.DivisionId = resolvedDivisionId;

                // -----------------------------------------------------------------
                // STEP 1: INSERT OR UPDATE `products` TABLE
                // -----------------------------------------------------------------
                if (product.ProductId == 0)
                {
                    const string insertProductSql = @"
                INSERT INTO products (
                    DivisionId, Name, ShortName, BrandName, SKU, Unit, CategoryId, 
                    Manufacturer, Packaging, InitialStock, RemainingStock, ReorderQuantity, 
                    AutoReorderEnabled, MRP, CostPrice, SellingPrice, GSTPercent, TotalCost, 
                    TrackCost, HasBatchTracking, MfgDate, ExpiryDate, CreatedAt
                ) VALUES (
                    @DivisionId, @Name, @ShortName, @BrandName, @SKU, @Unit, @CategoryId, 
                    @Manufacturer, @Packaging, @InitialStock, @RemainingStock, @ReorderQuantity, 
                    @AutoReorderEnabled, @MRP, @CostPrice, @SellingPrice, @GSTPercent, @TotalCost, 
                    @TrackCost, @HasBatchTracking, @MfgDate, @ExpiryDate, NOW()
                );";

                    await db.ExecuteAsync(insertProductSql, product, transaction);

                    // Fetch the generated ProductId explicitly
                    product.ProductId = await db.ExecuteScalarAsync<int>("SELECT LAST_INSERT_ID();", transaction: transaction);
                }
                else
                {
                    const string updateProductSql = @"
                UPDATE products 
                SET 
                    DivisionId = @DivisionId,
                    Name = @Name,
                    ShortName = @ShortName,
                    BrandName = @BrandName,
                    SKU = @SKU,
                    Unit = @Unit,
                    CategoryId = @CategoryId,
                    Manufacturer = @Manufacturer,
                    Packaging = @Packaging,
                    ReorderQuantity = @ReorderQuantity,
                    AutoReorderEnabled = @AutoReorderEnabled,
                    MRP = @MRP,
                    SellingPrice = @SellingPrice,
                    GSTPercent = @GSTPercent,
                    TotalCost = @TotalCost,
                    TrackCost = @TrackCost,
                    HasBatchTracking = @HasBatchTracking,
                    MfgDate = @MfgDate,
                    ExpiryDate = @ExpiryDate
                WHERE ProductId = @ProductId;";

                    await db.ExecuteAsync(updateProductSql, product, transaction);
                }

                // -----------------------------------------------------------------
                // STEP 2: LINK BATCH TO VALID PRODUCT & DIVISION
                // -----------------------------------------------------------------
                batch.ProductId = product.ProductId; // MUST be set for both Insert and Update
                if (batch.DivisionId == 0)
                {
                    batch.DivisionId = product.DivisionId;
                }

                // Default fallback batch number if none is supplied in the form
                if (string.IsNullOrWhiteSpace(batch.BatchNumber))
                {
                    batch.BatchNumber = "DEFAULT";
                }

                // -----------------------------------------------------------------
                // STEP 3: INSERT OR UPDATE `productbatches` TABLE
                // -----------------------------------------------------------------
                if (batch.BatchId == 0)
                {
                    const string insertBatchSql = @"
                INSERT INTO productbatches (
                    ProductId, DivisionId, BatchNumber, MfgDate, ExpiryDate, 
                    QuantityReceived, CurrentStock, MinimumSellingPrice, CreatedAt
                ) VALUES (
                    @ProductId, @DivisionId, @BatchNumber, @MfgDate, @ExpiryDate, 
                    @QuantityReceived, @CurrentStock, @MinimumSellingPrice, NOW()
                );";

                    await db.ExecuteAsync(insertBatchSql, batch, transaction);
                    batch.BatchId = await db.ExecuteScalarAsync<int>("SELECT LAST_INSERT_ID();", transaction: transaction);
                }
                else
                {
                    const string updateBatchSql = @"
                UPDATE productbatches 
                SET 
                    DivisionId = @DivisionId,
                    BatchNumber = @BatchNumber,
                    MfgDate = @MfgDate,
                    ExpiryDate = @ExpiryDate,
                    QuantityReceived = @QuantityReceived,
                    CurrentStock = @CurrentStock,
                    MinimumSellingPrice = @MinimumSellingPrice
                WHERE BatchId = @BatchId AND ProductId = @ProductId;";

                    await db.ExecuteAsync(updateBatchSql, batch, transaction);
                }

                // -----------------------------------------------------------------
                // STEP 4: RECALCULATE STOCK & WAC IN `products`
                // -----------------------------------------------------------------
                const string syncSql = @"
            UPDATE products p 
            SET 
                p.InitialStock = IFNULL((SELECT SUM(QuantityReceived) FROM productbatches WHERE ProductId = p.ProductId), 0),
                p.RemainingStock = IFNULL((SELECT SUM(CurrentStock) FROM productbatches WHERE ProductId = p.ProductId AND CurrentStock > 0), 0),
                p.CostPrice = IFNULL(
                    (SELECT ROUND(SUM(CurrentStock * MinimumSellingPrice) / NULLIF(SUM(CurrentStock), 0), 2) 
                     FROM productbatches 
                     WHERE ProductId = p.ProductId AND CurrentStock > 0), 
                    @FallbackCost
                )
            WHERE p.ProductId = @ProductId;";

                await db.ExecuteAsync(syncSql, new
                {
                    ProductId = product.ProductId,
                    FallbackCost = batch.MinimumSellingPrice > 0 ? batch.MinimumSellingPrice : product.CostPrice
                }, transaction);

                // -----------------------------------------------------------------
                // STEP 5: SYNC UPDATED VALUES BACK TO MODEL
                // -----------------------------------------------------------------
                const string fetchUpdatedSql = @"
            SELECT RemainingStock, InitialStock, CostPrice 
            FROM products 
            WHERE ProductId = @ProductId LIMIT 1;";

                var updatedMetrics = await db.QuerySingleAsync<(int Remaining, int Initial, decimal Wac)>(
                    fetchUpdatedSql,
                    new { ProductId = product.ProductId },
                    transaction
                );

                product.RemainingStock = updatedMetrics.Remaining;
                product.InitialStock = updatedMetrics.Initial;
                product.CostPrice = updatedMetrics.Wac;

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<int> GetProductUsageCountAsync(int productId)
        {
            using var db = _context.CreateConnection();
            const string sql = @"
        SELECT 
            (SELECT COUNT(1) FROM OrderItems WHERE ProductId = @productId) +
            (SELECT COUNT(1) FROM PurchaseOrderDetails WHERE ProductId = @productId);";

            return await db.ExecuteScalarAsync<int>(sql, new { productId });
        }

        public async Task<bool> ForceDeleteProductAsync(int productId)
        {
            using var db = _context.CreateConnection();
            if (db.State == ConnectionState.Closed)
                await ((System.Data.Common.DbConnection)db).OpenAsync();

            using var tx = db.BeginTransaction();
            try
            {
                // 1. Remove child records referencing this product
                await db.ExecuteAsync("DELETE FROM OrderItems WHERE ProductId = @productId;", new { productId }, tx);
                await db.ExecuteAsync("DELETE FROM PurchaseOrderDetails WHERE ProductId = @productId;", new { productId }, tx);
                await db.ExecuteAsync("DELETE FROM ProductBatches WHERE ProductId = @productId;", new { productId }, tx);

                // 2. Remove Tier-3 Custom Field values if mapped
                await db.ExecuteAsync("DELETE FROM customfieldvalues WHERE EntityId = @productId AND EntityType = 'Product';", new { productId }, tx);

                // 3. Delete the parent product record
                int rows = await db.ExecuteAsync("DELETE FROM products WHERE ProductId = @productId;", new { productId }, tx);

                tx.Commit();
                return rows > 0;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task<bool> IsBatchNumberDuplicateAsync(string batchNumber, int divisionId)
        {
            if (string.IsNullOrWhiteSpace(batchNumber)) return false;

            const string sql = @"
                SELECT COUNT(1) 
                FROM ProductBatches 
                WHERE BatchNumber = @BatchNo AND DivisionId = @DivId;";

            using var conn = _context.CreateConnection();
            int count = await conn.ExecuteScalarAsync<int>(sql, new { BatchNo = batchNumber, DivId = divisionId });
            return count > 0;
        }

        public async Task<int> SaveProductAssemblyAsync(Product product, ProductBatch? initialBatch = null)
        {
            using var conn = _context.CreateConnection();
            if (conn.State == ConnectionState.Closed) await ((System.Data.Common.DbConnection)conn).OpenAsync();

            using var transaction = conn.BeginTransaction();
            try
            {
                const string productSql = @"
            INSERT INTO Products (
                DivisionId, Name, ShortName, SKU, Unit, CategoryId, 
                Manufacturer, Packaging, InitialStock, RemainingStock, 
                MRP, CostPrice, SellingPrice, GSTPercent, TotalCost, TrackCost, HasBatchTracking, CreatedAt
            ) VALUES (
                @DivisionId, @Name, @ShortName, @SKU, @Unit, @CategoryId, 
                @Manufacturer, @Packaging, @InitialStock, @RemainingStock, 
                @MRP, @CostPrice, @SellingPrice, @GSTPercent, @TotalCost, @TrackCost, @HasBatchTracking, NOW()
            );
            SELECT LAST_INSERT_ID();";

                int generatedProductId = await conn.ExecuteScalarAsync<int>(productSql, product, transaction);

                product.ProductId = generatedProductId;

                // Insert batch only if provided
                if (initialBatch != null)
                {
                    initialBatch.ProductId = generatedProductId;

                    const string batchSql = @"
                INSERT INTO ProductBatches (
                    ProductId, DivisionId, BatchNumber, MfgDate, ExpiryDate, 
                    QuantityReceived, CurrentStock, MinimumSellingPrice, CreatedAt
                ) VALUES (
                    @ProductId, @DivisionId, @BatchNumber, @MfgDate, @ExpiryDate, 
                    @QuantityReceived, @CurrentStock, @MinimumSellingPrice, NOW()
                );";

                    await conn.ExecuteAsync(batchSql, initialBatch, transaction);
                }

                transaction.Commit();
                return generatedProductId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<IEnumerable<Product>> GetProductsByDashboardContextAsync(DashboardTargetView target, DashboardFilter? filter)
        {
            using var db = _context.CreateConnection();
            var parameters = new DynamicParameters();

            string orderHolderFilter = "";
            if (filter != null && !string.IsNullOrEmpty(filter.LeadHolder))
            {
                orderHolderFilter = " AND o.ProcessedBy = @Holder ";
                parameters.Add("Holder", filter.LeadHolder);
            }

            parameters.Add("From", filter?.FromDate);
            parameters.Add("To", filter?.ToDate);
            string dateCondition = (filter?.FromDate != null) ? " AND o.OrderDate BETWEEN @From AND @To " : "";
            string productCreationCondition = (filter?.FromDate != null) ? " AND p.CreatedAt BETWEEN @From AND @To " : "";

            string baseSql = "SELECT p.*, c.CategoryName FROM Products p LEFT JOIN Categories c ON p.CategoryId = c.Id WHERE 1=1 ";

            switch (target)
            {
                case DashboardTargetView.CategoriesList:
                    baseSql = $@"
                        SELECT DISTINCT p.*, c.CategoryName 
                        FROM Products p
                        INNER JOIN Categories c ON p.CategoryId = c.Id
                        INNER JOIN OrderItems oi ON p.ProductId = oi.ProductId
                        INNER JOIN Orders o ON oi.OrderId = o.OrderId
                        WHERE 1=1 {orderHolderFilter} {dateCondition}";
                    break;

                case DashboardTargetView.ProductsList:
                    baseSql += productCreationCondition;
                    break;

                case DashboardTargetView.NewProducts:
                    baseSql += " AND p.CreatedAt >= DATE_SUB(NOW(), INTERVAL 30 DAY)";
                    break;

                case DashboardTargetView.FastMovingProducts:
                    baseSql = $@"
                        SELECT p.*, c.CategoryName 
                        FROM Products p
                        LEFT JOIN Categories c ON p.CategoryId = c.Id
                        INNER JOIN OrderItems oi ON p.ProductId = oi.ProductId
                        INNER JOIN Orders o ON oi.OrderId = o.OrderId
                        WHERE 1=1 {orderHolderFilter} {dateCondition}
                        GROUP BY p.ProductId, c.CategoryName
                        HAVING SUM(oi.Quantity) >= 50";
                    break;

                case DashboardTargetView.SlowMovingProducts:
                    baseSql = $@"
                        SELECT p.*, c.CategoryName 
                        FROM Products p
                        LEFT JOIN Categories c ON p.CategoryId = c.Id
                        LEFT JOIN OrderItems oi ON p.ProductId = oi.ProductId
                        LEFT JOIN Orders o ON oi.OrderId = o.OrderId {orderHolderFilter} {dateCondition}
                        GROUP BY p.ProductId, c.CategoryName
                        HAVING IFNULL(SUM(oi.Quantity), 0) < 5";
                    break;

                case DashboardTargetView.NearSkuProducts:
                    // Corrected check: Low stock alert where remaining stock <= 10% of initial stock or <= threshold
                    baseSql += " AND p.RemainingStock <= (p.InitialStock * 0.10) AND p.InitialStock > 0";
                    break;

                case DashboardTargetView.NearExpiryBatches:
                    baseSql = @"
                        SELECT DISTINCT p.*, c.CategoryName 
                        FROM Products p
                        LEFT JOIN Categories c ON p.CategoryId = c.Id
                        INNER JOIN ProductBatches pb ON p.ProductId = pb.ProductId
                        WHERE pb.ExpiryDate IS NOT NULL 
                          AND pb.ExpiryDate >= NOW() 
                          AND pb.ExpiryDate <= DATE_ADD(NOW(), INTERVAL 3 MONTH)";
                    break;

                case DashboardTargetView.SkippedProducts:
                    baseSql = $@"
                        SELECT DISTINCT p.*, c.CategoryName 
                        FROM Products p
                        LEFT JOIN Categories c ON p.CategoryId = c.Id
                        INNER JOIN OrderItems oi_prev ON p.ProductId = oi_prev.ProductId
                        INNER JOIN Orders o ON oi_prev.OrderId = o.OrderId {orderHolderFilter}
                          AND o.OrderDate >= DATE_ADD(LAST_DAY(DATE_SUB(NOW(), INTERVAL 3 MONTH)), INTERVAL 1 DAY)
                          AND o.OrderDate <= LAST_DAY(DATE_SUB(NOW(), INTERVAL 2 MONTH))
                        WHERE p.ProductId NOT IN (
                            SELECT DISTINCT curr.ProductId 
                            FROM OrderItems curr
                            INNER JOIN Orders ocurr ON curr.OrderId = ocurr.OrderId
                            WHERE ocurr.OrderDate >= DATE_ADD(LAST_DAY(DATE_SUB(NOW(), INTERVAL 2 MONTH)), INTERVAL 1 DAY)
                              AND ocurr.OrderDate <= LAST_DAY(DATE_SUB(NOW(), INTERVAL 1 MONTH))
                              {(string.IsNullOrEmpty(filter?.LeadHolder) ? "" : " AND ocurr.ProcessedBy = @Holder ")}
                        )";
                    break;

                default:
                    baseSql += " AND 1=0 ";
                    break;
            }

            baseSql += " ORDER BY p.Name ASC;";
            return await db.QueryAsync<Product>(baseSql, parameters);
        }

        public async Task<bool> EnableDisableAutoPOAsync(Product product, bool autoReorderEnabled)
        {
            using var db = _context.CreateConnection();
            if (db.State == ConnectionState.Closed) await ((System.Data.Common.DbConnection)db).OpenAsync();

            using var transaction = db.BeginTransaction();
            try
            {
                // STEP 1: Upsert Parent Product (Including HasBatchTracking Column)
                const string sql = @"
            UPDATE products 
            SET AutoReorderEnabled = @AutoReorderEnabled 
            WHERE ProductId = @ProductId;";

                await db.ExecuteAsync(sql, new
                {
                    AutoReorderEnabled = autoReorderEnabled,
                    product.ProductId
                }, transaction);

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }
}