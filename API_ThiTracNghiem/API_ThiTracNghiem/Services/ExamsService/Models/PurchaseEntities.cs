using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExamsService.Models
{
    public class PaymentTransaction
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TransactionId { get; set; }

        [MaxLength(100)]
        public string? OrderId { get; set; }

        public int UserId { get; set; }

        public decimal Amount { get; set; }

        [MaxLength(10)]
        public string? Currency { get; set; }

        [MaxLength(50)]
        public string? Gateway { get; set; }

        [MaxLength(100)]
        public string? GatewayTransactionId { get; set; }

        [MaxLength(50)]
        public string? Status { get; set; }

        public string? QrCodeData { get; set; }
        public string? Payload { get; set; }

        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? User { get; set; }
    }

    public class ExamEnrollment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int EnrollmentId { get; set; }

        public int ExamId { get; set; }
        public int UserId { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "Active"; // Active, Pending, Cancelled

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool HasDelete { get; set; }

        // Navigation
        public Exam? Exam { get; set; }
        public User? User { get; set; }
    }
}