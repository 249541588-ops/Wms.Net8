using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wms.Core.Domain.Entities.Identity;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.WebApi.Configuration;

namespace Wms.Core.WebApi.Services;

/// <summary>
/// 数据库初始化服务
/// </summary>
public class DbInitializer
{
    private readonly WmsDbContext _db;
    private readonly WmsLogDbContext _logDb;
    private readonly ILogger<DbInitializer> _logger;
    private readonly JwtOptions _jwtOptions;

    /// <summary>
    /// 初始化数据库初始化服务类的新实例
    /// </summary>
    public DbInitializer(
        WmsDbContext db,
        WmsLogDbContext logDb,
        IOptions<JwtOptions> jwtOptions,
        ILogger<DbInitializer> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logDb = logDb ?? throw new ArgumentNullException(nameof(logDb));
        _jwtOptions = jwtOptions.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 初始化数据库数据
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("开始初始化数据库数据...");

            // 确保数据库已创建（如果不存在则自动创建）
            await _db.Database.EnsureCreatedAsync();

            // 确保独立日志数据库已创建（如果不存在则自动创建）
            await _logDb.Database.EnsureCreatedAsync();

            // 确保流程引擎表存在（已有数据库不会自动添加新表）
            await EnsureFlowTablesAsync();

            // 种子流程模板（每次启动都同步，确保内置模板节点正确）
            await FlowTemplateSeeder.SeedAsync(_db, _logger);
            _logger.LogInformation("流程模板种子数据同步完成");

            // 检查是否已有用户
            var existingUserCount = await _db.Set<User>().CountAsync();
            if (existingUserCount > 0)
            {
                _logger.LogInformation("数据库已有用户数据，跳过初始化");
                return;
            }

            // 创建角色
            var adminRole = new Role
            {
                RoleName = "Admin",
                Description = "系统管理员",
                IsBuiltIn = true,
                CreatedTime = DateTime.UtcNow,
                ModifiedTime = DateTime.UtcNow,
                CreatedBy = "System"
            };

            var userRole = new Role
            {
                RoleName = "User",
                Description = "普通用户",
                IsBuiltIn = true,
                CreatedTime = DateTime.UtcNow,
                ModifiedTime = DateTime.UtcNow,
                CreatedBy = "System"
            };

            _db.Add(adminRole);
            _db.Add(userRole);

            // 创建默认管理员账号
            var admin = new User
            {
                UserName = "admin",
                RealName = "系统管理员",
                Email = "admin@wms.com",
                IsActive = true,
                IsBuiltIn = true,
                CreatedTime = DateTime.UtcNow,
                ModifiedTime = DateTime.UtcNow,
                CreatedBy = "System"
            };
            admin.AddRole(adminRole);
            admin.SetPassword("admin123");

            _db.Add(admin);

            // 创建普通用户
            var user = new User
            {
                UserName = "user",
                RealName = "普通用户",
                Email = "user@wms.com",
                IsActive = true,
                CreatedTime = DateTime.UtcNow,
                ModifiedTime = DateTime.UtcNow,
                CreatedBy = "System"
            };
            user.AddRole(userRole);
            user.SetPassword("user123");

            _db.Add(user);

            await _db.SaveChangesAsync();
            _logger.LogInformation("数据库初始化成功: 创建了 2 个默认用户");

            _logger.LogInformation("默认登录信息:");
            _logger.LogInformation("管理员 - 用户名: admin, 密码: admin123");
            _logger.LogInformation("普通用户 - 用户名: user, 密码: user123");
            _logger.LogWarning("生产环境请立即修改默认密码!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "数据库初始化失败: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// 确保流程引擎 4 张表存在（已有数据库 EnsureCreatedAsync 不会添加新表）
    /// </summary>
    private async Task EnsureFlowTablesAsync()
    {
        var sql = """
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'FlowTemplates')
            BEGIN
                CREATE TABLE FlowTemplates (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Name NVARCHAR(100) NOT NULL,
                    Code NVARCHAR(100) NOT NULL,
                    Category NVARCHAR(50) NOT NULL,
                    Phase NVARCHAR(20) NOT NULL,
                    Description NVARCHAR(500) NULL,
                    IsActive BIT NOT NULL DEFAULT 1,
                    IsBuiltIn BIT NOT NULL DEFAULT 0,
                    SortOrder INT NOT NULL DEFAULT 0,
                    Priority INT NOT NULL DEFAULT 0,
                    MatchRules NVARCHAR(2000) NULL,
                    CreatedTime DATETIME2 NULL,
                    ModifiedTime DATETIME2 NULL,
                    CreatedBy NVARCHAR(64) NULL,
                    ModifiedBy NVARCHAR(64) NULL
                );
            END

            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'FlowNodes')
            BEGIN
                CREATE TABLE FlowNodes (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    TemplateId INT NOT NULL,
                    NodeType NVARCHAR(100) NOT NULL,
                    NodeName NVARCHAR(100) NOT NULL,
                    StepOrder INT NOT NULL DEFAULT 0,
                    ConfigJson NVARCHAR(2000) NULL,
                    IsEnabled BIT NOT NULL DEFAULT 1,
                    OnFailure NVARCHAR(20) NULL,
                    SkipCondition NVARCHAR(500) NULL,
                    IsTransactionBoundary BIT NOT NULL DEFAULT 0,
                    CONSTRAINT FK_FlowNodes_Templates FOREIGN KEY (TemplateId) REFERENCES FlowTemplates(Id)
                );
            END

            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'FlowInstances')
            BEGIN
                CREATE TABLE FlowInstances (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    InstanceCode NVARCHAR(50) NOT NULL,
                    TemplateId INT NOT NULL,
                    BusinessType NVARCHAR(50) NOT NULL,
                    BusinessId NVARCHAR(100) NULL,
                    Status NVARCHAR(20) NOT NULL DEFAULT 'Running',
                    CurrentNodeOrder INT NOT NULL DEFAULT 0,
                    ContextJson NVARCHAR(4000) NULL,
                    ErrorMsg NVARCHAR(2000) NULL,
                    CompletedTime DATETIME2 NULL,
                    CreatedTime DATETIME2 NULL,
                    ModifiedTime DATETIME2 NULL,
                    CreatedBy NVARCHAR(64) NULL,
                    ModifiedBy NVARCHAR(64) NULL
                );
            END

            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'FlowNodeLogs')
            BEGIN
                CREATE TABLE FlowNodeLogs (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    InstanceId INT NOT NULL,
                    NodeOrder INT NOT NULL,
                    NodeType NVARCHAR(100) NOT NULL,
                    NodeName NVARCHAR(100) NOT NULL,
                    Status NVARCHAR(20) NOT NULL,
                    DurationMs BIGINT NOT NULL DEFAULT 0,
                    InputJson NVARCHAR(4000) NULL,
                    OutputJson NVARCHAR(4000) NULL,
                    ErrorMsg NVARCHAR(2000) NULL,
                    CreatedTime DATETIME2 NOT NULL,
                    CONSTRAINT FK_FlowNodeLogs_Instances FOREIGN KEY (InstanceId) REFERENCES FlowInstances(Id)
                );
            END
            """;

        await _db.Database.ExecuteSqlRawAsync(sql);
        _logger.LogInformation("流程引擎表检查/创建完成");
    }
}
