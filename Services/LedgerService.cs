using CallMan.Data;
using CallMan.Models;
using Dapper;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Services
{
    public class LedgerService
    {
        private readonly CrmDbContext _context;

        public LedgerService(CrmDbContext context)
        {
            _context = context;
        }

        public async Task<List<PaymentEntry>> GetAllLedgerEntriesAsync()
        {
            const string sql = @"
                SELECT 
                    p.PaymentId,
                    p.OrderId,
                    p.LeadId,
                    l.CustomerName,
                    l.CompanyName,
                    o.OrderType,
                    p.AmountReceived,
                    p.Remarks,
                    CASE 
                        WHEN p.AmountReceived < 0 THEN 'Debit'
                        ELSE 'Credit'
                    END AS TransactionType,
                    COALESCE(u.FullName, 'Admin') AS RecordedBy,
                    p.PaymentDate
                FROM Payments p
                LEFT JOIN `Orders` o ON p.OrderId = o.OrderId
                LEFT JOIN `Leads` l ON p.LeadId = l.LeadId
                LEFT JOIN `Users` u ON p.UserId = u.UserId
                ORDER BY p.PaymentId DESC;";
            try
            {
                using (IDbConnection db = _context.CreateConnection())
                {
                    var result = await db.QueryAsync<PaymentEntry>(sql);
                    return result.ToList();
                }
            }
            catch (Exception e)
            {
                return new List<PaymentEntry>();
            }            
        }

        public async Task<bool> DeleteLedgerEntryAsync(int paymentId)
        {
            const string sql = "DELETE FROM Payments WHERE PaymentId = @PaymentId;";
            using (IDbConnection db = _context.CreateConnection())
            {
                int rows = await db.ExecuteAsync(sql, new { PaymentId = paymentId });
                return rows > 0;
            }
        }
    }
}
