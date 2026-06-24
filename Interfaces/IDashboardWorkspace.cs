using CallMan.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Interfaces
{
    public interface IDashboardWorkspace
    {
        bool WorkspaceViewIsActive { get; set; }
        Lead? ActiveProfileLead { get; set; }
        void ShowLeadWorkspace(Lead selectedLead);
        void HideLeadWorkspace();
    }
}
