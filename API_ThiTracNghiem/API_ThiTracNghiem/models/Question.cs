using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_ThiTracNghiem.Models
{
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
        public DateTime? UpdatedAt { get; set; }
        public bool HasDelete { get; set; }

        // Navigation
        public QuestionBank? Bank { get; set; }
        public User? Creator { get; set; }
    }
}


