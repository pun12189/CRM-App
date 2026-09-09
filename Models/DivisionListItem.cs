using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Tijori.Models
{
    public partial class DivisionListItem : ObservableObject
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }

        // Joined Profile Details
        public int ProfileId { get; set; }
        public byte[]? LogoData { get; set; }
        public BitmapSource? LogoImage { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string GstNumber { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string OfficialEmail { get; set; } = string.Empty;
        public string CompanyInitials { get; set; } = string.Empty;
    }
}
