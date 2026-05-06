using CallMan.Data;
using CallMan.Models;
using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Services
{
    public class OrderService
    {
        private readonly CrmDbContext _context;
        public OrderService(CrmDbContext context) => _context = context;

        public async Task<bool> SaveProformaAsync(Order order, LeadHistoryEntry history)
        {
            using var db = _context.CreateConnection();
            db.Open();
            using var transaction = db.BeginTransaction();

            try
            {
                // 1. Save the main Proforma record
                string orderSql = @"INSERT INTO Orders (CustomerId, TotalAmount, Status, ProformaNumber) 
                            VALUES (@CustomerId, @GrandTotal, 'Proforma', @ProformaNumber); 
                            SELECT LAST_INSERT_ID();";

                int orderId = await db.QuerySingleAsync<int>(orderSql,
                    new { order.LeadId, order.GrandTotal, order.ProformaNumber }, transaction);

                // 2. Save the line items
                foreach (var item in order.Items)
                {
                    string itemSql = @"INSERT INTO OrderItems (OrderId, ProductId, Quantity, UnitPrice, GSTPercent, SubTotal) 
                               VALUES (@orderId, @ProductId, @Quantity, @UnitPrice, @GSTPercent, @SubTotal)";

                    await db.ExecuteAsync(itemSql,
                        new { orderId, item.ProductId, item.Quantity, item.UnitPrice, item.GstPercent, item.SubTotal },
                        transaction);
                }

                string hist2Sql = @"INSERT INTO LeadHistory (LeadId, Message, ActionType, NextFollowUpDate, FollowupStage, UpdatedBy, LogDate) 
                            VALUES (@LeadId, @Message, @ActionType, @NextFollowUpDate, @FollowupStage, @UpdatedBy, NOW())";
                await db.ExecuteAsync(hist2Sql, history, transaction);

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                return false;
            }
        }
    }
}
