using CallMan.Models.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public class ToastQueueItem
    {
        public int ToastId { get; set; }
        public int EventId { get; set; }
        public int LeadId { get; set; } = 0;
        public string ReminderType { get; set; } = string.Empty;
        public string MessageText { get; set; } = string.Empty;
        public DateTime ScheduleTime { get; set; }
        public string NotificationStatus { get; set; } = "Pending";
        public string? TargetUser { get; set; }
        public string? TargetMachine { get; set; }
        public string CreatedBy { get; set; } = string.Empty;

        // ADDED: Tracks the arrival timestamp of the background entry row
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // UI Helper property to display clean time format (e.g., "11:45 AM" or "02:15 PM")
        public string FormattedTime => CreatedAt.ToString("dd-MMM-yyyy hh:mm tt");
    }

    public class NewToastRequest
    {
        public int EventId { get; set; }
        public int LeadId { get; set; } = 0;
        public string ReminderType { get; set; } = "FollowUp";
        public string MessageContent { get; set; } = string.Empty;
        public DateTime ScheduleTime { get; set; } = DateTime.Now;
        public string? TargetUser { get; set; }
        public string? TargetMachine { get; set; }
        public string SenderUser { get; set; } = string.Empty;
    }
}
