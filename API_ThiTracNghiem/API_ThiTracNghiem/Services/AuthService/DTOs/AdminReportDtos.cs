using System.ComponentModel.DataAnnotations;

namespace API_ThiTracNghiem.Services.AuthService.DTOs
{
    public class UpdateReportStatusRequest
    {
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = string.Empty; // "Đang xử lý" hoặc "Đã xử lý"
    }
}