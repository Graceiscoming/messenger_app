using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Messenger.Api.Hubs;
using Messenger.Api.Infrastructure;
using Messenger.Api.Middleware;
using Messenger.Api.Realtime;
using Messenger.Data;
using Messenger.Domain;
using Messenger.Domain.Abstractions;
using Messenger.Domain.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

const long MaxRequestBytes = 8 * 1024 * 1024; // > 5 MB image limit + multipart overhead; Domain returns the 413 for 5..8 MB

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---- Options (fail fast on a weak JWT key) ----
var jwt = config.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrEmpty(jwt.Key) || jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be configured and at least 32 characters long (set the Jwt__Key environment variable).");
const string DevOnlyJwtKey = "dev-only-insecure-key-change-me-0123456789abcdef";
if (builder.Environment.IsProduction() && jwt.Key == DevOnlyJwtKey)
    throw new InvalidOperationException("The development-only Jwt:Key must not be used in Production (set the Jwt__Key environment variable).");

builder.Services.Configure<ChatOptions>(config.GetSection(ChatOptions.SectionName));
builder.Services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
builder.Services.Configure<StorageOptions>(config.GetSection(StorageOptions.SectionName));
builder.Services.Configure<AuthOptions>(config.GetSection(AuthOptions.SectionName));

// ---- Layers ----
var connectionString = config.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
builder.Services.AddDomain();
builder.Services.AddData(connectionString);

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<Messenger.Domain.Abstractions.IPasswordHasher, PasswordHasherAdapter>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddSingleton<IImageInspector, ImageInspector>();
builder.Services.AddSingleton<IFileStore, LocalFileStore>();
builder.Services.AddSingleton<PresenceTracker>();
builder.Services.AddSingleton<IPresenceTracker>(sp => sp.GetRequiredService<PresenceTracker>());
builder.Services.AddSingleton<IRealtimeNotifier, SignalRNotifier>();

// ---- MVC / JSON ----
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
    })
    .ConfigureApiBehaviorOptions(o =>
    {
        // Model-binding / malformed-JSON errors -> RFC 7807 400 with an `errors` map.
        o.InvalidModelStateResponseFactory = ctx =>
        {
            var problem = new ValidationProblemDetails(ctx.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Type = "https://httpstatuses.io/400",
            };
            problem.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
            return new ObjectResult(problem)
            {
                StatusCode = StatusCodes.Status400BadRequest,
                ContentTypes = { "application/problem+json" },
            };
        };
    });

builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = MaxRequestBytes);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = MaxRequestBytes);

// ---- Auth ----
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false; // keep "sub" / "unique_name" as-is
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "unique_name",
        };
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization(o =>
    o.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser().Build());

// ---- Forwarded headers (behind nginx / reverse proxy) ----
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Only trust proxies inside these ranges (default: private ranges incl. the docker network).
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
    var nets = config.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>()
        ?? new[] { "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16" };
    foreach (var n in nets)
    {
        var parts = n.Split('/');
        o.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse(parts[0]), int.Parse(parts[1])));
    }
});

// ---- Rate limiting (auth endpoints) ----
var authPerMinute = config.GetValue("RateLimit:AuthPerMinute", 20);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = authPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await ctx.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = 429,
            Title = "Too many requests",
            Detail = "Rate limit exceeded. Try again later.",
            Type = "https://httpstatuses.io/429",
        }, (JsonSerializerOptions?)null, "application/problem+json", ct);
    };
});

// ---- CORS ----
var origins = config.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    if (origins.Length > 0)
        p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));

// ---- SignalR ----
builder.Services.AddSignalR().AddJsonProtocol(o =>
{
    o.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
});

// ---- Swagger ----
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Messenger API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT access token",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }]
            = Array.Empty<string>(),
    });
});

var app = builder.Build();

if (config.GetValue("RunMigrations", false))
    await app.Services.MigrateDatabaseAsync();

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment() && config.GetValue("Https:Enabled", false))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

app.Run();

public partial class Program;
