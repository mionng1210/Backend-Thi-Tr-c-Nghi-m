using System.ComponentModel.DataAnnotations;

namespace API_ThiTracNghiem.Services.AuthService.DTOs;

public class PermissionRequestItem
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public int RequestedRoleId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedById { get; set; }
    public string? RejectReason { get; set; }
}

public class RejectPermissionRequest
{
    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}