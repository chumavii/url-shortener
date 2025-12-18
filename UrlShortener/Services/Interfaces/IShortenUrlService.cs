using UrlShortener.Models.DTOs;

namespace UrlShortener.Services.Interfaces
{
    public interface IShortenUrlService
    {
        Task<ShortenUrlResponseDto?> ShortenUrlAsync(UrlMappingDto model);
    }
}
