using CorrelationId;
using CorrelationId.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StackExchange.Redis;
using System.Threading.RateLimiting;
using UrlShortener.Data;
using UrlShortener.Middleware;
using UrlShortener.Services;
using UrlShortener.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Logger
builder.Host.UseSerilog((context, config) =>
{
    config
    .Enrich.FromLogContext()
    .Enrich.WithCorrelationId()
    .ReadFrom.Configuration(context.Configuration);
});

// Correlation Id
builder.Services.AddDefaultCorrelationId(options =>
{
    options.UpdateTraceIdentifier = true;
    options.AddToLoggingScope = true;
    options.IncludeInResponse = true;
});

// Database & Redis
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        npgsql =>
        {
            npgsql.EnableRetryOnFailure(
                maxRetryCount: 10,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null
            );
        });
});
var redisHost = builder.Configuration["Redis:Host"] ?? "localhost:6379";
var redis = ConnectionMultiplexer.Connect(new ConfigurationOptions
{
    EndPoints = { redisHost },
    AbortOnConnectFail = false, // keeps retrying until ready
    ConnectRetry = 5,
    ConnectTimeout = 5000
});
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

builder.Services.AddScoped<IExpandUrlService, ExpandUrlService>();

// Health Check Service
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("Database")
    .AddRedis(redis);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);
    options.SwaggerDoc("v1", new() { Title = "URL Shortener API", Version = "v1" });
});

//Rate Limiter Service
builder.Services.AddRateLimiter(option =>
{
    option.OnRejected = async (context, token) =>
    {
        var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString();
        Console.WriteLine($"Rate limit triggered for IP {ip} at {DateTime.UtcNow}");

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "Rate limit exceeded. Please wait before retrying."
        }, cancellationToken: token);
    };

    option.AddPolicy("PerIpLimit", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromSeconds(10),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });
});

//CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

builder.WebHost.UseUrls("http://+:80");

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCorrelationId();
app.MapHealthChecks("/health");
app.UseRateLimiter();
app.UseCors("AllowAll");
app.UseHttpsRedirection();
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Test"))
{
    //Apply pending migrations if any
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var pendingMigrations = dbContext.Database.GetPendingMigrations();

        if (pendingMigrations.Any())
        {
            Console.WriteLine("Applying pending migrations...");
            dbContext.Database.Migrate();
            Console.WriteLine("Migrations applied successfully.");
        }
        else
            Console.WriteLine("No pending migrations found. Database is up to date.");
    }

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("v1/swagger.json", "UrlShorterner API");
    });
}
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }
