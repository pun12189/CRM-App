using CallMan.Interfaces;
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
using System.Windows;

namespace CallMan.ViewModels
{
    public partial class GenericSettingsViewModel : ObservableObject
    {
        private readonly SettingService _settingService;
        private readonly IDialogService _dialogService;
        private string _currentTableName;

        [ObservableProperty] private string _pageTitle; // e.g., "Dead Reasons"
        [ObservableProperty] private ObservableCollection<SettingItem> _itemsList = new();

        public GenericSettingsViewModel(SettingService settingService, IDialogService dialogService)
        {
            _settingService = settingService;
            _dialogService = dialogService;
        }

        // This method is called to configure the ViewModel for a specific setting type
        public async Task Initialize(string settingType)
        {
            PageTitle = settingType;

            // Map UI Name to Database Table Name
            _currentTableName = settingType switch
            {
                "Dead Reasons" => "DeadReasons",
                "Followup Stages" => "LeadStatuses", // Mapped based on your 2nd image title
                "Mature Stages" => "MatureStages",
                "Lead Tags" => "LeadTags",
                "Lead Source" => "LeadSources",
                "Lead Labels" => "LeadLabels",
                _ => ""
            };

            await LoadData();
        }

        private async Task LoadData()
        {
            var items = await _settingService.GetSettingsAsync(_currentTableName);
            ItemsList = new ObservableCollection<SettingItem>(items);
        }

        [RelayCommand]
        private async Task OpenAddDialog()
        {
            // Open the generic single-input dialog (e.g., "Add New Dead Reason")
            var result = await _dialogService.ShowSingleInputDialog($"Add New {PageTitle.Replace(" Stages", "")}", null);

            if (!string.IsNullOrEmpty(result))
            {
                await _settingService.CreateSettingAsync(_currentTableName, result);
                await LoadData();
            }
        }

        [RelayCommand]
        private async Task EditItem(SettingItem item)
        {
            if (item == null) return;

            // Open dialog pre-filled for editing
            var result = await _dialogService.ShowSingleInputDialog($"Edit {PageTitle.Replace(" Stages", "")}", item.Name);

            if (!string.IsNullOrEmpty(result) && result != item.Name)
            {
                item.Name = result;
                await _settingService.UpdateSettingAsync(_currentTableName, item);
                await LoadData();
            }
        }

        [RelayCommand]
        private async Task DeleteItem(SettingItem item)
        {
            if (item == null) return;

            MessageBoxResult confirm = MessageBox.Show($"Are you sure you want to delete '{item.Name}'?", $"Delete {PageTitle.Replace(" Stages", "")}", MessageBoxButton.YesNo);
            if (confirm == MessageBoxResult.Yes)
            {
                await _settingService.DeleteSettingAsync(_currentTableName, item.Id);
                ItemsList.Remove(item);
            }
        }
    }
}
