using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace CallMan.Models
{
    public partial class ProformaHeader : ObservableObject
    {
        public int ProformaId { get; set; }
        public string ProformaNumber { get; set; } = string.Empty;
        public int LeadId { get; set; }
        public string CustomerName { get; set; } = string.Empty;

        // Structured Layout Form Bindings matching your native order view fields
        [ObservableProperty] private string _billTo = string.Empty;
        [ObservableProperty] private string _deliverTo = string.Empty;
        [ObservableProperty] private string _termsAndConditions = "30% ADVANCE WILL BE REQUIRED";
        [ObservableProperty] private string _preferedTransport = string.Empty;
        [ObservableProperty] private string _internalRemarks = string.Empty;
        [ObservableProperty] private DateTime? _nextFollowupDate = DateTime.Now.AddDays(1);

        // Financial breakdowns
        [ObservableProperty] private decimal _itemSubTotal;
        [ObservableProperty] private decimal _extraChargesTotal;
        [ObservableProperty] private decimal _grandTotal;
        [ObservableProperty] private decimal _totalPaid;
        [ObservableProperty] private decimal _balanceDue;
        [ObservableProperty] private string _proformaStatus = "Quotation";

        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ObservableCollection<ProformaLineItem> Items { get; set; } = new();
        public ObservableCollection<ProformaExtraChargeItem> ExtraCharges { get; set; } = new();
    }

    public partial class ProformaLineItem : ObservableObject
    {
        public int ProformaItemId { get; set; }
        public int ProformaId { get; set; }
        public int? ProductId { get; set; }

        [ObservableProperty] private string _batchNo = string.Empty;
        [ObservableProperty] private string _productName = string.Empty;

        // Changing these properties will automatically refresh the SubTotal binding in your DataGrid
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SubTotal))]
        private int _quantity = 1;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SubTotal))]
        private decimal _unitPrice; // Exclusive of GST Base Rate

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SubTotal))]
        private decimal _gstPercent;

        public int IsCustom { get; set; }

        [ObservableProperty] private byte[]? _productImageBlob;

        // ====================================================================
        // FIXED: SubTotal now dynamically calculates and exposes the 
        // true compounded Exclusive-Rate + GST total value directly to your XAML
        // ====================================================================
        public decimal SubTotal
        {
            get
            {
                decimal baseValue = Quantity * UnitPrice;
                decimal taxComponent = baseValue * (GstPercent / 100);
                return baseValue + taxComponent; // Example: ₹95,000 + 18% GST = ₹112,100.00
            }
        }

        /// <summary>
        /// UI VISUAL HELPER: Dynamically decodes database image binary streams for your grid rows
        /// </summary>
        public BitmapImage? LineItemImageSource
        {
            get
            {
                if (ProductImageBlob == null || ProductImageBlob.Length == 0) return null;
                try
                {
                    using (var stream = new MemoryStream(ProductImageBlob))
                    {
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.StreamSource = stream;
                        image.EndInit();
                        image.Freeze();
                        return image;
                    }
                }
                catch { return null; }
            }
        }
    }

    public partial class ProformaExtraChargeItem : ObservableObject
    {
        public int ExtraChargeId { get; set; }
        public int ProformaId { get; set; }

        [ObservableProperty] private string _chargeDescription = string.Empty;
        [ObservableProperty] private string _chargeAction = "Add (+)"; // 'Add (+)', 'Subtract (-)', 'Percentage (+)', 'Percentage (-)'
        [ObservableProperty] private decimal _baseValue;
        [ObservableProperty] private decimal _gstPercent;
        [ObservableProperty] private decimal _chargeAmount; // Calculated absolute final impact value
    }
}
