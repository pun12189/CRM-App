using Tijori.Models;
using Tijori.Models.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Interfaces
{
    public interface IDashboardFilterable
    {
        // Receives the current active filter context (or null for All-Time data)
        void ApplyDashboardFilter(DashboardFilter? filter, DashboardTargetView target);
    }
}
