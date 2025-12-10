using StackExchange.Redis;
using UrlShortener.Data;
using UrlShortener.Models;
using UrlShortener.Models.DTOs;

namespace UrlShortener.Services.Interfaces
{
    public interface IExpandUrlService
    {
        Task<ExpandUrlResponseDto?> ExpandUrl(string shortCode);
    }
}
