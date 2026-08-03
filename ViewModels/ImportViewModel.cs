using Tijori.Interfaces;
using Tijori.Models;
using Tijori.Models.Enums;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tijori.ViewModels
{
    public partial class ImportViewModel : ObservableObject
    {
        private readonly IImportService _service;
        private const string SkipToken = "[ Skip Column ]";

        [ObservableProperty] private ImportType _currentType;
        [ObservableProperty] private string _filePath;
        [ObservableProperty] private bool _isMappingVisible;
        [ObservableProperty] private string _statusMessage;

        public ObservableCollection<string> ExcelHeaders { get; } = new();
        public ObservableCollection<ImportMapping> Mappings { get; } = new();
        [ObservableProperty] private DataTable _previewData;

        // Event to close the window from ViewModel
        public event Action<bool>? RequestClose;

        public ImportViewModel(IImportService service)
        {
            _service = service;
        }

        public async Task InitializeAsync(ImportType type)
        {
            CurrentType = type;
        }

        [RelayCommand]
        private async Task BrowseAndLoad()
        {
            var openFile = new Microsoft.Win32.OpenFileDialog { Filter = "Excel Files|*.xlsx" };
            if (openFile.ShowDialog() == true)
            {
                FilePath = openFile.FileName;
                await LoadExcelContext();
            }
        }

        private async Task LoadExcelContext()
        {
            try
            {
                using (var workbook = new XLWorkbook(FilePath))
                {
                    var sheet = workbook.Worksheet(1);

                    // 1. Get Headers
                    var headers = sheet.Row(1).CellsUsed().Select(c => c.GetValue<string>()).ToList();

                    // 2. Refresh the ObservableCollections on the UI Thread
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        ExcelHeaders.Clear();
                        foreach (var h in headers) ExcelHeaders.Add(h);

                        // 3. Generate the Mapping Rows based on your Lead/Product Class
                        GenerateMappings();

                        // 5. Trigger Visibility
                        IsMappingVisible = Mappings.Count > 0;
                    });
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Error: " + ex.Message;
            }
        }

        [RelayCommand]
        private async Task StartImport()
        {
            try
            {
                var payloadList = new List<Dictionary<string, object>>();

                // Find columns explicitly matched by the user or the auto-matcher
                var activeFieldMappings = Mappings.Where(m => !string.IsNullOrEmpty(m.SelectedExcelHeader)).ToList();

                // Create a fast lookup hash set of all explicitly claimed spreadsheet columns
                var claimedExcelHeaders = activeFieldMappings.Select(m => m.SelectedExcelHeader).ToHashSet();

                using (var workbook = new XLWorkbook(FilePath))
                {
                    var sheet = workbook.Worksheet(1);
                    int lastRow = sheet.LastRowUsed().RowNumber();

                    // Get every column header actually present in this spreadsheet file
                    var totalExcelColumns = sheet.Row(1).CellsUsed().Select(c => c.GetValue<string>().Trim()).ToList();

                    for (int r = 2; r <= lastRow; r++)
                    {
                        var rowData = sheet.Row(r);
                        var dbRow = new Dictionary<string, object>();
                        var metadataPool = new Dictionary<string, string>();

                        // STEP A: Map standard target destination parameters
                        foreach (var m in activeFieldMappings)
                        {
                            var cellValue = rowData.Cell(totalExcelColumns.IndexOf(m.SelectedExcelHeader) + 1).Value;
                            dbRow[m.InternalPropertyName] = cellValue.IsBlank ? null : cellValue.ToString().Trim();
                        }

                        // STEP B: AUTOMATED EXTRA CATCH-ALL RULE
                        // Loop through all spreadsheet columns. If a column wasn't claimed above, 
                        // pack it straight into the metadata dictionary automatically.
                        foreach (var header in totalExcelColumns)
                        {
                            if (!claimedExcelHeaders.Contains(header))
                            {
                                var cellValue = rowData.Cell(totalExcelColumns.IndexOf(header) + 1).Value;
                                dbRow[header] = cellValue.IsBlank ? null : cellValue.ToString().Trim();
                            }
                        }

                        // Append the processed JSON string directly into the query payload parameter slot
                        //dbRow["MetadataJson"] = metadataPool.Count > 0 ? JsonSerializer.Serialize(metadataPool) : null;
                        payloadList.Add(dbRow);
                    }
                }

                int count = await _service.BulkInsertAsync(payloadList, CurrentType);
                StatusMessage = $"Import completed! {count} records successfully committed.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Import Failure: " + ex.Message;
            }
        }

        private void GenerateMappings()
        {
            Mappings.Clear();

            // 1. Resolve your standard entity schemas dynamically
            var targetProperties = CurrentType switch
            {
                ImportType.Lead => new Dictionary<string, (MappingTargetType Type, string Table, string IdCol)>
                {
                    { "CustomerName", (MappingTargetType.StandardField, null, null) },
                    { "Email", (MappingTargetType.StandardField, null, null) },
                    { "Phone", (MappingTargetType.StandardField, null, null) },
                    { "AltPhone", (MappingTargetType.StandardField, null, null) },
                    { "CompanyName", (MappingTargetType.StandardField, null, null) },
                    { "AddressLine", (MappingTargetType.StandardField, null, null) },
                    { "City", (MappingTargetType.StandardField, null, null) },
                    { "District", (MappingTargetType.StandardField, null, null) },
                    { "State", (MappingTargetType.StandardField, null, null) },
                    { "Pincode", (MappingTargetType.StandardField, null, null) },
                    { "Country", (MappingTargetType.StandardField, null, null) },
                    { "MonthlyTarget", (MappingTargetType.StandardField, null, null) },
                    { "WorkingArea", (MappingTargetType.StandardField, null, null) },
                    { "LeadSource", (MappingTargetType.ForeignKeyLookup, "LeadSources", "Id") },
                    { "LeadTag", (MappingTargetType.ForeignKeyLookup, "LeadTags", "Id") },
                    { "LeadHolder", (MappingTargetType.ForeignKeyLookup, "Users", "UserId") },
                    { "FollowupStage", (MappingTargetType.ForeignKeyLookup, "LeadStatuses", "Id") }

                },
                ImportType.Product => new Dictionary<string, (MappingTargetType Type, string Table, string IdCol)>
                {
                    // Core Product Structural Fields
                    { "Name", (MappingTargetType.StandardField, null, null) },
                    { "ShortName", (MappingTargetType.StandardField, null, null) },
                    { "SKU", (MappingTargetType.StandardField, null, null) },
                    { "Unit", (MappingTargetType.StandardField, null, null) },
                    { "Manufacturer", (MappingTargetType.StandardField, null, null) },
                    { "BrandName", (MappingTargetType.StandardField, null, null) },
                    { "Packaging", (MappingTargetType.StandardField, null, null) },
                    { "InitialStock", (MappingTargetType.StandardField, null, null) },
                    { "MRP", (MappingTargetType.StandardField, null, null) },
                    { "CostPrice", (MappingTargetType.StandardField, null, null) },
                    { "SellingPrice", (MappingTargetType.StandardField, null, null) },
                    { "GSTPercent", (MappingTargetType.StandardField, null, null) },
                    { "TotalCost", (MappingTargetType.StandardField, null, null) },
    
                    // Relational Category text-to-ID lookup rule
                    { "CategoryName", (MappingTargetType.ForeignKeyLookup, "Categories", "CategoryId") },

                    // Parallel Batch Tracking Fields (Auto-inserted into child ProductBatches table)
                    { "BatchNumber", (MappingTargetType.StandardField, null, null) },
                    { "MfgDate", (MappingTargetType.StandardField, null, null) },
                    { "ExpiryDate", (MappingTargetType.StandardField, null, null) }
                },
                ImportType.Order => new Dictionary<string, (MappingTargetType Type, string Table, string IdCol)>
                {
                    // Parent Order Core Configuration Fields
                    { "InvoiceNumber", (MappingTargetType.StandardField, null, null) }, // Automatically maps to 'VCN'
                    { "OrderDate", (MappingTargetType.StandardField, null, null) },      // Automatically maps to 'C_DATE'
                    { "OrderType", (MappingTargetType.StandardField, null, null) },      // Automatically maps to 'TYPE2'
    
                    // Relational Connection Lookups
                    { "CustomerName", (MappingTargetType.ForeignKeyLookup, "Leads", "LeadId") }, // Maps to 'PNAME'
                    { "ProcessedBy", (MappingTargetType.ForeignKeyLookup, "Users", "UserId") },   // Maps to 'SALESMEN'

                    // Child Line-Item Attributes (OrderItems)
                    { "ProductName", (MappingTargetType.StandardField, null, null) },    // Maps to 'NAME'
                    { "BatchNumber", (MappingTargetType.StandardField, null, null) },    // Maps to 'BATCH'
                    { "Quantity", (MappingTargetType.StandardField, null, null) },       // Maps to 'QTY'
                    { "FreeQuantity", (MappingTargetType.StandardField, null, null) },   // Maps to 'FREE'
                    { "UnitPrice", (MappingTargetType.StandardField, null, null) },      // Maps to 'RATE'
                    { "GSTPercent", (MappingTargetType.StandardField, null, null) },     // Maps to 'GST'
                    { "GstAmount", (MappingTargetType.StandardField, null, null) },       // Maps to 'TAXAMT'
                    { "Total", (MappingTargetType.StandardField, null, null) },          // Maps to 'AMOUNT'

                    // Fallback tracking variables
                    { "BrandName", (MappingTargetType.StandardField, null, null) },      // Maps to 'COMPANY'
                    { "AmountReceived", (MappingTargetType.StandardField, null, null) }
                },
                _ => throw new NotImplementedException()
            };

            // 2. Build the UI rows. If a property can't auto-match an Excel header, 
            // it leaves SelectedExcelHeader as null (ignored during standard insert).
            foreach (var prop in targetProperties)
            {
                var mapping = new ImportMapping
                {
                    InternalPropertyName = prop.Key,
                    DisplayName = Regex.Replace(prop.Key, "([a-z])([A-Z])", "$1 $2"),
                    TargetType = prop.Value.Type,
                    LookupTableName = prop.Value.Table,
                    LookupIdColumn = prop.Value.IdCol
                };

                // Strict auto-matching loop
                mapping.SelectedExcelHeader = ExcelHeaders.FirstOrDefault(h =>
                    h.Equals(mapping.InternalPropertyName, StringComparison.OrdinalIgnoreCase) ||
                    h.Equals(mapping.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                    (mapping.InternalPropertyName == "CustomerName" && h.ToLower().Contains("name")) ||
                    (mapping.InternalPropertyName == "CompanyName" && h.ToLower().Contains("firm")));

                mapping.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ImportMapping.SelectedExcelHeader))
                        PreviewData = GenerateFilteredPreview(FilePath, Mappings);
                };

                Mappings.Add(mapping);
            }
        }

        private DataTable GenerateFilteredPreview(string filePath, ObservableCollection<ImportMapping> mappings)
        {
            DataTable dt = new DataTable();

            using (var workbook = new XLWorkbook(filePath))
            {
                var sheet = workbook.Worksheet(1);
                var firstRow = sheet.Row(1);

                // 1. Get only the mappings where a header has been selected
                var activeMappings = mappings.Where(m => !string.IsNullOrEmpty(m.SelectedExcelHeader)).ToList();

                if (!activeMappings.Any())
                {
                    // If nothing is mapped, show all headers but 0 rows to keep it clean
                    foreach (var header in ExcelHeaders) dt.Columns.Add(header);
                    return dt;
                }

                // 2. Add columns to DataTable based on User's Mapping (Internal Names)
                foreach (var m in activeMappings)
                {
                    dt.Columns.Add(m.DisplayName); // Show "Product Name" instead of "PNAME"
                }

                // 3. Load only the top 5 rows for performance
                int lastRow = Math.Min(sheet.LastRowUsed().RowNumber(), 6);

                for (int r = 2; r <= lastRow; r++)
                {
                    var rowData = sheet.Row(r);
                    DataRow dr = dt.NewRow();

                    for (int i = 0; i < activeMappings.Count; i++)
                    {
                        var mapping = activeMappings[i];
                        // Find the column index in the original Excel
                        int excelColIndex = ExcelHeaders.IndexOf(mapping.SelectedExcelHeader) + 1;
                        dr[i] = rowData.Cell(excelColIndex).Value;
                    }
                    dt.Rows.Add(dr);
                }
            }
            return dt;
        }
    }
}
