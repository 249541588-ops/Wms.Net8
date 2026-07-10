using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Wms.Core.WebApi.Configuration;
using Wms.Core.WebApi.Security;
using Wms.Core.WebApi.Services;

namespace Wms.Core.WebApi.Extensions;

public static class AuthenticationExtensions
{
    /// <summary>
    /// 配置 JWT 认证和授权
    /// </summary>
    public static IServiceCollection AddWmsAuthentication(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        var secretKey = jwtSection["SecretKey"];

        // 验证 JWT 配置
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException(
                "JWT SecretKey is missing in configuration. " +
                "Please set 'Jwt:SecretKey' in appsettings.json or use environment variable 'Jwt__SecretKey'. " +
                "The key must be at least 32 characters long.");
        }

        if (secretKey.Length < 32)
        {
            throw new InvalidOperationException(
                $"JWT SecretKey is too short. Current length: {secretKey.Length}, Minimum required: 32 characters. " +
                "Please use a stronger secret key in production.");
        }

        // 检查是否使用了默认密钥（仅在生产环境警告）
        if (environment.IsProduction() &&
            (secretKey.StartsWith("YourSuperSecretKey") ||
             secretKey.StartsWith("DevelopmentSecretKey") ||
             secretKey.StartsWith("CHANGE_ME")))
        {
            throw new InvalidOperationException(
                "Insecure JWT SecretKey detected in production environment. " +
                "Please change the 'Jwt:SecretKey' in appsettings.Production.json or use environment variable 'Jwt__SecretKey'.");
        }

        var key = Encoding.UTF8.GetBytes(secretKey);

        // 配置安全选项
        var securitySection = configuration.GetSection(SecurityOptions.SectionName);
        var requireHttps = securitySection.GetValue<bool>("RequireHttps");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            // 根据配置决定是否要求 HTTPS
            options.RequireHttpsMetadata = requireHttps && !environment.IsDevelopment();
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSection["Issuer"],
                ValidAudience = jwtSection["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.Zero
            };

            // R503: JWT 黑名单校验
            // 每次通过基础签名/过期校验后，再到分布式缓存检查 jti 是否已被吊销。
            // fail-open 策略：缓存后端故障时 IsRevokedAsync 返回 false（放行），
            // 避免单点故障锁死整个认证系统；代价是故障期间被吊销 token 仍可用至自然过期。
            // C6: OnMessageReceived 必须在 OnTokenValidated 之前，先从 query string 提取
            // SignalR WebSocket 连接携带的 access_token，后续签名/黑名单校验自动复用。
            options.Events = new JwtBearerEvents
            {
                // C6: SignalR WebSocket 连接无法使用 Authorization 头，
                // 需从 query string 的 access_token 提取 JWT（仅限 /hubs 路径，避免普通 API 接受 query token）。
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) &&
                        path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                    if (string.IsNullOrEmpty(jti))
                    {
                        // 没有 jti claim 的 token 视为非法（TokenService 始终写入 jti）
                        context.Fail("Token missing jti claim");
                        return;
                    }

                    var blacklist = context.HttpContext.RequestServices
                        .GetRequiredService<IJwtBlacklistService>();

                    if (await blacklist.IsRevokedAsync(jti))
                    {
                        context.Fail("Token has been revoked");
                    }
                }
            };
        });

        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// 配置 CORS 策略
    /// </summary>
    public static IServiceCollection AddWmsCors(this IServiceCollection services, IConfiguration configuration)
    {
        var securitySection = configuration.GetSection(SecurityOptions.SectionName);
        var allowedOrigins = securitySection.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

        services.AddCors(options =>
        {
            options.AddPolicy("AllowConfiguredOrigins", policy =>
            {
                // 统一策略：开发与生产均使用白名单 + AllowCredentials
                // SignalR negotiate 请求默认 credentials='include'，浏览器禁止响应头为通配符 '*'，
                // 因此必须用 WithOrigins（回显具体 origin）配合 AllowCredentials，不能用 AllowAnyOrigin。
                if (allowedOrigins.Length == 0)
                {
                    throw new InvalidOperationException(
                        "Security:AllowedOrigins is not configured. " +
                        "Please set allowed origins in appsettings.json/appsettings.{Environment}.json " +
                        "or use environment variable 'Security__AllowedOrigins'.");
                }

                policy.WithOrigins(allowedOrigins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            });
        });

        return services;
    }
}
