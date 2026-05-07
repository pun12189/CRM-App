using CallMan.Models;
using CallMan.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Org.BouncyCastle.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.ViewModels
{
    public partial class LoginLogsViewModel : ObservableObject
    {
        private readonly LoginLogService _logService;
        [ObservableProperty] private ObservableCollection<LoginLog> _logs;
        [ObservableProperty] private string _searchText;

        public LoginLogsViewModel(LoginLogService logService)
        {
            _logService = logService;
            _ = LoadLogs();
        }

        [RelayCommand]
        private async Task LoadLogs()
        {
            var data = await _logService.GetRecentLogsAsync();
            Logs = new ObservableCollection<LoginLog>(data);
        }
    }
}
