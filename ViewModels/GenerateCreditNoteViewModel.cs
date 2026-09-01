using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tijori.Models;
using Tijori.Services;

namespace Tijori.ViewModels
{
    public partial class GenerateCreditNoteViewModel : ObservableObject
    {
        private readonly ReturnService _returnService;

        [ObservableProperty] private int _orderId;
        [ObservableProperty] private int? _customerId;
        [ObservableProperty] private string _customerName = string.Empty;
        [ObservableProperty] private string _orderNumber = string.Empty;
        [ObservableProperty] private string _creditNoteNo = $"CN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";
        [ObservableProperty] private DateTime _returnDate = DateTime.Today;
        [ObservableProperty] private string _reason = "Customer Return / Excess Stock";
        [ObservableProperty] private decimal _calculatedTotal;
        [ObservableProperty] private bool _isSaving;
        [ObservableProperty] private string _statusMessage = string.Empty;

        public ObservableCollection<SalesReturnDetail> ReturnLines { get; } = new();
        public Action<bool>? RequestClose { get; set; }

        public GenerateCreditNoteViewModel(ReturnService returnService)
        {
            _returnService = returnService;
        }

        public void LoadFromOrder(int orderId, int? customerId, string customerName, string orderNo, IEnumerable<(int ProductId, string Name, string Batch, int SoldQty, decimal Price)> items)
        {
            OrderId = orderId;
            CustomerId = customerId;
            CustomerName = customerName;
            OrderNumber = orderNo;

            ReturnLines.Clear();
            foreach (var item in items)
            {
                var line = new SalesReturnDetail
                {
                    ProductId = item.ProductId,
                    ProductName = item.Name,
                    BatchNumber = item.Batch,
                    Quantity = 0,
                    MaxAvailableQty = item.SoldQty,
                    UnitPrice = item.Price,
                    TaxPercent = 5.0m
                };
                line.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(SalesReturnDetail.Quantity))
                    {
                        if (line.Quantity > line.MaxAvailableQty) line.Quantity = line.MaxAvailableQty;
                        decimal sub = line.Quantity * line.UnitPrice;
                        line.TaxAmount = sub * (line.TaxPercent / 100m);
                        line.TotalAmount = sub + line.TaxAmount;
                        CalculatedTotal = ReturnLines.Sum(l => l.TotalAmount);
                    }
                };
                ReturnLines.Add(line);
            }
        }

        [RelayCommand]
        private async Task SaveCreditNoteAsync()
        {
            var validLines = ReturnLines.Where(l => l.Quantity > 0).ToList();
            if (!validLines.Any())
            {
                StatusMessage = "Please specify return quantity (> 0) for at least one item.";
                return;
            }

            IsSaving = true;
            StatusMessage = "Generating Credit Note and restocking inventory...";

            try
            {
                var sr = new SalesReturn
                {
                    CreditNoteNo = CreditNoteNo,
                    CustomerId = CustomerId,
                    OrderId = OrderId,
                    ReturnDate = ReturnDate,
                    Reason = Reason,
                    TotalAmount = CalculatedTotal,
                    TaxAmount = validLines.Sum(l => l.TaxAmount),
                    Status = "Completed",
                    CreatedBy = "Admin"
                };

                await _returnService.CreateSalesReturnAsync(sr, validLines);
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                StatusMessage = "Error: " + ex.Message;
            }
            finally
            {
                IsSaving = false;
            }
        }

        [RelayCommand]
        private void Cancel() => RequestClose?.Invoke(false);
    }
}
