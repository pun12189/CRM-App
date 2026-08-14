using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Helper
{
    public enum ActivePackageTier
    {
        LMS = 1,      // Base Level
        CRM = 2,      // Includes LMS + CRM
        CRMPro = 3,   // Includes LMS + CRM + CRM-Pro
        ERP = 4       // Includes All (LMS + CRM + CRM-Pro + ERP)
    }

    public static class ModuleManager
    {
        public static event EventHandler? OnModuleStateChanged;

        // Current Active Package Selected in Admin Drawer (Default: LMS)
        public static ActivePackageTier CurrentPackage { get; private set; } = ActivePackageTier.LMS;

        // Granular Sub-Option Overrides (within allowed active package capabilities)
        private static readonly Dictionary<string, bool> SubFeatureStates = new(StringComparer.OrdinalIgnoreCase)
        {
            { "LMS:AddLead", true },
            { "LMS:ImportLeads", true },
            { "LMS:ExportLeads", true },
            { "LMS:DeleteLead", true }
        };

        /// <summary>
        /// Switches the active package exclusively (e.g. selecting CRM automatically disables pure LMS/ERP package modes)
        /// </summary>
        public static void SwitchPackage(ActivePackageTier newPackage)
        {
            CurrentPackage = newPackage;
            OnModuleStateChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// Evaluates whether a UI control or menu is enabled under the current package rank.
        /// </summary>
        public static bool IsFeatureEnabled(string featureKey)
        {
            if (string.IsNullOrWhiteSpace(featureKey)) return true;

            // 1. Direct Module Category Checks (e.g., "LMS", "CRM", "CRMPro", "ERP")
            if (Enum.TryParse<ActivePackageTier>(featureKey, true, out var requiredPackageLevel))
            {
                // Feature is visible if Current Package level is GREATER than or EQUAL to required package level
                return (int)CurrentPackage >= (int)requiredPackageLevel;
            }

            // 2. Sub-Feature Action Checks (e.g., "LMS:AddLead")
            if (featureKey.Contains(":"))
            {
                string parentModuleStr = featureKey.Split(':')[0];
                if (Enum.TryParse<ActivePackageTier>(parentModuleStr, true, out var parentPackageLevel))
                {
                    // If the active package level doesn't reach this parent feature, hide it
                    if ((int)CurrentPackage < (int)parentPackageLevel) return false;

                    // If package supports it, check individual toggle switch state
                    if (SubFeatureStates.TryGetValue(featureKey, out bool isSubActive))
                    {
                        return isSubActive;
                    }
                }
            }

            return true;
        }

        public static void SetSubFeatureState(string featureKey, bool isEnabled)
        {
            SubFeatureStates[featureKey] = isEnabled;
            OnModuleStateChanged?.Invoke(null, EventArgs.Empty);
        }
    }
}
