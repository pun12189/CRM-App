using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tijori.Interfaces;
using Tijori.Models;

namespace Tijori.Helper
{
    public static class TileCardAdapterExtensions
    {
        // ====================================================================
        // 1. LEAD ADAPTER (Includes Structured Address, Follow-up & Actions)
        // ====================================================================
        public static ITileCardItem ToTileCard(this Lead lead)
        {
            var card = new GenericTileCardItem
            {
                IsSelectedForAction = lead.IsSelectedForAction,
                HeaderTag = !string.IsNullOrWhiteSpace(lead.CompanyName) ? $"({lead.CompanyName})" : "(No Company)",
                PrimaryTitle = lead.CustomerName,
                BadgeText = lead.Initials,
                BadgeBackgroundHex = "#E0F2FE",
                BadgeForegroundHex = "#0369A1",
                OwnerOrMetaLabel = $"Holder: {lead.LeadHolder ?? "Unassigned"}",
                // 1. Dedicated Status Mapping
                StatusText = lead.Status ?? "New",
                StatusColorHex = lead.Status?.ToLower() switch
                {
                    "matured" => "#DCFCE7",
                    "dead" => "#FEE2E2",
                    "follow-up" or "followup" => "#E0F2FE",
                    _ => "#F1F5F9"
                },
                StatusTextColorHex = lead.Status?.ToLower() switch
                {
                    "matured" => "#15803D",
                    "dead" => "#B91C1C",
                    "follow-up" or "followup" => "#0369A1",
                    _ => "#475569"
                },

                // 2. Dedicated Single Tag Mapping
                TagText = lead.LeadTag ?? string.Empty,
                TagColorHex = "#FEF3C7",       // Light Amber / Gold
                TagTextColorHex = "#B45309",   // Dark Amber

                SourceOrCategory = lead.LeadSource ?? "Direct",
                RawModel = lead
            };

            // Dynamic Hover Details
            if (!string.IsNullOrWhiteSpace(lead.Phone))
                card.ExpandedDetails.Add(new() { IconKind = "Phone", Label = "Phone", Value = lead.Phone });

            if (!string.IsNullOrWhiteSpace(lead.Email))
                card.ExpandedDetails.Add(new() { IconKind = "EmailOutline", Label = "Email", Value = lead.Email });

            string fullLocation = string.Join(", ", new[] { lead.City, lead.District, lead.State, lead.Pincode }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (!string.IsNullOrWhiteSpace(fullLocation))
                card.ExpandedDetails.Add(new() { IconKind = "MapMarkerOutline", Label = "Location", Value = fullLocation });

            if (lead.LatestUpdate != null)
            {
                card.ExpandedDetails.Add(new() { IconKind = "CalendarClock", Label = "Follow-up", Value = $"{lead.LatestUpdate.NextFollowUpDate:dd-MMM-yyyy hh:mm tt}", ValueColorHex = "#2563EB" });
                card.ExpandedDetails.Add(new() { IconKind = "ChatProcessingOutline", Label = "Last Note", Value = lead.LatestUpdate.Message ?? string.Empty });
            }

            if (lead.LeadLabels != null && lead.LeadLabels.Any())
                card.BadgesOrLabels = lead.LeadLabels.ToList();

            return card;
        }

        // ====================================================================
        // 2. PRODUCT ADAPTER
        // ====================================================================
        public static ITileCardItem ToTileCard(this Product product)
        {
            var card = new GenericTileCardItem
            {
                HeaderTag = !string.IsNullOrWhiteSpace(product.BrandName) ? $"({product.BrandName})" : "(No Brand)",
                PrimaryTitle = product.Name,
                BadgeText = product.Name.Length >= 2 ? product.Name.Substring(0, 2).ToUpper() : "PR",
                BadgeBackgroundHex = "#F3E8FF",
                BadgeForegroundHex = "#7E22CE",
                OwnerOrMetaLabel = $"SKU: {product.SKU}",
                StatusText = product.RemainingStock > 0 ? $"In Stock ({product.RemainingStock} {product.Unit})" : "Out of Stock",
                StatusColorHex = product.RemainingStock > 0 ? "#DCFCE7" : "#FEE2E2",
                StatusTextColorHex = product.RemainingStock > 0 ? "#15803D" : "#B91C1C",
                SourceOrCategory = product.CategoryName,
                RawModel = product
            };

            card.ExpandedDetails.Add(new() { IconKind = "CurrencyInr", Label = "Selling Price", Value = $"₹{product.SellingPrice:N2} (+{product.GstPercent}% GST)", ValueColorHex = "#0D9488" });
            card.ExpandedDetails.Add(new() { IconKind = "PackageVariantClosed", Label = "Packaging", Value = product.Packaging });
            card.ExpandedDetails.Add(new() { IconKind = "Factory", Label = "Manufacturer", Value = product.Manufacturer });

            if (product.ExpiryDate.HasValue)
                card.ExpandedDetails.Add(new() { IconKind = "CalendarAlert", Label = "Expiry Date", Value = $"{product.ExpiryDate:dd-MMM-yyyy}", ValueColorHex = "#DC2626" });

            return card;
        }

        // ====================================================================
        // 3. ORDER ADAPTER
        // ====================================================================
        public static ITileCardItem ToTileCard(this Order order)
        {
            var card = new GenericTileCardItem
            {
                HeaderTag = !string.IsNullOrWhiteSpace(order.FirmName) ? $"({order.FirmName})" : "(Individual)",
                PrimaryTitle = order.CustomerName ?? order.FormattedOrderId,
                BadgeText = "ORD",
                BadgeBackgroundHex = "#FEF3C7",
                BadgeForegroundHex = "#B45309",
                OwnerOrMetaLabel = $"Holder: {order.LeadHolder ?? "Staff"}",
                StatusText = order.PaymentStatus,
                StatusColorHex = order.PaymentStatus == "Paid" ? "#DCFCE7" : "#FEF3C7",
                StatusTextColorHex = order.PaymentStatus == "Paid" ? "#15803D" : "#B45309",
                SourceOrCategory = order.FormattedOrderId,
                RawModel = order
            };

            card.ExpandedDetails.Add(new() { IconKind = "CurrencyInr", Label = "Grand Total", Value = $"₹{order.GrandTotal:N2}", ValueColorHex = "#16A34A" });
            card.ExpandedDetails.Add(new() { IconKind = "CashSync", Label = "Balance Due", Value = $"₹{order.OrderBalance:N2}", ValueColorHex = order.OrderBalance > 0 ? "#DC2626" : "#16A34A" });
            card.ExpandedDetails.Add(new() { IconKind = "TruckDeliveryOutline", Label = "Transport", Value = order.PreferedTransport ?? "Standard" });
            card.ExpandedDetails.Add(new() { IconKind = "CalendarCheck", Label = "Order Date", Value = $"{order.OrderDate:dd-MMM-yyyy}" });

            return card;
        }

        // ====================================================================
        // 4. PURCHASE ORDER ADAPTER
        // ====================================================================
        public static ITileCardItem ToTileCard(this PurchaseOrder po)
        {
            var card = new GenericTileCardItem
            {
                HeaderTag = $"({po.VendorName})",
                PrimaryTitle = po.PoNumber,
                BadgeText = "PO",
                BadgeBackgroundHex = "#E0E7FF",
                BadgeForegroundHex = "#4338CA",
                OwnerOrMetaLabel = $"Created By: {po.CreatedBy}",
                StatusText = po.IsDelayed ? $"Delayed ({po.DelayInDays}d)" : po.OrderStatus,
                StatusColorHex = po.IsDelayed ? "#FEE2E2" : "#DCFCE7",
                StatusTextColorHex = po.IsDelayed ? "#B91C1C" : "#15803D",
                SourceOrCategory = $"₹{po.TotalAmount:N2}",
                RawModel = po
            };

            card.ExpandedDetails.Add(new() { IconKind = "CalendarClock", Label = "Expected Date", Value = $"{po.ExpectedDeliveryDate:dd-MMM-yyyy}" });
            if (po.ActualDeliveryDate.HasValue)
                card.ExpandedDetails.Add(new() { IconKind = "CalendarCheck", Label = "Received Date", Value = $"{po.ActualDeliveryDate:dd-MMM-yyyy}" });

            return card;
        }

        // ====================================================================
        // 5. VENDOR ADAPTER
        // ====================================================================
        public static ITileCardItem ToTileCard(this Vendor vendor)
        {
            var card = new GenericTileCardItem
            {
                HeaderTag = $"({vendor.CompanyName})",
                PrimaryTitle = vendor.ContactPerson ?? vendor.CompanyName,
                BadgeText = vendor.CompanyName.Length >= 2 ? vendor.CompanyName.Substring(0, 2).ToUpper() : "VN",
                BadgeBackgroundHex = "#FEE2E2",
                BadgeForegroundHex = "#B91C1C",
                OwnerOrMetaLabel = $"GST: {vendor.GstNumber ?? "N/A"}",
                StatusText = vendor.Status,
                StatusColorHex = vendor.Status == "Active" ? "#DCFCE7" : "#F1F5F9",
                StatusTextColorHex = vendor.Status == "Active" ? "#15803D" : "#64748B",
                SourceOrCategory = "Vendor",
                RawModel = vendor
            };

            if (!string.IsNullOrWhiteSpace(vendor.Phone))
                card.ExpandedDetails.Add(new() { IconKind = "Phone", Label = "Phone", Value = vendor.Phone });
            if (!string.IsNullOrWhiteSpace(vendor.Email))
                card.ExpandedDetails.Add(new() { IconKind = "EmailOutline", Label = "Email", Value = vendor.Email });
            if (!string.IsNullOrWhiteSpace(vendor.Address))
                card.ExpandedDetails.Add(new() { IconKind = "MapMarkerOutline", Label = "Address", Value = vendor.Address });

            return card;
        }

        // ====================================================================
        // 6. USER / STAFF ADAPTER
        // ====================================================================
        public static ITileCardItem ToTileCard(this User user)
        {
            var card = new GenericTileCardItem
            {
                HeaderTag = !string.IsNullOrWhiteSpace(user.DepartmentName) ? $"({user.DepartmentName})" : "(No Dept)",
                PrimaryTitle = user.FullName,
                BadgeText = user.FullName.Length >= 2 ? user.FullName.Substring(0, 2).ToUpper() : "US",
                BadgeBackgroundHex = "#EDE9FE",
                BadgeForegroundHex = "#6D28D9",
                OwnerOrMetaLabel = $"Senior: {user.SeniorName ?? "None"}",
                StatusText = user.IsActive ? "Active" : "Inactive",
                StatusColorHex = user.IsActive ? "#DCFCE7" : "#FEE2E2",
                StatusTextColorHex = user.IsActive ? "#15803D" : "#B91C1C",
                SourceOrCategory = user.Role.ToString(),
                RawModel = user
            };

            if (!string.IsNullOrWhiteSpace(user.Phone))
                card.ExpandedDetails.Add(new() { IconKind = "Phone", Label = "Phone", Value = user.Phone });
            if (!string.IsNullOrWhiteSpace(user.Email))
                card.ExpandedDetails.Add(new() { IconKind = "EmailOutline", Label = "Email", Value = user.Email });
            card.ExpandedDetails.Add(new() { IconKind = "TargetAccount", Label = "Monthly Target", Value = $"₹{user.MonthlyTarget:N2}", ValueColorHex = "#2563EB" });

            return card;
        }
    }
}
