using CallMan.Data;
using CallMan.Models;
using CallMan.Models.Enums;
using Dapper;
using DocumentFormat.OpenXml.Wordprocessing;
using Mysqlx.Crud;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;

namespace CallMan.Services
{
    public class LeadService
    {
        private readonly CrmDbContext _context;
        public LeadService(CrmDbContext context) => _context = context;        

        // Fetch all leads for the DataGrid
        public async Task<IEnumerable<Lead>> GetAllActiveLeadsAsync()
        {
            using var db = _context.CreateConnection();
            string sql = "SELECT * FROM Leads WHERE Status NOT IN ('Dead') ORDER BY CreatedAt DESC";

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

        public async Task<int> SaveLeadAsync(Lead lead, LeadHistoryEntry initialHistoryEntry, string user)
        {
            using var db = _context.CreateConnection();
            // Serialize dynamic fields to JSON
            lead.MetadataJson = JsonSerializer.Serialize(lead.CustomFields);
            lead.LabelsJson = JsonSerializer.Serialize(lead.LeadLabels);

            string sql = @"INSERT INTO Leads (CustomerName, Email, Phone, Status, MetadataJson, 
               CompanyName, AddressLine, City, District, State, Pincode, Country, CreatedAt, LeadHolder, WorkingArea, LeadSource, LeadTag, LabelsJson, MonthlyTarget) 
               VALUES (@CustomerName, @Email, @Phone, @Status, @MetadataJson, 
               @CompanyName, @AddressLine, @City, @District, @State, @Pincode, @Country, NOW(), @LeadHolder, @WorkingArea, @LeadSource, @LeadTag, @LabelsJson, @MonthlyTarget);
            SELECT LAST_INSERT_ID();";

            int newId = await db.ExecuteScalarAsync<int>(sql, lead);

            string linkSql = "INSERT INTO LeadDivisions (LeadId, DivisionId) VALUES (@LeadId, @DivId)";
            var linkParams = lead.AssignedDivisions.Select(divId => new { LeadId = newId, DivId = divId });
            await db.ExecuteAsync(linkSql, linkParams);

            // Save initial history entry
            await AddHistoryAsync(newId, initialHistoryEntry);
            return newId;
        }

        public async Task AddHistoryAsync(int leadId, LeadHistoryEntry historyEntry)
        {
            using var db = _context.CreateConnection();
            string sql = @"INSERT INTO LeadHistory (LeadId, Message, NextFollowUpDate, UpdatedBy, UpdatedByContent, LogDate, IsPriority) 
                       VALUES (@leadId, @message, @nextDate, @user, @updatedByContent, @logDate, @isPriority)";
            await db.ExecuteAsync(sql, new
            {
                leadId,
                message = historyEntry.Message,
                nextDate = historyEntry.NextFollowUpDate,
                user = historyEntry.UpdatedBy,
                updatedByContent = historyEntry.UpdatedByContent,
                logDate = historyEntry.LogDate,
                isPriority = historyEntry.IsPriority
            });
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
                    Status = @Status, StatusId = @StatusId, DeadReasonId = @DeadReasonId, MatureStageId = @MatureStageId, LeadSourceId = @LeadSourceId, LeadTagId = @LeadTagId,
                    CompanyName = @CompanyName, AddressLine = @AddressLine, 
                    City = @City, District = @District, State = @State, 
                    Pincode = @Pincode, MetadataJson = @MetadataJson, LeadHolder = @LeadHolder, WorkingArea = @WorkingArea, LeadSource = @LeadSource, LeadTag = @LeadTag, LabelsJson = @LabelsJson, MonthlyTarget = @MonthlyTarget WHERE LeadId = @LeadId";

            var rows = await db.ExecuteAsync(sql, lead);

            await DeleteLeadDivisionsAsync(lead.LeadId);

            string linkSql = "INSERT INTO LeadDivisions (LeadId, DivisionId) VALUES (@LeadId, @DivId)";
            var linkParams = lead.AssignedDivisions.Select(divId => new { LeadId = lead.LeadId, DivId = divId });
            await db.ExecuteAsync(linkSql, linkParams);

            return rows > 0;
        }

