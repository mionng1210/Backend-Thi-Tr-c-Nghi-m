using System.ComponentModel.DataAnnotations;
using ExamsService.DTOs;

namespace ExamsService.Models
{
    public class CreateQuestionBankRequest
    {
        [Required(ErrorMessage = "Nội dung câu hỏi là bắt buộc")]
        public string Content { get; set; } = string.Empty;

        [Required(ErrorMessage = "Loại câu hỏi là bắt buộc")]
        public string QuestionType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mức độ khó là bắt buộc")]
        public string Difficulty { get; set; } = string.Empty;

        [Range(0.1, 100, ErrorMessage = "Điểm phải từ 0.1 đến 100")]
        public decimal Marks { get; set; }

        public string? Tags { get; set; }

        [Required(ErrorMessage = "Danh sách đáp án là bắt buộc")]
        public List<CreateAnswerOptionRequest> AnswerOptions { get; set; } = new();
    }

    public class QuestionBankResponse
    {
        public int QuestionId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? QuestionType { get; set; }
        public string? Difficulty { get; set; }
        public decimal? Marks { get; set; }
        public string? Tags { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<AnswerOptionResponse> AnswerOptions { get; set; } = new();
    }

    public class AnswerOptionResponse
    {
        public int OptionId { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
        public int? OrderIndex { get; set; }
    }

    public class QuestionBankFilterRequest
    {
        public string? QuestionType { get; set; }
        public string? Difficulty { get; set; }
        public string? Tags { get; set; }
        public string? SearchContent { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class QuestionBankListResponse
    {
        public List<QuestionBankResponse> Questions { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// DTO cho việc cập nhật câu hỏi trong ngân hàng
    /// </summary>
    public class UpdateQuestionBankRequest
    {
        [Required(ErrorMessage = "Nội dung câu hỏi là bắt buộc")]
        [StringLength(2000, ErrorMessage = "Nội dung câu hỏi không được vượt quá 2000 ký tự")]
        public string Content { get; set; } = string.Empty;

        [Required(ErrorMessage = "Loại câu hỏi là bắt buộc")]
        [StringLength(50, ErrorMessage = "Loại câu hỏi không được vượt quá 50 ký tự")]
        public string QuestionType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Độ khó là bắt buộc")]
        [StringLength(20, ErrorMessage = "Độ khó không được vượt quá 20 ký tự")]
        public string Difficulty { get; set; } = string.Empty;

        [Required(ErrorMessage = "Điểm số là bắt buộc")]
        [Range(0.1, 100, ErrorMessage = "Điểm số phải từ 0.1 đến 100")]
        public decimal Marks { get; set; }

        [StringLength(500, ErrorMessage = "Tags không được vượt quá 500 ký tự")]
        public string? Tags { get; set; }

        [Required(ErrorMessage = "Danh sách đáp án là bắt buộc")]
        [MinLength(2, ErrorMessage = "Phải có ít nhất 2 đáp án")]
        public List<CreateAnswerOptionRequest> AnswerOptions { get; set; } = new();
    }

    /// <summary>
    /// DTO cho việc thêm câu hỏi từ ngân hàng vào bài thi
    /// </summary>
    public class AddQuestionsFromBankRequest
    {
        [Required(ErrorMessage = "Danh sách ID câu hỏi là bắt buộc")]
        [MinLength(1, ErrorMessage = "Phải chọn ít nhất 1 câu hỏi")]
        public List<int> QuestionIds { get; set; } = new();

        [Range(0.1, 100, ErrorMessage = "Điểm số mặc định phải từ 0.1 đến 100")]
        public decimal? DefaultMarks { get; set; }
    }
}