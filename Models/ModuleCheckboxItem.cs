using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class ModuleCheckboxItem : ObservableObject
    {
        [ObservableProperty] private string _moduleName = string.Empty; // "Lead", "Customer", etc.
        [ObservableProperty] private bool _isSelected;
    }
}
