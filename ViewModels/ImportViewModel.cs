using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Tijori.Interfaces;
using Tijori.Models;
using Tijori.Models.Enums;
using Tijori.Services;

namespace Tijori.ViewModels
{
    public partial class ImportViewModel : ObservableObject
    {
        private readonly IImportService _importService;
        private readonly CustomFieldService _customFieldService;

        // UI State
        [ObservableProperty] private ImportType _currentType;
        [ObservableProperty] private string _filePath = string.Empty;
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _loadingMessage = "Reading Excel file...";
        [ObservableProperty] private bool _isMappingVisible;
        [ObservableProperty] private string _statusMessage = string.Empty;

        public ObservableCollection<ImportMappingProfile> SavedProfiles { get; } = new();

        [ObservableProperty] private ImportMappingProfile? _selectedProfile;
        [ObservableProperty] private string _newProfileName = string.Empty;
        [ObservableProperty] private bool _isSaveProfileDialogOpen;

        // Collections
        public ObservableCollection<string> AllExcelHeaders { get; } = new();
        public ObservableCollection<ImportMappingRow> Mappings { get; } = new();

        [ObservableProperty] private string? _selectedUnmappedExcelHeader;

        public ObservableCollection<SystemFieldDescriptor> AvailableUnmappedTier2Fields { get; } = new();
        public ObservableCollection<string> UnmappedExcelHeaders { get; } = new();
        public List<CustomFieldDefinition> AllModuleTier2Fields { get; } = new();

        [ObservableProperty] private SystemFieldDescriptor? _selectedTier2TargetField;

        private List<SystemFieldDescriptor> _masterTier2Fields = new();

        [ObservableProperty] private bool _isCreateTier3DialogOpen;
        [ObservableProperty] private string _newTier3FieldName = string.Empty;
        [ObservableProperty] private string _newTier3DisplayLabel = string.Empty;
        [ObservableProperty] private string _selectedTier3FieldType = "Textbox";
        [ObservableProperty] private string _newTier3Tooltip = string.Empty;

        public List<string> SupportedControlTypes { get; } = new() { "Textbox", "TextArea", "DropdownSingle", "DropdownMultiple", "CalendarClock" };

        private bool _isRefreshingHeaders;

        public event Action<bool>? RequestClose;

        public ImportViewModel(IImportService importService, CustomFieldService customFieldService)
        {
            _importService = importService;
            _customFieldService = customFieldService;
        }

        public async Task InitializeAsync(ImportType type)
        {
            CurrentType = type;
            await LoadSavedProfilesAsync();
            LoadHardcodedTier2Fields(type);
        }

