using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public partial class PurchaseOrder : ObservableObject
    {
        [ObservableProperty] private int _purchaseOrderId;
        [ObservableProperty] private string _poNumber = string.Empty; // e.g., PO-2026-0001
        [ObservableProperty] private int _vendorId;
        [ObservableProperty] private DateTime _orderDate = DateTime.Today;

        // --- NEW: Delivery Tracking Fields ---
        [ObservableProperty] private DateTime? _expectedDeliveryDate = DateTime.Today.AddDays(7); // Default expected turnaround
        [ObservableProperty] private DateTime? _actualDeliveryDate; // Populated upon GRN / Received status

        [ObservableProperty] private decimal _totalAmount;
        [ObservableProperty] private string _orderStatus = "Draft"; // Draft, Ordered, Received, Cancelled
        [ObservableProperty] private string _createdBy = "Admin";

        // Code-linked display property populated by the Service layer join query
        [ObservableProperty] private string _vendorName = string.Empty;

        // --- Dynamic Delay Helpers ---

        /// <summary>
        /// Calculates the delay in days. 
        /// Returns 0 if delivered on time or still within the expected window.
        /// </summary>
        public int DelayInDays
        {
            get
            {
                if (OrderStatus == "Received" && ActualDeliveryDate.HasValue && ExpectedDeliveryDate.HasValue)
                {
                    int days = (ActualDeliveryDate.Value.Date - ExpectedDeliveryDate.Value.Date).Days;
                    return days > 0 ? days : 0;
                }

                // If order is still open but past expected date, calculate ongoing delay
                if (OrderStatus == "Ordered" && ExpectedDeliveryDate.HasValue && DateTime.Today > ExpectedDeliveryDate.Value.Date)
                {
                    return (DateTime.Today - ExpectedDeliveryDate.Value.Date).Days;
                }

                return 0;
            }
        }

        public bool IsDelayed => DelayInDays > 0;
    }
}
