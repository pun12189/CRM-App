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
        private ObservableCollection<CustomFieldDefinition> _customFieldsSource = new();

        public CustomFieldsViewModel(CustomFieldService fieldService, IServiceProvider serviceProvider)
        {
            _fieldService = fieldService;
            _serviceProvider = serviceProvider;
            _ = LoadCustomFieldsListAsync();
        }

        public async Task LoadCustomFieldsListAsync()
        {
            var data = await _fieldService.GetAllFieldsAsync();
            CustomFieldsSource.Clear();

            int index = 1;
            foreach (var item in data)
            {
                item.RowIndex = index++;
                CustomFieldsSource.Add(item);
            }
        }

        [RelayCommand]
        private void OpenCreateFieldDialog()
        {
            var vm = _serviceProvider.GetRequiredService<CreateFieldViewModel>();
            var dialogWindow = new CreateFieldWindow { DataContext = vm };
            dialogWindow.Owner = Application.Current.MainWindow;

            vm.RequestClose += (bool isSaved) =>
            {
                dialogWindow.DialogResult = isSaved;
                dialogWindow.Close();
            };

            if (dialogWindow.ShowDialog() == true)
            {
                _ = LoadCustomFieldsListAsync(); // Reload grid values on success
            }
        }

        [RelayCommand]
        private async Task DeleteField(CustomFieldDefinition field)
        {
            if (field == null) return;
            var result = MessageBox.Show($"Are you sure you want to completely drop field '{field.FieldName}'?",
                "Confirm Drop", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                await _fieldService.DeleteCustomFieldAsync(field.FieldId);
                await LoadCustomFieldsListAsync();
            }
        }
    }
}
