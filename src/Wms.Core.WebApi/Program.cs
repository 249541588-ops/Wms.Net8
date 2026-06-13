using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using System.Text;
using Wms.Core.WebApi.Configuration;
using Wms.Core.WebApi.Filters;
using Wms.Core.WebApi.HealthChecks;
using Wms.Core.WebApi.Helpers;
using Wms.Core.WebApi.Middleware;
using Wms.Core.WebApi.Services;
using Wms.Core.Infrastructure.DependencyInjection;
using Wms.Core.Infrastructure.Services;
using NLog;
using NLog.Extensions.Logging;
using Wms.Core.Domain.Services;
using Wms.Core.Domain.Interfaces;
using Hangfire;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.WebApi.Hubs;
using Wms.Core.WebApi.Services.Wcs;
using Wms.Core.WebApi.Jobs;
using WcsReqHandler = Wms.Core.Application.Handlers.WcsRequest.IWcsRequestHandler;
using WcsReqHandlers = Wms.Core.Infrastructure.Handlers.WcsRequest;
using TskCompHandlers = Wms.Core.Infrastructure.Handlers.TaskCompletion;
using StackExchange.Redis;
using Wms.Core.Domain.Tasks;
using Wms.Core.Infrastructure.Tasks.Rules;
using Wms.Core.Infrastructure.Caching;
using Wms.Core.Infrastructure.Clients;
using Polly;

// 早期初始化 NLog
var logger = LogManager.Setup().LoadConfigurationFromFile("nlog.config").GetCurrentClassLogger();
logger.Info("MES API starting up...");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // 配置文件上传大小限制
    builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = 50 * 1024 * 1024; // 50MB
    });
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = 50 * 1024 * 1024; // 50MB
    });

    // 配置 Data Protection - 避免 DPAPI 加密问题
    // 将密钥持久化到文件系统，而不是使用 Windows DPAPI
    var dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys");
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
        //.ProtectKeysWithDpapi()  // Windows 生产环境
        .SetApplicationName("Wms.Core.WebApi");

    // 配置 NLog 日志
    builder.Logging.ClearProviders();
    builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
    // 过滤 Data Protection 的警告日志（开发环境密钥未加密警告）
    builder.Logging.AddFilter("Microsoft.AspNetCore.DataProtection", Microsoft.Extensions.Logging.LogLevel.Warning);
    builder.Logging.AddNLog();

// 添加服务
builder.Services.AddControllers(options =>
    {
        // 注册全局操作日志过滤器
        options.Filters.AddService<OperationLogFilter>();
    })
    .AddJsonOptions(options =>
    {
        // 处理循环引用
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        // 写入缩进（开发环境美化输出）
        options.JsonSerializerOptions.WriteIndented = true;
        // 使用 camelCase 命名策略，前后端统一
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        // 反序列化时不区分大小写，兼容性更好
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

builder.Services.AddHttpContextAccessor();

// 添加响应缓存服务
builder.Services.AddResponseCaching();

// 配置 JWT 选项
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));

// 配置安全选项
builder.Services.Configure<SecurityOptions>(
    builder.Configuration.GetSection(SecurityOptions.SectionName));

// 配置速率限制选项
builder.Services.Configure<RateLimitOptions>(
    builder.Configuration.GetSection(RateLimitOptions.SectionName));

// 添加内存缓存（用于速率限制）
builder.Services.AddMemoryCache();

// 配置 Redis 分布式缓存
var redisSection = builder.Configuration.GetSection(RedisOptions.SectionName);
var redisEnabled = redisSection.GetValue<bool>("Enabled");
var redisConnectionString = redisSection["ConnectionString"];

if (redisEnabled && !string.IsNullOrEmpty(redisConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = redisSection.GetValue<string>("InstanceName") ?? "Wms:";
    });

    Console.WriteLine("Redis distributed cache enabled.");

    // 注册 IConnectionMultiplexer（单例）供 InventoryCacheService 和 DistributedLockService 使用
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
        ConnectionMultiplexer.Connect(redisConnectionString));

    // 注册库存缓存和分布式锁服务
    builder.Services.AddScoped<IInventoryCacheService, InventoryCacheService>();
    builder.Services.AddScoped<IDistributedLockService, DistributedLockService>();
}
else
{
    // 如果 Redis 未配置，使用内存分布式缓存作为后备
    builder.Services.AddDistributedMemoryCache();
    Console.WriteLine("Using in-memory distributed cache (Redis not configured).");
}

