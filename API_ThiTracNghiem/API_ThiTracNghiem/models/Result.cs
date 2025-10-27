using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_ThiTracNghiem.Models
{
    public class Result
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ResultId { get; set; }

        public int AttemptId { get; set; }
        public int UserId { get; set; }
        public int ExamId { get; set; }

        public decimal? TotalScore { get; set; }

        [MaxLength(50)]
        public string? Status { get; set; }

        public string? Feedback { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ExamAttempt? Attempt { get; set; }
        public User? User { get; set; }
        public Exam? Exam { get; set; }
    }
}


