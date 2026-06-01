using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public partial class ProformaSummaryItem : ObservableObject
    {
        public int ProformaId { get; set; }
        [ObservableProperty] private string _proformaNumber = string.Empty;
        [ObservableProperty] private string _paymentType = "Cash"; // Matches your image lookup parameters
        [ObservableProperty] private DateTime _dateCreated;
        [ObservableProperty] private decimal _amount;
        [ObservableProperty] private string _status = "Pending"; // 'Pending', 'ConvertedToOrder'
        [ObservableProperty] private string _paymentStatus = "Unpaid"; // 'Unpaid', 'Paid'

        // UI Helpers
        public string FormattedDate => DateCreated.ToString("dd-MMMM-yyyy hh:mm tt");

        // Status Pill Badge Color Indicators
        public string StatusColor => Status == "Pending" ? "#F39C12" : "#27AE60";
        public string PaymentStatusColor => PaymentStatus == "Unpaid" ? "#E74C3C" : "#2ECC71";
    }
}
