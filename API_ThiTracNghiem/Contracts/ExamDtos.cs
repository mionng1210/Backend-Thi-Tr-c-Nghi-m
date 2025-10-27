using System;
using System.ComponentModel.DataAnnotations;

namespace API_ThiTracNghiem.Contracts
{
    // DTO for exam list items
    public class ExamListItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? CourseId { get; set; }
        public string? CourseName { get; set; }
        public int? TeacherId { get; set; }
        public string? TeacherName { get; set; }
        public int? SubjectId { get; set; }
        public string? SubjectName { get; set; }
        public int? DurationMinutes { get; set; }
        public int? TotalQuestions { get; set; }
        public decimal? TotalMarks { get; set; }
        public decimal? PassingMark { get; set; }
        public string? ExamType { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    // DTO for exam details with questions
    public class ExamDetailDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? CourseId { get; set; }
        public string? CourseName { get; set; }
        public int? TeacherId { get; set; }
        public string? TeacherName { get; set; }
        public int? SubjectId { get; set; }
        public string? SubjectName { get; set; }
        public int? DurationMinutes { get; set; }
        public int? TotalQuestions { get; set; }
        public decimal? TotalMarks { get; set; }
        public decimal? PassingMark { get; set; }
        public string? ExamType { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public bool RandomizeQuestions { get; set; }
        public bool AllowMultipleAttempts { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<ExamQuestionDto> Questions { get; set; } = new();
    }

    // DTO for exam questions
    public class ExamQuestionDto
    {
        public int ExamQuestionId { get; set; }
        public int QuestionId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? QuestionType { get; set; }
        public string? Difficulty { get; set; }
        public decimal? Marks { get; set; }
        public int? SequenceIndex { get; set; }
        public List<AnswerOptionDto> Options { get; set; } = new();
    }

    // DTO for answer options
    public class AnswerOptionDto
    {
        public int OptionId { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
        public int? SequenceIndex { get; set; }
    }

    // Request DTO for creating exam
    public class CreateExamRequest
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        public int? CourseId { get; set; }

        [Range(1, 1440)] // 1 minute to 24 hours
        public int? DurationMinutes { get; set; }

        [Range(1, 500)]
        public int? TotalQuestions { get; set; }

        [Range(0.01, 1000)]
        public decimal? TotalMarks { get; set; }

        [Range(0.01, 1000)]
        public decimal? PassingMark { get; set; }

        [MaxLength(50)]
        public string? ExamType { get; set; } = "Quiz";

        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }

        public bool RandomizeQuestions { get; set; } = false;
        public bool AllowMultipleAttempts { get; set; } = true;

        [MaxLength(50)]
        public string? Status { get; set; } = "Draft";

        public List<CreateExamQuestionRequest> Questions { get; set; } = new();
    }

    // Request DTO for adding questions to exam
    public class CreateExamQuestionRequest
    {
        [Required]
        public int QuestionId { get; set; }

        [Range(0.01, 100)]
        public decimal? Marks { get; set; }

        public int? SequenceIndex { get; set; }
    }

    // Request DTO for updating exam
    public class UpdateExamRequest
    {
        [MaxLength(200)]
        public string? Title { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        public int? CourseId { get; set; }

        [Range(1, 1440)]
        public int? DurationMinutes { get; set; }

        [Range(1, 500)]
        public int? TotalQuestions { get; set; }

        [Range(0.01, 1000)]
        public decimal? TotalMarks { get; set; }

        [Range(0.01, 1000)]
        public decimal? PassingMark { get; set; }

        [MaxLength(50)]
        public string? ExamType { get; set; }

        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }

        public bool? RandomizeQuestions { get; set; }
        public bool? AllowMultipleAttempts { get; set; }

        [MaxLength(50)]
        public string? Status { get; set; }
    }
}