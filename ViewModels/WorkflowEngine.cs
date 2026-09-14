using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tijori.Core;
using Tijori.Data;
using Tijori.Interfaces;
using Tijori.Models;
using Tijori.Services;

namespace Tijori.ViewModels
{
    public class WorkflowEngine
    {
        private readonly IWorkflowDataService _dataService;
        private readonly EmailService _emailService;
        private readonly LeadService _leadService;
        private readonly NotificationRoutingService _routingService; // 👈 Injected Notification Router
        private readonly CrmDbContext _db;

        public WorkflowEngine(
            IWorkflowDataService dataService,
            EmailService emailService,
            LeadService leadService,
            NotificationRoutingService routingService,
            CrmDbContext db)
        {
            _dataService = dataService;
            _emailService = emailService;
            _leadService = leadService;
            _routingService = routingService;
            _db = db;
        }

        public async Task EnqueueEventAsync(string eventName, int targetId, string targetType)
        {
            var workflows = await _dataService.GetAllWorkflowsAsync();
            var matchedRules = workflows.Where(w => w.EventName == eventName && w.IsEnabled).ToList();

            foreach (var rule in matchedRules)
            {
                await _dataService.EnqueueActionAsync(rule.Id, targetId, targetType);
            }

            // Immediately trigger queue flush for real-time responsiveness
            await ProcessQueueAsync();
        }

        public async Task ProcessQueueAsync()
        {
            var pending = await _dataService.GetPendingQueueAsync();

            foreach (var item in pending)
            {
                try
                {
                    var workflows = await _dataService.GetAllWorkflowsAsync();
                    var rule = workflows.FirstOrDefault(w => w.Id == item.WorkflowId);

                    if (rule != null)
                    {
                        var targetData = await FetchTargetDataAsync(item.TargetId, item.TargetType);
                        if (targetData != null)
                        {
                            await ExecuteRuleActionsAsync(rule, targetData, item.TargetId, item.TargetType);
                        }
                    }
                    await _dataService.MarkAsProcessedAsync(item.Id);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[WORKFLOW ENGINE EXECUTION ERROR]: {ex.Message}");
                }
            }
        }

        private async Task ExecuteRuleActionsAsync(Workflow rule, dynamic data, int targetId, string targetType)
        {
            // 1. WhatsApp Action
            if (rule.SendWhatsApp && !string.IsNullOrWhiteSpace(rule.WhatsAppMessage))
            {
                string msg = FormatTemplate(rule.WhatsAppMessage, data);
                if (rule.WhatsAppToLead && !string.IsNullOrWhiteSpace((string)data.Phone))
                {
                    // Dispatch to WhatsApp Gateway
                }
            }

            // 2. Email Action
            if (rule.SendEmail && !string.IsNullOrWhiteSpace(rule.EmailMessage))
            {
                string body = FormatTemplate(rule.EmailMessage, data);
                if (rule.EmailToLead && !string.IsNullOrWhiteSpace((string)data.Email))
                {
                    await _emailService.SendEmailAsync(data.Email, rule.WorkflowName, body);
                }
            }

            // 3. In-App Windows Toast Notification (via NotificationRoutingService)
            if (rule.SendNotification && !string.IsNullOrWhiteSpace(rule.NotificationMessage))
            {
                string toastContent = FormatTemplate(rule.NotificationMessage, data);

                // Determine recipient agent / assigned user
                string targetUser = null;
                try
                {
                    targetUser = data.AssignedTo ?? data.CreatedBy;
                }
                catch { /* AssignedTo not present on dynamic data */ }

                int leadId = targetType == "Lead" ? targetId : 0;

                var toastRequest = new NewToastRequest
                {
                    EventId = rule.Id,
                    LeadId = leadId,
                    ReminderType = rule.WorkflowName,
                    MessageContent = toastContent,
                    ScheduleTime = DateTime.Now,
                    TargetUser = targetUser, // If null, fires for all / broadcast
                    TargetMachine = null,
                    SenderUser = "Workflow Engine"
                };

                await _routingService.DispatchTargetedToastAsync(toastRequest);
            }
        }

