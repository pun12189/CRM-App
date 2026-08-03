using Tijori.Interfaces;
using OtpNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Services
{
    public class TwoFactorService : ITwoFactorService
    {
        public (string secretKey, string qrCodeUri) GenerateSetupInfo(string userEmail)
        {
            byte[] secretBytes = KeyGeneration.GenerateRandomKey(20);
            string secretBase32 = Base32Encoding.ToString(secretBytes);
            string qrCodeUri = $"otpauth://totp/TIJORI:{userEmail}?secret={secretBase32}&issuer=TIJORI";
            return (secretBase32, qrCodeUri);
        }

        public bool VerifyCode(string secretKey, string code)
        {
            if (string.IsNullOrEmpty(secretKey) || string.IsNullOrEmpty(code)) return false;
            try
            {
                byte[] secretBytes = Base32Encoding.ToBytes(secretKey);
                var totp = new Totp(secretBytes, step: 30);
                return totp.VerifyTotp(code, out _, new VerificationWindow(2, 2));
            }
            catch { return false; }
        }

        public bool VerifyAdminCode(string adminSecretKey, string code)
        {
            if (string.IsNullOrEmpty(adminSecretKey) || string.IsNullOrEmpty(code))
                return false;

            try
            {
                byte[] secretBytes = Base32Encoding.ToBytes(adminSecretKey);
                var totp = new Totp(secretBytes, step: 30);

                // Using Window(2,2) to accommodate clock drift between the server and the admin's mobile phone over LAN
                return totp.VerifyTotp(code, out _, new VerificationWindow(2, 2));
            }
            catch
            {
                return false;
            }
        }
    }
}
