using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Tijori.Models.Enums;

namespace Tijori.Models
{
    public partial class Product : ObservableObject
    {
        [ObservableProperty] private bool _isSelectedForAction;

        public int ProductId { get; set; }
        public int DivisionId { get; set; } = 1;
        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private string _shortName = string.Empty;
        [ObservableProperty] private string _brandName = string.Empty;
        [ObservableProperty] private string _sKU = string.Empty;
        [ObservableProperty] private string _unit = "Pcs";
        [ObservableProperty] private int _categoryId;
        [ObservableProperty] private string _categoryName = string.Empty; // For Display
        public ItemClassification CategoryType { get; set; }

        [ObservableProperty] private string _manufacturer = string.Empty;
        [ObservableProperty] private string _packaging = string.Empty;

        // Stock
        [ObservableProperty] private int _remainingStock;
        [ObservableProperty] private int _initialStock;

        [ObservableProperty] private DateTime? _mfgDate;
        [ObservableProperty] private DateTime? _expiryDate;

        // Costing
        [ObservableProperty] private decimal _mRP;
        [ObservableProperty] private decimal _costPrice;
        [ObservableProperty] private decimal _sellingPrice;
        [ObservableProperty] private decimal _gstPercent;

        [ObservableProperty] private bool _trackCost = true;

        [ObservableProperty] private bool _hasBatchTracking = true;

        [ObservableProperty] private bool _isExpanded;

        [ObservableProperty] private int _reorderQuantity = 50;
        [ObservableProperty] private bool _autoReorderEnabled = true;

        // Calculated Property
        public decimal TotalCost => SellingPrice + (SellingPrice * (GstPercent / 100));

        /// <summary>
        /// Holds the on-demand loaded batches for this specific product row.
        /// Bound directly to the nested child DataGrid in the RowDetailsTemplate.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ProductBatch> _innerBatchesCollection = new();

        /// <summary>
        /// Stores the summary count of how many batches exist for this product.
        /// Displays inside the primary DataGrid column template link button.
        /// </summary>
        [ObservableProperty]
        private int _totalBatchesCount;

        // Refresh UI when components change
        partial void OnSellingPriceChanged(decimal value) => OnPropertyChanged(nameof(TotalCost));
        partial void OnGstPercentChanged(decimal value) => OnPropertyChanged(nameof(TotalCost));

        [ObservableProperty] private byte[]? _productImageBytes;

        public bool IsStockThresholdBreached
        {
            get
            {
                if (!AutoReorderEnabled) return false;

                // Parse StockKeepingUnit / SKU limit safely
                if (int.TryParse(SKU, out int threshold))
                {
                    return RemainingStock <= threshold;
                }
                return false;
            }
        }

        /// <summary>
        /// UI VISUAL HELPER: Dynamically decodes database image binary streams for your main products list dashboards
        /// </summary>
        public BitmapImage? CatalogImageSource
        {
            get
            {
                if (ProductImageBytes == null || ProductImageBytes.Length == 0) return null;
                try
                {
                    using (var stream = new MemoryStream(ProductImageBytes))
                    {
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.StreamSource = stream;
                        image.EndInit();
                        image.Freeze(); // Freeze to enable safe multi-threaded rendering over UI channels
                        return image;
                    }
                }
                catch { return null; }
            }
        }
    }
}
