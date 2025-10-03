using BurningFuryApi.Services;
using BurningFuryApi.Configuration;
using BurningFuryApi.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using BurningFuryApi.Authentication;
using Microsoft.AspNetCore.Authentication;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog first
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

try
{
    // Configure logging early (Serilog already set). Remove default providers.
    builder.Logging.ClearProviders();

    // Add Auth0 configuration with null checking
    builder.Services.Configure<Auth0Settings>(builder.Configuration.GetSection("Auth0"));
    var auth0Settings = builder.Configuration.GetSection("Auth0").Get<Auth0Settings>();

    // Add services to the container.
    builder.Services.AddControllers();

    // Register PlayerService
    builder.Services.AddScoped<IPlayerService, PlayerService>();
    // Register Feedback service + HttpClient
    builder.Services.AddHttpClient<IFeedbackService, FeedbackService>();

    // Rate limiting (10 feedback submissions per IP per hour)
    builder.Services.AddRateLimiter(options =>
    {
        options.AddFixedWindowLimiter("feedback", limiterOptions =>
        {
            limiterOptions.Window = TimeSpan.FromHours(1);
            limiterOptions.PermitLimit = 10;
            limiterOptions.QueueLimit = 0;
            limiterOptions.AutoReplenishment = true;
        });
    });

    // Composite authentication: JWT + API Key
    var authBuilder = builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "Composite"; // custom scheme selecting first successful
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddPolicyScheme("Composite", "JWT or API Key", opts =>
    {
        opts.ForwardDefaultSelector = context =>
        {
            if (context.Request.Headers.ContainsKey(ApiKeyAuthenticationHandler.HeaderName) ||
                context.Request.Query.ContainsKey("api_key"))
            {
                return "ApiKey";
            }
            return JwtBearerDefaults.AuthenticationScheme;
        };
    })
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", _ => { });

    if (!string.IsNullOrEmpty(auth0Settings?.Domain) && !string.IsNullOrEmpty(auth0Settings?.Audience))
    {
        authBuilder.AddJwtBearer(options =>
        {
            options.Authority = $"https://{auth0Settings.Domain}/";
            options.Audience = auth0Settings.Audience;
            options.RequireHttpsMetadata = false; // Set to true in production if using HTTPS
            options.Challenge = string.Empty; // allow anonymous gracefully
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = $"https://{auth0Settings.Domain}/",
                ValidateAudience = true,
                ValidAudience = auth0Settings.Audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.Zero,
                NameClaimType = ClaimTypes.NameIdentifier
            };
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    try
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                        logger.LogWarning("Authentication failed: {Error} for path {Path}", context.Exception.Message, context.HttpContext.Request.Path);
                        var endpoint = context.HttpContext.GetEndpoint();
                        var allowAnonymous = endpoint?.Metadata?.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() != null;
                        if (allowAnonymous)
                        {
                            context.NoResult();
                            return Task.CompletedTask;
                        }
                    }
                    catch { }
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    try
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                        var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.Principal?.FindFirst("sub")?.Value;
                        logger.LogInformation("Token validated for user: {UserId}", userId);
                    }
                    catch { }
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    try
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                        var endpoint = context.HttpContext.GetEndpoint();
                        var allowAnonymous = endpoint?.Metadata?.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() != null;
                        if (allowAnonymous)
                        {
                            context.HandleResponse();
                            return Task.CompletedTask;
                        }
                    }
                    catch { }
                    return Task.CompletedTask;
                }
            };
        });
    }
    else
    {
        // Add basic authentication scheme even if Auth0 is not configured
        builder.Services.AddAuthentication("Bearer")
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, Microsoft.AspNetCore.Authentication.AuthenticationHandler<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>>("Bearer", null);
    }

    // Add Authorization with fallback policy
    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireAssertion(_ => true)
            .Build();
    });

    // Add CORS if needed for frontend applications
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        });
    });

    // Add Swagger/OpenAPI services
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "BurningFury API",
            Version = "v1",
            Description = "API for managing players in BurningFury with Auth0 authentication",
            Contact = new Microsoft.OpenApi.Models.OpenApiContact { Name = "BurningFury Team" }
        });
        try
        {
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
        }
        catch { }

        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Example: 'Bearer 12345abcdef'",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });
        c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
        {
            Description = "API Key via X-Api-Key header",
            Name = ApiKeyAuthenticationHandler.HeaderName,
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                }, new List<string>()
            },
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
                }, new List<string>()
            }
        });
    });

    var app = builder.Build();

    app.UseJwtErrorHandling();

    try
    {
        using var scope = app.Services.CreateScope();
        var playerService = scope.ServiceProvider.GetRequiredService<IPlayerService>();
        await playerService.InitializeDatabaseAsync();
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Failed to initialize database. The application will continue but database operations may fail.");
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "BurningFury API v1");
            c.RoutePrefix = string.Empty;
        });
    }

    app.UseCors();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapGet("/health", () => new { Status = "Healthy", Timestamp = DateTime.UtcNow });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
