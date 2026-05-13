using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public partial class ImportMapping : ObservableObject
    {
        public string InternalPropertyName { get; set; } // The C# Class Property Name
        public string DisplayName { get; set; }          // Human-readable name
        [ObservableProperty] private string _selectedExcelHeader; // Chosen from Dropdown
    }
}
