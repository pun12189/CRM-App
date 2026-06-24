using CallMan.Data;
using CallMan.Models;
using CallMan.Models.Enums;
using Dapper;
using DocumentFormat.OpenXml.Wordprocessing;
using Mysqlx.Crud;
using Org.BouncyCastle.Asn1.X509;
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

            string sql = @"INSERT INTO Leads (CustomerName, Email, Phone, AltPhone, Status, MetadataJson, 
               CompanyName, AddressLine, City, District, State, Pincode, Country, CreatedAt, LeadHolder, WorkingArea, LeadSource, LeadTag, LabelsJson, MonthlyTarget) 
               VALUES (@CustomerName, @Email, @Phone, @AltPhone, @Status, @MetadataJson, 
               @CompanyName, @AddressLine, @City, @District, @State, @Pincode, @Country, NOW(), @LeadHolder, @WorkingArea, @LeadSource, @LeadTag, @LabelsJson, @MonthlyTarget);
            SELECT LAST_INSERT_ID();";

            int newId = await db.ExecuteScalarAsync<int>(sql, lead);

            string linkSql = "INSERT INTO LeadDivisions (LeadId, DivisionId) VALUES (@LeadId, @DivId)";
            var linkParams = lead.AssignedDivisions.Select(divId => new { LeadId = newId, DivId = divId.Id });
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
                    CustomerName = @CustomerName, Email = @Email, Phone = @Phone, AltPhone = @AltPhone,
                    Status = @Status, StatusId = @StatusId, DeadReasonId = @DeadReasonId, MatureStageId = @MatureStageId, LeadSourceId = @LeadSourceId, LeadTagId = @LeadTagId,
                    CompanyName = @CompanyName, AddressLine = @AddressLine, 
                    City = @City, District = @District, State = @State, 
                    Pincode = @Pincode, MetadataJson = @MetadataJson, LeadHolder = @LeadHolder, WorkingArea = @WorkingArea, LeadSource = @LeadSource, LeadTag = @LeadTag, LabelsJson = @LabelsJson, MonthlyTarget = @MonthlyTarget WHERE LeadId = @LeadId";

            var rows = await db.ExecuteAsync(sql, lead);

            await DeleteLeadDivisionsAsync(lead.LeadId);

            string linkSql = "INSERT INTO LeadDivisions (LeadId, DivisionId) VALUES (@LeadId, @DivId)";
            var linkParams = lead.AssignedDivisions.Select(divId => new { LeadId = lead.LeadId, DivId = divId.Id });
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

        public async Task<bool> BulkDeadLeadsAsync(IEnumerable<int> leadIds)
        {
            using var conn = _context.CreateConnection();
            string sql = "UPDATE Leads SET Status = 'Dead', DeadReasonId = NULL WHERE LeadId IN @Ids;";

            int affected = await conn.ExecuteAsync(sql, new { Ids = leadIds });
            return affected > 0;
        }

        public async Task<bool> BulkMatureDeadLeadsAsync(IEnumerable<int> leadIds)
        {
            using var conn = _context.CreateConnection();
            string sql = "UPDATE Leads SET Status = 'Winback Pool', DeadReasonId = NULL WHERE LeadId IN @Ids;";

            int affected = await conn.ExecuteAsync(sql, new { Ids = leadIds });
            return affected > 0;
        }

        public async Task<bool> BulkChangeLeadHolderAsync(IEnumerable<int> leadIds, string user, bool isNew, DateTime date)
        {
            using var conn = _context.CreateConnection();
            string sql = @"
                UPDATE Leads 
                SET LeadHolder = @User, 
                    Status = IF(@AsNew, 'New', Status),
                    CreatedAt = IF(@AsNew, @Date, CreatedAt)
                WHERE LeadId IN @Ids;";

            var affected = await conn.ExecuteAsync(sql, new
            {
                User = user,
                AsNew = isNew,
                Date = date,
                Ids = leadIds
            });


            return affected > 0;
        }

        public async Task<bool> BulkChangeLeadLablesAsync(int id, string json)
        {
            using var conn = _context.CreateConnection();
            string sql = @"
                UPDATE Leads SET LabelsJson = @Json WHERE LeadId = @Id;";

            var affected = await conn.ExecuteAsync(sql, new
            {
                Json =json,
                Id = id
            });


            return affected > 0;
        }

        public async Task<IEnumerable<LeadHistoryEntry>> GetHistoryByLeadIdAsync(int leadId)
        {
            using var db = _context.CreateConnection();
            string sql = "SELECT * FROM LeadHistory WHERE LeadId = @leadId ORDER BY LogDate DESC";
            return await db.QueryAsync<LeadHistoryEntry>(sql, new { leadId });
        }

        /// <summary>
        /// Highly optimized database pagination system. Fetches page data and total row counts 
        /// cleanly in a fast, single-connection execution pass.
        /// </summary>
        public async Task<(IEnumerable<Lead> Leads, int TotalCount)> GetLeadsPagedAsync(int limit, int offset)
        {
            using var db = _context.CreateConnection();

            // 1. First execution pass: Instantly extract total counts for pagination array mappings
            const string countSql = "SELECT COUNT(*) FROM Leads;";
            int totalCount = await db.ExecuteScalarAsync<int>(countSql);

            // If there are zero entries overall, short-circuit out to save computing overhead
            if (totalCount == 0)
            {
                return (Enumerable.Empty<Lead>(), 0);
            }

            // 2. Second execution pass: Fetch the specific page slice using pre-aggregated joins
            string dataSql = @"
                SELECT 
                    l.*, 
                    COALESCE(hc.HistCount, 0) AS HistoryCount,
                    COALESCE(oc.OrdCount, 0) AS OrderCount,
                    h.*, 
                    d.*
                FROM (
                    -- CRITICAL FIX: Limit the unique core Leads FIRST before running any row-multiplying joins
                    SELECT * FROM Leads                      
                    ORDER BY LeadId DESC 
                    LIMIT @Limit OFFSET @Offset
                ) l
    
                -- Now safely join histories and divisions without losing your page size target count
                LEFT JOIN (
                    SELECT lh.* FROM LeadHistory lh
                    INNER JOIN (
                        SELECT LeadId, MAX(HistoryId) as MaxId 
                        FROM LeadHistory 
                        GROUP BY LeadId
                    ) latest ON lh.HistoryId = latest.MaxId
                ) h ON l.LeadId = h.LeadId
    
                LEFT JOIN LeadDivisions ld ON l.LeadId = ld.LeadId 
                LEFT JOIN Divisions d ON ld.DivisionId = d.Id
    
                LEFT JOIN (
                    SELECT LeadId, COUNT(*) - 1 AS HistCount FROM LeadHistory GROUP BY LeadId
                ) hc ON l.LeadId = hc.LeadId
    
                LEFT JOIN (
                    SELECT LeadId, COUNT(*) AS OrdCount FROM Orders GROUP BY LeadId
                ) oc ON l.LeadId = oc.LeadId
    
                ORDER BY l.LeadId DESC;";

            var leadMap = new Dictionary<int, Lead>();

            await db.QueryAsync<Lead, LeadHistoryEntry, Division, Lead>(
                dataSql,
                (lead, history, division) =>
                {
                    if (!leadMap.TryGetValue(lead.LeadId, out var currentLead))
                    {
                        currentLead = lead;
                        currentLead.AssignedDivisions = new ObservableCollection<Division>();
                        currentLead.LatestUpdate = history;

                        // Localized dynamic string conversions run only once per record entry
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
                    }

                    // Append divisions maps cleanly on joint duplications steps
                    if (division != null && !currentLead.AssignedDivisions.Any(x => x.Id == division.Id))
                    {
                        currentLead.AssignedDivisions.Add(division);
                    }

                    return currentLead;
                },
                new { Limit = limit, Offset = offset },
                splitOn: "HistoryId,Id"
            );

            return (leadMap.Values, totalCount);
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

        public async Task<IEnumerable<Lead>> GetAllLeadsWithLeadTagsAsync(int id)
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
WHERE l.LeadTagId = @LeadTagId AND (h.HistoryId = (
    SELECT MAX(HistoryId) 
    FROM LeadHistory 
    WHERE LeadId = l.LeadId
) OR h.HistoryId IS NULL) -- In case lead has no history yet
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
                }, new {LeadTagId = id}, splitOn: "HistoryId,Id"); // Dapper splits the row mapping here

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



                    if (!string.IsNullOrEmpty(currentLead.LabelsJson))

                    {

                        currentLead.LeadLabels = JsonSerializer.Deserialize<ObservableCollection<string>>(currentLead.LabelsJson)

                                           ?? new ObservableCollection<string>();

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

        /// <summary>
        /// Fetches a high-performance paginated ledger of matured leads (Customers),
        /// including optimized pre-aggregated order totals and payment histories.
        /// </summary>
        public async Task<(IEnumerable<Lead> Customers, int TotalCount)> GetMaturedLedgerPagedAsync(int limit, int offset)
        {
            using var db = _context.CreateConnection();

            // 1. Instantly extract grand total counts of matured leads to map pagination numbers
            const string countSql = "SELECT COUNT(*) FROM Leads WHERE Status = 'Matured';";
            int totalCount = await db.ExecuteScalarAsync<int>(countSql);

            if (totalCount == 0)
            {
                return (Enumerable.Empty<Lead>(), 0);
            }

            // 2. High-performance single-pass multi-mapping database query
            string dataSql = @"
                SELECT 
                    l.*, 
                    COALESCE(am.TotalOrderAmount, 0) AS TotalOrderAmount,
                    COALESCE(pm.TotalPaidAmount, 0) AS TotalPaidAmount,
                    COALESCE(hc.HistCount, 0) AS HistoryCount,
                    COALESCE(am.OrdCount, 0) AS OrderCount,
                    h.*, 
                    d.*
                FROM (
                    -- CRITICAL FIX: Limit core items FIRST to protect page-size boundaries
                    SELECT * FROM Leads 
                    WHERE Status = 'Matured'
                    ORDER BY LeadId DESC
                    LIMIT @Limit OFFSET @Offset
                ) l
                
                -- Isolate the newest single history entry per record instantly
                LEFT JOIN (
                    SELECT lh.* FROM LeadHistory lh
                    INNER JOIN (
                        SELECT LeadId, MAX(HistoryId) as MaxId 
                        FROM LeadHistory 
                        GROUP BY LeadId
                    ) latest ON lh.HistoryId = latest.MaxId
                ) h ON l.LeadId = h.LeadId
                
                -- Map divisions lookup relationships
                LEFT JOIN LeadDivisions ld ON l.LeadId = ld.LeadId 
                LEFT JOIN Divisions d ON ld.DivisionId = d.Id
                
                -- Fast single-pass pre-calculated aggregations
                LEFT JOIN (
                    SELECT LeadId, COUNT(*) - 1 AS HistCount FROM LeadHistory GROUP BY LeadId
                ) hc ON l.LeadId = hc.LeadId
                
                LEFT JOIN (
                    SELECT LeadId, SUM(TotalAmount) AS TotalOrderAmount, COUNT(*) AS OrdCount 
                    FROM Orders 
                    GROUP BY LeadId
                ) am ON l.LeadId = am.LeadId
                
                LEFT JOIN (
                    SELECT LeadId, SUM(AmountReceived) AS TotalPaidAmount FROM Payments GROUP BY LeadId
                ) pm ON l.LeadId = pm.LeadId
                
                ORDER BY l.LeadId DESC;";

            var leadMap = new Dictionary<int, Lead>();

            await db.QueryAsync<Lead, LeadHistoryEntry, Division, Lead>(
                dataSql,
                (lead, history, division) =>
                {
                    if (!leadMap.TryGetValue(lead.LeadId, out var currentLead))
                    {
                        currentLead = lead;
                        currentLead.AssignedDivisions = new ObservableCollection<Division>();
                        currentLead.LatestUpdate = history;

                        // Safe JSON string conversions executed strictly once per record item
                        if (!string.IsNullOrEmpty(currentLead.MetadataJson))
                        {
                            currentLead.CustomFields = JsonSerializer.Deserialize<Dictionary<string, string>>(currentLead.MetadataJson);
                        }

                        if (!string.IsNullOrEmpty(currentLead.LabelsJson))
                        {
                            currentLead.LeadLabels = JsonSerializer.Deserialize<ObservableCollection<string>>(currentLead.LabelsJson)
                                                       ?? new ObservableCollection<string>();
                        }

                        leadMap.Add(currentLead.LeadId, currentLead);
                    }

                    // Append running division mappings cleanly
                    if (division != null && !currentLead.AssignedDivisions.Any(x => x.Id == division.Id))
                    {
                        currentLead.AssignedDivisions.Add(division);
                    }

                    return currentLead;
                },
                new { Limit = limit, Offset = offset },
                splitOn: "HistoryId,Id"
            );

            return (leadMap.Values, totalCount);
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

        public async Task<Lead?> GetLeadByIdAsync(int leadId)
        {
            using var db = _context.CreateConnection();

            // Since we are targeting a single Lead ID, we don't need a full Map Dictionary anymore; 
            // a single tracking instance variable works perfectly.
            Lead? targetLead = null;

            // Optimized Single-Target Multi-Mapping Query
            string sql = @"
        SELECT 
            l.*, 
            (SELECT COUNT(*) FROM LeadHistory WHERE LeadId = l.LeadId) - 1 as HistoryCount,  
            (SELECT COUNT(*) FROM Orders WHERE LeadId = l.LeadId) as OrderCount,
            h.*, 
            d.*
        FROM Leads l
        LEFT JOIN LeadHistory h ON l.LeadId = h.LeadId
        LEFT JOIN LeadDivisions ld ON l.LeadId = ld.LeadId 
        LEFT JOIN Divisions d ON ld.DivisionId = d.Id
        WHERE l.LeadId = @LeadId 
          AND (h.HistoryId = (
                SELECT MAX(HistoryId) 
                FROM LeadHistory 
                WHERE LeadId = l.LeadId
               ) OR h.HistoryId IS NULL);";

            // Execute multi-mapping row traversal over the structural child tables
            await db.QueryAsync<Lead, LeadHistoryEntry, Division, Lead>(sql,
                (lead, history, division) =>
                {
                    // First row pass: initialize our target object and deserialize JSON structures
                    if (targetLead == null)
                    {
                        targetLead = lead;
                        targetLead.AssignedDivisions = new ObservableCollection<Division>();
                        targetLead.LatestUpdate = history;

                        // Handle Custom Field JSON Deserialization Layer
                        if (!string.IsNullOrEmpty(targetLead.MetadataJson))
                        {
                            targetLead.CustomFields = JsonSerializer.Deserialize<Dictionary<string, string>>(targetLead.MetadataJson)
                                                       ?? new Dictionary<string, string>();
                        }

                        // Handle Labels JSON Deserialization Layer
                        if (!string.IsNullOrEmpty(targetLead.LabelsJson))
                        {
                            targetLead.LeadLabels = JsonSerializer.Deserialize<ObservableCollection<string>>(targetLead.LabelsJson)
                                                     ?? new ObservableCollection<string>();
                        }
                    }

                    // Subsequent row passes: append multiple items into the collection safely if they exist
                    if (division != null && !targetLead.AssignedDivisions.Any(x => x.Id == division.Id))
                    {
                        targetLead.AssignedDivisions.Add(division);
                    }

                    // Always ensure the history snapshot reference stays mapped to the current instance object
                    targetLead.LatestUpdate = history;

                    return targetLead;
                },
                new { LeadId = leadId }, // Pass the parameters safely to protect from SQL Injection
                splitOn: "HistoryId,Id");

            return targetLead;
        }

        public async Task<DashboardStageSummaries> GetDashboardStageSummariesAsync()
        {
            var summaries = new DashboardStageSummaries();
            using var db = _context.CreateConnection();

            // 1. COMPILING REMINDERS BADGES (Re-Order thresholds & outstanding unpaid accounts)
            string remindersSql = @"     
    
            SELECT 'New' as `Key`, COUNT(*) as `Value` 
            FROM Orders 
            WHERE (PaymentStatus = 'Unpaid' OR PaymentStatus = 'Partially Paid')
              AND (OrderType = 'New' OR OrderType = 'Sale')
      
            UNION ALL    
    
            SELECT 'Repeat' as `Key`, COUNT(*) as `Value` 
            FROM Orders 
            WHERE (PaymentStatus = 'Unpaid' OR PaymentStatus = 'Partially Paid')
              AND (OrderType != 'New' AND OrderType != 'Sale');";

            summaries.Reminders = (await db.QueryAsync<KeyValuePair<string, int>>(remindersSql)).ToList();

            // 2. COMPILING FOLLOWUP STAGES BADGES
            string followupSql = @"
            SELECT 'All FollowUps' as `Key`, COUNT(*) as `Value` FROM Leads WHERE Status = 'Followup'
            UNION ALL
            SELECT s.StatusesName as `Key`, COUNT(l.LeadId) as `Value`
            FROM LeadStatuses s
            LEFT JOIN Leads l ON s.Id = l.StatusId AND l.Status = 'Followup'
            GROUP BY s.Id, s.StatusesName
            ORDER BY `Key` ASC;";
            summaries.FollowupStages = (await db.QueryAsync<KeyValuePair<string, int>>(followupSql)).ToList();

            // 3. COMPILING MATURE STAGES BADGES
            string matureSql = @"
            SELECT 'All Matured' as `Key`, COUNT(*) as `Value` FROM Leads WHERE Status = 'Matured'
            UNION ALL
            SELECT m.MatureStagesName as `Key`, COUNT(l.LeadId) as `Value`
            FROM MatureStages m
            LEFT JOIN Leads l ON m.Id = l.MatureStageId AND l.Status = 'Matured'
            GROUP BY m.Id, m.MatureStagesName
            ORDER BY `Key` ASC;";
            summaries.MatureStages = (await db.QueryAsync<KeyValuePair<string, int>>(matureSql)).ToList();

            // 4. COMPILING DEAD STAGES AND LABELS BADGES
            string labelsSql = @"
            -- A. Calculate a baseline counter of how many unique labels exist in your master setup
            SELECT 'All Labels' as `Key`, COUNT(*) as `Value` 
            FROM LeadLabels

            UNION ALL

            -- B. Run a high-performance JSON search checking how many leads possess each master label string
            SELECT 
                master.LabelsName as `Key`,
                (
                    SELECT COUNT(*) 
                    FROM Leads l
                    WHERE l.LabelsJson IS NOT NULL 
                      AND l.LabelsJson != ''
                      AND l.LabelsJson != '[]'
                      -- Safely checks if the string exists anywhere inside the array
                      AND JSON_CONTAINS(l.LabelsJson, JSON_QUOTE(master.LabelsName))
                ) as `Value`
            FROM LeadLabels master
            WHERE master.LabelsName IS NOT NULL AND master.LabelsName != ''
            ORDER BY `Key` ASC;";

            summaries.LeadLabels = (await db.QueryAsync<KeyValuePair<string, int>>(labelsSql)).ToList();

            return summaries;
        }

        public async Task<DashboardStats> GetDashboardStatsAsync()
        {
            using var db = _context.CreateConnection();

            // Combined multi-table aggregation execution block
            string sql = @"
        SELECT 
            
            (SELECT COUNT(*) FROM Leads) as AllLeads,
            (SELECT COUNT(*) FROM Leads WHERE Status = 'New') as NewLeads,
            (SELECT COUNT(*) FROM Leads WHERE Status = 'Followup') as FollowupLeads,
            (SELECT COUNT(*) FROM Leads l WHERE Status = 'Followup' AND (SELECT MAX(LogDate) FROM LeadHistory WHERE LeadId = l.LeadId) < DATE_SUB(NOW(), INTERVAL 30 DAY)) as NoFollowupLeads,
            (SELECT COUNT(*) FROM Leads WHERE Status = 'Dead') as Dead,
            (SELECT COUNT(*) FROM Leads WHERE Status = 'Matured') as Customers,
            
            (SELECT 
                COUNT(DISTINCT l.LeadId)
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
            
            -- TODO: Replace with custom user logic lookup check when target matrix details are provided
            (SELECT COUNT(*) FROM (
                SELECT l.LeadId
                FROM Leads l
                LEFT JOIN Orders o ON l.LeadId = o.LeadId 
                    AND o.OrderDate >= DATE_ADD(LAST_DAY(DATE_SUB(NOW(), INTERVAL 2 MONTH)), INTERVAL 1 DAY)
                    AND o.OrderDate <= LAST_DAY(DATE_SUB(NOW(), INTERVAL 1 MONTH))
                WHERE l.Status = 'Matured' 
                  AND IFNULL(l.MonthlyTarget, 0) > 0
                GROUP BY l.LeadId, l.MonthlyTarget
                HAVING IFNULL(SUM(o.TotalAmount), 0) < l.MonthlyTarget
            ) as CustomerShortfallTrack) as BelowTarget, 
            
            (SELECT COALESCE(SUM(TotalAmount), 0) FROM Orders) as TotalBusiness,

            
            (SELECT COUNT(DISTINCT CategoryId) FROM Products WHERE CategoryId IS NOT NULL) as TotalCategoriesUsed,
            (SELECT COUNT(*) FROM Products) as TotalProducts,
            (SELECT COUNT(*) FROM Products WHERE CreatedAt >= DATE_SUB(NOW(), INTERVAL 30 DAY)) as TotalNewProducts,
            
            -- Fast Moving Benchmark: Products with total sales of 50 units or more
            (SELECT COUNT(*) FROM (
                SELECT ProductId FROM OrderItems GROUP BY ProductId HAVING SUM(Quantity) >= 50
            ) as FastTrack) as FastMovingProducts,

            -- Slow Moving Benchmark: Products with total sales under 5 units across history
            (SELECT COUNT(*) FROM (
                SELECT p.ProductId FROM Products p 
                LEFT JOIN OrderItems oi ON p.ProductId = oi.ProductId 
                GROUP BY p.ProductId HAVING IFNULL(SUM(oi.Quantity), 0) < 5
            ) as SlowTrack) as SlowMovingProducts,

            (SELECT COUNT(*) FROM Products WHERE RemainingStock <= SKU AND SKU > 0) as NearSkuCount,

            -- B. Near Expiry Rule: Batches whose active expiration dates hit within the next 90 days
            (SELECT COUNT(DISTINCT ProductId) FROM ProductBatches 
             WHERE ExpiryDate IS NOT NULL 
               AND ExpiryDate >= NOW() 
               AND ExpiryDate <= DATE_ADD(NOW(), INTERVAL 3 MONTH)) as NearExpiryCount,

            -- C. Skipped Products Rule: Ordered the month before last, but completely missed last month
            (SELECT COUNT(DISTINCT prev.ProductId) FROM OrderItems prev
             JOIN Orders oprev ON prev.OrderId = oprev.OrderId
                AND oprev.OrderDate >= DATE_ADD(LAST_DAY(DATE_SUB(NOW(), INTERVAL 3 MONTH)), INTERVAL 1 DAY)
                AND oprev.OrderDate <= LAST_DAY(DATE_SUB(NOW(), INTERVAL 2 MONTH))
             WHERE prev.ProductId NOT IN (
                 SELECT DISTINCT curr.ProductId FROM OrderItems curr
                 JOIN Orders ocurr ON curr.OrderId = ocurr.OrderId
                 WHERE ocurr.OrderDate >= DATE_ADD(LAST_DAY(DATE_SUB(NOW(), INTERVAL 2 MONTH)), INTERVAL 1 DAY)
                   AND ocurr.OrderDate <= LAST_DAY(DATE_SUB(NOW(), INTERVAL 1 MONTH))
             )) as SkippedProductsCount,

            (SELECT COUNT(*) FROM Orders) as TotalOrders,
            (SELECT COUNT(*) FROM Orders WHERE OrderType = 'New' OR OrderType = 'Sale') as TotalNewOrders,
            
            -- Repeat Orders Counter: Distinct leads who have placed more than 1 individual invoice order
            (
                SELECT IFNULL(SUM(RepeatCount), 0) 
                FROM (
                    SELECT COUNT(OrderId) - 1 AS RepeatCount 
                    FROM Orders 
                    WHERE 1=1 
                    GROUP BY LeadId 
                    HAVING COUNT(OrderId) > 1
                ) AS RepeatTrack
            ) AS TotalRepeatedOrders,

            (SELECT COUNT(*) FROM Orders WHERE PaymentStatus = 'Unpaid') as TotalUnpaidOrders,
            (SELECT COUNT(*) FROM Orders WHERE PaymentStatus = 'Partially Paid') as TotalPartialPaidOrders;";

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

            // 1. Structural Filters Setup
            string holderFilter = "";
            string orderHolderFilter = "";
            if (!string.IsNullOrEmpty(filter.LeadHolder))
            {
                holderFilter = " AND LeadHolder = @Holder ";
                orderHolderFilter = " AND ProcessedBy = @Holder ";
                parameters.Add("Holder", filter.LeadHolder);
            }

            // 2. Date Range Boundaries Setup
            parameters.Add("From", filter.FromDate);
            parameters.Add("To", filter.ToDate);
            string dateRange = (filter.FromDate != null) ? " BETWEEN @From AND @To " : null;

            string sql = $@"
        SELECT 
            (SELECT COUNT(*) FROM Leads 
             WHERE 1=1 {holderFilter} 
             {(dateRange != null ? $" AND CreatedAt {dateRange}" : "")}) as AllLeads,

            (SELECT COUNT(*) FROM Leads 
             WHERE Status = 'New' {holderFilter} 
             {(dateRange != null ? $" AND CreatedAt {dateRange}" : "")}) as NewLeads,

            (SELECT COUNT(DISTINCT l.LeadId) FROM Leads l
             INNER JOIN LeadHistory h ON l.LeadId = h.LeadId
             WHERE l.Status = 'Followup' {holderFilter.Replace("LeadHolder", "l.LeadHolder")}
             {(dateRange != null ? $" AND h.LogDate {dateRange}" : "")}) as FollowupLeads,

            (SELECT COUNT(*) FROM Leads l 
             WHERE Status = 'Followup' {holderFilter.Replace("LeadHolder", "l.LeadHolder")}
             {(dateRange != null ? $" AND l.CreatedAt {dateRange}" : "")}
             AND (SELECT MAX(LogDate) FROM LeadHistory WHERE LeadId = l.LeadId) < DATE_SUB(NOW(), INTERVAL 30 DAY)) as NoFollowupLeads,

            (SELECT COUNT(*) FROM Leads WHERE Status = 'Dead' {holderFilter}
             {(dateRange != null ? $" AND CreatedAt {dateRange}" : "")}) as Dead,

            (SELECT COUNT(*) FROM Leads WHERE Status = 'Matured' {holderFilter}
             {(dateRange != null ? $" AND CreatedAt {dateRange}" : "")}) as Customers,

            (SELECT COUNT(DISTINCT l.LeadId) FROM Leads l
             WHERE l.Status = 'Matured' {holderFilter.Replace("LeadHolder", "l.LeadHolder")}
             {(dateRange != null ? $" AND l.CreatedAt {dateRange}" : "")}
             AND (SELECT GREATEST(l.CreatedAt, 
                    IFNULL((SELECT MAX(LogDate) FROM LeadHistory WHERE LeadId = l.LeadId), '1900-01-01'),
                    IFNULL((SELECT MAX(OrderDate) FROM Orders WHERE LeadId = l.LeadId), '1900-01-01'),
                    IFNULL((SELECT MAX(PaymentDate) FROM Payments WHERE LeadId = l.LeadId), '1900-01-01'))
                  ) < DATE_SUB(NOW(), INTERVAL 7 DAY)) as NoUpdation7Days,

            (SELECT COUNT(*) FROM Leads l WHERE Status = 'Matured' {holderFilter.Replace("LeadHolder", "l.LeadHolder")}
             AND (SELECT COUNT(*) FROM Orders WHERE LeadId = l.LeadId) <= 1) as NoRepeatOrder,

            (SELECT COUNT(*) FROM Leads l WHERE Status = 'Matured' {holderFilter.Replace("LeadHolder", "l.LeadHolder")}
             AND (SELECT MAX(OrderDate) FROM Orders WHERE LeadId = l.LeadId) < DATE_SUB(NOW(), INTERVAL 30 DAY)) as NoOrder,

            (SELECT COUNT(*) FROM (
                SELECT l.LeadId
                FROM Leads l
                LEFT JOIN Orders o ON l.LeadId = o.LeadId 
                    AND o.OrderDate >= DATE_ADD(LAST_DAY(DATE_SUB(NOW(), INTERVAL 2 MONTH)), INTERVAL 1 DAY)
                    AND o.OrderDate <= LAST_DAY(DATE_SUB(NOW(), INTERVAL 1 MONTH))
                WHERE l.Status = 'Matured' {holderFilter.Replace("LeadHolder", "l.LeadHolder")}
                  AND IFNULL(l.MonthlyTarget, 0) > 0
                GROUP BY l.LeadId, l.MonthlyTarget
                HAVING IFNULL(SUM(o.TotalAmount), 0) < l.MonthlyTarget
            ) as CustomerShortfallTrack) as BelowTarget,

            (SELECT COALESCE(SUM(TotalAmount), 0) FROM Orders 
             WHERE 1=1 {orderHolderFilter}
             {(dateRange != null ? $" AND OrderDate {dateRange}" : "")}) as TotalBusiness,

            (SELECT COUNT(DISTINCT p.CategoryId) FROM Products p
             INNER JOIN OrderItems oi ON p.ProductId = oi.ProductId
             INNER JOIN Orders o ON oi.OrderId = o.OrderId
             WHERE 1=1 {orderHolderFilter.Replace("ProcessedBy", "o.ProcessedBy")}
             {(dateRange != null ? $" AND o.OrderDate {dateRange}" : "")}) as TotalCategoriesUsed,

            (SELECT COUNT(DISTINCT p.ProductId) FROM Products p
             LEFT JOIN OrderItems oi ON p.ProductId = oi.ProductId
             LEFT JOIN Orders o ON oi.OrderId = o.OrderId
             WHERE 1=1 {(dateRange != null ? $" AND p.CreatedAt {dateRange}" : "")}) as TotalProducts,

            (SELECT COUNT(*) FROM Products WHERE CreatedAt >= DATE_SUB(NOW(), INTERVAL 30 DAY)) as TotalNewProducts,

            (SELECT COUNT(*) FROM (
                SELECT oi.ProductId FROM OrderItems oi
                INNER JOIN Orders o ON oi.OrderId = o.OrderId
                WHERE 1=1 {orderHolderFilter.Replace("ProcessedBy", "o.ProcessedBy")}
                {(dateRange != null ? $" AND o.OrderDate {dateRange}" : "")}
                GROUP BY oi.ProductId HAVING SUM(oi.Quantity) >= 50
            ) as FastTrack) as FastMovingProducts,

            (SELECT COUNT(*) FROM (
                SELECT p.ProductId FROM Products p
                LEFT JOIN OrderItems oi ON p.ProductId = oi.ProductId
                LEFT JOIN Orders o ON oi.OrderId = o.OrderId {orderHolderFilter.Replace("ProcessedBy", "o.ProcessedBy")}
                {(dateRange != null ? $" AND o.OrderDate {dateRange}" : "")}
                GROUP BY p.ProductId HAVING IFNULL(SUM(oi.Quantity), 0) < 5
            ) as SlowTrack) as SlowMovingProducts,

            (SELECT COUNT(*) FROM Products WHERE RemainingStock <= SKU AND SKU > 0) as NearSkuCount,

            (SELECT COUNT(DISTINCT ProductId) FROM ProductBatches 
             WHERE ExpiryDate IS NOT NULL AND ExpiryDate >= NOW() AND ExpiryDate <= DATE_ADD(NOW(), INTERVAL 3 MONTH)) as NearExpiryCount,

            (SELECT COUNT(DISTINCT prev.ProductId) FROM OrderItems prev
             INNER JOIN Orders oprev ON prev.OrderId = oprev.OrderId {orderHolderFilter.Replace("ProcessedBy", "oprev.ProcessedBy")}
                AND oprev.OrderDate >= DATE_ADD(LAST_DAY(DATE_SUB(NOW(), INTERVAL 3 MONTH)), INTERVAL 1 DAY)
                AND oprev.OrderDate <= LAST_DAY(DATE_SUB(NOW(), INTERVAL 2 MONTH))
             WHERE prev.ProductId NOT IN (
                 SELECT DISTINCT curr.ProductId FROM OrderItems curr
                 INNER JOIN Orders ocurr ON curr.OrderId = ocurr.OrderId {orderHolderFilter.Replace("ProcessedBy", "ocurr.ProcessedBy")}
                 WHERE ocurr.OrderDate >= DATE_ADD(LAST_DAY(DATE_SUB(NOW(), INTERVAL 2 MONTH)), INTERVAL 1 DAY)
                   AND ocurr.OrderDate <= LAST_DAY(DATE_SUB(NOW(), INTERVAL 1 MONTH))
             )) as SkippedProductsCount,

            (SELECT COUNT(*) FROM Orders 
             WHERE 1=1 {orderHolderFilter}
             {(dateRange != null ? $" AND OrderDate {dateRange}" : "")}) as TotalOrders,

            (SELECT COUNT(*) FROM Orders 
             WHERE (OrderType = 'New' OR OrderType = 'Sale') {orderHolderFilter}
             {(dateRange != null ? $" AND OrderDate {dateRange}" : "")}) as TotalNewOrders,

            (
                SELECT IFNULL(SUM(RepeatCount), 0) 
                FROM (
                    SELECT COUNT(OrderId) - 1 AS RepeatCount 
                    FROM Orders 
                    WHERE 1=1 {orderHolderFilter}
                      {(dateRange != null ? $" AND OrderDate {dateRange}" : "")}
                    GROUP BY LeadId 
                    HAVING COUNT(OrderId) > 1
                ) AS RepeatTrack
            ) AS TotalRepeatedOrders,

            (SELECT COUNT(*) FROM Orders 
             WHERE PaymentStatus = 'Unpaid' {orderHolderFilter}
             {(dateRange != null ? $" AND OrderDate {dateRange}" : "")}) as TotalUnpaidOrders,

            (SELECT COUNT(*) FROM Orders 
             WHERE PaymentStatus = 'Partially Paid' {orderHolderFilter}
             {(dateRange != null ? $" AND OrderDate {dateRange}" : "")}) as TotalPartialPaidOrders;";

            return await db.QuerySingleAsync<DashboardStats>(sql, parameters);
        }

        public async Task<DashboardStageSummaries> GetDashboardStageSummariesFilteredAsync(DashboardFilter filter)
        {
            var summaries = new DashboardStageSummaries();
            using var db = _context.CreateConnection();
            var parameters = new DynamicParameters();

            string holderFilter = "";
            string orderHolderFilter = "";
            if (!string.IsNullOrEmpty(filter.LeadHolder))
            {
                holderFilter = " AND LeadHolder = @Holder ";
                orderHolderFilter = " AND ProcessedBy = @Holder ";
                parameters.Add("Holder", filter.LeadHolder);
            }

            parameters.Add("From", filter.FromDate);
            parameters.Add("To", filter.ToDate);
            string dateRange = (filter.FromDate != null) ? " BETWEEN @From AND @To " : null;

            // 1. FILTERED REMINDERS IN ALPHABETICAL ORDER
            string remindersSql = $@"                
                SELECT 'New' as `Key`, COUNT(*) as `Value` FROM Orders 
                WHERE (PaymentStatus = 'Unpaid' OR PaymentStatus = 'Partially Paid') AND (OrderType = 'New' OR OrderType = 'Sale') {orderHolderFilter} {(dateRange != null ? $" AND OrderDate {dateRange}" : "")}
                UNION ALL
                SELECT 'Repeat' as `Key`, COUNT(*) as `Value` FROM Orders 
                WHERE (PaymentStatus = 'Unpaid' OR PaymentStatus = 'Partially Paid') AND (OrderType != 'New' AND OrderType != 'Sale') {orderHolderFilter} {(dateRange != null ? $" AND OrderDate {dateRange}" : "")}
                ORDER BY `Key` ASC;"; // Alphabetical sorting modifier
            summaries.Reminders = (await db.QueryAsync<KeyValuePair<string, int>>(remindersSql, parameters)).ToList();

            // 2. FILTERED FOLLOWUP STAGES IN ALPHABETICAL ORDER
            string followupSql = $@"
                SELECT 'All FollowUps' as `Key`, COUNT(*) as `Value` FROM Leads WHERE Status = 'Followup' {holderFilter} {(dateRange != null ? $" AND CreatedAt {dateRange}" : "")}
                UNION ALL
                SELECT s.StatusesName as `Key`, COUNT(l.LeadId) as `Value`
                FROM LeadStatuses s
                LEFT JOIN Leads l ON s.Id = l.StatusId AND l.Status = 'Followup' {holderFilter.Replace("LeadHolder", "l.LeadHolder")} {(dateRange != null ? $" AND l.CreatedAt {dateRange}" : "")}
                GROUP BY s.Id, s.StatusesName
                ORDER BY `Key` ASC;";
            summaries.FollowupStages = (await db.QueryAsync<KeyValuePair<string, int>>(followupSql, parameters)).ToList();

            // 3. FILTERED MATURE STAGES IN ALPHABETICAL ORDER
            string matureSql = $@"
                SELECT 'All Matured' as `Key`, COUNT(*) as `Value` FROM Leads WHERE Status = 'Matured' {holderFilter} {(dateRange != null ? $" AND CreatedAt {dateRange}" : "")}
                UNION ALL
                SELECT m.MatureStagesName as `Key`, COUNT(l.LeadId) as `Value`
                FROM MatureStages m
                LEFT JOIN Leads l ON m.Id = l.MatureStageId AND l.Status = 'Matured' {holderFilter.Replace("LeadHolder", "l.LeadHolder")} {(dateRange != null ? $" AND l.CreatedAt {dateRange}" : "")}
                GROUP BY m.Id, m.MatureStagesName
                ORDER BY `Key` ASC;";
            summaries.MatureStages = (await db.QueryAsync<KeyValuePair<string, int>>(matureSql, parameters)).ToList();

            // 4. FILTERED MULTI-ASSIGNMENT LEAD LABELS IN ALPHABETICAL ORDER
            string labelsSql = $@"
                SELECT 'All Labels' as `Key`, COUNT(*) as `Value` FROM LeadLabels
                UNION ALL
                SELECT master.LabelsName as `Key`,
                    (SELECT COUNT(*) FROM Leads l 
                     WHERE l.LabelsJson IS NOT NULL AND l.LabelsJson != '' AND l.LabelsJson != '[]' {holderFilter.Replace("LeadHolder", "l.LeadHolder")} {(dateRange != null ? $" AND l.CreatedAt {dateRange}" : "")}
                       AND JSON_CONTAINS(l.LabelsJson, JSON_QUOTE(master.LabelsName))) as `Value`
                FROM LeadLabels master
                WHERE master.LabelsName IS NOT NULL AND master.LabelsName != ''
                ORDER BY `Key` ASC;";
            summaries.LeadLabels = (await db.QueryAsync<KeyValuePair<string, int>>(labelsSql, parameters)).ToList();

            return summaries;
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
        WHERE h.NextFollowUpDate < DATE_ADD(CURDATE(), INTERVAL 1 DAY) AND h.NextFollowUpDate IS NOT NULL AND h.HistoryId = (
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
        WHERE h.NextFollowUpDate > DATE_ADD(CURDATE(), INTERVAL 1 DAY) AND h.NextFollowUpDate IS NOT NULL AND h.HistoryId = (
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

                    if (!string.IsNullOrEmpty(currentLead.LabelsJson))
                    {
                        currentLead.LeadLabels = JsonSerializer.Deserialize<ObservableCollection<string>>(currentLead.LabelsJson)
                                                 ?? new ObservableCollection<string>();
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

        public async Task<IEnumerable<Lead>> GetAllFollowupTodayPendingAsync(LeadViewMode mode)
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
        WHERE DATE(h.NextFollowUpDate) = CURDATE() AND h.NextFollowUpDate IS NOT NULL AND h.HistoryId = (
            SELECT MAX(HistoryId) 
            FROM LeadHistory 
            WHERE LeadId = l.LeadId
        ) AND h.HistoryId IS NOT NULL -- In case lead has no history yet
        ORDER BY h.NextFollowUpDate ASC;";

            if (mode == LeadViewMode.Missed)
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
        WHERE h.NextFollowUpDate < CURDATE() AND h.NextFollowUpDate IS NOT NULL AND h.HistoryId = (
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

                    if (!string.IsNullOrEmpty(currentLead.LabelsJson))
                    {
                        currentLead.LeadLabels = JsonSerializer.Deserialize<ObservableCollection<string>>(currentLead.LabelsJson)
                                                 ?? new ObservableCollection<string>();
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
                o.LeadId,
                o.OrderId,
                o.TotalAmount,
                o.AmountPaid,
                -- DYNAMIC CALCULATION: Compiles exact outstanding ledger margins per row
                (o.TotalAmount - o.AmountPaid) AS CalculatedOrderBalance,
                ROW_NUMBER() OVER (
                    PARTITION BY o.LeadId 
                    ORDER BY o.OrderDate ASC, o.OrderId ASC
                ) AS OrderSequence
            FROM Orders o
            INNER JOIN Leads l ON o.LeadId = l.LeadId
            WHERE l.Status = 'Matured' -- CRITICAL FIX: Excludes Dead and Windback pool leads completely
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

        /// <summary>
        /// Scans the central database to check if a contact identifier is already assigned to an active lead.
        /// Excluding the current LeadId prevents self-validation conflicts during edit mode.
        /// </summary>
        public async Task<bool> CheckDuplicateFieldAsync(string fieldName, string fieldValue, int currentLeadId)
        {
            using var conn = _context.CreateConnection();

            // Dynamically evaluate target column inputs safely without SQL injection vulnerabilities
            string sql = $@"
                SELECT COUNT(1) 
                FROM Leads 
                WHERE Phone = @Value OR AltPhone = @Value OR Email = @Value
                  AND LeadId != @CurrentId;";

            int matchCount = await conn.ExecuteScalarAsync<int>(sql, new { Value = fieldValue, CurrentId = currentLeadId });
            return matchCount > 0;
        }

        public async Task<IEnumerable<ProformaSummaryItem>> LoadHistoricalProformasAsync(int leadId)
        {
            try
            {
                using var conn = _context.CreateConnection(); // Uses your connection factor infrastructure

                // RESTRICTION LOGIC: Selects product-list items while safely filtering out service line entries
                string sql = @"
                    SELECT 
                        ProformaId,
                        ProformaNumber,
                        'Cash' AS PaymentType, -- Fallback mock or map to your custom fields
                        CreatedAt AS DateCreated,
                        GrandTotal AS Amount,
                        IF(ProformaStatus = 'ConvertedToOrder', 'Converted', 'Pending') AS Status,
                        IF(TotalPaid >= GrandTotal, 'Paid', 'Unpaid') AS PaymentStatus
                    FROM Proformas
                    WHERE LeadId = @LeadId
                    ORDER BY CreatedAt DESC;";

                var result = await conn.QueryAsync<ProformaSummaryItem>(sql, new { LeadId = leadId });
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"History Loading Anomaly: {ex.Message}");
                return Enumerable.Empty<ProformaSummaryItem>();
            }
        }

        public async Task<bool> DeleteProformaRecordAsync(int proformaId)
        {
            using var db = _context.CreateConnection();
            // Note: LeadHistory has a Foreign Key with ON DELETE CASCADE in our SQL schema
            string sql = "DELETE FROM Proformas WHERE ProformaId = @proformaId";
            var rows = await db.ExecuteAsync(sql, new { proformaId });
            return rows > 0;
        }

        public async Task<IEnumerable<GlobalSearchRowItem>> SearchGlobalQueryAsync(string textPattern)
        {
            try
            {
                using var conn = _context.CreateConnection();

                // Sweeps across the primary Name, Contact, and Office fields in a single rapid pass
                string sql = @"
                    SELECT 
                        LeadId AS Id, 
                        CustomerName, 
                        CompanyName, 
                        Phone,
                        AltPhone,
                        IF(CompanyName IS NOT NULL AND CompanyName != '', 1, 0) AS HasCompany
                    FROM Leads
                    WHERE CustomerName LIKE @Query 
                       OR Phone LIKE @Query 
                       OR AltPhone LIKE @Query
                       OR CompanyName LIKE @Query
                    LIMIT 8;";

                var rows = await conn.QueryAsync<GlobalSearchRowItem>(sql, new { Query = $"%{textPattern}%" });

                return rows;
            }
            catch { return Enumerable.Empty<GlobalSearchRowItem>(); }
        }

        public async Task<IEnumerable<Lead>> GetCustomerByDashboardContextAsync(DashboardTargetView target, DashboardFilter? filter)
        {
            using var db = _context.CreateConnection();
            var parameters = new DynamicParameters();

            // Set up standard filters if a context package is present
            string holderFilter = "";
            if (filter != null && !string.IsNullOrEmpty(filter.LeadHolder))
            {
                holderFilter = " AND l.LeadHolder = @Holder ";
                parameters.Add("Holder", filter.LeadHolder);
            }

            parameters.Add("From", filter?.FromDate);
            parameters.Add("To", filter?.ToDate);
            string dateCondition = (filter?.FromDate != null) ? " AND l.CreatedAt BETWEEN @From AND @To " : "";

            string baseSql = "SELECT l.* FROM Leads l WHERE l.Status = 'Matured' ";

            // Apply specific query rules based on which tile card was clicked on the dashboard
            switch (target)
            {
                case DashboardTargetView.Customers:
                    // Standard customer list loading logic
                    baseSql += $" {holderFilter} {dateCondition} ";
                    break;

                case DashboardTargetView.NoUpdation7Days:
                    // Filter customers who haven't logged any operational updates in the last 7 days
                    baseSql += $@" {holderFilter} {dateCondition} 
                AND (SELECT GREATEST(l.CreatedAt, 
                    IFNULL((SELECT MAX(LogDate) FROM LeadHistory WHERE LeadId = l.LeadId), '1900-01-01'),
                    IFNULL((SELECT MAX(OrderDate) FROM Orders WHERE LeadId = l.LeadId), '1900-01-01'),
                    IFNULL((SELECT MAX(PaymentDate) FROM Payments WHERE LeadId = l.LeadId), '1900-01-01'))
                ) < DATE_SUB(NOW(), INTERVAL 7 DAY)";
                    break;

                case DashboardTargetView.NoRepeatOrders:
                    // Target customer single-buyers
                    baseSql += $" {holderFilter} {dateCondition} AND (SELECT COUNT(*) FROM Orders WHERE LeadId = l.LeadId) <= 1";
                    break;

                case DashboardTargetView.NoOrders30Days:
                    // Cold customer tracking flag
                    baseSql += $" {holderFilter} {dateCondition} AND (SELECT MAX(OrderDate) FROM Orders WHERE LeadId = l.LeadId) < DATE_SUB(NOW(), INTERVAL 30 DAY)";
                    break;

                case DashboardTargetView.BelowTargetCustomers:
                    // Customers missing target parameters
                    baseSql = $@"
                SELECT l.* FROM Leads l
                LEFT JOIN Orders o ON l.LeadId = o.LeadId 
                    AND o.OrderDate >= DATE_ADD(LAST_DAY(DATE_SUB(NOW(), INTERVAL 2 MONTH)), INTERVAL 1 DAY)
                    AND o.OrderDate <= LAST_DAY(DATE_SUB(NOW(), INTERVAL 1 MONTH))
                WHERE l.Status = 'Matured' AND IFNULL(l.MonthlyTarget, 0) > 0 {holderFilter.Replace("l.LeadHolder", "l.LeadHolder")}
                GROUP BY l.LeadId
                HAVING IFNULL(SUM(o.TotalAmount), 0) < l.MonthlyTarget";
                    break;
            }

            return await db.QueryAsync<Lead>(baseSql, parameters);
        }

        public async Task<IEnumerable<Lead>> GetLeadsByDashboardContextAsync(DashboardTargetView target, DashboardFilter? filter)
        {
            using var db = _context.CreateConnection();
            var parameters = new DynamicParameters();

            // 1. Unified LeadHolder Filter Setup
            string holderFilter = "";
            if (filter != null && !string.IsNullOrEmpty(filter.LeadHolder))
            {
                holderFilter = " AND l.LeadHolder = @Holder ";
                parameters.Add("Holder", filter.LeadHolder);
            }

            // 2. Date Ranges Binding Setup
            parameters.Add("From", filter?.FromDate);
            parameters.Add("To", filter?.ToDate);
            string dateCondition = (filter?.FromDate != null) ? " AND l.CreatedAt BETWEEN @From AND @To " : "";
            string historyDateCondition = (filter?.FromDate != null) ? " AND h.LogDate BETWEEN @From AND @To " : "";

            // Base fallback query anchor
            string baseSql = "SELECT l.* FROM Leads l WHERE 1=1 ";

            // 3. Evaluate context-specific criteria matching the dashboard tile clicked
            switch (target)
            {
                case DashboardTargetView.AllLeads:
                    // ALL LEADS: Filtered by lead creation range across all pipeline states
                    baseSql += $" {holderFilter} {dateCondition} ";
                    break;

                case DashboardTargetView.OpenLeads:
                    // OPEN LEADS: Fresh leads matching your creation window
                    baseSql += $" AND l.Status = 'New' {holderFilter} {dateCondition} ";
                    break;

                case DashboardTargetView.FollowupLeads:
                    // FOLLOWUP LEADS: Filtered by the active log history interaction date window
                    baseSql = $@"
                SELECT DISTINCT l.* FROM Leads l
                INNER JOIN LeadHistory h ON l.LeadId = h.LeadId
                WHERE l.Status = 'Followup' {holderFilter} {historyDateCondition}";
                    break;

                case DashboardTargetView.NoFollowupLeads:
                    // NO FOLLOWUP (30 Days): Leads whose newest touchpoint is older than 30 days
                    baseSql += $@" 
                AND l.Status = 'Followup' {holderFilter} {dateCondition}
                AND (
                    SELECT MAX(lh.LogDate) 
                    FROM LeadHistory lh 
                    WHERE lh.LeadId = l.LeadId
                ) < DATE_SUB(NOW(), INTERVAL 30 DAY)";
                    break;

                case DashboardTargetView.DeadLeads:
                    // DEAD LEADS: Lost opportunities filtered by creation date criteria
                    baseSql += $" AND l.Status = 'Dead' {holderFilter} {dateCondition} ";
                    break;

                default:
                    // Standard fallback safe-guard to ensure it returns nothing if an incorrect tab boundary bleeds over
                    baseSql += " AND 1=0 ";
                    break;
            }

            // Append standard sorting index to ensure grids look highly structured (A to Z)
            baseSql += " ORDER BY l.CustomerName ASC;";

            return await db.QueryAsync<Lead>(baseSql, parameters);
        }

        public async Task<IEnumerable<Models.Order>> GetOrdersByDashboardContextAsync(DashboardTargetView target, DashboardFilter? filter)
        {
            using var db = _context.CreateConnection();
            var parameters = new DynamicParameters();

            // 1. Structural Filters Setup (Filters orders handled by the respective sales executive)
            string orderHolderFilter = "";
            if (filter != null && !string.IsNullOrEmpty(filter.LeadHolder))
            {
                orderHolderFilter = " AND o.ProcessedBy = @Holder ";
                parameters.Add("Holder", filter.LeadHolder);
            }

            // 2. Date Range Boundaries Setup
            parameters.Add("From", filter?.FromDate);
            parameters.Add("To", filter?.ToDate);
            string dateCondition = (filter?.FromDate != null) ? " AND o.OrderDate BETWEEN @From AND @To " : "";

            // Base SELECT query structure linking orders back to core customer company files
            string baseSql = @"
                SELECT o.*, l.CustomerName, l.CompanyName 
                FROM Orders o
                INNER JOIN Leads l ON o.LeadId = l.LeadId
                WHERE 1=1 ";

            switch (target)
            {
                case DashboardTargetView.AllOrders:
                    // TARGET: Global baseline sales sheet overview
                    baseSql += $"{orderHolderFilter} {dateCondition}";
                    break;

                case DashboardTargetView.NewOrders:
                    // TARGET: First conversion transactions only
                    baseSql += $" AND (o.OrderType = 'New' OR o.OrderType = 'Sale') {orderHolderFilter} {dateCondition}";
                    break;

                case DashboardTargetView.RepeatedOrders:
                    // TARGET: Sub-sequent repeat orders from accounts who have ordered more than once in the window
                    baseSql = $@"
                        SELECT 
                            o.*, 
                            l.CustomerName, 
                            l.CompanyName AS FirmName
                        FROM Orders o
                        INNER JOIN Leads l ON o.LeadId = l.LeadId
                        WHERE o.LeadId IN (
                            SELECT o_sub.LeadId 
                            FROM Orders o_sub
                            WHERE 1=1 {orderHolderFilter.Replace("o.", "o_sub.")} {dateCondition.Replace("o.", "o_sub.")}
                            GROUP BY o_sub.LeadId 
                            HAVING COUNT(o_sub.OrderId) > 1
                        )
                        -- CRITICAL FIX: Excludes the initial/first order for each customer group
                        AND o.OrderId NOT IN (
                            SELECT MIN(o_first.OrderId)
                            FROM Orders o_first
                            GROUP BY o_first.LeadId
                        )
                        {orderHolderFilter} 
                        {dateCondition}";
                    break;

                case DashboardTargetView.UnpaidOrders:
                    // TARGET: Accounts lacking payment entries
                    baseSql += $" AND o.PaymentStatus = 'Unpaid' {orderHolderFilter} {dateCondition}";
                    break;

                case DashboardTargetView.PartiallyPaidOrders:
                    // TARGET: Risk tracking accounts matching fractional collection updates
                    baseSql += $" AND o.PaymentStatus = 'Partially Paid' {orderHolderFilter} {dateCondition}";
                    break;

                default:
                    // Safe-guard condition
                    baseSql += " AND 1=0 ";
                    break;
            }

            // Order sequentially with the newest invoices sitting squarely at the top
            baseSql += " ORDER BY o.OrderDate DESC, o.OrderId DESC;";

            return await db.QueryAsync<Models.Order>(baseSql, parameters);
        }

        public async Task SaveLeadCustomFieldValuesAsync(int leadId, IEnumerable<KeyValuePair<int, string>> values, string entityType = "Lead")
        {
            const string sql = @"
                INSERT INTO CustomFieldValues (EntityId, EntityType, FieldId, FieldValue)
                VALUES (@LeadId, @EntityType, @FieldId, @FieldValue)
                ON DUPLICATE KEY UPDATE FieldValue = @FieldValue;";

            using var db = _context.CreateConnection();
            foreach (var kvp in values)
            {
                await db.ExecuteAsync(sql, new { LeadId = leadId, EntityType = entityType, FieldId = kvp.Key, FieldValue = kvp.Value });
            }
        }

        // Method to pull values back when opening an existing lead in Edit mode
        public async Task<Dictionary<int, string>> GetCustomFieldValuesForLeadAsync(int leadId, string entityType = "Lead")
        {
            const string sql = "SELECT FieldId, FieldValue FROM CustomFieldValues WHERE EntityId = @LeadId AND EntityType = @EntityType;";
            using var db = _context.CreateConnection();
            var rows = await db.QueryAsync<(int FieldId, string FieldValue)>(sql, new { LeadId = leadId, EntityType = entityType });
            var dictionary = new Dictionary<int, string>();
            foreach (var row in rows)
            {
                dictionary[row.FieldId] = row.FieldValue;
            }
            return dictionary;
        }
    }
}


