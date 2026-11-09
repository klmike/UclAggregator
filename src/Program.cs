using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using UclAggregator.Auth;
using UclAggregator.Clients;
using UclAggregator.Errors;
using UclAggregator.Metrics;
using UclAggregator.Metrics.Api;
using UclAggregator.Metrics.Clients;
using UclAggregator.Models;
using UclAggregator.Clients.Espn;
using UclAggregator.Clients.GoogleNews;
using UclAggregator.Clients.Guardian;
using UclAggregator.Clients.OpenMeteo;
using UclAggregator.Clients.TheSportsDb;
using UclAggregator.Options;
using UclAggregator.Services;

namespace UclAggregator;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        ConfigureServices(builder.Services, builder.Configuration);

        var app = builder.Build();
        ConfigurePipeline(app);
        app.Run();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services
            .AddControllers()
            .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ctx =>
            {
                var message = string.Join(' ', ctx.ModelState.Values
                    .SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Where(m => m.Length > 0));
                ctx.HttpContext.Items[ApiMetricsMiddleware.ErrorCodeKey] = ErrorCode.InvalidRequest;
                var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Validation");
                logger.LogWarning("{Path} -> {Code}: {Message}", ctx.HttpContext.Request.Path, ErrorCode.InvalidRequest, message);
                return new ObjectResult(ApiResponse.Failure(ErrorCode.InvalidRequest, message))
                {
                    StatusCode = ErrorCode.InvalidRequest.ToStatusCode(),
                };
            })
            .AddJsonOptions(o => o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);
        services.AddOpenApi();

        services.Configure<JwtOptions>(config.GetSection("Jwt"));
        services.Configure<DevUserOptions>(config.GetSection("DevUser"));
        services.AddSingleton<TokenService>();
        var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        if (jwt.SigningKey.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                IssuerSigningKey = TokenService.CreateKey(jwt),
            })
            .Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
                o.Events = new JwtBearerEvents
                {
                    OnChallenge = ctx =>
                    {
                        ctx.HandleResponse();
                        ctx.HttpContext.Items[ApiMetricsMiddleware.ErrorCodeKey] = ErrorCode.Unauthorized;
                        ctx.Response.StatusCode = ErrorCode.Unauthorized.ToStatusCode();
                        return ctx.Response.WriteAsJsonAsync(
                            ApiResponse.Failure(ErrorCode.Unauthorized, "A valid bearer token is required."));
                    },
                });
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        services.AddProblemDetails();
        services.AddMemoryCache();

        services.Configure<AggregatorOptions>(config.GetSection("Aggregator"));
        services.Configure<EspnOptions>(config.GetSection("Espn"));
        services.Configure<OpenMeteoOptions>(config.GetSection("OpenMeteo"));
        services.Configure<GoogleNewsOptions>(config.GetSection("GoogleNews"));
        services.Configure<GuardianOptions>(config.GetSection("Guardian"));

        services.AddAggregatorMetrics();
        services.AddApiClient<EspnClient>(config, "Espn");
        services.AddApiClient<TheSportsDbClient>(config, "TheSportsDb");
        services.AddApiClient<OpenMeteoClient>(config, "OpenMeteo");
        services.AddApiClient<GoogleNewsClient>(config, "GoogleNews");
        services.AddApiClient<GuardianClient>(config, "Guardian");

        services.AddSingleton<FootballDataService>();
        services.AddSingleton<INewsSource>(sp => sp.GetRequiredService<GoogleNewsClient>());
        services.AddSingleton<INewsSource>(sp => sp.GetRequiredService<GuardianClient>());
        services.AddSingleton<INewsSource, EspnNewsSource>();
        services.AddSingleton<NewsService>();
        services.AddSingleton<WeatherService>();
        services.AddSingleton<ReportService>();
    }

    private static void ConfigurePipeline(WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
            app.MapScalarApiReference().AllowAnonymous();
        }

        app.UseExceptionHandler(errors => errors.Run(async context =>
        {
            var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;
            var error = ApiError.From(ex ?? new InvalidOperationException(), "The request");
            var level = error.Code.ToStatusCode() < 500 ? LogLevel.Warning : LogLevel.Error;
            app.Logger.Log(level, ex, "Request failed: {Method} {Path} -> {Code}: {Message}", context.Request.Method, context.Request.Path, error.Code, error.Message);
            context.Response.StatusCode = error.Code.ToStatusCode();
            await context.Response.WriteAsJsonAsync(ApiResponse.Failure(error.Code, error.Message));
        }));
        app.UseHttpsRedirection();
        app.UseMiddleware<ApiMetricsMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
    }
}
