using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_ThiTracNghiem.Models
{
    public class Certificate
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CertificateId { get; set; }

        public int UserId { get; set; }
        public int? CourseId { get; set; }
        public int? ExamId { get; set; }
        public int? ResultId { get; set; }

        [MaxLength(20)]
        public string? Grade { get; set; }

        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string? PdfUrl { get; set; }

        public int? CreatedBy { get; set; }

        // Navigation
        public User? User { get; set; }
        public Course? Course { get; set; }
        public Exam? Exam { get; set; }
        public Result? Result { get; set; }
        public User? Creator { get; set; }
    }
}


