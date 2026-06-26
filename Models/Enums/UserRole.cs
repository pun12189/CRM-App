using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models.Enums
{
    public enum UserRole : byte
    {
        Admin = 0,
        SubAdmin = 1,
        TeamLeader = 2,
        Executive = 3
    }
}
