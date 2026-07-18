using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public partial class SelectableReportEntity : ObservableObject
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        [ObservableProperty] private bool _isChecked;
    }
}
