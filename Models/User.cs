using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public partial class User : ObservableObject
    {
        public int UserId { get; set; }

        [ObservableProperty] private string _email = string.Empty;
        [ObservableProperty] private string? _phone; // Optional
        [ObservableProperty] private string _fullName = string.Empty;
        [ObservableProperty] private string _password;
        [ObservableProperty] private string _role = "Executive";
        [ObservableProperty] private int? _seniorId;
        [ObservableProperty] private decimal _monthlyTarget;
        [ObservableProperty] private bool _isActive = true;

        // Helper for ComboBoxes
        public string DisplayName => $"{FullName} ({Role})";

        public string? SeniorName { get; set; }
    }
}
