using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Interfaces
{
    public interface IActionSecurityGuard
    {
        Task<bool> IsActionAuthorizedAsync();
    }
}
