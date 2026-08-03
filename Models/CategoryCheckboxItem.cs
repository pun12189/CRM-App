using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    /// <summary>
    /// Represents a selectable category checkbox item used for 
    /// multi-select tag chip lists inside dialog windows.
    /// </summary>
    public partial class CategoryCheckboxItem : ObservableObject
    {
        // Unique database key identifier for the Business Category
        [ObservableProperty] private int _categoryId;

        // Simple layman name displayed on the UI check box (e.g., "VIP Customer")
        [ObservableProperty] private string _categoryName = string.Empty;

        // Tracks whether the admin has selected this specific filter chip
        [ObservableProperty] private bool _isSelected;
    }
}
