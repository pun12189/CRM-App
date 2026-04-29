using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public partial class SettingItem : ObservableObject
    {
        public int Id { get; set; }

        [ObservableProperty] private string _name = string.Empty;

        // Total Leads associated with this setting (seen in image)
        public int TotalLeads { get; set; }
    }
}
