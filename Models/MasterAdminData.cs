using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public class MasterAdminData
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("phone")]
        public string? Phone { get; set; }

        [JsonPropertyName("role_name")]
        public string RoleName { get; set; } = string.Empty;

        [JsonPropertyName("member_since")]
        public string? MemberSince { get; set; }

        [JsonPropertyName("user_limit")]
        public int UserLimit { get; set; }

        [JsonPropertyName("expiry_date")]
        public DateTime? ExpiryDate { get; set; }
    }
}
