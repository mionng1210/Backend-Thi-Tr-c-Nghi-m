using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_ThiTracNghiem.Models
{
    public class AiRequest
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AiRequestId { get; set; }

        public int? RequesterId { get; set; }

        [MaxLength(50)]
        public string? RequestType { get; set; }

        public string? PromptText { get; set; }
        public string? ParametersJson { get; set; }
        public string? ModelName { get; set; }
        public string? ResponseText { get; set; }
        public int? RelatedExamId { get; set; }
        public string? TokenUsage { get; set; }
        public decimal? Cost { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? Requester { get; set; }
        public Exam? RelatedExam { get; set; }
    }
}


