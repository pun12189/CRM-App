using CallMan.Models.Enums;
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
        [ObservableProperty] private string? _phone;
        [ObservableProperty] private string _fullName = string.Empty;
        [ObservableProperty] private string _password = string.Empty;
        [ObservableProperty] private UserRole _role = UserRole.Executive;
        [ObservableProperty] private int? _seniorId;
        [ObservableProperty] private int _departmentId;
        [ObservableProperty] private bool _isActive = true;
        [ObservableProperty] private double _monthlyTarget = 0.0;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Unmapped relational lookup elements loaded via join queries
        [NotMapped] public string? SeniorName { get; set; }
        [NotMapped] public string? DepartmentName { get; set; }

        public string DisplayName => $"{FullName} ({Role})";
    }
}
