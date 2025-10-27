using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_ThiTracNghiem.Models
{
    public class Statistics
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int StatisticsId { get; set; }

        [MaxLength(50)]
        public string? EntityType { get; set; }
        public int? EntityId { get; set; }
        public DateTime? MetricDate { get; set; }
        public int? ActiveUsersCount { get; set; }
        public decimal? AverageScore { get; set; }
        public int? AttemptsCount { get; set; }
        public decimal? RevenueAmount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}


