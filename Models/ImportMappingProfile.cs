using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public class ImportMappingProfile
    {
        public int ProfileId { get; set; }
        public string ProfileName { get; set; } = string.Empty;
        public string ModuleType { get; set; } = string.Empty;
        public string MappingJson { get; set; } = string.Empty;
    }
}
