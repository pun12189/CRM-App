using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class EmailSettings : ObservableObject
    {
        // Primary Key for MySQL
        public int Id { get; set; }

        [ObservableProperty]
        private string _senderName;

        [ObservableProperty]
        private string _emailAddress;

        [ObservableProperty]
        private string _smtpServer;

        [ObservableProperty]
        private int _port = 587; // Default for TLS

        [ObservableProperty]
        private bool _enableSSL = true;

        [ObservableProperty]
        private string _username;

        // Note: Password is usually handled via PasswordBox in the View
        // and passed directly to the service for security.
        [ObservableProperty]
        private string _password;

        [ObservableProperty]
        private bool _isDefault = true;
    }
}
