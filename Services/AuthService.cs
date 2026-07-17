using CallMan.Core;
using CallMan.Data;
using CallMan.Interfaces;
using CallMan.Models;
using Dapper;
using MySql.Data.MySqlClient;
using Org.BouncyCastle.Crypto.Generators;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApiService _apiService;
        private readonly CrmDbContext _context;
        private readonly IUserSession _session;
        private readonly LoginLogService _logService;
        private readonly PermissionService _permissionService;

        public AuthService(ApiService apiService, CrmDbContext context, IUserSession session, LoginLogService logService, PermissionService permissionService)
        {
            _apiService = apiService;
            _context = context;
            _session = session;
            _logService = logService;
            _permissionService = permissionService;
        }

        public async Task<bool> AuthenticateByEmailAsync(string email, string password)
        {
            // 1. CALL THE API SERVICE FIRST (Master Admin)
            /*var masterAdmin = await _apiService.CheckMasterAdminAsync(email, password);

            if (masterAdmin != null)
            {
                _session.CurrentUserEmail = masterAdmin.Email;
                _session.CurrentUser = "Admin";
                _session.DisplayName = "Administrator";
                _session.UserRole = "Admin";
                _session.UserId = 0;
                _session.UserLimit = masterAdmin.UserLimit;
                _session.ExpiryDate = masterAdmin.ExpiryDate;
                _session.MemberSince = masterAdmin.MemberSince;
                var id = await _logService.RecordLoginAsync(0);
                _session.LogId = id;
                return true;
            }*/

            // 2. FALLBACK TO DATABASE (Local Staff)
            using IDbConnection db = _context.CreateConnection();

            string sql = "SELECT * FROM Users WHERE Email = @email AND IsActive = 1 LIMIT 1";
            var user = await db.QueryFirstOrDefaultAsync<User>(sql, new { email });

            if (user != null && BCrypt.Net.BCrypt.Verify(password, user.Password))
            {
                SecurityGuard.ActiveUserRole = user.Role;

                // Load the 34 relational module matrix rows straight into the lookup dictionary cache
                SecurityGuard.SessionRightsCache = await _permissionService.HydrateUserSessionSecurityProfileAsync(user.Role);

                _session.UserId = user.UserId;
                _session.CurrentUserEmail = user.Email;
                _session.DisplayName = user.FullName;
                _session.CurrentUser = user.FullName;
                _session.UserRole = user.Role.ToString();
                _session.SeniorId = user.SeniorId;
                var id = await _logService.RecordLoginAsync(user.UserId);
                _session.LogId = id;
                return true;
            }

            return false;
        }

        public async Task<bool> ResetPasswordAsync(string email)
        {
            using IDbConnection db = _context.CreateConnection();

            // 1. Check if user exists
            const string checkSql = "SELECT COUNT(1) FROM Users WHERE Email = @email";
            var exists = await db.ExecuteScalarAsync<bool>(checkSql, new { email });

            if (!exists) return false;

            // 1. Generate a simple temporary password
            string tempPassword = Guid.NewGuid().ToString().Substring(0, 8);

            // 2. Hash the new password
            string newHash = BCrypt.Net.BCrypt.HashPassword(tempPassword);

            // 3. Update the database
            const string updateSql = "UPDATE Users SET Password = @newHash WHERE Email = @email";
            var result = await db.ExecuteAsync(updateSql, new { newHash, email });

            return await SendEmailAsync(email, tempPassword);
        }

        private async Task<bool> SendEmailAsync(string userEmail, string tempPass)
        {
            try
            {
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("sofricerp@gmail.com", "oazd ncms rbfa ongy"),
                    EnableSsl = true,
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress("sofricerp@gmail.com"),
                    Subject = "Password Reset - Tijori",
                    Body = $"<h1>Security Update</h1><p>Your temporary password is: <b>{tempPass}</b></p>",
                    IsBodyHtml = true,
                };
                mailMessage.To.Add(userEmail);

                await smtpClient.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
