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
        private readonly CrmDbContext _context;

        // The context is automatically injected by the DI container
        public AuthService(CrmDbContext context)
        {
            _context = context;
        }

        public async Task<User?> AuthenticateByEmailAsync(string email, string password)
        {
            using IDbConnection db = _context.CreateConnection();

            string sql = "SELECT * FROM Users WHERE Email = @email AND IsActive = 1 LIMIT 1";
            var user = await db.QueryFirstOrDefaultAsync<User>(sql, new { email });

            if (user != null && BCrypt.Net.BCrypt.Verify(password, user.Password))
            {                
                return user;
            }

            return null;
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
                    Subject = "Password Reset - CallMan",
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
