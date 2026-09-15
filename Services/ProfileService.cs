using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tijori.Data;
using Tijori.Models;

namespace Tijori.Services
{
    public class ProfileService
    {
        private readonly CrmDbContext _context;
        public ProfileService(CrmDbContext context) => _context = context;

        public async Task<CompanyProfile> GetProfileAsync()
        {
            using var db = _context.CreateConnection();
            if (db.State == ConnectionState.Closed) db.Open();
            string sql = "SELECT * FROM CompanyProfile WHERE Id = 1";
            var profile = await db.QueryFirstOrDefaultAsync<CompanyProfile>(sql) ?? new CompanyProfile();

            // Convert the BLOB data to BitmapSource after fetching
            if (profile.LogoData != null)
            {
                profile.LogoImage = Helper.Helper.ToBitmapSource(profile.LogoData);
            }
            return profile;
        }

        public async Task<IEnumerable<Division>> GetActiveDivisionsAsync()
        {
            using var conn = _context.CreateConnection();
            return await conn.QueryAsync<Division>("SELECT * FROM Divisions WHERE IsActive = 1");
        }

        public async Task<CompanyProfile> GetProfileByDivisionAsync(int divisionId)
        {
            using var conn = _context.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<CompanyProfile>("SELECT * FROM CompanyProfile WHERE DivisionId = @divId", new { divId = divisionId });
        }

        public async Task<int> CreateDivisionAsync(Division div)
        {
            using var conn = _context.CreateConnection();
            string sql = "INSERT INTO Divisions (Name, IsActive) VALUES (@Name, @IsActive); SELECT LAST_INSERT_ID();";
            return await conn.ExecuteScalarAsync<int>(sql, div);
        }

        public async Task InitializeBlankProfileAsync(int divId, string name)
        {
            using var conn = _context.CreateConnection();
            string sql = @"INSERT INTO CompanyProfile (DivisionId, CompanyName) 
                   VALUES (@divId, @name)";
            await conn.ExecuteAsync(sql, new { divId, name });
        }

        public async Task<bool> SaveProfileAsync(CompanyProfile profile)
        {
            using var conn = _context.CreateConnection();
            if (conn.State == ConnectionState.Closed)
                conn.Open();

            using var tx = conn.BeginTransaction();
            try
            {
                const string sql = @"
            INSERT INTO companyprofile (
                DivisionId, LogoData, StampData, CompanyName, ProprietorName, GstNumber, PanNumber, 
                ContactNumber, OfficialEmail, BankName, AccountNumber, IfscCode, UpiId, 
                RegisteredAddress, CompanyInitials, InvoiceStartNumber, TermsAndConditions
            )
            VALUES (
                @DivisionId, @LogoData, @StampData, @CompanyName, @ProprietorName, @GstNumber, @PanNumber, 
                @ContactNumber, @OfficialEmail, @BankName, @AccountNumber, @IfscCode, @UpiId, 
                @RegisteredAddress, @CompanyInitials, @InvoiceStartNumber, @TermsAndConditions
            )
            ON DUPLICATE KEY UPDATE 
                LogoData = COALESCE(@LogoData, LogoData),
                StampData = COALESCE(@StampData, StampData),
                CompanyName = @CompanyName,
                ProprietorName = @ProprietorName, 
                GstNumber = @GstNumber,
                PanNumber = @PanNumber,
                ContactNumber = @ContactNumber, 
                OfficialEmail = @OfficialEmail,
                BankName = @BankName,
                AccountNumber = @AccountNumber, 
                IfscCode = @IfscCode,
                UpiId = @UpiId,
                RegisteredAddress = @RegisteredAddress, 
                CompanyInitials = @CompanyInitials,
                InvoiceStartNumber = @InvoiceStartNumber, 
                TermsAndConditions = @TermsAndConditions;";

                int affected = await conn.ExecuteAsync(sql, profile, tx);

                tx.Commit();
                return affected > 0;
            }
            catch (Exception ex)
            {
                tx.Rollback();
                System.Diagnostics.Debug.WriteLine($"[SaveProfileAsync ERROR]: {ex.Message}");
                return false;
            }
        }

