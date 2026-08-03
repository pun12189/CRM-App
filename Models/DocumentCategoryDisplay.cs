using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class DocumentCategoryDisplay : ObservableObject
    {
        [ObservableProperty] private int _categoryId;
        [ObservableProperty] private string _categoryName = string.Empty;
        [ObservableProperty] private string _linkedModulesDisplay = string.Empty; // E.g. "Lead, Customer, Staff"
        [ObservableProperty] private bool _isSystemDefined;

        public BusinessCategory RawCategory { get; set; }
    }
}
