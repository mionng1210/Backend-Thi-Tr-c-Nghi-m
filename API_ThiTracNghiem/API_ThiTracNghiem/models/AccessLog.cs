using System;

namespace API_ThiTracNghiem.Models
{
    public class AccessLog
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public string? Role { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? Endpoint { get; set; }
        public string? Method { get; set; }
        public int StatusCode { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

