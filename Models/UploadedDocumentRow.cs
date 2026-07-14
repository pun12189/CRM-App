using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public partial class UploadedDocumentRow : ObservableObject
    {
        public int DocumentId { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;

        [ObservableProperty] private string _fileName = string.Empty;
        [ObservableProperty] private string _storagePath = string.Empty;

        public string UploadedBy { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
