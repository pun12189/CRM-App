using CallMan.Interfaces;
using CallMan.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.ViewModels
{
    public class WorkflowEngine
    {
        private readonly IWorkflowDataService _dataService;
        private readonly EmailService _email;
        private readonly LeadService _leadService; // Optional for future WhatsApp integration

        public WorkflowEngine(IWorkflowDataService dataService, EmailService email, LeadService leadService)
        {
            _dataService = dataService;
            _email = email;
            _leadService = leadService;
        }

        /// <summary>
        /// Call this from LeadService or OrderService to queue a new event.
        /// </summary>
        public async Task EnqueueEventAsync(string eventName, int targetId, string targetType)
        {
            // 1. Get all active workflows for this event through the service
            var workflows = await _dataService.GetAllWorkflowsAsync();
            var activeRules = workflows.Where(w => w.EventName == eventName && w.IsEnabled);

            foreach (var rule in activeRules)
            {
                // 2. Persist the task in the database queue
                await _dataService.EnqueueActionAsync(rule.Id, targetId, targetType);
            }

            // 3. Trigger immediate processing for the local session
            await ProcessQueueAsync();
        }

        // Core execution logic used by both instant and inactivity triggers
        public async Task ExecuteWorkflowInternalAsync(int workflowId, dynamic customerData)
        {
            // 1. Get the specific rule
            var workflows = await _dataService.GetAllWorkflowsAsync();
            var rule = workflows.FirstOrDefault(w => w.Id == workflowId);

            if (rule == null) return;

            try
            {
                // 2. Handle WhatsApp
                //if (rule.SendWhatsApp && !string.IsNullOrEmpty(customerData.Phone))
                //{
                //    string body = ParseTemplate(rule.TemplateBody, customerData);
                //    await _wa.SendMessageAsync(customerData.Phone, body);
                //}

                // 3. Handle Email
                if (rule.SendEmail && !string.IsNullOrEmpty(customerData.Email))
                {
                    string body = ParseTemplate(rule.TemplateBody, customerData);
                    await _email.SendEmailAsync(customerData.Email, rule.WorkflowName, body);
                }

                // 4. Log the success so we don't repeat it tomorrow
                // Note: We reuse EnqueueActionAsync logic or a direct log insert
                await _dataService.MarkAsProcessedAsync(workflowId);
            }
            catch (Exception ex)
            {
                // Log error for Aggarwal Cycles Hub admin
            }
        }

        /// <summary>
        /// Processes pending tasks. Call this on App Startup and periodically.
        /// </summary>
        public async Task ProcessQueueAsync()
        {
            // 1. Fetch pending items from the service
            var pendingItems = await _dataService.GetPendingQueueAsync();

            foreach (var item in pendingItems)
            {
                try
                {
                    // 2. Fetch the specific workflow rule
                    var workflows = await _dataService.GetAllWorkflowsAsync();
                    var rule = workflows.FirstOrDefault(w => w.Id == item.WorkflowId);

                    // 3. Fetch target data (Lead/Order) - logic to be implemented based on targetType
                    var data = await FetchTargetDataAsync(item.TargetId, item.TargetType);

                    if (rule != null && data != null)
                    {
                        // WhatsApp Logic
                        //if (!string.IsNullOrEmpty(rule.WhatsAppTemplate))
                        //{
                        //    string body = ParseTemplate(rule.WhatsAppTemplate, data);
                        //    await _wa.SendMessageAsync(data.Phone, body);
                        //}

                        // Email Logic
                        if (!string.IsNullOrEmpty(rule.TemplateBody))
                        {
                            string body = ParseTemplate(rule.TemplateBody, data);
                            await _email.SendEmailAsync(data.Email, rule.WorkflowName, body);
                        }

                        // 4. Mark as processed via service
                        await _dataService.MarkAsProcessedAsync(item.Id);
                    }
                }
                catch (Exception ex)
                {
                    // Log error through a logging service if available
                }
            }
        }

        private string ParseTemplate(string template, dynamic data)
        {
            if (string.IsNullOrEmpty(template)) return "";

            // Reflection-based replacement for {{Property}} tags
            foreach (var prop in data.GetType().GetProperties())
            {
                string tag = "{{" + prop.Name + "}}";
                if (template.Contains(tag))
                {
                    template = template.Replace(tag, prop.GetValue(data)?.ToString() ?? "");
                }
            }
            return template;
        }

        private async Task<dynamic> FetchTargetDataAsync(int id, string type)
        {
            var leads = await _leadService.GetLeadByIdAsync(id);
            return leads; // Placeholder for actual object retrieval
        }

        public async Task CheckInactivityWorkflowsAsync()
        {
            var workflows = await _dataService.GetInactivityWorkflowsAsync(); // SQL: SELECT * FROM Workflows WHERE InactivityDays > 0

            foreach (var wf in workflows)
            {
                // Query users who haven't ordered for the specified number of days
                var inactiveUsers = await _dataService.GetInactiveCustomersAsync(wf.InactivityDays);

                foreach (var user in inactiveUsers)
                {
                    // Avoid double-sending by checking WorkflowLogs
                    if (!await _dataService.HasAlreadyReceivedInactivityNotice(user.Id, wf.Id))
                    {
                        await ExecuteWorkflowInternalAsync(wf.Id, user);
                    }
                }
            }
        }
    }
}
