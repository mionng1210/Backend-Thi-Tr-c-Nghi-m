using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;

namespace MaterialsService.Integrations;

public interface IDocumentStorage
{
    Task<string> UploadDocumentAsync(IFormFile file, string pathInBucket);
}

public class SupabaseStorage : IDocumentStorage
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private readonly string _projectUrl;
    private readonly string _anonKey;
    private readonly string? _serviceKey;
    private readonly string _bucket;

    public SupabaseStorage(IConfiguration configuration, IHttpClientFactory factory)
    {
        _factory = factory;
        _config = configuration;
        _projectUrl = configuration["Supabase:ProjectUrl"] ?? string.Empty;
        _anonKey = configuration["Supabase:AnonKey"] ?? string.Empty;
        _serviceKey = configuration["Supabase:ServiceKey"];
        _bucket = configuration["Supabase:Bucket"] ?? "materials";
    }

    public async Task<string> UploadDocumentAsync(IFormFile file, string pathInBucket)
    {
        var client = _factory.CreateClient();
        var encodedPath = Uri.EscapeDataString(pathInBucket).Replace("%2F", "/");
        var url = $"{_projectUrl}/storage/v1/object/{_bucket}/{encodedPath}";
        using var content = new StreamContent(file.OpenReadStream());
        content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        var authKey = string.IsNullOrWhiteSpace(_serviceKey) ? _anonKey : _serviceKey;
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authKey);
        req.Headers.Add("apikey", authKey);
        req.Headers.Add("x-upsert", "true");
        var resp = await client.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Supabase upload failed {(int)resp.StatusCode}: {body}");
        }
        return $"{_projectUrl}/storage/v1/object/public/{_bucket}/{encodedPath}";
    }
}


