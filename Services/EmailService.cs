using CallMan.Data;
using CallMan.Models;
using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Services
{
    public class EmailService
    {
        private readonly CrmDbContext _context;
        public EmailService(CrmDbContext context) => _context = context;

        // Fetch the active settings
        public async Task<EmailSettings> GetDefaultSettingsAsync()
        {
            using var db = _context.CreateConnection();
            return await db.QueryFirstOrDefaultAsync<EmailSettings>("SELECT * FROM EmailSettings LIMIT 1");
        }

        public async Task SendEmailAsync(string recipientEmail, string subject, string body, List<string> attachments = null)
        {
            // 1. Fetch settings from DB
            using var db = _context.CreateConnection();
            var settings = await db.QueryFirstOrDefaultAsync<EmailSettings>("SELECT * FROM EmailSettings WHERE IsDefault = 1");

            if (settings == null) throw new Exception("Email settings not configured.");

            // 2. Setup SMTP Client
            using var client = new SmtpClient(settings.SmtpServer, settings.Port)
            {
                Credentials = new NetworkCredential(settings.EmailAddress, settings.Password),
                EnableSsl = settings.EnableSSL
            };

            // 3. Compose Message
            var mailMessage = new MailMessage
            {
                From = new MailAddress(settings.EmailAddress, settings.SenderName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            mailMessage.To.Add(recipientEmail);

            if (attachments != null)
            {
                foreach (var file in attachments)
                    mailMessage.Attachments.Add(new Attachment(file));
            }

            // 4. Send
            await client.SendMailAsync(mailMessage);
        }

        // Save or Update logic
        public async Task<bool> SaveSettingsAsync(EmailSettings settings)
        {
            using var db = _context.CreateConnection();

            // We use Id=1 as the fixed record for the primary company email
            string sql = @"
            INSERT INTO EmailSettings (Id, SenderName, EmailAddress, SmtpServer, Port, EnableSSL, Username, Password, IsDefault)
            VALUES (1, @SenderName, @EmailAddress, @SmtpServer, @Port, @EnableSSL, @Username, @Password, 1)
            ON DUPLICATE KEY UPDATE 
                SenderName=@SenderName, 
                EmailAddress=@EmailAddress, 
                SmtpServer=@SmtpServer, 
                Port=@Port, 
                EnableSSL=@EnableSSL, 
                Username=@Username, 
                Password=@Password";

            return await db.ExecuteAsync(sql, settings) > 0;
        }
    }
}
