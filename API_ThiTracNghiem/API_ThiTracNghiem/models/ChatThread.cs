using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_ThiTracNghiem.Models
{
    public class ChatThread
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ThreadId { get; set; }

        public int? CourseId { get; set; }

        [MaxLength(200)]
        public string? Subject { get; set; }

        public int? CreatedBy { get; set; }

        public string? ParticipantsJson { get; set; }
        public DateTime? LastMessageAt { get; set; }

        [MaxLength(50)]
        public string? Status { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation
        public Course? Course { get; set; }
        public User? Creator { get; set; }
    }
}


