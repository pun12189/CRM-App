using Tijori.Interfaces;
using Tijori.Models;
using Tijori.Services;
using MaterialDesignThemes.Wpf;
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
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Tijori.Dialogs
{
    /// <summary>
    /// Interaction logic for ToastWindow.xaml
    /// </summary>
    public partial class ToastWindow : Window
    {
        private readonly NotificationRoutingService _routingService;

        private readonly NotificationHistoryService _historyService;

        private readonly IDialogService _dialogService;

        private readonly ToastQueueItem _item;

        public ToastWindow(ToastQueueItem item, NotificationRoutingService routingService, NotificationHistoryService historyService, IDialogService dialogService)
        {
            InitializeComponent();
            _item = item;
            _routingService = routingService;
            _historyService = historyService;
            _dialogService = dialogService;
            TxtMessage.Text = _item.MessageText;
            TxtTitle.Text = $"{_item.ReminderType} Reminder";
            TxtTimeStamp.Text = $"({_item.FormattedTime})";

            if (_item.ReminderType.Equals("Payment", StringComparison.OrdinalIgnoreCase))
            {
                NotificationIcon.Kind = PackIconKind.CurrencyInr;
                NotificationIcon.Foreground = System.Windows.Media.Brushes.Crimson;
            }
            else
            {
                NotificationIcon.Kind = PackIconKind.AccountClock;
                NotificationIcon.Foreground = System.Windows.Media.Brushes.SteelBlue;
            }            
        }

        private async void SnoozeButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Extract the selected minute parameters out of the Material ComboBox Tag mapping
            if (ComboSnooze.SelectedValue is string minStr && int.TryParse(minStr, out int minutes))
            {
                try
                {
                    // 2. Remove the item from the top navbar history drawer collection instantly so the unread badge clears out
                    foreach (var item in _historyService.ActiveNotifications)
                    {
                        if (item.EventId == _item.EventId)
                        {
                            _historyService.ClearNotification(item);
                            break;
                        }
                    }                    

                    // Fire the update task asynchronously in the background to prevent UI stuttering
                    await _routingService.SnoozeNotificationAsync(_item.EventId, minutes);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to write snooze data to MySQL: {ex.Message}");
                }
            }

            this.Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close(); // Closes screen presence, stays safely inside the navbar 7-day drawer
        }

        private async void CardGrid_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Call the direct workflow router when clicked
            this.Close();
            await _dialogService.ShowHistoryDialog(_item.LeadId);            
        }
    }
}
