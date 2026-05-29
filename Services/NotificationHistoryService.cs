using CallMan.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CallMan.Services
{
    public class NotificationHistoryService : INotifyPropertyChanged
    {
        // Holds the last 7 days of alerts for the top navbar drawer list
        public ObservableCollection<ToastQueueItem> ActiveNotifications { get; } = new();

        private int _unreadCount;
        public int UnreadCount
        {
            get => _unreadCount;
            set
            {
                _unreadCount = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UnreadCount)));
            }
        }

        public void RefreshFromDatabaseList(System.Collections.Generic.List<ToastQueueItem> items)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ActiveNotifications.Clear();
                foreach (var item in items.OrderByDescending(x => x.CreatedAt))
                {
                    ActiveNotifications.Add(item);
                }
                // Badge count shows only what hasn't been opened/read yet
                UnreadCount = ActiveNotifications.Count(x => x.NotificationStatus != "Read");
            });
        }

        public void AddNotification(ToastQueueItem item)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ActiveNotifications.Insert(0, item);
                UnreadCount = ActiveNotifications.Count;
            });
        }

        public void ClearNotification(ToastQueueItem item)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ActiveNotifications.Remove(item);
                UnreadCount = ActiveNotifications.Count;
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
