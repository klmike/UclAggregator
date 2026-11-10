using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using UclAggregator.Errors;
using UclAggregator.Metrics.Api;
using UclAggregator.Models;

namespace UclAggregator.Auth;

public static class AuthRegistration
{
    public static IServiceCollection AddJwtAuth(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<JwtOptions>(config.GetSection("Jwt"));
        services.Configure<DevUserOptions>(config.GetSection("DevUser"));
        services.AddSingleton<TokenService>();

        var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        if (jwt.SigningKey.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                // Single service issuing and consuming its own tokens, so issuer/audience checks add nothing.
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    IssuerSigningKey = TokenService.CreateKey(jwt),
                };
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
                };
            });
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        return services;
    }
}