// 配置 Redis 选项
builder.Services.Configure<RedisOptions>(
    builder.Configuration.GetSection(RedisOptions.SectionName));

// 注册缓存服务（单机部署用 MemoryCacheService，多实例部署切换为 DistributedCacheService）
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
// builder.Services.AddScoped<ICacheService, DistributedCacheService>();

// 注册 Token 服务
builder.Services.AddScoped<ITokenService, TokenService>();

// 注册 Excel 服务
//builder.Services.AddScoped<IExcelService, ExcelService>();

// 注册导出服务
builder.Services.AddScoped<IExportService, ExportService>();

// 注册 Dapper 高频读取服务（条码扫描、库存查询等场景）
builder.Services.AddScoped<IDapperReadService, DapperReadService>();

// 注册 SignalR 实时通信
builder.Services.AddSignalR();

// 注册 Hangfire 任务调度（使用 SQL Server 存储）
var hangfireConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddHangfire(x => x
    .UseSqlServerStorage(hangfireConnectionString));
builder.Services.AddHangfireServer();

// 注册定时任务调度器
builder.Services.AddSingleton<IJobDispatcher, JobDispatcher>();
builder.Services.AddScoped<BackgroundJobService>();
builder.Services.AddHttpClient(); // 用于 HTTP API 模式调用

// 注册 WCS 通信服务
// ctask 数据库访问（Dapper，独立连接）
builder.Services.AddScoped<ICtaskDbService, CtaskDbService>();
// 通信适配器（Database / Http 模式）
var wcsMode = builder.Configuration.GetValue<string>("Wcs:Mode") ?? "Database";
if (wcsMode.Equals("Http", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<IWcsTaskBridge, HttpWcsTaskBridge>();
}
else
{
    builder.Services.AddScoped<IWcsTaskBridge, DatabaseWcsTaskBridge>();
}

// WCS 任务同步服务
builder.Services.AddScoped<WcsTaskSyncService>();
// 日志自动清理服务
builder.Services.AddHostedService<LogCleanupService>();
// WCS 请求处理器（策略模式）
builder.Services.AddScoped<WcsReqHandlers.LocationAllocator>();
builder.Services.AddScoped<WcsReqHandler, WcsReqHandlers.InboundRequestHandler>();
builder.Services.AddScoped<WcsReqHandler, WcsReqHandlers.OutboundRequestHandler>();
builder.Services.AddScoped<WcsReqHandler, WcsReqHandlers.MoveRequestHandler>();
// 任务完成处理器（策略模式）
builder.Services.AddScoped<ITaskCompletionHandler, TskCompHandlers.InboundCompletionHandler>();
builder.Services.AddScoped<ITaskCompletionHandler, TskCompHandlers.OutboundCompletionHandler>();
builder.Services.AddScoped<ITaskCompletionHandler, TskCompHandlers.MoveCompletionHandler>();

// 注册库位分配规则（15 条规则 + 策略引擎）
builder.Services.AddSingleton<ILocationAllocationRule, SSRule01>();
builder.Services.AddSingleton<ILocationAllocationRule, SSRule02>();
builder.Services.AddSingleton<ILocationAllocationRule, SSRule03>();
builder.Services.AddSingleton<ILocationAllocationRule, SSRule04>();
builder.Services.AddSingleton<ILocationAllocationRule, SSRule04HcLx>();
builder.Services.AddSingleton<ILocationAllocationRule, SSRule05>();
builder.Services.AddSingleton<ILocationAllocationRule, SSRule06>();
builder.Services.AddSingleton<ILocationAllocationRule, SSRule07>();
builder.Services.AddSingleton<ILocationAllocationRule, SSRule08>();
builder.Services.AddSingleton<ILocationAllocationRule, SSRule09>();
builder.Services.AddSingleton<ILocationAllocationRule, SSRule10>();
builder.Services.AddSingleton<ILocationAllocationRule, SDRule01>();
builder.Services.AddSingleton<ILocationAllocationRule, SDRule02>();
builder.Services.AddSingleton<ILocationAllocationRule, SDRule03>();
builder.Services.AddSingleton<ILocationAllocationRule, SDRule04>();
builder.Services.AddScoped<LocationAllocationEngine>();

// 注册数据库初始化服务（Scoped，因为依赖 WmsDbContext）
builder.Services.AddScoped<DbInitializer>();

// 注册语言包缓存键跟踪器（单例，全局共享）
builder.Services.AddSingleton<ILanguagePackCacheTracker, LanguagePackCacheTracker>();

// 注册翻译服务
builder.Services.AddScoped<ITranslationService, TranslationService>();

// 注册全局异常处理中间件
builder.Services.AddScoped<GlobalExceptionHandler>();

// 注册全局操作日志过滤器
builder.Services.AddScoped<OperationLogFilter>();

// 配置 JWT 认证
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
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
if (builder.Environment.IsProduction() &&
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
var securitySection = builder.Configuration.GetSection(SecurityOptions.SectionName);
var requireHttps = securitySection.GetValue<bool>("RequireHttps");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // 根据配置决定是否要求 HTTPS
    options.RequireHttpsMetadata = requireHttps && !builder.Environment.IsDevelopment();
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
});

