using CommunityToolkit.Mvvm.ComponentModel;
using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class CustomFieldDefinition : ObservableObject
    {
        [ObservableProperty] private int _fieldId;
        [ObservableProperty] private string _fieldName = string.Empty;
        [ObservableProperty] private string? _displayLabel;
        [ObservableProperty] private string _fieldType = "Textbox"; // Textbox, TextArea, DropdownSingle, DropdownMultiple, CalendarClock

        // --- NEW SCHEMA COLUMNS ---
        [ObservableProperty] private string _moduleType = "Lead"; // Lead, Customer, Product, Order, Purchase, Vendor, Staff
        [ObservableProperty] private int _fieldTier = 3;          // 1 = System Mandatory, 2 = Optional Model Field, 3 = Dynamic Custom Field
        [ObservableProperty] private bool _isVisible = true;      // Universal Visibility Flag
        [ObservableProperty] private bool _isRequired;           // Universal Required Validation Flag

        [ObservableProperty] private bool _isFilter;
        [ObservableProperty] private bool _isAdmin;
        [ObservableProperty] private bool _inPdf;

        // Raw Database JSON Storage Holder for Dropdown Options
        [ObservableProperty] private string? _seedValues;

        // Non-persisted transient runtime helper list used for WPF ItemsControl binding lookups
        [ObservableProperty] private ObservableCollection<string> _seedValueOptionsList = new();

        // Runtime UI state management helper flags
        [ObservableProperty] private bool _isSelected;
        [ObservableProperty] private int _rowIndex;

        /// <summary>
        /// Returns contextual tooltips for Tier 1, Tier 2, and Tier 3 fields to guide users on functional impact.
        /// </summary>
        public string InfoTooltip => FieldTier switch
        {
            1 => $"Mandatory Core Field ({FieldName}) - Essential key required for database integrity.",

            2 => GetTier2Tooltip(ModuleType, FieldName),

            _ => $"Custom Dynamic Field ({FieldName}) - Control Type: {FieldType}"
        };

        /// <summary>
        /// Helper property to display effective UI label (DisplayLabel if available, otherwise FieldName)
        /// </summary>
        public string EffectiveLabel => !string.IsNullOrWhiteSpace(DisplayLabel) ? DisplayLabel : FieldName;

        /// <summary>
        /// Badge text helper for Tier level
        /// </summary>
        public string TierBadgeText => FieldTier switch
        {
            1 => "Mandatory Core",
            2 => "Standard Model",
            _ => "Custom Extended"
        };

        public string TierBadgeColor => FieldTier switch
        {
            1 => "#DC2626", // Red
            2 => "#2563EB", // Blue
            _ => "#0D9488"  // Teal
        };

        private static string GetTier2Tooltip(string moduleType, string fieldName)
        {
            string key = $"{moduleType?.TrimEnd('s')}:{fieldName}";

            return key switch
            {
                // ================= LEADS =================
                "Lead:Email" => "Primary email address for sending estimates, automated follow-ups, and email campaigns.",
                "Lead:AltPhone" => "Secondary contact number for SMS updates and emergency reachability.",
                "Lead:CompanyName" => "Business or organization name. Used to group multiple lead contacts under one account.",
                "Lead:AddressLine" => "Street address or premise details for location-based mapping and site visits.",
                "Lead:Pincode" => "Postal code used for automatic territory routing and regional lead assignment.",
                "Lead:City" => "City location used for geographical sales filtering and regional reporting.",
                "Lead:District" => "District division used for regional sales grouping and territory analysis.",
                "Lead:State" => "State location required for interstate vs. intrastate GST tax calculations.",
                "Lead:Country" => "Country identifier used for international lead segmentation and currency formatting.",
                "Lead:BestTimeToTalk" or "Customer:BestTimeToTalk"
    => "Preferred time window for phone outreach (e.g. Morning 10-12 AM, Evening 4-6 PM).",

                "Lead:DOB" or "Customer:DOB"
                    => "Date of Birth. Used for automated birthday greeting triggers and promotional offers.",

                "Lead:Anniversary" or "Customer:Anniversary"
                    => "Marriage or Business Incorporation anniversary date for relationship management alerts.",

                "Lead:DivisionId" or "Customer:DivisionId"
                    => "Associates record with a specific business division or branch office.",

                "Lead:LeadSourceId" or "Customer:LeadSourceId"
                    => "Tracks the acquisition channel (e.g., Website, Exhibition, Referral, Cold Call).",

                "Lead:LeadTagIds" or "Customer:LeadTagIds"
                    => "Multi-select tags for flexible customer segment categorization (e.g., VIP, High Priority, Hot Lead).",

                "Lead:LeadLabelIds" or "Customer:LeadLabelIds"
                    => "Color-coded visual status labels for pipeline Kanban board tracking.",

                // ================= CUSTOMERS =================
                "Customer:Email" => "Primary billing email for sending invoices, account statements, and receipt confirmations.",
                "Customer:AltPhone" => "Secondary phone contact for delivery dispatch alerts and accounts payable follow-ups.",
                "Customer:CompanyName" => "Registered business entity name printed on tax invoices and legal ledger statements.",
                "Customer:AddressLine" => "Registered business billing address required for tax compliance and ledger entries.",
                "Customer:Pincode" => "Postal code used for dispatch zone mapping and regional courier fee estimates.",
                "Customer:City" => "Billing city used for localized customer grouping and regional sales analytics.",
                "Customer:District" => "Administrative district used for sales representative coverage and logistics routing.",
                "Customer:State" => "State location used to determine Place of Supply for CGST/SGST vs. IGST calculations.",
                "Customer:Country" => "Country location required for international billing and export invoice compliance.",
                "Customer:WorkingArea" => "Specific market zone or operational beat assigned to local field representatives.",
                "Customer:MonthlyTarget" => "Target revenue goal allocated to this customer account for periodic sales tracking.",

                // ================= PRODUCTS =================
                "Product:ShortName" => "Abbreviated item name for quick UI search, POS thermal receipts, and mobile views.",
                "Product:SKU" => "Stock Keeping Unit. Unique internal inventory code used for barcode scanning and warehouse tracking.",
                "Product:Unit" => "Primary measurement unit (e.g., Pcs, Box, Kg, Mtr) for billing and stock inventory control.",
                "Product:CategoryId" => "Links item to a master product category for organized cataloging and profit margin reporting.",
                "Product:BrandName" => "Manufacturer brand tag used for brand-wise sales filtering and promotional group discounts.",
                "Product:Manufacturer" => "Manufacturing entity name printed on compliance tags and quality assurance documentation.",
                "Product:Packaging" => "Pack size description (e.g., 10x10 Strip, 12 Pcs Box) used for wholesale bulk orders.",
                "Product:InitialStock" => "Opening inventory count recorded during initial system setup or stock intake.",
                "Product:CostPrice" => "Base purchase cost per unit used to calculate net profit margins on sales orders.",
                "Product:MRP" => "Maximum Retail Price printed on packaging. Serves as the ceiling for consumer billing.",
                "Product:SellingPrice" => "Default selling price before item-level or invoice-level promotional discounts.",
                "Product:GSTPercent" => "Applicable GST tax rate percentage used for automatic invoice tax breakup.",
                "Product:BatchNumber" => "Enables batch-wise inventory tracking for lot-based stock management and expiry alerts.",
                "Product:MfgDate" => "Production date used for calculating product shelf life and warranty coverage.",
                "Product:ExpiryDate" => "Expiration date used to trigger Near-Expiry alerts and prevent selling expired stock.",

                // ================= VENDORS =================
                "Vendor:ContactPerson" => "Primary point of contact at the supplier organization for order coordination.",
                "Vendor:Email" => "Supplier email address for sending digital Purchase Orders and payment advice notes.",
                "Vendor:GstNumber" => "15-digit GSTIN used to verify vendor eligibility for Input Tax Credit (ITC) claims.",
                "Vendor:Address" => "Supplier's registered business address used for purchase documentation and shipping origins.",
                "Vendor:Status" => "Active/Inactive account status flag. Inactive vendors are hidden from purchase order selection.",

                // ================= STAFF =================
                "Staff:Phone" => "Staff member's primary contact number for system login OTPs and internal communications.",
                "Staff:DepartmentId" => "Links staff to operational departments (e.g., Sales, Logistics, Accounts) for permission access.",
                "Staff:SeniorId" => "Designates reporting manager for approval workflows and hierarchical team reporting.",
                "Staff:MonthlyTarget" => "Assigned monthly sales quota for tracking individual KPI performance and incentives.",
                "Staff:IsActive" => "System access toggle. Deactivating revokes mobile app and desktop portal login credentials.",

                // ================= ORDERS =================
                "Order:InvoiceNumber" => "Mapped to Sales Invoices. System automatically generates sequential numbers if left blank.",
                "Order:ProformaNumber" => "Used for Proforma Quotes & Draft Orders. Enabling this maps the column during Excel imports.",
                "Order:OrderType" => "Categorizes transaction (e.g., Retail, Wholesale, B2B, Sample) for workflow processing.",
                "Order:PaymentStatus" => "Tracks payment clearance (e.g., Unpaid, Partial, Paid) to control dispatch approvals.",
                "Order:AmountPaid" => "Recorded cash/online payment received against the total order bill value.",
                "Order:ProcessedBy" => "Tracks staff assignment for commissions, audit trails, and performance analytics.",
                "Order:LeadHolder" => "Primary sales representative accountable for pipeline conversion and deal closing.",
                "Order:PreferedTransport" => "Designated courier or transport agency specified for goods dispatch logistics.",
                "Order:Status" => "Current fulfillment stage (e.g., Pending, Packed, Dispatched, Delivered, Cancelled).",
                "Order:Remarks" => "Internal operational notes hidden from customer-facing print invoices.",
                "Order:Description" => "Public order summary or terms & conditions printed on customer estimates and bills.",
                "Order:TotalCostAmount" => "Sum total COGS (Cost of Goods Sold) calculated for real-time order profit analysis.",
                "Order:DivisionId" => "Associates order with specific business branches, warehouses, or operating divisions.",
                "Order:BatchId" => "Links item to specific inventory batch records for traceable lot dispatch.",
                "Order:BatchNumber" => "Batch identification number printed on shipping manifests and compliance invoices.",
                "Order:ExpiryDate" => "Batch expiration date printed on delivery notes for regulated consumer goods.",
                "Order:CostPrice" => "Unit acquisition cost recorded at time of order placement for profit calculation.",
                "Order:GSTPercent" => "Applied tax percentage applied to item line entries.",
                "Order:SubTotal" => "Sum of all line items prior to adding shipping fees, taxes, and final discounts.",
                "Order:GstAmount" => "Total computed GST tax value (CGST + SGST or IGST) across all order items.",
                "Order:Total" => "Final net payable amount including items, taxes, shipping, and extra charges.",
                "Order:ChargeName" => "Label for additional order fees (e.g., Freight Charges, Packaging Fee, Expedited Shipping).",
                "Order:ChargeAmount" => "Monetary value added to total order bill for additional services.",

                // ================= PURCHASES =================
                "Purchase:ExpectedDeliveryDate" => "Target shipment arrival date used for warehouse dock planning and vendor follow-ups.",
                "Purchase:ActualDeliveryDate" => "Recorded date when stock physically arrived at the receiving bay.",
                "Purchase:OrderStatus" => "Lifecycle stage of Purchase Order (e.g., Draft, Issued, Partially Received, Completed).",
                "Purchase:CreatedBy" => "Purchasing agent or manager who authored and authorized the Purchase Order.",
                "Purchase:SupplierSku" => "Vendor's internal item part number used to simplify PO recognition during supply intake.",
                "Purchase:TotalCost" => "Aggregate cost of purchased inventory including item costs, freight, and vendor taxes.",

                // ================= DEFAULT FALLBACK =================
                _ => $"System Model Property ({fieldName}) - Standard database property for {moduleType}."
            };
        }
    }
}
