using Tijori.Dialogs;
using Tijori.Interfaces;
using Tijori.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Tijori.Services
{
    public static class ToastWindowManager
    {
        // Tracks currently open visual popup cards visible on the monitor screen space
        private static readonly List<ToastWindow> OpenWindows = new();

        public static void ShowNotification(ToastQueueItem item, NotificationRoutingService routingService, NotificationHistoryService historyService, IDialogService dialogService)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var popupCard = new ToastWindow(item, routingService, historyService, dialogService);
                // IMPORTANT: We wait until the Content is rendered to guarantee ActualHeight is not 0
                popupCard.ContentRendered += (s, e) =>
                {
                    double workingAreaRight = SystemParameters.WorkArea.Right;
                    double workingAreaBottom = SystemParameters.WorkArea.Bottom;

                    // Fixed Card Width boundary metrics size
                    popupCard.Left = workingAreaRight - 350 - 12;

                    // Loop upwards from the bottom workspace taskbar line boundary securely
                    double totalOffset = 12;
                    foreach (var openWin in OpenWindows)
                    {
                        if (openWin != popupCard)
                        {
                            // FIX: If a preceding card hasn't fully rendered its structural height yet,
                            // fall back to a safe explicit fallback height (140px) to prevent overlapping cards!
                            double cardHeight = openWin.ActualHeight > 0 ? openWin.ActualHeight : 140;
                            totalOffset += cardHeight + 8;
                        }
                    }

                    // Set coordinates precisely pushing upwards from the bottom monitor edge
                    popupCard.Top = workingAreaBottom - (popupCard.ActualHeight > 0 ? popupCard.ActualHeight : 140) - totalOffset;
                };

                // Track and clean up stack arrays concurrently when dismissed by user operations
                popupCard.Closed += (s, e) =>
                {
                    OpenWindows.Remove(popupCard);
                    RearrangeStack();
                };

                OpenWindows.Add(popupCard);
                popupCard.Show();
            });
        }

        private static void RearrangeStack()
        {
            double workingAreaBottom = SystemParameters.WorkArea.Bottom;
            double totalOffset = 12;

            // When a single window is dismissed, every remaining card is fully rendered,
            // so we can reliably use openWin.ActualHeight for precise rearrangement adjustments
            for (int i = 0; i < OpenWindows.Count; i++)
            {
                double cardHeight = OpenWindows[i].ActualHeight > 0 ? OpenWindows[i].ActualHeight : 140;
                OpenWindows[i].Top = workingAreaBottom - cardHeight - totalOffset;
                totalOffset += cardHeight + 8;
            }
        }
    }
}
