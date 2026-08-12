using Tijori.Dialogs;
using Tijori.Models;
using Tijori.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Tijori.ViewModels
{
    public partial class CustomFieldsViewModel : ObservableObject
    {
        private readonly CustomFieldService _fieldService;
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty]
        private ObservableCollection<string> _availableModules = new()
        {
            "Leads",
            "Customers",
            "Products",
            "Orders",
            "Purchases",
            "Vendors",
            "Staff"
        };

        // Currently selected sidebar module
        [ObservableProperty]
        private string _selectedModule = "Leads";

        [ObservableProperty]
        private ObservableCollection<CustomFieldDefinition> _customFieldsSource = new();

        /// <summary>
        /// User-friendly notice explaining how settings for this module take effect.
        /// </summary>
        public string ModuleInfoNotice => SelectedModule switch
        {
            "Order" or "Orders" or "Purchase" or "Purchases" =>
                "These fields are used for importing Excel sheets and will not affect your standard screen forms.",
            _ =>
                "Changes will apply immediately across your forms."
        };

        public CustomFieldsViewModel(CustomFieldService fieldService, IServiceProvider serviceProvider)
        {
            _fieldService = fieldService;
            _serviceProvider = serviceProvider;
            _ = LoadCustomFieldsListAsync();
        }

        partial void OnSelectedModuleChanged(string value)
        {
            OnPropertyChanged(nameof(ModuleInfoNotice));
            _ = LoadCustomFieldsListAsync();
        }

        public async Task LoadCustomFieldsListAsync()
        {
            string normalizedModule = SelectedModule.TrimEnd('s'); // Convert "Leads" -> "Lead"

            var data = await _fieldService.GetFieldsByModuleAsync(normalizedModule);

            App.Current.Dispatcher.Invoke(() =>
            {
                CustomFieldsSource.Clear();

                int index = 1;
                foreach (var item in data)
                {
                    item.RowIndex = index++;
                    CustomFieldsSource.Add(item);
                }
            });
        }

        [RelayCommand]
        private async Task OpenCreateFieldDialog()
        {
            var vm = _serviceProvider.GetRequiredService<CreateFieldViewModel>();

            string activeModule = SelectedModule.TrimEnd('s'); // e.g., "Leads" -> "Lead"

            // Initialize 2-Column Catalog and DataGrid List asynchronously
            await vm.InitializeAsync(activeModule);

            var dialogWindow = new CreateFieldWindow { DataContext = vm };
            dialogWindow.Owner = Application.Current.MainWindow;

            vm.RequestClose += (bool isSaved) =>
            {
                dialogWindow.DialogResult = isSaved;
                dialogWindow.Close();
            };

            if (dialogWindow.ShowDialog() == true)
            {
                await LoadCustomFieldsListAsync();
            }
        }

        [RelayCommand]
        private async Task EditField(CustomFieldDefinition fieldToEdit)
        {
            if (fieldToEdit == null) return;

            var vm = _serviceProvider.GetRequiredService<CreateFieldViewModel>();

            // 1. Initialize ViewModel for the target module
            await vm.InitializeAsync(fieldToEdit.ModuleType);

            // 2. Load target field into editor
            vm.SelectFieldForEditCommand.Execute(fieldToEdit);

            var dialogWindow = new CreateFieldWindow { DataContext = vm };
            dialogWindow.Owner = Application.Current.MainWindow;

            vm.RequestClose += (bool isSaved) =>
            {
                dialogWindow.DialogResult = isSaved;
                dialogWindow.Close();
            };

            if (dialogWindow.ShowDialog() == true)
            {
                await LoadCustomFieldsListAsync();
            }
        }

        [RelayCommand]
        private async Task DeleteField(CustomFieldDefinition field)
        {
            if (field == null) return;

            // Prevent deletion of Tier 1 Mandatory System Fields
            if (field.FieldTier == 1)
            {
                MessageBox.Show("Mandatory system fields cannot be removed.", "Action Restricted", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"Are you sure you want to drop field '{field.EffectiveLabel}'?",
                "Confirm Drop", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                await _fieldService.DeleteCustomFieldAsync(field.FieldId);
                await LoadCustomFieldsListAsync();
            }
        }
    }
}
