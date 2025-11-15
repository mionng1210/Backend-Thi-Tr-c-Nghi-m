using System;

namespace API_ThiTracNghiem.Contracts
{
    /// <summary>
    /// DTO để đồng bộ thông tin User giữa các microservices
    /// </summary>
    public class UserSyncDto
    {
        public int UserId { get; set; }
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public int? RoleId { get; set; }
        public string? RoleName { get; set; }
        public string? Status { get; set; }
        public bool IsEmailVerified { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool HasDelete { get; set; }
    }

    /// <summary>
    /// Response cho API đồng bộ User
    /// </summary>
    public class UserSyncResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public UserSyncDto? User { get; set; }
        public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Request để lấy thông tin User từ AuthService
    /// </summary>
    public class UserSyncRequest
    {
        public int? UserId { get; set; }
        public string? Email { get; set; }
        public string? Token { get; set; }
    }
}