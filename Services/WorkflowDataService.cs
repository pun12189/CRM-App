using CallMan.Data;
using CallMan.Interfaces;
using CallMan.Models;
using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Services
{
    public class WorkflowDataService : IWorkflowDataService
    {
        private readonly CrmDbContext _db;
        public WorkflowDataService(CrmDbContext db) => _db = db;

        public async Task<IEnumerable<Workflow>> GetAllWorkflowsAsync()
        {
            using var conn = _db.CreateConnection();
            return await conn.QueryAsync<Workflow>("SELECT * FROM Workflows");
        }

        public async Task<bool> SaveWorkflowAsync(Workflow workflow)
        {
            using var conn = _db.CreateConnection();
            // Using WorkflowName as the identifier or a simple insert for new rules
            string sql = @"
        INSERT INTO Workflows (WorkflowName, EventName, InactivityDays, SendEmail, SendWhatsApp, TemplateBody, IsEnabled)
        VALUES (@WorkflowName, @EventName, @InactivityDays, @SendEmail, @SendWhatsApp, @TemplateBody, @IsEnabled)
        ON DUPLICATE KEY UPDATE 
            EventName=@EventName, 
            InactivityDays=@InactivityDays, 
            SendEmail=@SendEmail, 
            SendWhatsApp=@SendWhatsApp, 
            TemplateBody=@TemplateBody, 
            IsEnabled=@IsEnabled";

            return await conn.ExecuteAsync(sql, workflow) > 0;
        }

        public async Task<IEnumerable<WorkflowTag>> GetTagsByEventAsync(string eventName)
        {
            using var conn = _db.CreateConnection();
            return await conn.QueryAsync<WorkflowTag>(
                "SELECT * FROM WorkflowTags WHERE EventName = @eventName", new { eventName });
        }

        public async Task EnqueueActionAsync(int workflowId, int targetId, string targetType)
        {
            using var conn = _db.CreateConnection();
            await conn.ExecuteAsync(@"INSERT INTO WorkflowQueue (WorkflowId, TargetId, TargetType, ScheduledTime) 
                                VALUES (@workflowId, @targetId, @targetType, NOW())",
                                    new { workflowId, targetId, targetType });
        }

        public async Task<IEnumerable<WorkflowQueueItem>> GetPendingQueueAsync()
        {
            using var conn = _db.CreateConnection();
            return await conn.QueryAsync<WorkflowQueueItem>(
                "SELECT * FROM WorkflowQueue WHERE IsProcessed = 0;");
        }

        public async Task MarkAsProcessedAsync(int queueId)
        {
            using var conn = _db.CreateConnection();
            await conn.ExecuteAsync("UPDATE WorkflowQueue SET IsProcessed = 1 WHERE Id = @Id", new { Id = queueId });
        }

        // Fetches workflows where InactivityDays is set (e.g., > 0)
        public async Task<IEnumerable<Workflow>> GetInactivityWorkflowsAsync()
        {
            using var conn = _db.CreateConnection();
            return await conn.QueryAsync<Workflow>(
                "SELECT * FROM Workflows WHERE InactivityDays > 0 AND IsEnabled = 1");
        }

        // Finds customers whose last order was exactly 'days' ago
        public async Task<IEnumerable<dynamic>> GetInactiveCustomersAsync(int days)
        {
            using var conn = _db.CreateConnection();
            string sql = @"
            SELECT c.LeadId, c.CustomerName, c.Phone, c.Email 
            FROM Leads c
            JOIN Orders o ON c.LeadId = o.LeadId
            GROUP BY c.LeadId
            HAVING DATEDIFF(NOW(), MAX(o.OrderDate)) = @days";

            return await conn.QueryAsync<dynamic>(sql, new { days });
        }

        // Prevents spamming the customer by checking if this specific workflow 
        // was already sent to them recently
        public async Task<bool> HasAlreadyReceivedInactivityNotice(int customerId, int workflowId)
        {
            using var conn = _db.CreateConnection();
            string sql = @"
            SELECT COUNT(1) FROM WorkflowLogs 
            WHERE TargetId = @customerId 
            AND WorkflowId = @workflowId 
            AND ExecutedAt > DATE_SUB(NOW(), INTERVAL 30 DAY)";

            return await conn.ExecuteScalarAsync<int>(sql, new { customerId, workflowId }) > 0;
        }
    }
}
