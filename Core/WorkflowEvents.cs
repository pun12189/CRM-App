using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Core
{
    public static class WorkflowEvents
    {
        // --- 1. Action-Driven / Instant Triggers ---
        public const string LeadCreate = "Lead Create";
        public const string LeadTransfer = "Lead Transfer";
        public const string LeadMatured = "Lead Matured";
        public const string RepeatOrders = "Repeat Orders";
        public const string EditOrder = "Edit Order";

        public const string NewProforma = "New Proforma";
        public const string EditProforma = "Edit Proforma";
        
        
        
        public const string FollowupStages = "Followup stages";
        public const string MatureStages = "Mature stages";
        public const string BrandMissed = "Brand Missed";
        public const string BalancePayment = "Balance Payment";

        // --- 2. Time-Based / Scheduled Triggers ---
        public const string NoUpdationSince = "No updation since";
        public const string NoOrderSince = "No order since";
        public const string ProductServiceRenewal = "Product/Service Renewal";
        public const string Birthday = "Birthday";
        public const string Anniversary = "Anniversary";
    }
}
