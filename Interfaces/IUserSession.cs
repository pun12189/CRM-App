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
    }
}
