using System;
using System.ComponentModel.DataAnnotations;

namespace ExamsService.DTOs
{
    public class PurchaseExamRequest
    {
        [MaxLength(50, ErrorMessage = "Gateway không được vượt quá 50 ký tự")]
        public string? Gateway { get; set; } = "VNPay";

        [MaxLength(10, ErrorMessage = "Currency không được vượt quá 10 ký tự")]
        public string? Currency { get; set; } = "VND";

        // Dùng cho môi trường demo để giả lập thanh toán thành công ngay
        public bool SimulateSuccess { get; set; } = true;
    }

    public class PurchaseExamResponse
    {
        public int TransactionId { get; set; }
        public string OrderId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Gateway { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? QrCodeData { get; set; }
        public string? PaymentUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public ExamPurchaseInfo Exam { get; set; } = new();
        public string EnrollmentStatus { get; set; } = string.Empty; // Active, Pending
    }

    public class ExamPurchaseInfo
    {
        public int ExamId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public string? CourseTitle { get; set; }
        public decimal Price { get; set; }
        public bool IsCourseFree { get; set; }
    }
}