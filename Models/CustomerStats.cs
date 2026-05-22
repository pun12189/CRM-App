using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public class CustomerStats
    {
        public int TotalCustomers { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalFirstOrders { get; set; }
        public decimal TotalOtherOrders { get; set; }
        public decimal TotalFirstOrderAmountPaid { get; set; }
        public decimal TotalFirstOrderOutstanding { get; set; }
        public decimal TotalOtherOrderAmountPaid { get; set; }
        public decimal TotalOtherOrderOutstanding { get; set; }
        public decimal TotalBusiness { get; set; }
        public decimal TotalOutstanding { get; set; }

        private static readonly CultureInfo cultureInfo = new CultureInfo("en-US");

        public string SummaryDisplayText => $"{TotalCustomers} | " +
                             $"{TotalFirstOrders.ToString("C2", cultureInfo)} + " +
                             $"{TotalOtherOrders.ToString("C2", cultureInfo)} = " +
                             $"{TotalBusiness.ToString("C2", cultureInfo)}";
    }
}
