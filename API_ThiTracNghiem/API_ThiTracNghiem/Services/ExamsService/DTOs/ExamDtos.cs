using System;
using System.ComponentModel.DataAnnotations;

namespace ExamsService.DTOs
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
        public int? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
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
        public int? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
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

    // Request DTO for adding questions to existing exam
    public class AddQuestionToExamRequest
    {
        [Required]
        [MaxLength(2000)]
        public string Content { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? QuestionType { get; set; } = "MultipleChoice";

        [MaxLength(20)]
        public string? Difficulty { get; set; } = "Medium";

        [Range(0.01, 100)]
        public decimal? Marks { get; set; }

        public int? SequenceIndex { get; set; }

        [Required]
        public List<CreateAnswerOptionRequest> AnswerOptions { get; set; } = new();
    }

    // Request DTO for creating answer options
    public class CreateAnswerOptionRequest
    {
        [Required]
        [MaxLength(1000)]
        public string Content { get; set; } = string.Empty;

        public bool IsCorrect { get; set; }

        public int? OrderIndex { get; set; }
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

    // Pagination response DTO
    public class PagedResponse<T>
    {
        public List<T> Items { get; set; } = new();
        public long Total { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);
        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;
    }

    // API Response wrapper
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public int StatusCode { get; set; }

        public static ApiResponse<T> SuccessResponse(T data, string message = "Success")
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = data,
                StatusCode = 200
            };
        }

        public static ApiResponse<T> ErrorResponse(string message, int statusCode = 500)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                Data = default,
                StatusCode = statusCode
            };
        }
    }

    public class ApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }
        public int StatusCode { get; set; }

        public static ApiResponse SuccessResponse(object? data = null, string message = "Success")
        {
            return new ApiResponse
            {
                Success = true,
                Message = message,
                Data = data,
                StatusCode = 200
            };
        }

        public static ApiResponse ErrorResponse(string message, int statusCode = 500)
        {
            return new ApiResponse
            {
                Success = false,
                Message = message,
                Data = null,
                StatusCode = statusCode
            };
        }
    }

    // Request DTO for mixing questions based on difficulty
    public class MixQuestionsRequest
    {
        [Range(1, 100)]
        public int NumberOfVariants { get; set; } = 1;

        [Range(1, 500)]
        public int TotalQuestions { get; set; }

        public List<DifficultyDistribution> DifficultyDistribution { get; set; } = new();
    }

    // DTO for difficulty distribution
    public class DifficultyDistribution
    {
        [Required]
        [MaxLength(50)]
        public string Difficulty { get; set; } = string.Empty; // Easy, Medium, Hard

        [Range(1, 500)]
        public int QuestionCount { get; set; }

        [Range(0.01, 100)]
        public decimal MarksPerQuestion { get; set; }
    }

    // Response DTO for mixed questions
    public class MixQuestionsResponse
    {
        public int ExamId { get; set; }
        public List<ExamVariant> Variants { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }

    // DTO for exam variant
    public class ExamVariant
    {
        public string VariantCode { get; set; } = string.Empty;
        public List<ExamQuestionDto> Questions { get; set; } = new();
        public decimal TotalMarks { get; set; }
    }

    // Request DTO for starting an exam
    public class StartExamRequest
    {
        public string? VariantCode { get; set; }
    }

    // Response DTO for starting an exam
    public class StartExamResponse
    {
        public int ExamAttemptId { get; set; }
        public int ExamId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public string? VariantCode { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int DurationMinutes { get; set; }
        public List<ExamQuestionDto> Questions { get; set; } = new();
        public decimal TotalMarks { get; set; }
        public decimal PassingMark { get; set; }
        public string Instructions { get; set; } = string.Empty;
    }

    // Request DTO for submitting an exam
    public class SubmitExamRequest
    {
        [Required]
        public List<SubmittedAnswerDto> Answers { get; set; } = new();
    }

    // DTO for submitted answers
    public class SubmittedAnswerDto
    {
        [Required]
        public int QuestionId { get; set; }

        public List<int> SelectedOptionIds { get; set; } = new(); // For multiple choice
        public string? TextAnswer { get; set; } // For text-based questions
    }

    // Response DTO for exam submission
    public class SubmitExamResponse
    {
        public int ExamAttemptId { get; set; }
        public int ExamId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public decimal Score { get; set; }
        public decimal MaxScore { get; set; }
        public decimal Percentage { get; set; }
        public bool IsPassed { get; set; }
        public DateTime SubmittedAt { get; set; }
        public int TimeSpentMinutes { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<QuestionResultDto> QuestionResults { get; set; } = new();
    }

    // DTO for individual question results
    public class QuestionResultDto
    {
        public int QuestionId { get; set; }
        public string Content { get; set; } = string.Empty;
        public decimal Marks { get; set; }
        public decimal EarnedMarks { get; set; }
        public bool IsCorrect { get; set; }
        public List<int> CorrectOptionIds { get; set; } = new();
        public List<int> SelectedOptionIds { get; set; } = new();
        public string? TextAnswer { get; set; }
        public string? CorrectTextAnswer { get; set; }
    }

    // Response DTO for user exam results
    public class UserExamResultsResponse
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public List<ExamResultDto> Results { get; set; } = new();
        public ExamResultsStatistics Statistics { get; set; } = new();
    }

    // DTO for individual exam result
    public class ExamResultDto
    {
        public int ExamAttemptId { get; set; }
        public int ExamId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public string? CourseName { get; set; }
        public string? SubjectName { get; set; }
        public decimal Score { get; set; }
        public decimal MaxScore { get; set; }
        public decimal Percentage { get; set; }
        public bool IsPassed { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public int TimeSpentMinutes { get; set; }
        public string Status { get; set; } = string.Empty;
        public int AttemptNumber { get; set; }
    }

    // DTO for exam results statistics
    public class ExamResultsStatistics
    {
        public int TotalExams { get; set; }
        public int PassedExams { get; set; }
        public int FailedExams { get; set; }
        public decimal AverageScore { get; set; }
        public decimal HighestScore { get; set; }
        public decimal LowestScore { get; set; }
        public double PassRate { get; set; }
    }

    // Response DTO for exam ranking
    public class ExamRankingResponse
    {
        public int ExamId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public string? CourseName { get; set; }
        public string? SubjectName { get; set; }
        public List<RankingEntryDto> Rankings { get; set; } = new();
        public RankingStatistics Statistics { get; set; } = new();
    }

    // DTO for individual ranking entry
    public class RankingEntryDto
    {
        public int Rank { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? UserEmail { get; set; }
        public decimal Score { get; set; }
        public decimal MaxScore { get; set; }
        public decimal Percentage { get; set; }
        public DateTime SubmittedAt { get; set; }
        public int TimeSpentMinutes { get; set; }
        public int AttemptNumber { get; set; }
    }

    // DTO for ranking statistics
    public class RankingStatistics
    {
        public int TotalParticipants { get; set; }
        public decimal AverageScore { get; set; }
        public decimal HighestScore { get; set; }
        public decimal LowestScore { get; set; }
        public double PassRate { get; set; }
        public decimal MedianScore { get; set; }
    }
}