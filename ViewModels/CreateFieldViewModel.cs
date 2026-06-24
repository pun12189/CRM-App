using CallMan.Models;
using CallMan.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.ViewModels
{
    public partial class CreateFieldViewModel : ObservableObject
    {
        private readonly CustomFieldService _fieldService;
        public event Action<bool>? RequestClose;

        [ObservableProperty]
        private CustomFieldDefinition _newField = new();

        [ObservableProperty] private string _newValueOptionText = string.Empty;

        public CreateFieldViewModel(CustomFieldService fieldService)
        {
            _fieldService = fieldService;
            // Establish systemic safe collection model initializations
            NewField.SeedValueOptionsList = new ObservableCollection<string>();
        }

        [RelayCommand]
        private void AddValueOption()
        {
            if (string.IsNullOrWhiteSpace(NewValueOptionText)) return;

            // Split the input string by commas to support both single entries and bulk entries
            var rawItems = NewValueOptionText.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var item in rawItems)
            {
                string cleanValue = item.Trim();

                // Ensure the item isn't empty and doesn't already exist in the list
                if (!string.IsNullOrEmpty(cleanValue) && !NewField.SeedValueOptionsList.Contains(cleanValue))
                {
                    NewField.SeedValueOptionsList.Add(cleanValue);
                }
            }

            // Clear the input field box for the next typing action
            NewValueOptionText = string.Empty;
        }

        /// <summary>
        /// Command to remove an existing choice item from the chips display matrix
        /// </summary>
        [RelayCommand]
        private void RemoveValueOption(string optionToRemove)
        {
            if (optionToRemove != null && NewField.SeedValueOptionsList.Contains(optionToRemove))
            {
                NewField.SeedValueOptionsList.Remove(optionToRemove);
            }
        }

        [RelayCommand]
        private async Task SubmitCustomField()
        {
            if (string.IsNullOrWhiteSpace(NewField.FieldName)) return;

            // Enforcement constraint validation checks pipeline safety fallbacks
            if (!NewField.IsRequired)
            {
                NewField.IsRequiredInLead = false;
                NewField.IsRequiredInCustomer = false;
                NewField.IsRequiredInProduct = false;
            }

            if (NewField.FieldType != "DropdownSingle" && NewField.FieldType != "DropdownMultiple")
            {
                NewField.SeedValueOptionsList.Clear();
            }

            bool isSaved = await _fieldService.SaveCustomFieldAsync(NewField);
            if (isSaved)
            {
                RequestClose?.Invoke(true); // Close window and report success state execution
            }
        }

        [RelayCommand]
        private void CloseDialog()
        {
            RequestClose?.Invoke(false);
        }
    }
}
