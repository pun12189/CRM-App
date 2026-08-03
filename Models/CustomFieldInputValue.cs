using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class CustomFieldInputValue : ObservableObject
    {
        // Core structural properties inherited from DB mapping configurations
        public int FieldId { get; set; }
        public string FieldName { get; set; } = string.Empty;
        public string FieldType { get; set; } = "Textbox";
        public bool IsRequiredInLead { get; set; }
        public ObservableCollection<string> SeedValueOptionsList { get; set; } = new();

        // Observable runtime string capture bound straight to the UI control elements
        [ObservableProperty] private string _fieldValue = string.Empty;

        [ObservableProperty] private DateTime? _selectedDate;
        [ObservableProperty] private DateTime? _selectedTime;

        // Visual layout validation error tracking strings
        [ObservableProperty] private bool _hasValidationError;
        [ObservableProperty] private string _validationErrorMessage = string.Empty;

        partial void OnSelectedDateChanged(DateTime? value) => SyncUnifiedTimestamp();
        partial void OnSelectedTimeChanged(DateTime? value) => SyncUnifiedTimestamp();

        private void SyncUnifiedTimestamp()
        {
            if (SelectedDate.HasValue)
            {
                var datePart = SelectedDate.Value.Date;
                var timePart = SelectedTime.HasValue ? SelectedTime.Value.TimeOfDay : TimeSpan.Zero;

                // Formats values back to safe database insertion string blocks: "2026-06-19 17:45:00"
                FieldValue = datePart.Add(timePart).ToString("yyyy-MM-dd HH:mm:ss");
            }
            else
            {
                FieldValue = string.Empty;
            }
        }
    }
}
