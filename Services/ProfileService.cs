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

        public async Task<bool> SaveProfileAsync(CompanyProfile profile)
        {
            using var db = _context.CreateConnection();
            string sql = @"
            INSERT INTO CompanyProfile (Id, LogoData, CompanyName, ProprietorName, GstNumber, PanNumber, 
                ContactNumber, OfficialEmail, BankName, AccountNumber, IfscCode, UpiId, 
                RegisteredAddress, CompanyInitials, InvoiceStartNumber, TermsAndConditions)
            VALUES (1, @LogoData, @CompanyName, @ProprietorName, @GstNumber, @PanNumber, 
                @ContactNumber, @OfficialEmail, @BankName, @AccountNumber, @IfscCode, @UpiId, 
                @RegisteredAddress, @CompanyInitials, @InvoiceStartNumber, @TermsAndConditions)
            ON DUPLICATE KEY UPDATE 
                LogoData=@LogoData, CompanyName=@CompanyName, ProprietorName=@ProprietorName, 
                GstNumber=@GstNumber, PanNumber=@PanNumber, ContactNumber=@ContactNumber, 
                OfficialEmail=@OfficialEmail, BankName=@BankName, AccountNumber=@AccountNumber, 
                IfscCode=@IfscCode, UpiId=@UpiId, RegisteredAddress=@RegisteredAddress, 
                CompanyInitials=@CompanyInitials, InvoiceStartNumber=@InvoiceStartNumber, 
                TermsAndConditions=@TermsAndConditions";

            return await db.ExecuteAsync(sql, profile) > 0;
        }
    }
}
