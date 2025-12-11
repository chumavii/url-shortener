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
        private ApplicationDbContext _dbContext;
        private IConnectionMultiplexer _redis;
        private ILogger<ExpandUrlService> _logger;

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
                var cacheRecord = await CheckRedis(_redis, shortCode);
                if (!cacheRecord.IsNullOrEmpty)
                {
                    _logger.LogInformation($"Cache hit for short code: {shortCode}");
                    return CreateExpandUrResponse(cacheRecord.ToString());
                }
                _logger.LogInformation($"Cache miss for short code: {shortCode}");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error ocurred.");
            }


            // If cache miss check database
            try
            {
                var dbRecord = await CheckDatabase(_dbContext, shortCode);
                if (dbRecord == null)
                {
                    _logger.LogInformation("Short code [{ShortCode}] not found.", shortCode);
                    return null;
                }
                await CacheRecord(_redis, dbRecord.ShortCode, dbRecord.OriginalUrl);
                return CreateExpandUrResponse(dbRecord.OriginalUrl);
            }
            catch (DbUpdateException d)
            {
                _logger.LogError(d, "An error ocurred with the Database.");
                throw new Exception("An error ocurred.");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error ocurred.");
                throw new Exception("An error ocurred.");
            }

        }


        /*--------
         Helpers
        ---------*/
        private async Task<RedisValue> CheckRedis(IConnectionMultiplexer _redis, string shortCode)
        {
            shortCode = shortCode.Replace(Environment.NewLine, "");
            _logger.LogInformation("Checking redis for [USER INPUT]: {ShortCode}...", shortCode);

            var db = _redis.GetDatabase();
            var cacheRecord = await db.StringGetAsync(shortCode);

            return cacheRecord;
        }

        private async Task<UrlMapping?> CheckDatabase(ApplicationDbContext _dbContext, string shortCode)
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

        private ExpandUrlResponseDto CreateExpandUrResponse(string longUrl)
        {
            return new ExpandUrlResponseDto { OriginalUrl = longUrl };
        }

    }
}
