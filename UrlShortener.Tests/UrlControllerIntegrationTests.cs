using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Text.Json;
using UrlShortener.Data;

namespace UrlShortener.Tests
{
    public class UrlControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;
        private readonly WebApplicationFactory<Program> _factory;

        public UrlControllerIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task ShortenUrl_ShouldShortenUrl()
        {
            //Arrange
            var longUrl = "https://testurl.com/thistestcase";

            //Act
            var response = await _client.PostAsJsonAsync("/", new { originalUrl = longUrl });

            //Assert
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json.TryGetProperty("shortUrl", out var shortUrlProperty));
            var shortUrl = shortUrlProperty.GetString();
            Assert.NotNull(shortUrl);
            Assert.StartsWith("http://localhost/", shortUrl);

            Console.WriteLine($"Shortened URL: {shortUrl}");

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var record = await db.UrlMappings.FirstOrDefaultAsync(u => u.OriginalUrl == longUrl);
            Assert.NotNull(record);
            Assert.Equal(shortUrl.Split('/').Last(), record.ShortCode);
        }

        [Fact]
        public async Task ExpandUrl_ShouldReturnOriginalUrl()
        {
            // Arrange
            var longUrl = "https://github.com/chumavii/UrlShortener/";

            var shortenResponse = await _client.PostAsJsonAsync("/", new { originalUrl = longUrl });
            shortenResponse.EnsureSuccessStatusCode();

            var json = await shortenResponse.Content.ReadFromJsonAsync<JsonElement>();
            var shortUrl = json.GetProperty("shortUrl").GetString();
            var shortCode = shortUrl!.Split('/').Last();

            //Simulate API client to ensure response is 200 and not 302 which is for browser requests
            _client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors"); 

            // Act
            var expandResponse = await _client.GetAsync($"/{shortCode}");
            expandResponse.EnsureSuccessStatusCode();

            var content = await expandResponse.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains(longUrl, content);
        }

        //[Fact]
        //public async Task RateLimiter_ShouldReturn429_AfterLimitReached()
        //{
        //    // Arrange
        //    var urlDto = new { originalUrl = "https://thisisatest.com" };
        //    HttpResponseMessage response = null!;

        //    // Act - send 6 requests quickly to exceed the 5/10s limit
        //    for (int i = 0; i < 6; i++)
        //    {
        //        response = await _client.PostAsJsonAsync("/", urlDto);
        //    }

        //    // Assert - last one should be throttled
        //    Assert.Equal(System.Net.HttpStatusCode.TooManyRequests, response.StatusCode);
        //}
    }
}