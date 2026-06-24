using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public class PageItem
    {
        public string DisplayText { get; set; } = string.Empty;
        public int PageNumber { get; set; }
        public bool IsSelected { get; set; }
        public bool IsClickable { get; set; }
    }
}
