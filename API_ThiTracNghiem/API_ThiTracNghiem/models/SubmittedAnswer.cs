using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_ThiTracNghiem.Models
{
    public class SubmittedAnswer
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SubmittedAnswerId { get; set; }

        public int AttemptId { get; set; }
        public int QuestionId { get; set; }
        public int? SelectedOptionId { get; set; }
        public string? AnswerText { get; set; }
        public bool IsCorrect { get; set; }
        public decimal? MarksObtained { get; set; }
        public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ExamAttempt? Attempt { get; set; }
        public Question? Question { get; set; }
        public AnswerOption? SelectedOption { get; set; }
    }
}