        public async Task<bool> DeleteLeadDivisionsAsync(int leadId)
        {
            using var db = _context.CreateConnection();
            // Note: LeadHistory has a Foreign Key with ON DELETE CASCADE in our SQL schema
            string sql = "DELETE FROM LeadDivisions WHERE LeadId = @leadId";
            var rows = await db.ExecuteAsync(sql, new { leadId });
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

        public async Task<bool> BulkDeleteLeadsAsync(IEnumerable<int> leadIds)
        {
            using var conn = _context.CreateConnection();
            string sql = "DELETE FROM Leads WHERE LeadId IN @Ids";

            int affected = await conn.ExecuteAsync(sql, new { Ids = leadIds });
            return affected > 0;
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

            var leadMap = new Dictionary<int, Lead>();

            // Complex query: Select Lead info, and join with ONLY the newest History entry
            string sql = @"
        SELECT 
    l.*, 
    (SELECT COUNT(*) FROM LeadHistory WHERE LeadId = l.LeadId) - 1 as HistoryCount,  
(SELECT COUNT(*) FROM Orders WHERE LeadId = l.LeadId) as OrderCount,
    h.*, d.*
    FROM Leads l
    LEFT JOIN LeadHistory h ON l.LeadId = h.LeadId
    LEFT JOIN LeadDivisions ld ON l.LeadId = ld.LeadId 
    LEFT JOIN Divisions d ON ld.DivisionId = d.Id
WHERE h.HistoryId = (
    SELECT MAX(HistoryId) 
    FROM LeadHistory 
    WHERE LeadId = l.LeadId
) OR h.HistoryId IS NULL -- In case lead has no history yet
ORDER BY l.LeadId DESC;";

            // Use Dapper to map both objects (Lead and History)
            var result = await db.QueryAsync<Lead, LeadHistoryEntry, Division, Lead>(sql,
                (lead, history, division) =>
                {
                    if (!leadMap.TryGetValue(lead.LeadId, out var currentLead))
                    {
                        // First time seeing this Lead - initialize and add to map
                        currentLead = lead;
                        currentLead.AssignedDivisions = new ObservableCollection<Division>();
                        currentLead.LatestUpdate = history;

                        // Handle your JSON deserialization here (only happens once)
                        if (!string.IsNullOrEmpty(currentLead.MetadataJson))
                        {
                            currentLead.CustomFields = JsonSerializer.Deserialize<Dictionary<string, string>>(currentLead.MetadataJson)
                                               ?? new Dictionary<string, string>();
                        }

                        if (!string.IsNullOrEmpty(currentLead.LabelsJson))
                        {
                            currentLead.LeadLabels = JsonSerializer.Deserialize<ObservableCollection<string>>(currentLead.LabelsJson)
                                               ?? new ObservableCollection<string>();
                        }

                        leadMap.Add(currentLead.LeadId, currentLead);
                    }                    // Map the dynamic metadata as before


                    if (division != null && !currentLead.AssignedDivisions.Any(x => x.Id == division.Id))
                    {
                        currentLead.AssignedDivisions.Add(division);
                    }

                    // Assign the latest history entry to the calculated property
                    currentLead.LatestUpdate = history;
                    return currentLead;
                },
                splitOn: "HistoryId,Id"); // Dapper splits the row mapping here

            return leadMap.Values;
        }

        public async Task UpdateLeadFullAsync(Lead lead, LeadHistoryEntry history)
        {
            using var db = _context.CreateConnection();
            using var trans = db.BeginTransaction();

            try
            {
                // 1. Update main lead status
                string updateLead = "UPDATE Leads SET Status = @Status, StatusId = @StatusId, DeadReasonId = @DeadReasonId, MatureStageId = @MatureStageId WHERE LeadId = @LeadId";
                await db.ExecuteAsync(updateLead, lead, trans);

                // 2. Insert into History
                string insertHistory = @"INSERT INTO LeadHistory 
            (LeadId, Message, Content, UpdatedByContent, NextFollowUpDate, UpdatedBy, ActionType, FollowupStage, IsPriority) 
            VALUES (@LeadId, @Message, @Content, @UpdatedByContent, @NextFollowUpDate, @UpdatedBy, @ActionType, @FollowupStage, @IsPriority)";
                await db.ExecuteAsync(insertHistory, history, trans);

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
                await db.ExecuteAsync("UPDATE Leads SET Status = @Status, StatusId = @StatusId, DeadReasonId = @DeadReasonId, MatureStageId = @MatureStageId WHERE LeadId = @LeadId", lead, trans);

                // 2. Create the Order
                string orderSql = @"INSERT INTO Orders (LeadId, TotalAmount, Description, PaymentStatus, ProcessedBy, AmountPaid, Status) 
                            VALUES (@LeadId, @TotalAmount, @Description, @PaymentStatus, @ProcessedBy, @AmountPaid, @Status);
                            SELECT LAST_INSERT_ID();";
                int newOrderId = await db.QuerySingleAsync<int>(orderSql, order, trans);

                // 3. Record First Payment linked to that Order
                payment.OrderId = newOrderId;
                string paySql = @"INSERT INTO Payments (LeadId, OrderId, TotalOrderValue, AmountReceived, Remarks) 
                          VALUES (@LeadId, @OrderId, @TotalOrderValue, @AmountReceived, @Remarks)";
                await db.ExecuteAsync(paySql, payment, trans);

                // 4. Add History Milestone
                string hist1Sql = @"INSERT INTO LeadHistory 
            (LeadId, Message, Content, UpdatedByContent, NextFollowUpDate, UpdatedBy, ActionType, FollowupStage, IsPriority) 
            VALUES (@LeadId, @Message, @Content, @UpdatedByContent, @NextFollowUpDate, @UpdatedBy, @ActionType, @FollowupStage, @IsPriority)";
                await db.ExecuteAsync(hist1Sql, history, trans);

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
                await db.ExecuteAsync("UPDATE Leads SET Status = 'Matured', StatusId = @StatusId, DeadReasonId = @DeadReasonId, MatureStageId = @MatureStageId WHERE LeadId = @LeadId", new { lead.LeadId, lead.StatusId, lead.DeadReasonId, lead.MatureStageId  }, trans);

                // 2. Create the Order
                string orderSql = @"INSERT INTO Orders (LeadId, TotalAmount, Description, PaymentStatus, ProcessedBy, AmountPaid, Status) 
                            VALUES (@LeadId, @TotalAmount, @Description, @PaymentStatus, @ProcessedBy, @AmountPaid, @Status);
                            SELECT LAST_INSERT_ID();";
                int newOrderId = await db.QuerySingleAsync<int>(orderSql, order, trans);

                // 3. Record First Payment linked to that Order
                payment.OrderId = newOrderId;
                string paySql = @"INSERT INTO Payments (LeadId, OrderId, TotalOrderValue, AmountReceived, Remarks) 
                          VALUES (@LeadId, @OrderId, @TotalOrderValue, @AmountReceived, @Remarks)";
                await db.ExecuteAsync(paySql, payment, trans);

                // 3. ENTRY #1: The Maturity Milestone (System Entry)                
                string hist1Sql = @"INSERT INTO LeadHistory 
            (LeadId, Message, Content, UpdatedByContent, NextFollowUpDate, UpdatedBy, ActionType, FollowupStage, IsPriority) 
            VALUES (@LeadId, @Message, @Content, @UpdatedByContent, @NextFollowUpDate, @UpdatedBy, @ActionType, @FollowupStage, @IsPriority)";
                await db.ExecuteAsync(hist1Sql, followUp, trans);                

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
            var leadMap = new Dictionary<int, Lead>();

            string sql = @"SELECT l.*, 
            (SELECT COALESCE(SUM(TotalAmount), 0) FROM Orders WHERE LeadId = l.LeadId) as TotalOrderAmount,
            (SELECT COALESCE(SUM(AmountReceived), 0) FROM Payments WHERE LeadId = l.LeadId) as TotalPaidAmount,
(SELECT COUNT(*) FROM LeadHistory WHERE LeadId = l.LeadId) - 1 as HistoryCount,
(SELECT COUNT(*) FROM Orders WHERE LeadId = l.LeadId) as OrderCount,
h.*, d.*
            FROM Leads l  
LEFT JOIN LeadHistory h ON l.LeadId = h.LeadId
LEFT JOIN LeadDivisions ld ON l.LeadId = ld.LeadId
LEFT JOIN Divisions d ON ld.DivisionId = d.Id
        WHERE l.Status = 'Matured' AND h.HistoryId = (
            SELECT MAX(HistoryId) 
            FROM LeadHistory 
            WHERE LeadId = l.LeadId
        ) ORDER BY l.LeadId DESC;";
            await db.QueryAsync<Lead, LeadHistoryEntry, Division, Lead>(sql, (lead, history, division) =>
            {
                // 1. If lead isn't in our map, add it
                if (!leadMap.TryGetValue(lead.LeadId, out var currentLead))
                {
                    currentLead = lead;
                    currentLead.AssignedDivisions = new ObservableCollection<Division>();
                    currentLead.LatestUpdate = history;

                    // Deserialize JSON metadata if present
                    if (!string.IsNullOrEmpty(currentLead.MetadataJson))
                    {
                        currentLead.CustomFields = JsonSerializer.Deserialize<Dictionary<string, string>>(currentLead.MetadataJson);
                    }

                    leadMap.Add(currentLead.LeadId, currentLead);
                }

                // 2. Add the division from this specific row to the existing lead's collection
                if (division != null && !currentLead.AssignedDivisions.Any(x => x.Id == division.Id))
                {
                    currentLead.AssignedDivisions.Add(division);
                }

                return currentLead;
            }, splitOn: "HistoryId,Id");

            return leadMap.Values;
        }

        // Record a payment and auto-update Order status
        public async Task RecordPaymentAsync(PaymentEntry p, LeadHistoryEntry initialHistoryEntry)
        {
            using var db = _context.CreateConnection();
            using var trans = db.BeginTransaction();
            try
            {
                await db.ExecuteAsync("INSERT INTO Payments (OrderId, LeadId, AmountReceived, PaymentMethod, Remarks) VALUES (@OrderId, @LeadId, @AmountReceived, @PaymentMethod, @Remarks)", p, trans);
                string updateOrder = "UPDATE Orders o SET PaymentStatus = IF((SELECT SUM(AmountReceived) FROM Payments WHERE OrderId = o.OrderId) >= o.TotalAmount, 'Paid', 'Partially Paid'), AmountPaid = (SELECT SUM(AmountReceived) FROM Payments WHERE OrderId = o.OrderId) WHERE OrderId = @OrderId";
                await db.ExecuteAsync(updateOrder, new { p.OrderId }, trans);

                await AddHistoryAsync(p.LeadId, initialHistoryEntry);

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
            string sql = "INSERT INTO Orders (LeadId, TotalAmount, Description, PaymentStatus, AmountPaid) VALUES (@LeadId, @TotalAmount, @Description, @PaymentStatus, @AmountPaid)";
            await db.ExecuteAsync(sql, order);
        }

        public async Task<IEnumerable<Models.Order>> GetAllOrdersWithCustomerNamesAsync()
        {
            using var db = _context.CreateConnection();
            // Join Orders with Leads to get the CustomerName for each order
            string sql = @"
        SELECT o.*, l.CustomerName , l.CompanyName as FirmName
        FROM Orders o
        INNER JOIN Leads l ON o.LeadId = l.LeadId
        ORDER BY o.OrderDate DESC";

            return await db.QueryAsync<Models.Order>(sql);
        }

        public async Task<Lead> GetLeadByIdAsync(int leadId)
        {
            using var conn = _context.CreateConnection();
            Lead resultLead = null;

            string sql = @"
        SELECT l.*, d.Id, d.Name 
        FROM Leads l
        LEFT JOIN LeadDivisions ld ON l.LeadId = ld.LeadId
        LEFT JOIN Divisions d ON ld.DivisionId = d.Id
        WHERE l.LeadId = @Id";

            await conn.QueryAsync<Lead, Division, Lead>(sql, (lead, division) =>
            {
                if (resultLead == null)
                {
                    resultLead = lead;
                    resultLead.AssignedDivisions = new ObservableCollection<Division>();
                }

                if (division != null)
                {
                    resultLead.AssignedDivisions.Add(division);
                }
                return lead;
            }, new { Id = leadId }, splitOn: "Id");

            return resultLead;
        }

        public async Task<DashboardStats> GetDashboardStatsAsync()
        {
            using var db = _context.CreateConnection();

            // Using a single complex query to get all counts for performance
            string sql = @"
        SELECT 
            (SELECT COUNT(*) FROM Leads) as AllLeads,
            (SELECT COUNT(*) FROM Leads WHERE Status = 'New') as NewLeads,
            (SELECT COUNT(*) FROM Leads WHERE Status = 'Followup') as FollowupLeads,
            (SELECT COUNT(*) FROM Leads l WHERE Status = 'Followup' AND (SELECT MAX(LogDate) FROM LeadHistory WHERE LeadId = l.LeadId) < DATE_SUB(NOW(), INTERVAL 30 DAY)) as NoFollowupLeads,
            (SELECT COUNT(*) FROM Leads WHERE Status = 'Dead') as Dead,

            (SELECT COUNT(*) FROM Leads WHERE Status = 'Matured') as Customers,
(SELECT 
    COUNT(DISTINCT l.LeadId) AS NoUpdation7Days
FROM Leads l
WHERE l.Status = 'Matured'
AND (
    SELECT GREATEST(
        l.CreatedAt,
        IFNULL((SELECT MAX(LogDate) FROM LeadHistory WHERE LeadId = l.LeadId), '1900-01-01'),
        IFNULL((SELECT MAX(OrderDate) FROM Orders WHERE LeadId = l.LeadId), '1900-01-01'),
        IFNULL((SELECT MAX(PaymentDate) FROM Payments WHERE LeadId = l.LeadId), '1900-01-01')
    )
) < DATE_SUB(NOW(), INTERVAL 7 DAY)) AS NoUpdation7Days,
            (SELECT COUNT(*) FROM Leads l WHERE Status = 'Matured' AND (SELECT COUNT(*) FROM Orders WHERE LeadId = l.LeadId) <= 1) as NoRepeatOrder,
            (SELECT COUNT(*) FROM Leads l WHERE Status = 'Matured' AND (SELECT MAX(OrderDate) FROM Orders WHERE LeadId = l.LeadId) < DATE_SUB(NOW(), INTERVAL 30 DAY)) as NoOrder,
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
            var parameters = new DynamicParameters();

            // 1. Unified LeadHolder Filter
            string holderFilter = "";
            if (!string.IsNullOrEmpty(filter.LeadHolder))
            {
                holderFilter = " AND LeadHolder = @Holder ";
                parameters.Add("Holder", filter.LeadHolder);
            }

            // 2. Date Parameters
            parameters.Add("From", filter.FromDate);
            parameters.Add("To", filter.ToDate);

            // Helper logic for Date Filtering
            string dateRange = (filter.FromDate != null) ? " BETWEEN @From AND @To " : null;

            string sql = $@"
    SELECT 
        /* ALL LEADS: Counted by Creation Date */
        (SELECT COUNT(*) FROM Leads 
         WHERE 1=1 {holderFilter} 
         {(dateRange != null ? $" AND CreatedAt {dateRange}" : "")}) as AllLeads,

        /* NEW LEADS: Created in this range */
        (SELECT COUNT(*) FROM Leads 
         WHERE Status = 'New' {holderFilter} 
         {(dateRange != null ? $" AND CreatedAt {dateRange}" : "")}) as NewLeads,

        /* FOLLOWUP LEADS: Filtered by the LATEST Activity Date (History Log) */
        (SELECT COUNT(DISTINCT l.LeadId) FROM Leads l
         INNER JOIN LeadHistory h ON l.LeadId = h.LeadId
         WHERE l.Status = 'Followup' {holderFilter.Replace("LeadHolder", "l.LeadHolder")}
         {(dateRange != null ? $" AND h.LogDate {dateRange}" : "")}) as FollowupLeads,

        /* NO FOLLOWUP (30 Days): Stale leads within the filtered group */
        (SELECT COUNT(*) FROM Leads l 
         WHERE Status = 'Followup' {holderFilter.Replace("LeadHolder", "l.LeadHolder")}
         {(dateRange != null ? $" AND l.CreatedAt {dateRange}" : "")}
         AND (SELECT MAX(LogDate) FROM LeadHistory WHERE LeadId = l.LeadId) < DATE_SUB(NOW(), INTERVAL 30 DAY)) as NoFollowupLeads,

        /* NO UPDATION (7 Days): Based on GREATEST of all activities */
        (SELECT COUNT(DISTINCT l.LeadId) FROM Leads l
         WHERE l.Status = 'Matured' {holderFilter.Replace("LeadHolder", "l.LeadHolder")}
         {(dateRange != null ? $" AND l.CreatedAt {dateRange}" : "")}
         AND (SELECT GREATEST(l.CreatedAt, 
                IFNULL((SELECT MAX(LogDate) FROM LeadHistory WHERE LeadId = l.LeadId), '1900-01-01'),
                IFNULL((SELECT MAX(OrderDate) FROM Orders WHERE LeadId = l.LeadId), '1900-01-01'),
                IFNULL((SELECT MAX(PaymentDate) FROM Payments WHERE LeadId = l.LeadId), '1900-01-01'))
             ) < DATE_SUB(NOW(), INTERVAL 7 DAY)) as NoUpdation7Days,

        /* TOTAL BUSINESS: Filtered by ORDER DATE, not Lead Creation Date */
        (SELECT COALESCE(SUM(o.TotalAmount), 0) FROM Orders o 
         INNER JOIN Leads l ON o.LeadId = l.LeadId
         WHERE 1=1 {holderFilter.Replace("LeadHolder", "l.LeadHolder")}
         {(dateRange != null ? $" AND o.OrderDate {dateRange}" : "")}) as TotalBusiness,

        /* CUSTOMERS: Matured leads in this period */
        (SELECT COUNT(*) FROM Leads WHERE Status = 'Matured' {holderFilter}
         {(dateRange != null ? $" AND CreatedAt {dateRange}" : "")}) as Customers";

            var stats = await db.QuerySingleAsync<DashboardStats>(sql, parameters);

            // 4. Calculate Percentages in the ViewModel/Service after retrieval
            return stats;
        }

        /*public async Task<DashboardStats> GetDashboardStatsFilteredAsync(DashboardFilter filter)
        {
            using var db = _context.CreateConnection();

            // Base WHERE clause logic
            string whereClause = " WHERE 1=1 ";
            string whereClause2 = " WHERE 1=1 ";
            var parameters = new DynamicParameters();

            if (!string.IsNullOrEmpty(filter.LeadHolder))
            {
                whereClause += " AND LeadHolder = @LeadHolder ";
                whereClause2 += " AND l.LeadHolder = @LeadHolder ";
                parameters.Add("LeadHolder", filter.LeadHolder);
            }

            if (filter.FromDate != null && filter.ToDate != null)
            {
                // Filtering based on Lead Creation or Update date
                whereClause += " AND CreatedAt BETWEEN @FromDate AND @ToDate ";
                whereClause2 += " AND l.CreatedAt BETWEEN @FromDate AND @ToDate ";
                parameters.Add("FromDate", filter.FromDate);
                parameters.Add("ToDate", filter.ToDate);
            }

            string sql = $@"
        SELECT 
            (SELECT COUNT(*) FROM Leads {whereClause}) as AllLeads,
            (SELECT COUNT(*) FROM Leads {whereClause} AND Status = 'New') as NewLeads,
            (SELECT COUNT(*) FROM Leads {whereClause} AND Status = 'Followup') as FollowupLeads,
            (SELECT COUNT(*) FROM Leads l {whereClause2} AND Status = 'Followup' AND (SELECT MAX(LogDate) FROM LeadHistory WHERE LeadId = l.LeadId) < DATE_SUB(NOW(), INTERVAL 30 DAY)) as NoFollowupLeads,
            (SELECT COUNT(*) FROM Leads {whereClause} AND Status = 'Dead') as Dead,
(SELECT 
    COUNT(DISTINCT l.LeadId) AS NoUpdation7Days
FROM Leads l
{whereClause2} AND l.Status = 'Matured'
AND (
    SELECT GREATEST(
        l.CreatedAt,
        IFNULL((SELECT MAX(LogDate) FROM LeadHistory WHERE LeadId = l.LeadId), '1900-01-01'),
        IFNULL((SELECT MAX(OrderDate) FROM Orders WHERE LeadId = l.LeadId), '1900-01-01'),
        IFNULL((SELECT MAX(PaymentDate) FROM Payments WHERE LeadId = l.LeadId), '1900-01-01')
    )
) < DATE_SUB(NOW(), INTERVAL 7 DAY)) AS NoUpdation7Days,
            (SELECT COUNT(*) FROM Leads {whereClause} AND Status = 'Matured') as Customers,
            (SELECT COUNT(*) FROM Leads l {whereClause} AND Status = 'Matured' AND (SELECT COUNT(*) FROM Orders WHERE LeadId = l.LeadId) <= 1) as NoRepeatOrder,
            (SELECT COUNT(*) FROM Leads l {whereClause} AND Status = 'Matured' AND (SELECT MAX(OrderDate) FROM Orders WHERE LeadId = l.LeadId) < DATE_SUB(NOW(), INTERVAL 30 DAY)) as NoOrder,
            (SELECT COALESCE(SUM(o.TotalAmount), 0) FROM Orders o 
                    INNER JOIN Leads l ON o.LeadId = l.LeadId 
                    {whereClause.Replace("CreatedAt", "o.OrderDate")}) as TotalBusiness";

            return await db.QuerySingleAsync<DashboardStats>(sql, parameters);
        }  */     

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
            var leadMap = new Dictionary<int, Lead>();

            // Complex query: Select Lead info, and join with ONLY the newest History entry
            string sql = @"
        SELECT 
            l.*, 
            (SELECT COUNT(*) FROM LeadHistory WHERE LeadId = l.LeadId) - 1 as HistoryCount,
            (SELECT COUNT(*) FROM Orders WHERE LeadId = l.LeadId) as OrderCount,
            h.*, d.*
        FROM Leads l
        LEFT JOIN LeadHistory h ON l.LeadId = h.LeadId
        LEFT JOIN LeadDivisions ld ON l.LeadId = ld.LeadId
        LEFT JOIN Divisions d ON ld.DivisionId = d.Id
        WHERE h.NextFollowUpDate <= CURDATE() AND h.NextFollowUpDate IS NOT NULL AND h.HistoryId = (
            SELECT MAX(HistoryId) 
            FROM LeadHistory 
            WHERE LeadId = l.LeadId
        ) AND h.HistoryId IS NOT NULL -- In case lead has no history yet
        ORDER BY h.NextFollowUpDate ASC;";

            if (mode == LeadViewMode.FutureFollowUp)
            {
                sql = @"
        SELECT 
            l.*, 
            (SELECT COUNT(*) FROM LeadHistory WHERE LeadId = l.LeadId) - 1 as HistoryCount,
            (SELECT COUNT(*) FROM Orders WHERE LeadId = l.LeadId) as OrderCount,
            h.*, d.*
        FROM Leads l
        LEFT JOIN LeadHistory h ON l.LeadId = h.LeadId
        LEFT JOIN LeadDivisions ld ON l.LeadId = ld.LeadId
        LEFT JOIN Divisions d ON ld.DivisionId = d.Id
        WHERE h.NextFollowUpDate > CURDATE() AND h.NextFollowUpDate IS NOT NULL AND h.HistoryId = (
            SELECT MAX(HistoryId) 
            FROM LeadHistory 
            WHERE LeadId = l.LeadId
        ) AND h.HistoryId IS NOT NULL -- In case lead has no history yet
        ORDER BY h.NextFollowUpDate ASC;";
            }

            // Use Dapper to map both objects (Lead and History)
            await db.QueryAsync<Lead, LeadHistoryEntry, Division, Lead>(sql, (lead, history, division) =>
            {
                // 1. If lead isn't in our map, add it
                if (!leadMap.TryGetValue(lead.LeadId, out var currentLead))
                {
                    currentLead = lead;
                    currentLead.AssignedDivisions = new ObservableCollection<Division>();
                    currentLead.LatestUpdate = history;

                    // Deserialize JSON metadata if present
                    if (!string.IsNullOrEmpty(currentLead.MetadataJson))
                    {
                        currentLead.CustomFields = JsonSerializer.Deserialize<Dictionary<string, string>>(currentLead.MetadataJson);
                    }

                    leadMap.Add(currentLead.LeadId, currentLead);
                }

                // 2. Add the division from this specific row to the existing lead's collection
                if (division != null && !currentLead.AssignedDivisions.Any(x => x.Id == division.Id))
                {
                    currentLead.AssignedDivisions.Add(division);
                }

                return currentLead;
            }, splitOn: "HistoryId,Id"); // Dapper splits the row mapping here

            return leadMap.Values;
        }

        /// <summary>
        /// Fetches the comprehensive financial summary for a given division context.
        /// </summary>
        /// <param name="divisionId">The active division registry filter ID.</param>
        /// <returns>A populated CustomerStats object.</returns>
        public async Task<CustomerStats> GetCustomerFinancialSummaryAsync(int divisionId)
        {
            // WHERE DivisionId = @DivId
            const string sql = @"
    WITH CustomerOrderStats AS (
        SELECT 
            LeadId,
            OrderId,
            TotalAmount,
            AmountPaid,
            -- DYNAMIC CALCULATION: Subtraction replaces the missing column footprint
            (TotalAmount - AmountPaid) AS CalculatedOrderBalance,
            ROW_NUMBER() OVER (PARTITION BY LeadId ORDER BY OrderDate ASC, OrderId ASC) AS OrderSequence
        FROM Orders
        WHERE DivisionId = @DivId OR DivisionId IS NULL
    )
    SELECT 
        COUNT(DISTINCT o.LeadId) AS TotalCustomers,
        COUNT(o.OrderId) AS TotalOrders,
        
        -- Business Value Aggregations
        IFNULL(SUM(CASE WHEN o.OrderSequence = 1 THEN o.TotalAmount ELSE 0 END), 0.00) AS TotalFirstOrders,
        IFNULL(SUM(CASE WHEN o.OrderSequence > 1 THEN o.TotalAmount ELSE 0 END), 0.00) AS TotalOtherOrders,
        IFNULL(SUM(o.TotalAmount), 0.00) AS TotalBusiness,
        
        -- First Order Financial Breakdowns
        IFNULL(SUM(CASE WHEN o.OrderSequence = 1 THEN o.AmountPaid ELSE 0 END), 0.00) AS TotalFirstOrderAmountPaid,
        IFNULL(SUM(CASE WHEN o.OrderSequence = 1 THEN o.CalculatedOrderBalance ELSE 0 END), 0.00) AS TotalFirstOrderOutstanding,
        
        -- Other (Repeat) Order Financial Breakdowns
        IFNULL(SUM(CASE WHEN o.OrderSequence > 1 THEN o.AmountPaid ELSE 0 END), 0.00) AS TotalOtherOrderAmountPaid,
        IFNULL(SUM(CASE WHEN o.OrderSequence > 1 THEN o.CalculatedOrderBalance ELSE 0 END), 0.00) AS TotalOtherOrderOutstanding,
        
        -- Global Outstanding Target
        IFNULL(SUM(o.CalculatedOrderBalance), 0.00) AS TotalOutstanding
    FROM CustomerOrderStats o;";

            try
            {
                using var conn = _context.CreateConnection();

                // QueryFirstOrDefaultAsync safely returns a default fallback object if no rows exist
                var result = await conn.QueryFirstOrDefaultAsync<CustomerStats>(sql, new { DivId = divisionId });

                return result ?? new CustomerStats();
            }
            catch (Exception ex)
            {
                // Log exception here according to SofricONE logging standards (e.g., Sentry)
                throw new InvalidOperationException("Failed to retrieve customer financial metrics.", ex);
            }
        }
    }
}

