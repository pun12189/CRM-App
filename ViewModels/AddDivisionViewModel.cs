using Tijori.Models;
using Tijori.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.ViewModels
{
    public partial class AddDivisionViewModel : ObservableObject
    {
        private readonly ProfileService _service;

        [ObservableProperty] private Division _newDivision = new() { IsActive = true };

        // Event to close the window from ViewModel
        public event Action<bool>? RequestClose;

        public AddDivisionViewModel(ProfileService service)
        {
            _service = service;
        }

        [RelayCommand]
        private async Task Save()
        {
            if (string.IsNullOrWhiteSpace(NewDivision.Name)) return;

            // 1. Save the Division to the 'Divisions' table
            int newId = await _service.CreateDivisionAsync(NewDivision);

            if (newId > 0)
            {
                // 2. Automatically create a blank Profile linked to this ID
                await _service.InitializeBlankProfileAsync(newId, NewDivision.Name);

                RequestClose?.Invoke(true); // Close the dialog with 'True' result
            }
        }
    }
}
