using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Tijori.Models.Enums;

namespace Tijori.Models
{
    public partial class Category : ObservableObject
    {
        public int Id { get; set; }

        [ObservableProperty]
        private string _categoryName = string.Empty;

        public int? ParentId { get; set; }
        public string? ParentName { get; set; }

        public ItemClassification CategoryType { get; set; } = ItemClassification.FinishedGood;

        public ObservableCollection<Category> SubCategories { get; set; } = new();

        // 🌟 Hierarchy Depth & Indentation Helpers 🌟
        public int HierarchyLevel { get; set; } = 0;
        public bool IsSubCategory => HierarchyLevel > 0;
        public Thickness IndentationMargin => new Thickness(HierarchyLevel * 18, 0, 0, 0);

        public string ClassificationDisplay => CategoryType switch
        {
            ItemClassification.FinishedGood => "Finished Good",
            ItemClassification.RawMaterial => "Raw Material / Chemical",
            ItemClassification.PackagingMaterial => "Packaging Material",
            ItemClassification.SemiFinished => "Semi-Finished / Bulk",
            ItemClassification.TradingGoods => "Trading Item",
            ItemClassification.Service => "Service / Job Work",
            _ => CategoryType.ToString()
        };

        public string ClassificationBadgeBackground => CategoryType switch
        {
            ItemClassification.FinishedGood => "#DCFCE7",
            ItemClassification.RawMaterial => "#E0F2FE",
            ItemClassification.PackagingMaterial => "#FEF3C7",
            ItemClassification.SemiFinished => "#F3E8FF",
            ItemClassification.Service => "#FFEDD5",
            _ => "#F1F5F9"
        };

        public string ClassificationBadgeForeground => CategoryType switch
        {
            ItemClassification.FinishedGood => "#15803D",
            ItemClassification.RawMaterial => "#0369A1",
            ItemClassification.PackagingMaterial => "#B45309",
            ItemClassification.SemiFinished => "#7E22CE",
            ItemClassification.Service => "#C2410C",
            _ => "#475569"
        };
    }
}
