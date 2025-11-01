using API_ThiTracNghiem.Contracts;
using System.Text.Json;

namespace API_ThiTracNghiem.Services
{
    /// <summary>
    /// Service để đồng bộ thông tin User giữa các microservices
    /// </summary>
    public interface IUserSyncService
    {
        Task<UserSyncDto?> GetUserByIdAsync(int userId);
        Task<UserSyncDto?> GetUserByEmailAsync(string email);
        Task<UserSyncDto?> GetUserFromTokenAsync(string token);
        Task<bool> ValidateUserPermissionAsync(int userId, string requiredRole);
    }

    public class UserSyncService : IUserSyncService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly string _authServiceUrl;

        public UserSyncService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
            _authServiceUrl = _config["Services:AuthService:BaseUrl"] ?? "http://localhost:5001";
        }

        /// <summary>
        /// Lấy thông tin User từ AuthService theo UserId
        /// </summary>
        public async Task<UserSyncDto?> GetUserByIdAsync(int userId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_authServiceUrl}/api/UserSync/user/{userId}");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var syncResponse = JsonSerializer.Deserialize<UserSyncResponse>(json, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    
                    return syncResponse?.User;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Error syncing user {userId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Lấy thông tin User từ AuthService theo Email
        /// </summary>
        public async Task<UserSyncDto?> GetUserByEmailAsync(string email)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_authServiceUrl}/api/UserSync/user/email/{email}");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var syncResponse = JsonSerializer.Deserialize<UserSyncResponse>(json, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    
                    return syncResponse?.User;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Error syncing user by email {email}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Lấy thông tin User từ JWT Token
        /// </summary>
        public async Task<UserSyncDto?> GetUserFromTokenAsync(string token)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.GetAsync($"{_authServiceUrl}/api/UserSync/user/current");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var syncResponse = JsonSerializer.Deserialize<UserSyncResponse>(json, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    
                    return syncResponse?.User;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Error syncing user from token: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Kiểm tra quyền của User
        /// </summary>
        public async Task<bool> ValidateUserPermissionAsync(int userId, string requiredRole)
        {
            var user = await GetUserByIdAsync(userId);
            
            if (user == null || user.HasDelete || user.Status != "Active")
                return false;

            return user.RoleName?.ToLower() == requiredRole.ToLower() || 
                   user.RoleName?.ToLower() == "admin"; // Admin có tất cả quyền
        }
    }
}