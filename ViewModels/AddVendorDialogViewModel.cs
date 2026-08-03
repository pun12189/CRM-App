using Tijori.Models;
using Tijori.Services;
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

namespace Tijori.ViewModels
{
    public partial class AddVendorWindowViewModel : ObservableValidator
    {
        private readonly VendorService _vendorService;

        [ObservableProperty]
        private int _vendorId;

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
        [RegularExpression(@"^([0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1})?$", ErrorMessage = "Invalid GSTIN format.")]
        [NotifyDataErrorInfo]
        private string _gstNumber = string.Empty;

        [ObservableProperty]
        private string _address = string.Empty;

        [ObservableProperty]
        private string _status = "Active";

        [ObservableProperty]
        private string _windowTitle = "Register New Supplier Account";

        [ObservableProperty]
        private string _headerTitle = "Register New Vendor Account";

        [ObservableProperty]
        private string _saveButtonText = "Save Vendor";

        public bool IsEditMode { get; }

        public AddVendorWindowViewModel(VendorService vendorService, Vendor? existingVendor = null)
        {
            _vendorService = vendorService;

            if (existingVendor != null)
            {
                IsEditMode = true;
                WindowTitle = "Update Supplier Details";
                HeaderTitle = "Edit Vendor Account";
                SaveButtonText = "Update Vendor";

                // Populate properties from existing record
                VendorId = existingVendor.VendorId;
                CompanyName = existingVendor.CompanyName ?? string.Empty;
                ContactPerson = existingVendor.ContactPerson ?? string.Empty;
                Phone = existingVendor.Phone ?? string.Empty;
                Email = existingVendor.Email ?? string.Empty;
                GstNumber = existingVendor.GstNumber ?? string.Empty;
                Address = existingVendor.Address ?? string.Empty;
                Status = existingVendor.Status ?? "Active";

                // Trigger initial validation for existing data
                ValidateAllProperties();
            }
            else
            {
                IsEditMode = false;
                // Clear initial validation state so required errors don't trigger before input
                ClearErrors();
            }
        }

        protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            // Notify command validation state when properties change
            if (e.PropertyName != nameof(HasErrors))
            {
                SaveVendorCommand.NotifyCanExecuteChanged();
            }
        }

        public bool CanSave(Window currentWindow) => !HasErrors;

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveVendorAsync(Window currentWindow)
        {
            ValidateAllProperties();
            if (HasErrors) return;

            var vendor = new Vendor
            {
                VendorId = VendorId,
                CompanyName = CompanyName.Trim(),
                ContactPerson = string.IsNullOrWhiteSpace(ContactPerson) ? null : ContactPerson.Trim(),
                Phone = Phone.Trim(),
                Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
                GstNumber = string.IsNullOrWhiteSpace(GstNumber) ? null : GstNumber.Trim().ToUpper(),
                Address = string.IsNullOrWhiteSpace(Address) ? null : Address.Trim(),
                Status = Status
            };

            bool success;

            if (IsEditMode)
            {
                // Execute Update in database
                success = await _vendorService.UpdateVendorAsync(vendor);
            }
            else
            {
                // Execute Insert in database
                int generatedId = await _vendorService.SaveVendorAsync(vendor);
                success = generatedId > 0;
            }

            if (success)
            {
                if (currentWindow != null)
                {
                    currentWindow.DialogResult = true;
                    currentWindow.Close();
                }
            }
            else
            {
                MessageBox.Show("Failed to save vendor details. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
