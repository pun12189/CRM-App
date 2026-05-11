using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public class WorkflowQueueItem
    {
        public int Id { get; set; }
        public int WorkflowId { get; set; }
        public int TargetId { get; set; }
        public string TargetType { get; set; }
        public DateTime ScheduledTime { get; set; }
    }
}
