using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public partial class PermissionRow : ObservableObject
    {
        public int ModuleId { get; set; }
        public string ModuleKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        [ObservableProperty] private bool _canView;
        [ObservableProperty] private bool _canEdit;
        [ObservableProperty] private bool _canCreate;
        [ObservableProperty] private bool _canDelete;
    }
}
