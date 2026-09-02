using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tijori.Data;

namespace Tijori.Services
{
    public class AutoPurchaseOrderService
    {
        private readonly CrmDbContext _context;

        public AutoPurchaseOrderService(CrmDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Evaluates all inventory. If any product breaches its minimum SKU threshold,
        /// creates consolidated draft purchase orders grouped by primary vendor.
        /// </summary>
        public async Task<List<int>> EvaluateAndGenerateAutoPOsAsync(string createdBy = "System Auto-Reorder")
        {
            using var db = _context.CreateConnection();
            if (db.State == ConnectionState.Closed) db.Open();
            using var tx = db.BeginTransaction();

            var generatedPoIds = new List<int>();

            try
            {
                // 1. Fetch breached items with their top-priority vendor
                const string scanBreachedSql = @"
                    WITH RankedVendors AS (
                        SELECT 
                            p.ProductId,
                            p.Name AS ProductName,
                            p.ShortName AS SupplierSku,
                            p.RemainingStock,
                            CAST(NULLIF(REGEXP_REPLACE(p.SKU, '[^0-9]', ''), '') AS SIGNED) AS ThresholdLimit,
                            COALESCE(NULLIF(p.ReorderQuantity, 0), 50) AS ReorderQty,
                            p.CostPrice AS DefaultCostPrice,
                            p.MRP,
                            vpl.VendorId,
                            v.CompanyName AS VendorName,
                            COALESCE(NULLIF(vpl.PurchasePrice, 0), p.CostPrice, 0.00) AS EffectiveRate,
                            COALESCE(NULLIF(vpl.LeadTimeDays, 0), 3) AS DeliveryDays,
                            ROW_NUMBER() OVER(
                                PARTITION BY p.ProductId 
                                ORDER BY vpl.IsPreferredVendor DESC, vpl.VendorPriority ASC, vpl.PurchasePrice ASC
                            ) AS VendorRank
                        FROM products p
                        INNER JOIN vendorproductlinks vpl ON p.ProductId = vpl.ProductId
                        INNER JOIN vendors v ON vpl.VendorId = v.VendorId
                        WHERE p.AutoReorderEnabled = 1
                          -- Filter where remaining stock has touched or breached the SKU limit
                          AND p.RemainingStock <= CAST(NULLIF(REGEXP_REPLACE(p.SKU, '[^0-9]', ''), '') AS SIGNED)
                          -- Duplicate Order Guard: Don't reorder if an open/draft PO is already in progress
                          AND p.ProductId NOT IN (
                              SELECT pod.ProductId 
                              FROM purchaseorderdetails pod
                              INNER JOIN purchaseorders po ON pod.PurchaseOrderId = po.PurchaseOrderId
                              WHERE po.OrderStatus IN ('Draft', 'Ordered')
                          )
                    )
                    SELECT * FROM RankedVendors WHERE VendorRank = 1;";

                var breachedItems = (await db.QueryAsync<BreachedProductDTO>(scanBreachedSql, transaction: tx)).ToList();

                if (!breachedItems.Any())
                {
                    tx.Commit();
                    return generatedPoIds;
                }

                // 2. Group items by supplier so multiple low-stock items go into a single PO
                var vendorGroups = breachedItems.GroupBy(b => b.VendorId).ToList();

                foreach (var group in vendorGroups)
                {
                    int vendorId = group.Key;
                    var vendorItems = group.ToList();

                    string poNumber = $"AUTO-PO-{DateTime.Today:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";
                    int maxLeadDays = vendorItems.Max(i => i.DeliveryDays);
                    DateTime expectedDelivery = DateTime.Today.AddDays(maxLeadDays > 0 ? maxLeadDays : 3);

                    decimal taxableTotal = vendorItems.Sum(i => i.ReorderQty * i.EffectiveRate);
                    decimal estimatedTax = taxableTotal * 0.05m; // 5% GST estimate
                    decimal grandTotal = taxableTotal + estimatedTax;

                    // 3. Insert Purchase Order Header
                    const string insertPoSql = @"
                        INSERT INTO purchaseorders (
                            PoNumber, VendorId, OrderDate, InvoiceDate, ExpectedDeliveryDate,
                            TaxableAmount, DiscountAmount, TaxAmount, RoundOff, TotalAmount,
                            OrderStatus, CreatedBy
                        ) VALUES (
                            @PoNumber, @VendorId, CURDATE(), CURDATE(), @ExpectedDeliveryDate,
                            @TaxableAmount, 0.00, @TaxAmount, 0.00, @TotalAmount,
                            'Draft', @CreatedBy
                        );
                        SELECT LAST_INSERT_ID();";

                    int poId = await db.ExecuteScalarAsync<int>(insertPoSql, new
                    {
                        PoNumber = poNumber,
                        VendorId = vendorId,
                        ExpectedDeliveryDate = expectedDelivery,
                        TaxableAmount = taxableTotal,
                        TaxAmount = estimatedTax,
                        TotalAmount = grandTotal,
                        CreatedBy = createdBy
                    }, tx);

                    // 4. Insert Purchase Order Line Items
                    const string insertLineSql = @"
                        INSERT INTO purchaseorderdetails (
                            PurchaseOrderId, ProductId, BatchNumber, Quantity, FreeQuantity,
                            UnitPrice, MRP, DiscountPercent, TaxPercent, TaxAmount, TotalAmount
                        ) VALUES (
                            @PurchaseOrderId, @ProductId, NULL, @Quantity, 0,
                            @UnitPrice, @MRP, 0.00, 5.00, @TaxAmount, @TotalAmount
                        );";

                    foreach (var item in vendorItems)
                    {
                        decimal lineTaxable = item.ReorderQty * item.EffectiveRate;
                        decimal lineTax = lineTaxable * 0.05m;

                        await db.ExecuteAsync(insertLineSql, new
                        {
                            PurchaseOrderId = poId,
                            item.ProductId,
                            Quantity = item.ReorderQty,
                            UnitPrice = item.EffectiveRate,
                            MRP = item.MRP > 0 ? item.MRP : (item.EffectiveRate * 1.25m),
                            TaxAmount = lineTax,
                            TotalAmount = lineTaxable + lineTax
                        }, tx);
                    }

                    generatedPoIds.Add(poId);
                }

                tx.Commit();
                return generatedPoIds;
            }
            catch (Exception ex)
            {
                tx.Rollback();
                System.Diagnostics.Debug.WriteLine($"[AUTO-PO ENGINE ERROR]: {ex.Message}");
                throw;
            }
        }

        private class BreachedProductDTO
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; } = string.Empty;
            public string? SupplierSku { get; set; }
            public int RemainingStock { get; set; }
            public int ThresholdLimit { get; set; }
            public int ReorderQty { get; set; }
            public decimal DefaultCostPrice { get; set; }
            public decimal MRP { get; set; }
            public int VendorId { get; set; }
            public string VendorName { get; set; } = string.Empty;
            public decimal EffectiveRate { get; set; }
            public int DeliveryDays { get; set; }
        }
    }
}
