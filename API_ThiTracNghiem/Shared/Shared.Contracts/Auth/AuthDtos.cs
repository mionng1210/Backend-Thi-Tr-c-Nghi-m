using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Shared.Contracts.Auth;

public class RegisterRequest : IValidatableObject
{
    [Required(ErrorMessage = "Họ tên không được bỏ trống")]
    [MaxLength(150, ErrorMessage = "Họ tên tối đa 150 ký tự")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email không được bỏ trống")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Số điện thoại không được bỏ trống")]
    [RegularExpression("^(03|05|07|08|09)\\d{8,9}$", ErrorMessage = "Số điện thoại phải 10-11 số và bắt đầu bằng 03, 05, 07, 08, 09")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Giới tính không được bỏ trống")]
    [RegularExpression("^(Nam|Nữ)$", ErrorMessage = "Giới tính chỉ nhận 'Nam' hoặc 'Nữ'")]
    public string Gender { get; set; } = "Nam"; // Nam/Nữ

    [Required(ErrorMessage = "Ngày sinh không được bỏ trống")]
    public string DateOfBirth { get; set; } = string.Empty; // dd/MM/yyyy

    [Required(ErrorMessage = "Mật khẩu không được bỏ trống")]
    [MinLength(6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
    [RegularExpression("^(?=.*[A-Z])(?=.*[a-z])(?=.*\\d)(?=.*[!@#$%^&*()_+{}|:<>?~\\-=[\\]\\\\;\\'\",./]).{6,}$", ErrorMessage = "Mật khẩu cần ít nhất 1 chữ hoa, 1 chữ thường, 1 số và 1 ký tự đặc biệt")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Xác nhận mật khẩu không được bỏ trống")]
    [Compare("Password", ErrorMessage = "Xác nhận mật khẩu không khớp")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(DateOfBirth))
        {
            if (!DateTime.TryParseExact(DateOfBirth, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dob))
            {
                yield return new ValidationResult("Ngày sinh không đúng định dạng dd/MM/yyyy", new[] { nameof(DateOfBirth) });
            }
            else
            {
                var year = dob.Year;
                var currentYear = DateTime.UtcNow.Year;
                if (year < 1900 || year > currentYear)
                {
                    yield return new ValidationResult($"Năm sinh phải từ 1900 đến {currentYear}", new[] { nameof(DateOfBirth) });
                }
            }
        }
    }
}

public class VerifyOtpRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Otp { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Email không được bỏ trống")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    [Required(ErrorMessage = "Email không được bỏ trống")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mã OTP không được bỏ trống")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP phải có 6 chữ số")]
    public string Otp { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu mới không được bỏ trống")]
    [MinLength(6, ErrorMessage = "Mật khẩu mới tối thiểu 6 ký tự")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Xác nhận mật khẩu không được bỏ trống")]
    [Compare("NewPassword", ErrorMessage = "Xác nhận mật khẩu không khớp")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "Mật khẩu cũ không được bỏ trống")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu mới không được bỏ trống")]
    [MinLength(6, ErrorMessage = "Mật khẩu mới tối thiểu 6 ký tự")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Xác nhận mật khẩu không được bỏ trống")]
    [Compare("NewPassword", ErrorMessage = "Xác nhận mật khẩu không khớp")]
    public string ConfirmPassword { get; set; } = string.Empty;
}


