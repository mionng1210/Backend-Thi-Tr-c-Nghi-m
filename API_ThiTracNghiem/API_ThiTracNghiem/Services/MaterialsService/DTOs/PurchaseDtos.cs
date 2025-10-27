using System.ComponentModel.DataAnnotations;

namespace MaterialsService.DTOs
{
    public class PurchaseMaterialRequest
    {
        [Required(ErrorMessage = "MaterialId là bắt buộc")]
        [Range(1, int.MaxValue, ErrorMessage = "MaterialId phải lớn hơn 0")]
        public int MaterialId { get; set; }

        [MaxLength(50, ErrorMessage = "Gateway không được vượt quá 50 ký tự")]
        public string? Gateway { get; set; } = "VNPay"; // Default payment gateway

        [MaxLength(10, ErrorMessage = "Currency không được vượt quá 10 ký tự")]
        public string? Currency { get; set; } = "VND"; // Default currency
    }

    public class PurchaseMaterialResponse
    {
        public int TransactionId { get; set; }
        public string OrderId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Gateway { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? QrCodeData { get; set; }
        public string? PaymentUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public MaterialPurchaseInfo Material { get; set; } = new();
    }

    public class MaterialPurchaseInfo
    {
        public int MaterialId { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? MediaType { get; set; }
    }
}