        /// <summary>
        /// Scans top rows of an Excel worksheet and returns the 1-based index of the actual Header Row.
        /// </summary>
        private int DetectHeaderRowIndex(IXLWorksheet sheet, List<string> knownKeywords)
        {
            int maxScanRows = Math.Min(25, sheet.LastRowUsed()?.RowNumber() ?? 1);
            int bestHeaderRowIndex = 1;
            int highestScore = -1;

            for (int r = 1; r <= maxScanRows; r++)
            {
                var row = sheet.Row(r);
                var cells = row.CellsUsed().ToList();

                // Skip completely empty rows
                if (!cells.Any()) continue;

                int filledColumnCount = cells.Count;
                int keywordMatchCount = 0;
                int stringCellCount = 0;

                foreach (var cell in cells)
                {
                    string val = cell.GetValue<string>().Trim();
                    if (string.IsNullOrEmpty(val)) continue;

                    stringCellCount++;

                    // Check if cell matches common header terms
                    if (knownKeywords.Any(k => val.Equals(k, StringComparison.OrdinalIgnoreCase) ||
                                               val.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    {
                        keywordMatchCount += 2; // Extra weight for matching system keywords
                    }
                }

                // Calculate a score: More distinct non-empty string columns + keyword matches = True Header Row
                int currentScore = (filledColumnCount * 2) + stringCellCount + (keywordMatchCount * 3);

                // Header rows typically have at least 3 distinct populated columns
                if (filledColumnCount >= 3 && currentScore > highestScore)
                {
                    highestScore = currentScore;
                    bestHeaderRowIndex = r;
                }
            }

            return bestHeaderRowIndex;
        }

        /// <summary>
        /// Loads the hardcoded Tier 2 fields based on the active ImportType.
        /// </summary>
        private void LoadHardcodedTier2Fields(ImportType type)
        {
            // Convert Enum to string matching GetStandardModelProperties
            string moduleType = type.ToString();

            _masterTier2Fields = moduleType switch
            {
                "Lead" => new List<SystemFieldDescriptor>
        {
            new() { FieldName = "Email", DisplayLabel = "Email", InfoTooltip = "Email address" },
            new() { FieldName = "AltPhone", DisplayLabel = "Alternate Phone", InfoTooltip = "Secondary phone number" },
            new() { FieldName = "CompanyName", DisplayLabel = "Company Name", InfoTooltip = "Business/Organization name" },
            new() { FieldName = "AddressLine", DisplayLabel = "Address Line", InfoTooltip = "Street or locality address" },
            new() { FieldName = "Pincode", DisplayLabel = "Pincode", InfoTooltip = "Postal zip code" },
            new() { FieldName = "City", DisplayLabel = "City", InfoTooltip = "City name" },
            new() { FieldName = "District", DisplayLabel = "District", InfoTooltip = "District name" },
            new() { FieldName = "State", DisplayLabel = "State", InfoTooltip = "State name" },
            new() { FieldName = "Country", DisplayLabel = "Country", InfoTooltip = "Country name (Default: India)" },
            new() { FieldName = "BestTimeToTalk", DisplayLabel = "Best Time To Talk", InfoTooltip = "Preferred contact call window" },
            new() { FieldName = "DOB", DisplayLabel = "DOB", InfoTooltip = "Date of birth" },
            new() { FieldName = "Anniversary", DisplayLabel = "Anniversary", InfoTooltip = "Anniversary date" },
            new() { FieldName = "DivisionId", DisplayLabel = "Division ID", InfoTooltip = "Assigned division ID" },
            new() { FieldName = "LeadSourceId", DisplayLabel = "Lead Source ID", InfoTooltip = "Source lookup ID" },
            new() { FieldName = "LeadTagIds", DisplayLabel = "Lead Tag IDs", InfoTooltip = "Associated tag lookup IDs" },
            new() { FieldName = "LeadLabelIds", DisplayLabel = "Lead Label IDs", InfoTooltip = "Associated label lookup IDs" }
        },

                "Customer" => new List<SystemFieldDescriptor>
        {
            new() { FieldName = "Email", DisplayLabel = "Email", InfoTooltip = "Email address" },
            new() { FieldName = "AltPhone", DisplayLabel = "Alternate Phone", InfoTooltip = "Secondary phone number" },
            new() { FieldName = "CompanyName", DisplayLabel = "Company Name", InfoTooltip = "Business/Organization name" },
            new() { FieldName = "AddressLine", DisplayLabel = "Address Line", InfoTooltip = "Street or locality address" },
            new() { FieldName = "Pincode", DisplayLabel = "Pincode", InfoTooltip = "Postal zip code" },
            new() { FieldName = "City", DisplayLabel = "City", InfoTooltip = "City name" },
            new() { FieldName = "District", DisplayLabel = "District", InfoTooltip = "District name" },
            new() { FieldName = "State", DisplayLabel = "State", InfoTooltip = "State name" },
            new() { FieldName = "Country", DisplayLabel = "Country", InfoTooltip = "Country name (Default: India)" },
            new() { FieldName = "WorkingArea", DisplayLabel = "Working Area", InfoTooltip = "Operational or service territory" },
            new() { FieldName = "MonthlyTarget", DisplayLabel = "Monthly Target", InfoTooltip = "Target sales revenue" },
            new() { FieldName = "BestTimeToTalk", DisplayLabel = "Best Time To Talk", InfoTooltip = "Preferred contact call window" },
            new() { FieldName = "DOB", DisplayLabel = "DOB", InfoTooltip = "Date of birth" },
            new() { FieldName = "Anniversary", DisplayLabel = "Anniversary", InfoTooltip = "Anniversary date" },
            new() { FieldName = "DivisionId", DisplayLabel = "Division ID", InfoTooltip = "Assigned division ID" },
            new() { FieldName = "LeadSourceId", DisplayLabel = "Lead Source ID", InfoTooltip = "Source lookup ID" },
            new() { FieldName = "LeadTagIds", DisplayLabel = "Lead Tag IDs", InfoTooltip = "Associated tag lookup IDs" },
            new() { FieldName = "LeadLabelIds", DisplayLabel = "Lead Label IDs", InfoTooltip = "Associated label lookup IDs" }
        },

                "Product" => new List<SystemFieldDescriptor>
        {
            new() { FieldName = "ShortName", DisplayLabel = "Short Name", InfoTooltip = "Abbreviated item name" },
            new() { FieldName = "SKU", DisplayLabel = "SKU", InfoTooltip = "Stock keeping unit code" },
            new() { FieldName = "Unit", DisplayLabel = "Unit", InfoTooltip = "Measurement unit (e.g., Pcs, Box, Kg)" },
            new() { FieldName = "CategoryId", DisplayLabel = "Category ID", InfoTooltip = "Product category lookup ID" },
            new() { FieldName = "BrandName", DisplayLabel = "Brand Name", InfoTooltip = "Brand / Manufacturer label" },
            new() { FieldName = "Manufacturer", DisplayLabel = "Manufacturer", InfoTooltip = "Product manufacturer" },
            new() { FieldName = "Packaging", DisplayLabel = "Packaging", InfoTooltip = "Packaging specifications" },
            new() { FieldName = "InitialStock", DisplayLabel = "Initial Stock", InfoTooltip = "Starting inventory quantity" },
            new() { FieldName = "CostPrice", DisplayLabel = "Cost Price", InfoTooltip = "Purchase cost per item" },
            new() { FieldName = "MRP", DisplayLabel = "MRP", InfoTooltip = "Maximum retail price" },
            new() { FieldName = "SellingPrice", DisplayLabel = "Selling Price", InfoTooltip = "Base selling price" },
            new() { FieldName = "GSTPercent", DisplayLabel = "GST Percent", InfoTooltip = "Tax percentage slab" },
            new() { FieldName = "BatchNumber", DisplayLabel = "Batch Number", InfoTooltip = "Manufacturing batch lot code" },
            new() { FieldName = "MfgDate", DisplayLabel = "Mfg Date", InfoTooltip = "Manufacturing date" },
            new() { FieldName = "ExpiryDate", DisplayLabel = "Expiry Date", InfoTooltip = "Expiration date" }
        },

                "Vendor" => new List<SystemFieldDescriptor>
        {
            new() { FieldName = "ContactPerson", DisplayLabel = "Contact Person", InfoTooltip = "Primary vendor contact" },
            new() { FieldName = "Email", DisplayLabel = "Email", InfoTooltip = "Vendor email address" },
            new() { FieldName = "GstNumber", DisplayLabel = "GST Number", InfoTooltip = "GSTIN registration number" },
            new() { FieldName = "Address", DisplayLabel = "Address", InfoTooltip = "Vendor location address" },
            new() { FieldName = "Status", DisplayLabel = "Status", InfoTooltip = "Vendor status (e.g., Active/Inactive)" }
        },

                "Staff" => new List<SystemFieldDescriptor>
        {
                    new() { FieldName = "Username", DisplayLabel = "Username", InfoTooltip = "Short handle or username for quick system login" },
            new() { FieldName = "Phone", DisplayLabel = "Phone Number", InfoTooltip = "Mobile phone number" },
            new() { FieldName = "DepartmentId", DisplayLabel = "Department ID", InfoTooltip = "Assigned department ID" },
            new() { FieldName = "SeniorId", DisplayLabel = "Reporting Manager / Senior ID", InfoTooltip = "Manager/Senior staff lookup ID" },
            new() { FieldName = "MonthlyTarget", DisplayLabel = "Monthly Target", InfoTooltip = "Monthly sales allocation" },
            new() { FieldName = "IsActive", DisplayLabel = "Is Active", InfoTooltip = "Account active status (1/0)" }
                },

                "Order" => new List<SystemFieldDescriptor>
        {
            new() { FieldName = "InvoiceNumber", DisplayLabel = "Invoice Number", InfoTooltip = "Sales invoice / VCN code" },
            new() { FieldName = "ProformaNumber", DisplayLabel = "Proforma Number", InfoTooltip = "Proforma invoice reference" },
            new() { FieldName = "OrderType", DisplayLabel = "Order Type", InfoTooltip = "Type (e.g., Sale, Return, Quotation)" },
            new() { FieldName = "PaymentStatus", DisplayLabel = "Payment Status", InfoTooltip = "Paid, Unpaid, or Partially Paid" },
            new() { FieldName = "AmountPaid", DisplayLabel = "Amount Paid", InfoTooltip = "Upfront payment amount received" },
            new() { FieldName = "ProcessedBy", DisplayLabel = "Processed By", InfoTooltip = "Staff user who processed the order" },
            new() { FieldName = "LeadHolder", DisplayLabel = "Lead Holder", InfoTooltip = "Assigned lead owner" },
            new() { FieldName = "PreferedTransport", DisplayLabel = "Preferred Transport", InfoTooltip = "Logistics or courier transport provider" },
            new() { FieldName = "Status", DisplayLabel = "Status", InfoTooltip = "Order status (Pending, Completed)" },
            new() { FieldName = "Remarks", DisplayLabel = "Remarks", InfoTooltip = "Additional order notes" },
            new() { FieldName = "Description", DisplayLabel = "Description", InfoTooltip = "Detailed order description" },
            new() { FieldName = "TotalCostAmount", DisplayLabel = "Total Cost Amount", InfoTooltip = "Total COGS / Cost amount" },
            new() { FieldName = "DivisionId", DisplayLabel = "Division ID", InfoTooltip = "Assigned division ID" },
            new() { FieldName = "BatchId", DisplayLabel = "Batch ID", InfoTooltip = "Product batch lot ID" },
            new() { FieldName = "BatchNumber", DisplayLabel = "Batch Number", InfoTooltip = "Batch lot number string" },
            new() { FieldName = "ExpiryDate", DisplayLabel = "Expiry Date", InfoTooltip = "Item lot expiry date" },
            new() { FieldName = "CostPrice", DisplayLabel = "Cost Price", InfoTooltip = "Unit item cost price" },
            new() { FieldName = "GSTPercent", DisplayLabel = "GST Percent", InfoTooltip = "Line item GST tax slab percentage" },
            new() { FieldName = "SubTotal", DisplayLabel = "Sub Total", InfoTooltip = "Line subtotal without tax" },
            new() { FieldName = "GstAmount", DisplayLabel = "GST Amount", InfoTooltip = "Line item tax amount" },
            new() { FieldName = "Total", DisplayLabel = "Line Total", InfoTooltip = "Line item total amount including tax" },
            new() { FieldName = "ChargeName", DisplayLabel = "Charge Name", InfoTooltip = "Extra charge name (e.g. Freight)" },
            new() { FieldName = "ChargeAmount", DisplayLabel = "Charge Amount", InfoTooltip = "Extra charge numeric amount" }
        },

                "Purchase" => new List<SystemFieldDescriptor>
        {
            new() { FieldName = "ExpectedDeliveryDate", DisplayLabel = "Expected Delivery Date", InfoTooltip = "Target delivery schedule date" },
            new() { FieldName = "ActualDeliveryDate", DisplayLabel = "Actual Delivery Date", InfoTooltip = "Actual received date" },
            new() { FieldName = "OrderStatus", DisplayLabel = "Order Status", InfoTooltip = "Purchase order fulfillment status" },
            new() { FieldName = "CreatedBy", DisplayLabel = "Created By", InfoTooltip = "User who raised the PO" },
            new() { FieldName = "SupplierSku", DisplayLabel = "Supplier SKU", InfoTooltip = "Vendor's item code / SKU" },
            new() { FieldName = "TotalCost", DisplayLabel = "Total Cost", InfoTooltip = "Total PO cost valuation" }
        },

                _ => new List<SystemFieldDescriptor>()
            };
        }

        /// <summary>
        /// Loads saved profiles for the active module type.
        /// </summary>
        public async Task LoadSavedProfilesAsync()
        {
            // Ensure moduleType matches the exact string saved in DB ("Lead", "Product", "Order")
            string moduleType = CurrentType switch
            {
                ImportType.Lead => "Lead",
                ImportType.Product => "Product",
                ImportType.Order => "Order",
                _ => CurrentType.ToString()
            };

            var profiles = await _importService.GetMappingProfilesAsync(moduleType);

            App.Current.Dispatcher.Invoke(() =>
            {
                SavedProfiles.Clear();
                foreach (var p in profiles)
                {
                    SavedProfiles.Add(p);
                }
            });
        }

        [RelayCommand]
        private async Task BrowseAndLoad()
        {
            var openFile = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls"
            };

            if (openFile.ShowDialog() == true)
            {
                FilePath = openFile.FileName;
                await LoadExcelContextAsync();
            }
        }

        private async Task LoadExcelContextAsync()
        {
            IsLoading = true;
            LoadingMessage = "Detecting Excel structure and reading headers...";
            IsMappingVisible = false;

            try
            {
                string moduleType = CurrentType.ToString();
                var configuredFields = await _customFieldService.GetFieldsByModuleAsync(moduleType);

                // 1. Populate Tier 2 master descriptors for active module
                LoadHardcodedTier2Fields(CurrentType);

                // 2. Build system keywords list safely using DisplayLabel & FieldName
                var systemKeywords = _masterTier2Fields.Select(f => f.DisplayLabel)
                    .Concat(_masterTier2Fields.Select(f => f.FieldName))
                    .Concat(configuredFields.Select(f => string.IsNullOrEmpty(f.DisplayLabel) ? f.FieldName : f.DisplayLabel))
                    .Concat(new[] { "Party", "Name", "Date", "Bill", "Invoice", "Qty", "Amount", "Rate", "MRP", "GST", "Phone", "Code", "Type", "Item" })
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // 3. Perform dynamic header detection on background thread
                int headerRowIndex = 1;
                List<string> detectedHeaders = new();

                await Task.Run(() =>
                {
                    using var workbook = new XLWorkbook(FilePath);
                    var sheet = workbook.Worksheet(1);

                    headerRowIndex = DetectHeaderRowIndex(sheet, systemKeywords);

                    detectedHeaders = sheet.Row(headerRowIndex).CellsUsed()
                        .Select(c => c.GetValue<string>().Trim())
                        .Where(h => !string.IsNullOrEmpty(h))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                });

                App.Current.Dispatcher.Invoke(() =>
                {
                    AllExcelHeaders.Clear();
                    foreach (var h in detectedHeaders) AllExcelHeaders.Add(h);

                    // Populate initial active mapping rows for Tier 1 mandatory fields
                    Mappings.Clear();
                    foreach (var field in configuredFields.Where(f => f.IsVisible && f.FieldTier == 1))
                    {
                        var row = new ImportMappingRow(this)
                        {
                            InternalPropertyName = field.FieldName,
                            DisplayName = string.IsNullOrEmpty(field.DisplayLabel) ? field.FieldName : field.DisplayLabel,
                            FieldTier = field.FieldTier,
                            InfoTooltip = field.InfoTooltip
                        };

                        // Auto-match Excel headers with Tier 1 properties
                        string? matchedHeader = AllExcelHeaders.FirstOrDefault(h =>
                            h.Equals(field.FieldName, StringComparison.OrdinalIgnoreCase) ||
                            h.Equals(row.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                            (field.FieldName == "CustomerName" && (h.ToLower().Contains("name") || h.ToLower().Contains("pname") || h.ToLower().Contains("party"))) ||
                            (field.FieldName == "Phone" && (h.ToLower().Contains("phone") || h.ToLower().Contains("mobile"))));

                        if (!string.IsNullOrEmpty(matchedHeader))
                        {
                            row.SelectedExcelHeader = matchedHeader;
                        }

                        Mappings.Add(row);
                    }

                    IsMappingVisible = true;
                    StatusMessage = $"Excel loaded successfully. Header detected at Row #{headerRowIndex}.";

                    RefreshAvailableHeadersForAllRows();
                    UpdateUnmappedHeadersAndTier2Lists();
                });
            }
            catch (Exception ex)
            {
                StatusMessage = "Error loading Excel file: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Recalculates available Excel headers for each row, filtering out choices claimed by other rows.
        /// </summary>
        public void RefreshAvailableHeadersForAllRows()
        {
            if (_isRefreshingHeaders) return;
            _isRefreshingHeaders = true;

            try
            {
                // 1. Get all currently selected headers across all mapping rows
                var claimedHeaders = Mappings
                    .Where(m => !string.IsNullOrEmpty(m.SelectedExcelHeader))
                    .Select(m => m.SelectedExcelHeader!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var row in Mappings)
                {
                    string? currentSelected = row.SelectedExcelHeader;

                    // 2. Filter Master List: Keep unclaimed headers OR the row's own current selection
                    var filtered = AllExcelHeaders
                        .Where(h => !claimedHeaders.Contains(h) || string.Equals(h, currentSelected, StringComparison.OrdinalIgnoreCase))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    // 3. Update collection only if items actually changed (prevents resetting ComboBox selection)
                    if (!row.AvailableHeaders.SequenceEqual(filtered))
                    {
                        row.AvailableHeaders.Clear();
                        foreach (var header in filtered)
                        {
                            row.AvailableHeaders.Add(header);
                        }

                        // Restore selection in case clearing collection temporarily nulled it out
                        if (!string.IsNullOrEmpty(currentSelected) && row.AvailableHeaders.Contains(currentSelected))
                        {
                            row.SelectedExcelHeader = currentSelected;
                        }
                    }
                }
            }
            finally
            {
                _isRefreshingHeaders = false;
            }
        }

        [RelayCommand]
        private void ClearMapping(ImportMappingRow row)
        {
            if (row != null)
            {
                row.SelectedExcelHeader = null;
            }
        }

        /// <summary>
        /// Applies a saved mapping profile to the current active mapping rows.
        /// </summary>
        partial void OnSelectedProfileChanged(ImportMappingProfile? value)
        {
            if (value == null || string.IsNullOrEmpty(value.MappingJson)) return;

            try
            {
                var savedDict = JsonSerializer.Deserialize<Dictionary<string, string>>(value.MappingJson);
                if (savedDict == null) return;

                foreach (var row in Mappings)
                {
                    if (savedDict.TryGetValue(row.InternalPropertyName, out string? headerName))
                    {
                        // Check if this saved header actually exists in the newly loaded Excel sheet
                        if (AllExcelHeaders.Contains(headerName))
                        {
                            row.SelectedExcelHeader = headerName;
                        }
                        else
                        {
                            row.SelectedExcelHeader = null; // Reset if column missing from new Excel file
                        }
                    }
                }

                RefreshAvailableHeadersForAllRows();
                StatusMessage = $"Applied saved template: '{value.ProfileName}'";
            }
            catch (Exception ex)
            {
                StatusMessage = "Error applying mapping template: " + ex.Message;
            }
        }

        /// <summary>
        /// Option A: Maps an Excel Header directly to an unmapped Tier 2 System Field.
        /// </summary>
        [RelayCommand]
        private void MapExcelToTier2Field()
        {
            if (string.IsNullOrEmpty(SelectedUnmappedExcelHeader) || SelectedTier2TargetField == null)
            {
                StatusMessage = "Please select both an Excel column and a Target Tier 2 Field.";
                return;
            }

            // Add a new row to the lower grid for this hardcoded Tier 2 property
            var newRow = new ImportMappingRow(this)
            {
                InternalPropertyName = SelectedTier2TargetField.FieldName,
                DisplayName = SelectedTier2TargetField.DisplayLabel,
                FieldTier = 2,
                SelectedExcelHeader = SelectedUnmappedExcelHeader.Trim(),
                InfoTooltip = SelectedTier2TargetField.InfoTooltip,
                IsNewCustomFieldToCreate = false
            };

            App.Current.Dispatcher.Invoke(() =>
            {
                Mappings.Add(newRow);

                SelectedTier2TargetField = null;
                RefreshAvailableHeadersForAllRows();
                UpdateUnmappedHeadersAndTier2Lists();

                StatusMessage = $"Mapped '{newRow.SelectedExcelHeader}' to Tier 2 field '{newRow.DisplayName}'.";
            });
        }

        /// <summary>
        /// Option B: Auto-provisions a Brand New Tier 3 Custom Field for this Excel Header.
        /// </summary>
        [RelayCommand]
        private void CreateNewTier3CustomField()
        {
            if (string.IsNullOrEmpty(SelectedUnmappedExcelHeader))
            {
                StatusMessage = "Please select an Excel column to create a Custom Field for.";
                return;
            }

            string cleanFieldName = Regex.Replace(SelectedUnmappedExcelHeader, @"[^a-zA-Z0-9]", "");

            var newCustomRow = new ImportMappingRow(this)
            {
                InternalPropertyName = cleanFieldName,
                DisplayName = SelectedUnmappedExcelHeader.Trim(),
                FieldTier = 3,
                IsNewCustomFieldToCreate = true, // Flag for Service Layer DB insertion
                SelectedExcelHeader = SelectedUnmappedExcelHeader.Trim(),
                InfoTooltip = "This new Tier 3 field will be created in the database during import."
            };

            App.Current.Dispatcher.Invoke(() =>
            {
                Mappings.Add(newCustomRow);
                RefreshAvailableHeadersForAllRows();
                UpdateUnmappedHeadersAndTier2Lists();
                StatusMessage = $"Created new Tier 3 Custom Field for '{SelectedUnmappedExcelHeader}'.";
            });
        }

        /// <summary>
        /// Synchronizes the unmapped Excel headers dropdown and unmapped Tier 2 fields dropdown.
        /// </summary>
        public void UpdateUnmappedHeadersAndTier2Lists()
        {
            // 1. Get all Excel header names currently claimed in the mapping grid
            var claimedExcelHeaders = Mappings
                .Where(m => !string.IsNullOrWhiteSpace(m.SelectedExcelHeader))
                .Select(m => m.SelectedExcelHeader!.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 2. Filter top Unmapped Excel Columns dropdown
            var unmappedExcel = AllExcelHeaders
                .Where(h => !claimedExcelHeaders.Contains(h.Trim()))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            App.Current.Dispatcher.Invoke(() =>
            {
                UnmappedExcelHeaders.Clear();
                foreach (var header in unmappedExcel)
                {
                    UnmappedExcelHeaders.Add(header);
                }
            });

            // 3. Get property names already present/mapped in the grid
            var mappedPropertyNames = Mappings
                .Select(m => m.InternalPropertyName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 4. Populate Option A dropdown from hardcoded _masterTier2Fields
            App.Current.Dispatcher.Invoke(() =>
            {
                AvailableUnmappedTier2Fields.Clear();

                foreach (var field in _masterTier2Fields.Where(f => !mappedPropertyNames.Contains(f.FieldName)))
                {
                    AvailableUnmappedTier2Fields.Add(field);
                }
            });
        }

        [RelayCommand]
        private async Task StartImport()
        {
            // 1. Guard against unselected file or empty mapping
            if (string.IsNullOrWhiteSpace(FilePath) || !System.IO.File.Exists(FilePath))
            {
                StatusMessage = "Error: Invalid or missing Excel file path.";
                return;
            }

            var activeFieldMappings = Mappings
                .Where(m => !string.IsNullOrEmpty(m.SelectedExcelHeader))
                .ToList();

            if (!activeFieldMappings.Any())
            {
                StatusMessage = "Validation Warning: Please map at least one Excel column before starting import.";
                return;
            }

            IsLoading = true;
            LoadingMessage = "Parsing Excel rows and preparing payload...";
            StatusMessage = "Processing import...";

            try
            {
                string moduleType = CurrentType.ToString();
                var configuredFields = await _customFieldService.GetFieldsByModuleAsync(moduleType);

                // Build keywords list to accurately locate header row during parse
                LoadHardcodedTier2Fields(CurrentType);
                var systemKeywords = _masterTier2Fields.Select(f => f.DisplayLabel)
                    .Concat(_masterTier2Fields.Select(f => f.FieldName))
                    .Concat(configuredFields.Select(f => string.IsNullOrEmpty(f.DisplayLabel) ? f.FieldName : f.DisplayLabel))
                    .Concat(new[] { "Party", "Name", "Date", "Bill", "Invoice", "Qty", "Amount", "Rate", "MRP", "GST", "Phone", "Code", "Type", "Item" })
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // 2. Parse Excel file asynchronously starting after detected header row
                var payloadList = await Task.Run(() =>
                {
                    var rowsPayload = new List<Dictionary<string, object?>>();

                    using var workbook = new XLWorkbook(FilePath);
                    var sheet = workbook.Worksheet(1);
                    var lastRowUsed = sheet.LastRowUsed();

                    if (lastRowUsed == null) return rowsPayload;

                    // ⚡ DYNAMIC HEADER ROW LOCATION
                    int headerRowIndex = DetectHeaderRowIndex(sheet, systemKeywords);
                    int lastRow = lastRowUsed.RowNumber();

                    if (lastRow <= headerRowIndex)
                    {
                        return rowsPayload; // No data rows present below header
                    }

                    // Fetch headers from detected row and map to 1-based ClosedXML column indices
                    var totalExcelColumns = sheet.Row(headerRowIndex).CellsUsed()
                        .Select(c => c.GetValue<string>().Trim())
                        .ToList();

                    var columnIndexMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < totalExcelColumns.Count; i++)
                    {
                        string headerName = totalExcelColumns[i];
                        if (!columnIndexMap.ContainsKey(headerName))
                        {
                            columnIndexMap[headerName] = i + 1; // 1-based ClosedXML index
                        }
                    }

                    var claimedHeaders = activeFieldMappings
                        .Select(m => m.SelectedExcelHeader!)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    // ⚡ START PARSING DATA ROWS AT headerRowIndex + 1
                    for (int r = headerRowIndex + 1; r <= lastRow; r++)
                    {
                        var rowData = sheet.Row(r);

                        // Skip summary/total lines often found at bottom of Marg/Busy exports
                        string firstCellValue = rowData.Cell(1).Value.ToString().Trim();
                        if (firstCellValue.Equals("TOTAL", StringComparison.OrdinalIgnoreCase) ||
                            firstCellValue.Equals("GRAND TOTAL", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var dbRow = new Dictionary<string, object?>();

                        // STEP A: Map explicitly mapped target destination parameters (Tier 1, 2, 3)
                        foreach (var m in activeFieldMappings)
                        {
                            if (columnIndexMap.TryGetValue(m.SelectedExcelHeader!, out int colIndex))
                            {
                                var cellValue = rowData.Cell(colIndex).Value;
                                dbRow[m.InternalPropertyName] = cellValue.IsBlank ? null : cellValue.ToString().Trim();
                            }
                            else
                            {
                                dbRow[m.InternalPropertyName] = null;
                            }
                        }

                        // STEP B: CATCH-ALL RULE for unmapped Excel columns
                        foreach (var header in totalExcelColumns)
                        {
                            if (!claimedHeaders.Contains(header) && columnIndexMap.TryGetValue(header, out int colIndex))
                            {
                                var cellValue = rowData.Cell(colIndex).Value;
                                dbRow[header] = cellValue.IsBlank ? null : cellValue.ToString().Trim();
                            }
                        }

                        rowsPayload.Add(dbRow);
                    }

                    return rowsPayload;
                });

                if (!payloadList.Any())
                {
                    StatusMessage = "Import Warning: The selected Excel sheet contains no data rows.";
                    return;
                }

                // 3. Perform bulk database insertion via service layer
                LoadingMessage = $"Inserting {payloadList.Count} records into database...";

                int count = await _importService.BulkInsertAsync(payloadList, CurrentType, Mappings.ToList());

                StatusMessage = $"Import Successful! {count} records successfully committed.";

                await Task.Delay(1200);
                App.Current.Dispatcher.Invoke(() =>
                {
                    RequestClose?.Invoke(true);
                });
            }
            catch (Exception ex)
            {
                StatusMessage = "Import Failure: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void RequestCloseWindow()
        {
            // Passes false indicating cancellation/manual close
            RequestClose?.Invoke(false);
        }

        [RelayCommand]
        private void OpenSaveProfileDialog()
        {
            var currentMap = Mappings.Where(m => !string.IsNullOrEmpty(m.SelectedExcelHeader)).ToList();
            if (!currentMap.Any())
            {
                StatusMessage = "Cannot save an empty mapping. Please map at least one column.";
                return;
            }

            NewProfileName = string.Empty;
            IsSaveProfileDialogOpen = true;
        }

        [RelayCommand]
        private void CancelSaveProfileDialog()
        {
            IsSaveProfileDialogOpen = false;
            NewProfileName = string.Empty;
        }

        [RelayCommand]
        private async Task ConfirmSaveProfile()
        {
            if (string.IsNullOrWhiteSpace(NewProfileName))
            {
                StatusMessage = "Template name cannot be empty.";
                return;
            }

            var currentMap = Mappings
                .Where(m => !string.IsNullOrEmpty(m.SelectedExcelHeader))
                .ToDictionary(m => m.InternalPropertyName, m => m.SelectedExcelHeader!);

            await _importService.SaveMappingProfileAsync(NewProfileName.Trim(), CurrentType.ToString(), currentMap);

            IsSaveProfileDialogOpen = false;
            StatusMessage = $"Template '{NewProfileName.Trim()}' saved successfully!";
            NewProfileName = string.Empty;

            // Refresh saved profiles dropdown and pre-select the newly saved template
            await LoadSavedProfilesAsync();
        }

        [RelayCommand]
        private async Task DeleteSelectedProfile()
        {
            if (SelectedProfile == null) return;

            await _importService.DeleteMappingProfileAsync(SelectedProfile.ProfileId);
            SelectedProfile = null;
            await LoadSavedProfilesAsync();
            StatusMessage = "Mapping profile deleted.";
        }

        /// <summary>
        /// Opens the Create Tier 3 Custom Field Dialog for the selected unmapped Excel header.
        /// </summary>
        [RelayCommand]
        private void OpenCreateTier3Dialog()
        {
            if (string.IsNullOrWhiteSpace(SelectedUnmappedExcelHeader))
            {
                StatusMessage = "Please select an Excel column to create a Custom Field for.";
                return;
            }

            // Pre-fill display label and auto-generate clean database field name
            NewTier3DisplayLabel = SelectedUnmappedExcelHeader.Trim();
            NewTier3FieldName = Regex.Replace(SelectedUnmappedExcelHeader.Trim(), @"[^a-zA-Z0-9]", "");
            SelectedTier3FieldType = "Textbox";
            NewTier3Tooltip = $"Auto-created field for {SelectedUnmappedExcelHeader.Trim()}";

            IsCreateTier3DialogOpen = true;
        }

        [RelayCommand]
        private void CancelCreateTier3Dialog()
        {
            IsCreateTier3DialogOpen = false;
        }

        [RelayCommand]
        private async Task ConfirmCreateTier3Field()
        {
            if (string.IsNullOrWhiteSpace(NewTier3DisplayLabel) || string.IsNullOrWhiteSpace(NewTier3FieldName))
            {
                StatusMessage = "Field Name and Display Label are required.";
                return;
            }

            // Check if property name already exists in current mapping list
            if (Mappings.Any(m => m.InternalPropertyName.Equals(NewTier3FieldName, StringComparison.OrdinalIgnoreCase)))
            {
                StatusMessage = $"Field '{NewTier3FieldName}' is already mapped in the list.";
                return;
            }

            try
            {
                string moduleType = CurrentType.ToString(); // "Lead", "Product", or "Order"

                // 1. SAVE CUSTOM FIELD DEFINITION DIRECTLY TO DATABASE FOR THIS MODULE
                var newFieldDefinition = new CustomFieldDefinition
                {
                    FieldName = NewTier3FieldName.Trim(),
                    DisplayLabel = NewTier3DisplayLabel.Trim(),
                    ModuleType = moduleType,
                    FieldTier = 3,
                    FieldType = SelectedTier3FieldType,
                    IsVisible = true,
                    IsRequired = false
                };

                // Call your CustomFieldService to save it into the CustomFieldDefinitions table
                await _customFieldService.SaveCustomFieldAsync(newFieldDefinition);

                // 2. ADD MAPPING ROW TO THE IMPORT ENGINE UI
                var newCustomRow = new ImportMappingRow(this)
                {
                    InternalPropertyName = newFieldDefinition.FieldName,
                    DisplayName = newFieldDefinition.DisplayLabel,
                    FieldTier = 3,
                    IsNewCustomFieldToCreate = false, // Definition is now already created in DB
                    SelectedExcelHeader = SelectedUnmappedExcelHeader?.Trim(),
                    InfoTooltip = newFieldDefinition.InfoTooltip
                };

                App.Current.Dispatcher.Invoke(() =>
                {
                    Mappings.Add(newCustomRow);

                    IsCreateTier3DialogOpen = false;
                    RefreshAvailableHeadersForAllRows();
                    UpdateUnmappedHeadersAndTier2Lists();

                    StatusMessage = $"Tier 3 Field '{newCustomRow.DisplayName}' created and saved to module '{moduleType}' successfully!";
                });
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to create Custom Field: " + ex.Message;
            }
        }

        [RelayCommand]
        private async Task DownloadFormat()
        {
            try
            {
                string moduleName = CurrentType.ToString(); // "Lead", "Product", or "Order"

                // 1. Open SaveFileDialog to choose where to save the template
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                    FileName = $"{moduleName}_Import_Format.xlsx",
                    Title = $"Save {moduleName} Import Format Template"
                };

                if (saveFileDialog.ShowDialog() != true) return;

                string savePath = saveFileDialog.FileName;

                IsLoading = true;
                LoadingMessage = "Generating Excel format template...";

                await Task.Run(async () =>
                {
                    // 2. Fetch all configured fields (Tier 1, Tier 2, Tier 3) for the active module
                    var allConfiguredFields = await _customFieldService.GetFieldsByModuleAsync(moduleName);

                    // Add hardcoded Tier 2 fields if not returned from DB service
                    var tier2Descriptors = _masterTier2Fields ?? new List<SystemFieldDescriptor>();

                    using var workbook = new XLWorkbook();
                    var worksheet = workbook.Worksheets.Add($"{moduleName} Import Format");

                    int colIndex = 1;

                    // --- A. ADD TIER 1 MANDATORY FIELDS ---
                    var tier1Fields = allConfiguredFields.Where(f => f.FieldTier == 1 && f.IsVisible).ToList();
                    foreach (var field in tier1Fields)
                    {
                        string headerText = field.IsRequired ? $"{field.EffectiveLabel}*" : field.EffectiveLabel;
                        var cell = worksheet.Cell(1, colIndex++);
                        cell.Value = headerText;

                        // Style Header: Dark Teal background for Mandatory
                        cell.Style.Font.Bold = true;
                        cell.Style.Font.FontColor = XLColor.White;
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0D9488");
                    }

                    // --- B. ADD TIER 2 SYSTEM FIELDS ---
                    foreach (var field in tier2Descriptors)
                    {
                        // Check if matching custom field definition has IsRequired set
                        var matchedDef = allConfiguredFields.FirstOrDefault(f => f.FieldName.Equals(field.FieldName, StringComparison.OrdinalIgnoreCase));
                        bool isReq = matchedDef?.IsRequired ?? false;

                        string headerText = isReq ? $"{field.DisplayLabel}*" : field.DisplayLabel;
                        var cell = worksheet.Cell(1, colIndex++);
                        cell.Value = headerText;

                        // Style Header: Slate Blue background for Tier 2
                        cell.Style.Font.Bold = true;
                        cell.Style.Font.FontColor = XLColor.White;
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0284C7");
                    }

                    // --- C. ADD TIER 3 CUSTOM FIELDS ---
                    var tier3Fields = allConfiguredFields.Where(f => f.FieldTier == 3 && f.IsVisible).ToList();
                    foreach (var field in tier3Fields)
                    {
                        string headerText = field.IsRequired ? $"{field.EffectiveLabel}*" : field.EffectiveLabel;
                        var cell = worksheet.Cell(1, colIndex++);
                        cell.Value = headerText;

                        // Style Header: Warm Amber background for Custom Tier 3
                        cell.Style.Font.Bold = true;
                        cell.Style.Font.FontColor = XLColor.Black;
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F59E0B");
                    }

                    // Auto-fit column widths for clear readability
                    worksheet.Columns().AdjustToContents();
                    worksheet.Row(1).Height = 24;
                    worksheet.Row(1).Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);

                    // Save file to chosen path
                    workbook.SaveAs(savePath);
                });

                StatusMessage = $"Format template downloaded successfully: {Path.GetFileName(savePath)}";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to generate format template: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    /// <summary>
    /// Represents a single field mapping row in the UI with dynamic header filtering.
    /// </summary>
    public partial class ImportMappingRow : ObservableObject
    {
        private readonly ImportViewModel _parentViewModel;

        public string InternalPropertyName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int FieldTier { get; set; }
        public string InfoTooltip { get; set; } = string.Empty;

        [ObservableProperty]
        private string? _selectedExcelHeader;

        public ObservableCollection<string> AvailableHeaders { get; } = new();

        public bool IsMapped => !string.IsNullOrEmpty(SelectedExcelHeader);

        public bool IsNewCustomFieldToCreate { get; set; } = false;

        public string TierBadgeText => IsNewCustomFieldToCreate
            ? "Tier 3 (New Auto-Create)"
            : FieldTier switch
            {
                1 => "Tier 1 (Mandatory)",
                2 => "Tier 2 (Standard)",
                _ => "Tier 3 (Custom)"
            };

        public string TierBadgeColor => FieldTier switch
        {
            1 => "#DC2626", // Red
            2 => "#2563EB", // Blue
            _ => "#D97706"  // Amber
        };

        public ImportMappingRow(ImportViewModel parentViewModel)
        {
            _parentViewModel = parentViewModel;
        }

        partial void OnSelectedExcelHeaderChanged(string? value)
        {
            OnPropertyChanged(nameof(IsMapped));
            // Trigger parent to remove this selected header from all other dropdown options
            _parentViewModel?.RefreshAvailableHeadersForAllRows();
            _parentViewModel?.UpdateUnmappedHeadersAndTier2Lists();
        }
    }
}
