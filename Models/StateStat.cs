using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public class StateStat
    {
        public string State { get; set; } = string.Empty;
        public int MaturedCount { get; set; }
        public int TotalLeads { get; set; }

        // Formatted for the Sidebar Display
        public string DisplayLabel => $"{(string.IsNullOrWhiteSpace(State) ? "Unknown" : State)} ({MaturedCount}/{TotalLeads})";
    }
}
