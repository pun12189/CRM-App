using CommunityToolkit.Mvvm.ComponentModel;

namespace Tijori.Models
{
    public partial class ProductBatch : ObservableObject
    {
        public int BatchId { get; set; }
        public int ProductId { get; set; }
        public int DivisionId { get; set; }

        [ObservableProperty] private string _batchNumber = string.Empty;
        [ObservableProperty] private DateTime? _mfgDate;
        [ObservableProperty] private DateTime? _expiryDate;
        [ObservableProperty] private int _quantityReceived;
        [ObservableProperty] private int _currentStock;

        /// <summary>
        /// Equivalent to the unique landing/purchase price for this specific batch.
        /// </summary>
        [ObservableProperty] private decimal _minimumSellingPrice;

        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Automatically runs whenever the user modifies the CurrentStock value in the UI grid.
        /// </summary>
        partial void OnCurrentStockChanged(int value)
        {
            // If this is a brand-new batch row being typed in for the first time,
            // automatically copy the value over to QuantityReceived.
            if (BatchId == 0)
            {
                QuantityReceived = value;
            }
        }

        // Helper property to check if the batch has expired compared to the current date (2026)
        public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value.Date < DateTime.Today;
    }
}
