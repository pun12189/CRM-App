using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tijori.Interfaces;

namespace Tijori.Models
{
    public class GenericTileCardItem : ObservableObject, ITileCardItem
    {
        public string HeaderTag { get; set; } = string.Empty;
        public string PrimaryTitle { get; set; } = string.Empty;
        public string BadgeText { get; set; } = "??";
        public string BadgeBackgroundHex { get; set; } = "#E0F2FE";
        public string BadgeForegroundHex { get; set; } = "#0369A1";
        public string OwnerOrMetaLabel { get; set; } = string.Empty;
        // Separate Status & Tag
        public string StatusText { get; set; } = string.Empty;
        public string StatusColorHex { get; set; } = "#F1F5F9";
        public string StatusTextColorHex { get; set; } = "#475569";

        public string TagText { get; set; } = string.Empty;
        public string TagColorHex { get; set; } = "#FEF3C7";
        public string TagTextColorHex { get; set; } = "#B45309";
        public string SourceOrCategory { get; set; } = string.Empty;
        public List<TileCardDetailField> ExpandedDetails { get; set; } = new();
        public List<string> BadgesOrLabels { get; set; } = new();
        public object RawModel { get; set; } = null!;
        private bool _isSelectedForAction;
        public bool IsSelectedForAction
        {
            get => _isSelectedForAction;
            set
            {
                if (SetProperty(ref _isSelectedForAction, value))
                {
                    // Sync with original domain model (e.g., Lead.IsSelectedForAction)
                    if (RawModel is Lead lead)
                    {
                        lead.IsSelectedForAction = value;
                    }
                }
            }
        }
    }
}
