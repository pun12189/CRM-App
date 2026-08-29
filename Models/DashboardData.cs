using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public class ExecutiveDashboardData
    {
        public FinancialMetricsDto Financials { get; set; } = new();
        public SalesPipelineDto SalesPipeline { get; set; } = new();
        public OrderMetricsDto Orders { get; set; } = new();
        public TerritoryHealthDto Territory { get; set; } = new();
        public ManufacturingBatchDto Manufacturing { get; set; } = new();
        public InventorySupplyDto Inventory { get; set; } = new();
        public StaffOperationsDto StaffTasks { get; set; } = new();
        public SidebarSummaryDto Sidebar { get; set; } = new();
    }

    public class FinancialMetricsDto
    {
        public decimal TotalInvoicedSales { get; set; }
        public decimal TotalCostAmount { get; set; }
        public decimal GrossMarginPercentage => TotalInvoicedSales > 0
            ? Math.Round(((TotalInvoicedSales - TotalCostAmount) / TotalInvoicedSales) * 100, 2)
            : 0;
        public decimal ProformaPipelineValue { get; set; }
        public decimal TotalUnpaidCreditRisk { get; set; }
        public decimal VendorPoOutstandingValue { get; set; }
        public decimal TotalBusinessRealized { get; set; }
    }

    public class SalesPipelineDto
    {
        public int AllLeads { get; set; }
        public int NewLeads { get; set; }
        public int FollowupLeads { get; set; }
        public int NoFollowupLeads30Days { get; set; }
        public int DeadLeads { get; set; }

        // Percentage helpers for UI badges
        public double NewLeadsPercentage => AllLeads > 0 ? Math.Round((double)NewLeads / AllLeads * 100, 1) : 0;
        public double FollowupLeadsPercentage => AllLeads > 0 ? Math.Round((double)FollowupLeads / AllLeads * 100, 1) : 0;
        public double NoFollowupLeadsPercentage => AllLeads > 0 ? Math.Round((double)NoFollowupLeads30Days / AllLeads * 100, 1) : 0;
        public double DeadPercentage => AllLeads > 0 ? Math.Round((double)DeadLeads / AllLeads * 100, 1) : 0;

        // Customer Retention Metrics
        public int ActiveCustomers { get; set; }
        public double CustomersPercentage => AllLeads > 0 ? Math.Round((double)ActiveCustomers / AllLeads * 100, 1) : 0;
        public int NoUpdation7Days { get; set; }
        public int NoRepeatOrders { get; set; }
        public int Dormant30DaysNoOrders { get; set; }
        public int BelowTargetCustomers { get; set; }
    }

    public class OrderMetricsDto
    {
        public int TotalOrders { get; set; }
        public int TotalNewOrders { get; set; }
        public int TotalRepeatedOrders { get; set; }
        public int TotalUnpaidOrders { get; set; }
        public int TotalPartialPaidOrders { get; set; }
    }

    public class TerritoryHealthDto
    {
        public int CoveredPincodes { get; set; }
        public int VacantPincodes { get; set; }
        public int TotalDistinctDistricts { get; set; }
    }

    public class ManufacturingBatchDto
    {
        public int Active3POrders { get; set; }
        public decimal Total3PContractValue { get; set; }
        public int RunningBatches { get; set; }
        public int BatchesInFormulation { get; set; }
        public int BatchesInQaHold { get; set; }
        public int BatchesInPackaging { get; set; }
        public int ReadyForDispatch { get; set; }
        public int DelayedBatchesAlert { get; set; }
    }

    public class InventorySupplyDto
    {
        public int TotalCategoriesUsed { get; set; }
        public int TotalProducts { get; set; }
        public int TotalNewProducts { get; set; }
        public int FastMovingProducts { get; set; }
        public int SlowMovingProducts { get; set; }
        public int NearSkuAlertCount { get; set; }
        public int NearExpiryBatchCount { get; set; }
        public int SkippedProductsCount { get; set; }
        public int OpenVendorPurchaseOrders { get; set; }
        public int ActiveVendorsCount { get; set; }
    }

    public class StaffOperationsDto
    {
        public int TotalOpenTasks { get; set; }
        public int OverdueTasksToday { get; set; }
    }

    public class SidebarSummaryDto
    {
        public List<KeyValuePair<string, int>> Reminders { get; set; } = new();
        public List<KeyValuePair<string, int>> FollowupStages { get; set; } = new();
        public List<KeyValuePair<string, int>> MatureStages { get; set; } = new();
        public List<KeyValuePair<string, int>> LeadLabels { get; set; } = new();
    }
}
