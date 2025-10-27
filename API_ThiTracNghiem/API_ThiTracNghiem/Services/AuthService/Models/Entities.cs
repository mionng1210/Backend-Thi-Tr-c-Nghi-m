using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthService.Models;

public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int UserId { get; set; }
    [MaxLength(256)]
    public string? Email { get; set; }
    [MaxLength(30)]
    public string? PhoneNumber { get; set; }
    [Required]
    [MaxLength(256)]
    public string PasswordHash { get; set; } = string.Empty;
    [MaxLength(150)]
    public string? FullName { get; set; }
    public int? RoleId { get; set; }
    [MaxLength(20)]
    public string? Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    [MaxLength(500)]
    public string? AvatarUrl { get; set; }
    [MaxLength(50)]
    public string? Status { get; set; }
    public bool IsEmailVerified { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool HasDelete { get; set; }
    public Role? Role { get; set; }
}

public class Role
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int RoleId { get; set; }
    [Required]
    [MaxLength(100)]
    public string RoleName { get; set; } = string.Empty;
    [MaxLength(500)]
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class OTP
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int OtpId { get; set; }
    public int UserId { get; set; }
    [Required]
    [MaxLength(10)]
    public string OtpCode { get; set; } = string.Empty;
    [MaxLength(50)]
    public string? Purpose { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User? User { get; set; }
}

public class AuthSession
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int SessionId { get; set; }
    public int UserId { get; set; }
    [MaxLength(300)]
    public string? DeviceInfo { get; set; }
    [MaxLength(45)]
    public string? IpAddress { get; set; }
    public DateTime LoginAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public User? User { get; set; }
}


