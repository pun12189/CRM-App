using CallMan.Data;
using CallMan.Models;
using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Services
{
    public class ProfileService
    {
        private readonly CrmDbContext _context;
        public ProfileService(CrmDbContext context) => _context = context;

        public async Task<CompanyProfile> GetProfileAsync()
        {
            using var db = _context.CreateConnection();
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

        public async Task SaveProfileAsync(CompanyProfile profile)
        {
            using var db = _context.CreateConnection();
            string sql = @"
            INSERT INTO CompanyProfile (DivisionId, LogoData, StampData, CompanyName, ProprietorName, GstNumber, PanNumber, 
                ContactNumber, OfficialEmail, BankName, AccountNumber, IfscCode, UpiId, 
                RegisteredAddress, CompanyInitials, InvoiceStartNumber, TermsAndConditions)
            VALUES (@DivisionId, @LogoData, @StampData, @CompanyName, @ProprietorName, @GstNumber, @PanNumber, 
                @ContactNumber, @OfficialEmail, @BankName, @AccountNumber, @IfscCode, @UpiId, 
                @RegisteredAddress, @CompanyInitials, @InvoiceStartNumber, @TermsAndConditions)
            ON DUPLICATE KEY UPDATE 
                DivisionId=@DivisionId, LogoData=@LogoData, StampData=@StampData, CompanyName=@CompanyName, ProprietorName=@ProprietorName, 
                GstNumber=@GstNumber, PanNumber=@PanNumber, ContactNumber=@ContactNumber, 
                OfficialEmail=@OfficialEmail, BankName=@BankName, AccountNumber=@AccountNumber, 
                IfscCode=@IfscCode, UpiId=@UpiId, RegisteredAddress=@RegisteredAddress, 
                CompanyInitials=@CompanyInitials, InvoiceStartNumber=@InvoiceStartNumber, 
                TermsAndConditions=@TermsAndConditions";

            await db.ExecuteAsync(sql, profile);
        }
    }
}
