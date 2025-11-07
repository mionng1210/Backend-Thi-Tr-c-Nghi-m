using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChatService.Models
{
    public class ChatRoom
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RoomId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(50)]
        public string RoomType { get; set; } = "general"; // general, course, exam, private

        public int? CourseId { get; set; }
        public int? ExamId { get; set; }

        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
        public bool HasDelete { get; set; } = false;

        // Navigation
        public User? Creator { get; set; }
        public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
        public ICollection<ChatRoomMember> Members { get; set; } = new List<ChatRoomMember>();
    }

    public class ChatMessage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MessageId { get; set; }

        public int RoomId { get; set; }
        public int SenderId { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        [MaxLength(50)]
        public string MessageType { get; set; } = "text"; // text, image, file, system

        public string? AttachmentUrl { get; set; }
        public string? AttachmentName { get; set; }

        public int? ReplyToMessageId { get; set; }

        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public bool IsEdited { get; set; } = false;
        public DateTime? EditedAt { get; set; }
        public bool HasDelete { get; set; } = false;

        // Navigation
        public ChatRoom? Room { get; set; }
        public User? Sender { get; set; }
        public ChatMessage? ReplyToMessage { get; set; }
        public ICollection<ChatMessage> Replies { get; set; } = new List<ChatMessage>();
    }

    public class ChatRoomMember
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MemberId { get; set; }

        public int RoomId { get; set; }
        public int UserId { get; set; }

        [MaxLength(50)]
        public string Role { get; set; } = "member"; // admin, moderator, member

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastSeenAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation
        public ChatRoom? Room { get; set; }
        public User? User { get; set; }
    }

    // User model for reference (synced from AuthService)
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UserId { get; set; }

        [MaxLength(256)]
        public string? Email { get; set; }

        [MaxLength(30)]
        public string? PhoneNumber { get; set; }

        [Required]
        [MaxLength(256)]
        public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? FullName { get; set; }

        public int? RoleId { get; set; }

        [MaxLength(20)]
        public string? Gender { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [MaxLength(500)]
        public string? AvatarUrl { get; set; }

        [MaxLength(500)]
        public string? Address { get; set; }

        public bool IsEmailVerified { get; set; }
        public bool IsPhoneVerified { get; set; }

        [MaxLength(50)]
        public string? Status { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool HasDelete { get; set; }

        // Navigation
        public Role? Role { get; set; }
    }

    public class Role
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RoleId { get; set; }

        [Required]
        [MaxLength(50)]
        public string RoleName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool HasDelete { get; set; }

        // Navigation
        public ICollection<User> Users { get; set; } = new List<User>();
    }

    // Feedback entity
    public class Feedback
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int FeedbackId { get; set; }

        public int UserId { get; set; }

        [Range(1, 5)]
        public int Stars { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool HasDelete { get; set; } = false;
    }

    // Notification entity
    public class Notification
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int NotificationId { get; set; }

        public int UserId { get; set; }

        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string Message { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Type { get; set; }

        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool HasDelete { get; set; } = false;
    }

    // Notification settings entity (one per user)
    public class NotificationSetting
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SettingId { get; set; }

        public int UserId { get; set; }
        public bool EmailEnabled { get; set; } = true;
        public bool PopupEnabled { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    // Report entity
    public class Report
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ReportId { get; set; }

        public int UserId { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? AttachmentPath { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "Chưa xử lý";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool HasDelete { get; set; } = false;
    }
}