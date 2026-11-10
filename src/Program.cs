using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;
using UclAggregator.Auth;
using UclAggregator.Clients;
using UclAggregator.Errors;
using UclAggregator.Metrics;
using UclAggregator.Metrics.Api;
using UclAggregator.Models;
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

        services.AddJwtAuth(config);
        services.AddMemoryCache();

        services.Configure<AggregatorOptions>(config.GetSection("Aggregator"));

        services.AddAggregatorMetrics();
        services.AddApiClients(config);

        services.AddScoped<FootballDataService>();
        services.AddScoped<NewsService>();
        services.AddScoped<WeatherService>();
        services.AddScoped<ReportService>();
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
