using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class StockLedger : ObservableObject
    {
        [ObservableProperty] private int _ledgerId;
        [ObservableProperty] private int _productId;
        [ObservableProperty] private string _productName = string.Empty;
        [ObservableProperty] private string _productCode = string.Empty;
        [ObservableProperty] private string? _batchNumber;
        [ObservableProperty] private string _movementType = "Production_Consume";
        [ObservableProperty] private decimal _quantity;
        [ObservableProperty] private string _unit = "Kg";
        [ObservableProperty] private string _referenceDocument = string.Empty;
        [ObservableProperty] private string? _notes;
        [ObservableProperty] private DateTime _createdDate = DateTime.Now;
    }
}
