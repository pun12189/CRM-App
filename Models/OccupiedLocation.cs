using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class OccupiedLocation : ObservableObject
    {
        // Basic Info from Leads Table
        public int Id { get; set; }
        public string State { get; set; }
        public string District { get; set; }
        public string City { get; set; }
        public string WorkingArea { get; set; }
        public string Pincode { get; set; }

        // Customer/Firm Info
        public string CustomerName { get; set; }
        public string FirmName { get; set; }

        // Leadholder Info
        public string LeadHolder { get; set; }
        public string Phone { get; set; }
        public string Senior { get; set; }

        // Summary Totals (Calculated via SQL)
        public int TotalOrders { get; set; }
        public decimal TotalPayments { get; set; }

        public ObservableCollection<Division> AssignedDivisions { get; set; } = new();

        // UI Formatted Summary
        public string SummaryBrief => $"Orders: {TotalOrders} | Recv: ₹{TotalPayments:N0}";
    }
}
