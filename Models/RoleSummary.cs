using CallMan.Models.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public class RoleSummary
    {
        public UserRole Role { get; set; }

        // Formats the underlying enum definitions into clean text blocks
        public string Name => Role switch
        {
            UserRole.Admin => "Administrator",
            UserRole.SubAdmin => "Sub Administrator",
            UserRole.TeamLeader => "Team Leader",
            UserRole.Executive => "Executive",
            _ => Role.ToString()
        };

        public int TotalUser { get; set; }

        // PROTECTION GATE: Administrator baseline profiles cannot be manipulated
        public bool CanEdit => Role != UserRole.Admin;
    }
}
