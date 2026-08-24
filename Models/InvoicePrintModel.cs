using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public class InvoicePrintModel
    {
        // 1. Company / Seller Info
        public string SellerCompanyName { get; set; } = string.Empty;
        public string SellerAddress { get; set; } = string.Empty;
        public string SellerGstin { get; set; } = string.Empty;
        public string SellerPan { get; set; } = string.Empty;
        public string SellerPhone { get; set; } = string.Empty;
        public string SellerEmail { get; set; } = string.Empty;
        public string SellerBankName { get; set; } = string.Empty;
        public string SellerAccountNumber { get; set; } = string.Empty;
        public string SellerIfsc { get; set; } = string.Empty;
        public string SellerUpi { get; set; } = string.Empty;
        public string TermsAndConditions { get; set; } = string.Empty;
        public byte[]? CompanyLogo { get; set; }

        // 2. Buyer / Customer Info
        public int LeadId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string BillingAddress { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Pincode { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? BuyerGstin { get; set; } // Read from metadata or empty

        // 3. Order & Invoice Info
        public int OrderId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public string PaymentStatus { get; set; } = "Pending";
        public string? PreferedTransport { get; set; }
        public string? Remarks { get; set; }

        // 4. Line Items & Charges
        public List<InvoicePrintLineItem> Items { get; set; } = new();
        public List<InvoicePrintExtraCharge> ExtraCharges { get; set; } = new();

        // 5. Commercial Totals
        public decimal SubTotalAmount => Items.Sum(x => x.SubTotal);
        public decimal TotalGstAmount => Items.Sum(x => x.GstAmount) + ExtraCharges.Sum(x => (x.Amount * x.GSTPercent / 100m));
        public decimal TotalExtraCharges => ExtraCharges.Where(x => !x.IsDiscount).Sum(x => x.Amount);
        public decimal TotalDiscounts => ExtraCharges.Where(x => x.IsDiscount).Sum(x => x.Amount);
        public decimal GrandTotal { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal BalanceDue => GrandTotal - AmountPaid;

        public bool IsInterState => !string.IsNullOrWhiteSpace(State) && !string.IsNullOrWhiteSpace(SellerAddress) &&
                                    !SellerAddress.Contains(State, StringComparison.OrdinalIgnoreCase);
    }

    public class InvoicePrintLineItem
    {
        public int OrderItemId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string? BatchNumber { get; set; }
        public string Unit { get; set; } = "Pcs";
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal GSTPercent { get; set; }
        public decimal SubTotal { get; set; }
        public decimal GstAmount { get; set; }
        public decimal Total { get; set; }
    }

    public class InvoicePrintExtraCharge
    {
        public int ChargeId { get; set; }
        public string ChargeName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal GSTPercent { get; set; }
        public bool IsDiscount { get; set; }
    }
}
