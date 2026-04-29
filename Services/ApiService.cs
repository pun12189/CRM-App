using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Services
{
    using CallMan.Models;
    using System.Net.Http;
    using System.Net.Http.Json;

    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private const string ApiBaseUrl = "https://klikcrm.com/api/bahikitab/";

        public ApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<MasterAdminData?> CheckMasterAdminAsync(string email, string password)
        {
            try
            {
                var loginData = new { Email = email, Password = password };
                var response = await _httpClient.PostAsJsonAsync($"{ApiBaseUrl}auth/login", loginData);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<MasterAdminResponse>();

                    if (result != null && result.Status)
                    {
                        return result.Data; // Success: Return the admin details
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error: API might be unreachable or timed out
                System.Diagnostics.Debug.WriteLine($"API Error: {ex.Message}");
            }
            return null;
        }
    }
}
