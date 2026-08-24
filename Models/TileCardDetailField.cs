using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public class TileCardDetailField
    {
        public string IconKind { get; set; } = "InformationOutline"; // MaterialDesign Icon name
        public string Label { get; set; } = string.Empty;            // e.g., "Phone:", "Location:"
        public string Value { get; set; } = string.Empty;            // e.g., "+91 98765 43210"
        public string ValueColorHex { get; set; } = "#1E293B";       // Default dark slate
    }
}
