using CallMan.Models.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Interfaces
{
    public interface IImportService
    {
        Task<int> BulkInsertAsync(IEnumerable<dynamic> data, ImportType type);
    }
}
