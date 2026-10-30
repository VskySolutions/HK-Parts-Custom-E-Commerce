using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using HKPartsEcommerce.API.Authentication;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.API.Services;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Persistence;
using HKPartsEcommerce.Infrastructure.Security;

namespace HKPartsEcommerce.API;

/// <summary>Registers API/presentation-layer services: MVC, OpenAPI, health checks, auth, HTTP context.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers()
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddEndpointsApiExplorer();
        services.AddOpenApi();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database", tags: new[] { "ready" });

        // Authentication (WO-1 JWT + WO-95 API key): a composite policy scheme routes each request to
        // either RS256 JWT bearer or the X-Api-Key scheme. JWT bearer wins when an Authorization: Bearer
        // header is present; otherwise an X-Api-Key header selects the API-key scheme; failing both, the
        // JWT scheme runs and challenges with 401.
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = ApiKeyDefaults.PolicyScheme;
                options.DefaultChallengeScheme = ApiKeyDefaults.PolicyScheme;
            })
            .AddJwtBearer()
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyDefaults.Scheme, _ => { })
            .AddPolicyScheme(ApiKeyDefaults.PolicyScheme, "JWT or API Key", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    var authHeader = context.Request.Headers.Authorization.ToString();
                    if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        return JwtBearerDefaults.AuthenticationScheme;
                    if (context.Request.Headers.ContainsKey(ApiKeyDefaults.HeaderName))
                        return ApiKeyDefaults.Scheme;
                    return JwtBearerDefaults.AuthenticationScheme;
                };
            });
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<RsaKeyProvider>((options, keys) =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"] ?? "HKPartsEcommerce",
                    ValidateAudience = true,
                    ValidAudience = configuration["Jwt:Audience"] ?? "HKPartsEcommerce.Client",
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = keys.SecurityKey,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.SuperAdmin, p =>
                p.RequireRole(nameof(RoleType.SuperAdmin)));
            options.AddPolicy(Policies.TenantAdmin, p =>
                p.RequireRole(nameof(RoleType.SuperAdmin), nameof(RoleType.TenantAdmin)));
        });

        // Module-based access ([RequireModule(...)]): dynamic "module:*" policies + handler.
        services.AddSingleton<IAuthorizationPolicyProvider, ModulePolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, ModuleAuthorizationHandler>();

        return services;
    }
}
