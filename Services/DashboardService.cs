using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tijori.Data;
using Tijori.Models;

namespace Tijori.Services
{
    public class DashboardService
    {
        private readonly CrmDbContext _context;

        public DashboardService(CrmDbContext context)
        {
            _context = context;
        }

        public async Task<ExecutiveDashboardData> GetExecutiveDashboardDataAsync(DashboardFilter? filter = null)
        {
            var data = new ExecutiveDashboardData();
            using var db = _context.CreateConnection();

            string sql = @"
                -- 1. FINANCIALS
                    SELECT 
                        -- A. Total Invoiced Sales (Billed Value from Orders)
                        IFNULL((SELECT SUM(TotalAmount) FROM orders), 0) AS TotalInvoicedSales,
    
                        -- B. Total Cost of Invoiced Goods
                        IFNULL((SELECT SUM(TotalCostAmount) FROM orders), 0) AS TotalCostAmount,
    
                        -- C. Proforma Pipeline (Active Quotations)
                        IFNULL((SELECT SUM(GrandTotal) FROM proformas WHERE ProformaStatus = 'Quotation'), 0) AS ProformaPipelineValue,
    
                        -- D. Total Business Realized (Actual Cash Inflow from Payments Table)
                        IFNULL((SELECT SUM(AmountReceived) FROM payments), 0) AS TotalBusinessRealized,
    
                        -- E. Unpaid / Credit Risk (Total Invoiced minus Actual Cash Received)
                        GREATEST(0, IFNULL((SELECT SUM(TotalAmount) FROM orders), 0) - IFNULL((SELECT SUM(AmountReceived) FROM payments), 0)) AS TotalUnpaidCreditRisk,
    
                        -- F. Committed Procurement Payables
                        IFNULL((SELECT SUM(TotalAmount) FROM purchaseorders WHERE OrderStatus NOT IN ('Completed', 'Cancelled')), 0) AS VendorPoOutstandingValue;

                -- 2. SALES PIPELINE & RETENTION
                SELECT 
                    (SELECT COUNT(*) FROM leads) AS AllLeads,
                    (SELECT COUNT(*) FROM leads WHERE Status = 'New') AS NewLeads,
                    (SELECT COUNT(*) FROM leads WHERE Status = 'Followup') AS FollowupLeads,
                    (SELECT COUNT(*) FROM leads l WHERE Status = 'Followup' 
                        AND (SELECT MAX(LogDate) FROM leadhistory WHERE LeadId = l.LeadId) < DATE_SUB(NOW(), INTERVAL 30 DAY)) AS NoFollowupLeads30Days,
                    (SELECT COUNT(*) FROM leads WHERE Status = 'Dead') AS DeadLeads,
                    (SELECT COUNT(*) FROM leads WHERE Status = 'Matured') AS ActiveCustomers,
                    (SELECT COUNT(DISTINCT l.LeadId) FROM leads l WHERE l.Status = 'Matured'
                        AND (SELECT GREATEST(
                            l.CreatedAt,
                            IFNULL((SELECT MAX(LogDate) FROM leadhistory WHERE LeadId = l.LeadId), '1900-01-01'),
                            IFNULL((SELECT MAX(OrderDate) FROM orders WHERE LeadId = l.LeadId), '1900-01-01'),
                            IFNULL((SELECT MAX(PaymentDate) FROM payments WHERE LeadId = l.LeadId), '1900-01-01')
                        )) < DATE_SUB(NOW(), INTERVAL 7 DAY)) AS NoUpdation7Days,
                    (SELECT COUNT(*) FROM leads l WHERE Status = 'Matured' 
                        AND (SELECT COUNT(*) FROM orders WHERE LeadId = l.LeadId) <= 1) AS NoRepeatOrders,
                    (SELECT COUNT(*) FROM leads l WHERE Status = 'Matured' 
                        AND (SELECT MAX(OrderDate) FROM orders WHERE LeadId = l.LeadId) < DATE_SUB(NOW(), INTERVAL 30 DAY)) AS Dormant30DaysNoOrders,
                    (SELECT COUNT(*) FROM (
                        SELECT l.LeadId FROM leads l
                        LEFT JOIN orders o ON l.LeadId = o.LeadId 
                            AND o.OrderDate >= DATE_ADD(LAST_DAY(DATE_SUB(NOW(), INTERVAL 2 MONTH)), INTERVAL 1 DAY)
                            AND o.OrderDate <= LAST_DAY(DATE_SUB(NOW(), INTERVAL 1 MONTH))
                        WHERE l.Status = 'Matured' AND IFNULL(l.MonthlyTarget, 0) > 0
                        GROUP BY l.LeadId, l.MonthlyTarget
                        HAVING IFNULL(SUM(o.TotalAmount), 0) < l.MonthlyTarget
                    ) AS BelowTargetTrack) AS BelowTargetCustomers;

                -- 3. ORDER METRICS
                SELECT 
                    COUNT(*) AS TotalOrders,
                    COUNT(CASE WHEN OrderType = 'New' OR OrderType = 'Sale' THEN 1 END) AS TotalNewOrders,
                    IFNULL((SELECT SUM(RepeatCount) FROM (
                        SELECT COUNT(OrderId) - 1 AS RepeatCount FROM orders GROUP BY LeadId HAVING COUNT(OrderId) > 1
                    ) AS r), 0) AS TotalRepeatedOrders,
                    COUNT(CASE WHEN PaymentStatus = 'Unpaid' THEN 1 END) AS TotalUnpaidOrders,
                    COUNT(CASE WHEN PaymentStatus = 'Partially Paid' THEN 1 END) AS TotalPartialPaidOrders
                FROM orders;

                -- 4. TERRITORY HEALTH
                SELECT 
                    COUNT(DISTINCT CASE WHEN Status = 'Matured' AND Pincode IS NOT NULL AND Pincode != '' THEN Pincode END) AS CoveredPincodes,
                    COUNT(DISTINCT CASE WHEN Status != 'Matured' AND Pincode IS NOT NULL AND Pincode != '' 
                                        AND Pincode NOT IN (SELECT DISTINCT Pincode FROM leads WHERE Status = 'Matured' AND Pincode IS NOT NULL) THEN Pincode END) AS VacantPincodes,
                    COUNT(DISTINCT District) AS TotalDistinctDistricts
                FROM leads;

                -- 4. 3P MANUFACTURING & BATCHES
                    SELECT 
                        IFNULL((SELECT COUNT(*) FROM service_orders WHERE OrderStatus NOT IN ('Completed', 'Cancelled')), 0) AS Active3POrders,
                        IFNULL((SELECT SUM(GrandTotalAmount) FROM service_orders WHERE OrderStatus NOT IN ('Completed', 'Cancelled')), 0) AS Total3PContractValue,
    
                        -- Running Batches: Any batch not completed or dispatched
                        IFNULL((SELECT COUNT(*) FROM production_work_orders 
                                WHERE CurrentStage NOT IN ('Completed', 'Dispatched', 'QC Lab Testing & Release')), 0) AS RunningBatches,
    
                        -- In Formulation: Covers Dispensing (Stage 1) & Phase Mixing (Stage 2)
                        IFNULL((SELECT COUNT(*) FROM production_work_orders 
                                WHERE CurrentStage IN ('Dispensing', 'Mixing', 'Material Dispensing & Weighing', 'Phase Mixing & Formulation', 'Formulation')), 0) AS BatchesInFormulation,
    
                        -- QA / Lab Hold: Covers Stage 5 (QC Lab Testing) or QA Hold
                        IFNULL((SELECT COUNT(*) FROM production_work_orders 
                                WHERE CurrentStage IN ('QC', 'QC Lab Testing & Release', 'QA Hold', 'Testing')), 0) AS BatchesInQaHold,
    
                        -- In Packaging: Covers Primary Filling (Stage 3) & Secondary Packing (Stage 4)
                        IFNULL((SELECT COUNT(*) FROM production_work_orders 
                                WHERE CurrentStage IN ('Filling', 'Packing', 'Primary Filling & Packing', 'Secondary Packing & Labeling', 'Packaging')), 0) AS BatchesInPackaging,
    
                        -- Ready for Dispatch: Completed batches awaiting transport/invoicing
                        IFNULL((SELECT COUNT(*) FROM production_work_orders 
                                WHERE CurrentStage IN ('Completed', 'Ready for Dispatch')), 0) AS ReadyForDispatch,
    
                        -- Delayed Batches (SLA Alert)
                        IFNULL((SELECT COUNT(*) FROM production_work_orders pwo 
                                JOIN service_orders so ON pwo.OrderId = so.OrderId 
                                WHERE so.DeliveryDueDate < CURDATE() 
                                  AND pwo.CurrentStage NOT IN ('Completed', 'Dispatched')), 0) AS DelayedBatchesAlert;

                -- 6. INVENTORY & SUPPLY
                SELECT 
                    (SELECT COUNT(DISTINCT CategoryId) FROM products WHERE CategoryId IS NOT NULL) AS TotalCategoriesUsed,
                    (SELECT COUNT(*) FROM products) AS TotalProducts,
                    (SELECT COUNT(*) FROM products WHERE CreatedAt >= DATE_SUB(NOW(), INTERVAL 30 DAY)) AS TotalNewProducts,
                    (SELECT COUNT(*) FROM (
                        SELECT ProductId FROM orderitems GROUP BY ProductId HAVING SUM(Quantity) >= 50
                    ) AS FastTrack) AS FastMovingProducts,
                    (SELECT COUNT(*) FROM (
                        SELECT p.ProductId FROM products p 
                        LEFT JOIN orderitems oi ON p.ProductId = oi.ProductId 
                        GROUP BY p.ProductId HAVING IFNULL(SUM(oi.Quantity), 0) < 5
                    ) AS SlowTrack) AS SlowMovingProducts,
                    (SELECT COUNT(*) FROM products WHERE RemainingStock <= SKU AND SKU > 0) AS NearSkuAlertCount,
                    (SELECT COUNT(DISTINCT ProductId) FROM productbatches 
                     WHERE ExpiryDate IS NOT NULL AND ExpiryDate >= NOW() AND ExpiryDate <= DATE_ADD(NOW(), INTERVAL 3 MONTH)) AS NearExpiryBatchCount,
                    (SELECT COUNT(DISTINCT prev.ProductId) FROM orderitems prev
                     JOIN orders oprev ON prev.OrderId = oprev.OrderId
                        AND oprev.OrderDate >= DATE_ADD(LAST_DAY(DATE_SUB(NOW(), INTERVAL 3 MONTH)), INTERVAL 1 DAY)
                        AND oprev.OrderDate <= LAST_DAY(DATE_SUB(NOW(), INTERVAL 2 MONTH))
                     WHERE prev.ProductId NOT IN (
                         SELECT DISTINCT curr.ProductId FROM orderitems curr
                         JOIN orders ocurr ON curr.OrderId = ocurr.OrderId
                         WHERE ocurr.OrderDate >= DATE_ADD(LAST_DAY(DATE_SUB(NOW(), INTERVAL 2 MONTH)), INTERVAL 1 DAY)
                           AND ocurr.OrderDate <= LAST_DAY(DATE_SUB(NOW(), INTERVAL 1 MONTH))
                     )) AS SkippedProductsCount,
                    (SELECT COUNT(*) FROM purchaseorders WHERE OrderStatus NOT IN ('Completed', 'Cancelled')) AS OpenVendorPurchaseOrders,
                    (SELECT COUNT(*) FROM vendors WHERE Status = 'Active') AS ActiveVendorsCount;

                -- 7. STAFF TASKS
                SELECT 
                    (SELECT COUNT(*) FROM systemtoastsqueue WHERE NotificationStatus = 'Pending') AS TotalOpenTasks,
                    (SELECT COUNT(*) FROM systemtoastsqueue WHERE NotificationStatus = 'Pending' AND ScheduleTime <= NOW()) AS OverdueTasksToday;

                -- 8. SIDEBAR: REMINDERS
                SELECT 'New' AS `Key`, COUNT(*) AS `Value` 
                FROM orders 
                WHERE (PaymentStatus = 'Unpaid' OR PaymentStatus = 'Partially Paid')
                  AND (OrderType = 'New' OR OrderType = 'Sale')
                UNION ALL 
                SELECT 'Repeat' AS `Key`, COUNT(*) AS `Value` 
                FROM orders 
                WHERE (PaymentStatus = 'Unpaid' OR PaymentStatus = 'Partially Paid')
                  AND (OrderType != 'New' AND OrderType != 'Sale');

                -- 9. SIDEBAR: FOLLOWUP STAGES
                SELECT 'All FollowUps' AS `Key`, COUNT(*) AS `Value` FROM leads WHERE Status = 'Followup'
                UNION ALL
                SELECT s.StatusesName AS `Key`, COUNT(l.LeadId) AS `Value`
                FROM leadstatuses s
                LEFT JOIN leads l ON s.Id = l.StatusId AND l.Status = 'Followup'
                GROUP BY s.Id, s.StatusesName
                ORDER BY `Key` ASC;

                -- 10. SIDEBAR: MATURE STAGES
                SELECT 'All Matured' AS `Key`, COUNT(*) AS `Value` FROM leads WHERE Status = 'Matured'
                UNION ALL
                SELECT m.MatureStagesName AS `Key`, COUNT(l.LeadId) AS `Value`
                FROM maturestages m
                LEFT JOIN leads l ON m.Id = l.MatureStageId AND l.Status = 'Matured'
                GROUP BY m.Id, m.MatureStagesName
                ORDER BY `Key` ASC;

                -- 11. SIDEBAR: LEAD LABELS
                SELECT 'All Labels' AS `Key`, COUNT(*) AS `Value` FROM leadlabels
                UNION ALL
                SELECT 
                    master.LabelsName AS `Key`,
                    (
                        SELECT COUNT(*) FROM leads l
                        WHERE l.LabelsJson IS NOT NULL AND l.LabelsJson != '' AND l.LabelsJson != '[]'
                          AND JSON_CONTAINS(l.LabelsJson, JSON_QUOTE(master.LabelsName))
                    ) AS `Value`
                FROM leadlabels master
                WHERE master.LabelsName IS NOT NULL AND master.LabelsName != ''
                ORDER BY `Key` ASC;
            ";

            using var multi = await db.QueryMultipleAsync(sql);

            data.Financials = await multi.ReadSingleOrDefaultAsync<FinancialMetricsDto>() ?? new();
            data.SalesPipeline = await multi.ReadSingleOrDefaultAsync<SalesPipelineDto>() ?? new();
            data.Orders = await multi.ReadSingleOrDefaultAsync<OrderMetricsDto>() ?? new();
            data.Territory = await multi.ReadSingleOrDefaultAsync<TerritoryHealthDto>() ?? new();
            data.Manufacturing = await multi.ReadSingleOrDefaultAsync<ManufacturingBatchDto>() ?? new();
            data.Inventory = await multi.ReadSingleOrDefaultAsync<InventorySupplyDto>() ?? new();
            data.StaffTasks = await multi.ReadSingleOrDefaultAsync<StaffOperationsDto>() ?? new();

            data.Sidebar.Reminders = (await multi.ReadAsync<KeyValuePair<string, int>>()).ToList();
            data.Sidebar.FollowupStages = (await multi.ReadAsync<KeyValuePair<string, int>>()).ToList();
            data.Sidebar.MatureStages = (await multi.ReadAsync<KeyValuePair<string, int>>()).ToList();
            data.Sidebar.LeadLabels = (await multi.ReadAsync<KeyValuePair<string, int>>()).ToList();

            return data;
        }
    }
}
