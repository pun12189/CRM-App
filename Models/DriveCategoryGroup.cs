using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public partial class DriveCategoryGroup : ObservableObject
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;

        // The target inner source collection holding document rows for this specific category block
        [ObservableProperty]
        private ObservableCollection<UploadedDocumentRow> _documents = new();
    }
}
