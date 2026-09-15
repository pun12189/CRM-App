using Tijori.Data;
using Tijori.Interfaces;
using Tijori.Models;
using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Services
{
    public class WorkflowDataService : IWorkflowDataService
    {
        private readonly CrmDbContext _db;
        public WorkflowDataService(CrmDbContext db) => _db = db;

        public async Task<IEnumerable<Workflow>> GetAllWorkflowsAsync()
        {
            using var conn = _db.CreateConnection();
            const string sql = "SELECT * FROM workflows ORDER BY CreatedAt DESC";
            return await conn.QueryAsync<Workflow>(sql);
        }

        public async Task<bool> SaveWorkflowAsync(Workflow wf)
        {
            using var conn = _db.CreateConnection();
            const string sql = @"
                INSERT INTO workflows (
                    Id, WorkflowName, EventName, ExecutionDays, IsEnabled,
                    SendWhatsApp, WhatsAppSender, WhatsAppToLead, WhatsAppToUser, WhatsAppMessage,
                    SendEmail, EmailToLead, EmailToUser, EmailMessage,
                    SendNotification, NotificationMessage, CreatedAt
                ) VALUES (
                    @Id, @WorkflowName, @EventName, @ExecutionDays, @IsEnabled,
                    @SendWhatsApp, @WhatsAppSender, @WhatsAppToLead, @WhatsAppToUser, @WhatsAppMessage,
                    @SendEmail, @EmailToLead, @EmailToUser, @EmailMessage,
                    @SendNotification, @NotificationMessage, NOW()
                )
                ON DUPLICATE KEY UPDATE
                    WorkflowName = @WorkflowName,
                    EventName = @EventName,
                    ExecutionDays = @ExecutionDays,
                    IsEnabled = @IsEnabled,
                    SendWhatsApp = @SendWhatsApp,
                    WhatsAppSender = @WhatsAppSender,
                    WhatsAppToLead = @WhatsAppToLead,
                    WhatsAppToUser = @WhatsAppToUser,
                    WhatsAppMessage = @WhatsAppMessage,
                    SendEmail = @SendEmail,
                    EmailToLead = @EmailToLead,
                    EmailToUser = @EmailToUser,
                    EmailMessage = @EmailMessage,
                    SendNotification = @SendNotification,
                    NotificationMessage = @NotificationMessage;";

            return await conn.ExecuteAsync(sql, wf) > 0;
        }

        public async Task<bool> DeleteWorkflowAsync(int id)
        {
            using var conn = _db.CreateConnection();
            return await conn.ExecuteAsync("DELETE FROM workflows WHERE Id = @id", new { id }) > 0;
        }

        public async Task<IEnumerable<WorkflowTag>> GetTagsForEventAsync(string eventName)
        {
            using var conn = _db.CreateConnection();
            const string sql = "SELECT * FROM workflowtags WHERE EventName IN (@eventName, 'All')";
            return await conn.QueryAsync<WorkflowTag>(sql, new { eventName });
        }

        public async Task EnqueueActionAsync(int workflowId, int targetId, string targetType)
        {
            using var conn = _db.CreateConnection();
            const string sql = @"
                INSERT INTO workflowqueue (WorkflowId, TargetId, TargetType, ScheduledTime, IsProcessed, CreatedAt)
                VALUES (@workflowId, @targetId, @targetType, NOW(), 0, NOW());";
            await conn.ExecuteAsync(sql, new { workflowId, targetId, targetType });
        }

        public async Task<IEnumerable<WorkflowQueueItem>> GetPendingQueueAsync()
        {
            using var conn = _db.CreateConnection();
            const string sql = "SELECT * FROM workflowqueue WHERE IsProcessed = 0;";
            return await conn.QueryAsync<WorkflowQueueItem>(sql);
        }

        public async Task MarkAsProcessedAsync(int queueId)
        {
            using var conn = _db.CreateConnection();
            await conn.ExecuteAsync("UPDATE workflowqueue SET IsProcessed = 1 WHERE Id = @queueId", new { queueId });
        }
    }
}
