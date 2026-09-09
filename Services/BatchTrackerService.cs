using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Tijori.Data;
using Tijori.Models;

namespace Tijori.Services
{
    public class BatchTrackerService
    {
        private readonly CrmDbContext _context;
        private readonly StockLedgerService _stockService;

        public BatchTrackerService(CrmDbContext context, StockLedgerService stockService)
        {
            _context = context;
            _stockService = stockService;
        }

        public async Task<IEnumerable<ProductionWorkOrder>> GetAllBatchesAsync(string? stageFilter = null)
        {
            using var conn = _context.CreateConnection();
            string sql = @"
                SELECT 
                    pwo.*,
                    c.CustomerName AS CustomerName,
                    COALESCE(p.Name, soi.ProductName) AS ProductName,
                    soi.PackagingType,
                    soi.PackSize,
                    soi.ColorShade,
                    soi.FragranceFlavor
                FROM production_work_orders pwo
                LEFT JOIN leads c ON pwo.CustomerId = c.LeadId
                LEFT JOIN service_order_items soi ON pwo.OrderItemId = soi.OrderItemId
                LEFT JOIN products p ON pwo.ProductId = p.ProductId
                WHERE (@StageFilter IS NULL OR @StageFilter = 'All' OR pwo.CurrentStage = @StageFilter)
                ORDER BY pwo.WorkOrderId DESC;";

            return await conn.QueryAsync<ProductionWorkOrder>(sql, new { StageFilter = stageFilter });
        }

        public async Task<ProductionWorkOrder?> GetBatchDetailsAsync(int workOrderId)
        {
            using var conn = _context.CreateConnection();

            const string headerSql = @"
                SELECT 
                    pwo.*,
                    c.CustomerName AS CustomerName,
                    COALESCE(p.Name, soi.ProductName) AS ProductName,
                    so.OrderNumber,
                    soi.PackagingType,
                    soi.PackSize,
                    soi.ColorShade,
                    soi.FragranceFlavor
                FROM production_work_orders pwo
                LEFT JOIN leads c ON pwo.CustomerId = c.LeadId
                LEFT JOIN service_orders so ON pwo.OrderId = so.OrderId
                LEFT JOIN service_order_items soi ON pwo.OrderItemId = soi.OrderItemId
                LEFT JOIN products p ON pwo.ProductId = p.ProductId
                WHERE pwo.WorkOrderId = @WorkOrderId;";

            var batch = await conn.QueryFirstOrDefaultAsync<ProductionWorkOrder>(headerSql, new { WorkOrderId = workOrderId });
            if (batch == null) return null;

            // Fetch Stages
            const string stagesSql = @"
                SELECT * FROM work_order_stages 
                WHERE WorkOrderId = @WorkOrderId 
                ORDER BY SequenceOrder ASC;";
            var stages = await conn.QueryAsync<WorkOrderStage>(stagesSql, new { WorkOrderId = workOrderId });
            batch.Stages = new System.Collections.ObjectModel.ObservableCollection<WorkOrderStage>(stages);

            return batch;
        }

        public async Task<IEnumerable<WorkOrderBomItem>> GetBatchBOMAsync(int workOrderId)
        {
            using var conn = _context.CreateConnection();
            const string bomSql = @"
                SELECT 
                    wob.*,
                    p.Name AS RawMaterialName,
                    p.ShortName AS RawMaterialCode
                FROM work_order_bom_items wob
                INNER JOIN products p ON wob.RawMaterialProductId = p.ProductId
                WHERE wob.WorkOrderId = @WorkOrderId
                ORDER BY wob.Phase ASC, wob.SequenceOrder ASC;";

            return await conn.QueryAsync<WorkOrderBomItem>(bomSql, new { WorkOrderId = workOrderId });
        }