builder.Services.AddAuthorization();

// 配置 API 版本控制
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

builder.Services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// 配置 Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "WMS Core API",
        Version = "v1",
        Description = "WMS Core API - Version 1.0",
        Contact = new OpenApiContact
        {
            Name = "MES Team",
            Email = "wms-team@example.com"
        }
    });

    options.SwaggerDoc("v2", new OpenApiInfo
    {
        Title = "WMS Core API",
        Version = "v2",
        Description = "WMS Core API - Version 2.0 (Latest)",
        Contact = new OpenApiContact
        {
            Name = "MES Team",
            Email = "wms-team@example.com"
        }
    });

    // 为 API 版本添加文档描述
    options.DocInclusionPredicate((docName, apiDesc) =>
    {
        // 获取 API 版本属性
        var apiVersionAttribute = apiDesc.ActionDescriptor.EndpointMetadata
            .OfType<ApiVersionAttribute>()
            .FirstOrDefault();

        if (apiVersionAttribute == null) return false;

        var version = apiVersionAttribute.Versions.FirstOrDefault();
        if (version == null) return false;

        var versionString = $"v{version.MajorVersion}";
        return versionString == docName;
    });

    // 包含 XML 注释
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // 添加 JWT 认证到 Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "输入 'Bearer' [空格] 然后输入您的 Token\r\n\rnbsp;示例: \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9\""
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // 操作过滤器以支持 API 版本
    options.OperationFilter<ApiVersionSwaggerFilter>();

    // 添加示例和文档过滤器
    //options.OperationFilter<SwaggerExamplesFilter>();
    //options.DocumentFilter<SwaggerDocumentFilter>();
});

// 配置 CORS
var allowedOrigins = securitySection.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowConfiguredOrigins", policy =>
    {
        // 开发环境允许所有来源，生产环境使用配置的列表
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            if (allowedOrigins.Length == 0)
            {
                throw new InvalidOperationException(
                    "Security:AllowedOrigins is not configured. " +
                    "Please set allowed origins in appsettings.Production.json or use environment variable 'Security__AllowedOrigins'.");
            }

            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
    });
});

    // 添加 Wms Core 基础设施服务
    builder.Services.AddWmsCoreInfrastructure(
        builder.Configuration);

    // 配置健康检查选项
    builder.Services.Configure<HealthCheckOptions>(
    builder.Configuration.GetSection(HealthCheckOptions.SectionName));

// 添加 HttpClient 用于 WCS 健康检查
builder.Services.AddHttpClient<WcsHealthCheck>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});

// 配置 WCS 客户端选项
builder.Services.Configure<WcsClientOptions>(
    builder.Configuration.GetSection(WcsClientOptions.SectionName));

