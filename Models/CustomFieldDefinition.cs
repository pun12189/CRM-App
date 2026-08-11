using CommunityToolkit.Mvvm.ComponentModel;
using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class CustomFieldDefinition : ObservableObject
    {
        [ObservableProperty] private int _fieldId;
        [ObservableProperty] private string _fieldName = string.Empty;
        [ObservableProperty] private string? _displayLabel;
        [ObservableProperty] private string _fieldType = "Textbox"; // Textbox, TextArea, DropdownSingle, DropdownMultiple, CalendarClock

        // --- NEW SCHEMA COLUMNS ---
        [ObservableProperty] private string _moduleType = "Lead"; // Lead, Customer, Product, Order, Purchase, Vendor, Staff
        [ObservableProperty] private int _fieldTier = 3;          // 1 = System Mandatory, 2 = Optional Model Field, 3 = Dynamic Custom Field
        [ObservableProperty] private bool _isVisible = true;      // Universal Visibility Flag
        [ObservableProperty] private bool _isRequired;           // Universal Required Validation Flag

        // Raw Database JSON Storage Holder for Dropdown Options
        [ObservableProperty] private string? _seedValues;

        // Non-persisted transient runtime helper list used for WPF ItemsControl binding lookups
        [ObservableProperty] private ObservableCollection<string> _seedValueOptionsList = new();

        // Runtime UI state management helper flags
        [ObservableProperty] private bool _isSelected;
        [ObservableProperty] private int _rowIndex;

        /// <summary>
        /// Helper property to display effective UI label (DisplayLabel if available, otherwise FieldName)
        /// </summary>
        public string EffectiveLabel => !string.IsNullOrWhiteSpace(DisplayLabel) ? DisplayLabel : FieldName;

        /// <summary>
        /// Badge text helper for Tier level
        /// </summary>
        public string TierBadgeText => FieldTier switch
        {
            1 => "Mandatory Core",
            2 => "Standard Model",
            _ => "Custom Extended"
        };

        public string TierBadgeColor => FieldTier switch
        {
            1 => "#DC2626", // Red
            2 => "#2563EB", // Blue
            _ => "#0D9488"  // Teal
        };
    }
}
