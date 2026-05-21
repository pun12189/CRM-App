using CallMan.Data;
using CallMan.Models;
using CallMan.ViewModels;
using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
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
            if (conn.State == ConnectionState.Closed) await ((System.Data.Common.DbConnection)conn).OpenAsync();

            using var transaction = conn.BeginTransaction();

            try
            {
                string invoiceNo = await GenerateInvoiceNumberAsync();

                string updateLeadSql = @"
            UPDATE Leads 
            SET Status = 'Matured' 
            WHERE LeadId = @LeadId AND Status != 'Matured'";

                await conn.ExecuteAsync(updateLeadSql, new { LeadId = vm.SelectedCustomer?.LeadId }, transaction);

                decimal totalOrderCostFootprint = 0;
                foreach (var item in vm.CartItems)
                {
                    var masterProduct = vm.AllMasterProducts.FirstOrDefault(p => p.ProductId == item.ProductId);
                    if (masterProduct != null)
                    {
                        // Multiply this item's quantity by its current static WAC cost price baseline
                        totalOrderCostFootprint += (item.Quantity * masterProduct.CostPrice);
                    }
                }

                // 1. Insert into Orders Table (Include DivisionId multi-tenant scoping context)
                string orderSql = @"
            INSERT INTO Orders (
                DivisionId, LeadId, OrderDate, TotalAmount, TotalCostAmount, GrandTotal, InvoiceNumber, 
                Status, Description, ProcessedBy, PreferedTransport, Remarks
            ) VALUES (
                @DivisionId, @LeadId, NOW(), @TotalAmount, @TotalCostAmount, @GrandTotal, @InvoiceNumber, 
                @Status, @Description, @ProcessedBy, @Transport, @Remarks
            );
            SELECT LAST_INSERT_ID();";

                // Assuming your ViewModel or application configuration passes an active tracking DivisionId context
                // int activeDivisionId = vm.SelectedCustomer?.DivisionId ?? 1;
                int activeDivisionId = 1;

                int orderId = await conn.ExecuteScalarAsync<int>(orderSql, new
                {
                    DivisionId = activeDivisionId,
                    LeadId = vm.SelectedCustomer?.LeadId,
                    TotalAmount = Math.Round(vm.OrderValue, 2),
                    TotalCostAmount = Math.Round(totalOrderCostFootprint, 2), // <-- SAVED VALUE
                    GrandTotal = Math.Round(vm.CalculatedGrandValue, 2),
                    InvoiceNumber = invoiceNo,
                    Description = $"Order for {vm.CartItems.Count} items",
                    ProcessedBy = vm.CurrentUser,
                    Transport = vm.PreferedTransport,
                    Status = (vm.CalculatedGrandValue - vm.AmountReceived <= 0) ? "Fully Paid" : (vm.AmountReceived > 0 ? "Partially Paid" : "Pending"),
                    vm.Remarks
                }, transaction);

                string sql = @"INSERT INTO LeadHistory (LeadId, Message, UpdatedBy, FollowupStage) 
                       VALUES (@LeadId, @Message, @UpdatedBy, @FollowupStage)";
                await conn.ExecuteAsync(sql, new
                {
                    LeadId = vm.SelectedCustomer?.LeadId,
                    Message = $"Order Update \r\n Order ID: {orderId}\r\n Order Value: {vm.CalculatedGrandValue} \r\n Payment Received: {vm.AmountReceived}",
                    UpdatedBy = vm.CurrentUser,
                    FollowupStage = "Matured"
                }, transaction);

                // 2. Insert Order Items (with BatchId context) & Update Batch Stocks
                foreach (var item in vm.CartItems)
                {
                    // FIXED: Added BatchId target column reference to line item storage footprint
                    var masterProduct = vm.AllMasterProducts.FirstOrDefault(p => p.ProductId == item.ProductId);
                    decimal currentWac = masterProduct?.CostPrice ?? 0;

                    string itemSql = @"
                    INSERT INTO OrderItems (OrderId, ProductId, BatchId, Quantity, UnitPrice, CostPrice, GSTPercent, SubTotal, GstAmount, Total)
                    VALUES (@OrderId, @ProductId, @BatchId, @Qty, @Price, @CostPrice, @Gst, @SubTotal, @GstAmount, @Total)";

                    await conn.ExecuteAsync(itemSql, new
                    {
                        OrderId = orderId,
                        ProductId = item.ProductId,
                        BatchId = item.BatchId, // Assumes item model now populates this choice
                        Qty = item.Quantity,
                        Price = Math.Round(item.UnitPrice, 2),
                        CostPrice = Math.Round(currentWac, 2),
                        Gst = item.GstPercent,
                        SubTotal = Math.Round(item.SubTotal, 2),
                        GstAmount = Math.Round(item.GstAmount, 2),
                        Total = Math.Round(item.Total, 2)
                    }, transaction);

                    // FIXED STEP: Deduct stock from specific Lot/Batch instead of generic global product row
                    string batchStockSql = @"
                UPDATE ProductBatches 
                SET CurrentStock = CurrentStock - @Qty 
                WHERE BatchId = @BatchId AND ProductId = @ProductId AND DivisionId = @DivisionId;";

                    await conn.ExecuteAsync(batchStockSql, new
                    {
                        Qty = item.Quantity,
                        item.BatchId,
                        item.ProductId,
                        DivisionId = activeDivisionId
                    }, transaction);

                    // COMPACTION SYNC ENGINE: Instantly push newly balanced batch stocks and recalculate product WAC Cost Price
                    const string productSyncSql = @"
                UPDATE Products p 
                SET p.SellingPrice = @sp,
                    p.RemainingStock = IFNULL((SELECT SUM(CurrentStock) FROM ProductBatches WHERE ProductId = p.ProductId AND CurrentStock > 0), 0),
                    p.CostPrice = IFNULL(
                        (SELECT ROUND(SUM(CurrentStock * MinimumSellingPrice) / SUM(CurrentStock), 2) 
                         FROM ProductBatches 
                         WHERE ProductId = p.ProductId AND CurrentStock > 0), 
                        p.CostPrice
                    )
                WHERE p.ProductId = @ProductId AND p.DivisionId = @DivisionId;";

                    await conn.ExecuteAsync(productSyncSql, new { sp = vm.Rate, item.ProductId, DivisionId = activeDivisionId }, transaction);
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
                            Amount = Math.Round(charge.Value, 2),
                            Gst = charge.GstPercent,
                            IsDisc = charge.Action == "Subtract (-)"
                        }, transaction);
                    }
                }

                // 4. Record Initial Payment (with added DivisionId validation mapping alignment)
                if (vm.AmountReceived > 0)
                {
                    string paymentSql = @"
                INSERT INTO Payments (DivisionId, LeadId, OrderId, TotalOrderValue, AmountReceived, PaymentDate, PaymentMethod, Remarks)
                VALUES (@DivisionId, @LeadId, @OrderId, @Total, @Received, NOW(), @Method, @Remarks)";

                    await conn.ExecuteAsync(paymentSql, new
                    {
                        DivisionId = activeDivisionId,
                        LeadId = vm.SelectedCustomer?.LeadId,
                        OrderId = orderId,
                        Total = Math.Round(vm.CalculatedGrandValue, 2),
                        Received = Math.Round(vm.AmountReceived, 2),
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
                // Hook your centralized Sentry monitoring reporting framework here 
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
