using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Tijori.Helper
{
    public static class ModuleVisibilityProps
    {
        public static readonly DependencyProperty RequiredFeatureProperty =
            DependencyProperty.RegisterAttached(
                "RequiredFeature",
                typeof(string),
                typeof(ModuleVisibilityProps),
                new PropertyMetadata(null, OnRequiredFeatureChanged));

        public static string GetRequiredFeature(DependencyObject obj) =>
            (string)obj.GetValue(RequiredFeatureProperty);

        public static void SetRequiredFeature(DependencyObject obj, string value) =>
            obj.SetValue(RequiredFeatureProperty, value);

        private static void OnRequiredFeatureChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element)
            {
                UpdateElementVisibility(element, e.NewValue as string);

                // Subscribe to live state updates
                ModuleManager.OnModuleStateChanged -= Element_ModuleStateChanged;
                ModuleManager.OnModuleStateChanged += Element_ModuleStateChanged;

                void Element_ModuleStateChanged(object? sender, EventArgs args)
                {
                    UpdateElementVisibility(element, GetRequiredFeature(element));
                }
            }
        }

        private static void UpdateElementVisibility(UIElement element, string? featureKey)
        {
            if (string.IsNullOrWhiteSpace(featureKey))
            {
                element.Visibility = Visibility.Visible;
                return;
            }

            bool isAllowed = ModuleManager.IsFeatureEnabled(featureKey);
            element.Visibility = isAllowed ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
