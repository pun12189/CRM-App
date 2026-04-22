using CallMan.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Interfaces
{
    public interface IAuthService
    {
        Task<User?> AuthenticateByEmailAsync(string email, string password);
        Task<bool> ResetPasswordAsync(string email);
    }
}