        public async Task CheckTimeBasedSchedulesAsync()
        {
            using var conn = _db.CreateConnection();
            var workflows = await _dataService.GetAllWorkflowsAsync();
            var activeTimeRules = workflows.Where(w => w.IsEnabled).ToList();

            foreach (var rule in activeTimeRules)
            {
                switch (rule.EventName)
                {
                    // 1. Leads not updated for X days
                    case WorkflowEvents.NoUpdationSince when rule.ExecutionDays > 0:
                        {
                            const string sql = @"
                    SELECT l.LeadId, l.CustomerName, l.Phone, l.Email, l.LeadHolder
                    FROM Leads l
                    WHERE DATEDIFF(CURDATE(), l.CreatedAt) = @days
                      AND NOT EXISTS (
                          SELECT 1 FROM workflowqueue wq 
                          WHERE wq.WorkflowId = @wfId 
                            AND wq.TargetId = l.LeadId 
                            AND wq.TargetType = 'Lead'
                            AND DATE(wq.ScheduledTime) = CURDATE()
                      );";

                            var leads = await conn.QueryAsync<dynamic>(sql, new { days = rule.ExecutionDays, wfId = rule.Id });
                            foreach (var lead in leads)
                            {
                                // Enqueue with today's schedule, then process
                                await _dataService.EnqueueActionAsync(rule.Id, (int)lead.LeadId, "Lead");
                            }
                            break;
                        }

                    // 2. Customers with no orders for X days
                    case WorkflowEvents.NoOrderSince when rule.ExecutionDays > 0:
                        {
                            const string sql = @"
                    SELECT l.LeadId, l.CustomerName, l.Phone, l.Email, l.LeadHolder
                    FROM Leads l
                    INNER JOIN Orders o ON l.LeadId = o.LeadId
                    GROUP BY l.LeadId, l.CustomerName, l.Phone, l.Email, l.LeadHolder
                    HAVING DATEDIFF(CURDATE(), MAX(o.OrderDate)) = @days
                       AND NOT EXISTS (
                           SELECT 1 FROM workflowqueue wq 
                           WHERE wq.WorkflowId = @wfId 
                             AND wq.TargetId = l.LeadId 
                             AND wq.TargetType = 'Customer'
                             AND DATE(wq.ScheduledTime) = CURDATE()
                       );";

                            var dormant = await conn.QueryAsync<dynamic>(sql, new { days = rule.ExecutionDays, wfId = rule.Id });
                            foreach (var cust in dormant)
                            {
                                await _dataService.EnqueueActionAsync(rule.Id, (int)cust.LeadId, "Customer");
                            }
                            break;
                        }

                    // 3. Contact Birthday (Same Month & Day)
                    // 1. Contact Birthday (Evaluated via Custom Fields)
                    case WorkflowEvents.Birthday:
                        {
                            const string sql = @"
            SELECT 
                l.LeadId, 
                l.CustomerName, 
                l.Phone, 
                l.Email, 
                l.LeadHolder
            FROM leads l
            INNER JOIN customfieldvalues cfv ON l.LeadId = cfv.LeadId
            INNER JOIN customfields cf ON cfv.FieldId = cf.FieldId
            WHERE LOWER(cf.FieldName) IN ('birthday', 'dob', 'date of birth', 'birth date')
              AND cfv.FieldValue IS NOT NULL 
              AND TRIM(cfv.FieldValue) != ''
              -- Safely extract Month and Day regardless of standard date format
              AND MONTH(COALESCE(
                    STR_TO_DATE(cfv.FieldValue, '%Y-%m-%d'),
                    STR_TO_DATE(cfv.FieldValue, '%d/%m/%Y'),
                    STR_TO_DATE(cfv.FieldValue, '%d-%m-%Y')
                  )) = MONTH(CURDATE())
              AND DAY(COALESCE(
                    STR_TO_DATE(cfv.FieldValue, '%Y-%m-%d'),
                    STR_TO_DATE(cfv.FieldValue, '%d/%m/%Y'),
                    STR_TO_DATE(cfv.FieldValue, '%d-%m-%Y')
                  )) = DAY(CURDATE())
              -- Duplicate guard: Only trigger once per lead per calendar year
              AND NOT EXISTS (
                  SELECT 1 FROM workflowqueue wq 
                  WHERE wq.WorkflowId = @wfId 
                    AND wq.TargetId = l.LeadId 
                    AND wq.TargetType = 'Lead'
                    AND YEAR(wq.ScheduledTime) = YEAR(CURDATE())
              );";

                            var bdays = await conn.QueryAsync<dynamic>(sql, new { wfId = rule.Id });
                            foreach (var person in bdays)
                            {
                                await _dataService.EnqueueActionAsync(rule.Id, (int)person.LeadId, "Lead");
                            }
                            break;
                        }

                    // 2. Contact Anniversary (Evaluated via Custom Fields)
                    case WorkflowEvents.Anniversary:
                        {
                            const string sql = @"
            SELECT 
                l.LeadId, 
                l.CustomerName, 
                l.Phone, 
                l.Email, 
                l.LeadHolder
            FROM leads l
            INNER JOIN customfieldvalues cfv ON l.LeadId = cfv.LeadId
            INNER JOIN customfields cf ON cfv.FieldId = cf.FieldId
            WHERE LOWER(cf.FieldName) IN ('anniversary', 'anniversary date', 'marriage anniversary', 'wedding anniversary')
              AND cfv.FieldValue IS NOT NULL 
              AND TRIM(cfv.FieldValue) != ''
              AND MONTH(COALESCE(
                    STR_TO_DATE(cfv.FieldValue, '%Y-%m-%d'),
                    STR_TO_DATE(cfv.FieldValue, '%d/%m/%Y'),
                    STR_TO_DATE(cfv.FieldValue, '%d-%m-%Y')
                  )) = MONTH(CURDATE())
              AND DAY(COALESCE(
                    STR_TO_DATE(cfv.FieldValue, '%Y-%m-%d'),
                    STR_TO_DATE(cfv.FieldValue, '%d/%m/%Y'),
                    STR_TO_DATE(cfv.FieldValue, '%d-%m-%Y')
                  )) = DAY(CURDATE())
              -- Duplicate guard: Only trigger once per lead per calendar year
              AND NOT EXISTS (
                  SELECT 1 FROM workflowqueue wq 
                  WHERE wq.WorkflowId = @wfId 
                    AND wq.TargetId = l.LeadId 
                    AND wq.TargetType = 'Lead'
                    AND YEAR(wq.ScheduledTime) = YEAR(CURDATE())
              );";

                            var anniversaries = await conn.QueryAsync<dynamic>(sql, new { wfId = rule.Id });
                            foreach (var person in anniversaries)
                            {
                                await _dataService.EnqueueActionAsync(rule.Id, (int)person.LeadId, "Lead");
                            }
                            break;
                        }
                }
            }

            // Flush any freshly queued tasks
            await ProcessQueueAsync();
        }

        private string FormatTemplate(string template, dynamic data)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;

            foreach (var prop in data.GetType().GetProperties())
            {
                string atTag = $"@{prop.Name}";
                string braceTag = "{{" + prop.Name + "}}";
                string val = prop.GetValue(data)?.ToString() ?? string.Empty;

                template = template.Replace(atTag, val, StringComparison.OrdinalIgnoreCase);
                template = template.Replace(braceTag, val, StringComparison.OrdinalIgnoreCase);
            }
            return template;
        }

        private async Task<dynamic> FetchTargetDataAsync(int id, string type)
        {
            switch (type)
            {
                case "Lead":
                case "Customer":
                    return await _leadService.GetLeadByIdAsync(id);

                default:
                    return await _leadService.GetLeadByIdAsync(id);
            }
        }
    }
}
