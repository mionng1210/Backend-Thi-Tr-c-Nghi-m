using System;
using Microsoft.AspNetCore.Http;

namespace API_ThiTracNghiem.Contracts
{
    public class MaterialListItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? MediaType { get; set; }
        public bool IsPaid { get; set; }
        public decimal? Price { get; set; }
        public string? ExternalLink { get; set; }
        public int? DurationSeconds { get; set; }
        public int CourseId { get; set; }
        public int? OrderIndex { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class UploadedFileDto
    {
        public int MaterialId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }

    // Dùng cho PUT form-data
    public class UpdateMaterialForm
    {
        public int? CourseId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public bool? IsPaid { get; set; }
        public decimal? Price { get; set; }
        public int? OrderIndex { get; set; }
        public IFormFile? File { get; set; }
    }
}


