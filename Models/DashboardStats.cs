using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public class DashboardStats
    {
        public int AllLeads { get; set; }
        public int NewLeads { get; set; }
        public int FollowupLeads { get; set; }
        public int NoFollowupLeads { get; set; }
        public int NoRepeatOrder { get; set; }
        public int NoOrder { get; set; }
        public int Dead { get; set; }
        public int Customers { get; set; }
        public int NoUpdation7Days { get; set; }
        public int BelowTarget { get; set; }
        public decimal TotalBusiness { get; set; }

        public double NewLeadsPercentage => AllLeads > 0 ? Math.Round((double)NewLeads / AllLeads * 100, 1) : 0;
        public double FollowupLeadsPercentage => AllLeads > 0 ? Math.Round((double)FollowupLeads / AllLeads * 100, 1) : 0;
        public double NoFollowupLeadsPercentage => AllLeads > 0 ? Math.Round((double)NoFollowupLeads / AllLeads * 100, 1) : 0;
        public double DeadPercentage => AllLeads > 0 ? Math.Round((double)Dead / AllLeads * 100, 1) : 0;
        public double CustomersPercentage => AllLeads > 0 ? Math.Round((double)Customers / AllLeads * 100, 1) : 0;
    }
}
