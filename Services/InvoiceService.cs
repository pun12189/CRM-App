using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Tijori.Data;
using Tijori.Models;

namespace Tijori.Services
{
    public class InvoiceService
    {
        private readonly CrmDbContext _context;

        public InvoiceService(CrmDbContext context) => _context = context;

        public async Task<InvoicePrintModel?> GetOrderInvoiceDataAsync(int orderId)
        {
            using var conn = _context.CreateConnection();

            // 1. Fetch Order Header & Lead / Customer details
            const string orderSql = @"
                SELECT 
                    o.OrderId, o.InvoiceNumber, o.OrderDate, o.TotalAmount, o.GrandTotal, 
                    o.AmountPaid, o.PaymentStatus, o.PreferedTransport, o.Remarks, o.DivisionId,
                    l.LeadId, l.CustomerName, l.CompanyName, l.AddressLine AS BillingAddress, 
                    l.City, l.District, l.State, l.Pincode, l.Phone, l.Email
                FROM orders o
                LEFT JOIN leads l ON o.LeadId = l.LeadId
                WHERE o.OrderId = @OrderId;";

            var invoice = await conn.QueryFirstOrDefaultAsync<InvoicePrintModel>(orderSql, new { OrderId = orderId });
            if (invoice == null) return null;

            // 2. Fetch Division Company Profile
            const string companySql = @"
                SELECT 
                    CompanyName AS SellerCompanyName, RegisteredAddress AS SellerAddress,
                    GstNumber AS SellerGstin, PanNumber AS SellerPan, ContactNumber AS SellerPhone,
                    OfficialEmail AS SellerEmail, BankName AS SellerBankName, 
                    AccountNumber AS SellerAccountNumber, IfscCode AS SellerIfsc,
                    UpiId AS SellerUpi, TermsAndConditions, LogoData AS CompanyLogo
                FROM companyprofile
                WHERE DivisionId = 1 
                LIMIT 1;";

            var company = await conn.QueryFirstOrDefaultAsync<InvoicePrintModel>(companySql);
            if (company != null)
            {
                invoice.SellerCompanyName = company.SellerCompanyName;
                invoice.SellerAddress = company.SellerAddress;
                invoice.SellerGstin = company.SellerGstin;
                invoice.SellerPan = company.SellerPan;
                invoice.SellerPhone = company.SellerPhone;
                invoice.SellerEmail = company.SellerEmail;
                invoice.SellerBankName = company.SellerBankName;
                invoice.SellerAccountNumber = company.SellerAccountNumber;
                invoice.SellerIfsc = company.SellerIfsc;
                invoice.SellerUpi = company.SellerUpi;
                invoice.TermsAndConditions = company.TermsAndConditions;
                invoice.CompanyLogo = company.CompanyLogo;
            }

            // 3. Fetch Line Items (joining products and batches)
            const string itemsSql = @"
                SELECT 
                    oi.OrderItemId, oi.Quantity, oi.UnitPrice, oi.GSTPercent, 
                    oi.SubTotal, oi.GstAmount, oi.Total,
                    p.Name AS ProductName, p.SKU, p.Unit,
                    pb.BatchNumber
                FROM orderitems oi
                INNER JOIN products p ON oi.ProductId = p.ProductId
                LEFT JOIN productbatches pb ON oi.BatchId = pb.BatchId
                WHERE oi.OrderId = @OrderId;";

            var items = await conn.QueryAsync<InvoicePrintLineItem>(itemsSql, new { OrderId = orderId });
            invoice.Items = items.ToList();

            // 4. Fetch Extra Charges / Discounts
            const string chargesSql = @"
                SELECT ChargeId, ChargeName, Amount, GSTPercent, IsDiscount
                FROM orderextracharges
                WHERE OrderId = @OrderId;";

            var charges = await conn.QueryAsync<InvoicePrintExtraCharge>(chargesSql, new { OrderId = orderId });
            invoice.ExtraCharges = charges.ToList();

            return invoice;
        }

