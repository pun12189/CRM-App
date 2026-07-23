using CallMan.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Interfaces
{
    public interface IOrderHistoryService
    {
        Task LogActivityAsync(OrderHistoryEntry entry);
        Task<List<OrderHistoryEntry>> GetHistoryByOrderIdAsync(int orderId);
    }
}
