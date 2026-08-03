using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class LoginLog : ObservableObject
    {
        public int Id { get; set; }
        public int StaffId { get; set; }
        public string StaffName { get; set; } // Joined from Staff table
        public string DepartmentName { get; set; } // Joined from Department table
        public DateTime LoginTimestamp { get; set; }
        public DateTime? LogoutTimestamp { get; set; }
        public string MachineName { get; set; }
        public string IPAddress { get; set; }
        public string Status { get; set; }
    }
}
