using CallMan.Models;
using CallMan.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace CallMan.ViewModels
{
    public partial class CompanyProfileViewModel : ObservableObject
    {
        private readonly ProfileService _profileService;

        [ObservableProperty] private CompanyProfile _currentProfile = new();

        public CompanyProfileViewModel(ProfileService profileService)
        {
            _profileService = profileService;
            _ = LoadProfile();
        }

        private async Task LoadProfile()
        {
            CurrentProfile = await _profileService.GetProfileAsync();
        }

        [RelayCommand]
        private void SelectLogo()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png"
            };
            if (dialog.ShowDialog() == true)
            {
                // 1. Read the file into a byte array for the DB
                CurrentProfile.LogoData = System.IO.File.ReadAllBytes(dialog.FileName);

                // 2. Convert to BitmapSource for the UI
                CurrentProfile.LogoImage = Helper.Helper.ToBitmapSource(CurrentProfile.LogoData);
            }
        }

        [RelayCommand]
        private async Task SaveProfile()
        {
            if (await _profileService.SaveProfileAsync(CurrentProfile))
            {
                // Show Success Notification
            }
        }
    }
}
