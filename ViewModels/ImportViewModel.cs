using CallMan.Interfaces;
using CallMan.Models;
using CallMan.Models.Enums;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Text.RegularExpressions;

namespace CallMan.ViewModels
{
    public partial class ImportViewModel : ObservableObject
    {
        private readonly IImportService _service;

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
            var list = new List<dynamic>();

            using (var workbook = new XLWorkbook(FilePath))
            {
                var sheet = workbook.Worksheet(1);
                var activeMappings = Mappings.Where(m => !string.IsNullOrEmpty(m.SelectedExcelHeader)).ToList();

                // Map header name to column number
                var headerMap = new Dictionary<string, int>();
                var firstRow = sheet.Row(1);
                int colCount = sheet.LastColumnUsed().ColumnNumber();
                for (int c = 1; c <= colCount; c++)
                {
                    headerMap[firstRow.Cell(c).GetValue<string>()] = c;
                }

                int lastRow = sheet.LastRowUsed().RowNumber();

                for (int r = 2; r <= lastRow; r++)
                {
                    var rowData = sheet.Row(r);
                    var rowObj = new System.Dynamic.ExpandoObject() as IDictionary<string, object>;

                    foreach (var m in activeMappings)
                    {
                        int colIndex = headerMap[m.SelectedExcelHeader];

                        // FIX: Use GetValue<object>() or ToString() to extract the primitive value
                        // instead of passing the XLCellValue object directly.
                        var rawValue = rowData.Cell(colIndex).Value;

                        // Convert to a database-friendly type
                        object finalValue = rawValue.IsBlank ? null : rawValue.ToString();

                        rowObj.Add(m.InternalPropertyName, finalValue);
                    }
                    list.Add(rowObj);
                }
            }

            // Now Dapper will receive standard strings/numbers
            int count = await _service.BulkInsertAsync(list, CurrentType);
            StatusMessage = $"Successfully imported {count} records!";
        }

        private void GenerateMappings()
        {
            Mappings.Clear();

            // Using Reflection to get all public properties of your Lead model
            var properties = typeof(Lead).GetProperties();

            foreach (var prop in properties)
            {
                // Skip ID and internal fields
                if (prop.Name.Contains("Id") || prop.Name == "CreatedAt") continue;

                var mapping = new ImportMapping
                {
                    InternalPropertyName = prop.Name,
                    // Formats "CompanyName" to "Company Name" for the UI label
                    DisplayName = Regex.Replace(prop.Name, "([a-z])([A-Z])", "$1 $2")
                };

                // SMART MATCH: Try to auto-select the best header from the Excel file
                // Based on your image, PNAME would auto-match to "FullName"
                mapping.SelectedExcelHeader = ExcelHeaders.FirstOrDefault(h =>
                    h.ToLower().Contains(prop.Name.ToLower()) ||
                    (prop.Name == "FullName" && h.ToLower().Contains("pname")) ||
                    (prop.Name == "CompanyName" && h.ToLower().Contains("firm")));

                // Subscribe to changes so Step 2 (Preview) updates instantly
                mapping.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ImportMapping.SelectedExcelHeader))
                    {
                        PreviewData = GenerateFilteredPreview(FilePath, Mappings);
                    }
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
