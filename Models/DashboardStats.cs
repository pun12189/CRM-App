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
        public int Untouched { get; set; }
        public int Dead { get; set; }
        public int DeadRequest { get; set; }
        public int Customers { get; set; }
        public decimal TotalBusiness { get; set; }
    }
}