        /// <summary>
        /// Completes the active stage and advances the batch to the next sequential stage.
        /// </summary>
        public async Task<bool> AdvanceBatchStageAsync(int workOrderId, int currentStageId, string? operatorRemarks)
        {
            using var conn = _context.CreateConnection();
            if (conn.State == ConnectionState.Closed)
                await ((System.Data.Common.DbConnection)conn).OpenAsync();

            using var transaction = conn.BeginTransaction();
            try
            {
                // 1. Fetch Work Order & Stage Details
                const string batchSql = @"
            SELECT pwo.*, soi.UnitPrice, soi.PackagingType, soi.PackSize 
            FROM production_work_orders pwo
            LEFT JOIN service_order_items soi ON pwo.OrderItemId = soi.OrderItemId
            WHERE pwo.WorkOrderId = @WorkOrderId;";
                var batch = await conn.QueryFirstOrDefaultAsync<dynamic>(batchSql, new { WorkOrderId = workOrderId }, transaction);
                if (batch == null) throw new InvalidOperationException("Batch work order not found.");

                const string stageSql = "SELECT * FROM work_order_stages WHERE StageId = @StageId;";
                var currentStage = await conn.QueryFirstOrDefaultAsync<WorkOrderStage>(stageSql, new { StageId = currentStageId }, transaction);
                if (currentStage == null) throw new InvalidOperationException("Stage record not found.");

                // =========================================================================
                // 🌟 1. STAGE 1 (DISPENSING): Deduct Raw Materials (RM/PM)
                // =========================================================================
                if (currentStage.StageName.Contains("Dispensing", StringComparison.OrdinalIgnoreCase))
                {
                    const string getBomSql = "SELECT * FROM work_order_bom_items WHERE WorkOrderId = @WorkOrderId;";
                    var bomItems = await conn.QueryAsync<WorkOrderBomItem>(getBomSql, new { WorkOrderId = workOrderId }, transaction);

                    foreach (var item in bomItems)
                    {
                        // Inward negative to deduct
                        var ledgerEntry = new StockLedger
                        {
                            ProductId = item.RawMaterialProductId,
                            BatchNumber = (string)batch.BatchNumber,
                            MovementType = "Production_Consume",
                            Quantity = -Math.Abs(item.CalculatedQuantity),
                            Unit = item.Unit,
                            ReferenceDocument = (string)batch.BatchNumber,
                            Notes = $"Consumed in Batch {batch.BatchNumber} ({item.Phase})"
                        };

                        // Deduct from `Remaining Stock` column
                        const string deductRmStockSql = @"
                    UPDATE products 
                    SET `RemainingStock` = `RemainingStock` - @DeductQty 
                    WHERE ProductId = @ProductId;";
                        await conn.ExecuteAsync(deductRmStockSql, new { DeductQty = (int)Math.Ceiling(item.CalculatedQuantity), ProductId = item.RawMaterialProductId }, transaction);

                        // Record into stock_ledgers
                        const string insertLedgerSql = @"
                    INSERT INTO stock_ledgers (
                        ProductId, BatchNumber, MovementType, Quantity, Unit, ReferenceDocument, Notes, CreatedDate
                    ) VALUES (
                        @ProductId, @BatchNumber, @MovementType, @Quantity, @Unit, @ReferenceDocument, @Notes, NOW()
                    );";
                        await conn.ExecuteAsync(insertLedgerSql, ledgerEntry, transaction);
                    }
                }

                // 2. Mark Active Stage Completed
                const string completeStageSql = @"
            UPDATE work_order_stages 
            SET Status = 'Completed', CompletedAt = NOW(), OperatorRemarks = @OperatorRemarks 
            WHERE StageId = @StageId;";
                await conn.ExecuteAsync(completeStageSql, new { StageId = currentStageId, OperatorRemarks = operatorRemarks }, transaction);

                // 3. Find Next Sequential Stage
                const string nextStageSql = @"
            SELECT * FROM work_order_stages 
            WHERE WorkOrderId = @WorkOrderId 
              AND SequenceOrder > (SELECT SequenceOrder FROM work_order_stages WHERE StageId = @StageId)
            ORDER BY SequenceOrder ASC LIMIT 1;";
                var nextStage = await conn.QueryFirstOrDefaultAsync<WorkOrderStage>(nextStageSql, new { WorkOrderId = workOrderId, StageId = currentStageId }, transaction);

                if (nextStage != null)
                {
                    // Activate Next Stage
                    const string activateNextSql = @"
                UPDATE work_order_stages 
                SET Status = 'InProgress', StartedAt = NOW() 
                WHERE StageId = @NextStageId;";
                    await conn.ExecuteAsync(activateNextSql, new { NextStageId = nextStage.StageId }, transaction);

                    string stageCode = nextStage.StageName.Contains("Dispensing") ? "Dispensing" :
                                       nextStage.StageName.Contains("Mixing") ? "Mixing" :
                                       nextStage.StageName.Contains("Filling") ? "Filling" :
                                       nextStage.StageName.Contains("Packing") ? "Packing" :
                                       nextStage.StageName.Contains("QC") ? "QC" : nextStage.StageName;

                    const string updateHeaderStageSql = @"
                UPDATE production_work_orders SET CurrentStage = @CurrentStage WHERE WorkOrderId = @WorkOrderId;";
                    await conn.ExecuteAsync(updateHeaderStageSql, new { CurrentStage = stageCode, WorkOrderId = workOrderId }, transaction);
                }
                else
                {
                    // =========================================================================
                    // 🌟 4. FINAL STAGE (QC RELEASE / COMPLETED): Auto-Register & Credit FG
                    // =========================================================================
                    const string completeBatchSql = @"
                UPDATE production_work_orders SET CurrentStage = 'Completed' WHERE WorkOrderId = @WorkOrderId;";
                    await conn.ExecuteAsync(completeBatchSql, new { WorkOrderId = workOrderId }, transaction);

                    int finalProductId = batch.ProductId != null ? (int)batch.ProductId : 0;

                    // Step A: Find default Finished Good CategoryId from `categories` table
                    const string getFgCategoryIdSql = "SELECT Id FROM categories WHERE CategoryType = 1 LIMIT 1;";
                    int fgCategoryId = await conn.ExecuteScalarAsync<int>(getFgCategoryIdSql, transaction: transaction);

                    // Step B: Auto-Register into `products` table if not linked
                    if (finalProductId == 0)
                    {
                        const string checkExistingSql = "SELECT ProductId FROM products WHERE Name = @Name AND BrandName = @BrandName LIMIT 1;";
                        finalProductId = await conn.ExecuteScalarAsync<int>(checkExistingSql, new { Name = (string)batch.BrandName, BrandName = (string)batch.BrandName }, transaction);

                        if (finalProductId == 0)
                        {
                            const string insertProductSql = @"
                        INSERT INTO products (
                            Name, ShortName, SKU, Unit, CategoryId, 
                            Packaging, `InitialStock`, `RemainingStock`, 
                            SellingPrice, CostPrice, GSTPercent, HasBatchTracking, 
                            MfgDate, ExpiryDate, BrandName, CreatedAt
                        ) VALUES (
                            @Name, @ShortName, @SKU, 'Pcs', @CategoryId, 
                            @Packaging, 0, 0, 
                            @SellingPrice, 0.00, 18.00, 1, 
                            @MfgDate, @ExpiryDate, @BrandName, NOW()
                        );
                        SELECT LAST_INSERT_ID();";

                            finalProductId = await conn.ExecuteScalarAsync<int>(insertProductSql, new
                            {
                                Name = (string)batch.BrandName,
                                ShortName = (string)batch.BatchNumber,
                                SKU = (string)batch.BatchNumber,
                                CategoryId = fgCategoryId > 0 ? (int?)fgCategoryId : null,
                                Packaging = (string?)batch.PackagingType ?? "Std Pack",
                                SellingPrice = (decimal?)batch.UnitPrice ?? 0.00m,
                                MfgDate = (DateTime)batch.MfgDate,
                                ExpiryDate = (DateTime)batch.ExpiryDate,
                                BrandName = (string)batch.BrandName
                            }, transaction);
                        }

                        // Link newly created/found product to work order
                        await conn.ExecuteAsync("UPDATE production_work_orders SET ProductId = @ProductId WHERE WorkOrderId = @WorkOrderId;",
                            new { ProductId = finalProductId, WorkOrderId = workOrderId }, transaction);
                    }

                    // Step C: Increment `Remaining Stock` in `products`
                    int producedQty = (int)Math.Round((decimal)batch.BatchSize);
                    const string incrementFgStockSql = @"
                UPDATE products 
                SET `RemainingStock` = `RemainingStock` + @ProducedQty 
                WHERE ProductId = @ProductId;";
                    await conn.ExecuteAsync(incrementFgStockSql, new { ProducedQty = producedQty, ProductId = finalProductId }, transaction);

                    // Step D: Write Inward Ledger Entry
                    const string insertFgLedgerSql = @"
                INSERT INTO stock_ledgers (
                    ProductId, BatchNumber, MovementType, Quantity, Unit, ReferenceDocument, Notes, CreatedDate
                ) VALUES (
                    @ProductId, @BatchNumber, 'Production_Yield', @Quantity, 'Pcs', @ReferenceDocument, @Notes, NOW()
                );";
                    await conn.ExecuteAsync(insertFgLedgerSql, new
                    {
                        ProductId = finalProductId,
                        BatchNumber = (string)batch.BatchNumber,
                        Quantity = (decimal)batch.BatchSize,
                        ReferenceDocument = (string)batch.BatchNumber,
                        Notes = $"Production Output Yield for {batch.BrandName}"
                    }, transaction);

                    // Step E: Upsert into productbatches
                    const string upsertBatchLotSql = @"
                INSERT INTO productbatches (ProductId, BatchNumber, DivisionId, MfgDate, ExpiryDate, QuantityReceived, CreatedAt)
                VALUES (@ProductId, @BatchNumber, 1, @MfgDate, @ExpiryDate, @Quantity, NOW())
                ON DUPLICATE KEY UPDATE QuantityReceived = QuantityReceived + VALUES(QuantityReceived), CreatedAt = NOW();";
                    await conn.ExecuteAsync(upsertBatchLotSql, new
                    {
                        ProductId = finalProductId,
                        BatchNumber = (string)batch.BatchNumber,
                        MfgDate = (DateTime)batch.MfgDate,
                        ExpiryDate = (DateTime)batch.ExpiryDate,
                        Quantity = (decimal)batch.BatchSize
                    }, transaction);

                    // Step F: Auto-complete parent Service Line Order if all batches are done
                    const string checkParentSql = "SELECT COUNT(1) FROM production_work_orders WHERE OrderId = @OrderId AND CurrentStage != 'Completed';";
                    int pendingBatches = await conn.ExecuteScalarAsync<int>(checkParentSql, new { OrderId = (int)batch.OrderId }, transaction);

                    if (pendingBatches == 0)
                    {
                        await conn.ExecuteAsync("UPDATE service_orders SET OrderStatus = 'Completed', UpdatedAt = NOW() WHERE OrderId = @OrderId;",
                            new { OrderId = (int)batch.OrderId }, transaction);
                    }
                }

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public FlowDocument CreateBmrFlowDocument(ProductionWorkOrder batch, IEnumerable<WorkOrderBomItem> bomItems, double printableWidth = 793.7)
        {
            var doc = new FlowDocument
            {
                PagePadding = new Thickness(36), // ~0.5 inch standard margins
                PageWidth = printableWidth,
                ColumnWidth = double.PositiveInfinity, // 🌟 Fixes the multi-column wrapping bug!
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
            };

            double contentWidth = printableWidth - 72; // Width minus left/right margins

            // 1. HEADER SECTION
            var headerTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 12) };
            headerTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.65) });
            headerTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.35) });

            var headerRowGroup = new TableRowGroup();
            var headerRow = new TableRow();

            var titleCell = new TableCell(new Paragraph(new Bold(new Run("BATCH MANUFACTURING RECORD (BMR)")) { FontSize = 15, Foreground = new SolidColorBrush(Color.FromRgb(23, 148, 161)) }) { Margin = new Thickness(0) });
            titleCell.Blocks.Add(new Paragraph(new Run("Contract Manufacturing & Processing Travelers Sheet")) { FontSize = 9, Foreground = Brushes.Gray, Margin = new Thickness(0, 2, 0, 0) });

            var docMetaCell = new TableCell(new Paragraph(new Run($"Doc Ref: BMR-{batch.BatchNumber}")) { TextAlignment = TextAlignment.Right, FontSize = 9.5, Margin = new Thickness(0) });
            docMetaCell.Blocks.Add(new Paragraph(new Run($"Printed: {DateTime.Now:dd MMM yyyy HH:mm}")) { TextAlignment = TextAlignment.Right, FontSize = 8.5, Foreground = Brushes.Gray, Margin = new Thickness(0, 2, 0, 0) });

            headerRow.Cells.Add(titleCell);
            headerRow.Cells.Add(docMetaCell);
            headerRowGroup.Rows.Add(headerRow);
            headerTable.RowGroups.Add(headerRowGroup);
            doc.Blocks.Add(headerTable);

            // 2. BATCH METADATA GRID
            var metaTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 14), BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)), BorderThickness = new Thickness(1, 1, 0, 0) };
            metaTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.18) });
            metaTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.32) });
            metaTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.18) });
            metaTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.32) });

            var metaGroup = new TableRowGroup();
            AddMetaRow(metaGroup, "Batch Number:", batch.BatchNumber, "Customer / Client:", batch.CustomerName);
            AddMetaRow(metaGroup, "Brand Name:", batch.BrandName, "Product / SKU:", batch.ProductName);
            AddMetaRow(metaGroup, "Batch Target:", $"{batch.BatchSize:N0} Units", "Order Ref #:", $"SO-{batch.OrderId}");
            AddMetaRow(metaGroup, "Mfg Date:", batch.MfgDate.ToString("dd MMM yyyy"), "Exp Date:", batch.ExpiryDate.ToString("dd MMM yyyy"));
            metaTable.RowGroups.Add(metaGroup);
            doc.Blocks.Add(metaTable);

            // 3. BILL OF MATERIALS (BOM) DISPENSING TABLE
            doc.Blocks.Add(new Paragraph(new Bold(new Run("1. Raw Material Dispensing & Bill of Materials (BOM)")) { FontSize = 11 }) { Margin = new Thickness(0, 0, 0, 4) });

            var bomTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 14), BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)), BorderThickness = new Thickness(1, 1, 0, 0) };
            bomTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.12) }); // Phase
            bomTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.38) }); // RM Name
            bomTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.12) }); // Ratio %
            bomTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.15) }); // Target Qty
            bomTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.13) }); // Actual Weighed
            bomTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.10) }); // Sign

            var bomHeaderGroup = new TableRowGroup();
            var bHeaderRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)) };
            bHeaderRow.Cells.Add(CreateCell(new Bold(new Run("Phase")), isHeader: true));
            bHeaderRow.Cells.Add(CreateCell(new Bold(new Run("Raw Material Name")), isHeader: true));
            bHeaderRow.Cells.Add(CreateCell(new Bold(new Run("Ratio %")), isHeader: true));
            bHeaderRow.Cells.Add(CreateCell(new Bold(new Run("Target Weight")), isHeader: true));
            bHeaderRow.Cells.Add(CreateCell(new Bold(new Run("Actual Weighed")), isHeader: true));
            bHeaderRow.Cells.Add(CreateCell(new Bold(new Run("Disp. Sign")), isHeader: true));
            bomHeaderGroup.Rows.Add(bHeaderRow);

            foreach (var item in bomItems)
            {
                var row = new TableRow();
                row.Cells.Add(CreateCell(new Run(item.Phase)));
                row.Cells.Add(CreateCell(new Bold(new Run(item.RawMaterialName))));
                row.Cells.Add(CreateCell(new Run($"{item.PercentageValue:N2}%")));
                row.Cells.Add(CreateCell(new Bold(new Run($"{item.CalculatedQuantity:N3} {item.Unit}"))));
                row.Cells.Add(CreateCell(new Run("________")));
                row.Cells.Add(CreateCell(new Run("______")));
                bomHeaderGroup.Rows.Add(row);
            }

            bomTable.RowGroups.Add(bomHeaderGroup);
            doc.Blocks.Add(bomTable);

            // 4. PROCESS CHECKPOINTS & STAGES
            doc.Blocks.Add(new Paragraph(new Bold(new Run("2. Process Checkpoints & Quality Verification")) { FontSize = 11 }) { Margin = new Thickness(0, 0, 0, 4) });

            var stageTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 16), BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)), BorderThickness = new Thickness(1, 1, 0, 0) };
            stageTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.08) }); // Step
            stageTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.35) }); // Stage
            stageTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.15) }); // Status
            stageTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.28) }); // Remarks / Readings
            stageTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.14) }); // Sign & Date

            var stageHeaderGroup = new TableRowGroup();
            var sHeaderRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)) };
            sHeaderRow.Cells.Add(CreateCell(new Bold(new Run("Step")), isHeader: true));
            sHeaderRow.Cells.Add(CreateCell(new Bold(new Run("Process Stage")), isHeader: true));
            sHeaderRow.Cells.Add(CreateCell(new Bold(new Run("Status")), isHeader: true));
            sHeaderRow.Cells.Add(CreateCell(new Bold(new Run("Operator Remarks / Temp / pH")), isHeader: true));
            sHeaderRow.Cells.Add(CreateCell(new Bold(new Run("Sign & Date")), isHeader: true));
            stageHeaderGroup.Rows.Add(sHeaderRow);

            if (batch.Stages != null && batch.Stages.Any())
            {
                foreach (var stage in batch.Stages)
                {
                    var row = new TableRow();
                    row.Cells.Add(CreateCell(new Run(stage.SequenceOrder.ToString())));
                    row.Cells.Add(CreateCell(new Bold(new Run(stage.StageName))));
                    row.Cells.Add(CreateCell(new Run(stage.Status)));
                    row.Cells.Add(CreateCell(new Run(string.IsNullOrWhiteSpace(stage.OperatorRemarks) ? "________________" : stage.OperatorRemarks)));
                    row.Cells.Add(CreateCell(new Run(stage.CompletedAt.HasValue ? stage.CompletedAt.Value.ToString("dd/MM/yy") : "________")));
                    stageHeaderGroup.Rows.Add(row);
                }
            }

            stageTable.RowGroups.Add(stageHeaderGroup);
            doc.Blocks.Add(stageTable);

            // 5. SIGNATURE & RELEASE BLOCK
            var signTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 10, 0, 0) };
            signTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth / 3) });
            signTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth / 3) });
            signTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth / 3) });

            var signGroup = new TableRowGroup();
            var signRow = new TableRow();
            signRow.Cells.Add(CreateSignBlock("Production Chemist", "Dispensed & Processed By"));
            signRow.Cells.Add(CreateSignBlock("Plant Supervisor", "Checked & Verified By"));
            signRow.Cells.Add(CreateSignBlock("Quality Assurance (QA)", "Tested & Batch Released By"));
            signGroup.Rows.Add(signRow);
            signTable.RowGroups.Add(signGroup);
            doc.Blocks.Add(signTable);

            return doc;
        }

        private static void AddMetaRow(TableRowGroup group, string k1, string v1, string k2, string v2)
        {
            var row = new TableRow();
            row.Cells.Add(CreateCell(new Bold(new Run(k1)), isHeader: true));
            row.Cells.Add(CreateCell(new Run(v1 ?? "—")));
            row.Cells.Add(CreateCell(new Bold(new Run(k2)), isHeader: true));
            row.Cells.Add(CreateCell(new Run(v2 ?? "—")));
            group.Rows.Add(row);
        }

        private static TableCell CreateCell(Inline inline, bool isHeader = false)
        {
            var cell = new TableCell(new Paragraph(inline) { Margin = new Thickness(0) })
            {
                Padding = new Thickness(5, 3, 5, 3),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(0, 0, 1, 1)
            };
            if (isHeader)
                cell.Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
            return cell;
        }

        private static TableCell CreateSignBlock(string title, string role)
        {
            var cell = new TableCell();
            cell.Blocks.Add(new Paragraph(new Run("Signature: ____________________")) { Margin = new Thickness(0, 0, 0, 3), FontSize = 9.5 });
            cell.Blocks.Add(new Paragraph(new Bold(new Run(title))) { Margin = new Thickness(0, 0, 0, 1), FontSize = 10 });
            cell.Blocks.Add(new Paragraph(new Run(role)) { FontSize = 8.5, Foreground = Brushes.Gray, Margin = new Thickness(0) });
            return cell;
        }
    }
}
