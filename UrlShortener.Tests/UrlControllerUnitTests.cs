using CorrelationId.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using UrlShortener.Controllers;
using UrlShortener.Data;
using UrlShortener.Models;
using UrlShortener.Models.DTOs;
using UrlShortener.Services.Interfaces;

namespace UrlShortener.Tests
{
    public class UrlControllerUnitTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;
        private readonly UrlController _controller;
        private readonly ApplicationDbContext _context;
        private readonly Mock<IConnectionMultiplexer> _redisMock;

        public UrlControllerUnitTests(WebApplicationFactory<Program> factory)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new ApplicationDbContext(options);

            var mockDatabase = new Mock<IDatabase>();
            mockDatabase
                .Setup(db => db.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<bool>(),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()
                ))
                .ReturnsAsync(true);

            mockDatabase
                .Setup(db => db.StringGetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<CommandFlags>()
                ))
                .ReturnsAsync((RedisValue)string.Empty);

            _redisMock = new Mock<IConnectionMultiplexer>();
            _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                      .Returns(mockDatabase.Object);

            var mockLogger = new Mock<ILogger<UrlController>>();

            var mockAccessor = new Mock<ICorrelationContextAccessor>();
            mockAccessor.Setup(a => a.CorrelationContext);

            var mockExpandUrl = new Mock<IExpandUrlService>();
            mockExpandUrl.Setup(x => x.ExpandUrlAsync(It.IsAny<string>()))
                .ReturnsAsync(new ExpandUrlResponseDto { OriginalUrl = "abc123" });

            var mockShortenUrl = new Mock<IShortenUrlService>();
            mockShortenUrl.Setup(x => x.ShortenUrlAsync(It.IsAny<UrlMappingDto>()))
                .ReturnsAsync(new ShortenUrlResposeDto { ShortUrl = "https://www.test.com"});

            _controller = new UrlController(mockLogger.Object, mockAccessor.Object, mockExpandUrl.Object, mockShortenUrl.Object);
            _client = factory.CreateClient();
        }
        [Fact]
        public async Task HealthEndpoint_ShouldReturnSuccess()
        {
            //Act
            var response = await _client.GetAsync("/health");

            //Assert
            Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK);
        }

        [Fact]
        public async Task ShortenUrl_AddsHttpsIfMissing()
        {
            //Arrange
            var dto = new UrlMappingDto { OriginalUrl = "www.testr.com" };

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
                {
                    Request =
                    {
                        Scheme = "https",
                        Host = new HostString("localhost:8080")
                    }
                }
            };

            //Act
            var result = await _controller.ShortenUrlAsync(dto) as OkObjectResult;
            var response = Assert.IsType<ShortenUrlResposeDto>(result?.Value);
            var shortUrl = response.ShortUrl;

            //Assert
            Assert.StartsWith("https", shortUrl);

        }

        [Theory]
        [InlineData("User-Agent", "postman")]
        [InlineData("Sec-Fetch-Mode", "cors")]
        [InlineData("Referer", "swagger")]
        public async Task IsBrowserRequest_IfNotBrowser_Returns200Ok(string headerName, string headerValue)
        {
            //Arrange
            var record = new UrlMapping
            {
                OriginalUrl = "www.test.com",
                ShortCode = "cEdfxXXd",
                CreatedAt = DateTime.UtcNow
            };
            await _context.UrlMappings.AddAsync(record);
            await _context.SaveChangesAsync();

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            _controller.Request.Headers[headerName] = headerValue;

            //Act
            var result = await _controller.ExpandUrlAsync("cEdfxXXd");

            //Assert
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task IsBrowserRequest_IfEmpty_ReturnsNullException()
        {
            //Arrange
            var record = new UrlMapping
            {
                OriginalUrl = "www.test.com",
                ShortCode = "cEdfxXXd",
                CreatedAt = DateTime.UtcNow
            };
            _context.UrlMappings.Add(record);
            await _context.SaveChangesAsync();

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = null!
            };

            //Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => _controller.ExpandUrlAsync("cEdfxXXd"));

            //Assert
            Assert.Contains("HTTP request cannot be null", ex.Message);
        }

    }
}