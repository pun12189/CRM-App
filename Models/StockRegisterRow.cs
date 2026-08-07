using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public class StockRegisterRow
    {
        public string BillNo { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public string VoucherType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string BatchNumber { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal Value { get; set; }
        public decimal BalanceQuantity { get; set; }
        public string MovementType { get; set; } = string.Empty;
    }
}
