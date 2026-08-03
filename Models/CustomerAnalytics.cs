using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public class CustomerAnalytics : ObservableObject
    {
        public int LeadId { get; set; }

        // Core fields for your current UI
        public decimal FirstOrderAmount { get; set; }
        public decimal LastOrderAmount { get; set; }
    }
}
