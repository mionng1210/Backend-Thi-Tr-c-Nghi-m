using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace API_ThiTracNghiem.Services
{
    public interface IDocumentStorage
    {
        Task<string> UploadDocumentAsync(IFormFile file, string pathInBucket);
        Task<bool> DeleteDocumentAsync(string fileUrl);
    }

    public class SupabaseStorage : IDocumentStorage
    {
        private readonly HttpClient _httpClient;
        private readonly string _projectUrl;
        private readonly string _anonKey;
        private readonly string? _serviceKey;
        private readonly string _bucket;

        public SupabaseStorage(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
            _projectUrl = configuration["Supabase:ProjectUrl"] ?? string.Empty;
            _anonKey = configuration["Supabase:AnonKey"] ?? string.Empty;
            _serviceKey = configuration["Supabase:ServiceKey"];
            var configuredBucket = configuration["Supabase:Bucket"] ?? "materials";
            // Giữ nguyên tên bucket như cấu hình (có phân biệt hoa-thường)
            _bucket = configuredBucket;
        }

        public async Task<string> UploadDocumentAsync(IFormFile file, string pathInBucket)
        {
            if (file == null || file.Length == 0) return string.Empty;

            // Encode path nhưng giữ nguyên dấu '/'
            var encodedPath = Uri.EscapeDataString(pathInBucket).Replace("%2F", "/");
            var uploadUrl = $"{_projectUrl}/storage/v1/object/{_bucket}/{encodedPath}";

            using var content = new StreamContent(file.OpenReadStream());
            content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");

            using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl)
            {
                Content = content
            };
            var authKey = string.IsNullOrWhiteSpace(_serviceKey) ? _anonKey : _serviceKey;
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authKey);
            request.Headers.Add("apikey", authKey);
            request.Headers.Add("x-upsert", "true");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                if ((int)response.StatusCode == 404 && body.Contains("Bucket not found") && !string.IsNullOrWhiteSpace(_serviceKey))
                {
                    // Thử tạo bucket nếu chưa tồn tại (cần service role key)
                    await EnsureBucketExistsAsync();
                    // Retry upload 1 lần
                    using var retryContent = new StreamContent(file.OpenReadStream());
                    retryContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
                    using var retryReq = new HttpRequestMessage(HttpMethod.Post, uploadUrl)
                    {
                        Content = retryContent
                    };
                    retryReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authKey);
                    retryReq.Headers.Add("apikey", authKey);
                    retryReq.Headers.Add("x-upsert", "true");
                    var retryResp = await _httpClient.SendAsync(retryReq);
                    if (!retryResp.IsSuccessStatusCode)
                    {
                        var retryBody = await retryResp.Content.ReadAsStringAsync();
                        throw new ArgumentException($"Supabase upload failed after create-bucket ({(int)retryResp.StatusCode}): {retryBody}");
                    }
                }
                else
                {
                    throw new ArgumentException($"Supabase upload failed ({(int)response.StatusCode}): {body}");
                }
            }

            // Public URL (bucket public). Nếu bucket private, cần signed URL (không triển khai ở đây)
            var publicUrl = $"{_projectUrl}/storage/v1/object/public/{_bucket}/{encodedPath}";
            return publicUrl;
        }

        private async Task EnsureBucketExistsAsync()
        {
            var bucketsUrl = $"{_projectUrl}/storage/v1/bucket";
            var payload = $"{{\"name\":\"{_bucket}\",\"public\":true}}";
            using var createBody = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            using var req = new HttpRequestMessage(HttpMethod.Post, bucketsUrl)
            {
                Content = createBody
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceKey);
            req.Headers.Add("apikey", _serviceKey);
            var resp = await _httpClient.SendAsync(req);
            // 200/201/409 (exists) đều coi như ok
        }

        public async Task<bool> DeleteDocumentAsync(string fileUrl)
        {
            if (string.IsNullOrWhiteSpace(fileUrl)) return false;

            try
            {
                // Extract path from Supabase public URL
                // Format: {projectUrl}/storage/v1/object/public/{bucket}/{path}
                var expectedPrefix = $"{_projectUrl}/storage/v1/object/public/{_bucket}/";
                if (!fileUrl.StartsWith(expectedPrefix))
                {
                    return false; // Not a valid Supabase URL for this bucket
                }

                var pathInBucket = fileUrl.Substring(expectedPrefix.Length);
                var encodedPath = Uri.EscapeDataString(pathInBucket).Replace("%2F", "/");
                var deleteUrl = $"{_projectUrl}/storage/v1/object/{_bucket}/{encodedPath}";

                using var request = new HttpRequestMessage(HttpMethod.Delete, deleteUrl);
                var authKey = string.IsNullOrWhiteSpace(_serviceKey) ? _anonKey : _serviceKey;
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authKey);
                request.Headers.Add("apikey", authKey);

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}


