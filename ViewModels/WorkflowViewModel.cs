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
using Tijori.Interfaces;
using Tijori.Models;

namespace Tijori.ViewModels
{
    public partial class WorkflowViewModel : ObservableObject
    {
        private readonly IWorkflowDataService _dataService;

        [ObservableProperty] private ObservableCollection<Workflow> _workflows = new();
        [ObservableProperty] private bool _isLoading;

        public WorkflowViewModel(IWorkflowDataService dataService)
        {
            _dataService = dataService;
            _ = LoadWorkflowsAsync();
        }

        [RelayCommand]
        public async Task LoadWorkflowsAsync()
        {
            IsLoading = true;
            var list = await _dataService.GetAllWorkflowsAsync();
            Workflows = new ObservableCollection<Workflow>(list);
            IsLoading = false;
        }

        [RelayCommand]
        private async Task OpenCreateDialogAsync()
        {
            var dialogVm = new CreateWorkflowDialogViewModel(_dataService);
            var dialog = new CreateWorkflowDialogWindow
            {
                DataContext = dialogVm,
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                await LoadWorkflowsAsync();
            }
        }

        [RelayCommand]
        private async Task EditWorkflowAsync(Workflow? item)
        {
            if (item == null) return;
            var dialogVm = new CreateWorkflowDialogViewModel(_dataService, item);
            var dialog = new CreateWorkflowDialogWindow
            {
                DataContext = dialogVm,
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                await LoadWorkflowsAsync();
            }
        }

        [RelayCommand]
        private async Task DeleteWorkflowAsync(Workflow? item)
        {
            if (item == null) return;

            var confirm = MessageBox.Show($"Delete automation rule '{item.WorkflowName}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm == MessageBoxResult.Yes)
            {
                await _dataService.DeleteWorkflowAsync(item.Id);
                await LoadWorkflowsAsync();
            }
        }
    }
}
