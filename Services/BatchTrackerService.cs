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
    public class BatchTrackerService
    {
        private readonly CrmDbContext _context;

        public BatchTrackerService(CrmDbContext context)
        {
            _context = context;
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
                // 1. Mark current stage completed
                const string completeCurrentStageSql = @"
                    UPDATE work_order_stages 
                    SET Status = 'Completed', 
                        CompletedAt = NOW(), 
                        OperatorRemarks = @OperatorRemarks 
                    WHERE StageId = @StageId;";
                await conn.ExecuteAsync(completeCurrentStageSql, new { StageId = currentStageId, OperatorRemarks = operatorRemarks }, transaction);

                // 2. Find next sequential stage
                const string getNextStageSql = @"
                    SELECT * FROM work_order_stages 
                    WHERE WorkOrderId = @WorkOrderId 
                      AND SequenceOrder > (SELECT SequenceOrder FROM work_order_stages WHERE StageId = @StageId)
                    ORDER BY SequenceOrder ASC 
                    LIMIT 1;";
                var nextStage = await conn.QueryFirstOrDefaultAsync<WorkOrderStage>(getNextStageSql, new { WorkOrderId = workOrderId, StageId = currentStageId }, transaction);

                if (nextStage != null)
                {
                    // Activate next stage
                    const string activateNextSql = @"
                        UPDATE work_order_stages 
                        SET Status = 'InProgress', StartedAt = NOW() 
                        WHERE StageId = @NextStageId;";
                    await conn.ExecuteAsync(activateNextSql, new { NextStageId = nextStage.StageId }, transaction);

                    // Update main header current stage name
                    string stageCode = nextStage.StageName.Contains("Dispensing") ? "Dispensing" :
                                       nextStage.StageName.Contains("Mixing") ? "Mixing" :
                                       nextStage.StageName.Contains("Filling") ? "Filling" :
                                       nextStage.StageName.Contains("Packing") ? "Packing" :
                                       nextStage.StageName.Contains("QC") ? "QC" : nextStage.StageName;

                    const string updateHeaderStageSql = @"
                        UPDATE production_work_orders 
                        SET CurrentStage = @CurrentStage 
                        WHERE WorkOrderId = @WorkOrderId;";
                    await conn.ExecuteAsync(updateHeaderStageSql, new { CurrentStage = stageCode, WorkOrderId = workOrderId }, transaction);
                }
                else
                {
                    // All stages finished -> Mark batch as Completed
                    const string markBatchCompletedSql = @"
                        UPDATE production_work_orders 
                        SET CurrentStage = 'Completed' 
                        WHERE WorkOrderId = @WorkOrderId;";
                    await conn.ExecuteAsync(markBatchCompletedSql, new { WorkOrderId = workOrderId }, transaction);

                    // Check if all batches for parent order are completed
                    const string checkParentOrderSql = @"
                        SELECT OrderId FROM production_work_orders WHERE WorkOrderId = @WorkOrderId;";
                    int parentOrderId = await conn.ExecuteScalarAsync<int>(checkParentOrderSql, new { WorkOrderId = workOrderId }, transaction);

                    const string countPendingBatchesSql = @"
                        SELECT COUNT(1) FROM production_work_orders 
                        WHERE OrderId = @OrderId AND CurrentStage != 'Completed';";
                    int pendingCount = await conn.ExecuteScalarAsync<int>(countPendingBatchesSql, new { OrderId = parentOrderId }, transaction);

                    if (pendingCount == 0)
                    {
                        const string completeOrderSql = @"
                            UPDATE service_orders SET OrderStatus = 'Completed', UpdatedAt = NOW() 
                            WHERE OrderId = @OrderId;";
                        await conn.ExecuteAsync(completeOrderSql, new { OrderId = parentOrderId }, transaction);
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
    }
}
