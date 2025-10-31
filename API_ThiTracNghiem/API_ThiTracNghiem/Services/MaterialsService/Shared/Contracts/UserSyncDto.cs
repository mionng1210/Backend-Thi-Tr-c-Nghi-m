namespace API_ThiTracNghiem.Shared.Contracts
{
    /// <summary>
    /// DTO để đồng bộ thông tin user giữa các microservices
    /// </summary>
    public class UserSyncDto
    {
        public int UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }

    /// <summary>
    /// Request để lấy thông tin user từ token
    /// </summary>
    public class UserSyncRequest
    {
        public string Token { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response từ AuthService
    /// </summary>
    public class UserSyncResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public UserSyncDto? Data { get; set; }
    }
}