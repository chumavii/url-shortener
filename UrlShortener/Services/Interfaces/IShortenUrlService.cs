using UrlShortener.Models.DTOs;

namespace UrlShortener.Services.Interfaces
{
    public interface IShortenUrlService
    {
        Task<ShortenUrlResposeDto?> ShortenUrlAsync(UrlMappingDto model);
    }
}
