using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models.Enums
{
    public enum DashboardTargetView
    {
        AllLeads,
        OpenLeads,
        FollowupLeads,
        NoFollowupLeads,
        DeadLeads,
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
        PartiallyPaidOrders
    }
}
