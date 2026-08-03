using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public class GlobalSearchRowItem
    {
        public int Id { get; set; } // Map to LeadId from your data schema source columns
        public string CustomerName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string AltPhone { get; set; } = string.Empty;
        public bool HasCompany { get; set; }
    }
}
