using CommunityToolkit.Mvvm.ComponentModel;
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class Lead : ObservableObject
    {
        [ObservableProperty] private int _serialNumber;
        [ObservableProperty] private bool _isSelectedForAction;

        public int LeadId { get; set; }
        [ObservableProperty] private ObservableCollection<Division> _assignedDivisions = new();
        [ObservableProperty] private string _customerName = string.Empty;
        [ObservableProperty] private string? _email;
        [ObservableProperty] private string? _phone;
        [ObservableProperty] private string? _altPhone;
        [ObservableProperty] private string _status = "New";
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Structured Address Fields
        [ObservableProperty] private string? _companyName;
        [ObservableProperty] private string? _addressLine;
        [ObservableProperty] private string? _city;
        [ObservableProperty] private string? _district;
        [ObservableProperty] private string? _state;
        [ObservableProperty] private string? _pincode;
        [ObservableProperty] private string? _country = "India";
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

        /// <summary>
        /// Automatically extracts uppercase initials from the CustomerName for the UI Avatar.
        /// e.g., "Ashish" -> "A", "Mr. Aggarwal" -> "MA", "Chhaya Medicine" -> "CM"
        /// </summary>
        public string Initials
        {
            get
            {
                if (string.IsNullOrWhiteSpace(CustomerName))
                    return "??";

                // Split name by spaces, filter out empty elements
                var parts = CustomerName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 1)
                {
                    // If single name, take first 2 letters if available, or just 1
                    return parts[0].Length >= 2
                        ? parts[0].Substring(0, 2).ToUpper()
                        : parts[0].Substring(0, 1).ToUpper();
                }

                // If multiple names, take the first letter of the first name and first letter of the last name
                string firstInitial = parts[0].Substring(0, 1);
                string lastInitial = parts[parts.Length - 1].Substring(0, 1);

                return (firstInitial + lastInitial).ToUpper();
            }
        }

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
        public string CustomInfoSummary => string.Join(Environment.NewLine, CustomFields.Select(x => $"{x.Key} : {x.Value}"));

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
        public decimal MonthlyTarget { get; set; }
        public int OrderCount { get; set; }
        [ObservableProperty] private int _historyCount;

        [ObservableProperty] private int? _statusId;
        [ObservableProperty] private int? _deadReasonId;
        [ObservableProperty] private int? _matureStageId;
        [ObservableProperty] private int? _leadSourceId;    // To replace raw text matching if needed
        [ObservableProperty] private int? _leadTagId;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AssignedCategoryLabelDisplay))]
        private int? _categoryId;

        // Code-linked helper text property used directly by your clickable XAML DialogHost label string
        public string AssignedCategoryLabelDisplay => string.IsNullOrWhiteSpace(CategoryName)
            ? "None Assigned [ Click to Set ]"
            : CategoryName;

        // Populated dynamically during join queries
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AssignedCategoryLabelDisplay))]
        private string? _categoryName;
    }
}
