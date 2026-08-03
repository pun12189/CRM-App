using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public class VendorActivityItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string CreatedBy { get; set; } = "Admin";
        public string IconKind { get; set; } = "InformationOutline";
        public string IconColor { get; set; } = "#64748B";
    }
}
