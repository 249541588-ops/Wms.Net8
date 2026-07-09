using Wms.Core.WebApi.HealthChecks;
using Wms.Core.WebApi.Hubs;
using Wms.Core.WebApi.Middleware;

namespace Wms.Core.WebApi.Extensions;

/// <summary>
/// HTTP 中间件管道配置扩展
/// </summary>
public static class MiddlewareExtensions
{
    /// <summary>
    /// 配置 HTTP 请求管道（严格顺序）
    /// </summary>
    public static WebApplication ConfigureMiddleware(this WebApplication app)
    {
        // ForwardedHeaders 必须在所有其他中间件之前
        app.UseForwardedHeaders();

        // 全局异常处理必须在最前面
        app.UseMiddleware<GlobalExceptionHandler>();

        // 安全响应头
        app.UseSecurityHeaders();

        // Swagger（仅开发环境）
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "WMS Core API v1");
                options.SwaggerEndpoint("/swagger/v2/swagger.json", "WMS Core API v2");
                options.RoutePrefix = string.Empty;
                options.DefaultModelsExpandDepth(-1);
                options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
            });
        }

        // 根据配置决定是否启用 HTTPS 重定向
        // RequireHttps=false 时跳过,避免对 CORS preflight 触发 307 重定向
        var requireHttps = app.Configuration.GetValue<bool>("Security:RequireHttps");
        if (requireHttps)
        {
            app.UseHttpsRedirection();
        }

        // 静态文件服务（上传文件访问 - 需要认证）
        var uploadBasePath = app.Configuration["Upload:BasePath"];
        if (string.IsNullOrWhiteSpace(uploadBasePath))
            uploadBasePath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        if (!Directory.Exists(uploadBasePath)) Directory.CreateDirectory(uploadBasePath);

        // uploads 路径需要认证才能访问
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/uploads") && !context.User.Identity?.IsAuthenticated == true)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            await next();
        });

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadBasePath),
            RequestPath = "/uploads"
        });

        app.UseCors("AllowConfiguredOrigins");

        // 响应缓存（必须在认证和授权之前）
        app.UseResponseCaching();

        // 语言包中间件
        app.UseLanguagePack();

        // 认证必须在授权之前
        app.UseAuthentication();
        app.UseAuthorization();

        // 速率限制
        app.UseMiddleware<RateLimitMiddleware>();

        // 端点映射
        app.MapControllers();
        // C6: SignalR Hub 路由层强制鉴权（与 WmsHub 类上的 [Authorize] 形成纵深防御）
        app.MapHub<WmsHub>("/hubs/wms").RequireAuthorization();

        // 健康检查端点
        app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";

                var result = System.Text.Json.JsonSerializer.Serialize(new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.Select(e => new
                    {
                        name = e.Key,
                        status = e.Value.Status.ToString(),
                        description = e.Value.Description,
                        duration = e.Value.Duration.TotalMilliseconds,
                        data = e.Value.Data,
                        exception = "参见服务端日志"
                    }),
                    total_duration_ms = report.TotalDuration.TotalMilliseconds
                },
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await context.Response.WriteAsync(result);
            }
        });

        // 健康检查端点（仅返回状态码，用于负载均衡器）
        app.MapHealthChecks("/healthz", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = _ => true,
            ResponseWriter = null
        });

        // 根路径返回 API 基本信息（避免访问 "/" 时 404 被误判为服务不可用）
        app.MapGet("/", () => Results.Ok(new
        {
            name = "WMS Core API",
            status = "running",
            health = "/health",
            docs = app.Environment.IsDevelopment() ? "/swagger" : null
        }));

        return app;
    }
}
