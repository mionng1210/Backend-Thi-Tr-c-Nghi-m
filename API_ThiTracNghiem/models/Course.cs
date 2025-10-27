using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_ThiTracNghiem.Models
{
    public class Course
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CourseId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        public int? TeacherId { get; set; }
        public int? SubjectId { get; set; }

        public decimal? Price { get; set; }
        public bool IsFree { get; set; }

        [MaxLength(500)]
        public string? ThumbnailUrl { get; set; }

        public int? DurationMinutes { get; set; }

        [MaxLength(50)]
        public string? Level { get; set; }

        [MaxLength(50)]
        public string? Status { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool HasDelete { get; set; }

        // Navigation
        public User? Teacher { get; set; }
        public Subject? Subject { get; set; }
    }
}


