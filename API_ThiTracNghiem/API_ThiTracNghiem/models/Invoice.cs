using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_ThiTracNghiem.Models
{
    public class Invoice
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int InvoiceId { get; set; }

        public int TransactionId { get; set; }
        public int UserId { get; set; }

        public decimal Amount { get; set; }

        [MaxLength(10)]
        public string? Currency { get; set; }

        public string? ItemsJson { get; set; }

        [MaxLength(50)]
        public string? Status { get; set; }

        public DateTime? IssuedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public PaymentTransaction? Transaction { get; set; }
        public User? User { get; set; }
    }
}


