using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public partial class Lead : ObservableObject
    {
        [ObservableProperty] private bool _isSelectedForAction;

        public int LeadId { get; set; }
        public ObservableCollection<Division> AssignedDivisions { get; set; } = new();
        public string CustomerName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        [ObservableProperty] private string _status = "New";
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Structured Address Fields
        public string? CompanyName { get; set; }
        public string? AddressLine { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? State { get; set; }
        public string? Pincode { get; set; }
        public string? Country { get; set; } = "India";

        // Mapping Properties for Excel
        [ObservableProperty] private string _leadSource;    // Maps to "Lead Source" column
        [ObservableProperty] private string _leadTag;      // Maps to "Tags" (e.g. "New,Urgent")
        [ObservableProperty] private ObservableCollection<string> _leadLabels = new();
        // This is what you save to the DB as a string
        public string LabelsJson { get; set; }


        // For Dynamic JSON
        public string? MetadataJson { get; set; }

        [ObservableProperty]
        private string? _leadHolder;

        [NotMapped]
        [ObservableProperty]
        private decimal _totalOrderAmount;

        [NotMapped]
        [ObservableProperty]
        private decimal _totalPaidAmount;

        [NotMapped]
        public decimal TotalBalanceDue => TotalOrderAmount - TotalPaidAmount;

        // Visual helper for the DataGrid
        public bool HasPendingBalance => TotalBalanceDue > 0;

        [NotMapped]
        public List<LeadHistoryEntry> History { get; set; } = new();

        // Helper to display dynamic info in the Grid
        [NotMapped]
        public string CustomInfoSummary => string.Join(", ", CustomFields.Select(x => $"{x.Key} : {x.Value}"));

        [NotMapped]
        public Dictionary<string, string> CustomFields { get; set; } = new();

        [NotMapped]
        [ObservableProperty]
        private LeadHistoryEntry? _latestUpdate;

        // UI Visual Helpers
        public string PaymentStatusText => TotalBalanceDue <= 0 ? "Fully Paid" : "Balance Pending";
        public string PaymentStatusColor => TotalBalanceDue <= 0 ? "#27AE60" : "#E67E22"; // Green vs Orange

        // Navigation Properties (Optional, useful for deep loading)
        public ObservableCollection<Order> Orders { get; set; } = new();
        public ObservableCollection<PaymentEntry> Payments { get; set; } = new();

        [ObservableProperty] private string? _workingArea; // New field for image_0d4f1a.png

        // Calculated property for the Customer View (image_0d52a4.png)
        public string Summary => $"{TotalMaturedAmount:C} | {OrderCount} orders";
        public decimal TotalMaturedAmount { get; set; }
        public int OrderCount { get; set; }
        [ObservableProperty] private int _historyCount;
    }
}
