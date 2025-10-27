using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_ThiTracNghiem.Models
{
    public class Exam
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ExamId { get; set; }

        public int? CourseId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        public int? DurationMinutes { get; set; }
        public int? TotalQuestions { get; set; }
        public decimal? TotalMarks { get; set; }
        public decimal? PassingMark { get; set; }

        [MaxLength(50)]
        public string? ExamType { get; set; }

        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }

        public bool RandomizeQuestions { get; set; }
        public bool AllowMultipleAttempts { get; set; }

        [MaxLength(50)]
        public string? Status { get; set; }

        public int? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool HasDelete { get; set; }

        // Navigation
        public Course? Course { get; set; }
        public User? Creator { get; set; }
    }
}


