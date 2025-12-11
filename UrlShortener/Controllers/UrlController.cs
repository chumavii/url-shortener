using CorrelationId.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UrlShortener.Models.DTOs;
using UrlShortener.Services.Interfaces;
using Utilities;

namespace UrlShortener.Controllers
{
    [EnableRateLimiting("PerIpLimit")]
    [ApiController]
    [Route("/")]
    public class UrlController : ControllerBase
    {
        private readonly ILogger<UrlController> _logger;
        private readonly ICorrelationContextAccessor _correlationAccessor;
        private readonly IExpandUrlService _expandUrl;
        private readonly IShortenUrlService _shortenUrl;

        public UrlController(ILogger<UrlController> logger, ICorrelationContextAccessor correlationId, IExpandUrlService expandUrl, IShortenUrlService shortenUrl)
        {
            _logger = logger;
            _correlationAccessor = correlationId;
            _expandUrl = expandUrl;
            _shortenUrl = shortenUrl;
        }

        /// <summary>
        /// Shortens the given URL and returns a short code.
        /// </summary>
        /// <returns>Short url but only short code is posted to db.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(UrlMappingDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> ShortenUrlAsync(UrlMappingDto model)
        {
            var correlationId = _correlationAccessor.CorrelationContext?.CorrelationId;
            _logger.LogInformation("NEW REQUEST: {CorrelationID}", correlationId);

            var result = await _shortenUrl.ShortenUrlAsync(model);
            if (result == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occured.");

            return Ok(CreateShortenUrlResponse(result.ShortUrl, HttpContext));
        }


        /// <summary>
        /// Redirects from short URL to original URL if browser or returns original URL if api call
        /// </summary>
        [HttpGet("{shortCode}")]
        [ProducesResponseType(StatusCodes.Status302Found)]
        [ProducesResponseType(typeof(UrlMappingDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> ExpandUrlAsync(string shortCode)
        {
            var correlationId = _correlationAccessor.CorrelationContext?.CorrelationId;
            _logger.LogInformation("NEW REQUEST: {CorrelationID}", correlationId);
            
            var isBrowser = RequestTypeHelper.CheckRequestType(Request);
            _logger.LogInformation($"Is Browser: {isBrowser}");

            var result = await _expandUrl.ExpandUrlAsync(shortCode);

            if (result == null)
                return NotFound("Short URL not found");

            if (isBrowser)
                return Redirect(result.OriginalUrl);
            return Ok(result);
        }


        /*--------
         Helpers
        ---------*/
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
    }
}