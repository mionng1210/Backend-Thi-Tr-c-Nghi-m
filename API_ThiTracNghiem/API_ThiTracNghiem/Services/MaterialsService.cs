using System.Threading.Tasks;
using API_ThiTracNghiem.Contracts;
using API_ThiTracNghiem.Repositories;
using API_ThiTracNghiem.Models;
using Microsoft.AspNetCore.Http;

namespace API_ThiTracNghiem.Services
{
    public interface IMaterialsService
    {
        Task<PagedResponse<MaterialListItemDto>> GetAsync(int pageIndex, int pageSize);
        Task<MaterialListItemDto?> GetByIdAsync(int id);
        Task<List<UploadedFileDto>> CreateManyAsync(int courseId, string? title, string? description, bool isPaid, decimal? price, int? orderIndex, IFormFileCollection files);
        Task<MaterialListItemDto?> UpdateAsync(int id, int? courseId, string? title, string? description, bool? isPaid, decimal? price, int? orderIndex, IFormFile? file);
        Task<bool> DeleteAsync(int id);
    }

    public class MaterialsService : IMaterialsService
    {
        private readonly IMaterialsRepository _repo;
        private readonly ICloudStorage _storage;
        private readonly IDocumentStorage _docStorage;

        public MaterialsService(IMaterialsRepository repo, ICloudStorage storage, IDocumentStorage docStorage)
        {
            _repo = repo;
            _storage = storage;
            _docStorage = docStorage;
        }

        public Task<PagedResponse<MaterialListItemDto>> GetAsync(int pageIndex, int pageSize)
        {
            return _repo.GetMaterialsAsync(pageIndex, pageSize);
        }

        public Task<MaterialListItemDto?> GetByIdAsync(int id)
        {
            return _repo.GetByIdAsync(id);
        }

        public async Task<List<UploadedFileDto>> CreateManyAsync(int courseId, string? title, string? description, bool isPaid, decimal? price, int? orderIndex, IFormFileCollection files)
        {
            if (files == null || files.Count == 0)
            {
                throw new System.ArgumentException("Không có tệp nào được tải lên");
            }

            var courseExists = await _repo.CourseExistsAsync(courseId);
            if (!courseExists)
            {
                throw new System.ArgumentException("CourseId không tồn tại");
            }

            var materials = new List<Material>();
            var uploaded = new List<UploadedFileDto>();
            int index = orderIndex ?? 1;
            foreach (var file in files)
            {
                var contentType = file.ContentType?.ToLowerInvariant() ?? string.Empty;
                string url;
                string? mediaType;

                var isDoc = contentType == "application/pdf" || contentType.Contains("msword") || contentType.Contains("officedocument");
                if (isDoc)
                {
                    var safeFileName = SanitizeFileName(System.IO.Path.GetFileName(file.FileName));
                    var pathInBucket = $"{courseId}/{Guid.NewGuid()}_{safeFileName}";
                    url = await _docStorage.UploadDocumentAsync(file, pathInBucket);
                    mediaType = GetStoredMediaType(file);
                }
                else
                {
                    url = await _storage.UploadFileAsync(file, $"materials/{courseId}");
                    mediaType = GetStoredMediaType(file);
                }

                var material = new Material
                {
                    CourseId = courseId,
                    Title = !string.IsNullOrWhiteSpace(title) ? title : file.FileName,
                    Description = description,
                    MediaType = mediaType,
                    ExternalLink = null,
                    IsPaid = isPaid,
                    Price = isPaid ? price : null,
                    OrderIndex = index++,
                    FileUrl = url,
                    CreatedAt = System.DateTime.UtcNow,
                    HasDelete = false
                };
                materials.Add(material);

                uploaded.Add(new UploadedFileDto
                {
                    FileName = file.FileName,
                    Url = url
                });
            }

            await _repo.CreateManyAsync(materials);
            return uploaded;
        }

