using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Interfaces
{
    public interface IGlobalSettingsService
    {
        Task<bool> GetMaster2FAStatusAsync();
        Task SaveGlobal2FAPolicyAsync(bool isEnabled, string adminSecret = null);
    }
}
