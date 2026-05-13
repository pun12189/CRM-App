using CallMan.Dialogs;
using CallMan.Models;
using CallMan.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace CallMan.ViewModels
{
    public partial class CompanyProfileViewModel : ObservableObject
    {
        private readonly ProfileService _profileService;

        [ObservableProperty] private ObservableCollection<Division> _divisions = new();
        [ObservableProperty] private Division _selectedDivision;
        [ObservableProperty] private CompanyProfile _currentProfile = new();

        public CompanyProfileViewModel(ProfileService profileService)
        {
            _profileService = profileService;
            _ = LoadProfile();
        }

        private async Task LoadProfile()
        {
            var list = await _profileService.GetActiveDivisionsAsync();
            Divisions = new ObservableCollection<Division>(list);
        }

        partial void OnSelectedDivisionChanged(Division value)
        {
            if (value != null) LoadProfile(value.Id);
        }

        private async void LoadProfile(int divisionId)
        {
            var profile = await _profileService.GetProfileByDivisionAsync(divisionId);
            // If no profile exists yet, create a blank one for that Division
            CurrentProfile = profile ?? new CompanyProfile { DivisionId = divisionId };
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
        private void SelectStamp()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png"
            };
            if (dialog.ShowDialog() == true)
            {
                // 1. Read the file into a byte array for the DB
                CurrentProfile.StampData = System.IO.File.ReadAllBytes(dialog.FileName);

                // 2. Convert to BitmapSource for the UI
                CurrentProfile.StampImage = Helper.Helper.ToBitmapSource(CurrentProfile.StampData);
            }
        }

        [RelayCommand]
        private async Task SaveProfile()
        {
            await _profileService.SaveProfileAsync(CurrentProfile);
        }

        [RelayCommand]
        private async Task AddDivision()
        {
            var addDivVM = new AddDivisionViewModel(_profileService);
            var addDivWindow = new AddDivisionWindow { DataContext = addDivVM };

            addDivVM.RequestClose += (result) =>
            {
                addDivWindow.DialogResult = result;
                addDivWindow.Close();
            };

            if (addDivWindow.ShowDialog() == true)
            {
                // Re-run the query to show the new lead in the DataGrid
                await LoadProfile();
                SelectedDivision = Divisions?.LastOrDefault();
            }
        }
    }
}
