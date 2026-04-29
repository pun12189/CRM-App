using CallMan.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Interfaces
{
    public interface IDialogService
    {
        Task<bool?> ShowNewOrderDialog(int leadId);
        Task<bool?> ShowAddPaymentDialog(Order order);
        void ShowOrderWindow(Lead selectedLead);
        Task<bool?> ShowGlobalNewOrderDialog();
        Task<DashboardFilter?> ShowFilterDialog();
        Task<bool?> ShowAddStaffWindow(User? userToEdit);
        Task<string> ShowSingleInputDialog(string item, string? existingValue = null);
    }
}
