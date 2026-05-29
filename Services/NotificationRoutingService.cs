using CallMan.Data;
using CallMan.Models;
using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Services
{
    public class NotificationRoutingService
    {
        private readonly CrmDbContext _context;

        public NotificationRoutingService(CrmDbContext context)
        {
            _context = context;
        }

        public async Task HeartbeatWorkstationAsync(string currentUsername)
        {
            using var conn = _context.CreateConnection();
            string sql = @"
                INSERT INTO ActiveWorkstations (Username, MachineName, IPAddress, LastPingTime)
                VALUES (@User, @Machine, @IP, NOW())
                ON DUPLICATE KEY UPDATE LastPingTime = NOW(), IPAddress = @IP;";

            await conn.ExecuteAsync(sql, new
            {
                User = currentUsername,
                Machine = Environment.MachineName,
                IP = GetLocalIPAddress()
            });
        }

        public async Task DispatchTargetedToastAsync(NewToastRequest request)
        {
            using var conn = _context.CreateConnection();
            string sql = @"
                INSERT INTO SystemToastsQueue (EventId, LeadId, ReminderType, MessageText, ScheduleTime, TargetUser, TargetMachine, CreatedBy)
                VALUES (@EventId, @LeadId, @Type, @Message, @Schedule, @TargetU, @TargetM, @Sender);";

            await conn.ExecuteAsync(sql, new
            {
                EventId = request.EventId,
                LeadId = request.LeadId,
                Type = request.ReminderType,
                Message = request.MessageContent,
                Schedule = request.ScheduleTime,
                TargetU = request.TargetUser,
                TargetM = request.TargetMachine,
                Sender = request.SenderUser
            });
        }

        public async Task HandleNotificationClick(int toastId)
        {
            using var conn = _context.CreateConnection();
            await conn.ExecuteAsync("UPDATE SystemToastsQueue SET NotificationStatus = 'Read' WHERE ToastId = @Id", new { Id = toastId });
        }

        /// <summary>
        /// Postpones the alert trigger by resetting the tracking metrics back to a clean pending state
        /// </summary>
        public async Task SnoozeNotificationAsync(int eventId, int snoozeMinutes)
        {
            using var conn = _context.CreateConnection();

            // This pulls the targeted alert out of the active loop and schedules it to re-fire after X minutes
            string sql = @"
        UPDATE SystemToastsQueue 
        SET NotificationStatus = 'Pending', 
            ScheduleTime = DATE_ADD(NOW(), INTERVAL @Minutes MINUTE)
        WHERE EventId = @EventId 
          AND NotificationStatus = 'Popped' 
        ORDER BY ToastId DESC 
        LIMIT 1;";

            await conn.ExecuteAsync(sql, new { EventId = eventId, Minutes = snoozeMinutes });
        }

        private string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
            }
            return "127.0.0.1";
        }
    }
}