// 添加 WCS 通信客户端（Polly 重试 + 熔断）
builder.Services.AddHttpClient<IWcsClient, DefaultWcsClient>(client =>
{
    var wcsEndpoint = builder.Configuration["Wcs:Endpoint"] ?? string.Empty;
    if (!string.IsNullOrEmpty(wcsEndpoint))
    {
        client.BaseAddress = new Uri(wcsEndpoint.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(
            builder.Configuration.GetValue("Wcs:TimeoutSeconds", 10));
    }
})
.AddPolicyHandler(Polly.Extensions.Http.HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(
        retryCount: builder.Configuration.GetValue("Wcs:RetryCount", 3),
        sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
        onRetry: (outcome, timespan, retryCount, context) =>
        {
            // 日志在 DefaultWcsClient 中处理
        }))
.AddPolicyHandler(Polly.Extensions.Http.HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(
        handledEventsAllowedBeforeBreaking: builder.Configuration.GetValue("Wcs:CircuitBreakerFailureThreshold", 5),
        durationOfBreak: TimeSpan.FromSeconds(builder.Configuration.GetValue("Wcs:CircuitBreakerDurationSeconds", 30))));

// 添加健康检查
var healthChecksBuilder = builder.Services.AddHealthChecks()
    .AddSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "sql-server",
        failureStatus: HealthStatus.Degraded)
    .AddCheck<WmsHealthCheck>("wms-system")
    .AddCheck<DiskSpaceHealthCheck>("disk-space", failureStatus: HealthStatus.Degraded)
    .AddCheck<WcsHealthCheck>("wcs", failureStatus: HealthStatus.Degraded)
    .AddCheck("self", () => HealthCheckResult.Healthy("API is running"));

// 只有在启用 Redis 时才添加 Redis 健康检查
if (redisEnabled && !string.IsNullOrEmpty(redisConnectionString))
{
    healthChecksBuilder.AddCheck<RedisHealthCheck>("redis", failureStatus: HealthStatus.Degraded);
}

// 添加健康检查 UI（简化版 - 不使用数据库）
// 移除了 AddHealthChecksUI，改用手动配置的端点
// 可以通过 /health 查看 JSON 格式的健康检查结果

var app = builder.Build();

// 初始化数据库（如果需要）
using (var scope = app.Services.CreateScope())
{
    var dbInitializer = scope.ServiceProvider.GetService<DbInitializer>();
    if (dbInitializer != null)
    {
        await dbInitializer.InitializeAsync();
    }
}

// 配置 HTTP 请求管道
// 全局异常处理必须在最前面
app.UseMiddleware<GlobalExceptionHandler>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "WMS Core API v1");
        options.SwaggerEndpoint("/swagger/v2/swagger.json", "WMS Core API v2");
        options.RoutePrefix = string.Empty; // 设置 Swagger 为根路径
        options.DefaultModelsExpandDepth(-1); // 隐藏模型
        options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None); // 默认折叠所有操作
    });
}

app.UseHttpsRedirection();

// 静态文件服务（上传文件访问）
var uploadBasePath = builder.Configuration["Upload:BasePath"];
if (string.IsNullOrWhiteSpace(uploadBasePath)) uploadBasePath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
if (!Directory.Exists(uploadBasePath)) Directory.CreateDirectory(uploadBasePath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadBasePath),
    RequestPath = "/uploads"
});

app.UseCors("AllowConfiguredOrigins");

// 添加响应缓存中间件（必须在认证和授权之前）
app.UseResponseCaching();

// 添加语言包中间件（全局语言包访问）
app.UseLanguagePack();

// 认证必须在授权之前
app.UseAuthentication();
app.UseAuthorization();

// 添加速率限制中间件
app.UseMiddleware<RateLimitMiddleware>();

app.MapControllers();

// 配置 SignalR Hub 端点
app.MapHub<WmsHub>("/hubs/wms");

// 配置 Hangfire 仪表盘
app.UseHangfireDashboard("/hangfire");

// 清理旧版 Hangfire 定时任务（WcsTaskSyncJob 已删除）
RecurringJob.RemoveIfExists("wcs-task-sync");

// 启动时同步 DB 中的定时任务到 Hangfire
using (var syncScope = app.Services.CreateScope())
{
    var jobService = syncScope.ServiceProvider.GetRequiredService<BackgroundJobService>();
    await jobService.SyncAllAsync();
}


// 配置健康检查端点
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
                exception = e.Value.Exception?.Message
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

logger.Info("MES API started successfully");

app.Run();

}
catch (Exception ex)
{
    // NLog: catch setup errors
    logger.Fatal(ex, "MES API stopped unexpectedly because of exception");
    throw;
}
finally
{
    // Ensure to flush and stop internal timers/threads before application-exit
    LogManager.Shutdown();
}

/// <summary>
/// Make Program class accessible for integration testing (WebApplicationFactory)
/// </summary>
public partial class Program { }
