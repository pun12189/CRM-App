using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public partial class  LeadHistoryEntry : ObservableObject
    {
        public int HistoryId { get; set; }
        public int LeadId { get; set; }

        [ObservableProperty]
        private string _message = string.Empty;

        [ObservableProperty]
        private string? _actionType; // Call, WhatsApp, Meeting, etc.

        [ObservableProperty]
        private string? _followupStage; // Initial, Negotiation, Matured

        [ObservableProperty]
        private DateTime? _nextFollowUpDate;

        [ObservableProperty]
        private DateTime _logDate = DateTime.Now;

        [ObservableProperty]
        private string _updatedBy = "Admin";

        [ObservableProperty]
        public bool _isPriority;

        // Helper for UI display
        public string DisplayDate => LogDate.ToString("dd MMM yyyy, hh:mm tt");
        public string NextFollowUpDisplay => NextFollowUpDate?.ToString("dd MMM yyyy") ?? "No Follow-up";
    }
}
