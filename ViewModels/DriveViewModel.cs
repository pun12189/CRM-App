using Tijori.Data;
using Tijori.Interfaces;
using Tijori.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Tijori.ViewModels
{
    public partial class DriveViewModel : ObservableObject
    {
        private readonly CrmDbContext _context;
        private readonly IUserSession _session;
        private readonly IActionSecurityGuard _securityGuard;

        // Dropdown tracking variables
        public ObservableCollection<KeyValuePair<string, string>> ModuleOptionsList { get; } = new()
        {
            new("Leads", "Lead"),
            new("Customers", "Customer"),
            new("Vendors", "Vendor"),
            new("Staffs", "Staff"),
            new("Purchase", "Purchase"),
            new("Orders", "Order"),
            new("Products", "Product")
        };

        [ObservableProperty] private KeyValuePair<string, string> _selectedModuleFilter;
        [ObservableProperty] private string _searchText = string.Empty;

        // The layout target source that your dynamic XAML container binds to
        [ObservableProperty] private ObservableCollection<DriveCategoryGroup> _iteratedCategoriesCollection = new();

        public DriveViewModel(CrmDbContext context, IUserSession session, IActionSecurityGuard securityGuard)
        {
            _context = context;
            _session = session;
            _securityGuard = securityGuard;

            // Set default view selection block on startup
            SelectedModuleFilter = ModuleOptionsList.First();
        }

        // Automatically executes database refresh pipelines whenever dropdown changes
        partial void OnSelectedModuleFilterChanged(KeyValuePair<string, string> value) => _ = FilterAndLoadDriveDataAsync();

        // Automatically filters the active list as the user types
        partial void OnSearchTextChanged(string value) => _ = FilterAndLoadDriveDataAsync();

        [RelayCommand]
        public async Task FilterAndLoadDriveDataAsync()
        {
            if (string.IsNullOrEmpty(SelectedModuleFilter.Value)) return;

            using var db = _context.CreateConnection();

            // Fetches all categories for the selected module, along with files matching your search filters
            const string sql = @"
                SELECT 
                    mud.DocumentId, mud.ModuleType, mud.CategoryId, bc.CategoryName,
                    mud.OriginalFileName AS FileName, mud.ServerStoragePath AS StoragePath, mud.UploadedBy, mud.UploadedAt, mud.UpdatedAt
                FROM ModuleUploadedDocuments mud
                INNER JOIN BusinessCategories bc ON mud.CategoryId = bc.CategoryId
                WHERE mud.ModuleType = @ModuleKey
                  AND (@Search = '' OR mud.OriginalFileName LIKE @Search OR bc.CategoryName LIKE @Search)
                ORDER BY bc.CategoryName ASC, mud.UploadedAt DESC;";

            var files = (await db.QueryAsync<UploadedDocumentRow>(sql, new
            {
                ModuleKey = SelectedModuleFilter.Value,
                Search = string.IsNullOrWhiteSpace(SearchText) ? "" : $"%{SearchText}%"
            })).ToList();

            App.Current.Dispatcher.Invoke(() =>
            {
                IteratedCategoriesCollection.Clear();

                // Group the matches by category name to drive the iterated view
                var groupedResults = files.GroupBy(f => new { f.CategoryId, f.CategoryName });

                foreach (var group in groupedResults)
                {
                    var categoryBlock = new DriveCategoryGroup
                    {
                        CategoryId = group.Key.CategoryId,
                        CategoryName = group.Key.CategoryName,
                        Documents = new ObservableCollection<UploadedDocumentRow>(group.ToList())
                    };

                    IteratedCategoriesCollection.Add(categoryBlock);
                }
            });
        }

        [RelayCommand]
        private async Task ReplaceDocumentFile(UploadedDocumentRow selectedRow)
        {
            if (selectedRow == null) return;
            var fileDialog = new Microsoft.Win32.OpenFileDialog { Filter = "All Files|*.*" };

            if (fileDialog.ShowDialog() == true)
            {
                try
                {
                    if (System.IO.File.Exists(selectedRow.StoragePath))
                        System.IO.File.Delete(selectedRow.StoragePath);

                    System.IO.File.Copy(fileDialog.FileName, selectedRow.StoragePath, true);

                    using var db = _context.CreateConnection();
                    await db.ExecuteAsync("UPDATE ModuleUploadedDocuments SET UploadedBy = @User, UploadedAt = CURRENT_TIMESTAMP WHERE DocumentId = @Id;",
                        new { User = _session.CurrentUser ?? "Admin", Id = selectedRow.DocumentId });

                    await FilterAndLoadDriveDataAsync();
                }
                catch (Exception ex) { MessageBox.Show($"Replace error: {ex.Message}"); }
            }
        }

        [RelayCommand]
        private async Task DownloadDocumentFile(UploadedDocumentRow selectedRow)
        {
            bool accessGranted = await _securityGuard.IsActionAuthorizedAsync();
            if (!accessGranted) return; // Halt execution path immediately

            if (selectedRow == null || !System.IO.File.Exists(selectedRow.StoragePath)) return;
            var saveDialog = new Microsoft.Win32.SaveFileDialog { FileName = selectedRow.FileName };

            if (saveDialog.ShowDialog() == true)
            {
                System.IO.File.Copy(selectedRow.StoragePath, saveDialog.FileName, true);
            }
        }

        [RelayCommand]
        private async Task DeleteDocumentFile(UploadedDocumentRow selectedRow)
        {
            if (selectedRow == null) return;
            if (MessageBox.Show("Delete this document permanently?", "Confirm Purge", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            if (System.IO.File.Exists(selectedRow.StoragePath))
                System.IO.File.Delete(selectedRow.StoragePath);

            using var db = _context.CreateConnection();
            await db.ExecuteAsync("DELETE FROM ModuleUploadedDocuments WHERE DocumentId = @Id;", new { Id = selectedRow.DocumentId });

            await FilterAndLoadDriveDataAsync();
        }
    }
}
