using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Tijori.Models;
using Tijori.Services;

namespace Tijori.ViewModels
{
    public partial class CreateEditDivisionDialogViewModel : ObservableObject
    {
        private readonly ProfileService _profileService;

        [ObservableProperty] private int _divisionId;
        [ObservableProperty] private string _divisionName = string.Empty;
        [ObservableProperty] private bool _isActive = true;
        [ObservableProperty] private bool _isEditMode;
        [ObservableProperty] private CompanyProfile _currentProfile = new();

        public CreateEditDivisionDialogViewModel(ProfileService profileService, int divisionId = 0, string divisionName = "")
        {
            _profileService = profileService;
            DivisionId = divisionId;
            DivisionName = divisionName;
            IsEditMode = divisionId > 0;

            _ = InitializeProfileAsync();
        }

        private async Task InitializeProfileAsync()
        {
            if (IsEditMode)
            {
                var profile = await _profileService.GetProfileByDivisionAsync(DivisionId);
                if (profile != null)
                {
                    if (profile.LogoData != null)
                        profile.LogoImage = Helper.Helper.ToBitmapSource(profile.LogoData);
                    if (profile.StampData != null)
                        profile.StampImage = Helper.Helper.ToBitmapSource(profile.StampData);

                    CurrentProfile = profile;
                }
                else
                {
                    CurrentProfile = new CompanyProfile { DivisionId = DivisionId, CompanyName = DivisionName };
                }
            }
            else
            {
                CurrentProfile = new CompanyProfile();
            }
        }

        [RelayCommand]
        private void SelectLogo()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png" };
            if (dialog.ShowDialog() == true)
            {
                CurrentProfile.LogoData = File.ReadAllBytes(dialog.FileName);
                CurrentProfile.LogoImage = Helper.Helper.ToBitmapSource(CurrentProfile.LogoData);
            }
        }

        [RelayCommand]
        private void SelectStamp()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png" };
            if (dialog.ShowDialog() == true)
            {
                CurrentProfile.StampData = File.ReadAllBytes(dialog.FileName);
                CurrentProfile.StampImage = Helper.Helper.ToBitmapSource(CurrentProfile.StampData);
            }
        }

        [RelayCommand]
        private async Task SaveAsync(Window window)
        {
            if (string.IsNullOrWhiteSpace(DivisionName))
            {
                MessageBox.Show("Please enter a Division Name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!IsEditMode)
            {
                var div = new Division { Name = DivisionName.Trim(), IsActive = IsActive };
                DivisionId = await _profileService.CreateDivisionAsync(div);
                CurrentProfile.DivisionId = DivisionId;
                if (string.IsNullOrWhiteSpace(CurrentProfile.CompanyName))
                    CurrentProfile.CompanyName = DivisionName.Trim();
            }
            else
            {
                // Update the Division Name in the divisions table when editing
                await _profileService.UpdateDivisionNameAsync(DivisionId, DivisionName.Trim());
            }

            CurrentProfile.DivisionId = DivisionId;

            bool saved = await _profileService.SaveProfileAsync(CurrentProfile);
            if (!saved)
            {
                MessageBox.Show("Failed to save company profile settings. Please check your inputs and database connection.",
                                "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            window.DialogResult = true;
            window.Close();
        }

        [RelayCommand]
        private void Cancel(Window window)
        {
            window.DialogResult = false;
            window.Close();
        }
    }
}
