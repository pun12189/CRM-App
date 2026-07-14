using CallMan.Models;
using CallMan.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CallMan.ViewModels
{
    public partial class AddVendorWindowViewModel : ObservableValidator
    {
        private readonly VendorService _vendorService;

        [ObservableProperty]
        [Required(ErrorMessage = "Company Name is mandatory.")]
        [MinLength(3, ErrorMessage = "Company Name must be at least 3 characters long.")]
        [NotifyDataErrorInfo]
        private string _companyName = string.Empty;

        [ObservableProperty]
        private string _contactPerson = string.Empty;

        [ObservableProperty]
        [Required(ErrorMessage = "Phone number is mandatory.")]
        [Phone(ErrorMessage = "Invalid phone number format.")]
        [NotifyDataErrorInfo]
        private string _phone = string.Empty;

        [ObservableProperty]
        [EmailAddress(ErrorMessage = "Invalid email address style.")]
        [NotifyDataErrorInfo]
        private string _email = string.Empty;

        [ObservableProperty]
        [RegularExpression(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$", ErrorMessage = "Invalid GSTIN format.")]
        [NotifyDataErrorInfo]
        private string _gstNumber = string.Empty;

        [ObservableProperty]
        private string _address = string.Empty;

        public AddVendorWindowViewModel(VendorService vendorService)
        {
            _vendorService = vendorService;
            ValidateAllProperties();
        }

        protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            // Re-evaluate command status if properties change
            if (e.PropertyName != nameof(HasErrors))
            {
                SaveVendorCommand.NotifyCanExecuteChanged();
            }
        }

        // Logic check to verify if the button is allowed to activate
        public bool CanSave(Window currentWindow) => !HasErrors;

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveVendorAsync(Window currentWindow)
        {
            ValidateAllProperties();
            if (HasErrors) return;

            if (string.IsNullOrWhiteSpace(CompanyName) || string.IsNullOrWhiteSpace(Phone))
            {
                MessageBox.Show("Company Name and Phone are required fields.", "Validation Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var vendor = new Vendor
            {
                CompanyName = CompanyName,
                ContactPerson = ContactPerson,
                Phone = Phone,
                Email = Email,
                GstNumber = GstNumber,
                Address = Address,
                Status = "Active"
            };

            int generatedId = await _vendorService.SaveVendorAsync(vendor);
            if (generatedId > 0)
            {
                if (currentWindow != null)
                {
                    currentWindow.DialogResult = true; // Sets success flag matrix parameter context
                    currentWindow.Close();
                }
            }
        }

        [RelayCommand]
        private void CloseWindow(Window currentWindow)
        {
            if (currentWindow != null)
            {
                currentWindow.DialogResult = false;
                currentWindow.Close();
            }
        }
    }
}
