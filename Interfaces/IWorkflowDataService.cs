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
        Task<IEnumerable<Workflow>> GetAllWorkflowsAsync();
        Task<bool> SaveWorkflowAsync(Workflow wf);
        Task<bool> DeleteWorkflowAsync(int id);
        Task<IEnumerable<WorkflowTag>> GetTagsForEventAsync(string eventName);
        Task EnqueueActionAsync(int workflowId, int targetId, string targetType);
        Task<IEnumerable<WorkflowQueueItem>> GetPendingQueueAsync();
        Task MarkAsProcessedAsync(int queueId);
    }
}
