using CallMan.Models.Enums;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Interfaces.Reports
{
    public interface IE2EReportStrategy
    {
        string GetStrategyKey(E2EMainFilter main, E2EComparisonTarget target);
        Task<DataTable> RunQueryAsync(IDbConnection db, DateTime from, DateTime to);
    }
}
