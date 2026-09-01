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
    public class ServiceOrderService
    {
        private readonly CrmDbContext _context;

        public ServiceOrderService(CrmDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 🌟 CUSTOMER BRANDS MANAGEMENT (1-to-Many)
        // ==========================================
        public async Task<IEnumerable<CustomerBrand>> GetBrandsByCustomerIdAsync(int customerId)
        {
            using var conn = _context.CreateConnection();
            const string sql = @"
                SELECT * FROM customer_brands 
                WHERE CustomerId = @CustomerId AND IsActive = 1 
                ORDER BY BrandName ASC;";
            return await conn.QueryAsync<CustomerBrand>(sql, new { CustomerId = customerId });
        }

        public async Task<int> SaveCustomerBrandAsync(CustomerBrand brand)
        {
            using var conn = _context.CreateConnection();
            if (brand.BrandId == 0)
            {
                const string insertSql = @"
                    INSERT INTO customer_brands (CustomerId, BrandName, TrademarkNumber, DrugLicenseNumber, FSSAINumber, IsActive, CreatedAt)
                    VALUES (@CustomerId, @BrandName, @TrademarkNumber, @DrugLicenseNumber, @FSSAINumber, @IsActive, NOW());
                    SELECT LAST_INSERT_ID();";
                return await conn.ExecuteScalarAsync<int>(insertSql, brand);
            }
            else
            {
                const string updateSql = @"
                    UPDATE customer_brands 
                    SET BrandName = @BrandName,
                        TrademarkNumber = @TrademarkNumber,
                        DrugLicenseNumber = @DrugLicenseNumber,
                        FSSAINumber = @FSSAINumber,
                        IsActive = @IsActive
                    WHERE BrandId = @BrandId;";
                await conn.ExecuteAsync(updateSql, brand);
                return brand.BrandId;
            }
        }

        // ==========================================
        // 🌟 SERVICE ORDER CRUD & TRANSACTIONAL SAVE
        // ==========================================
        public async Task<int> SaveServiceOrderAsync(ServiceOrder order)
        {
            using var conn = _context.CreateConnection();
            if (conn.State == ConnectionState.Closed)
                await ((System.Data.Common.DbConnection)conn).OpenAsync();

            using var transaction = conn.BeginTransaction();
            try
            {
                int orderId = order.OrderId;

                if (orderId == 0)
                {
                    const string insertHeaderSql = @"
                        INSERT INTO service_orders (
                            OrderNumber, CustomerId, OrderDate, DeliveryDueDate, 
                            OrderStatus, SubTotalAmount, TaxAmount, GrandTotalAmount, 
                            SpecialInstructions, CreatedAt
                        ) VALUES (
                            @OrderNumber, @CustomerId, @OrderDate, @DeliveryDueDate, 
                            @OrderStatus, @SubTotalAmount, @TaxAmount, @GrandTotalAmount, 
                            @SpecialInstructions, NOW()
                        );
                        SELECT LAST_INSERT_ID();";

                    orderId = await conn.ExecuteScalarAsync<int>(insertHeaderSql, order, transaction);
                    order.OrderId = orderId;
                }
                else
                {
                    // Guard: verify if batch work orders have already been spawned
                    const string checkBatchesSql = "SELECT COUNT(1) FROM production_work_orders WHERE OrderId = @OrderId;";
                    int batchCount = await conn.ExecuteScalarAsync<int>(checkBatchesSql, new { OrderId = orderId }, transaction);

                    if (batchCount > 0)
                    {
                        throw new InvalidOperationException("This order cannot be edited because Batch Work Orders have already been spawned. Any minor changes must be updated directly in the Batch Order remarks.");
                    }

                    const string updateHeaderSql = @"
                        UPDATE service_orders 
                        SET CustomerId = @CustomerId,
                            OrderDate = @OrderDate,
                            DeliveryDueDate = @DeliveryDueDate,
                            OrderStatus = @OrderStatus,
                            SubTotalAmount = @SubTotalAmount,
                            TaxAmount = @TaxAmount,
                            GrandTotalAmount = @GrandTotalAmount,
                            SpecialInstructions = @SpecialInstructions
                        WHERE OrderId = @OrderId;";

                    await conn.ExecuteAsync(updateHeaderSql, order, transaction);
                    await conn.ExecuteAsync("DELETE FROM service_order_items WHERE OrderId = @OrderId;", new { OrderId = orderId }, transaction);
                }

                if (order.Items != null && order.Items.Any())
                {
                    const string insertItemSql = @"
                        INSERT INTO service_order_items (
                            OrderId, BrandId, BrandName, ProductId, ProductName, MasterFormulationId, 
                            PackagingType, PackSize, ColorShade, FragranceFlavor, 
                            ContainerMaterial, CapOrClosureType, TargetQuantity, 
                            Unit, UnitPrice, GSTPercent, LineTotal, ProductionStatus
                        ) VALUES (
                            @OrderId, @BrandId, @BrandName, @ProductId, @ProductName, @MasterFormulationId, 
                            @PackagingType, @PackSize, @ColorShade, @FragranceFlavor, 
                            @ContainerMaterial, @CapOrClosureType, @TargetQuantity, 
                            @Unit, @UnitPrice, @GSTPercent, @LineTotal, @ProductionStatus
                        );
                        SELECT LAST_INSERT_ID();";

                    const string insertBomSql = @"
                        INSERT INTO service_order_item_bom (
                            OrderItemId, RawMaterialProductId, Phase, PercentageValue, 
                            CalculatedQuantity, Unit, Remarks, SequenceOrder
                        ) VALUES (
                            @OrderItemId, @RawMaterialProductId, @Phase, @PercentageValue, 
                            @CalculatedQuantity, @Unit, @Remarks, @SequenceOrder
                        );";

                    foreach (var item in order.Items)
                    {
                        item.OrderId = orderId;
                        int orderItemId = await conn.ExecuteScalarAsync<int>(insertItemSql, item, transaction);
                        item.OrderItemId = orderItemId;

                        if (item.BomItems != null && item.BomItems.Any())
                        {
                            int seq = 1;
                            foreach (var bom in item.BomItems)
                            {
                                bom.OrderItemId = orderItemId;
                                bom.SequenceOrder = seq++;
                                await conn.ExecuteAsync(insertBomSql, bom, transaction);
                            }
                        }
                    }
                }

                transaction.Commit();
                return orderId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<IEnumerable<ServiceOrder>> GetAllOrdersAsync()
        {
            using var conn = _context.CreateConnection();
            const string sql = @"
                SELECT 
                    so.OrderId,
                    so.OrderNumber,
                    so.CustomerId,
                    so.OrderDate,
                    so.DeliveryDueDate,
                    so.OrderStatus,
                    so.SubTotalAmount,
                    so.TaxAmount,
                    so.GrandTotalAmount,
                    so.SpecialInstructions,
                    c.CustomerName AS CustomerName,
                    (SELECT COUNT(1) FROM production_work_orders pwo WHERE pwo.OrderId = so.OrderId) AS BatchOrdersCount
                FROM service_orders so
                LEFT JOIN leads c ON so.CustomerId = c.LeadId
                ORDER BY so.OrderId DESC;";

            return await conn.QueryAsync<ServiceOrder>(sql);
        }

        public async Task<ServiceOrder?> GetOrderByIdAsync(int orderId)
        {
            using var conn = _context.CreateConnection();

            const string headerSql = @"
                SELECT 
                    so.*,
                    c.CustomerName AS CustomerName,
                    (SELECT COUNT(1) FROM production_work_orders pwo WHERE pwo.OrderId = so.OrderId) AS BatchOrdersCount
                FROM service_orders so
                LEFT JOIN leads c ON so.CustomerId = c.LeadId
                WHERE so.OrderId = @OrderId;";

            var order = await conn.QueryFirstOrDefaultAsync<ServiceOrder>(headerSql, new { OrderId = orderId });
            if (order == null) return null;

            const string itemsSql = @"
                SELECT 
                    soi.*,
                    p.Name AS BaseProductSKUName,
                    mf.FormulationName
                FROM service_order_items soi
                LEFT JOIN products p ON soi.ProductId = p.ProductId
                LEFT JOIN master_formulations mf ON soi.MasterFormulationId = mf.FormulationId
                WHERE soi.OrderId = @OrderId;";

            var items = (await conn.QueryAsync<ServiceOrderItem>(itemsSql, new { OrderId = orderId })).ToList();

            const string bomSql = @"
                SELECT 
                    b.*,
                    p.Name AS RawMaterialName,
                    p.ShortName AS RawMaterialCode
                FROM service_order_item_bom b
                INNER JOIN products p ON b.RawMaterialProductId = p.ProductId
                WHERE b.OrderItemId = @OrderItemId
                ORDER BY b.Phase ASC, b.SequenceOrder ASC;";

            foreach (var item in items)
            {
                var bomItems = await conn.QueryAsync<ServiceOrderItemBom>(bomSql, new { OrderItemId = item.OrderItemId });
                item.BomItems = new System.Collections.ObjectModel.ObservableCollection<ServiceOrderItemBom>(bomItems);
            }

            order.Items = new System.Collections.ObjectModel.ObservableCollection<ServiceOrderItem>(items);
            return order;
        }

        public async Task<bool> DeleteOrderAsync(int orderId)
        {
            using var conn = _context.CreateConnection();

            const string checkBatchesSql = "SELECT COUNT(1) FROM production_work_orders WHERE OrderId = @OrderId;";
            int batchCount = await conn.ExecuteScalarAsync<int>(checkBatchesSql, new { OrderId = orderId });

            if (batchCount > 0)
            {
                throw new InvalidOperationException("Cannot delete this order because Production Batch Orders have already been created.");
            }

            const string sql = "DELETE FROM service_orders WHERE OrderId = @OrderId;";
            return await conn.ExecuteAsync(sql, new { OrderId = orderId }) > 0;
        }

        /// <summary>
        /// Approves the order, transitions status to 'InProduction', and spawns 1 batch work order per line item with complete BOM & stages.
        /// </summary>
        public async Task<List<ProductionWorkOrder>> ApproveAndSpawnBatchesAsync(int orderId)
        {
            using var conn = _context.CreateConnection();
            if (conn.State == ConnectionState.Closed)
                await ((System.Data.Common.DbConnection)conn).OpenAsync();

            using var transaction = conn.BeginTransaction();
            try
            {
                // 1. Verify order exists and is in 'Draft' state
                const string checkOrderSql = @"
            SELECT OrderId, OrderNumber, CustomerId, OrderStatus 
            FROM service_orders 
            WHERE OrderId = @OrderId;";

                var order = await conn.QueryFirstOrDefaultAsync<ServiceOrder>(checkOrderSql, new { OrderId = orderId }, transaction);
                if (order == null)
                    throw new InvalidOperationException($"Service order #{orderId} was not found.");

                if (order.OrderStatus == "InProduction" || order.OrderStatus == "Completed")
                    throw new InvalidOperationException($"Order #{order.OrderNumber} is already in production or completed.");

                // 2. Fetch all Line Items
                const string itemsSql = @"
            SELECT * FROM service_order_items 
            WHERE OrderId = @OrderId;";
                var items = (await conn.QueryAsync<ServiceOrderItem>(itemsSql, new { OrderId = orderId }, transaction)).ToList();

                if (!items.Any())
                    throw new InvalidOperationException("Cannot process order without product line items.");

                // 3. Update Order status to 'InProduction'
                const string updateOrderSql = @"
            UPDATE service_orders 
            SET OrderStatus = 'InProduction', UpdatedAt = NOW() 
            WHERE OrderId = @OrderId;";
                await conn.ExecuteAsync(updateOrderSql, new { OrderId = orderId }, transaction);

                var generatedBatches = new List<ProductionWorkOrder>();
                int batchSequence = 1;

                // 4. Iterate and spawn 1 batch per item
                foreach (var item in items)
                {
                    string batchNumber = $"BT-{DateTime.Now:yyyyMMdd}-{orderId:D3}-{batchSequence:D2}";
                    batchSequence++;

                    const string insertWorkOrderSql = @"
                INSERT INTO production_work_orders (
                    BatchNumber, OrderId, OrderItemId, CustomerId, BrandName, 
                    ProductId, BatchSize, CurrentStage, MfgDate, ExpiryDate, 
                    ProductionNotes, CreatedAt
                ) VALUES (
                    @BatchNumber, @OrderId, @OrderItemId, @CustomerId, @BrandName, 
                    @ProductId, @BatchSize, 'Dispensing', @MfgDate, @ExpiryDate, 
                    @ProductionNotes, NOW()
                );
                SELECT LAST_INSERT_ID();";

                    var workOrder = new ProductionWorkOrder
                    {
                        BatchNumber = batchNumber,
                        OrderId = orderId,
                        OrderItemId = item.OrderItemId,
                        CustomerId = order.CustomerId,
                        BrandName = item.BrandName,
                        ProductId = item.ProductId,
                        BatchSize = item.TargetQuantity,
                        CurrentStage = "Dispensing",
                        MfgDate = DateTime.Today,
                        ExpiryDate = DateTime.Today.AddYears(2),
                        ProductionNotes = $"Specs: {item.PackagingType} | {item.PackSize} | Shade: {item.ColorShade ?? "Std"} | Fragrance: {item.FragranceFlavor ?? "Std"}"
                    };

                    int workOrderId = await conn.ExecuteScalarAsync<int>(insertWorkOrderSql, workOrder, transaction);
                    workOrder.WorkOrderId = workOrderId;

                    // 5. Copy Line-Item BOM to Batch BOM Snapshot
                    const string copyBomSql = @"
                INSERT INTO work_order_bom_items (
                    WorkOrderId, RawMaterialProductId, Phase, PercentageValue, 
                    CalculatedQuantity, Unit, Remarks, SequenceOrder
                )
                SELECT 
                    @WorkOrderId, RawMaterialProductId, Phase, PercentageValue, 
                    CalculatedQuantity, Unit, Remarks, SequenceOrder
                FROM service_order_item_bom
                WHERE OrderItemId = @OrderItemId;";

                    await conn.ExecuteAsync(copyBomSql, new { WorkOrderId = workOrderId, OrderItemId = item.OrderItemId }, transaction);

                    // 6. Spawn Default Production Stages Checklist
                    const string insertStageSql = @"
                INSERT INTO work_order_stages (
                    WorkOrderId, StageName, SequenceOrder, Status, StartedAt
                ) VALUES 
                (@WorkOrderId, 'Material Dispensing & Weighing', 1, 'InProgress', NOW()),
                (@WorkOrderId, 'Phase Mixing & Formulation', 2, 'Pending', NULL),
                (@WorkOrderId, 'Primary Filling & Packing', 3, 'Pending', NULL),
                (@WorkOrderId, 'Secondary Packing & Labeling', 4, 'Pending', NULL),
                (@WorkOrderId, 'QC Lab Testing & Release', 5, 'Pending', NULL);";

                    await conn.ExecuteAsync(insertStageSql, new { WorkOrderId = workOrderId }, transaction);

                    // 7. Mark Line Item as InProduction
                    const string updateItemSql = @"
                UPDATE service_order_items 
                SET ProductionStatus = 'InProduction' 
                WHERE OrderItemId = @OrderItemId;";
                    await conn.ExecuteAsync(updateItemSql, new { OrderItemId = item.OrderItemId }, transaction);

                    generatedBatches.Add(workOrder);
                }

                transaction.Commit();
                return generatedBatches;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }
}
