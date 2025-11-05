using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_ThiTracNghiem.Models
{
    public class Notification
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int NotificationId { get; set; }

        public int UserId { get; set; }

        [MaxLength(50)]
        public string? Type { get; set; }

        [MaxLength(200)]
        public string? Title { get; set; }

        public string? Body { get; set; }
        public string? MetaJson { get; set; }
        public bool IsRead { get; set; }
        public bool? SendEmail { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? User { get; set; }
    }
}


