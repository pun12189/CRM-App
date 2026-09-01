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
    public partial class GenerateDebitNoteViewModel : ObservableObject
    {
        private readonly ReturnService _returnService;

        [ObservableProperty] private int _purchaseOrderId;
        [ObservableProperty] private int _vendorId;
        [ObservableProperty] private string _vendorName = string.Empty;
        [ObservableProperty] private string _poNumber = string.Empty;
        [ObservableProperty] private string _debitNoteNo = $"DN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";
        [ObservableProperty] private DateTime _returnDate = DateTime.Today;
        [ObservableProperty] private string _reason = "Defective / Damaged Goods";
        [ObservableProperty] private decimal _calculatedTotal;
        [ObservableProperty] private bool _isSaving;
        [ObservableProperty] private string _statusMessage = string.Empty;

        public ObservableCollection<PurchaseReturnDetail> ReturnLines { get; } = new();
        public Action<bool>? RequestClose { get; set; }

        public GenerateDebitNoteViewModel(ReturnService returnService)
        {
            _returnService = returnService;
        }

        public void LoadFromPurchase(PurchaseOrder po, IEnumerable<PurchaseOrderDetail> details, string? batchNum)
        {
            PurchaseOrderId = po.PurchaseOrderId;
            VendorId = po.VendorId;
            VendorName = po.VendorName;
            PoNumber = po.PoNumber;

            ReturnLines.Clear();
            foreach (var d in details)
            {
                var line = new PurchaseReturnDetail
                {
                    ProductId = d.ProductId,
                    ProductName = d.ProductName,
                    BatchNumber = batchNum ?? $"BAT-PO{po.PurchaseOrderId}",
                    Quantity = 0,
                    MaxAvailableQty = d.Quantity,
                    UnitPrice = d.UnitPrice,
                    TaxPercent = 5.0m
                };
                line.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(PurchaseReturnDetail.Quantity))
                    {
                        if (line.Quantity > line.MaxAvailableQty) line.Quantity = line.MaxAvailableQty;
                        decimal sub = line.Quantity * line.UnitPrice;
                        line.TaxAmount = sub * (line.TaxPercent / 100m);
                        line.TotalAmount = sub + line.TaxAmount;
                        RecalculateGrandTotal();
                    }
                };
                ReturnLines.Add(line);
            }
        }

        private void RecalculateGrandTotal()
        {
            CalculatedTotal = ReturnLines.Sum(l => l.TotalAmount);
        }

        [RelayCommand]
        private async Task SaveDebitNoteAsync()
        {
            var validLines = ReturnLines.Where(l => l.Quantity > 0).ToList();
            if (!validLines.Any())
            {
                StatusMessage = "Please specify return quantity (> 0) for at least one item.";
                return;
            }

            IsSaving = true;
            StatusMessage = "Generating Debit Note and updating stock...";

            try
            {
                var pr = new PurchaseReturn
                {
                    ReturnDebitNo = DebitNoteNo,
                    VendorId = VendorId,
                    PurchaseOrderId = PurchaseOrderId,
                    ReturnDate = ReturnDate,
                    Reason = Reason,
                    TotalAmount = CalculatedTotal,
                    TaxAmount = validLines.Sum(l => l.TaxAmount),
                    Status = "Completed",
                    CreatedBy = "Admin"
                };

                await _returnService.CreatePurchaseReturnAsync(pr, validLines);
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
