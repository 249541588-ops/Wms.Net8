using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
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
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    /// <summary>
    /// 初始化数据库初始化服务类的新实例
    /// </summary>
    public DbInitializer(
        WmsDbContext db,
        WmsLogDbContext logDb,
        IOptions<JwtOptions> jwtOptions,
        ILogger<DbInitializer> logger,
        IWebHostEnvironment env,
        IConfiguration config)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logDb = logDb ?? throw new ArgumentNullException(nameof(logDb));
        _jwtOptions = jwtOptions.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _env = env ?? throw new ArgumentNullException(nameof(env));
        _config = config ?? throw new ArgumentNullException(nameof(config));
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

            // 确保报表模块 3 张表存在
            await EnsureReportTablesAsync();

            // 确保已有数据库的 FlowNodes 表包含 IsDeleted 列
            await EnsureFlowNodeIsDeletedColumnAsync();

            // 确保清理相关索引存在（已有数据库 EnsureCreatedAsync 不会添加新索引）
            await EnsureCleanupIndexesAsync();

            // 种子流程模板（每次启动都同步，确保内置模板节点正确）
            await FlowTemplateSeeder.SeedAsync(_db, _logger);
            _logger.LogInformation("流程模板种子数据同步完成");

            // 种子预置报表配置（幂等：按 ReportCode 判断是否已存在）
            await ReportConfigSeeder.SeedAsync(_db, _logger);
            _logger.LogInformation("报表配置种子数据同步完成");

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

            // 创建默认账号（生产环境需通过环境变量 ADMIN_DEFAULT_PASSWORD / USER_DEFAULT_PASSWORD 配置）
            var isProduction = !_env.IsDevelopment() && !_env.IsEnvironment("Local");
            var adminPwd = _config["ADMIN_DEFAULT_PASSWORD"];
            var userPwd = _config["USER_DEFAULT_PASSWORD"];

            if (isProduction && string.IsNullOrEmpty(adminPwd))
            {
                _logger.LogWarning("生产环境未配置 ADMIN_DEFAULT_PASSWORD，跳过默认账号创建");
                await _db.SaveChangesAsync();
                return;
            }

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
            admin.SetPassword(adminPwd ?? "admin123");

            _db.Add(admin);

            // 非生产环境或配置了 USER_DEFAULT_PASSWORD 时创建普通用户
            if (!isProduction || !string.IsNullOrEmpty(userPwd))
            {
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
                user.SetPassword(userPwd ?? "user123");
                _db.Add(user);
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("数据库初始化成功: 默认账号已创建");
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
                    IsDeleted BIT NOT NULL DEFAULT 0,
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

    /// <summary>
    /// 确保数据清理相关索引存在（已有数据库不会自动添加新索引）
    /// </summary>
    private async Task EnsureCleanupIndexesAsync()
    {
        var sql = """
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ArchivedTasks_ArchivedAt' AND object_id = OBJECT_ID('ArchivedTasks'))
                CREATE NONCLUSTERED INDEX IX_ArchivedTasks_ArchivedAt ON ArchivedTasks(ArchivedAt);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ArchivedUnitloads_ArchivedAt' AND object_id = OBJECT_ID('ArchivedUnitloads'))
                CREATE NONCLUSTERED INDEX IX_ArchivedUnitloads_ArchivedAt ON ArchivedUnitloads(ArchivedAt);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ArchivedUnitloadItems_UnitloadId' AND object_id = OBJECT_ID('ArchivedUnitloadItems'))
                CREATE NONCLUSTERED INDEX IX_ArchivedUnitloadItems_UnitloadId ON ArchivedUnitloadItems(UnitloadId);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ArchivedUnitloadItemDetails_UnitloadItemId' AND object_id = OBJECT_ID('ArchivedUnitloadItemDetails'))
                CREATE NONCLUSTERED INDEX IX_ArchivedUnitloadItemDetails_UnitloadItemId ON ArchivedUnitloadItemDetails(UnitloadItemId);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SystemLogs_OperationTime' AND object_id = OBJECT_ID('SystemLogs'))
                CREATE NONCLUSTERED INDEX IX_SystemLogs_OperationTime ON SystemLogs(OperationTime);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_UnitloadOps_CreatedTime' AND object_id = OBJECT_ID('UnitloadOps'))
                CREATE NONCLUSTERED INDEX IX_UnitloadOps_CreatedTime ON UnitloadOps(CreatedTime);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LocationOps_CreatedTime' AND object_id = OBJECT_ID('LocationOps'))
                CREATE NONCLUSTERED INDEX IX_LocationOps_CreatedTime ON LocationOps(CreatedTime);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_BatteryCells_CreatedTime' AND object_id = OBJECT_ID('BatteryCells'))
                CREATE NONCLUSTERED INDEX IX_BatteryCells_CreatedTime ON BatteryCells(CreatedTime);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_UploadMesInfo_ctime' AND object_id = OBJECT_ID('UploadMesInfo'))
                CREATE NONCLUSTERED INDEX IX_UploadMesInfo_ctime ON UploadMesInfo(ctime);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Flows_CreatedTime' AND object_id = OBJECT_ID('Flows'))
                CREATE NONCLUSTERED INDEX IX_Flows_CreatedTime ON Flows(CreatedTime);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_BatteryOps_CreateAt' AND object_id = OBJECT_ID('BatteryOps'))
                CREATE NONCLUSTERED INDEX IX_BatteryOps_CreateAt ON BatteryOps(CreateAt);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FlowNodeLogs_CreatedTime' AND object_id = OBJECT_ID('FlowNodeLogs'))
                CREATE NONCLUSTERED INDEX IX_FlowNodeLogs_CreatedTime ON FlowNodeLogs(CreatedTime);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FlowInstances_CreatedTime' AND object_id = OBJECT_ID('FlowInstances'))
                CREATE NONCLUSTERED INDEX IX_FlowInstances_CreatedTime ON FlowInstances(CreatedTime);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RefreshTokens_ExpiryTime' AND object_id = OBJECT_ID('RefreshTokens'))
                CREATE NONCLUSTERED INDEX IX_RefreshTokens_ExpiryTime ON RefreshTokens(ExpiryTime);
            """;

        await _db.Database.ExecuteSqlRawAsync(sql);
        _logger.LogInformation("清理索引检查/创建完成");
    }

    /// <summary>
    /// 确保已有数据库的 FlowNodes 表包含 IsDeleted 列（兼容旧数据库升级）
    /// </summary>
    private async Task EnsureFlowNodeIsDeletedColumnAsync()
    {
        var sql = """
            IF NOT EXISTS (
                SELECT * FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'FlowNodes' AND COLUMN_NAME = 'IsDeleted'
            )
            BEGIN
                ALTER TABLE FlowNodes ADD IsDeleted BIT NOT NULL DEFAULT 0;
            END
            """;

        await _db.Database.ExecuteSqlRawAsync(sql);
    }

    /// <summary>
    /// 确保报表模块 3 张表存在（已有数据库 EnsureCreatedAsync 不会添加新表）
    /// </summary>
    private async Task EnsureReportTablesAsync()
    {
        var sql = """
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ReportConfigs')
            BEGIN
                CREATE TABLE ReportConfigs (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    ReportCode NVARCHAR(50) NOT NULL,
                    ReportName NVARCHAR(100) NOT NULL,
                    Category NVARCHAR(50) NULL,
                    Description NVARCHAR(500) NULL,
                    ReportType NVARCHAR(20) NULL,
                    DefaultColumns NVARCHAR(MAX) NULL,
                    AvailableColumns NVARCHAR(MAX) NULL,
                    AvailableFilters NVARCHAR(MAX) NULL,
                    DefaultSort NVARCHAR(200) NULL,
                    SqlTemplate NVARCHAR(MAX) NULL,
                    CountSqlTemplate NVARCHAR(MAX) NULL,
                    FilterSqlMapping NVARCHAR(MAX) NULL,
                    IsEnabled BIT NOT NULL DEFAULT 1,
                    CreatedTime DATETIME2 NULL,
                    ModifiedTime DATETIME2 NULL,
                    CreatedBy NVARCHAR(255) NULL,
                    ModifiedBy NVARCHAR(255) NULL,
                    CONSTRAINT UQ_ReportConfigs_ReportCode UNIQUE (ReportCode)
                );
            END

            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'UserReportConfigs')
            BEGIN
                CREATE TABLE UserReportConfigs (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    UserId INT NOT NULL,
                    UserName NVARCHAR(100) NULL,
                    ReportCode NVARCHAR(50) NULL,
                    ConfigName NVARCHAR(100) NOT NULL,
                    SelectedColumns NVARCHAR(MAX) NULL,
                    ColumnOrder NVARCHAR(MAX) NULL,
                    ColumnWidths NVARCHAR(MAX) NULL,
                    FixedFilters NVARCHAR(MAX) NULL,
                    SortConfig NVARCHAR(MAX) NULL,
                    IsDefault BIT NOT NULL DEFAULT 0,
                    CreatedTime DATETIME2 NULL,
                    ModifiedTime DATETIME2 NULL
                );
                CREATE NONCLUSTERED INDEX IX_UserReportConfigs_UserId_ReportCode ON UserReportConfigs(UserId, ReportCode);
            END

            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ReportExportTasks')
            BEGIN
                CREATE TABLE ReportExportTasks (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    TaskId NVARCHAR(100) NOT NULL,
                    ReportCode NVARCHAR(50) NULL,
                    UserId INT NOT NULL,
                    UserName NVARCHAR(100) NULL,
                    Status NVARCHAR(20) NOT NULL DEFAULT 'Pending',
                    FilterParams NVARCHAR(MAX) NULL,
                    ColumnConfig NVARCHAR(MAX) NULL,
                    FileName NVARCHAR(200) NULL,
                    FilePath NVARCHAR(500) NULL,
                    FileSize BIGINT NOT NULL DEFAULT 0,
                    TotalRows INT NOT NULL DEFAULT 0,
                    ErrorMessage NVARCHAR(MAX) NULL,
                    StartedAt DATETIME2 NULL,
                    CompletedAt DATETIME2 NULL,
                    CreatedTime DATETIME2 NULL,
                    CONSTRAINT UQ_ReportExportTasks_TaskId UNIQUE (TaskId)
                );
                CREATE NONCLUSTERED INDEX IX_ReportExportTasks_UserId_CreatedTime ON ReportExportTasks(UserId, CreatedTime DESC);
            END
            """;

        await _db.Database.ExecuteSqlRawAsync(sql);
        _logger.LogInformation("报表表检查/创建完成");
    }
}
