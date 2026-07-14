using CallMan.Data;
using CallMan.Models;
using CallMan.Models.Enums;
using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
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

        public async Task<IEnumerable<Product>> GetAllProductsAsync(int divisionId)
        {
            using var db = _context.CreateConnection();

            // We fetch the category name and aggregate batch stock/WAC price dynamically
            string sql = @"
        SELECT 
            p.*, 
            c.CategoryName AS CategoryName,
            IFNULL(b.AggStock, 0) AS RemainingStock,
            IFNULL(b.BatchCount, 0) AS TotalBatchesCount, -- Pulls total registered lots count
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
                COUNT(BatchId) AS BatchCount, -- Counts total batches for this product
                SUM(CurrentStock * MinimumSellingPrice) AS TotalValue
            FROM ProductBatches
            WHERE DivisionId = @DivId
            GROUP BY ProductId
        ) b ON p.ProductId = b.ProductId        
        ORDER BY p.Name ASC;";

            return await db.QueryAsync<Product>(sql, new { DivId = divisionId });
        }

        public async Task<IEnumerable<Product>> GetProductsWithBatchesAsync(int divisionId)
        {
            // The dual SQL queries executing inside a unified round-trip connection block
            const string sql = @"
        SELECT * FROM Products WHERE DivisionId = @DivId;
        SELECT * FROM ProductBatches WHERE DivisionId = @DivId AND CurrentStock > 0;";

            using var db = _context.CreateConnection();

            // Use QueryMultiple to download both database tables simultaneously
            using var gridReader = await db.QueryMultipleAsync(sql, new { DivId = divisionId });

            // Read the datasets sequentially out of the stream memory buffer
            var products = (await gridReader.ReadAsync<Product>()).ToList();
            var batches = (await gridReader.ReadAsync<ProductBatch>()).ToList();

            // MAPPER LOOKUP LOOP: Group and link child batch lines straight into their respective parent product containers
            foreach (var product in products)
            {
                var productSpecificLots = batches.Where(b => b.ProductId == product.ProductId);

                product.InnerBatchesCollection.Clear();
                foreach (var batch in productSpecificLots)
                {
                    product.InnerBatchesCollection.Add(batch);
                }

                // Ensure the helper total property matches the actual list dimension depth footprint counts
                product.TotalBatchesCount = product.InnerBatchesCollection.Count;
            }

            return products;
        }

        /// <summary>
        /// Retrieves all inventory batches associated with a specific product and division context.
        /// </summary>
        /// <param name="productId">The target product ID to fetch batches for.</param>
        /// <param name="divisionId">The active division registry context filter ID.</param>
        /// <returns>A collection of ProductBatch objects matching the product.</returns>
        public async Task<IEnumerable<ProductBatch>> GetBatchesByProductIdAsync(int productId, int divisionId)
        {
            const string sql = @"
                SELECT 
                    BatchId,
                    ProductId,
                    DivisionId,
                    BatchNumber,
                    MfgDate,
                    ExpiryDate,
                    QuantityReceived,
                    CurrentStock,
                    MinimumSellingPrice,
                    CreatedAt
                FROM ProductBatches
                WHERE ProductId = @ProdId AND DivisionId = @DivId
                ORDER BY ExpiryDate ASC, BatchId DESC;";

            try
            {
                using var db = _context.CreateConnection();
                return await db.QueryAsync<ProductBatch>(sql, new { ProdId = productId, DivId = divisionId });
            }
            catch (Exception ex)
            {
                // Add your Sentry/logging hook here
                throw new InvalidOperationException($"Failed to retrieve inventory batches for Product ID: {productId}", ex);
            }
        }

        public async Task<bool> UpsertProductWithBatchAsync(Product product, ProductBatch batch)
        {
            using var db = _context.CreateConnection();
            if (db.State == ConnectionState.Closed) await ((System.Data.Common.DbConnection)db).OpenAsync();

            using var transaction = db.BeginTransaction();
            try
            {
                // STEP 1: Upsert the Parent Product Data Row
                string productSql;
                if (product.ProductId == 0)
                {
                    productSql = @"
                INSERT INTO Products (
                    DivisionId, Name, ShortName, BrandName, SKU, Unit, CategoryId, Manufacturer, Packaging, 
                    InitialStock, RemainingStock, MRP, CostPrice, SellingPrice, GSTPercent, TotalCost, TrackCost, CreatedAt
                ) VALUES (
                    @DivisionId, @Name, @ShortName, @BrandName, @SKU, @Unit, @CategoryId, @Manufacturer, @Packaging, 
                    @InitialStock, @RemainingStock, @MRP, @CostPrice, @SellingPrice, @GSTPercent, @TotalCost, @TrackCost, NOW()
                );
                SELECT LAST_INSERT_ID();";

                    product.ProductId = await db.ExecuteScalarAsync<int>(productSql, product, transaction);
                    batch.ProductId = product.ProductId; // Link the generated foreign key
                }
                else
                {
                    productSql = @"
                UPDATE Products 
                SET 
                    Name = @Name, ShortName = @ShortName, BrandName = @BrandName, SKU = @SKU, Unit = @Unit,
                    CategoryId = @CategoryId, Manufacturer = @Manufacturer, Packaging = @Packaging,
                    MRP = @MRP, SellingPrice = @SellingPrice, GSTPercent = @GSTPercent, 
                    TotalCost = @TotalCost, TrackCost = @TrackCost 
                WHERE ProductId = @ProductId AND DivisionId = @DivisionId;";

                    await db.ExecuteAsync(productSql, product, transaction);
                }

                // STEP 2: Upsert the Child Product Batch Row
                string batchSql;
                if (batch.BatchId == 0)
                {
                    batchSql = @"
                INSERT INTO ProductBatches (
                    ProductId, DivisionId, BatchNumber, MfgDate, ExpiryDate, 
                    QuantityReceived, CurrentStock, MinimumSellingPrice, CreatedAt
                ) VALUES (
                    @ProductId, @DivisionId, @BatchNumber, @MfgDate, @ExpiryDate, 
                    @QuantityReceived, @CurrentStock, @MinimumSellingPrice, NOW()
                );";
                }
                else
                {
                    batchSql = @"
                UPDATE ProductBatches 
                SET 
                    BatchNumber = @BatchNumber,
                    MfgDate = @MfgDate,
                    ExpiryDate = @ExpiryDate,
                    QuantityReceived = @QuantityReceived,
                    CurrentStock = @CurrentStock,
                    MinimumSellingPrice = @MinimumSellingPrice
                WHERE BatchId = @BatchId AND ProductId = @ProductId AND DivisionId = @DivisionId;";
                }

                await db.ExecuteAsync(batchSql, batch, transaction);

                // STEP 3: Automatically recalculate parent aggregates in the DB 
                // Handles total lifetime received stock, remaining current stock, and live WAC pricing engines
                const string syncSql = @"
            UPDATE Products p 
            SET 
                p.InitialStock = IFNULL((SELECT SUM(QuantityReceived) FROM ProductBatches WHERE ProductId = p.ProductId), 0),
                p.RemainingStock = IFNULL((SELECT SUM(CurrentStock) FROM ProductBatches WHERE ProductId = p.ProductId AND CurrentStock > 0), 0),
                p.CostPrice = IFNULL(
                    (SELECT ROUND(SUM(CurrentStock * MinimumSellingPrice) / SUM(CurrentStock), 2) 
                     FROM ProductBatches 
                     WHERE ProductId = p.ProductId AND CurrentStock > 0), 
                    @FallbackCost
                )
            WHERE p.ProductId = @ProductId AND p.DivisionId = @DivisionId;";

                await db.ExecuteAsync(syncSql, new
                {
                    ProductId = product.ProductId,
                    DivisionId = product.DivisionId,
                    FallbackCost = batch.MinimumSellingPrice
                }, transaction);

                // STEP 4: Pull the calculated metrics out of the DB and assign back to our UI bound C# model reference
                const string fetchUpdatedSql = @"
            SELECT RemainingStock, InitialStock, CostPrice 
            FROM Products 
            WHERE ProductId = @ProductId AND DivisionId = @DivisionId;";

                var updatedMetrics = await db.QuerySingleAsync<(int Remaining, int Initial, decimal Wac)>(
                    fetchUpdatedSql,
                    new { ProductId = product.ProductId, DivisionId = product.DivisionId },
                    transaction
                );

                // Map live database state straight back onto your C# instances so your DataGrid updates instantly
                product.RemainingStock = updatedMetrics.Remaining;
                product.InitialStock = updatedMetrics.Initial;
                product.CostPrice = updatedMetrics.Wac;

                transaction.Commit();
                return true;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                // Add your Sentry logging integration hook here
                throw new InvalidOperationException("Failed to upsert product with batch details safely.", ex);
            }
        }

        public async Task<bool> DeleteProductAsync(int id)
        {
            using var db = _context.CreateConnection();
            return await db.ExecuteAsync("DELETE FROM Products WHERE ProductId = @id", new { id }) > 0;
        }

        /// <summary>
        /// Checks if a Batch Number is already registered under a specific division.
        /// </summary>
        public async Task<bool> IsBatchNumberDuplicateAsync(string batchNumber, int divisionId)
        {
            const string sql = @"
                SELECT COUNT(1) 
                FROM ProductBatches 
                WHERE BatchNumber = @BatchNo AND DivisionId = @DivId;";

            using var conn = _context.CreateConnection();
            int count = await conn.ExecuteScalarAsync<int>(sql, new { BatchNo = batchNumber, DivId = divisionId });
            return count > 0;
        }

        /// <summary>
        /// Saves a new product along with its first inventory batch in a single atomic transaction.
        /// </summary>
        public async Task<int> SaveProductAssemblyAsync(Product product, ProductBatch initialBatch)
        {
            using var conn = _context.CreateConnection();

            using var transaction = conn.BeginTransaction();
            try
            {
                // 1. Insert Parent Product Row
                const string productSql = @"
                    INSERT INTO Products (
                        DivisionId, Name, ShortName, SKU, Unit, CategoryId, 
                        Manufacturer, Packaging, InitialStock, RemainingStock, 
                        MRP, CostPrice, SellingPrice, GSTPercent, TotalCost, TrackCost, CreatedAt
                    ) VALUES (
                        @DivisionId, @Name, @ShortName, @SKU, @Unit, @CategoryId, 
                        @Manufacturer, @Packaging, @InitialStock, @RemainingStock, 
                        @MRP, @CostPrice, @SellingPrice, @GSTPercent, @TotalCost, @TrackCost, NOW()
                    );
                    SELECT LAST_INSERT_ID();";

                // Execute and capture the generated ProductId auto-increment pointer
                int generatedProductId = await conn.ExecuteScalarAsync<int>(productSql, product, transaction);

                // Assign identity references to our tracking sub-objects
                product.ProductId = generatedProductId;
                initialBatch.ProductId = generatedProductId;

                // 2. Insert Initial Batch Row
                const string batchSql = @"
                    INSERT INTO ProductBatches (
                        ProductId, DivisionId, BatchNumber, MfgDate, ExpiryDate, 
                        QuantityReceived, CurrentStock, MinimumSellingPrice, CreatedAt
                    ) VALUES (
                        @ProductId, @DivisionId, @BatchNumber, @MfgDate, @ExpiryDate, 
                        @QuantityReceived, @CurrentStock, @MinimumSellingPrice, NOW()
                    );";

                await conn.ExecuteAsync(batchSql, initialBatch, transaction);

                transaction.Commit();
                return generatedProductId;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                // Add your Sentry/logging hook here
                throw new InvalidOperationException("Failed to commit product inventory batch assembly.", ex);
            }
        }

        public async Task<IEnumerable<Product>> GetProductsByDashboardContextAsync(DashboardTargetView target, DashboardFilter? filter)
        {
            using var db = _context.CreateConnection();
            var parameters = new DynamicParameters();

            // 1. Structural Filters Setup (Matches executive tracking for orders)
            string orderHolderFilter = "";
            if (filter != null && !string.IsNullOrEmpty(filter.LeadHolder))
            {
                orderHolderFilter = " AND o.ProcessedBy = @Holder ";
                parameters.Add("Holder", filter.LeadHolder);
            }

            // 2. Date Parameters Setup
            parameters.Add("From", filter?.FromDate);
            parameters.Add("To", filter?.ToDate);
            string dateCondition = (filter?.FromDate != null) ? " AND o.OrderDate BETWEEN @From AND @To " : "";
            string productCreationCondition = (filter?.FromDate != null) ? " AND p.CreatedAt BETWEEN @From AND @To " : "";

            // Base SELECT template anchor
            string baseSql = "SELECT p.*, c.CategoryName FROM Products p LEFT JOIN Categories c ON p.CategoryId = c.Id WHERE 1=1 ";

            switch (target)
            {
                case DashboardTargetView.CategoriesList:
                    // 1. TOTAL CATEGORIES: Pulls active products grouped inside categories sold within the filtered window
                    baseSql = $@"
                SELECT DISTINCT p.*, c.CategoryName 
                FROM Products p
                INNER JOIN Categories c ON p.CategoryId = c.Id
                INNER JOIN OrderItems oi ON p.ProductId = oi.ProductId
                INNER JOIN Orders o ON oi.OrderId = o.OrderId
                WHERE 1=1 {orderHolderFilter} {dateCondition}";
                    break;

                case DashboardTargetView.ProductsList:
                    // 2. TOTAL PRODUCTS: Active items in the master table (constrained by date range if applied)
                    baseSql += productCreationCondition;
                    break;

                case DashboardTargetView.NewProducts:
                    // 3. NEW PRODUCTS: Items introduced to the inventory ecosystem within the last 30 days
                    baseSql += " AND p.CreatedAt >= DATE_SUB(NOW(), INTERVAL 30 DAY)";
                    break;

                case DashboardTargetView.FastMovingProducts:
                    // 4. FAST MOVING: High-velocity items with a total sold volume greater than or equal to 50 units
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
                    // 5. SLOW MOVING: Low-velocity or stagnant items moving under 5 units within the filtered window
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
                    // 6. NEAR SKU: Low inventory alert check (remaining stock is less than or equal to minimum safe threshold limits)
                    baseSql += " AND p.RemainingStock <= p.SKU AND p.SKU > 0";
                    break;

                case DashboardTargetView.NearExpiryBatches:
                    // 7. NEAR EXPIRY: Inventory batches whose chemical/shelf expiration milestones arrive inside 3 months
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
                    // 8. SKIPPED PRODUCTS: Products sold 2-3 months ago but entirely missed/skipped during the past 30 days
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
    }
}
