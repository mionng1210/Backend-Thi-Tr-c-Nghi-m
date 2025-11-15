using System.Text.Json.Serialization;

namespace API_ThiTracNghiem.Services.AuthService.DTOs
{
    public class AdminStatisticsResponse
    {
        public UserStatsDto Users { get; set; } = new();
        public PermissionStatsDto Permissions { get; set; } = new();
        public RevenueStatsDto Revenue { get; set; } = new();
        public ExamStatsDto? Exam { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    public class UserStatsDto
    {
        public int TotalUsers { get; set; }
        public int TotalStudents { get; set; }
        public int TotalTeachers { get; set; }
        public int TotalAdmins { get; set; }
        public int NewUsersLast7Days { get; set; }
    }

    public class PermissionStatsDto
    {
        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
        public decimal PaidAmount { get; set; }
    }

    public class RevenueStatsDto
    {
        // Hiện tại chỉ lấy doanh thu từ PaymentAmount của PermissionRequests
        public decimal TotalRevenue { get; set; }
        public string Currency { get; set; } = "VND";
        public string Notes { get; set; } = "Bao gồm khoản phí duyệt quyền từ PermissionRequests";
    }

    public class ExamStatsDto
    {
        public int ExamId { get; set; }
        public string? ExamTitle { get; set; }
        public string? CourseName { get; set; }
        public string? SubjectName { get; set; }

        public int TotalStudents { get; set; }
        public int PassedStudents { get; set; }
        public int FailedStudents { get; set; }
        public double PassRate { get; set; }
        public decimal AverageScore { get; set; }
        public decimal HighestScore { get; set; }
        public decimal LowestScore { get; set; }
    }
}