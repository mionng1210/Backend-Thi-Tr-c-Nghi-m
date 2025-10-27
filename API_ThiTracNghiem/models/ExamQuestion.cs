using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_ThiTracNghiem.Models
{
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

        // Navigation
        public Exam? Exam { get; set; }
        public Question? Question { get; set; }
    }
}


