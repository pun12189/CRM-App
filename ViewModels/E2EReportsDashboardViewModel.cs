using CallMan.Models.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;

namespace CallMan.ViewModels
{
    public partial class E2EReportsDashboardViewModel : ObservableObject
    {
        // Active Selection Anchors
        [ObservableProperty] private E2EMainFilter _selectedMainFilter = E2EMainFilter.Sales;
        [ObservableProperty] private E2EComparisonTarget _selectedComparisonTarget = E2EComparisonTarget.Customer;

        // Timeline Parameters Context Bounds
        [ObservableProperty] private DateTime _fromDate = DateTime.Now.AddDays(-30);
        [ObservableProperty] private DateTime _toDate = DateTime.Now;

        // Dynamic Columns Source Collection for the UI Grid View
        [ObservableProperty] private ObservableCollection<ReportResultRow> _reportGridSource = new();

        // Control flags to dynamically enable/disable comparison buttons based on your precise rules
        [ObservableProperty] private bool _isCustomerEnabled = true;
        [ObservableProperty] private bool _isLeadHolderEnabled = true;
        [ObservableProperty] private bool _isItemsEnabled = true;
        [ObservableProperty] private bool _isBusinessEnabled = true;
        [ObservableProperty] private bool _isLedgersEnabled = true;
        [ObservableProperty] private bool _isAreasEnabled = true;
        [ObservableProperty] private bool _isVendorsEnabled = false;
        [ObservableProperty] private bool _isPLEnabled = true;

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
            // Triggered when user clicks "Generate" after setting their filter matrix configurations
            // Call your dynamic Dapper/FastReport query service here

            MessageBox.Show($"Generating report for {SelectedMainFilter} compared against {SelectedComparisonTarget} from {FromDate:d} to {ToDate:d}. This may take a moment.", "Report Generation", MessageBoxButton.OK, MessageBoxImage.Information);
            
        }
    }

    public class ReportResultRow
    {
        // Dynamic placeholder model to hold mapped SQL outputs
    }
}
