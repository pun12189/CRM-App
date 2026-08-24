using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class CustomerBrand : ObservableObject
    {
        [ObservableProperty] private int _brandId;
        [ObservableProperty] private int _customerId;
        [ObservableProperty] private string _brandName = string.Empty;
        [ObservableProperty] private string? _trademarkNumber;
        [ObservableProperty] private string? _drugLicenseNumber;
        [ObservableProperty] private string? _fssaiNumber;
        [ObservableProperty] private bool _isActive = true;
        [ObservableProperty] private DateTime _createdAt = DateTime.Now;
    }
}
