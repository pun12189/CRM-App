using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models.Enums
{
    public enum LicensePackageType : byte
    {
        Trial = 0,
        Package = 1,
        AMC = 2,
        AMCWithUpdates = 3
    }
}
