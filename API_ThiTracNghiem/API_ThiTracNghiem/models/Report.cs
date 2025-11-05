using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_ThiTracNghiem.Models
{
    public class Report
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ReportId { get; set; }

        public int ReporterId { get; set; }

        [MaxLength(50)]
        public string? TargetType { get; set; }

        public int? TargetId { get; set; }

        public string? Description { get; set; }
        public string? AttachmentsJson { get; set; }

        [MaxLength(50)]
        public string? Status { get; set; }

        public int? AssignedTo { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation
        public User? Reporter { get; set; }
        public User? Assignee { get; set; }
    }
}


