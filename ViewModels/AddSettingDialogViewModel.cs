using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.ViewModels
{
    public partial class AddSettingDialogViewModel : ObservableObject
    {
        public event Action<string?> RequestClose;

        [ObservableProperty] private string _dialogTitle;
        [ObservableProperty] private string _inputValue;

        public void Initialize(string title, string? existingValue = null)
        {
            DialogTitle = title;
            InputValue = existingValue ?? string.Empty;
        }

        [RelayCommand]
        private void Save()
        {
            if (string.IsNullOrWhiteSpace(InputValue)) return;
            RequestClose?.Invoke(InputValue); // Send the text back
        }
    }
}
