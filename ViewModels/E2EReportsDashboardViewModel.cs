using CallMan.Models;
using CallMan.Models.Enums;
using CallMan.Services;
using CallMan.Services.Reports;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows;

namespace CallMan.ViewModels
{
    public partial class E2EReportsDashboardViewModel : ObservableObject
    {
        private readonly E2EReportEngine _reportEngine;

        [ObservableProperty] private E2EMainFilter _selectedMainFilter = E2EMainFilter.Sales;

        private E2EComparisonTarget _selectedComparisonTarget = E2EComparisonTarget.Customer;

        public E2EComparisonTarget SelectedComparisonTarget
        {
            get => _selectedComparisonTarget;
            set
            {
                if (SetProperty(ref _selectedComparisonTarget, value))
                {
                    // If your interface rules dictate validation when targets shift directly:
                    EvaluateAvailableComparisonMatrix();
                }
            }
        }

        // Timeline Parameters Context Bounds
        [ObservableProperty] private DateTime _fromDate = DateTime.Now.AddDays(-30);
        [ObservableProperty] private DateTime _toDate = DateTime.Now;

        // Dynamic Columns Source Collection for the UI Grid View
        [ObservableProperty] private DataView? _reportGridSource = new();

        // Control flags to dynamically enable/disable comparison buttons based on your precise rules
        [ObservableProperty] private bool _isCustomerEnabled = true;
        [ObservableProperty] private bool _isLeadHolderEnabled = true;
        [ObservableProperty] private bool _isItemsEnabled = true;
        [ObservableProperty] private bool _isBusinessEnabled = true;
        [ObservableProperty] private bool _isLedgersEnabled = true;
        [ObservableProperty] private bool _isAreasEnabled = true;
        [ObservableProperty] private bool _isVendorsEnabled = false;
        [ObservableProperty] private bool _isPLEnabled = true;


        public E2EReportsDashboardViewModel(E2EReportEngine reportEngine)
        {
            _reportEngine = reportEngine;
            EvaluateAvailableComparisonMatrix();
        }

        partial void OnSelectedMainFilterChanged(E2EMainFilter value)
        {
            EvaluateAvailableComparisonMatrix();
        }

        /// <summary>
        /// Enforces your specific business rules matrix to lock/unlock targets on the fly
        /// </summary>
        private void EvaluateAvailableComparisonMatrix()
        {
            // Reset state controls based on your logic rules mapping matrix
            IsCustomerEnabled = SelectedMainFilter == E2EMainFilter.Sales || SelectedMainFilter == E2EMainFilter.Items || SelectedMainFilter == E2EMainFilter.Staff || SelectedMainFilter == E2EMainFilter.Payments;
            IsLeadHolderEnabled = SelectedMainFilter == E2EMainFilter.Sales || SelectedMainFilter == E2EMainFilter.Items || SelectedMainFilter == E2EMainFilter.Payments;
            IsItemsEnabled = SelectedMainFilter == E2EMainFilter.Sales || SelectedMainFilter == E2EMainFilter.Purchases || SelectedMainFilter == E2EMainFilter.Staff || SelectedMainFilter == E2EMainFilter.Payments;
            IsBusinessEnabled = true; // Business can compare all above filters with customers
            IsLedgersEnabled = SelectedMainFilter != E2EMainFilter.Payments; // Sales, Purchase, Items and Staff
            IsAreasEnabled = true; // Can compare all filters with customers
            IsVendorsEnabled = SelectedMainFilter == E2EMainFilter.Items || SelectedMainFilter == E2EMainFilter.Purchases || SelectedMainFilter == E2EMainFilter.Payments;
            IsPLEnabled = SelectedMainFilter != E2EMainFilter.Payments; // Profit and Loss of items, sales, purchases, and staff

            // Safeguard: If the active selection becomes disabled by a top-tier switch, push it to a safe fallback selection
            VerifyActiveSelectionIntegrity();
        }

        private void VerifyActiveSelectionIntegrity()
        {
            if (SelectedComparisonTarget == E2EComparisonTarget.Customer && !IsCustomerEnabled) SelectedComparisonTarget = E2EComparisonTarget.Business;
            if (SelectedComparisonTarget == E2EComparisonTarget.LeadHolder && !IsLeadHolderEnabled) SelectedComparisonTarget = E2EComparisonTarget.Business;
            if (SelectedComparisonTarget == E2EComparisonTarget.Vendors && !IsVendorsEnabled) SelectedComparisonTarget = E2EComparisonTarget.Business;
            if (SelectedComparisonTarget == E2EComparisonTarget.PL && !IsPLEnabled) SelectedComparisonTarget = E2EComparisonTarget.Business;
        }

        [RelayCommand]
        private async Task GenerateE2EReportAsync()
        {
            try
            {
                // 1. Pop up your global freeze-proof loader overlay cleanly
                LoadingService.Show($"Assembling matrix report data layout for {SelectedMainFilter} x {SelectedComparisonTarget}...");

                // Yield the execution context briefly so the spinner paints smoothly before the database queries execute
                await Task.Delay(60);

                // 2. Query the engine asynchronously on the database worker threads pool
                DataTable reportData = await _reportEngine.ExecuteMatrixQueryAsync(SelectedMainFilter, SelectedComparisonTarget, FromDate, ToDate);

                App.Current.Dispatcher.Invoke(() =>
                {
                    // Binding to the DefaultView forces the UI DataGrid to re-generate structural grid headers
                    ReportGridSource = reportData.DefaultView;
                });
            }
            catch (NotSupportedException ex)
            {
                ReportGridSource = null;
                MessageBox.Show(ex.Message, "Matrix Formula Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An unexpected analytical error occurred while pulling report records: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingService.Hide();
            }
        }
    }
}
