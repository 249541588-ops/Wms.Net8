using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Wms.Core.Domain.Exceptions;

namespace Wms.Core.WebApi.Middleware;

/// <summary>
/// 全局异常处理中间件
/// </summary>
public class GlobalExceptionHandler : IMiddleware
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IWebHostEnvironment _env;

    /// <summary>
    /// 初始化全局异常处理中间件类的新实例
    /// </summary>
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IWebHostEnvironment env)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _env = env ?? throw new ArgumentNullException(nameof(env));
    }

    /// <summary>
    /// 处理请求并捕获所有异常
    /// </summary>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "未处理的异常: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    /// <summary>
    /// 处理异常并返回统一的错误响应
    /// </summary>
    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = exception switch
        {
            InvalidRequestException => (int)HttpStatusCode.BadRequest,
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            KeyNotFoundException => (int)HttpStatusCode.NotFound,
            InvalidOperationException => (int)HttpStatusCode.BadRequest,
            ArgumentException => (int)HttpStatusCode.BadRequest,
            _ => (int)HttpStatusCode.InternalServerError
        };

        // 开发环境返回原始 Message（便于调试），生产环境返回安全消息
        var isDev = _env.IsDevelopment() || _env.IsEnvironment("Local");
        var response = new ErrorResponse
        {
            Status = context.Response.StatusCode,
            Message = isDev ? exception.Message : GetErrorDetail(exception) ?? "服务器内部错误",
            Detail = GetErrorDetail(exception),
            Path = context.Request.Path,
            Method = context.Request.Method,
            Timestamp = DateTime.UtcNow
        };

        if (isDev)
        {
            response = response with
            {
                StackTrace = exception.StackTrace,
                InnerExceptionMessage = exception.InnerException?.Message
            };
        }

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        return context.Response.WriteAsync(json);
    }

    /// <summary>
    /// 获取错误详细信息
    /// </summary>
    private static string? GetErrorDetail(Exception exception)
    {
        return exception switch
        {
            InvalidRequestException => "请求参数验证失败",
            UnauthorizedAccessException => "未授权访问",
            KeyNotFoundException => "请求的资源不存在",
            InvalidOperationException => "操作无效",
            ArgumentException => "参数错误",
            _ => "服务器内部错误"
        };
    }

}

/// <summary>
/// 错误响应模型
/// </summary>
public record ErrorResponse
{
    /// <summary>
    /// HTTP 状态码
    /// </summary>
    public int Status { get; init; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 错误详情
    /// </summary>
    public string? Detail { get; init; }

    /// <summary>
    /// 请求路径
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// 请求方法
    /// </summary>
    public string Method { get; init; } = string.Empty;

    /// <summary>
    /// 时间戳（UTC）
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// 堆栈跟踪（仅开发环境）
    /// </summary>
    public string? StackTrace { get; init; }

    /// <summary>
    /// 内部异常消息（仅开发环境）
    /// </summary>
    public string? InnerExceptionMessage { get; init; }
}
