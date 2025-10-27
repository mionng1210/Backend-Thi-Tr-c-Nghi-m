using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;

namespace MaterialsService.Integrations;

public interface ICloudStorage
{
    Task<string> UploadFileAsync(IFormFile file, string folder);
    Task<bool> DeleteFileAsync(string fileUrl);
}

public class CloudinaryService : ICloudStorage
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryService(IConfiguration configuration)
    {
        var cloud = configuration["Cloudinary:CloudName"];
        var key = configuration["Cloudinary:ApiKey"];
        var secret = configuration["Cloudinary:ApiSecret"];
        _cloudinary = new Cloudinary(new Account(cloud, key, secret)) { Api = { Secure = true } };
    }

    public async Task<string> UploadFileAsync(IFormFile file, string folder)
    {
        if (file == null || file.Length == 0) return string.Empty;
        await using var stream = file.OpenReadStream();
        var contentType = file.ContentType?.ToLowerInvariant() ?? string.Empty;

        if (contentType.StartsWith("video/"))
        {
            try
            {
                var up = new VideoUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = folder,
                    UseFilename = true,
                    UniqueFilename = true,
                    Overwrite = false,
                };
                var res = await _cloudinary.UploadAsync(up);
                return res.SecureUrl?.ToString() ?? string.Empty;
            }
            catch
            {
                // Retry with chunked large upload for big files or unstable network
                await stream.DisposeAsync();
                await using var largeStream = file.OpenReadStream();
                var large = new VideoUploadParams
                {
                    File = new FileDescription(file.FileName, largeStream),
                    Folder = folder,
                    UseFilename = true,
                    UniqueFilename = true,
                    Overwrite = false
                };
                var resLarge = await _cloudinary.UploadLargeAsync(large);
                return resLarge.SecureUrl?.ToString() ?? string.Empty;
            }
        }

        var img = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = folder,
            UseFilename = true,
            UniqueFilename = true,
            Overwrite = false
        };
        var imgRes = await _cloudinary.UploadAsync(img);
        return imgRes.SecureUrl?.ToString() ?? string.Empty;
    }

    public async Task<bool> DeleteFileAsync(string fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl)) return false;
        try
        {
            var uri = new Uri(fileUrl);
            var segments = uri.AbsolutePath.Split('/');
            var idx = Array.FindIndex(segments, s => s == "upload");
            if (idx < 0 || idx + 1 >= segments.Length) return false;
            var publicId = string.Join('/', segments.Skip(idx + 1));
            var dot = publicId.LastIndexOf('.');
            if (dot > 0) publicId = publicId[..dot];

            var del = await _cloudinary.DestroyAsync(new DeletionParams(publicId));
            if (del.Result == "ok") return true;
            var delVideo = await _cloudinary.DestroyAsync(new DeletionParams(publicId) { ResourceType = ResourceType.Video });
            return delVideo.Result == "ok";
        }
        catch { return false; }
    }
}


