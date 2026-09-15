using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class Workflow : ObservableObject
    {
        public int Id { get; set; }

        [ObservableProperty] private string _workflowName = string.Empty;
        [ObservableProperty] private string _eventName = "Select";
        [ObservableProperty] private int _executionDays;
        [ObservableProperty] private bool _isEnabled = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // --- Active Channels ---
        [ObservableProperty] private bool _sendWhatsApp;
        [ObservableProperty] private bool _sendEmail;
        [ObservableProperty] private bool _sendNotification;

        // --- WhatsApp Configuration ---
        [ObservableProperty] private string _whatsAppSender = "Primary"; // "Primary" or "Leadholder"
        [ObservableProperty] private bool _whatsAppToLead = true;
        [ObservableProperty] private bool _whatsAppToUser;
        [ObservableProperty] private string _whatsAppMessage = string.Empty;

        // --- Email Configuration ---
        [ObservableProperty] private bool _emailToLead = true;
        [ObservableProperty] private bool _emailToUser;
        [ObservableProperty] private string _emailMessage = string.Empty;

        // --- In-App Toast Notification ---
        [ObservableProperty] private string _notificationMessage = string.Empty;

        // UI Grid Helper Property
        public bool IsSelected { get; set; }
    }
}
