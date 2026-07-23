using CommunityToolkit.Mvvm.ComponentModel;
using DocumentFormat.OpenXml.Drawing.Charts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public partial class OrderHistoryEntry : ObservableObject
    {
        public int HistoryId { get; set; }
        public int OrderId { get; set; }
        public int LeadId { get; set; }

        /// <summary>
        /// Summary header of the action (e.g., "Order Created", "Payment Received", "Status Updated")
        /// </summary>
        [ObservableProperty]
        private string _actionTitle = string.Empty;

        /// <summary>
        /// Detailed information, change logs, or notes regarding the order event
        /// </summary>
        [ObservableProperty]
        private string _description = string.Empty;

        /// <summary>
        /// Action classification (e.g., "OrderCreated", "StatusChange", "PaymentAdded", "ItemAdded", "Dispatched", "DocumentUploaded")
        /// </summary>
        [ObservableProperty]
        private string _actionType = "SystemLog";

        /// <summary>
        /// Captures previous state if applicable (e.g., "Pending")
        /// </summary>
        [ObservableProperty]
        private string? _previousState;

        /// <summary>
        /// Captures new state if applicable (e.g., "Accepted" or "Dispatched")
        /// </summary>
        [ObservableProperty]
        private string? _newState;

        /// <summary>
        /// Optional financial amount involved in this log event (e.g., Payment amount)
        /// </summary>
        [ObservableProperty]
        private decimal? _transactionAmount;

        [ObservableProperty]
        private DateTime _logDate = DateTime.Now;

        /// <summary>
        /// Username or System ID of the actor performing the change
        /// </summary>
        [ObservableProperty]
        private string _performedBy = "Admin";

        /// <summary>
        /// Highlights crucial entries like dispatches or cancellations in the timeline
        /// </summary>
        [ObservableProperty]
        private bool _isImportant;

        // UI Display Helpers
        public string DisplayDate => LogDate.ToString("dd MMM yyyy, hh:mm tt");
        public string DisplayShortDate => LogDate.ToString("dd MMM yyyy");
    }
}
