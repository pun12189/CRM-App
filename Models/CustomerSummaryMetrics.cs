using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public class CustomerSummaryMetrics
    {
        // Header Info
        public string CustomerSince { get; set; }
        public decimal Last3MonthsBilling { get; set; }
        public string LastOrderDate { get; set; }
        public decimal OutstandingAmount { get; set; }

        // Performance Metrics
        public decimal MonthlyBusiness { get; set; }
        public decimal Last3MonthsBusiness { get; set; }
        public decimal OverallBusiness { get; set; }
    }
}
