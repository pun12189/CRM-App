using Tijori.Data;
using Tijori.Models;
using Tijori.ViewModels;
using Dapper;
using DocumentFormat.OpenXml.Wordprocessing;
using MySql.Data.MySqlClient;
using MySqlX.XDevAPI;
using Org.BouncyCastle.Utilities.Collections;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Services
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
                string orderSql = @"INSERT INTO Orders (CustomerId, TotalAmount, PaymentStatus, AmountPaid, ProformaNumber) 
                            VALUES (@CustomerId, @GrandTotal, 'Proforma', 0, @ProformaNumber); 
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

                string insertHistory = @"INSERT INTO LeadHistory 
            (LeadId, Message, Content, UpdatedByContent, NextFollowUpDate, UpdatedBy, ActionType, FollowupStage, IsPriority) 
            VALUES (@LeadId, @Message, @Content, @UpdatedByContent, @NextFollowUpDate, @UpdatedBy, @ActionType, @FollowupStage, @IsPriority)";
                await db.ExecuteAsync(insertHistory, history, transaction);

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                return false;
            }
        }

        /// <summary>
        /// Inserts complete multi-table proforma records into the database within an isolated, atomic transaction.
        /// </summary>
        public async Task<bool> SaveCompleteProformaWorkflowAsync(ProformaHeader proforma, LeadHistoryEntry history)
        {
            using var conn = _context.CreateConnection();
            if (conn.State != ConnectionState.Open) conn.Open();

            using var transaction = conn.BeginTransaction();
            try
            {
                // 1. Insert parent proforma record entry row
                string insertHeaderSql = @"
                    INSERT INTO Proformas (ProformaNumber, LeadId, BillTo, DeliverTo, TermsAndConditions, PreferedTransport, InternalRemarks, NextFollowupDate, ItemSubTotal, ExtraChargesTotal, GrandTotal, BalanceDue, CreatedBy)
                    VALUES (@ProformaNumber, @LeadId, @BillTo, @DeliverTo, @TermsAndConditions, @PreferedTransport, @InternalRemarks, @NextFollowupDate, @ItemSubTotal, @ExtraChargesTotal, @GrandTotal, @BalanceDue, @CreatedBy);
                    SELECT LAST_INSERT_ID();";

                int proformaId = await conn.ExecuteScalarAsync<int>(insertHeaderSql, proforma, transaction);

                // 2. Map and insert child product items
                string insertItemSql = @"
                    INSERT INTO ProformaItems (ProformaId, ProductId, BatchNo, ProductName, Quantity, UnitPrice, GstPercent, SubTotal, IsCustom, ProductImageBlob)
                    VALUES (@ProformaId, @ProductId, @BatchNo, @ProductName, @Quantity, @UnitPrice, @GstPercent, @SubTotal, @IsCustom, @ProductImageBlob);";

                foreach (var item in proforma.Items)
                {
                    item.ProformaId = proformaId;
                    await conn.ExecuteAsync(insertItemSql, item, transaction);
                }

                // 3. Map and insert miscellaneous logistical extra charges
                string insertChargeSql = @"
                    INSERT INTO ProformaExtraCharges (ProformaId, ChargeDescription, ChargeAmount)
                    VALUES (@ProformaId, @ChargeDescription, @ChargeAmount);";

                foreach (var charge in proforma.ExtraCharges)
                {
                    charge.ProformaId = proformaId;
                    await conn.ExecuteAsync(insertChargeSql, charge, transaction);
                }

                string hist2Sql = @"INSERT INTO LeadHistory (LeadId, Message, ActionType, NextFollowUpDate, FollowupStage, UpdatedBy, LogDate) 
                            VALUES (@LeadId, @Message, @ActionType, @NextFollowUpDate, @FollowupStage, @UpdatedBy, NOW())";
                await conn.ExecuteAsync(hist2Sql, history, transaction);

                transaction.Commit();
                return true;
            }
            catch (Exception)
            {
                transaction.Rollback();
                return false;
            }
        }

        /// <summary>
        /// CONVERSION ENGINE: Automatically converts an existing Proforma Quote into live system production 
        /// Orders, OrderItems, OrderExtraCharges, and Payments profiles when a customer deposit arrives.
        /// </summary>
        public async Task<bool> ConvertProformaToFinalOrderAsync(int proformaId, decimal incomingDeposit, string paymentMode, string operatorUser)
        {
            using var conn = _context.CreateConnection();
            if (conn.State != ConnectionState.Open) conn.Open();

            using var transaction = conn.BeginTransaction();
            try
            {
                // 1. Fetch the existing historical proforma data details snapshot record row
                string fetchProformaSql = "SELECT * FROM Proformas WHERE ProformaId = @Id FOR UPDATE;";
                var proforma = await conn.QueryFirstOrDefaultAsync<ProformaHeader>(fetchProformaSql, new { Id = proformaId }, transaction);
                if (proforma == null) return false;

                // 2. Commit Header to primary Orders table
                string insertOrderSql = @"
                    INSERT INTO Orders (LeadId, TotalAmount, AmountPaid, BalanceAmount, Description, PaymentStatus, Status, ProcessedBy, OrderDate)
                    VALUES (@LeadId, @GrandTotal, @Deposit, (@GrandTotal - @Deposit), @Remarks, IF(@GrandTotal <= @Deposit, 'Paid', 'Partially Paid'), 'Pending', @Operator, NOW());
                    SELECT LAST_INSERT_ID();";

                int orderId = await conn.ExecuteScalarAsync<int>(insertOrderSql, new
                {
                    LeadId = proforma.LeadId,
                    GrandTotal = proforma.GrandTotal,
                    Deposit = incomingDeposit,
                    Remarks = $"Converted from Proforma Quote: {proforma.ProformaNumber}. Notes: {proforma.InternalRemarks}",
                    Operator = operatorUser
                }, transaction);

                // 3. Clone and transfer line items to OrderItems, tracking warehouse inventory adjustments
                string fetchItemsSql = "SELECT * FROM ProformaItems WHERE ProformaId = @Id;";
                var items = await conn.QueryAsync<ProformaLineItem>(fetchItemsSql, new { Id = proformaId }, transaction);

                string insertOrderItemSql = @"
                    INSERT INTO OrderItems (OrderId, ProductId, ProductName, Quantity, UnitPrice, GstPercent, SubTotal)
                    VALUES (@OrderId, @ProductId, @ProductName, @Quantity, @UnitPrice, @GstPercent, @SubTotal);";

                foreach (var item in items)
                {
                    await conn.ExecuteAsync(insertOrderItemSql, new
                    {
                        OrderId = orderId,
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        GstPercent = item.GstPercent,
                        SubTotal = item.SubTotal
                    }, transaction);

                    // Decrement stock only if it points to a verified, non-custom catalog master product line element
                    if (item.IsCustom == 0 && item.ProductId.HasValue)
                    {
                        string deductStockSql = "UPDATE Products SET CurrentStock = CurrentStock - @Qty WHERE ProductId = @ProductId;";
                        await conn.ExecuteAsync(deductStockSql, new { Qty = item.Quantity, ProductId = item.ProductId.Value }, transaction);
                    }
                }

                // 4. Clone and transfer auxiliary courier/freight records to OrderExtraCharges
                string fetchChargesSql = "SELECT * FROM ProformaExtraCharges WHERE ProformaId = @Id;";
                var charges = await conn.QueryAsync<ProformaExtraChargeItem>(fetchChargesSql, new { Id = proformaId }, transaction);

                string insertOrderChargeSql = @"
                    INSERT INTO OrderExtraCharges (OrderId, ChargeDescription, ChargeAmount)
                    VALUES (@OrderId, @Desc, @Amount);";

                foreach (var charge in charges)
                {
                    await conn.ExecuteAsync(insertOrderChargeSql, new { OrderId = orderId, Desc = charge.ChargeDescription, Amount = charge.ChargeAmount }, transaction);
                }

                // 5. Append transaction entry straight into your core Payments table
                string insertPaymentSql = @"
                    INSERT INTO Payments (LeadId, OrderId, TotalOrderValue, AmountReceived, BalanceAmount, UserId, Remarks, PaymentDate)
                    VALUES (@LeadId, @OrderId, @GrandTotal, @Deposit, @Balance, @UserId, @Remarks, NOW());";

                await conn.ExecuteAsync(insertPaymentSql, new
                {
                    LeadId = proforma.LeadId,
                    OrderId = orderId,
                    GrandTotal = proforma.GrandTotal,
                    Deposit = incomingDeposit,
                    Balance = proforma.GrandTotal - incomingDeposit,
                    UserId = 1,
                    Remarks = $"Initial deposit collected via {paymentMode} against {proforma.ProformaNumber}"
                }, transaction);

                // 6. Flip original proforma quotation flag context record state parameters to close out loops
                string completeProformaSql = "UPDATE Proformas SET TotalPaid = @Deposit, BalanceDue = (GrandTotal - @Deposit), ProformaStatus = 'ConvertedToOrder' WHERE ProformaId = @Id;";
                await conn.ExecuteAsync(completeProformaSql, new { Id = proformaId, Deposit = incomingDeposit }, transaction);

                transaction.Commit();
                return true;
            }
            catch (Exception)
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
                DivisionId, LeadId, OrderDate, TotalAmount, AmountPaid, LeadHolder, TotalCostAmount, GrandTotal, InvoiceNumber, 
                PaymentStatus, Status, Description, ProcessedBy, PreferedTransport, Remarks
            ) VALUES (
                @DivisionId, @LeadId, NOW(), @TotalAmount, @AmountPaid, @LeadHolder, @TotalCostAmount, @GrandTotal, @InvoiceNumber, 
                @PaymentStatus, @Status, @Description, @ProcessedBy, @Transport, @Remarks
            );
            SELECT LAST_INSERT_ID();";

                // Assuming your ViewModel or application configuration passes an active tracking DivisionId context
                // int activeDivisionId = vm.SelectedCustomer?.DivisionId ?? 1;
                int activeDivisionId = 1;

                int orderId = await conn.ExecuteScalarAsync<int>(orderSql, new
                {
                    DivisionId = activeDivisionId,
                    LeadId = vm.SelectedCustomer?.LeadId,
                    AmountPaid = Math.Round(vm.AmountReceived, 2),
                    TotalAmount = Math.Round(vm.OrderValue, 2),
                    TotalCostAmount = Math.Round(totalOrderCostFootprint, 2), // <-- SAVED VALUE
                    GrandTotal = Math.Round(vm.CalculatedGrandValue, 2),
                    InvoiceNumber = invoiceNo,
                    Description = $"Order for {vm.CartItems.Count} items",
                    ProcessedBy = vm.CurrentUser,
                    Transport = vm.PreferedTransport,
                    Status = "Pending",
                    LeadHolder = vm.SelectedCustomer?.LeadHolder,
                    PaymentStatus = (vm.CalculatedGrandValue - vm.AmountReceived <= 0) ? "Paid" : (vm.AmountReceived > 0 ? "Partially Paid" : "Pending"),
                    vm.Remarks
                }, transaction);

                string sql = @"INSERT INTO LeadHistory (LeadId, Message, Content, NextFollowUpDate, UpdatedBy, UpdatedByContent, LogDate, IsPriority) 
                       VALUES (@LeadId, @Message, @Content, @NextFollowUpDate, @UpdatedBy, @UpdatedByContent, @LogDate, @IsPriority)";
                await conn.ExecuteAsync(sql, new
                {
                    LeadId = vm.SelectedCustomer?.LeadId,
                    Message = "Order Created",
                    Content = $"Order Update \r\n Order ID: {orderId}\r\n Order Value: {vm.CalculatedGrandValue} \r\n Payment Received: {vm.AmountReceived}",
                    UpdatedBy = vm.CurrentUser,
                    NextFollowUpDate = vm.CombinedDateTime, // Example: set next follow-up date 7 days from now
                    UpdatedByContent = $" create an order and schedule next follow-up on {vm.CombinedDateTime:G}",
                    LogDate = DateTime.Now,
                    IsPriority = false
                }, transaction);

                string ohsql = @"
                INSERT INTO OrderHistory 
                (OrderId, LeadId, ActionTitle, Description, ActionType, NewState, TransactionAmount, PerformedBy, IsImportant)
                VALUES 
                (@OrderId, @LeadId, @ActionTitle, @Description, @ActionType, @NewState, @TransactionAmount, @PerformedBy, @IsImportant);";

                await conn.ExecuteAsync(ohsql, new
                {
                    OrderId = orderId,
                    LeadId = vm.SelectedCustomer?.LeadId,
                    ActionTitle = "Order Created",
                    Description = $"Order #{orderId} created for {vm.SelectedCustomer?.CustomerName ?? "Customer"} with total value ₹ {vm.CalculatedGrandValue:N2}.",
                    ActionType = "OrderCreated",
                    NewState = "Pending",
                    TransactionAmount = vm.CalculatedGrandValue,
                    PerformedBy = vm.CurrentUser ?? "Admin",
                    IsImportant = true
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
                INSERT INTO Payments (DivisionId, LeadId, OrderId, TotalOrderValue, AmountReceived, BalanceAmount, UserId, PaymentDate, PaymentMethod, Remarks)
                VALUES (@DivisionId, @LeadId, @OrderId, @Total, @Received, @Balance, @UserId, NOW(), @Method, @Remarks)";

                    await conn.ExecuteAsync(paymentSql, new
                    {
                        DivisionId = activeDivisionId,
                        LeadId = vm.SelectedCustomer?.LeadId,
                        OrderId = orderId,
                        Total = Math.Round(vm.CalculatedGrandValue, 2),
                        Received = Math.Round(vm.AmountReceived, 2),
                        Balance = Math.Round(vm.CalculatedGrandValue - vm.AmountReceived, 2),
                        UserId = vm.CurrentUserId,
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

        /// <summary>
        /// Retrieves all payment entries recorded against a specific Order ID.
        /// </summary>
        /// <param name="orderId">The unique identifier of the order.</param>
        /// <returns>A list of PaymentEntry objects ordered by date.</returns>
        public async Task<List<PaymentEntry>> GetPaymentsByOrderIdAsync(int orderId)
        {
            if (orderId <= 0) return new List<PaymentEntry>();

            const string query = @"
                SELECT * FROM Payments WHERE OrderId = @OrderId ORDER BY PaymentDate DESC;";

            using (IDbConnection db = _context.CreateConnection())
            {
                var payments = await db.QueryAsync<PaymentEntry>(query, new { OrderId = orderId });
                return payments.ToList();
            }
        }

        public async Task<bool> UpdateOrderStatusAsync(int orderId, string newStatus)
        {
            if (orderId <= 0 || string.IsNullOrWhiteSpace(newStatus)) return false;

            const string sql = @"
                UPDATE `Orders` 
                SET `Status` = @Status 
                WHERE `OrderId` = @OrderId;";

            using (IDbConnection db = _context.CreateConnection())
            {
                int rowsAffected = await db.ExecuteAsync(sql, new { Status = newStatus, OrderId = orderId });
                return rowsAffected > 0;
            }
        }

        /// <summary>
        /// Fetches the complete Order record along with its child Items and ExtraCharges.
        /// </summary>
        public async Task<Order?> GetOrderDetailsByIdAsync(int orderId)
        {
            if (orderId <= 0) return null;

            const string sql = @"
                -- Query 1: Main Order Record
                SELECT * FROM `Orders` WHERE OrderId = @OrderId;

                -- Query 2: Order Items
                SELECT oi.*,
                    p.Name AS ProductName,
                    pb.BatchNumber AS BatchNumber,
                    pb.ExpiryDate AS ExpiryDate
                    FROM `OrderItems` oi
                    LEFT JOIN `Products` p ON oi.ProductId = p.ProductId
                    LEFT JOIN `ProductBatches` pb ON oi.BatchId = pb.BatchId
                    WHERE oi.OrderId = @OrderId;

                -- Query 3: Extra Charges & Discounts
                SELECT 
                    ChargeName AS Name,
                    Amount AS Value,
                    GSTPercent AS GstPercent,
                    CASE 
                        WHEN IsDiscount = 1 THEN 'Subtract (-)' 
                        ELSE 'Add (+)' 
                    END AS Action
                FROM `OrderExtraCharges` 
                WHERE OrderId = @OrderId;";

            using (IDbConnection db = _context.CreateConnection())
            {
                using (var multi = await db.QueryMultipleAsync(sql, new { OrderId = orderId }))
                {
                    // Read Order
                    var order = await multi.ReadFirstOrDefaultAsync<Order>();
                    if (order == null) return null;

                    // Read Items
                    var items = await multi.ReadAsync<OrderItem>();
                    order.Items = new System.Collections.ObjectModel.ObservableCollection<OrderItem>(items);

                    // Read Extra Charges
                    var charges = await multi.ReadAsync<ExtraCharge>();
                    order.ExtraCharges = new System.Collections.ObjectModel.ObservableCollection<ExtraCharge>(charges);

                    return order;
                }
            }
        }
    }
}
