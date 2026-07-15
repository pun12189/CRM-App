using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Interfaces
{
    public interface ITwoFactorService
    {
        (string secretKey, string qrCodeUri) GenerateSetupInfo(string userEmail);
        bool VerifyCode(string secretKey, string code);
        bool VerifyAdminCode(string adminSecretKey, string code);
    }
}