        public async Task<MaterialListItemDto?> UpdateAsync(int id, int? courseId, string? title, string? description, bool? isPaid, decimal? price, int? orderIndex, IFormFile? file)
        {
            var entity = await _repo.GetEntityByIdAsync(id);
            if (entity == null) return null;

            if (courseId.HasValue)
            {
                var exists = await _repo.CourseExistsAsync(courseId.Value);
                if (!exists) throw new System.ArgumentException("CourseId không tồn tại");
                entity.CourseId = courseId.Value;
            }

            if (!string.IsNullOrWhiteSpace(title)) entity.Title = title;
            if (!string.IsNullOrWhiteSpace(description)) entity.Description = description;
            if (orderIndex.HasValue) entity.OrderIndex = orderIndex.Value;
            if (isPaid.HasValue)
            {
                entity.IsPaid = isPaid.Value;
                entity.Price = isPaid.Value ? price : null;
            }

            if (file != null)
            {
                var contentType = file.ContentType?.ToLowerInvariant() ?? string.Empty;
                string url;
                var isDoc = contentType == "application/pdf" || contentType.Contains("msword") || contentType.Contains("officedocument");
                if (isDoc)
                {
                    var safeFileName = SanitizeFileName(System.IO.Path.GetFileName(file.FileName));
                    var pathInBucket = $"{entity.CourseId}/{Guid.NewGuid()}_{safeFileName}";
                    url = await _docStorage.UploadDocumentAsync(file, pathInBucket);
                }
                else
                {
                    url = await _storage.UploadFileAsync(file, $"materials/{entity.CourseId}");
                }
                entity.MediaType = GetStoredMediaType(file);
                entity.FileUrl = url;
            }

            entity.UpdatedAt = System.DateTime.UtcNow;
            await _repo.UpdateAsync(entity);

            return new MaterialListItemDto
            {
                Id = entity.MaterialId,
                Title = entity.Title,
                Description = entity.Description,
                MediaType = entity.MediaType,
                IsPaid = entity.IsPaid,
                Price = entity.Price,
                ExternalLink = entity.ExternalLink,
                DurationSeconds = entity.DurationSeconds,
                CourseId = entity.CourseId,
                OrderIndex = entity.OrderIndex,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }

        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return "file";
            string normalized = fileName.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (var ch in normalized)
            {
                var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(ch);
                }
            }
            var ascii = sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
            // replace spaces with '-', keep safe chars
            var safe = new System.Text.StringBuilder();
            foreach (var c in ascii)
            {
                if (char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-') safe.Append(c);
                else if (char.IsWhiteSpace(c)) safe.Append('-');
                // skip others
            }
            var result = safe.ToString();
            if (string.IsNullOrEmpty(result)) result = "file";
            return result;
        }

        private static string GetStoredMediaType(IFormFile file)
        {
            var ext = System.IO.Path.GetExtension(file.FileName)?.Trim('.').ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(ext) && ext.Length <= 20)
            {
                return ext;
            }
            var ct = file.ContentType ?? string.Empty;
            if (ct.Length > 50) return ct.Substring(0, 50);
            return ct;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _repo.GetEntityByIdAsync(id);
            if (entity == null || entity.HasDelete) return false;

            // Delete file from storage if exists
            if (!string.IsNullOrWhiteSpace(entity.FileUrl))
            {
                var contentType = entity.MediaType?.ToLowerInvariant() ?? string.Empty;
                var isDoc = contentType == "pdf" || contentType.Contains("doc") || contentType.Contains("word");
                
                if (isDoc)
                {
                    // Delete from document storage (Supabase)
                    await _docStorage.DeleteDocumentAsync(entity.FileUrl);
                }
                else
                {
                    // Delete from cloud storage (Cloudinary)
                    await _storage.DeleteFileAsync(entity.FileUrl);
                }
            }

            // Soft delete the material
            entity.HasDelete = true;
            entity.UpdatedAt = System.DateTime.UtcNow;
            await _repo.UpdateAsync(entity);

            return true;
        }
    }
}


