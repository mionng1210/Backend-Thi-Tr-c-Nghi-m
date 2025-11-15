using System.ComponentModel.DataAnnotations;

namespace API_ThiTracNghiem.Services.AuthService.DTOs;

public class RequestTeacherRoleRequest : IValidatableObject
{
    // Hồ sơ (optional - dùng để cập nhật nhanh)
    [MaxLength(150)]
    public string? FullName { get; set; }

    [RegularExpression("^(Nam|Nữ)$", ErrorMessage = "Giới tính chỉ nhận 'Nam' hoặc 'Nữ'")]
    public string? Gender { get; set; }

    // dd/MM/yyyy
    public string? DateOfBirth { get; set; }

    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    // Ngân hàng (bắt buộc)
    [Required]
    [MaxLength(200)]
    public string BankName { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string BankAccountName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string BankAccountNumber { get; set; } = string.Empty;

    // Thanh toán (optional)
    [MaxLength(50)]
    public string? PaymentMethod { get; set; } // e.g., Momo, BankTransfer, None

    [MaxLength(100)]
    public string? PaymentReference { get; set; }

    [MaxLength(30)]
    public string? PaymentStatus { get; set; } // paid | pending | none

    public decimal? PaymentAmount { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Nếu có PaymentMethod mà không có PaymentStatus, set mặc định pending
        if (!string.IsNullOrWhiteSpace(PaymentMethod) && string.IsNullOrWhiteSpace(PaymentStatus))
        {
            PaymentStatus = "pending";
        }
        return Array.Empty<ValidationResult>();
    }
}