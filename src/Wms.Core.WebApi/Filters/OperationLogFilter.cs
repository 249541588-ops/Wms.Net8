using Wms.Core.Domain.Entities.System;
using Wms.Core.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Wms.Core.WebApi.Filters;

/// <summary>
/// 全局操作日志过滤器，自动记录 POST/PUT/DELETE/PATCH 请求到 SystemLogs 表
/// </summary>
public class OperationLogFilter : IAsyncActionFilter
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OperationLogFilter> _logger;

    public OperationLogFilter(IServiceScopeFactory scopeFactory, ILogger<OperationLogFilter> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpMethod = context.HttpContext.Request.Method;
        var isWriteMethod = httpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase)
                         || httpMethod.Equals("PUT", StringComparison.OrdinalIgnoreCase)
                         || httpMethod.Equals("DELETE", StringComparison.OrdinalIgnoreCase)
                         || httpMethod.Equals("PATCH", StringComparison.OrdinalIgnoreCase);

        if (!isWriteMethod)
        {
            await next();
            return;
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var userId = context.HttpContext.User?.FindFirst("userId")?.Value;
        var userName = context.HttpContext.User?.FindFirst("username")?.Value;

        var module = context.ActionDescriptor.RouteValues["controller"];
        var action = context.ActionDescriptor.RouteValues["action"];

        // 提前读取请求参数（ActionArguments），因为 Action 执行后可能改变
        string? requestParams = null;
        try
        {
            if (context.ActionArguments.Count > 0)
            {
                requestParams = System.Text.Json.JsonSerializer.Serialize(context.ActionArguments, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                });
                if (requestParams.Length > 4000)
                    requestParams = requestParams[..4000];
            }
        }
        catch
        {
            requestParams = null;
        }

        var resultContext = await next();
        stopwatch.Stop();

        // 在 HttpContext 仍有效时提取数据
        var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = context.HttpContext.Request.Headers.UserAgent.ToString();
        if (userAgent?.Length > 500)
            userAgent = userAgent[..500];

        var log = new SystemLog
        {
            OperationTime = DateTime.UtcNow,
            HttpMethod = httpMethod,
            Module = module,
            Action = action,
            Url = context.HttpContext.Request.Path,
            UserName = userName,
            UserId = userId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            RequestBody = SanitizeRequestBody(requestParams, module) ?? string.Empty,
        };

        // 获取状态码
        if (resultContext.Result is ObjectResult objResult)
        {
            log.StatusCode = objResult.StatusCode ?? 200;
        }
        else if (resultContext.Result is StatusCodeResult statusResult)
        {
            log.StatusCode = statusResult.StatusCode;
        }
        else
        {
            log.StatusCode = context.HttpContext.Response.StatusCode;
        }

        log.DurationMs = stopwatch.ElapsedMilliseconds;
        log.Success = log.StatusCode >= 200 && log.StatusCode < 300;

        // 异步写入数据库，不阻塞响应
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
                db.SystemLogs.Add(log);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "写入操作日志失败: {Message}", ex.Message);
            }
        });
    }

    /// <summary>
    /// 脱敏请求体：Auth 模块请求中的 password 字段替换为 ***
    /// </summary>
    private static string? SanitizeRequestBody(string? json, string? module)
    {
        if (json == null) return null;
        if (module == "Auth" && json.Contains("password", StringComparison.OrdinalIgnoreCase))
            return Regex.Replace(json, @"""password""\s*:\s*""[^""]*""", @"""password"":""***""", RegexOptions.IgnoreCase);
        return json;
    }
}
