using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tijori.Models;

namespace Tijori.Interfaces
{
    public interface ITileCardItem
    {
        // 🌟 Selection Binding
        bool IsSelectedForAction { get; set; }

        // 1. Top Header / Parent Info
        string HeaderTag { get; }            // e.g., "(Acme Corp)", "(Electronics)", "(PO-2026-0001)"

        // 2. Primary Identifier & Avatar Badge
        string PrimaryTitle { get; }         // e.g., "Puneet Aggarwal", "MacBook Pro", "Rajesh Sharma"
        string BadgeText { get; }            // e.g., Initials "PA", "MB", or Short Code "ORD"
        string BadgeBackgroundHex { get; }   // e.g., "#E0F2FE"
        string BadgeForegroundHex { get; }   // e.g., "#0369A1"

        // 3. Compact Summary Row
        string OwnerOrMetaLabel { get; }     // e.g., "Lead Holder: Admin", "Stock: 45 Pcs", "Total: ₹45,000"

        // 🌟 DISTINCT STATUS 🌟
        string StatusText { get; }
        string StatusColorHex { get; }
        string StatusTextColorHex { get; }

        // 🌟 DISTINCT SINGLE TAG 🌟
        string TagText { get; }
        string TagColorHex { get; }
        string TagTextColorHex { get; }
        string SourceOrCategory { get; }     // e.g., "JustDial", "Laptops", "Direct Order"

        // 4. Expandable Hover Details Panel (Dynamic Key-Value Pairs)
        List<TileCardDetailField> ExpandedDetails { get; }
        List<string> BadgesOrLabels { get; }

        // 5. Original Domain Model Payload (for Commands: Edit, Update, Delete)
        object RawModel { get; }
    }
}
