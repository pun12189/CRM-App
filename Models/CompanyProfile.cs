using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace CallMan.Models
{
    public partial class CompanyProfile : ObservableObject
    {
        public int Id { get; set; } = 1;
        // This is what Dapper maps to the BLOB column
        [ObservableProperty] private byte[] _logoData;

        // This is what the XAML Image control binds to
        [ObservableProperty] private BitmapSource _logoImage;
        [ObservableProperty] private string _companyName;
        [ObservableProperty] private string _proprietorName;
        [ObservableProperty] private string _gstNumber;
        [ObservableProperty] private string _panNumber;
        [ObservableProperty] private string _contactNumber;
        [ObservableProperty] private string _officialEmail;
        [ObservableProperty] private string _bankName;
        [ObservableProperty] private string _accountNumber;
        [ObservableProperty] private string _ifscCode;
        [ObservableProperty] private string _upiId;
        [ObservableProperty] private string _registeredAddress;
        [ObservableProperty] private string _companyInitials;
        [ObservableProperty] private int _invoiceStartNumber;
        [ObservableProperty] private string _termsAndConditions;
    }
}
