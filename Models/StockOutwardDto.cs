using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public class StockOutwardDto
    {
        public string BillNo { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public int LeadId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerCity { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public int? BatchId { get; set; }
        public string BatchNumber { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; } // Uses `Total` column from OrderItems
    }
}
