using Tijori.Data;
using Tijori.Dialogs;
using Tijori.Interfaces;
using Tijori.Models;
using Dapper;
using Microsoft.Toolkit.Uwp.Notifications;

namespace Tijori.Services
{
    public class ToastPollingWorker
    {
        private readonly CrmDbContext _db;
        private readonly NotificationRoutingService _routingService;
        private CancellationTokenSource? _cts;
        private string _currentAppUser = "Unknown";
        private readonly IUserSession _session;
        private readonly IDialogService _dialogService;
        private readonly NotificationHistoryService _historyService;

        public ToastPollingWorker(CrmDbContext db, NotificationRoutingService routingService, IUserSession session, NotificationHistoryService historyService, IDialogService dialogService)
        {
            _db = db;
            _routingService = routingService;
            _historyService = historyService;
            _session = session;
            _dialogService = dialogService;
            InitializeAndStart(_session.CurrentUser);

        }

        public void InitializeAndStart(string username)
        {
            _currentAppUser = username;
            _cts = new CancellationTokenSource();
            Task.Run(() => PollingExecutionLoopAsync(_cts.Token));
        }

        public void Stop() => _cts?.Cancel();

        private async Task PollingExecutionLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await _routingService.HeartbeatWorkstationAsync(_currentAppUser);
                    await InterceptAndFireLocalToastsAsync();
                }
                catch { /* Fail Silently Offline */ }
                await Task.Delay(TimeSpan.FromSeconds(8), token);
            }
        }

        // Inside your loop routine block in ToastPollingWorker.cs
        private async Task InterceptAndFireLocalToastsAsync()
        {
            using var conn = _db.CreateConnection();

            // 1. ALWAYS REFRESH THE DRAWER: Fetch all items for target user within the last 7 days
            string historySql = @"
        SELECT * 
        FROM SystemToastsQueue 
        WHERE CreatedAt >= DATE_SUB(NOW(), INTERVAL 7 DAY)
          AND (TargetUser = @User OR TargetUser IS NULL);";

            var rollingHistory = (await conn.QueryAsync<ToastQueueItem>(historySql, new { User = _currentAppUser })).ToList();
            _historyService.RefreshFromDatabaseList(rollingHistory);

            // 2. ISOLATE NEW POPUPS: Find alerts that have not been popped on this station yet
            var pendingPopups = rollingHistory.Where(x => x.NotificationStatus == "Pending" && x.ScheduleTime <= DateTime.Now).ToList();

            foreach (var toastItem in pendingPopups)
            {
                string updateSql = "UPDATE SystemToastsQueue SET NotificationStatus = 'Popped' WHERE ToastId = @Id;";
                int rowsUpdated = await conn.ExecuteAsync(updateSql, new { Id = toastItem.ToastId });

                if (rowsUpdated == 1)
                {
                    toastItem.NotificationStatus = "Popped";
                    new ToastContentBuilder()
                     .AddArgument("toastId", toastItem.ToastId)
                     .AddArgument("leadId", toastItem.LeadId)
                     .AddText(toastItem.ReminderType)
                     .AddText(toastItem.MessageText)
                     .AddAttributionText("TIJORI")
                     .Show();
                }
            }
        }
    }
}
