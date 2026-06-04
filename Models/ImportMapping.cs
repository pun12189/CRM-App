using CallMan.Models.Enums;
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
        public string InternalPropertyName { get; set; } // e.g., "CustomerName"
        public string DisplayName { get; set; }          // e.g., "Customer Name"
        public MappingTargetType TargetType { get; set; } = MappingTargetType.StandardField;

        // Maps text values to relational lookup tables
        public string LookupTableName { get; set; }
        public string LookupIdColumn { get; set; }

        [ObservableProperty] private string _selectedExcelHeader;
    }
}
