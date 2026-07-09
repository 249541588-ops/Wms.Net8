using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Net;
using Wms.Core.WebApi.Configuration;
using Wms.Core.WebApi.Extensions;
using Wms.Core.WebApi.Filters;
using Wms.Core.WebApi.Helpers;
using Wms.Core.WebApi.Security;
using FluentValidation.AspNetCore;
using Wms.Core.WebApi.Services;
using Wms.Core.Infrastructure.DependencyInjection;
using NLog;
using NLog.Extensions.Logging;
using Wms.Core.Infrastructure.Persistence;

// 早期初始化 NLog
var logger = LogManager.Setup().LoadConfigurationFromFile("nlog.config").GetCurrentClassLogger();
logger.Info("WMS API starting up...");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // 基础配置
    builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = 50 * 1024 * 1024; // 50MB
    });
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = 50 * 1024 * 1024; // 50MB
    });

    // Data Protection
    var dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys");
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
        .SetApplicationName("Wms.Core.WebApi");

    // ForwardedHeaders（反向代理场景）
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.KnownProxies.Add(IPAddress.Loopback);
            options.KnownProxies.Add(IPAddress.IPv6Loopback);
        });
    }

    // 日志
    builder.Logging.ClearProviders();
    builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
    builder.Logging.AddFilter("Microsoft.AspNetCore.DataProtection", Microsoft.Extensions.Logging.LogLevel.Warning);
    builder.Logging.AddNLog();

    // Controllers + FluentValidation
    builder.Services.AddControllers(options =>
        {
            options.Filters.AddService<OperationLogFilter>();
        })
        .AddFluentValidation(fv => fv.RegisterValidatorsFromAssemblyContaining<Program>())
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
            options.JsonSerializerOptions.WriteIndented = true;
            options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        });
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddResponseCaching();

    // 选项配置
    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
    builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection(SecurityOptions.SectionName));
    builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection(RateLimitOptions.SectionName));
    builder.Services.AddMemoryCache();

    // 基础设施服务
    builder.Services.AddWmsCoreInfrastructure(builder.Configuration);

    // 各模块服务注册（通过扩展方法）
    builder.Services.AddWmsRedis(builder.Configuration);
    builder.Services.AddWmsServices();
    builder.Services.AddWcsServices(builder.Configuration);
    builder.Services.AddWcsClient(builder.Configuration);
    builder.Services.AddMesClient(builder.Configuration);
    builder.Services.AddHangKeClient(builder.Configuration);
    builder.Services.AddWmsHangfire(builder.Configuration);
    builder.Services.AddWmsAuthentication(builder.Configuration, builder.Environment);
    builder.Services.AddWmsCors(builder.Configuration);
    builder.Services.AddWmsHealthChecks(builder.Configuration);

    // Swagger（仅开发环境）
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "WMS Core API",
                Version = "v1",
                Description = "WMS Core API - Version 1.0",
                Contact = new OpenApiContact { Name = "WMS Team", Email = "wms-team@example.com" }
            });
            options.SwaggerDoc("v2", new OpenApiInfo
            {
                Title = "WMS Core API",
                Version = "v2",
                Description = "WMS Core API - Version 2.0 (Latest)",
                Contact = new OpenApiContact { Name = "WMS Team", Email = "wms-team@example.com" }
            });
            options.DocInclusionPredicate((docName, apiDesc) =>
            {
                var apiVersionAttribute = apiDesc.ActionDescriptor.EndpointMetadata
                    .OfType<ApiVersionAttribute>().FirstOrDefault();
                if (apiVersionAttribute == null) return false;
                var version = apiVersionAttribute.Versions.FirstOrDefault();
                if (version == null) return false;
                return $"v{version.MajorVersion}" == docName;
            });
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath);
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "输入 'Bearer' [空格] 然后输入您的 Token"
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
            options.OperationFilter<ApiVersionSwaggerFilter>();
        });
    }

    // API 版本控制
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

    var app = builder.Build();

    // 初始化数据库
    using (var scope = app.Services.CreateScope())
    {
        var dbInitializer = scope.ServiceProvider.GetService<DbInitializer>();
        if (dbInitializer != null) await dbInitializer.InitializeAsync();
    }

    // 中间件管道
    app.ConfigureMiddleware();

    // Hangfire 仪表盘 + 任务同步
    await app.UseWmsHangfireDashboard(builder.Configuration);

    logger.Info("WMS API started successfully");
    app.Run();
}
catch (Exception ex)
{
    logger.Fatal(ex, "WMS API stopped unexpectedly because of exception");
    throw;
}
finally
{
    LogManager.Shutdown();
}

/// <summary>
/// Make Program class accessible for integration testing (WebApplicationFactory)
/// </summary>
public partial class Program { }
