using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Tijori.Dialogs;
using Tijori.Models;
using Tijori.Services;

namespace Tijori.ViewModels
{
    public partial class DivisionsDirectoryViewModel : ObservableObject
    {
        private readonly ProfileService _profileService;

        [ObservableProperty] private ObservableCollection<DivisionListItem> _divisions = new();
        [ObservableProperty] private bool _isLoading;

        public DivisionsDirectoryViewModel(ProfileService profileService)
        {
            _profileService = profileService;
            _ = LoadDivisionsAsync();
        }

        [RelayCommand]
        public async Task LoadDivisionsAsync()
        {
            IsLoading = true;
            var list = await _profileService.GetAllDivisionListItemsAsync();
            Divisions = new ObservableCollection<DivisionListItem>(list);
            IsLoading = false;
        }

        [RelayCommand]
        private async Task OpenAddDivisionDialogAsync()
        {
            var dialogVm = new CreateEditDivisionDialogViewModel(_profileService);
            var dialog = new CreateEditDivisionDialogWindow
            {
                DataContext = dialogVm,
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                await LoadDivisionsAsync();
            }
        }

        [RelayCommand]
        private async Task EditDivisionAsync(DivisionListItem? item)
        {
            if (item == null) return;

            var dialogVm = new CreateEditDivisionDialogViewModel(_profileService, item.Id, item.Name);
            var dialog = new CreateEditDivisionDialogWindow
            {
                DataContext = dialogVm,
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                await LoadDivisionsAsync();
            }
        }

        [RelayCommand]
        private async Task DeleteDivisionAsync(DivisionListItem? item)
        {
            if (item == null) return;

            int linkedCount = await _profileService.GetDivisionLinkedCountAsync(item.Id);

            if (linkedCount > 0)
            {
                // Linked records detected -> Offer Deactivation (Yes) or Force Delete (No) or Abort (Cancel)
                var choice = MessageBox.Show(
                    $"Division '{item.Name}' is currently associated with {linkedCount} existing order(s)/proforma(s).\n\n" +
                    "• Click [YES] to DEACTIVATE (Recommended: Keeps historical records safe, hides division from new orders).\n" +
                    "• Click [NO] to FORCE DELETE (Unlinks division from past orders and permanently removes it).\n" +
                    "• Click [CANCEL] to abort.",
                    "Linked Records Detected",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                if (choice == MessageBoxResult.Yes)
                {
                    // Deactivate
                    bool deactivated = await _profileService.DeactivateDivisionAsync(item.Id);
                    if (deactivated)
                    {
                        MessageBox.Show($"Division '{item.Name}' has been deactivated.", "Status Updated", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadDivisionsAsync();
                    }
                }
                else if (choice == MessageBoxResult.No)
                {
                    // Secondary safety check before permanent deletion
                    var secondConfirm = MessageBox.Show(
                        $"Are you completely sure you want to permanently delete '{item.Name}'?\nPast transactions will lose their division branding reference.",
                        "Permanent Delete Confirmation",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Stop);

                    if (secondConfirm == MessageBoxResult.Yes)
                    {
                        await _profileService.CascadeDeleteDivisionAsync(item.Id);
                        await LoadDivisionsAsync();
                    }
                }
            }
            else
            {
                // Clean delete when no references exist
                var confirm = MessageBox.Show(
                    $"Permanently delete division '{item.Name}'?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm == MessageBoxResult.Yes)
                {
                    await _profileService.DeleteDivisionAsync(item.Id);
                    await LoadDivisionsAsync();
                }
            }
        }
    }
}
