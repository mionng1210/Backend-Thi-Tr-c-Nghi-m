using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace API_ThiTracNghiem.Services.AuthService.DTOs
{
    public class GetUserResponse
    {
        public int UserId { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? FullName { get; set; }
        public int? RoleId { get; set; }
        public string? RoleName { get; set; }
        public string? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Status { get; set; }
        public bool IsEmailVerified { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }

    public class UpdateUserRequest : IValidatableObject
    {
        [MaxLength(150, ErrorMessage = "Họ tên tối đa 150 ký tự")]
        public string? FullName { get; set; }

        [RegularExpression("^(Nam|Nữ)$", ErrorMessage = "Giới tính chỉ nhận 'Nam' hoặc 'Nữ'")]
        public string? Gender { get; set; }

        public string? DateOfBirth { get; set; } // dd/MM/yyyy format

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            // Validate DateOfBirth format if provided
            if (!string.IsNullOrWhiteSpace(DateOfBirth))
            {
                if (!DateTime.TryParseExact(DateOfBirth, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                {
                    results.Add(new ValidationResult("Ngày sinh phải có định dạng dd/MM/yyyy", new[] { nameof(DateOfBirth) }));
                }
                else
                {
                    // Check if date is not in the future
                    if (parsedDate > DateTime.Now)
                    {
                        results.Add(new ValidationResult("Ngày sinh không thể là ngày trong tương lai", new[] { nameof(DateOfBirth) }));
                    }

                    // Check if age is reasonable (not older than 120 years)
                    if (parsedDate < DateTime.Now.AddYears(-120))
                    {
                        results.Add(new ValidationResult("Ngày sinh không hợp lệ", new[] { nameof(DateOfBirth) }));
                    }
                }
            }

            return results;
        }
    }

    // Admin DTOs
    public class GetAllUsersResponse
    {
        public List<GetUserResponse> Users { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class AdminUpdateUserRequest : IValidatableObject
    {
        [MaxLength(256, ErrorMessage = "Email tối đa 256 ký tự")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string? Email { get; set; }

        [MaxLength(30, ErrorMessage = "Số điện thoại tối đa 30 ký tự")]
        public string? PhoneNumber { get; set; }

        [MaxLength(150, ErrorMessage = "Họ tên tối đa 150 ký tự")]
        public string? FullName { get; set; }

        public int? RoleId { get; set; }

        [RegularExpression("^(Nam|Nữ)$", ErrorMessage = "Giới tính chỉ nhận 'Nam' hoặc 'Nữ'")]
        public string? Gender { get; set; }

        public string? DateOfBirth { get; set; } // dd/MM/yyyy format

        [RegularExpression("^(active|inactive|banned)$", ErrorMessage = "Trạng thái chỉ nhận 'active', 'inactive' hoặc 'banned'")]
        public string? Status { get; set; }

        public bool? IsEmailVerified { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            // Validate DateOfBirth format if provided
            if (!string.IsNullOrWhiteSpace(DateOfBirth))
            {
                if (!DateTime.TryParseExact(DateOfBirth, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                {
                    results.Add(new ValidationResult("Ngày sinh phải có định dạng dd/MM/yyyy", new[] { nameof(DateOfBirth) }));
                }
                else
                {
                    // Check if date is not in the future
                    if (parsedDate > DateTime.Now)
                    {
                        results.Add(new ValidationResult("Ngày sinh không thể là ngày trong tương lai", new[] { nameof(DateOfBirth) }));
                    }

                    // Check if age is reasonable (not older than 120 years)
                    if (parsedDate < DateTime.Now.AddYears(-120))
                    {
                        results.Add(new ValidationResult("Ngày sinh không hợp lệ", new[] { nameof(DateOfBirth) }));
                    }
                }
            }

            return results;
        }
    }

    // Admin: Cập nhật vai trò theo tên vai trò
    public class AdminUpdateUserRoleRequest
    {
        [Required]
        [MaxLength(50, ErrorMessage = "Tên vai trò tối đa 50 ký tự")]
        public string Role { get; set; } = string.Empty; // Ví dụ: "student", "teacher", "admin"
    }
}