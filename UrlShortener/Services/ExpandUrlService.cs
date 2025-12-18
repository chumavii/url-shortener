using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using UrlShortener.Data;
using UrlShortener.Models;
using UrlShortener.Models.DTOs;
using UrlShortener.Services.Interfaces;

namespace UrlShortener.Services
{
    public class ExpandUrlService : IExpandUrlService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<ExpandUrlService> _logger;

        public ExpandUrlService(ApplicationDbContext dbContext, IConnectionMultiplexer redis, ILogger<ExpandUrlService> logger)
        {
            _dbContext = dbContext;
            _redis = redis;
            _logger = logger;
        }

        public async Task<ExpandUrlResponseDto?> ExpandUrlAsync(string shortCode)
        {
            shortCode = shortCode.Replace(Environment.NewLine, "");

            // Check Redis
            try
            {
                var cacheRecord = await CheckRedis(shortCode);
                if (!cacheRecord.IsNullOrEmpty)
                {
                    _logger.LogInformation($"Cache hit for short code: {shortCode}");
                    return CreateExpandUrlResponse(cacheRecord.ToString());
                }
                _logger.LogInformation($"Cache miss for short code: {shortCode}");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error occurred.");
            }


            // If cache miss check database
            try
            {
                var dbRecord = await CheckDatabase(shortCode);
                if (dbRecord == null)
                {
                    _logger.LogInformation("Short code [{ShortCode}] not found.", shortCode);
                    return null;
                }
                await CacheRecord(_redis, dbRecord.ShortCode, dbRecord.OriginalUrl);
                return CreateExpandUrlResponse(dbRecord.OriginalUrl);
            }
            catch (DbUpdateException d)
            {
                _logger.LogError(d, "An error occurred with the Database.");
                throw new Exception("An error occurred.");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error occurred.");
                throw new Exception("An error occurred.");
            }

        }


        /*--------
         Helpers
        ---------*/
        private async Task<RedisValue> CheckRedis(string shortCode)
        {
            shortCode = shortCode.Replace(Environment.NewLine, "");
            _logger.LogInformation("Checking redis for [USER INPUT]: {ShortCode}...", shortCode);

            var db = _redis.GetDatabase();
            var cacheRecord = await db.StringGetAsync(shortCode);

            return cacheRecord;
        }

        private async Task<UrlMapping?> CheckDatabase(string shortCode)
        {
            var record = await _dbContext.UrlMappings.FirstOrDefaultAsync(x => x.ShortCode == shortCode);
            return record;
        }

        private async Task CacheRecord(IConnectionMultiplexer _redis, string shortCode, string longUrl)
        {
            _logger.LogInformation("Caching record for [USER INPUT]: {ShortCode}...", shortCode);
            try
            {
                var db = _redis.GetDatabase();
                await db.StringSetAsync(shortCode, longUrl);
                await db.StringSetAsync($"url:{longUrl}", shortCode);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error occured with Redis");
            }
            return;
        }

        private ExpandUrlResponseDto CreateExpandUrlResponse(string longUrl)
        {
            return new ExpandUrlResponseDto { OriginalUrl = longUrl };
        }

    }
}
