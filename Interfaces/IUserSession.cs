using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Interfaces
{
    public interface IUserSession
    {
        string CurrentUser { get; set; }
        int UserId { get; set; }
        string CurrentUserEmail { get; set; }
        string DisplayName { get; set; }
        string UserRole { get; set; } // "Admin", "Executive", "Team Leader", "Sub-Admin"
        int? SeniorId { get; set; }
        int UserLimit { get; set; }
        DateTime? ExpiryDate { get; set; }
        string? MemberSince { get; set; }
        string? Phone { get; set; }
        bool IsAdmin { get; }
        void Clear();
    }
}
