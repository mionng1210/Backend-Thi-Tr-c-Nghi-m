using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_ThiTracNghiem.Models
{
    public class ExamAttempt
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AttemptId { get; set; }

        public int ExamId { get; set; }
        public int UserId { get; set; }

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SubmittedAt { get; set; }
        public int? DurationSeconds { get; set; }

        [MaxLength(50)]
        public string? Status { get; set; }

        public decimal? AutoScore { get; set; }
        public decimal? ManualScore { get; set; }
        public decimal? FinalScore { get; set; }

        public int? GradedBy { get; set; }
        public DateTime? GradedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Exam? Exam { get; set; }
        public User? User { get; set; }
    }
}


