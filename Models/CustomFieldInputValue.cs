using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class CustomFieldInputValue : ObservableObject
    {
        public int FieldId { get; set; }
        public string FieldName { get; set; } = string.Empty;
        public string? DisplayLabel { get; set; }
        public string FieldType { get; set; } = "Textbox"; // Textbox, TextArea, DropdownSingle, DropdownMultiple, CalendarClock
        public int FieldTier { get; set; } = 3;
        public bool IsRequired { get; set; }

        public string EffectiveLabel => !string.IsNullOrWhiteSpace(DisplayLabel) ? DisplayLabel : FieldName;

        // Raw options list deserialized from SeedValues JSON
        public ObservableCollection<string> SeedValueOptionsList { get; set; } = new();

        // NEW: Dynamic Options List (Unified source for static SeedValues OR live DB table query results)
        [ObservableProperty]
        private ObservableCollection<string> _optionsList = new();

        // NEW: Multi-Select Chips Collection (Used for DropdownMultiple types like Divisions, LeadLabels)
        [ObservableProperty]
        private ObservableCollection<string> _selectedMultiValues = new();

        // Observable runtime string capture bound straight to UI elements
        [ObservableProperty]
        private string _fieldValue = string.Empty;

        [ObservableProperty]
        private DateTime? _selectedDate;

        [ObservableProperty]
        private DateTime? _selectedTime;

        // Visual validation tracking
        [ObservableProperty]
        private bool _hasValidationError;

        [ObservableProperty]
        private string _validationErrorMessage = string.Empty;

        public CustomFieldInputValue()
        {
            // Listen for changes in SelectedMultiValues to keep FieldValue string synchronized
            SelectedMultiValues.CollectionChanged += SelectedMultiValues_CollectionChanged;
        }

        partial void OnSelectedDateChanged(DateTime? value) => SyncUnifiedTimestamp();
        partial void OnSelectedTimeChanged(DateTime? value) => SyncUnifiedTimestamp();

        private void SyncUnifiedTimestamp()
        {
            if (SelectedDate.HasValue)
            {
                var datePart = SelectedDate.Value.Date;
                var timePart = SelectedTime.HasValue ? SelectedTime.Value.TimeOfDay : TimeSpan.Zero;
                FieldValue = datePart.Add(timePart).ToString("yyyy-MM-dd HH:mm:ss");
            }
            else
            {
                FieldValue = string.Empty;
            }
        }

        private bool _isSyncingMultiValues;

        private void SelectedMultiValues_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_isSyncingMultiValues) return;

            try
            {
                _isSyncingMultiValues = true;
                // Syncs selected chips list to comma-separated FieldValue string for DB persistence
                FieldValue = string.Join(", ", SelectedMultiValues.Where(s => !string.IsNullOrWhiteSpace(s)));
            }
            finally
            {
                _isSyncingMultiValues = false;
            }
        }

        /// <summary>
        /// When FieldValue is reloaded from DB upon editing an existing record, 
        /// re-hydrate the SelectedMultiValues chips collection automatically.
        /// </summary>
        partial void OnFieldValueChanged(string value)
        {
            if (_isSyncingMultiValues || FieldType != "DropdownMultiple") return;

            try
            {
                _isSyncingMultiValues = true;
                SelectedMultiValues.Clear();

                if (!string.IsNullOrWhiteSpace(value))
                {
                    var items = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(i => i.Trim());

                    foreach (var item in items)
                    {
                        SelectedMultiValues.Add(item);
                    }
                }
            }
            finally
            {
                _isSyncingMultiValues = false;
            }
        }
    }
}
