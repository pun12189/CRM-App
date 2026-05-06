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
    public partial class OrderStagesViewModel : ObservableObject
    {
        private readonly OrderStageService _stageService;

        // Property to toggle the Color Picker Popup
        [ObservableProperty] private bool _isColorPickerOpen;

        [ObservableProperty] private ObservableCollection<OrderStage> _stages;
        [ObservableProperty] private OrderStage _selectedStage;

        // List of professional colors for quick selection
        [ObservableProperty]
        private ObservableCollection<string> _standardColors = new()
    {
        "#F44336", // Red
        "#E91E63", // Pink
        "#9C27B0", // Purple
        "#2196F3", // Blue
        "#00BCD4", // Cyan
        "#4CAF50", // Green
        "#FFC107", // Amber
        "#FF9800", // Orange
        "#795548", // Brown
        "#607D8B"  // Blue Grey
    };

        public OrderStagesViewModel(OrderStageService stageService)
        {
            _stageService = stageService;
            _ = LoadStages();
        }

        private async Task LoadStages()
        {
            var data = await _stageService.GetAllStagesAsync();
            Stages = new ObservableCollection<OrderStage>(data);
        }

        [RelayCommand]
        private void AddNewStage()
        {
            // Create a new stage and add to the list
            var newStage = new OrderStage
            {
                StageName = "New Stage",
                SequenceOrder = Stages.Count + 1,
                HexColor = "#757575"
            };
            Stages.Add(newStage);

            // Automatically select it so the user can start editing on the right
            SelectedStage = newStage;
        }

        [RelayCommand]
        private async Task SaveSelectedStage()
        {
            if (SelectedStage == null) return;

            bool success = await _stageService.SaveOrUpdateStageAsync(SelectedStage);
            if (success)
            {
                await LoadStages();
            }
        }

        [RelayCommand]
        private async Task DeleteStage(OrderStage stage)
        {
            if (stage == null) return;

            if (MessageBox.Show($"Delete '{stage.StageName}'?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                await _stageService.DeleteStageAsync(stage.Id);
                Stages.Remove(stage);
                SelectedStage = null;
            }
        }

        [RelayCommand]
        private void OpenColorPicker()
        {
            IsColorPickerOpen = true;
        }

        [RelayCommand]
        private void SelectColor(string hex)
        {
            if (SelectedStage != null)
            {
                SelectedStage.HexColor = hex;
            }
            IsColorPickerOpen = false; // Close after selection
        }
    }
}
