using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaterialsService.Models;

public class Material
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int MaterialId { get; set; }
    public int CourseId { get; set; }
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    [MaxLength(2000)]
    public string? Description { get; set; }
    public string? MediaType { get; set; }
    public string? FileUrl { get; set; }
    public bool IsPaid { get; set; }
    public decimal? Price { get; set; }
    public string? ExternalLink { get; set; }
    public int? DurationSeconds { get; set; }
    public int? OrderIndex { get; set; }
    public bool HasDelete { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class PaymentTransaction
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int TransactionId { get; set; }
    public string? OrderId { get; set; }
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string? Gateway { get; set; }
    public string Status { get; set; } = "Pending";
    public string? QrCodeData { get; set; }
    public string? Payload { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
