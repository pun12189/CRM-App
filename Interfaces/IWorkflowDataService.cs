using Tijori.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Interfaces
{
    public interface IWorkflowDataService
    {
        // Workflow CRUD
        Task<IEnumerable<Workflow>> GetAllWorkflowsAsync();
        Task<bool> SaveWorkflowAsync(Workflow workflow);

        // Tag Operations
        Task<IEnumerable<WorkflowTag>> GetTagsByEventAsync(string eventName);

        // Queue Operations
        Task EnqueueActionAsync(int workflowId, int targetId, string targetType);
        Task<IEnumerable<WorkflowQueueItem>> GetPendingQueueAsync();
        Task MarkAsProcessedAsync(int queueId);

        Task<IEnumerable<Workflow>> GetInactivityWorkflowsAsync();
        Task<IEnumerable<dynamic>> GetInactiveCustomersAsync(int days);
        Task<bool> HasAlreadyReceivedInactivityNotice(int customerId, int workflowId);
    }
}
