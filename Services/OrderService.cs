using CallMan.Data;
using CallMan.Models;
using CallMan.ViewModels;
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

        public async Task<bool> SaveCompleteOrderAsync(GlobalNewOrderViewModel vm)
        {
            using var conn = _context.CreateConnection();
            using var transaction = conn.BeginTransaction();

            try
            {
                string invoiceNo = await GenerateInvoiceNumberAsync();

                string updateLeadSql = @"
            UPDATE Leads 
            SET Status = 'Matured' 
            WHERE LeadId = @LeadId AND Status != 'Matured'";

                await conn.ExecuteAsync(updateLeadSql, new { LeadId = vm.SelectedCustomer?.LeadId }, transaction);

                // 1. Insert into Orders Table
                string orderSql = @"
            INSERT INTO Orders (
                LeadId, OrderDate, TotalAmount, GrandTotal, InvoiceNumber, 
                Status, Description, ProcessedBy, PreferedTransport, Remarks
            ) VALUES (
                @LeadId, NOW(), @TotalAmount, @GrandTotal, @InvoiceNumber, 
                @Status, @Description, @ProcessedBy, @Transport, @Remarks
            );
            SELECT LAST_INSERT_ID();";

                int orderId = await conn.ExecuteScalarAsync<int>(orderSql, new
                {
                    LeadId = vm.SelectedCustomer?.LeadId,
                    TotalAmount = vm.OrderValue,
                    GrandTotal = vm.CalculatedGrandValue,
                    InvoiceNumber = invoiceNo, // Helper method
                    Description = $"Order for {vm.CartItems.Count} items",
                    ProcessedBy = vm.CurrentUser, // Replace with actual logged-in User ID
                    Transport = vm.PreferedTransport,
                    Status = vm.CalculatedGrandValue - vm.AmountReceived == 0 ? "Fully Paid" : "Partially Paid",
                    vm.Remarks
                }, transaction);

                string sql = @"INSERT INTO LeadHistory (LeadId, Message, UpdatedBy, FollowupStage) 
                       VALUES (@LeadId, @Message, @UpdatedBy, @FollowupStage)";
                await conn.ExecuteAsync(sql, new {
                    LeadId = vm.SelectedCustomer?.LeadId,
                    Message = $"Order Update \r\n Order ID: {orderId}\r\n Order Value: {vm.CalculatedGrandValue} \r\n Payment Received: {vm.AmountReceived}",
                    UpdatedBy = vm.CurrentUser, // Replace with actual logged-in User ID
                    FollowupStage = "Matured" // Replace with actual follow-up stage if needed
                }, transaction);

                // 2. Insert Order Items & Update Product Stock
                foreach (var item in vm.CartItems)
                {
                    // Insert Line Item
                    string itemSql = @"
                INSERT INTO OrderItems (OrderId, ProductId, Quantity, UnitPrice, GSTPercent, SubTotal, GstAmount, Total)
                VALUES (@OrderId, @ProductId, @Qty, @Price, @Gst, @SubTotal, @GstAmount, @Total)";

                    await conn.ExecuteAsync(itemSql, new
                    {
                        OrderId = orderId,
                        ProductId = item.ProductId,
                        Qty = item.Quantity,
                        Price = item.UnitPrice,
                        Gst = item.GstPercent,
                        SubTotal = item.SubTotal,
                        GstAmount = item.GstAmount,
                        Total = item.Total
                    }, transaction);

                    // Deduct from Products Table
                    string stockSql = "UPDATE Products SET RemainingStock = RemainingStock - @Qty WHERE ProductId = @ProductId";
                    await conn.ExecuteAsync(stockSql, new { Qty = item.Quantity, item.ProductId }, transaction);
                }

                // 3. Insert Order Extra Charges (Carriage, etc.)
                if (vm.OtherCharges.Any())
                {
                    string chargeSql = @"
                INSERT INTO OrderExtraCharges (OrderId, ChargeName, Amount, GSTPercent, IsDiscount)
                VALUES (@OrderId, @Name, @Amount, @Gst, @IsDisc)";

                    foreach (var charge in vm.OtherCharges)
                    {
                        await conn.ExecuteAsync(chargeSql, new
                        {
                            OrderId = orderId,
                            Name = charge.Name,
                            Amount = charge.Value,
                            Gst = charge.GstPercent,
                            IsDisc = charge.Action == "Subtract (-)"
                        }, transaction);
                    }
                }

                // 4. Record Initial Payment (if applicable)
                if (vm.AmountReceived > 0)
                {
                    string paymentSql = @"
                INSERT INTO Payments (LeadId, OrderId, TotalOrderValue, AmountReceived, BalanceAmount, PaymentDate, PaymentMethod, Remarks)
                VALUES (@LeadId, @OrderId, @Total, @Received, @Balance, NOW(), @Method, @Remarks)";

                    await conn.ExecuteAsync(paymentSql, new
                    {
                        LeadId = vm.SelectedCustomer?.LeadId,
                        OrderId = orderId,
                        Total = vm.CalculatedGrandValue,
                        Received = vm.AmountReceived,
                        Balance = vm.CalculatedGrandValue - vm.AmountReceived,
                        Method = vm.PaymentMode,
                        Remarks = vm.Remarks
                    }, transaction);
                }

                transaction.Commit();
                return true;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                // Log the exception (ex) here as needed
                return false;
            }
        }

        public async Task<string> GenerateInvoiceNumberAsync()
        {
            using var conn = _context.CreateConnection();

            // 1. Get current financial year (e.g., Apr 2026 to Mar 2027)
            DateTime today = DateTime.Today;
            string currentFY = today.Month >= 4
                ? $"{today.ToString("yy")}-{today.AddYears(1).ToString("yy")}"
                : $"{today.AddYears(-1).ToString("yy")}-{today.ToString("yy")}";

            // 2. Atomic update: Increment and fetch in one go to prevent duplicates
            string sql = @"
        UPDATE InvoiceSettings 
        SET LastNumber = LastNumber + 1 
        WHERE FinancialYear = @FY;
        
        SELECT Prefix, LastNumber, FinancialYear 
        FROM InvoiceSettings 
        WHERE FinancialYear = @FY;";

            var settings = await conn.QueryFirstOrDefaultAsync<dynamic>(sql, new { FY = currentFY });

            if (settings == null)
            {
                // Handle New Financial Year: Reset counter
                await conn.ExecuteAsync("INSERT INTO InvoiceSettings (Prefix, LastNumber, FinancialYear) VALUES ('SO', 1, @FY)", new { FY = currentFY });
                return $"SO/{currentFY}/0001";
            }

            // 3. Format to SO/26-27/0001
            return $"{settings.Prefix}/{settings.FinancialYear}/{((int)settings.LastNumber).ToString("D4")}";
        }
    }
}
