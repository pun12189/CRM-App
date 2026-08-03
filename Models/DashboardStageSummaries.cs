using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public class DashboardStageSummaries
    {
        public List<KeyValuePair<string, int>> Reminders { get; set; } = new();
        public List<KeyValuePair<string, int>> FollowupStages { get; set; } = new();
        public List<KeyValuePair<string, int>> MatureStages { get; set; } = new();
        public List<KeyValuePair<string, int>> LeadLabels { get; set; } = new();
    }
}
