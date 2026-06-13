using CallMan.Models;
using CallMan.Models.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Interfaces
{
    public interface IDashboardFilterable
    {
        // Receives the current active filter context (or null for All-Time data)
        void ApplyDashboardFilter(DashboardFilter? filter, DashboardTargetView target);
    }
}
