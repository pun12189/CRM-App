using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models.Enums
{
    public enum DashboardTargetView
    {
        // Leads Targets
        AllLeads,
        OpenLeads,
        FollowupLeads,
        NoFollowupLeads,
        DeadLeads,

        // Customers Targets
        Customers,
        NoUpdation7Days,
        NoRepeatOrders,
        NoOrders30Days,
        BelowTargetCustomers,

        // Product Targets
        ProductsList,
        CategoriesList,
        NearSkuProducts,
        NearExpiryBatches,
        SkippedProducts,
        NewProducts,
        FastMovingProducts,
        SlowMovingProducts,

        // Order Targets
        AllOrders,
        NewOrders,
        RepeatedOrders,
        UnpaidOrders,
        PartiallyPaidOrders,

        // ====================================================================
        // 🌟 NEW MISSING NAVIGATION TARGETS
        // ====================================================================
        TasksView,
        OverdueTasksView,
        OccupiedLocationsView,
        VacantLocationsView,
        ProformaView,
        PurchaseOrdersView,
        VendorsView,
        ServiceOrdersView,
        BatchTrackerView,
        BatchQaHoldView
    }
}
