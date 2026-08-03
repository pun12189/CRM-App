using Tijori.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Interfaces
{
    public interface IDashboardWorkspace
    {
        bool WorkspaceViewIsActive { get; set; }
        Lead? ActiveProfileLead { get; set; }
        void ShowLeadWorkspace(Lead selectedLead);
        void HideLeadWorkspace();
    }
}
