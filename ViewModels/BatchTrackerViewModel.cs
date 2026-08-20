using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using Tijori.Dialogs;
using Tijori.Models;
using Tijori.Services;

namespace Tijori.ViewModels
{
    public partial class BatchTrackerViewModel : ObservableObject
    {
        private readonly BatchTrackerService _trackerService;

        // Navigation State (False = Kanban/List, True = Execution Workspace)
        [ObservableProperty] private bool _isBatchWorkspaceOpen;
        [ObservableProperty] private string _selectedStageFilter = "All";
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private int _totalBatchesCount;

        // Collections
        [ObservableProperty] private ObservableCollection<ProductionWorkOrder> _batchesList = new();
        public ICollectionView FilteredBatches { get; private set; } = null!;

        // Selected Batch Details for Workspace
        [ObservableProperty] private ProductionWorkOrder? _activeBatch;
        [ObservableProperty] private ObservableCollection<WorkOrderBomItem> _activeBatchBOM = new();
        [ObservableProperty] private WorkOrderStage? _currentActiveStage;
        [ObservableProperty] private string _stageRemarksEntry = string.Empty;

        // Metrics
        public decimal TotalActiveBOMPercentage => ActiveBatchBOM.Sum(x => x.PercentageValue);
        public decimal TotalBatchWeightKg => ActiveBatchBOM.Sum(x => x.CalculatedQuantity);

        public BatchTrackerViewModel(BatchTrackerService trackerService)
        {
            _trackerService = trackerService;
            _ = LoadBatchesAsync();
        }

        public async Task LoadBatchesAsync()
        {
            var list = (await _trackerService.GetAllBatchesAsync(SelectedStageFilter)).ToList();

            App.Current.Dispatcher.Invoke(() =>
            {
                BatchesList = new ObservableCollection<ProductionWorkOrder>(list);
                TotalBatchesCount = BatchesList.Count;

                FilteredBatches = CollectionViewSource.GetDefaultView(BatchesList);
                FilteredBatches.Filter = FilterBatchesItem;
                OnPropertyChanged(nameof(FilteredBatches));
            });
        }

        partial void OnSearchTextChanged(string value) => FilteredBatches?.Refresh();
        async partial void OnSelectedStageFilterChanged(string value) => await LoadBatchesAsync();

        private bool FilterBatchesItem(object obj)
        {
            if (obj is not ProductionWorkOrder item) return false;
            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            var term = SearchText.Trim();
            return (item.BatchNumber != null && item.BatchNumber.Contains(term, StringComparison.OrdinalIgnoreCase))
                || (item.BrandName != null && item.BrandName.Contains(term, StringComparison.OrdinalIgnoreCase))
                || (item.CustomerName != null && item.CustomerName.Contains(term, StringComparison.OrdinalIgnoreCase))
                || (item.CurrentStage != null && item.CurrentStage.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        // ==========================================
        // 🌟 WORKSPACE NAVIGATION & SIGN-OFF
        // ==========================================
        [RelayCommand]
        private async Task OpenBatchWorkspace(ProductionWorkOrder? item)
        {
            if (item == null) return;

            var fullBatch = await _trackerService.GetBatchDetailsAsync(item.WorkOrderId);
            if (fullBatch == null) return;

            var bomItems = await _trackerService.GetBatchBOMAsync(item.WorkOrderId);

            ActiveBatch = fullBatch;
            ActiveBatchBOM = new ObservableCollection<WorkOrderBomItem>(bomItems);
            CurrentActiveStage = fullBatch.Stages.FirstOrDefault(s => s.Status == "InProgress")
                ?? fullBatch.Stages.LastOrDefault();
            StageRemarksEntry = string.Empty;

            OnPropertyChanged(nameof(TotalActiveBOMPercentage));
            OnPropertyChanged(nameof(TotalBatchWeightKg));

            IsBatchWorkspaceOpen = true;
        }

        [RelayCommand]
        private void CloseBatchWorkspace()
        {
            IsBatchWorkspaceOpen = false;
            ActiveBatch = null;
            ActiveBatchBOM.Clear();
            CurrentActiveStage = null;
        }

        [RelayCommand]
        private async Task AdvanceCurrentStageAsync()
        {
            if (ActiveBatch == null || CurrentActiveStage == null) return;

            if (CurrentActiveStage.Status == "Completed")
            {
                MessageBox.Show("This batch has already completed all manufacturing stages.", "Batch Completed", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Sign off and complete stage '{CurrentActiveStage.StageName}'?\n\nThe batch will advance to the next stage.",
                "Confirm Stage Completion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                await _trackerService.AdvanceBatchStageAsync(ActiveBatch.WorkOrderId, CurrentActiveStage.StageId, StageRemarksEntry);

                // Refresh workspace details
                await OpenBatchWorkspace(ActiveBatch);
                await LoadBatchesAsync();

                MessageBox.Show("Stage completed and batch advanced successfully!", "Stage Updated", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error advancing stage: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void PrintBmrDocument()
        {
            if (ActiveBatch == null) return;

            try
            {
                var doc = _trackerService.CreateBmrFlowDocument(ActiveBatch, ActiveBatchBOM);
                
                var previewWin = new PrintPreviewWindow
                {
                    Owner = Application.Current.MainWindow
                };

                previewWin.LoadFlowDocument(doc, $"BMR - {ActiveBatch.BatchNumber}");
                previewWin.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error printing BMR sheet: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
