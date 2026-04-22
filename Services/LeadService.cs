using CallMan.Data;
using CallMan.Models;
using Dapper;
using System;
using System.Collections.Generic;
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
            }

            return leads;
        }

        public async Task SaveLeadAsync(Lead lead, string initialLog)
        {
            using var db = _context.CreateConnection();
            // Serialize dynamic fields to JSON
            lead.MetadataJson = JsonSerializer.Serialize(lead.CustomFields);

            string sql = @"INSERT INTO Leads (CustomerName, Email, Phone, Status, MetadataJson, 
               CompanyName, AddressLine, City, District, State, Pincode, Country, CreatedAt) 
               VALUES (@CustomerName, @Email, @Phone, @Status, @MetadataJson, 
               @CompanyName, @AddressLine, @City, @District, @State, @Pincode, @Country, NOW());
            SELECT LAST_INSERT_ID();";

            int newId = await db.ExecuteScalarAsync<int>(sql, lead);

            // Save initial history entry
            await AddHistoryAsync(newId, initialLog, null, "System");
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

            string sql = @"UPDATE Leads SET 
                    CustomerName = @CustomerName, Email = @Email, Phone = @Phone, 
                    Status = @Status, CompanyName = @CompanyName, AddressLine = @AddressLine, 
                    City = @City, District = @District, State = @State, 
                    Pincode = @Pincode, MetadataJson = @MetadataJson 
                   WHERE LeadId = @LeadId";

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

        public async Task AddFollowUpAsync(int leadId, string message, DateTime? nextDate)
        {
            using var db = _context.CreateConnection();
            string sql = @"INSERT INTO LeadHistory (LeadId, Message, NextFollowUpDate, UpdatedBy) 
                   VALUES (@leadId, @message, @nextDate, 'Admin')";
            await db.ExecuteAsync(sql, new { leadId, message, nextDate });

            // Also update the main Lead status to 'Follow-up' automatically
            await db.ExecuteAsync("UPDATE Leads SET Status = 'Follow-up' WHERE LeadId = @leadId", new { leadId });
        }

        public async Task<IEnumerable<Lead>> GetAllLeadsWithLatestUpdateAsync()
        {
            using var db = _context.CreateConnection();

            // Complex query: Select Lead info, and join with ONLY the newest History entry
            string sql = @"
        SELECT 
            l.*, 
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
            db.Open();
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
            db.Open();
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

        public async Task<bool> MatureLeadWithDoubleHistoryAsync(Lead lead, PaymentEntry payment, LeadHistoryEntry followUp)
        {
            using var db = _context.CreateConnection();
            db.Open();
            using var trans = db.BeginTransaction();

            try
            {
                // 1. Update Lead Status
                await db.ExecuteAsync("UPDATE Leads SET Status = 'Matured' WHERE LeadId = @LeadId", new { lead.LeadId }, trans);

                // 2. Record Financial Entry
                string paySql = @"INSERT INTO Payments (LeadId, TotalOrderValue, AmountReceived, Remarks, PaymentDate) 
                          VALUES (@LeadId, @TotalOrderValue, @AmountReceived, @Remarks, NOW())";
                await db.ExecuteAsync(paySql, payment, trans);

                // 3. ENTRY #1: The Maturity Milestone (System Entry)
                string milestoneMsg = $"[MILESTONE] Lead Matured. Order Value: ₹{payment.TotalOrderValue}, Received: ₹{payment.AmountReceived}.";
                string hist1Sql = @"INSERT INTO LeadHistory (LeadId, Message, ActionType, FollowupStage, UpdatedBy, LogDate) 
                            VALUES (@LeadId, @Message, 'System', 'Matured', @UpdatedBy, NOW())";
                await db.ExecuteAsync(hist1Sql, new { lead.LeadId, Message = milestoneMsg, UpdatedBy = followUp.UpdatedBy }, trans);

                // 4. ENTRY #2: The User's Follow-up/Message
                string hist2Sql = @"INSERT INTO LeadHistory (LeadId, Message, ActionType, NextFollowUpDate, FollowupStage, UpdatedBy, LogDate) 
                            VALUES (@LeadId, @Message, @ActionType, @NextFollowUpDate, 'Matured', @UpdatedBy, DATE_ADD(NOW(), INTERVAL 1 SECOND))";
                await db.ExecuteAsync(hist2Sql, followUp, trans);

                trans.Commit();
                return true;
            }
            catch
            {
                trans.Rollback();
                return false;
            }
        }
    }
}