        public FlowDocument CreateTaxInvoiceDocument(InvoicePrintModel inv, double printableWidth = 793.7)
        {
            var doc = new FlowDocument
            {
                PagePadding = new Thickness(36),
                PageWidth = printableWidth,
                ColumnWidth = double.PositiveInfinity,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 9.5,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
            };

            double contentWidth = printableWidth - 72;

            // 1. TOP TITLE BAR
            var titleTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 8) };
            titleTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.55) });
            titleTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.45) });

            var titleRow = new TableRow();
            var titleLeft = new TableCell(new Paragraph(new Bold(new Run("TAX INVOICE"))) { FontSize = 16, Foreground = new SolidColorBrush(Color.FromRgb(23, 148, 161)), Margin = new Thickness(0) });
            titleLeft.Blocks.Add(new Paragraph(new Run("Original for Recipient / Commercial Sale")) { FontSize = 8.5, Foreground = Brushes.Gray, Margin = new Thickness(0, 2, 0, 0) });

            var titleRight = new TableCell(new Paragraph(new Bold(new Run($"Invoice #: {inv.InvoiceNumber}"))) { FontSize = 11, TextAlignment = TextAlignment.Right, Margin = new Thickness(0) });
            titleRight.Blocks.Add(new Paragraph(new Run($"Date: {inv.OrderDate:dd MMM yyyy} | Ref Order: #{inv.OrderId}")) { FontSize = 9, TextAlignment = TextAlignment.Right, Foreground = Brushes.Gray, Margin = new Thickness(0, 2, 0, 0) });

            titleRow.Cells.Add(titleLeft);
            titleRow.Cells.Add(titleRight);
            var titleGroup = new TableRowGroup();
            titleGroup.Rows.Add(titleRow);
            titleTable.RowGroups.Add(titleGroup);
            doc.Blocks.Add(titleTable);

            // 2. SELLER & BUYER 2-COLUMN BOX
            var partyTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 10), BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)), BorderThickness = new Thickness(1) };
            partyTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.5) });
            partyTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.5) });

            var partyHeaderRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)) };
            partyHeaderRow.Cells.Add(CreateCell(new Bold(new Run("Sold By (Seller)")), isHeader: true));
            partyHeaderRow.Cells.Add(CreateCell(new Bold(new Run("Billed To (Buyer)")), isHeader: true));

            var partyBodyRow = new TableRow();

            // Seller Block
            var sellerBlock = new TableCell();
            sellerBlock.Padding = new Thickness(6);
            sellerBlock.Blocks.Add(new Paragraph(new Bold(new Run(inv.SellerCompanyName))) { Margin = new Thickness(0, 0, 0, 2) });
            sellerBlock.Blocks.Add(new Paragraph(new Run(inv.SellerAddress)) { Margin = new Thickness(0, 0, 0, 2) });
            sellerBlock.Blocks.Add(new Paragraph(new Run($"GSTIN: {inv.SellerGstin}  |  PAN: {inv.SellerPan}")) { FontSize = 8.5, Margin = new Thickness(0, 0, 0, 2) });
            sellerBlock.Blocks.Add(new Paragraph(new Run($"Phone: {inv.SellerPhone}  |  Email: {inv.SellerEmail}")) { FontSize = 8.5, Margin = new Thickness(0) });
            sellerBlock.BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225));
            sellerBlock.BorderThickness = new Thickness(0, 0, 1, 0);

            // Buyer Block
            var buyerBlock = new TableCell();
            buyerBlock.Padding = new Thickness(6);
            buyerBlock.Blocks.Add(new Paragraph(new Bold(new Run(!string.IsNullOrEmpty(inv.CompanyName) ? inv.CompanyName : inv.CustomerName))) { Margin = new Thickness(0, 0, 0, 2) });
            if (!string.IsNullOrEmpty(inv.CompanyName)) buyerBlock.Blocks.Add(new Paragraph(new Run($"Attn: {inv.CustomerName}")) { FontSize = 8.5, Margin = new Thickness(0, 0, 0, 2) });
            buyerBlock.Blocks.Add(new Paragraph(new Run($"{inv.BillingAddress}, {inv.City} {inv.Pincode}")) { Margin = new Thickness(0, 0, 0, 2) });
            buyerBlock.Blocks.Add(new Paragraph(new Run($"State: {inv.State}  |  Phone: {inv.Phone}")) { FontSize = 8.5, Margin = new Thickness(0, 0, 0, 2) });
            buyerBlock.Blocks.Add(new Paragraph(new Run($"Transport: {inv.PreferedTransport ?? "Direct Dispatch"}")) { FontSize = 8.5, Margin = new Thickness(0) });

            partyBodyRow.Cells.Add(sellerBlock);
            partyBodyRow.Cells.Add(buyerBlock);

            var partyGroup = new TableRowGroup();
            partyGroup.Rows.Add(partyHeaderRow);
            partyGroup.Rows.Add(partyBodyRow);
            partyTable.RowGroups.Add(partyGroup);
            doc.Blocks.Add(partyTable);

            // 3. LINE ITEMS TABLE
            var itemsTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 10), BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)), BorderThickness = new Thickness(1, 1, 0, 0) };
            itemsTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.06) }); // #
            itemsTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.38) }); // Description
            itemsTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.12) }); // Batch
            itemsTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.08) }); // Qty
            itemsTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.12) }); // Rate
            itemsTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.10) }); // GST %
            itemsTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.14) }); // Total

            var iHeaderGroup = new TableRowGroup();
            var iHeaderRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)) };
            iHeaderRow.Cells.Add(CreateCell(new Bold(new Run("#")), isHeader: true));
            iHeaderRow.Cells.Add(CreateCell(new Bold(new Run("Item Description & SKU")), isHeader: true));
            iHeaderRow.Cells.Add(CreateCell(new Bold(new Run("Batch #")), isHeader: true));
            iHeaderRow.Cells.Add(CreateCell(new Bold(new Run("Qty")), isHeader: true));
            iHeaderRow.Cells.Add(CreateCell(new Bold(new Run("Unit Rate")), isHeader: true));
            iHeaderRow.Cells.Add(CreateCell(new Bold(new Run("GST %")), isHeader: true));
            iHeaderRow.Cells.Add(CreateCell(new Bold(new Run("Amount (₹)")), isHeader: true));
            iHeaderGroup.Rows.Add(iHeaderRow);

            int seq = 1;
            foreach (var item in inv.Items)
            {
                var row = new TableRow();
                row.Cells.Add(CreateCell(new Run(seq++.ToString())));
                row.Cells.Add(CreateCell(new Bold(new Run(item.ProductName))));
                row.Cells.Add(CreateCell(new Run(item.BatchNumber ?? "—")));
                row.Cells.Add(CreateCell(new Run($"{item.Quantity} {item.Unit}")));
                row.Cells.Add(CreateCell(new Run(item.UnitPrice.ToString("N2"))));
                row.Cells.Add(CreateCell(new Run($"{item.GSTPercent:N0}%")));
                row.Cells.Add(CreateCell(new Bold(new Run(item.Total.ToString("N2")))));
                iHeaderGroup.Rows.Add(row);
            }

            itemsTable.RowGroups.Add(iHeaderGroup);
            doc.Blocks.Add(itemsTable);

            // 4. COMMERCIAL SUMMARY & BANK DETAILS SPLIT
            var summaryTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 10) };
            summaryTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.55) });
            summaryTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.45) });

            var sumRow = new TableRow();

            // Left: Bank & Remarks
            var leftSummary = new TableCell();
            leftSummary.Blocks.Add(new Paragraph(new Bold(new Run("Bank Account Details:"))) { Margin = new Thickness(0, 0, 0, 2) });
            leftSummary.Blocks.Add(new Paragraph(new Run($"Bank: {inv.SellerBankName}  |  A/C: {inv.SellerAccountNumber}")) { FontSize = 8.5, Margin = new Thickness(0, 0, 0, 1) });
            leftSummary.Blocks.Add(new Paragraph(new Run($"IFSC: {inv.SellerIfsc}  |  UPI: {inv.SellerUpi}")) { FontSize = 8.5, Margin = new Thickness(0, 0, 0, 6) });
            if (!string.IsNullOrEmpty(inv.Remarks))
                leftSummary.Blocks.Add(new Paragraph(new Run($"Remarks: {inv.Remarks}")) { FontSize = 8.5, Foreground = Brushes.DimGray, Margin = new Thickness(0) });

            // Right: Tax Calculation & Grand Total Breakdown
            var rightSummary = new TableCell();
            var calcTable = new Table { CellSpacing = 0, BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)), BorderThickness = new Thickness(1, 1, 0, 0) };
            calcTable.Columns.Add(new TableColumn { Width = new GridLength(1.3, GridUnitType.Star) });
            calcTable.Columns.Add(new TableColumn { Width = new GridLength(1.2, GridUnitType.Star) });
            var cGroup = new TableRowGroup();

            AddCalcRow(cGroup, "Taxable Amount:", $"₹ {inv.SubTotalAmount:N2}");

            if (inv.IsInterState)
            {
                AddCalcRow(cGroup, "IGST Amount:", $"₹ {inv.TotalGstAmount:N2}");
            }
            else
            {
                AddCalcRow(cGroup, "CGST Amount:", $"₹ {(inv.TotalGstAmount / 2):N2}");
                AddCalcRow(cGroup, "SGST Amount:", $"₹ {(inv.TotalGstAmount / 2):N2}");
            }

            if (inv.TotalExtraCharges > 0)
                AddCalcRow(cGroup, "Extra Charges:", $"₹ {inv.TotalExtraCharges:N2}");

            if (inv.TotalDiscounts > 0)
                AddCalcRow(cGroup, "Discount:", $"-₹ {inv.TotalDiscounts:N2}");

            AddCalcRow(cGroup, "Grand Total:", $"₹ {inv.GrandTotal:N2}", isGrand: true);
            AddCalcRow(cGroup, "Amount Paid:", $"₹ {inv.AmountPaid:N2}");
            AddCalcRow(cGroup, "Balance Due:", $"₹ {inv.BalanceDue:N2}", isDue: true);

            calcTable.RowGroups.Add(cGroup);
            rightSummary.Blocks.Add(calcTable);

            sumRow.Cells.Add(leftSummary);
            sumRow.Cells.Add(rightSummary);
            var sumGroup = new TableRowGroup();
            sumGroup.Rows.Add(sumRow);
            summaryTable.RowGroups.Add(sumGroup);
            doc.Blocks.Add(summaryTable);

            // 5. TERMS & SIGNATURE BLOCK
            var footerTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 10, 0, 0) };
            footerTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.60) });
            footerTable.Columns.Add(new TableColumn { Width = new GridLength(contentWidth * 0.40) });

            var fRow = new TableRow();
            var termsCell = new TableCell();
            termsCell.Blocks.Add(new Paragraph(new Bold(new Run("Terms & Conditions:"))) { FontSize = 8.5, Margin = new Thickness(0, 0, 0, 2) });
            termsCell.Blocks.Add(new Paragraph(new Run(string.IsNullOrWhiteSpace(inv.TermsAndConditions) ? "1. Goods once sold will not be taken back.\n2. Subject to local jurisdiction." : inv.TermsAndConditions)) { FontSize = 7.5, Foreground = Brushes.Gray, Margin = new Thickness(0) });

            var signCell = new TableCell();
            signCell.Blocks.Add(new Paragraph(new Run($"For {inv.SellerCompanyName}")) { TextAlignment = TextAlignment.Right, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 28) });
            signCell.Blocks.Add(new Paragraph(new Run("Authorized Signatory")) { TextAlignment = TextAlignment.Right, FontSize = 8.5, Foreground = Brushes.DimGray, Margin = new Thickness(0) });

            fRow.Cells.Add(termsCell);
            fRow.Cells.Add(signCell);
            var fGroup = new TableRowGroup();
            fGroup.Rows.Add(fRow);
            footerTable.RowGroups.Add(fGroup);
            doc.Blocks.Add(footerTable);

            return doc;
        }

        private static void AddCalcRow(TableRowGroup group, string label, string value, bool isGrand = false, bool isDue = false)
        {
            var row = new TableRow();
            if (isGrand) row.Background = new SolidColorBrush(Color.FromRgb(220, 252, 231)); // Light green

            var c1 = CreateCell(new Bold(new Run(label)), isHeader: !isGrand && !isDue);
            var c2 = CreateCell(new Bold(new Run(value)) { Foreground = isDue ? Brushes.Crimson : (isGrand ? new SolidColorBrush(Color.FromRgb(21, 128, 61)) : Brushes.Black) });
            c2.TextAlignment = TextAlignment.Right;

            row.Cells.Add(c1);
            row.Cells.Add(c2);
            group.Rows.Add(row);
        }

        private static TableCell CreateCell(Inline inline, bool isHeader = false)
        {
            var cell = new TableCell(new Paragraph(inline) { Margin = new Thickness(0) })
            {
                Padding = new Thickness(5, 3, 5, 3),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(0, 0, 1, 1)
            };
            if (isHeader) cell.Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
            return cell;
        }
    }
}
