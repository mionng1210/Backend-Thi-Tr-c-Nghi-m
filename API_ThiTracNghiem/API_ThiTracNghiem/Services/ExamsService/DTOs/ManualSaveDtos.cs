using System.ComponentModel.DataAnnotations;

namespace ExamsService.DTOs
{
    public class SaveAnswerRequest
    {
        [Required]
        public int ExamAttemptId { get; set; }
        [Required]
        public int QuestionId { get; set; }
        public List<int> SelectedOptionIds { get; set; } = new();
        public string? TextAnswer { get; set; }
        public int? BufferMinutes { get; set; }
    }

    public class SaveBatchAnswersRequest
    {
        [Required]
        public int ExamAttemptId { get; set; }
        [Required]
        public List<SaveAnswerItem> Answers { get; set; } = new();
        public int? BufferMinutes { get; set; }
    }

    public class SaveAnswerItem
    {
        [Required]
        public int QuestionId { get; set; }
        public List<int> SelectedOptionIds { get; set; } = new();
        public string? TextAnswer { get; set; }
    }

    public class RestoreProgressResponse
    {
        public int ExamAttemptId { get; set; }
        public int Count { get; set; }
        public List<SubmittedAnswerDto> Answers { get; set; } = new();
    }
}