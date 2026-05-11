using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public partial class Workflow : ObservableObject
    {
        public int Id { get; set; }

        [ObservableProperty]
        private string _workflowName;

        [ObservableProperty]
        private string _eventName; // Bound to ComboBox

        [ObservableProperty]
        private int _inactivityDays; // Bound to Inactivity TextBox

        [ObservableProperty]
        private bool _sendEmail; // Bound to Email Checkbox

        [ObservableProperty]
        private bool _sendWhatsApp; // Bound to WhatsApp Checkbox

        [ObservableProperty]
        private string _templateBody; // Bound to unified TemplateBox

        [ObservableProperty]
        private bool _isEnabled = true;
    }
}
