using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExamsService.Models
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
        public bool HasDelete { get; set; }

        // Navigation
        public Course? Course { get; set; }
        public User? Creator { get; set; }
    }

    public class ExamQuestion
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ExamQuestionId { get; set; }

        public int ExamId { get; set; }
        public int QuestionId { get; set; }
        public int? SequenceIndex { get; set; }
        public decimal? Marks { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool HasDelete { get; set; }

        // Navigation
        public Exam? Exam { get; set; }
        public Question? Question { get; set; }
    }

    public class Question
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int QuestionId { get; set; }

        public int BankId { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? QuestionType { get; set; }

        [MaxLength(50)]
        public string? Difficulty { get; set; }

        public decimal? Marks { get; set; }

        public string? TagsJson { get; set; }

        public int? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool HasDelete { get; set; }

        // Navigation
        public QuestionBank? Bank { get; set; }
        public User? Creator { get; set; }
        public ICollection<AnswerOption> AnswerOptions { get; set; } = new List<AnswerOption>();
    }

    public class QuestionBank
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BankId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        public int? SubjectId { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool HasDelete { get; set; }

        // Navigation
        public Subject? Subject { get; set; }
        public User? Creator { get; set; }
    }

    public class AnswerOption
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int OptionId { get; set; }

        public int QuestionId { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        public bool IsCorrect { get; set; }
        public int? OrderIndex { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool HasDelete { get; set; }

        // Navigation
        public Question? Question { get; set; }
    }

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
        public bool HasDelete { get; set; }

        // Navigation
        public User? Teacher { get; set; }
        public Subject? Subject { get; set; }
    }

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

        [MaxLength(50)]
        public string? Status { get; set; }

        public bool IsEmailVerified { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastLoginAt { get; set; }

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
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool HasDelete { get; set; }
    }

    public class Subject
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SubjectId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ExamAttempt
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ExamAttemptId { get; set; }

        public int ExamId { get; set; }
        public int UserId { get; set; }

        [MaxLength(50)]
        public string? VariantCode { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public DateTime? SubmittedAt { get; set; }

        public decimal? Score { get; set; }
        public decimal? MaxScore { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "InProgress"; // InProgress, Completed, Abandoned

        public bool IsSubmitted { get; set; }
        public int? TimeSpentMinutes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool HasDelete { get; set; }

        // Navigation
        public Exam? Exam { get; set; }
        public User? User { get; set; }
    }

    public class SubmittedAnswer
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SubmittedAnswerId { get; set; }

        public int ExamAttemptId { get; set; }
        public int QuestionId { get; set; }

        [MaxLength(4000)]
        public string? TextAnswer { get; set; } // For text-based questions

        public bool IsCorrect { get; set; }
        public decimal EarnedMarks { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool HasDelete { get; set; }

        // Navigation
        public ExamAttempt? ExamAttempt { get; set; }
        public Question? Question { get; set; }
    }

    public class SubmittedAnswerOption
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SubmittedAnswerOptionId { get; set; }

        public int SubmittedAnswerId { get; set; }
        public int AnswerOptionId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public SubmittedAnswer? SubmittedAnswer { get; set; }
        public AnswerOption? AnswerOption { get; set; }
    }
}