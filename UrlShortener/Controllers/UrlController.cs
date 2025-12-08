using CorrelationId;
using CorrelationId.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using UrlShortener.Data;
using UrlShortener.Models;
using UrlShortener.Models.DTOs;
using Utilities.Encode;

namespace UrlShortener.Controllers
{
    [EnableRateLimiting("PerIpLimit")]
    [ApiController]
    [Route("/")]
    public class UrlController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public readonly IConnectionMultiplexer _redis;
        private readonly ILogger<UrlController> _logger;
        private readonly ICorrelationContextAccessor _correlationAccessor;

        public UrlController(
            ApplicationDbContext context,
            IConnectionMultiplexer redis,
            ILogger<UrlController> logger,
            ICorrelationContextAccessor correlationId)
        {
            _context = context;
            _redis = redis;
            _logger = logger;
            _correlationAccessor = correlationId;
        }

        /// <summary>
        /// Shortens the given URL and returns a short code.
        /// </summary>
        /// <returns>Short url but only short code is posted to db.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(UrlMappingDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> ShortenUrl(UrlMappingDto model)
        {
            var correlationId = _correlationAccessor.CorrelationContext?.CorrelationId;
            _logger.LogInformation("NEW REQUEST: {CorrelationID}", correlationId);

            // Scheme validations
            var longUrl = EnsureUrlHasScheme(model.OriginalUrl);

            if (!Uri.TryCreate(longUrl, UriKind.Absolute, out var uriResult) ||
                (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
                return BadRequest("Invalid URL format.");

            // If record already exists in cache return short url
            try
            {
                var cachedCode = await CheckRedis(_redis, null, longUrl);
                if (!cachedCode.IsNullOrEmpty)
                {
                    _logger.LogInformation($"Cache hit for url: {longUrl.Replace(Environment.NewLine, "")} <=> {cachedCode}");
                    return Ok(CreateShortenUrlResponse(cachedCode!, HttpContext));
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error occured with redis");
            }

            // If record is in db but not cache, add to cache then return short url
            _logger.LogInformation("Checking db for record...");
            var record = await _context.UrlMappings.FirstOrDefaultAsync(x => x.OriginalUrl == longUrl);
            if (record != null && record.OriginalUrl != null && record.ShortCode != null)
            {
                try
                {
                    _logger.LogInformation($"Cache miss for url: {record.OriginalUrl}");
                    await AddRecordToCache(record.ShortCode, record.OriginalUrl, _redis, _logger);
                    return Ok(CreateShortenUrlResponse(record.ShortCode, HttpContext));
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Failed to cache URL mapping for {record.OriginalUrl}");
                    return Ok(CreateShortenUrlResponse(record.ShortCode, HttpContext));
                }
            }

            // Short code needs to be unique before adding to db
            var shortCode = await GenerateShortCode(longUrl, _context);

            var mapping = new UrlMapping
            {
                OriginalUrl = longUrl,
                ShortCode = shortCode,
                CreatedAt = DateTime.UtcNow
            };

            // Save record in db 
            try
            {
                await _context.UrlMappings.AddAsync(mapping);
                await _context.SaveChangesAsync();
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, $"Duplicate short code generated {shortCode}.");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            // Cache record
            try
            {
                await AddRecordToCache(shortCode, longUrl, _redis, _logger);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to cache URL mapping for {longUrl.Replace(Environment.NewLine, "")}");
            }

            return Ok(CreateShortenUrlResponse(shortCode, HttpContext));
        }


        /// <summary>
        /// Redirects from short URL to original URL if browser or returns original URL if api call
        /// </summary>
        [HttpGet("{shortCode}")]
        [ProducesResponseType(StatusCodes.Status302Found)]
        [ProducesResponseType(typeof(UrlMappingDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> RedirectToOriginal(string shortCode)
        {
            var correlationId = _correlationAccessor.CorrelationContext?.CorrelationId;
            _logger.LogInformation("NEW REQUEST: {CorrelationID}", correlationId);
            shortCode = shortCode.Replace(Environment.NewLine, "");

            var isBrowser = IsBrowserRequest(Request);
            _logger.LogInformation($"Is Browser: {isBrowser}");
            try
            {
                var cachedUrl = await CheckRedis(_redis, shortCode, null);
                if (!cachedUrl.IsNullOrEmpty)
                {
                    _logger.LogInformation($"Cache hit for short code: {shortCode}");
                    if (isBrowser)
                        return Redirect(cachedUrl.ToString());

                    return Ok(new { originalUrl = cachedUrl.ToString() });
                }
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "An error occured with Redis.");
            }
            
            //If record isnt in cache, check database 
            var record = await _context.UrlMappings.FirstOrDefaultAsync(x => x.ShortCode == shortCode);
            if (record == null) 
                return NotFound("Short URL not found.");

            //Store record in cache
            _logger.LogInformation($"Cache miss for short code: {shortCode}. Caching now.");
            try
            {
                await AddRecordToCache(record.ShortCode!, record.OriginalUrl, _redis, _logger);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "An error occured with Redis.");
            }

            if (isBrowser)
                return Redirect(record.OriginalUrl);

            return Ok(new { originalUrl = record.OriginalUrl });
        }


        /*--------
         Helpers
        ---------*/
        private async Task<string> GenerateShortCode(string longUrl, ApplicationDbContext _context)
        {
            string shortCode;
            bool codeExists;
            int retryCount = 0;
            int maxRetries = 5;
            do
            {
                _logger.LogInformation("Generating new short code...");
                shortCode = Url64Helper.Encode(longUrl + Guid.NewGuid());
                codeExists = await _context.UrlMappings.AnyAsync(c => c.ShortCode == shortCode);
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
        private async Task<RedisValue> CheckRedis(IConnectionMultiplexer _redis, string? shortCode, string? longUrl)
        {
            shortCode = shortCode?.Replace(Environment.NewLine, "");
            longUrl = longUrl?.Replace(Environment.NewLine, "");
            _logger.LogInformation("Checking redis for [USER INPUT]: {LongUrl} <=> {ShortCode}...", longUrl, shortCode);
            var db = _redis.GetDatabase();
            if (!String.IsNullOrEmpty(shortCode))
                return await db.StringGetAsync(shortCode);

            if (!String.IsNullOrEmpty(longUrl))
                return await db.StringGetAsync($"url:{longUrl}");

            return RedisValue.Null;
        }
        private ShortenUrlResposeDto CreateShortenUrlResponse(string shortCode, HttpContext context)
        {
            return new ShortenUrlResposeDto
            {
                ShortUrl = DecorateShortCode(shortCode, context)
            };
        }
        private string DecorateShortCode(string shortCode, HttpContext context)
        {
            return $"{context.Request.Scheme}://{context.Request.Host}/{shortCode}";
        }
        private static async Task AddRecordToCache(string shortCode, string longUrl, IConnectionMultiplexer _redis, ILogger _logger)
        {
            longUrl = longUrl.Replace(Environment.NewLine, "");
            _logger.LogInformation("Caching record for [USER INPUT]: {LongUrl}...", longUrl);
            var db = _redis.GetDatabase();
            await db.StringSetAsync($"url:{longUrl}", shortCode, TimeSpan.FromDays(30));
            await db.StringSetAsync(shortCode, longUrl, TimeSpan.FromDays(30));
            return;
        }
        private string EnsureUrlHasScheme(string url)
        {
            _logger.LogInformation("Validating URL scheme...");
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                return "https://" + url;
            return url;
        }
        private static bool IsBrowserRequest(HttpRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request), "HTTP request cannot be null");

            if (request.Headers.TryGetValue("User-Agent", out var userAgent))
            {
                if (userAgent.ToString().ToLower().Contains("postman"))
                    return false;
            }

            if (request.Headers.TryGetValue("Sec-Fetch-Mode", out var mode))
            {
                if (mode.ToString().ToLower() == "cors")
                    return false;
            }

            if (request.Headers.TryGetValue("Referer", out var referer))
            {
                if (referer.ToString().ToLower().Contains("swagger"))
                    return false;
            }
            return true;
        }
    }
}