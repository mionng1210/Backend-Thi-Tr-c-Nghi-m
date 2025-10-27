using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_ThiTracNghiem.Models
{
    public class NotificationSetting
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SettingId { get; set; }

        public int UserId { get; set; }

        public bool EmailNotificationsEnabled { get; set; }
        public bool PushNotificationsEnabled { get; set; }
        public bool SmsNotificationsEnabled { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Navigation
        public User? User { get; set; }
    }
}