        public async Task<IEnumerable<DivisionListItem>> GetAllDivisionListItemsAsync()
        {
            using var conn = _context.CreateConnection();
            const string sql = @"
        SELECT 
            d.Id,
            d.Name,
            d.IsActive,
            d.CreatedAt,
            cp.Id AS ProfileId,
            cp.LogoData,
            cp.CompanyName,
            cp.GstNumber,
            cp.ContactNumber,
            cp.OfficialEmail,
            cp.CompanyInitials
        FROM divisions d
        LEFT JOIN (
            -- Subquery guarantees only 1 profile per division is joined
            SELECT *
            FROM companyprofile
            WHERE Id IN (
                SELECT MAX(Id)
                FROM companyprofile
                GROUP BY DivisionId
            )
        ) cp ON d.Id = cp.DivisionId
        ORDER BY d.Id DESC;";

            var list = (await conn.QueryAsync<DivisionListItem>(sql)).ToList();

            foreach (var item in list)
            {
                if (item.LogoData != null && item.LogoData.Length > 0)
                {
                    item.LogoImage = Helper.Helper.ToBitmapSource(item.LogoData);
                }
            }

            return list;
        }

        public async Task<(bool Success, string Message)> DeleteDivisionAsync(int divisionId)
        {
            using var conn = _context.CreateConnection();
            if (conn.State == ConnectionState.Closed) conn.Open();            

            // 3. If zero references exist, perform hard delete safely
            using var tx = conn.BeginTransaction();
            try
            {
                await conn.ExecuteAsync("DELETE FROM companyprofile WHERE DivisionId = @divisionId", new { divisionId }, tx);
                int affected = await conn.ExecuteAsync("DELETE FROM divisions WHERE Id = @divisionId", new { divisionId }, tx);
                tx.Commit();

                return (affected > 0, "Division deleted successfully.");
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task<int> GetDivisionLinkedCountAsync(int divisionId)
        {
            using var conn = _context.CreateConnection();
            const string sql = @"
        SELECT 
            (SELECT COUNT(1) FROM orders WHERE DivisionId = @divisionId) +
            (SELECT COUNT(1) FROM payments WHERE DivisionId = @divisionId) +
            (SELECT COUNT(1) FROM products WHERE DivisionId = @divisionId) +
            (SELECT COUNT(1) FROM leaddivisions WHERE DivisionId = @divisionId);";

            return await conn.ExecuteScalarAsync<int>(sql, new { divisionId });
        }

        public async Task<bool> DeactivateDivisionAsync(int divisionId)
        {
            using var conn = _context.CreateConnection();
            const string sql = "UPDATE divisions SET IsActive = 0 WHERE Id = @divisionId;";
            return await conn.ExecuteAsync(sql, new { divisionId }) > 0;
        }

        public async Task<bool> CascadeDeleteDivisionAsync(int divisionId)
        {
            using var conn = _context.CreateConnection();
            if (conn.State == ConnectionState.Closed) conn.Open();

            using var tx = conn.BeginTransaction();
            try
            {
                // 1. Delete junction links
                await conn.ExecuteAsync("DELETE FROM leaddivisions WHERE DivisionId = @divisionId", new { divisionId }, tx);

                // 2. Unlink transactions and catalog products (set to NULL to preserve data)
                await conn.ExecuteAsync("UPDATE orders SET DivisionId = NULL WHERE DivisionId = @divisionId", new { divisionId }, tx);
                await conn.ExecuteAsync("UPDATE payments SET DivisionId = NULL WHERE DivisionId = @divisionId", new { divisionId }, tx);
                await conn.ExecuteAsync("UPDATE products SET DivisionId = NULL WHERE DivisionId = @divisionId", new { divisionId }, tx);

                // 3. Delete company profile
                await conn.ExecuteAsync("DELETE FROM companyprofile WHERE DivisionId = @divisionId", new { divisionId }, tx);

                // 4. Delete the division row
                int affected = await conn.ExecuteAsync("DELETE FROM divisions WHERE Id = @divisionId", new { divisionId }, tx);

                tx.Commit();
                return affected > 0;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task<bool> ToggleDivisionStatusAsync(int divisionId, bool isActive)
        {
            using var conn = _context.CreateConnection();
            if (conn.State == ConnectionState.Closed) conn.Open();
            const string sql = "UPDATE divisions SET IsActive = @isActive WHERE Id = @divisionId;";
            return await conn.ExecuteAsync(sql, new { divisionId, isActive }) > 0;
        }

        public async Task<bool> UpdateDivisionNameAsync(int divisionId, string name)
        {
            using var conn = _context.CreateConnection();
            const string sql = "UPDATE divisions SET Name = @name WHERE Id = @divisionId;";
            return await conn.ExecuteAsync(sql, new { divisionId, name }) > 0;
        }
    }
}
