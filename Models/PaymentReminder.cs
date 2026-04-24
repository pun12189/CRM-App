using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public class PaymentReminder
    {
        public string CustomerName { get; set; }
        public decimal PendingBalance { get; set; }
        public string Phone { get; set; }
    }
}
