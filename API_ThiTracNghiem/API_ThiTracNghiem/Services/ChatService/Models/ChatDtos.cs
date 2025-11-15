using System.ComponentModel.DataAnnotations;

namespace ChatService.Models
{
    public class SendMessageRequest
    {
        [Required]
        public string Content { get; set; } = string.Empty;
        
        public string MessageType { get; set; } = "text";
        public string? AttachmentUrl { get; set; }
        public string? AttachmentName { get; set; }
        public int? ReplyToMessageId { get; set; }
    }

    public class ChatMessageResponse
    {
        public int MessageId { get; set; }
        public int RoomId { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string? SenderAvatar { get; set; }
        public string Content { get; set; } = string.Empty;
        public string MessageType { get; set; } = string.Empty;
        public string? AttachmentUrl { get; set; }
        public string? AttachmentName { get; set; }
        public int? ReplyToMessageId { get; set; }
        public ChatMessageResponse? ReplyToMessage { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsEdited { get; set; }
        public DateTime? EditedAt { get; set; }
    }

    public class ChatRoomResponse
    {
        public int RoomId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string RoomType { get; set; } = string.Empty;
        // Loại bỏ CourseId và ExamId khỏi response
        public int CreatedBy { get; set; }
        public string CreatorName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public int MemberCount { get; set; }
        public ChatMessageResponse? LastMessage { get; set; }
    }

    public class ChatHistoryResponse
    {
        public ChatRoomResponse Room { get; set; } = new();
        public List<ChatMessageResponse> Messages { get; set; } = new();
        public int TotalMessages { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool HasNextPage { get; set; }
    }

    public class CreateRoomRequest
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        
        [MaxLength(1000)]
        public string? Description { get; set; }
        
        public string RoomType { get; set; } = "general";
    }

    public class JoinRoomRequest
    {
        public int UserId { get; set; }
        public string Role { get; set; } = "member";
    }

    public class UserResponse
    {
        public int UserId { get; set; }
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Status { get; set; }
        public bool IsOnline { get; set; }
    }

    // Feedback DTOs
    public class SubmitFeedbackRequest
    {
        [Required]
        [Range(1, 5)]
        public int Stars { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }
    }

    // Notifications DTOs
    public class NotificationResponse
    {
        public int NotificationId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Type { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
    }

    public class UpdateNotificationSettingsRequest
    {
        public bool EmailEnabled { get; set; }
        public bool PopupEnabled { get; set; }
    }

    // Reports DTOs
    public class CreateReportRequest
    {
        [Required]
        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;
    }

    public class ReportResponse
    {
        public int ReportId { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? AttachmentPath { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    // Admin Reports DTOs
    public class AdminReportResponse
    {
        public int ReportId { get; set; }
        public int UserId { get; set; }
        public string? UserEmail { get; set; }
        public string? UserFullName { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? AttachmentPath { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class UpdateReportStatusRequest
    {
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = string.Empty; // "Đang xử lý" hoặc "Đã xử lý"
    }
}