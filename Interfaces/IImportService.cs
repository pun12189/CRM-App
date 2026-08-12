using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tijori.Models;
using Tijori.Models.Enums;
using Tijori.ViewModels;

namespace Tijori.Interfaces
{
    public interface IImportService
    {
        Task<int> BulkInsertAsync(List<Dictionary<string, object>> data, ImportType type, List<ImportMappingRow> mappingRules);
        Task<List<ImportMappingProfile>> GetMappingProfilesAsync(string moduleType);
        Task SaveMappingProfileAsync(string profileName, string moduleType, Dictionary<string, string> mappings);
        Task DeleteMappingProfileAsync(int profileId);
    }
}
