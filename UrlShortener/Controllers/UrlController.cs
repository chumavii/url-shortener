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

        public UrlController(ApplicationDbContext context, IConnectionMultiplexer redis, ILogger<UrlController> logger)
        {
            _context = context;
            _redis = redis;
            _logger = logger;
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
            if (string.IsNullOrEmpty(model.OriginalUrl))
                return BadRequest("URL cannot be empty");

            var originalUrl = EnsureUrlHasScheme(model.OriginalUrl);

            //Url scheme needs to be valid
            if (!Uri.TryCreate(originalUrl, UriKind.Absolute, out var uriResult) ||
                (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
                return BadRequest("Invalid URL format.");

            //If record already exists in cache return short url
            var db = _redis.GetDatabase();
            var cachedCode = await db.StringGetAsync($"url:{originalUrl}");
            if (!cachedCode.IsNullOrEmpty)
            {
                _logger.LogInformation($"Cache hit for short code: {cachedCode}");
                var resultDto = new ShortenUrlResposeDto
                {
                    ShortUrl = $"{Request.Scheme}://{Request.Host}/{cachedCode}"
                };
                return Ok(resultDto);
            }

            //If record is in db but not cache, add to cache then return short url
            var record = await _context.UrlMappings.FirstOrDefaultAsync(x => x.OriginalUrl == originalUrl);
            if (record != null && record.OriginalUrl != null)
            {
                try
                {
                    _logger.LogInformation($"Cache miss for short code: {cachedCode}. Caching now.");
                    await db.StringSetAsync($"url:{record.OriginalUrl}", record.ShortCode);
                    await db.StringSetAsync(record.ShortCode, record.OriginalUrl);
                }
                catch (Exception e)
                {
                    _logger.LogError($"Failed to cache URL mapping for {record.OriginalUrl}", e.Message);
                    var resultDto = new ShortenUrlResposeDto
                    {
                        ShortUrl = $"{Request.Scheme}://{Request.Host}/{record.ShortCode}"
                    };
                    return Ok(resultDto);
                }
            }

            //short code needs to be unique before adding to db
            string shortCode;
            bool codeExists;
            int retryCount = 0;
            int maxRetries = 3;

            do
            {
                _logger.LogDebug("Generating new short code.");
                shortCode = Url64Helper.Encode(originalUrl + Guid.NewGuid());
                codeExists = await _context.UrlMappings.AnyAsync(c => c.ShortCode == shortCode);
                retryCount++;
            }
            while (codeExists && retryCount <= maxRetries);

            if (retryCount >= maxRetries)
            {
                _logger.LogError("Failed to generate unique short code");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            var mapping = new UrlMapping
            {
                OriginalUrl = originalUrl,
                ShortCode = shortCode,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                _context.UrlMappings.Add(mapping);
                await _context.SaveChangesAsync();

                await db.StringSetAsync(shortCode, originalUrl);
                await db.StringSetAsync($"url:{originalUrl}", shortCode);
            }
            catch (DbUpdateException e) when (e.InnerException?.Message.Contains("duplicate") ?? false)
            {
                _logger.LogWarning(e, $"Duplicate short code generated {shortCode}. Retrying...");
                await ShortenUrl(model);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to cache URL mapping for {originalUrl}");
            }

            var result = new ShortenUrlResposeDto
            {
                ShortUrl = $"{Request.Scheme}://{Request.Host}/{shortCode}"
            };
            
            return Ok(result);
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
            var db = _redis.GetDatabase();
            var isBrowser = IsBrowserRequest(Request);
            _logger.LogInformation($"Is Browser: {isBrowser}");

            //Check if shortcode is stored in cache and return original URL if it is
            var cachedUrl = await db.StringGetAsync(shortCode);
            if (!cachedUrl.IsNullOrEmpty)
            {
                try
                {
                    _logger.LogInformation($"Cache hit for short code: {shortCode}");
                    if (isBrowser)
                        return Redirect(EnsureUrlHasScheme(cachedUrl.ToString()));

                    return Ok(new { originalUrl = cachedUrl.ToString() });
                }
                catch (Exception e)
                {
                    _logger.LogError(e.Message, $"Exception occurred while retrieving cached URL for short code: {shortCode}");
                }
            }

            //If record isnt in cache, check database 
            var entity = await _context.UrlMappings.FirstOrDefaultAsync(x => x.ShortCode == shortCode);
            if (entity == null) return NotFound("Short URL not found.");

            //Store record in cache
            _logger.LogInformation($"Cache miss for short code: {shortCode}. Caching now.");
            await db.StringSetAsync(shortCode, entity.OriginalUrl);

            if (isBrowser)
                return Redirect(entity.OriginalUrl);

            return Ok(new { originalUrl = entity.OriginalUrl });
        }


        /*--------
         Helpers
        ---------*/
        private string EnsureUrlHasScheme(string url)
        {
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
