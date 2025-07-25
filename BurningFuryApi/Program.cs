using BurningFuryApi.Services;
using BurningFuryApi.Configuration;
using BurningFuryApi.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

try
{
    // Configure logging early
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();
    builder.Logging.AddEventSourceLogger();

    // Add Auth0 configuration with null checking
    builder.Services.Configure<Auth0Settings>(builder.Configuration.GetSection("Auth0"));
    var auth0Settings = builder.Configuration.GetSection("Auth0").Get<Auth0Settings>();

    // Add services to the container.
    builder.Services.AddControllers();

    // Register PlayerService
    builder.Services.AddScoped<IPlayerService, PlayerService>();

    // Add Authentication with error handling
    if (!string.IsNullOrEmpty(auth0Settings?.Domain) && !string.IsNullOrEmpty(auth0Settings?.Audience))
    {
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = $"https://{auth0Settings.Domain}/";
                options.Audience = auth0Settings.Audience;
                options.RequireHttpsMetadata = false; // Set to true in production if using HTTPS
                
                // Important: Don't challenge on authentication failure for anonymous endpoints
                options.Challenge = string.Empty;
                
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
                
                // Custom event handlers for better logging and graceful failure handling
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        try
                        {
                            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                            logger.LogWarning("Authentication failed: {Error} for path {Path}", 
                                context.Exception.Message, context.HttpContext.Request.Path);
                            
                            // Check if the endpoint allows anonymous access
                            var endpoint = context.HttpContext.GetEndpoint();
                            var allowAnonymous = endpoint?.Metadata?.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() != null;
                            
                            if (allowAnonymous)
                            {
                                // For anonymous endpoints, don't fail the authentication
                                logger.LogInformation("Allowing anonymous access to {Path} despite token validation failure", 
                                    context.HttpContext.Request.Path);
                                context.NoResult();
                                return Task.CompletedTask;
                            }
                        }
                        catch
                        {
                            // Ignore logging errors during startup
                        }
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        try
                        {
                            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                            var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
                                       context.Principal?.FindFirst("sub")?.Value;
                            logger.LogInformation("Token validated for user: {UserId}", userId);
                        }
                        catch
                        {
                            // Ignore logging errors during startup
                        }
                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        try
                        {
                            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                            
                            // Check if the endpoint allows anonymous access
                            var endpoint = context.HttpContext.GetEndpoint();
                            var allowAnonymous = endpoint?.Metadata?.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() != null;
                            
                            if (allowAnonymous)
                            {
                                logger.LogInformation("Skipping challenge for anonymous endpoint {Path}", 
                                    context.HttpContext.Request.Path);
                                context.HandleResponse();
                                return Task.CompletedTask;
                            }
                        }
                        catch
                        {
                            // Ignore logging errors
                        }
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
        // Set a default policy that doesn't require authentication
        // Individual endpoints can override this with [Authorize]
        options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireAssertion(_ => true) // Always allow access unless explicitly denied
            .Build();
    });

    // Add CORS if needed for frontend applications
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
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
            Contact = new Microsoft.OpenApi.Models.OpenApiContact
            {
                Name = "BurningFury Team"
            }
        });

        // Set the comments path for the Swagger JSON and UI with error handling
        try
        {
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                c.IncludeXmlComments(xmlPath);
            }
        }
        catch
        {
            // Ignore XML documentation errors
        }

        // Add JWT Authentication to Swagger
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = @"JWT Authorization header using the Bearer scheme. 
                          Enter 'Bearer' [space] and then your token in the text input below.
                          Example: 'Bearer 12345abcdef'",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement()
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    },
                    Scheme = "oauth2",
                    Name = "Bearer",
                    In = ParameterLocation.Header,
                },
                new List<string>()
            }
        });
    });

    var app = builder.Build();

    // Add custom JWT error handling middleware
    app.UseJwtErrorHandling();

    // Initialize database with error handling
    try
    {
        using (var scope = app.Services.CreateScope())
        {
            var playerService = scope.ServiceProvider.GetRequiredService<IPlayerService>();
            await playerService.InitializeDatabaseAsync();
        }
    }
    catch (Exception ex)
    {
        // Log the database initialization error but don't crash the app
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Failed to initialize database. The application will continue but database operations may fail.");
    }

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "BurningFury API v1");
            c.RoutePrefix = string.Empty; // This makes Swagger UI available at the root URL
        });
    }

    // Add CORS before authentication
    app.UseCors();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    // Add a simple health check endpoint
    app.MapGet("/health", () => new { Status = "Healthy", Timestamp = DateTime.UtcNow });

    app.Run();
}
catch (Exception ex)
{
    // Log startup errors
    Console.WriteLine($"Application startup failed: {ex}");
    
    // Try to create a minimal logger to log the error
    try
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<Program>();
        logger.LogCritical(ex, "Application failed to start");
    }
    catch
    {
        // If logging fails, at least write to console
        Console.WriteLine($"Critical startup error: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
    }
    
    throw; // Re-throw to ensure the process exits with an error code
}
