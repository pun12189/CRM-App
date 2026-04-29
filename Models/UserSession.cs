using CallMan.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public class UserSession : IUserSession
    {
        public string CurrentUser { get; set; } = "Admin"; // Default or set during login
        public int UserId { get; set; }
        public string CurrentUserEmail { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty;
        public int? SeniorId { get; set; }

        public bool IsAdmin => UserRole == "Admin";

        public int UserLimit { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string? MemberSince { get; set; }
        public string? Phone { get; set; }

        public void Clear()
        {
            UserId = 0;
            CurrentUserEmail = string.Empty;
            DisplayName = string.Empty;
            UserRole = string.Empty;
            SeniorId = null;
        }
    }
}
