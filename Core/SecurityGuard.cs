using Tijori.Models;
using Tijori.Models.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Tijori.Core
{
    public static class SecurityGuard
    {
        // Global in-memory cache populated instantly upon successful user login verification sequences
        public static Dictionary<string, PermissionRow> SessionRightsCache { get; set; } = new();
        public static UserRole ActiveUserRole { get; set; } = UserRole.Executive;

        // 1. REGISTER THE ATTACHED PROPERTY WRAPPER TO TRACK CORE STRINGS OVER XAML LAYOUT TREES
        public static readonly DependencyProperty RequiresProperty =
            DependencyProperty.RegisterAttached(
                "Requires",
                typeof(string),
                typeof(SecurityGuard),
                new PropertyMetadata(string.Empty, OnSecurityRuleConfigurationChanged));

        public static string GetRequires(DependencyObject obj) => (string)obj.GetValue(RequiresProperty);
        public static void SetRequires(DependencyObject obj, string value) => obj.SetValue(RequiresProperty, value);

        // 2. PARSE AND EVALUATE UI ENFORCEMENT RULES IN REAL TIME
        private static void OnSecurityRuleConfigurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement element) return;
            string ruleExpression = e.NewValue as string;

            if (string.IsNullOrWhiteSpace(ruleExpression) || !ruleExpression.Contains(':')) return;

            // Split out the token inputs (e.g. "Proforma:Create" splits to "Proforma" and "Create")
            string[] tokens = ruleExpression.Split(':');
            string moduleKey = tokens[0];
            string actionType = tokens[1];

            bool baseAccessAuthorized = EvaluateAuthorizationState(moduleKey, actionType);

            // If authorization rules fail, cleanly remove the element from view space parameters
            if (!baseAccessAuthorized)
            {
                element.Visibility = Visibility.Collapsed;
                element.IsEnabled = false;
            }
            else
            {
                // FIX: Re-enable and restore visibility if the user switches to an authorized module context
                element.Visibility = Visibility.Visible;
                element.IsEnabled = true;
            }
        }

        private static bool EvaluateAuthorizationState(string moduleKey, string action)
        {
            // Super-Admin profiles bypass all security rules globally
            if (ActiveUserRole == UserRole.Admin) return true;

            // Block access if the requested module isn't loaded or registered
            if (!SessionRightsCache.TryGetValue(moduleKey, out var matrixRow)) return false;

            return action switch
            {
                "View" => matrixRow.CanView,
                "Edit" => matrixRow.CanEdit,
                "Create" => matrixRow.CanCreate,
                "Delete" => matrixRow.CanDelete,
                "Update" => matrixRow.CanUpdate, // Added tracking support evaluation hooks
                _ => false
            };
        }
    }
}
