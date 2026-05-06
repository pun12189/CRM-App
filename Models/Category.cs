using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public partial class Category : ObservableObject
    {
        public int Id { get; set; }

        [ObservableProperty]
        private string _categoryName;

        public int? ParentId { get; set; }

        public string ParentName { get; set; }

        // This collection allows the hierarchy to function in WPF
        public ObservableCollection<Category> SubCategories { get; set; } = new();
    }
}
