using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public class WorkflowTag
    {
        public int Id { get; set; }
        public string EventName { get; set; }
        public string TagName { get; set; } // What the user sees
        public string TagValue { get; set; } // The code property

        // Formatted for the UI list
        public string DisplayName => $"@{TagName}";
    }
}
