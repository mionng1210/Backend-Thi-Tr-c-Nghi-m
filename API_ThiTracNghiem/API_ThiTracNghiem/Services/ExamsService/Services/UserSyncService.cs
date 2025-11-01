using System.Text.Json;
using API_ThiTracNghiem.Shared.Contracts;

namespace API_ThiTracNghiem.Services
{
    /// <summary>
    /// Interface cho User Sync Service
    /// </summary>
    public interface IUserSyncService
    {
        Task<UserSyncDto?> GetUserByIdAsync(int userId);
        Task<UserSyncDto?> GetUserByEmailAsync(string email);
        Task<UserSyncDto?> GetUserFromTokenAsync(string token);
        Task<bool> ValidateUserPermissionAsync(int userId, string requiredRole);
    }

    /// <summary>
    /// Service để đồng bộ thông tin user giữa các microservices
    /// </summary>
    public class UserSyncService : IUserSyncService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserSyncService> _logger;

        public UserSyncService(HttpClient httpClient, IConfiguration configuration, ILogger<UserSyncService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Lấy thông tin user theo ID từ AuthService
        /// </summary>
        public async Task<UserSyncDto?> GetUserByIdAsync(int userId)
        {
            try
            {
                var authServiceUrl = _configuration["Services:AuthService:BaseUrl"];
                if (string.IsNullOrEmpty(authServiceUrl))
                {
                    _logger.LogError("AuthService BaseUrl not configured");
                    return null;
                }

                var response = await _httpClient.GetAsync($"{authServiceUrl}/api/UserSync/{userId}");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<UserSyncResponse>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    return result?.User;
                }

                _logger.LogWarning($"Failed to get user {userId} from AuthService: {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting user {userId} from AuthService");
                return null;
            }
        }

        /// <summary>
        /// Lấy thông tin user theo email từ AuthService
        /// </summary>
        public async Task<UserSyncDto?> GetUserByEmailAsync(string email)
        {
            try
            {
                var authServiceUrl = _configuration["Services:AuthService:BaseUrl"];
                if (string.IsNullOrEmpty(authServiceUrl))
                {
                    _logger.LogError("AuthService BaseUrl not configured");
                    return null;
                }

                var response = await _httpClient.GetAsync($"{authServiceUrl}/api/UserSync/user/email/{Uri.EscapeDataString(email)}");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<UserSyncResponse>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    return result?.User;
                }

                _logger.LogWarning($"Failed to get user {email} from AuthService: {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting user {email} from AuthService");
                return null;
            }
        }

        /// <summary>
        /// Lấy thông tin user từ JWT token
        /// </summary>
        public async Task<UserSyncDto?> GetUserFromTokenAsync(string token)
        {
            try
            {
                var authServiceUrl = _configuration["Services:AuthService:BaseUrl"];
                if (string.IsNullOrEmpty(authServiceUrl))
                {
                    _logger.LogError("AuthService BaseUrl not configured");
                    return null;
                }

                // Set Authorization header with the token
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.GetAsync($"{authServiceUrl}/api/UserSync/user/current");
                
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<UserSyncResponse>(responseJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    return result?.User;
                }

                _logger.LogWarning($"Failed to get user from token from AuthService: {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user from token from AuthService");
                return null;
            }
            finally
            {
                // Clear the Authorization header to avoid affecting other requests
                _httpClient.DefaultRequestHeaders.Authorization = null;
            }
        }

        /// <summary>
        /// Kiểm tra quyền của user
        /// </summary>
        public async Task<bool> ValidateUserPermissionAsync(int userId, string requiredRole)
        {
            try
            {
                var authServiceUrl = _configuration["Services:AuthService:BaseUrl"];
                if (string.IsNullOrEmpty(authServiceUrl))
                {
                    _logger.LogError("AuthService BaseUrl not configured");
                    return false;
                }

                var request = new { UserId = userId, RequiredRole = requiredRole };
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{authServiceUrl}/api/UserSync/validate-permission", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
                    
                    if (result.TryGetProperty("hasPermission", out var hasPermissionElement))
                    {
                        return hasPermissionElement.GetBoolean();
                    }
                }

                _logger.LogWarning($"Failed to validate permission for user {userId} from AuthService: {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating permission for user {userId} from AuthService");
                return false;
            }
        }
    }
}