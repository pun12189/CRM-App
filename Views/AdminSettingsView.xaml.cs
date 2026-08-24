using Tijori.Services;
using Tijori.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Tijori.Views
{
    /// <summary>
    /// Interaction logic for AdminSettingsView.xaml
    /// </summary>
    public partial class AdminSettingsView : UserControl
    {
        private bool _isInternalScrollUpdate = false;

        public AdminSettingsView()
        {
            InitializeComponent();
            this.Loaded += AdminSettingsView_Loaded;
        }

        private void AdminSettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            LoadingService.Hide();
        }

        #region CLICK-TO-SCROLL INTERACTION WITH WEB-APP EASING
        private void SidebarButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton btn && btn.Tag != null)
            {
                FrameworkElement targetSection = null;

                // Match button tags cleanly to corresponding section framework targets
                switch (btn.Tag.ToString())
                {
                    case "InternalSetup": targetSection = SecInternalSetup; break;
                    case "LeadSettings": targetSection = SecLeadSettings; break;
                    case "Integrations": targetSection = SecIntegrations; break;
                    case "Automations": targetSection = SecAutomations; break;
                    case "Manufacturing": targetSection = SecManufacturing; break;
                }

                if (targetSection != null)
                {
                    // Calculate relative scroll vertical height position parameters
                    var transform = targetSection.TransformToAncestor(MainWorkspaceScroller);
                    Point relativePoint = transform.Transform(new Point(0, 0));
                    double targetVerticalOffset = MainWorkspaceScroller.VerticalOffset + relativePoint.Y;

                    // Execute a smooth, easing DoubleAnimation on the custom dependency tracking mechanism
                    AnimateScrollTo(targetVerticalOffset);
                }
            }
        }

        private void AnimateScrollTo(double targetOffset)
        {
            _isInternalScrollUpdate = true; // Lock scroll-spy updates while animating

            if (targetOffset > MainWorkspaceScroller.ScrollableHeight)
                targetOffset = MainWorkspaceScroller.ScrollableHeight;
            if (targetOffset < 0) targetOffset = 0;

            DoubleAnimation scrollAnimation = new DoubleAnimation
            {
                From = MainWorkspaceScroller.VerticalOffset,
                To = targetOffset,
                Duration = TimeSpan.FromSeconds(0.4),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            scrollAnimation.Completed += (s, e) => _isInternalScrollUpdate = false;

            var animator = new ScrollViewerAnimator(MainWorkspaceScroller);

            // Clean, direct call on the FrameworkElement animator instance
            animator.BeginAnimation(ScrollViewerAnimator.VerticalOffsetProperty, scrollAnimation);
        }
        #endregion

        #region AUTO SELECT ON MANUALLY SCROLLING (SCROLL-SPY ENGINE)
        private void MainWorkspaceScroller_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Drop out instantly if the movement was generated programmatically by clicking a sidebar button
            if (_isInternalScrollUpdate) return;

            double currentOffset = MainWorkspaceScroller.VerticalOffset;
            double scrollBuffer = 60; // Offset value context tweak buffer margin for header sizes

            // Compute item baseline vertical coordinates dynamically relative to the container frame viewports
            double leadSettingsTop = GetSectionTopOffset(SecLeadSettings);
            double integrationsTop = GetSectionTopOffset(SecIntegrations);
            double automationsTop = GetSectionTopOffset(SecAutomations);
            double manufacturingTop = GetSectionTopOffset(SecManufacturing);

            // Turn off event hooks temporarily to change check states safely without loops
            _isInternalScrollUpdate = true;

            if (currentOffset >= manufacturingTop - scrollBuffer || currentOffset >= MainWorkspaceScroller.ScrollableHeight - 10)
            {
                RbManufacturing.IsChecked = true;
            }
            else if (currentOffset >= automationsTop - scrollBuffer)
            {
                RbAutomations.IsChecked = true;
            }
            else if (currentOffset >= integrationsTop - scrollBuffer)
            {
                RbIntegrations.IsChecked = true;
            }
            else if (currentOffset >= leadSettingsTop - scrollBuffer)
            {
                RbLeadSettings.IsChecked = true;
            }
            else
            {
                RbInternalSetup.IsChecked = true;
            }

            _isInternalScrollUpdate = false;
        }

        private double GetSectionTopOffset(FrameworkElement element)
        {
            try
            {
                var transform = element.TransformToAncestor(MainWorkspaceScroller);
                return MainWorkspaceScroller.VerticalOffset + transform.Transform(new Point(0, 0)).Y;
            }
            catch
            {
                return 0; // Fallback context layer safety rule checks during background visual window processing routines
            }
        }
        #endregion
    }

    /// <summary>
    /// Lightweight internal animation helper class needed because WPF's ScrollViewer 
    /// doesn't expose VerticalOffset as a dependency property that can be directly animated.
    /// </summary>
    internal class ScrollViewerAnimator : FrameworkElement
    {
        private readonly ScrollViewer _scroller;

        public ScrollViewerAnimator(ScrollViewer scroller) => _scroller = scroller;

        public static readonly DependencyProperty VerticalOffsetProperty =
            DependencyProperty.Register(
                nameof(VerticalOffset),
                typeof(double),
                typeof(ScrollViewerAnimator),
                new PropertyMetadata(0.0, OnVerticalOffsetChanged));

        public double VerticalOffset
        {
            get => (double)GetValue(VerticalOffsetProperty);
            set => SetValue(VerticalOffsetProperty, value);
        }

        private static void OnVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewerAnimator animator && animator._scroller != null)
            {
                animator._scroller.ScrollToVerticalOffset((double)e.NewValue);
            }
        }
    }
}
