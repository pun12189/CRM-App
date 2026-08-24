using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models.Enums
{
    public enum ItemClassification
    {
        FinishedGood = 1,        // Items you manufacture or sell
        RawMaterial = 2,         // Active ingredients, excipients, chemicals
        PackagingMaterial = 3,   // Bottles, caps, foils, cartons, labels
        SemiFinished = 4,        // Bulk intermediate mixtures
        TradingGoods = 5,        // Direct buy & sell products
        Service = 6              // Job work / labor / conversion charges
    }
}
