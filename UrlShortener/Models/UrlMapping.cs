namespace UrlShortener.Models
{
    public class UrlMapping
    {
        public int Id { get; set; }
        public required string OriginalUrl { get; set; }
        public required string ShortCode { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
