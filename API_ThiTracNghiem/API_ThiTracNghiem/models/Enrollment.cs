using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_ThiTracNghiem.Models
{
    public class Enrollment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int EnrollmentId { get; set; }

        public int UserId { get; set; }
        public int CourseId { get; set; }

        public DateTime EnrollmentDate { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiryDate { get; set; }

        [MaxLength(50)]
        public string? Status { get; set; }

        public int? ProgressPercent { get; set; }

        [MaxLength(100)]
        public string? PaymentTransactionId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation
        public User? User { get; set; }
        public Course? Course { get; set; }
    }
}


