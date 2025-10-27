using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace API_ThiTracNghiem.Contracts
{
    public class UploadAvatarRequest
    {
        [Required]
        public IFormFile File { get; set; } = null!;
    }
}


