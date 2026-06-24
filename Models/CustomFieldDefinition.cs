using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public partial class CustomFieldDefinition : ObservableObject
    {
        [ObservableProperty] private int _fieldId;
        [ObservableProperty] private string _fieldName = string.Empty;
        [ObservableProperty] private string _fieldType = "Textbox"; // Textbox, TextArea, DropdownSingle, DropdownMultiple, CalendarClock

        // Scope Visibility States (Now fully observable)
        [ObservableProperty] private bool _isVisibleInLead;
        [ObservableProperty] private bool _isVisibleInCustomer;
        [ObservableProperty] private bool _isVisibleInProduct;

        // Dynamic Submission Validation Rules (Now fully observable)
        [ObservableProperty] private bool _isRequired;
        [ObservableProperty] private bool _isRequiredInLead;
        [ObservableProperty] private bool _isRequiredInCustomer;
        [ObservableProperty] private bool _isRequiredInProduct;

        // Raw Database JSON Storage Holder
        [ObservableProperty] private string? _seedValues;

        // Non-persisted transient runtime helper list used for WPF ItemsControl binding lookups
        [ObservableProperty] private ObservableCollection<string> _seedValueOptionsList = new();

        // Runtime UI state management check tracker helper flags
        [ObservableProperty] private bool _isSelected;
        [ObservableProperty] private int _rowIndex;
    }
}
