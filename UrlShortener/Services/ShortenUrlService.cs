using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using UrlShortener.Data;
using UrlShortener.Models;
using UrlShortener.Models.DTOs;
using UrlShortener.Services.Interfaces;
using Utilities;

namespace UrlShortener.Services
{
    public class ShortenUrlService : IShortenUrlService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<ExpandUrlService> _logger;
        public ShortenUrlService(ApplicationDbContext dbContext, IConnectionMultiplexer redis, ILogger<ExpandUrlService> logger)
        {
            _dbContext = dbContext;
            _redis = redis;
            _logger = logger;
        }

        public async Task<ShortenUrlResposeDto?> ShortenUrlAsync(UrlMappingDto model)
        {
            // Scheme validations
            var longUrl = EnsureUrlHasScheme(model.OriginalUrl);
            longUrl = ValidateUrl(longUrl);
            longUrl = longUrl.Replace(Environment.NewLine, "");

            var cacheRecord = await CheckRedis(longUrl);
            if (! cacheRecord.IsNull)
            {
                _logger.LogInformation("Cache hit for {LongUrl}", longUrl);
                return CreateShortenUrlResponse(cacheRecord!);
            }
                

            var dbRecord = await CheckDatabase(longUrl);
            if (dbRecord != null && dbRecord.OriginalUrl != null && dbRecord.ShortCode != null)
            {
                try
                {
                    _logger.LogInformation($"Cache miss for url: {dbRecord.OriginalUrl}");
                    await AddRecordToCache(dbRecord.ShortCode, dbRecord.OriginalUrl);
                    return CreateShortenUrlResponse(dbRecord.ShortCode);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Failed to cache URL mapping for {dbRecord.OriginalUrl}");
                    return CreateShortenUrlResponse(dbRecord.ShortCode);
                }
            }

            // Short code needs to be unique before adding to db
            _logger.LogInformation("Generating short code...");
            var shortCode = await GenerateShortCode(longUrl);

            var mapping = new UrlMapping
            {
                OriginalUrl = longUrl,
                ShortCode = shortCode,
                CreatedAt = DateTime.UtcNow
            };

            // Save record in db 
            try
            {
                _logger.LogInformation("Saving record to database..");
                await SaveToDatabase(mapping);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "An error occurred with the database.");
                throw new Exception("An error occurred with the database");
            }

            // Cache record
            try
            {
                await AddRecordToCache(shortCode, longUrl);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to cache URL mapping for {longUrl.Replace(Environment.NewLine, "")}");
            }

            return CreateShortenUrlResponse(shortCode);
        }

        /*--------
         Helpers
        ---------*/
        private string EnsureUrlHasScheme(string url)
        {
            _logger.LogInformation("Validating URL scheme...");
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                return "https://" + url;
            return url;
        }
        private string ValidateUrl(string longUrl)
        {
            if (!Uri.TryCreate(longUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new Exception("Invalid URL: must be a valid http or https URL.");
            return uri.ToString();
        }
        private async Task<RedisValue> CheckRedis(string longUrl)
        {
            longUrl = longUrl.Replace(Environment.NewLine, "");
            _logger.LogInformation("Checking redis for [USER INPUT]: {LongUrl}...", longUrl);

            var db = _redis.GetDatabase();
            var cacheRecord = await db.StringGetAsync(longUrl);

            return cacheRecord;
        }
        private async Task<UrlMapping?> CheckDatabase(string longUrl)
        {
            _logger.LogInformation("Checking db for record...");
            var record = await _dbContext.UrlMappings.FirstOrDefaultAsync(x => x.OriginalUrl == longUrl);
            return record;
        }
        private async Task AddRecordToCache(string shortCode, string longUrl)
        {
            _logger.LogInformation("Caching record for [USER INPUT]: {LongUrl}...", longUrl);
            var db = _redis.GetDatabase();
            await db.StringSetAsync($"url:{longUrl}", shortCode, TimeSpan.FromDays(30));
            await db.StringSetAsync(shortCode, longUrl, TimeSpan.FromDays(30));
            return;
        }
        private async Task SaveToDatabase(UrlMapping mapping)
        {
            await _dbContext.UrlMappings.AddAsync(mapping);
            await _dbContext.SaveChangesAsync();
            return;
        }
        private async Task<string> GenerateShortCode(string longUrl)
        {
            string shortCode;
            bool codeExists;
            int retryCount = 0;
            int maxRetries = 5;
            do
            {
                _logger.LogInformation("Generating new short code...");
                shortCode = Url64Helper.Encode(longUrl + Guid.NewGuid());
                codeExists = await _dbContext.UrlMappings.AnyAsync(c => c.ShortCode == shortCode);
                retryCount++;
            }
            while (codeExists && retryCount <= maxRetries);

            if (retryCount >= maxRetries)
            {
                _logger.LogError("Failed to generate unique short code.");
                throw new Exception("Failed to generate unique short code.");
            }
            return shortCode;
        }
        private ShortenUrlResposeDto CreateShortenUrlResponse(string shortCode)
        {
            return new ShortenUrlResposeDto
            {
                ShortUrl = shortCode
            };
        }
    }
}
