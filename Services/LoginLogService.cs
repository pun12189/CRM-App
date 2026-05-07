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
    public class LoginLogService
    {
        private readonly CrmDbContext _context;
        public LoginLogService(CrmDbContext context) => _context = context;

        public async Task<int> RecordLoginAsync(int staffId)
        {
            using var db = _context.CreateConnection();
            string sql = @"INSERT INTO LoginLogs (StaffId, MachineName, IPAddress) 
                       VALUES (@staffId, @machine, @ip); SELECT LAST_INSERT_ID();";
            return await db.ExecuteScalarAsync<int>(sql, new
            {
                staffId,
                machine = Environment.MachineName,
                ip = GetLocalIPAddress()
            });
        }

        public async Task<IEnumerable<LoginLog>> GetRecentLogsAsync(int limit = 100)
        {
            using var db = _context.CreateConnection();
            string sql = @"
            SELECT l.*, s.FullName as StaffName, d.DeptName as DepartmentName 
            FROM LoginLogs l
            JOIN Users s ON l.StaffId = s.UserId
            JOIN Departments d ON s.DepartmentId = d.Id
            ORDER BY l.LoginTimestamp DESC 
            LIMIT @limit";
            return await db.QueryAsync<LoginLog>(sql, new { limit });
        }

        private string GetLocalIPAddress() 
        {
            string hostName = Dns.GetHostName();
            IPHostEntry hostEntry = Dns.GetHostEntry(hostName);

            // Get the first IPv4 address in the list
            var localIP = hostEntry.AddressList
                .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            return localIP?.ToString() ?? "127.0.0.1";
        }

        // Updates the logout time
        public async Task RecordLogoutAsync(int logId)
        {
            using var db = _context.CreateConnection();
            string sql = @"UPDATE LoginLogs 
                       SET LogoutTimestamp = CURRENT_TIMESTAMP 
                       WHERE Id = @logId";
            await db.ExecuteAsync(sql, new { logId });
        }
    }
}
