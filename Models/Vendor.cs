using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public partial class Vendor : ObservableObject
    {
        [ObservableProperty] private int _vendorId;
        [ObservableProperty] private string _companyName = string.Empty;
        [ObservableProperty] private string? _contactPerson;
        [ObservableProperty] private string _phone = string.Empty;
        [ObservableProperty] private string? _email;
        [ObservableProperty] private string? _gstNumber;
        [ObservableProperty] private string? _address;
        [ObservableProperty] private string _status = "Active"; // Active, Inactive
        [ObservableProperty] private DateTime _createdAt = DateTime.Now;
    }
}
