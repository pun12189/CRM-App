using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public class OrderProductLookupItem
    {
        // Common Base properties
        public int ProductId { get; set; }
        public int? BatchId { get; set; } // Null if it represents the Parent Product row
        public string DisplayText { get; set; }
        public int AvailableStock { get; set; }
        public decimal Price { get; set; }
        public bool IsBatchRow { get; set; } // Flag to distinguish row types in UI triggers
        public string ProductName { get; set; }
        public string ShortName { get; set; }
        public string BatchNumber { get; set; }

        // Combined keyword property for instant searching
        public string SearchKeywords => $"{ProductName} {ShortName} {BatchNumber}".Trim();
    }
}
