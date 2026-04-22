using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public partial class User : ObservableObject
    {
        public int UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? Role { get; set; }
        public bool IsActive { get; set; }
        public string? MetadataJson { get; set; }

        [NotMapped]
        public Dictionary<string, object> AdditionalInfo { get; set; } = new();

        public void LoadMetadata() =>
            AdditionalInfo = string.IsNullOrEmpty(MetadataJson)
                ? new() : JsonSerializer.Deserialize<Dictionary<string, object>>(MetadataJson)!;
    }
}
