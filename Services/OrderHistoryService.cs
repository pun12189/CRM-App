using CallMan.Data;
using CallMan.Interfaces;
using CallMan.Models;
using Dapper;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Services
{
    public class OrderHistoryService : IOrderHistoryService
    {
        private readonly CrmDbContext _context;

        public OrderHistoryService(CrmDbContext context)
        {
            _context = context;
        }

        public async Task LogActivityAsync(OrderHistoryEntry entry)
        {
            if (entry == null || entry.OrderId <= 0) return;

            const string sql = @"
                INSERT INTO OrderHistory 
                (OrderId, LeadId, ActionTitle, Description, ActionType, PreviousState, NewState, TransactionAmount, LogDate, PerformedBy, IsImportant)
                VALUES 
                (@OrderId, @LeadId, @ActionTitle, @Description, @ActionType, @PreviousState, @NewState, @TransactionAmount, @LogDate, @PerformedBy, @IsImportant);";

            using (IDbConnection db = _context.CreateConnection())
            {
                await db.ExecuteAsync(sql, entry);
            }
        }

        public async Task<List<OrderHistoryEntry>> GetHistoryByOrderIdAsync(int orderId)
        {
            if (orderId <= 0) return new List<OrderHistoryEntry>();

            const string sql = @"
                SELECT 
                    HistoryId, OrderId, LeadId, ActionTitle, Description, ActionType,
                    PreviousState, NewState, TransactionAmount, LogDate, PerformedBy, IsImportant
                FROM OrderHistory
                WHERE OrderId = @OrderId
                ORDER BY LogDate DESC, HistoryId DESC;";

            using (IDbConnection db = _context.CreateConnection())
            {
                var logs = await db.QueryAsync<OrderHistoryEntry>(sql, new { OrderId = orderId });
                return logs.ToList();
            }
        }
    }
}
