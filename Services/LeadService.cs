using CallMan.Data;
using CallMan.Models;
using CallMan.Models.Enums;
using Dapper;
using Mysqlx.Crud;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CallMan.Services
{
    public class LeadService
    {
        private readonly CrmDbContext _context;
        public LeadService(CrmDbContext context) => _context = context;

        // Fetch all leads for the DataGrid
        public async Task<IEnumerable<Lead>> GetAllLeadsAsync()
        {
            using var db = _context.CreateConnection();
            string sql = "SELECT * FROM Leads ORDER BY CreatedAt DESC";

            var leads = await db.QueryAsync<Lead>(sql);

            // Deserialize JSON metadata back into the Dictionary for each lead
            foreach (var lead in leads)
            {
                if (!string.IsNullOrEmpty(lead.MetadataJson))
                {
                    lead.CustomFields = JsonSerializer.Deserialize<Dictionary<string, string>>(lead.MetadataJson)
                                       ?? new Dictionary<string, string>();
                }

                if (!string.IsNullOrEmpty(lead.LabelsJson))
                {
                    lead.LeadLabels = JsonSerializer.Deserialize<ObservableCollection<string>>(lead.LabelsJson)
                                       ?? new ObservableCollection<string>();
                }
            }

            return leads;
        }

        public async Task<int> SaveLeadAsync(Lead lead, string initialLog, string user)
        {
            using var db = _context.CreateConnection();
            // Serialize dynamic fields to JSON
            lead.MetadataJson = JsonSerializer.Serialize(lead.CustomFields);
            lead.LabelsJson = JsonSerializer.Serialize(lead.LeadLabels);

            string sql = @"INSERT INTO Leads (CustomerName, Email, Phone, Status, MetadataJson, 
               CompanyName, AddressLine, City, District, State, Pincode, Country, CreatedAt, LeadHolder, WorkingArea, LeadSource, LeadTag, LabelsJson) 
               VALUES (@CustomerName, @Email, @Phone, @Status, @MetadataJson, 
               @CompanyName, @AddressLine, @City, @District, @State, @Pincode, @Country, NOW(), @LeadHolder, @WorkingArea, @LeadSource, @LeadTag, @LabelsJson);
            SELECT LAST_INSERT_ID();";

            int newId = await db.ExecuteScalarAsync<int>(sql, lead);

            // Save initial history entry
            await AddHistoryAsync(newId, initialLog, null, user);
            return newId;
        }

        public async Task AddHistoryAsync(int leadId, string message, DateTime? nextDate, string user)
        {
            using var db = _context.CreateConnection();
            string sql = @"INSERT INTO LeadHistory (LeadId, Message, NextFollowUpDate, UpdatedBy) 
                       VALUES (@leadId, @message, @nextDate, @user)";
            await db.ExecuteAsync(sql, new { leadId, message, nextDate, user });
        }

        // Update an existing lead
        public async Task<bool> UpdateLeadAsync(Lead lead)
        {
            using var db = _context.CreateConnection();

            // Sync Dictionary to JSON before saving
            lead.MetadataJson = JsonSerializer.Serialize(lead.CustomFields);
            lead.LabelsJson = JsonSerializer.Serialize(lead.LeadLabels);

            string sql = @"UPDATE Leads SET 
                    CustomerName = @CustomerName, Email = @Email, Phone = @Phone, 
                    Status = @Status, CompanyName = @CompanyName, AddressLine = @AddressLine, 
                    City = @City, District = @District, State = @State, 
                    Pincode = @Pincode, MetadataJson = @MetadataJson, LeadHolder = @LeadHolder, WorkingArea = @WorkingArea, LeadSource = @LeadSource, LeadTag = @LeadTag, LabelsJson = @LabelsJson WHERE LeadId = @LeadId";

            var rows = await db.ExecuteAsync(sql, lead);
            return rows > 0;
        }

        // Delete a lead
        public async Task<bool> DeleteLeadAsync(int leadId)
        {
            using var db = _context.CreateConnection();
            // Note: LeadHistory has a Foreign Key with ON DELETE CASCADE in our SQL schema
            string sql = "DELETE FROM Leads WHERE LeadId = @leadId";
            var rows = await db.ExecuteAsync(sql, new { leadId });
            return rows > 0;
        }

        public async Task<IEnumerable<LeadHistoryEntry>> GetHistoryByLeadIdAsync(int leadId)
        {
            using var db = _context.CreateConnection();
            string sql = "SELECT * FROM LeadHistory WHERE LeadId = @leadId ORDER BY LogDate DESC";
            return await db.QueryAsync<LeadHistoryEntry>(sql, new { leadId });
        }        

        public async Task<IEnumerable<Lead>> GetAllLeadsWithLatestUpdateAsync()
        {
            using var db = _context.CreateConnection();

            // Complex query: Select Lead info, and join with ONLY the newest History entry
            string sql = @"
        SELECT 
            l.*, 
            (SELECT COUNT(*) FROM LeadHistory WHERE LeadId = l.LeadId) - 1 as HistoryCount,
h.HistoryId, h.LogDate, h.Message, h.NextFollowUpDate, h.UpdatedBy
        FROM Leads l
        LEFT JOIN LeadHistory h ON l.LeadId = h.LeadId
        WHERE h.HistoryId = (
            SELECT MAX(HistoryId) 
            FROM LeadHistory 
            WHERE LeadId = l.LeadId
        ) OR h.HistoryId IS NULL -- In case lead has no history yet
        ORDER BY l.LeadId DESC;";

            // Use Dapper to map both objects (Lead and History)
            var result = await db.QueryAsync<Lead, LeadHistoryEntry, Lead>(sql,
                (lead, history) =>
                {
                    // Map the dynamic metadata as before
                    if (!string.IsNullOrEmpty(lead.MetadataJson))
                    {
                        lead.CustomFields = JsonSerializer.Deserialize<Dictionary<string, string>>(lead.MetadataJson)
                                           ?? new Dictionary<string, string>();
                    }

                    if (!string.IsNullOrEmpty(lead.LabelsJson))
                    {
                        lead.LeadLabels = JsonSerializer.Deserialize<ObservableCollection<string>>(lead.LabelsJson)
                                           ?? new ObservableCollection<string>();
                    }

                    // Assign the latest history entry to the calculated property
                    lead.LatestUpdate = history;
                    return lead;
                },
                splitOn: "HistoryId"); // Dapper splits the row mapping here

            return result;
        }

        public async Task UpdateLeadFullAsync(Lead lead, LeadHistoryEntry history)
        {
            using var db = _context.CreateConnection();
            using var trans = db.BeginTransaction();

            try
            {
                // 1. Update main lead status
                string updateLead = "UPDATE Leads SET Status = @Status WHERE LeadId = @LeadId";
                await db.ExecuteAsync(updateLead, lead, trans);

                // 2. Insert into History
                string insertHistory = @"INSERT INTO LeadHistory 
            (LeadId, Message, NextFollowUpDate, ActionType, FollowupStage, UpdatedBy) 
            VALUES (@LeadId, @Message, @NextFollowUpDate, @ActionType, @FollowupStage, @UpdatedBy)";
                await db.ExecuteAsync(insertHistory, history, trans);

                trans.Commit();
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        }

        public async Task UpdateMaturedLeadWithFollowupAsync(Lead lead, PaymentEntry payment, LeadHistoryEntry history)
        {
            using var db = _context.CreateConnection();
            using var trans = db.BeginTransaction();

            try
            {
                // Update main Lead status
                await db.ExecuteAsync("UPDATE Leads SET Status = @Status WHERE LeadId = @LeadId", lead, trans);

                // Insert financial record
                string paySql = @"INSERT INTO Payments (LeadId, TotalOrderValue, AmountReceived, Remarks, PaymentDate) 
                          VALUES (@LeadId, @TotalOrderValue, @AmountReceived, @Remarks, NOW())";
                await db.ExecuteAsync(paySql, payment, trans);

                // Insert history with follow-up scheduling
                string histSql = @"INSERT INTO LeadHistory (LeadId, Message, ActionType, NextFollowUpDate, FollowupStage, UpdatedBy) 
                           VALUES (@LeadId, @Message, @ActionType, @NextFollowUpDate, @FollowupStage, @UpdatedBy)";
                await db.ExecuteAsync(histSql, history, trans);

                trans.Commit();
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        }

        public async Task<bool> MatureWithOrderAndPaymentAsync(Lead lead, Models.Order order, PaymentEntry payment, LeadHistoryEntry history)
        {
            using var db = _context.CreateConnection();
            using var trans = db.BeginTransaction();

            try
            {
                // 1. Update Lead Status
                //await db.ExecuteAsync("UPDATE Leads SET Status = 'Matured' WHERE LeadId = @LeadId", new { lead.LeadId }, trans);

                // 2. Create the Order
                string orderSql = @"INSERT INTO Orders (LeadId, TotalAmount, Description, Status, ProcessedBy) 
                            VALUES (@LeadId, @TotalAmount, @Description, @Status, @ProcessedBy);
                            SELECT LAST_INSERT_ID();";
                int newOrderId = await db.QuerySingleAsync<int>(orderSql, order, trans);

                // 3. Record First Payment linked to that Order
                payment.OrderId = newOrderId;
                string paySql = @"INSERT INTO Payments (LeadId, OrderId, TotalOrderValue, AmountReceived, Remarks) 
                          VALUES (@LeadId, @OrderId, @TotalOrderValue, @AmountReceived, @Remarks)";
                await db.ExecuteAsync(paySql, payment, trans);

                // 4. Add History Milestone
                string hist2Sql = @"INSERT INTO LeadHistory (LeadId, Message, ActionType, NextFollowUpDate, FollowupStage, UpdatedBy, LogDate) 
                            VALUES (@LeadId, @Message, @ActionType, @NextFollowUpDate, @FollowupStage, @UpdatedBy, NOW())";
                await db.ExecuteAsync(hist2Sql, history, trans);

                trans.Commit();
                return true;
            }
            catch { trans.Rollback(); throw; }
        }

        public async Task<bool> MatureLeadWithDoubleHistoryAsync(Lead lead, Models.Order order, PaymentEntry payment, LeadHistoryEntry followUp)
        {
            using var db = _context.CreateConnection();
            using var trans = db.BeginTransaction();

            try
            {
                // 1. Update Lead Status
                await db.ExecuteAsync("UPDATE Leads SET Status = 'Matured' WHERE LeadId = @LeadId", new { lead.LeadId }, trans);

                // 2. Create the Order
                string orderSql = @"INSERT INTO Orders (LeadId, TotalAmount, Description, Status, ProcessedBy) 
                            VALUES (@LeadId, @TotalAmount, @Description, @Status, @ProcessedBy);
                            SELECT LAST_INSERT_ID();";
                int newOrderId = await db.QuerySingleAsync<int>(orderSql, order, trans);

                // 3. Record First Payment linked to that Order
                payment.OrderId = newOrderId;
                string paySql = @"INSERT INTO Payments (LeadId, OrderId, TotalOrderValue, AmountReceived, Remarks) 
                          VALUES (@LeadId, @OrderId, @TotalOrderValue, @AmountReceived, @Remarks)";
                await db.ExecuteAsync(paySql, payment, trans);

                // 3. ENTRY #1: The Maturity Milestone (System Entry)
                string milestoneMsg = $"[MILESTONE] Lead Matured. Order Value: ₹{payment.TotalOrderValue}, Received: ₹{payment.AmountReceived}.";
                string hist1Sql = @"INSERT INTO LeadHistory (LeadId, Message, ActionType, FollowupStage, UpdatedBy, LogDate) 
                            VALUES (@LeadId, @Message, 'Call', 'Matured', @UpdatedBy, NOW())";
                await db.ExecuteAsync(hist1Sql, new { lead.LeadId, Message = milestoneMsg, UpdatedBy = followUp.UpdatedBy }, trans);

                // 4. ENTRY #2: The User's Follow-up/Message
                string hist2Sql = @"INSERT INTO LeadHistory (LeadId, Message, ActionType, NextFollowUpDate, FollowupStage, UpdatedBy, LogDate) 
                            VALUES (@LeadId, @Message, @ActionType, @NextFollowUpDate, 'Matured', @UpdatedBy, DATE_ADD(NOW(), INTERVAL 1 SECOND))";
                await db.ExecuteAsync(hist2Sql, followUp, trans);

                trans.Commit();
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                trans.Rollback();
                return false;
            }
        }

        // Get all Matured Leads with calculated totals
        public async Task<IEnumerable<Lead>> GetMaturedLedgerAsync()
        {
            using var db = _context.CreateConnection();
            string sql = @"SELECT l.*, 
            (SELECT COALESCE(SUM(TotalAmount), 0) FROM Orders WHERE LeadId = l.LeadId) as TotalOrderAmount,
            (SELECT COALESCE(SUM(AmountReceived), 0) FROM Payments WHERE LeadId = l.LeadId) as TotalPaidAmount,
(SELECT COUNT(*) FROM LeadHistory WHERE LeadId = l.LeadId) - 1 as HistoryCount,
h.HistoryId, h.LogDate, h.Message, h.NextFollowUpDate, h.UpdatedBy
            FROM Leads l  LEFT JOIN LeadHistory h ON l.LeadId = h.LeadId
        WHERE l.Status = 'Matured' AND h.HistoryId = (
            SELECT MAX(HistoryId) 
            FROM LeadHistory 
            WHERE LeadId = l.LeadId
        ) OR h.HistoryId IS NULL -- In case lead has no history yet
        ORDER BY l.LeadId DESC;";
            var result = await db.QueryAsync<Lead, LeadHistoryEntry, Lead>(sql,
                (lead, history) =>
                {
                    // Map the dynamic metadata as before
                    if (!string.IsNullOrEmpty(lead.MetadataJson))
                    {
                        lead.CustomFields = JsonSerializer.Deserialize<Dictionary<string, string>>(lead.MetadataJson)
                                           ?? new Dictionary<string, string>();
                    }

                    if (!string.IsNullOrEmpty(lead.LabelsJson))
                {
                    lead.LeadLabels = JsonSerializer.Deserialize<ObservableCollection<string>>(lead.LabelsJson)
                                       ?? new ObservableCollection<string>();
                }

                    // Assign the latest history entry to the calculated property
                    lead.LatestUpdate = history;
                    return lead;
                },
                splitOn: "HistoryId");

            return result;
        }

        // Record a payment and auto-update Order status
        public async Task RecordPaymentAsync(PaymentEntry p)
        {
            using var db = _context.CreateConnection();
            using var trans = db.BeginTransaction();
            try
            {
                await db.ExecuteAsync("INSERT INTO Payments (OrderId, LeadId, AmountReceived, PaymentMethod, Remarks) VALUES (@OrderId, @LeadId, @AmountReceived, @PaymentMethod, @Remarks)", p, trans);
                string updateOrder = "UPDATE Orders o SET Status = IF((SELECT SUM(AmountReceived) FROM Payments WHERE OrderId = o.OrderId) >= o.TotalAmount, 'Fully Paid', 'Partially Paid') WHERE OrderId = @OrderId";
                await db.ExecuteAsync(updateOrder, new { p.OrderId }, trans);
                trans.Commit();
            }
            catch { trans.Rollback(); throw; }
        }

        // Fetch all orders for a specific customer
        public async Task<IEnumerable<Models.Order>> GetOrdersByLeadIdAsync(int leadId)
        {
            using var db = _context.CreateConnection();
            string sql = "SELECT * FROM Orders WHERE LeadId = @leadId ORDER BY OrderDate DESC";
            return await db.QueryAsync<Models.Order>(sql, new { leadId });
        }

        // Create a new order (often done during the Maturity step or later)
        public async Task CreateOrderAsync(Models.Order order)
        {
            using var db = _context.CreateConnection();
            string sql = "INSERT INTO Orders (LeadId, TotalAmount, Description, Status) VALUES (@LeadId, @TotalAmount, @Description, 'Partially Paid')";
            await db.ExecuteAsync(sql, order);
        }

        public async Task<IEnumerable<Models.Order>> GetAllOrdersWithCustomerNamesAsync()
        {
            using var db = _context.CreateConnection();
            // Join Orders with Leads to get the CustomerName for each order
            string sql = @"
        SELECT o.*, l.CustomerName 
        FROM Orders o
        INNER JOIN Leads l ON o.LeadId = l.LeadId
        ORDER BY o.OrderDate DESC";

            return await db.QueryAsync<Models.Order>(sql);
        }

        public async Task<Lead> GetLeadByIdAsync(int leadId)
        {
            using var db = _context.CreateConnection();
            string sql = "SELECT * FROM Leads WHERE LeadId = @leadId";
            return await db.QuerySingleOrDefaultAsync<Lead>(sql, new { leadId });
        }

        public async Task<DashboardStats> GetDashboardStatsAsync()
        {
            using var db = _context.CreateConnection();

            // Using a single complex query to get all counts for performance
            string sql = @"
        SELECT 
            (SELECT COUNT(*) FROM Leads) as AllLeads,
            (SELECT COUNT(*) FROM Leads WHERE Status = 'New') as NewLeads,
            (SELECT COUNT(*) FROM Leads WHERE Status = 'Untouched') as Untouched,
            (SELECT COUNT(*) FROM Leads WHERE Status = 'Dead') as Dead,
            (SELECT COUNT(*) FROM Leads WHERE Status = 'Matured') as Customers,
            (SELECT COALESCE(SUM(TotalAmount), 0) FROM Orders) as TotalBusiness";

            return await db.QuerySingleAsync<DashboardStats>(sql);
        }

        public async Task<IEnumerable<PaymentReminder>> GetPaymentRemindersAsync()
        {
            using var db = _context.CreateConnection();

            // We calculate pending balance per customer
            string sql = @"
        SELECT l.CustomerName, l.Phone,
               ((SELECT COALESCE(SUM(TotalAmount), 0) FROM Orders WHERE LeadId = l.LeadId) - 
                (SELECT COALESCE(SUM(AmountReceived), 0) FROM Payments WHERE LeadId = l.LeadId)) as PendingBalance
        FROM Leads l
        WHERE l.Status = 'Matured'
        HAVING PendingBalance > 0
        ORDER BY PendingBalance DESC";

            return await db.QueryAsync<PaymentReminder>(sql);
        }

        public async Task<IEnumerable<string>> GetUniqueLeadHoldersAsync()
        {
            using var db = _context.CreateConnection();
            // Fetch unique names of staff assigned to leads
            string sql = "SELECT DISTINCT FullName FROM Users WHERE Email IS NOT NULL AND Email != '' ORDER BY FullName";
            return await db.QueryAsync<string>(sql);
        }

        public async Task<DashboardStats> GetDashboardStatsFilteredAsync(DashboardFilter filter)
        {
            using var db = _context.CreateConnection();

            // Base WHERE clause logic
            string whereClause = " WHERE 1=1 ";
            var parameters = new DynamicParameters();

            if (!string.IsNullOrEmpty(filter.LeadHolder))
            {
                whereClause += " AND LeadHolder = @LeadHolder ";
                parameters.Add("LeadHolder", filter.LeadHolder);
            }

            if (filter.FromDate != null && filter.ToDate != null)
            {
                // Filtering based on Lead Creation or Update date
                whereClause += " AND CreatedAt BETWEEN @FromDate AND @ToDate ";
                parameters.Add("FromDate", filter.FromDate);
                parameters.Add("ToDate", filter.ToDate);
            }

            string sql = $@"
        SELECT 
            (SELECT COUNT(*) FROM Leads {whereClause}) as AllLeads,
            (SELECT COUNT(*) FROM Leads {whereClause} AND Status = 'New') as NewLeads,
            (SELECT COUNT(*) FROM Leads {whereClause} AND Status = 'Matured') as Customers,
            (SELECT COALESCE(SUM(o.TotalAmount), 0) FROM Orders o 
                    INNER JOIN Leads l ON o.LeadId = l.LeadId 
                    {whereClause.Replace("CreatedAt", "o.OrderDate")}) as TotalBusiness";

            return await db.QuerySingleAsync<DashboardStats>(sql, parameters);
        }       

        // --- USER MANAGEMENT METHODS ---

        // 1. Get all users including their Senior's name for the DataGrid
        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            using var db = _context.CreateConnection();
            string sql = @"
            SELECT u.*, s.FullName as SeniorName 
            FROM Users u
            LEFT JOIN Users s ON u.SeniorId = s.UserId
            ORDER BY u.Role, u.FullName";

            return await db.QueryAsync<User>(sql);
        }

        // 2. Create User (Email-based)
        public async Task<int> CreateUserAsync(User user)
        {
            using var db = _context.CreateConnection();
            string sql = @"
            INSERT INTO Users (Email, Password, FullName, Phone, Role, SeniorId, MonthlyTarget, IsActive)
            VALUES (@Email, @Password, @FullName, @Phone, @Role, @SeniorId, @MonthlyTarget, @IsActive);
            SELECT LAST_INSERT_ID();";

            return await db.QuerySingleAsync<int>(sql, user);
        }

        // 3. Update User
        public async Task<bool> UpdateUserAsync(User user)
        {
            using var db = _context.CreateConnection();
            string sql = @"
            UPDATE Users 
            SET Email = @Email, 
                FullName = @FullName, 
                Phone = @Phone, 
                Role = @Role, 
                SeniorId = @SeniorId, 
                MonthlyTarget = @MonthlyTarget, 
                IsActive = @IsActive
            WHERE UserId = @UserId";

            int affected = await db.ExecuteAsync(sql, user);
            return affected > 0;
        }        

        // --- DASHBOARD & HIERARCHY LOGIC ---

        // 5. Get Team Stats (Used by Team Leaders or Sub-Admins)
        public async Task<decimal> GetTeamTotalBusinessAsync(int seniorId)
        {
            using var db = _context.CreateConnection();
            string sql = @"
            SELECT COALESCE(SUM(o.TotalAmount), 0)
            FROM Orders o
            INNER JOIN Leads l ON o.LeadId = l.LeadId
            INNER JOIN Users u ON l.LeadHolder = u.Email
            WHERE u.SeniorId = @seniorId";

            return await db.QuerySingleAsync<decimal>(sql, new { seniorId });
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            using var db = _context.CreateConnection();
            // Safety Check: We might want to prevent deleting the last Admin
            string sql = "DELETE FROM Users WHERE UserId = @userId";

            int affected = await db.ExecuteAsync(sql, new { userId });
            return affected > 0;
        }

        public async Task<CustomerAnalytics> GetCustomerSummaryAsync(int leadId)
        {
            using var db = _context.CreateConnection();

            // Fetching only the first and last order amounts
            string sql = @"
        SELECT 
            (SELECT TotalAmount FROM Orders WHERE LeadId = @leadId ORDER BY OrderDate ASC LIMIT 1) as FirstOrderAmount,
            (SELECT TotalAmount FROM Orders WHERE LeadId = @leadId ORDER BY OrderDate DESC LIMIT 1) as LastOrderAmount";

            var result = await db.QuerySingleOrDefaultAsync<CustomerAnalytics>(sql, new { leadId });

            // Ensure we return an object even if no orders exist yet
            return result ?? new CustomerAnalytics { LeadId = leadId };
        }        

        public async Task<IEnumerable<Lead>> GetAllFollowupLeadsAsync(LeadViewMode mode)
        {
            using var db = _context.CreateConnection();

            // Complex query: Select Lead info, and join with ONLY the newest History entry
            string sql = @"
        SELECT 
            l.*, 
            (SELECT COUNT(*) FROM LeadHistory WHERE LeadId = l.LeadId) - 1 as HistoryCount,
h.HistoryId, h.LogDate, h.Message, h.NextFollowUpDate, h.UpdatedBy
        FROM Leads l
        LEFT JOIN LeadHistory h ON l.LeadId = h.LeadId
        WHERE l.Status = 'Followup' AND h.NextFollowUpDate <= CURDATE() AND h.NextFollowUpDate IS NOT NULL AND h.HistoryId = (
            SELECT MAX(HistoryId) 
            FROM LeadHistory 
            WHERE LeadId = l.LeadId
        ) OR h.HistoryId IS NULL -- In case lead has no history yet
        ORDER BY h.NextFollowUpDate ASC;";

            if (mode == LeadViewMode.FutureFollowUp)
            {
                sql = @"
        SELECT 
            l.*, 
            (SELECT COUNT(*) FROM LeadHistory WHERE LeadId = l.LeadId) - 1 as HistoryCount,
h.HistoryId, h.LogDate, h.Message, h.NextFollowUpDate, h.UpdatedBy
        FROM Leads l
        LEFT JOIN LeadHistory h ON l.LeadId = h.LeadId
        WHERE l.Status = 'Followup' AND h.NextFollowUpDate > CURDATE() AND h.NextFollowUpDate IS NOT NULL AND h.HistoryId = (
            SELECT MAX(HistoryId) 
            FROM LeadHistory 
            WHERE LeadId = l.LeadId
        ) OR h.HistoryId IS NULL -- In case lead has no history yet
        ORDER BY h.NextFollowUpDate ASC;";
            }

            // Use Dapper to map both objects (Lead and History)
            var result = await db.QueryAsync<Lead, LeadHistoryEntry, Lead>(sql,
                (lead, history) =>
                {
                    // Map the dynamic metadata as before
                    if (!string.IsNullOrEmpty(lead.MetadataJson))
                    {
                        lead.CustomFields = JsonSerializer.Deserialize<Dictionary<string, string>>(lead.MetadataJson)
                                           ?? new Dictionary<string, string>();
                    }

                    if (!string.IsNullOrEmpty(lead.LabelsJson))
                    {
                        lead.LeadLabels = JsonSerializer.Deserialize<ObservableCollection<string>>(lead.LabelsJson)
                                           ?? new ObservableCollection<string>();
                    }

                    // Assign the latest history entry to the calculated property
                    lead.LatestUpdate = history;
                    return lead;
                },
                splitOn: "HistoryId"); // Dapper splits the row mapping here

            return result;
        }
    }
}

