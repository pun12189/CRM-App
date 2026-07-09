using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public class LicenseTokenPayload
    {
        public string TargetSystemId { get; set; } = string.Empty;
        public byte PackageType { get; set; }
        public string ExpirationDateStr { get; set; } = string.Empty; // yyyy-MM-dd
        public bool AllowUpdates { get; set; }
        public int CustomTrialDays { get; set; }
        public string SecuritySignature { get; set; } = string.Empty;
    }
}
