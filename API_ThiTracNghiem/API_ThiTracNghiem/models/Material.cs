using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_ThiTracNghiem.Models
{
    public class Material
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MaterialId { get; set; }

        public int CourseId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        [MaxLength(50)]
        public string? MediaType { get; set; }

        [MaxLength(500)]
        public string? FileUrl { get; set; }

        [MaxLength(500)]
        public string? ExternalLink { get; set; }

        public bool IsPaid { get; set; }
        public decimal? Price { get; set; }
        public int? OrderIndex { get; set; }
        public int? DurationSeconds { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool HasDelete { get; set; }

        // Navigation
        public Course? Course { get; set; }
    }
}


