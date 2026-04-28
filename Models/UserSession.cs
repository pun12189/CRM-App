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
    }
}
