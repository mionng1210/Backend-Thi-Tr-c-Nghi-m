using MaterialsService.Data;
using MaterialsService.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Materials;
using MaterialsService.Integrations;

namespace MaterialsService.Services;

public interface IMaterialsService
{
    Task<object> GetAsync(int pageIndex, int pageSize);
    Task<MaterialListItemDto?> GetByIdAsync(int id);
    Task<List<UploadedFileDto>> CreateManyAsync(int courseId, string? title, string? description, bool isPaid, decimal? price, int? orderIndex, IFormFileCollection files);
    Task<MaterialListItemDto?> UpdateAsync(int id, int? courseId, string? title, string? description, bool? isPaid, decimal? price, int? orderIndex, IFormFile? file);
    Task<bool> DeleteAsync(int id);
}

public class MaterialsService : IMaterialsService
{
    private readonly MaterialsDbContext _db;
    private readonly ICloudStorage _cloud;
    private readonly IDocumentStorage _docs;
    public MaterialsService(MaterialsDbContext db, ICloudStorage cloud, IDocumentStorage docs)
    {
        _db = db;
        _cloud = cloud;
        _docs = docs;
    }

    public async Task<object> GetAsync(int pageIndex, int pageSize)
    {
        if (pageIndex <= 0) pageIndex = 1;
        if (pageSize <= 0) pageSize = 10;

        var query = _db.Materials.Where(m => !m.HasDelete).OrderBy(m => m.OrderIndex);
        var totalItems = await query.CountAsync();

        var items = await query
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MaterialListItemDto
            {
                Id = m.MaterialId,
                Title = m.Title,
                Description = m.Description,
                MediaType = m.MediaType,
                IsPaid = m.IsPaid,
                Price = m.Price,
                ExternalLink = m.ExternalLink,
                DurationSeconds = m.DurationSeconds,
                CourseId = m.CourseId,
                OrderIndex = m.OrderIndex,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt
            }).ToListAsync();

        return new
        {
            pageIndex = pageIndex,
            pageSize = pageSize,
            totalItems = totalItems,
            totalPages = (int)Math.Ceiling((double)totalItems / pageSize),
            items = items
        };
    }

    public async Task<MaterialListItemDto?> GetByIdAsync(int id)
    {
        return await _db.Materials
            .Where(m => m.MaterialId == id && !m.HasDelete)
            .Select(m => new MaterialListItemDto
            {
                Id = m.MaterialId,
                Title = m.Title,
                Description = m.Description,
                MediaType = m.MediaType,
                IsPaid = m.IsPaid,
                Price = m.Price,
                ExternalLink = m.ExternalLink,
                DurationSeconds = m.DurationSeconds,
                CourseId = m.CourseId,
                OrderIndex = m.OrderIndex,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt
            }).FirstOrDefaultAsync();
    }

    public async Task<List<UploadedFileDto>> CreateManyAsync(int courseId, string? title, string? description, bool isPaid, decimal? price, int? orderIndex, IFormFileCollection files)
    {
        // Tối giản: chỉ lưu meta, bỏ upload thật để biên dịch chạy ngay
        var list = new List<UploadedFileDto>();
        int index = orderIndex ?? 1;
        foreach (var file in files)
        {
            var safeFileName = SanitizeFileName(file.FileName);
            var entity = new Material
            {
                CourseId = courseId,
                Title = title ?? safeFileName,
                Description = description,
                IsPaid = isPaid,
                Price = price,
                MediaType = file.ContentType,
                OrderIndex = index++
            };
            // Upload to proper storage
            string url;
            if ((file.ContentType?.StartsWith("video/") ?? false))
            {
                url = await _cloud.UploadFileAsync(file, "materials/videos");
            }
            else
            {
                var path = $"documents/{Guid.NewGuid()}_{safeFileName}";
                url = await _docs.UploadDocumentAsync(file, path);
            }

            entity.FileUrl = url;
            _db.Materials.Add(entity);
            await _db.SaveChangesAsync();
            list.Add(new UploadedFileDto { MaterialId = entity.MaterialId, FileName = file.FileName, Url = url });
        }
        return list;
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return "file";
        var normalized = fileName.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }
        var ascii = sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
        // Map special Vietnamese characters not handled by decomposition
        ascii = ascii.Replace('đ', 'd').Replace('Đ', 'D');
        // replace spaces and invalid url chars
        var safe = new string(ascii.Select(c =>
        {
            // Only keep ASCII letters/digits and ._- ; others -> '-'
            if (c <= 127 && (char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-')) return c;
            return '-';
        }).ToArray());
        while (safe.Contains("--")) safe = safe.Replace("--", "-");
        return safe.Trim('-');
    }

    public async Task<MaterialListItemDto?> UpdateAsync(int id, int? courseId, string? title, string? description, bool? isPaid, decimal? price, int? orderIndex, IFormFile? file)
    {
        var m = await _db.Materials.FirstOrDefaultAsync(x => x.MaterialId == id && !x.HasDelete);
        if (m == null) return null;
        if (courseId.HasValue) m.CourseId = courseId.Value;
        if (!string.IsNullOrWhiteSpace(title)) m.Title = title!;
        if (!string.IsNullOrWhiteSpace(description)) m.Description = description!;
        if (isPaid.HasValue) m.IsPaid = isPaid.Value;
        if (price.HasValue) m.Price = price.Value;
        if (orderIndex.HasValue) m.OrderIndex = orderIndex.Value;
        if (file != null && file.Length > 0)
        {
            var safeName = SanitizeFileName(file.FileName);
            string url;
            if ((file.ContentType?.StartsWith("video/") ?? false))
            {
                url = await _cloud.UploadFileAsync(file, "materials/videos");
            }
            else
            {
                var path = $"documents/{Guid.NewGuid()}_{safeName}";
                url = await _docs.UploadDocumentAsync(file, path);
            }
            m.FileUrl = url;
            m.MediaType = file.ContentType;
        }
        m.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var m = await _db.Materials.FirstOrDefaultAsync(x => x.MaterialId == id && !x.HasDelete);
        if (m == null) return false;
        m.HasDelete = true;
        m.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }
}


