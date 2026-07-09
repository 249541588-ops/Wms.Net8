# Wms.Core 项目综合改造计划

## Context

Wms.Core 是一个基于 .NET 8 的 WMS（仓储管理系统），从 MES 系统迁移而来，目前处于半收敛状态。经过多轮分析（架构结构、技术栈、安全审计），发现以下核心问题：
- 安全漏洞（匿名代码生成接口、Hangfire Dashboard 无鉴权、默认密码明文写日志、HMACSHA256 密码哈希）
- 架构层次违规（Domain 依赖 EF Core、WebApi 直接引用 Domain/Infrastructure、Application 层空心化）
- 代码质量问题（14 处 Task.Run fire-and-forget、.Wait() 同步阻塞、返回异常信息给前端）
- 工程卫生问题（Docker 配置引用旧项目名 Mes、628 warnings、重复 PackageReference）
- 多实例部署缺陷（5 个子系统在多实例下失效）

本计划按风险从高到低、改动面从小到大排列，共分 6 个阶段。每个阶段结束后应保证项目可编译、可运行。

---

## Phase S0：安全漏洞修复（最高优先级）

> 目标：封住可被直接利用的安全口

### S0-1 移除 CodeGenerationController
- **文件**：`src/Wms.Core.WebApi/Controllers/CodeGenerationController.cs`
- **操作**：删除整个文件。该控制器 `[AllowAnonymous]` 可读取数据库 schema + 写入源码目录，属于 RCE 级风险
- **同时删除**：`src/Wms.Core.Infrastructure/Persistence/CodeGeneration/EntityGenerator.cs`（仅被此 Controller 使用）
- **验证**：`dotnet build --no-restore` 通过

### S0-2 Hangfire Dashboard 加鉴权
- **文件**：
  - 新建 `src/Wms.Core.WebApi/Security/HangfireLocalOnlyAuthorizationFilter.cs`
  - 修改 `src/Wms.Core.WebApi/Program.cs`（第 553 行 `UseHangfireDashboard` 调用处）
- **方案**：创建 `IDashboardAuthorizationFilter` 实现。仅允许本地回环访问，不支持外部配置（WMS 生产环境不应暴露 Dashboard）
- **注意**：如果部署在 Nginx/HAProxy 反向代理后面，`LocalIpAddress` 会拿到代理 IP（如 172.x.x.x）而非 loopback。**需通过配置 `ForwardedHeaders` 中间件**（`app.UseForwardedHeaders()`）让 ASP.NET Core 正确识别原始 IP。如果暂未配置 ForwardedHeaders，则改为从配置读取允许的 IP 列表
- **代码要点**：
  ```csharp
  public class HangfireLocalOnlyAuthorizationFilter : IDashboardAuthorizationFilter
  {
      // 允许的 IP 列表（从配置读取，默认仅 loopback）
      private readonly string[] _allowedIps;
      public HangfireLocalOnlyAuthorizationFilter(IConfiguration config)
      {
          _allowedIps = config.GetSection("Hangfire:AllowedIps").Get<string[]>()
              ?? new[] { "127.0.0.1", "::1" };
      }
      public bool Authorize(DashboardContext context) =>
          context.Request.LocalIpAddress == null
          || _allowedIps.Contains(context.Request.LocalIpAddress.ToString());
  }
  ```

### S0-3 默认账号处理
- **文件**：`src/Wms.Core.WebApi/Services/DbInitializer.cs`
- **修改**：
  1. 默认账号创建逻辑改为**仅在非 Production 环境**执行（检查 `env.IsProduction()`）
  2. 移除第 126 行 `builder.Services.GetRequiredService<ILogger>()` 对默认密码的日志输出
  3. Production 环境改为从环境变量 `ADMIN_DEFAULT_PASSWORD` 读取（如未配置则跳过创建）

### S0-4 WCS/出库接口加 IP 白名单
- **文件**：
  - 新建 `src/Wms.Core.WebApi/Filters/InternalIpWhitelistAttribute.cs`
  - 修改 `src/Wms.Core.WebApi/Controllers/Api/WcsController.cs`（第 32 行）
  - 修改 `src/Wms.Core.WebApi/Controllers/Api/OutboundTimerController.cs`（第 15 行）
- **方案**：创建 `[InternalIpWhitelist]` ActionFilter，从配置 `Wcs:AllowedIps` 读取允许的 IP 列表（支持 CIDR）
- **重要：反向代理支持**：WCS 通常部署在内网经反向代理访问，`HttpContext.Connection.RemoteIpAddress` 可能拿到代理 IP。需要**优先从 `X-Forwarded-For` header 提取真实 IP**（配合 `UseForwardedHeaders` 中间件）
- **WcsController**：保留 `[AllowAnonymous]` 但添加 `[InternalIpWhitelist]`
- **OutboundTimerController**：同上处理

### S0-5 异常信息不再泄露到前端
- **文件**：`src/Wms.Core.WebApi/Middleware/GlobalExceptionHandler.cs`
- **修改**：
  1. 第 57 行：`exception.Message` 替换为统一错误码 + 通用消息（如 "服务器内部错误，请联系管理员"）
  2. 详细异常只写入 `ILogger`，不返回到 Response
  3. 在 `appsettings.json` 中控制：Development 环境可返回详细错误（通过 `IWebHostEnvironment` 注入判断，**不要**用 `Environment.GetEnvironmentVariable`）

### S0 验证
```bash
dotnet build --no-restore
# 确认 CodeGenerationController 路由 404
# 确认 /hangfire 非本地访问返回 403
# 确认 WCS 接口非白名单 IP 返回 403
# 确认异常返回不包含 SQL/路径等敏感信息
```

---

## Phase S1：同步阻塞修复（稳定性）

> 目标：消除 .Wait()、.GetAwaiter().GetResult()、Task.Run 包装同步代码

### S1-1 AuthService 去除 Task.Run + .Wait()
- **文件**：`src/Wms.Core.Infrastructure/Services/AuthService.cs`
- **修改 4 个方法**：`LoginAsync`、`CreateUserAsync`、`ChangePasswordAsync`、`ResetPasswordAsync`
- **改动**：
  1. 移除外层 `Task.Run(() => { ... })` 包装
  2. `LoginAsync` 中 `_db.SaveChangesAsync().Wait()` → `await _db.SaveChangesAsync()`
  3. `CreateUserAsync` 中 `_userRepository.Exists()` 和 `_db.Set<Role>().FirstOrDefault()` 改为异步版本或保持同步调用（EF Core 同步方法在非高并发场景可接受）
  4. 所有方法已经是 `async Task<T>`，只需把内部改为真正的 `await`

### S1-2 RefreshTokenRepository 改为异步接口
- **文件**：
  - 修改 `src/Wms.Core.Domain/Repositories/IRefreshTokenRepository.cs`（接口改为 async）
  - 修改 `src/Wms.Core.Infrastructure/Persistence/Repositories/RefreshTokenRepository.cs`（实现改为 async）
  - 修改 `src/Wms.Core.WebApi/Controllers/AuthController.cs`（调用处改为 await）
- **接口变更**：
  ```csharp
  // Before                              // After
  RefreshToken? GetByToken(string);     Task<RefreshToken?> GetByTokenAsync(string);
  void RevokeAllUserTokens(int);         Task RevokeAllUserTokensAsync(int);
  void RevokeToken(RefreshToken);        Task RevokeTokenAsync(RefreshToken);
  int CleanExpiredTokens();              Task<int> CleanExpiredTokensAsync();
  // GetValidRefreshTokens 保持 IQueryable（不需要改）
  ```
- **AuthController 改动**：`Create(refreshTokenEntity)` → `await CreateAsync(...)`，`RevokeAllUserTokens(userId)` → `await ...`

### S1-3 LocationAllocator 去除 .GetAwaiter().GetResult()
- **文件**：`src/Wms.Core.Infrastructure/Handlers/WcsRequest/LocationAllocator.cs`
- **修改**：第 450-453 行 `SplitUnitload` 方法中的 `ExecuteSqlRawAsync(...).GetAwaiter().GetResult()` → `await ExecuteSqlRawAsync(...)`
- **传播链**：`SplitUnitload` 是 `static void` 同步方法（第 428 行），需改为 `static async Task SplitUnitloadAsync`。其调用方需改为 `await`，向上传播到 `AllocateAsync`（已是 async，可直接 await）
- **影响范围**：需搜索所有调用 `SplitUnitload` 的位置并同步修改

### S1 验证
```bash
dotnet build --no-restore
dotnet test --no-build --verbosity normal
```

---

## Phase P0：构建基线清理

> 目标：0 errors，warnings 按类别压低，重复包清理

### P0-1 移除重复 PackageReference
- **文件**：
  - `src/Wms.Core.Domain/Wms.Core.Domain.csproj` — 移除 `Microsoft.EntityFrameworkCore`、`Microsoft.EntityFrameworkCore.Relational`、`Microsoft.Data.SqlClient`、`System.Configuration.ConfigurationManager`（S1 完成后 Domain 不再需要 EF）
  - `src/Wms.Core.Application/Wms.Core.Application.csproj` — 保留 Excel 相关包
  - `src/Wms.Core.Infrastructure/Wms.Core.Infrastructure.csproj` — 移除重复的 `Microsoft.Extensions.Logging.Abstractions`
  - `src/Wms.Core.WebApi/Wms.Core.WebApi.csproj` — 移除与 Infrastructure/Application 重复的 `ExcelDataReader`、`ClosedXML`；NLog 留在 WebApi

### P0-2 修复已知测试失败
- **文件**：
  - `tests/Wms.Core.UnitTests/Services/AuthServiceTests.cs`（4 个测试）
  - `tests/Wms.Core.UnitTests/Entities/UserTests.cs`（12 个测试）
- **问题**：AuthService 测试 Mock 了 `WmsDbContext` 但 `SaveChangesAsync().Wait()` 在 Mock 上会失败。S1-1 修复 AuthService 后这些测试需要同步更新 Mock 设置
- **更新**：`_mockDb.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);`

### P0-3 压低构建警告
- **分类处理**：
  - CA/CS 类警告（安全相关）：优先修复（如 CA1062 null 检查）
  - CS1591 XML 注释缺失：在项目文件中 `<NoWarn>CS1591</NoWarn>` 全局抑制（避免一次性修复 628 条）
  - EF Core Raw SQL 警告：逐个参数化
- **目标**：将 628 warnings 压到 200 以下（剩余大部分为 CS1591）

### P0 验证
```bash
dotnet build --no-restore
# 确认 0 errors
dotnet test --no-build --verbosity normal
# 确认所有测试通过
```

---

## Phase P1：收紧分层边界

> 目标：Domain 零外部依赖，WebApi 只引用 Application

### P1-1 Domain 去除所有基础设施依赖
- **文件**：`src/Wms.Core.Domain/Wms.Core.Domain.csproj`
- **前置条件**：S1 已完成，所有 .Wait() 和 EF 类型引用已从 Domain 层代码中移除
- **移除的包**：
  - `Microsoft.EntityFrameworkCore`
  - `Microsoft.EntityFrameworkCore.Relational`
  - `Microsoft.Data.SqlClient`
  - `System.Configuration.ConfigurationManager`
- **阻塞性问题 — 必须先解决**：
  - `src/Wms.Core.Domain/Repositories/IRepository.cs` 第 2 行 `using Microsoft.EntityFrameworkCore.Query;` 引用了 `SetPropertyCalls<T>`（用于第 116-118 行 `BulkUpdateAsync` 方法）
  - **处理方式**：从 `IRepository<T,TKey>` 接口中**移除** `BulkUpdateAsync` 和 `BulkDeleteAsync` 两个方法，将它们移到 Infrastructure 层的新接口 `IBulkRepository<T,TKey>` 中（仅在 Infrastructure 引用的地方可用）
  - `Repository<T,TKey>` 实现类中这两个方法保留不变，只是 Domain 层的接口不再包含它们
- **检查点**：`dotnet build src/Wms.Core.Domain/` 通过。实体上的 `[Table]`/`[Column]`/`[NotMapped]` 等注解来自 `System.ComponentModel.DataAnnotations.Schema`（.NET 基础库），不依赖 EF Core 包，无需修改

### P1-2 WebApi 移除对 Domain 的直接引用（降级为原则约束）
- **文件**：`src/Wms.Core.WebApi/Wms.Core.WebApi.csproj`
- **现实评估**：WebApi 中有 **32 个文件**直接引用 `Wms.Core.Domain.Entities`、**22 个文件**直接引用 `Wms.Core.Infrastructure.Persistence`（WmsDbContext）。Application 层仅包含 DTOs 和 ExcelService，不包含 Domain 实体类型。因此**在 P1 阶段直接移除 WebApi → Domain 引用不可行**——编译会失败
- **调整为原则约束**：
  1. 保留 WebApi → Domain 引用（暂不动 csproj）
  2. WebApi → Infrastructure 引用保留（DI 注册 + WmsDbContext 注入需要）
  3. **约束**：后续新增 Controller 不得直接注入 Domain Service 实现类或 WmsDbContext——通过 Application Service 间接使用。已有 Controller 的违规在 P2 中逐步修正
- **长期目标**：P2 完成后（Application Service 覆盖主要业务），再考虑移除 WebApi → Domain 引用。可能永远不需要完全移除——Controller 引用 Domain Entity 作为参数类型是合理的

### P1-3 接口从 Domain 迁移到 Application（严格 Clean Architecture 核心）

> 这是最关键的架构改动。把 18 个接口从 Domain 搬到 Application，让 Domain 层真正变为纯领域模型

- **从 `Domain/Services/` 迁移到 `Application/Ports/`（13 个接口）**：
  - `IAuthService.cs`、`ILocationService.cs`、`IUnitloadService.cs`
  - `IBasicDictionaryService.cs`、`IBatteryCellService.cs`、`IBatteryCellSortingService.cs`
  - `ICacheService.cs`、`IOutboundTimerService.cs`、`IPortService.cs`
  - `IRoleService.cs`、`ITranslationService.cs`、`IPasswordHasher.cs`
  - `IContainerCodeValidator.cs`

- **从 `Domain/Interfaces/` 迁移到 `Application/Ports/`（5 个接口）**：
  - `IWcsClient.cs`、`IWcsTaskBridge.cs`、`ICtaskDbService.cs`
  - `IDistributedLockService.cs`、`IInventoryCacheService.cs`
  - `IDapperReadService.cs`、`ITaskCompletionHandler.cs`

- **保留在 Domain**（3 个接口）：
  - `IAuditable`（实体接口）、`IPasswordHasher`（领域安全接口）、`IContainerCodeValidator`（领域校验）
  - Repository 接口（`IRepository<T,TKey>`、`IUserRepository`、`IRefreshTokenRepository`）

- **文件操作**：
  1. 移动接口文件：`git mv src/Wms.Core.Domain/Services/I*.cs src/Wms.Core.Application/Ports/`
  2. 更新所有接口文件的 namespace：`Wms.Core.Domain.Services` → `Wms.Core.Application.Ports`
  3. 更新 Infrastructure 中所有实现类的 `using` 语句（~18 个文件）
  4. 更新 WebApi 中所有 Controller 的 `using` 语句（~20 个文件）
  5. 更新 Tests 中的 `using` 语句

- **Application.csproj 修改**：无需额外添加，已引用 Domain

- **验证**：`dotnet build --no-restore` 通过

### P1-4 WebApi 代码中消除 Infrastructure 类型直接使用
- **目标**：WebApi 保留对 Infrastructure 的项目引用（仅用于 DI 注册），但 Controller/Service/Middleware 代码中不再出现 `WmsDbContext` 等 Infrastructure 类型
- **改动范围**（22 个文件引用 WmsDbContext）：
  - **高优先级**（已规划 Application Service 接管的）：
    - `AuthController.cs` — P2-2 中 `AuthApplicationService` 接管
    - `WcsController.cs` — P2-2 中 `WcsRequestApplicationService` 接管
  - **中优先级**（后续补充 Application Service）：
    - `UsersController.cs`、`UnitloadsController.cs`、`LocationsController.cs`
    - `TransTasksController.cs`、`PortController.cs`、`RacksController.cs`
    - `LanewaysController.cs`、`WarehousesController.cs`、`MaterialsController.cs`
    - `OutboundBatchController.cs`、`BasicDictionaryController.cs`
    - `UnitloadItemDetailsController.cs`、`ArchivedUnitloadsController.cs`
    - `LogController.cs`、`RoleController.cs`、`MenusController.cs`
    - `SimToolController.cs`、`FlowController.cs`
  - **豁免**（可保留 WmsDbContext）：
    - `Program.cs` — DI 注册属于 Composition Root
    - `DbInitializer.cs` — 启动时的数据库初始化，不属于业务逻辑
    - `JobDispatcher.cs` — 内部方法分发，通过 scope 获取 db（后续可优化）
    - `BackgroundJobService.cs` — 同上
    - `LogCleanupService.cs` — 后台清理任务（后续可提取到 Infrastructure）
    - `WcsTaskSyncService.cs` — 后续 P2 中由 Application Service 接管
    - `FlowTemplateSeeder.cs` — 启动种子数据
    - `OperationLogFilter.cs` — 后续 P4-5 中改为 Channel 队列
    - `WmsHealthCheck.cs` — 健康检查需要直接访问 db
- **此阶段执行策略**：P1-4 仅处理**高优先级**的 2 个 Controller（AuthController、WcsController），其余 Controller 在后续阶段逐批迁移。WebApi → Infrastructure 项目引用**暂保留**

### P1-3 清理 Domain 空 Folder
- **文件**：`src/Wms.Core.Domain/Wms.Core.Domain.csproj`
- **移除**：`<Folder Include="Helpers\" />`、`<Folder Include="Models\" />`
- **WebApi**：移除 `<Folder Include="Validators\" />`（FluentValidation Validator 在 P3 中补充）

### P1 验证
```bash
dotnet build --no-restore
dotnet test --no-build --verbosity normal
```

---

## Phase P2：重塑 Application 层 + 拆分启动配置

> 目标：Controller 业务逻辑上移到 Application Service；Program.cs 拆分为扩展方法

### P2-1 Program.cs 拆分
- **新建文件**：
  - `src/Wms.Core.WebApi/Extensions/AuthenticationExtensions.cs` — JWT + CORS 配置
  - `src/Wms.Core.WebApi/Extensions/HangfireExtensions.cs` — Hangfire 注册 + Dashboard
  - `src/Wms.Core.WebApi/Extensions/WcsExtensions.cs` — WCS 客户端 + Bridge + Handler 注册
  - `src/Wms.Core.WebApi/Extensions/RedisExtensions.cs` — Redis/缓存/SignalR backplane 注册
  - `src/Wms.Core.WebApi/Extensions/HealthCheckExtensions.cs` — 健康检查
  - `src/Wms.Core.WebApi/Extensions/MiddlewareExtensions.cs` — 中间件管道配置
- **Program.cs 目标**：从 ~620 行缩减到 ~60 行
- **复用现有**：`src/Wms.Core.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`（已有，保留）

### P2-2 Application 层补充业务服务
- **新建文件**（按业务模块组织）：
  - `src/Wms.Core.Application/Services/Wcs/WcsRequestApplicationService.cs` — 从 `WcsController.WcsRequest()` (200+ 行) 下沉
  - `src/Wms.Core.Application/Services/Auth/AuthApplicationService.cs` — 从 `AuthController.Login()` / `Refresh()` 下沉角色查询 + Token 生成逻辑
- **核心挑战 — WmsDbContext 直接注入**：
  - 当前 WcsController 第 166 行直接 `GetRequiredService<WmsDbContext>()`，AuthController 第 32 行直接注入 `WmsDbContext`
  - Application 层不应依赖 Infrastructure 中的 `WmsDbContext`
  - **处理方式**：WcsRequestApplicationService 通过 `IRepository`/`ILocationService`/`IUnitloadService` 等已有接口操作数据，不直接接触 DbContext。AuthApplicationService 中的角色查询也通过 Repository 接口
  - **FlowContext 问题**：FlowContext 直接持有 `WmsDbContext`（违反分层），在 P2 中暂时保留（改 FlowContext 需要修改 15+ 个 NodeHandler，风险太高），标记为后续优化项
- **原则**：
  - Controller 只做参数绑定 + 调用 ApplicationService + 返回响应
  - ApplicationService 协调 Domain Service + Repository，编排业务流程
  - 不改变现有接口命名风格（`IXxxService`），不引入 UseCase 命名
- **SignalR Hub 无鉴权**：`src/Wms.Core.WebApi/Hubs/WmsHub.cs` 的 `OnConnectedAsync` 是空实现且无任何认证。在 P2 中添加 JWT Token 认证（通过 `Context.User` 或 query string token），并在连接后按用户/角色分组

### P2-3 统一返回格式
- **文件**：
  - 现有 `Wms.Core.Domain/Utilities/Response/Result.cs` — 保留为标准响应
  - `WcsResult` 保留（WCS 设备接口需要固定格式）
  - 修改 `AuthController`：`Ok(response)` / `StatusCode(...)` 匿名对象 → 统一用 `Result<T>`
  - 修改 `GlobalExceptionHandler`：返回统一 `Result.Fail(errorCode)`

### P2 验证
```bash
dotnet build --no-restore
dotnet test --no-build --verbosity normal
# 手动测试关键接口：登录、WCS 请求、刷新 Token
```

---

## Phase P3：多实例与缓存治理

> 目标：补 Redis 多实例支持，消除 5 个子系统失效风险

### P3-1 RedisExtensions 实现多实例切换
- **文件**：`src/Wms.Core.WebApi/Extensions/RedisExtensions.cs`（P2-1 新建）
- **改动**：
  1. Redis 启用时：`ICacheService` 注册为 `DistributedCacheService`（替代 `MemoryCacheService`）
  2. SignalR 添加 Redis Backplane：`services.AddSignalR().AddStackExchangeRedis(...)`
  3. 速率限制：启用已安装的 `AspNetCoreRateLimit` + Redis 分布式后端（替代自实现 `RateLimitMiddleware`）

### P3-2 FlowEngine 模板缓存一致性
- **文件**：`src/Wms.Core.Infrastructure/Services/FlowEngineService.cs`
- **改动**：模板缓存使用 `IDistributedCache`（Redis 启用时），失效时通过 Redis pub/sub 通知所有实例清除本地缓存

### P3-3 WCS 轮询防重复消费
- **文件**：`src/Wms.Core.Infrastructure/Clients/DatabaseWcsTaskBridge.cs`
- **改动**：`PollStatusChangesAsync` 加分布式锁（使用已有 `IDistributedLockService`），确保多实例只有一个在处理

### P3-4 LogCleanupService 多实例协调
- **文件**：`src/Wms.Core.WebApi/Services/LogCleanupService.cs`
- **改动**：执行前获取分布式锁，获取失败则跳过（另一个实例正在执行）

### P3 验证
```bash
dotnet build --no-restore
dotnet test --no-build --verbosity normal
# 多实例部署测试（docker-compose --scale wms-api=2）
```

---

## Phase P4：工程卫生与质量提升

> 目标：清理 MES 残留、补 FluentValidation、补测试、密码哈希升级

### P4-1 Docker 配置全面修复
- **文件**：
  - `docker/Dockerfile` — `Mes.Core.*` → `Wms.Core.*`
  - `docker/docker-compose.yml` — 容器名、网络名、数据库连接字符串
  - `docker/docker-compose.prod.yml` — 同上
- **具体改动**：
  - `COPY ["src/Mes.Core.WebApi/Mes.Core.WebApi.csproj", ...]` → `COPY ["src/Wms.Core.WebApi/Wms.Core.WebApi.csproj", ...]`
  - `ENTRYPOINT ["dotnet", "Mes.Core.WebApi.dll"]` → `ENTRYPOINT ["dotnet", "Wms.Core.WebApi.dll"]`
  - `mes-api` → `wms-api`，`mes-sqlserver` → `wms-sqlserver`，`mes-redis` → `wms-redis`
  - `MesDb` → `WmsDb`
  - HEALTHCHECK：`curl` → `wget --spider http://localhost/healthz`（或安装 curl）
  - Redis healthcheck：添加 `--pass ${REDIS_PASSWORD}` 参数

### P4-2 .gitignore 补充 + 清理
- **文件**：`.gitignore`
- **添加**：
  ```
  DataProtection-Keys/
  uploads/
  *.csproj.user
  .vs/
  ```
- **清理**：
  - 删除 `src/Wms.Core.WebApi/update-controllers.bat`、`test-db.csx`、`update-versions.csx` → 移到 `scripts/`
  - 删除 `.vs/Mes.Net8/` 目录

### P4-3 FluentValidation 落地（为关键接口创建 Validator）
- **新建文件**：
  - `src/Wms.Core.WebApi/Validators/LoginRequestValidator.cs`
  - `src/Wms.Core.WebApi/Validators/GenerateRequestValidator.cs`（如果 CodeGenerationController 保留的话，S0-1 会删除）
- **注册**：在 `Program.cs`（或 `AuthenticationExtensions.cs`）中 `services.AddValidatorsFromAssembly(...)`
- **范围**：仅对 Auth 相关请求创建 Validator，其余接口后续逐步补充

### P4-4 密码哈希迁移（HMACSHA256 → BCrypt）
- **文件**：
  - `src/Wms.Core.Domain/Entities/Identity/User.cs`
  - `src/Wms.Core.Infrastructure/Services/AuthService.cs`
- **方案**：
  1. `SetPassword()` 改用 `BCrypt.Net.BCrypt.HashPassword(password)`
  2. `ValidatePassword()` 先检测格式：BCrypt hash 以 `$2` 开头 → 用 `BCrypt.Verify()`；否则用旧 HMAC 方式验证
  3. 登录时如果检测到旧 hash，自动升级为 BCrypt（rehash on verify）
  4. `PasswordSalt` 字段保留（BCrypt 自带 salt，该字段废弃但数据库列不删除）
- **注意**：项目已安装 `BCrypt.Net-Next` 包。迁移后需更新 `tests/Wms.Core.UnitTests/Entities/UserTests.cs` 中的密码相关测试

### P4-5 Task.Run fire-and-forget 统一为 Channel 队列
- **背景**：全项目有 14 处 `Task.Run`，其中 FlowEngineService 的 2 处（保存流程实例状态+节点日志）为高优先级——如果丢失会造成审计不可靠
- **文件**：
  - 新建 `src/Wms.Core.Infrastructure/Background/BackgroundTaskQueue.cs`（`Channel<Func<Task>>` 实现）
  - 新建 `src/Wms.Core.Infrastructure/Background/BackgroundTaskQueueHostedService.cs`（`BackgroundService` 消费 Channel）
  - 修改涉及 Task.Run 的文件：WcsController（5 处）、DatabaseWcsTaskBridge（1 处）、FlowEngineService（2 处）、OperationLogFilter（1 处）
- **优先级**：先替换 FlowEngineService（审计数据丢失风险高），再替换其余接口日志写入

### P4-6 LanguagePackMiddleware 性能优化
- **文件**：`src/Wms.Core.WebApi/Middleware/LanguagePackMiddleware.cs`
- **问题**：`InjectLanguagePackToContext` 在每个请求路径上都执行缓存检查/数据库查询（第 141-194 行），即使请求的是静态文件或健康检查
- **修改**：
  1. 只对 `/api/` 路径前缀的请求注入语言包（静态文件、健康检查不需要）
  2. 缓存命中时避免不必要的对象创建

### P4-7 EF Core Migration 管理
- **操作**：
  1. 创建初始 Migration：`dotnet ef migrations add InitialCreate --project src/Wms.Core.Infrastructure`（`Microsoft.EntityFrameworkCore.Design` 已安装）
  2. 修改 `DbInitializer.cs`：移除 `EnsureCreatedAsync()` 和 Raw SQL 建表，改为 `Migrate()`
  3. Flow 表的 seed 数据通过 Migration 的 `SeedData` 方法插入

### P4-8 补测试基线
- **修改文件**：
  - `tests/Wms.Core.UnitTests/Services/AuthServiceTests.cs` — 更新 Mock 以适配 S1 的 async 改造
- **新建文件**（关键业务路径测试）：
  - `tests/Wms.Core.UnitTests/Services/WcsRequestApplicationServiceTests.cs`（P2-2 创建后编写）
  - `tests/Wms.Core.UnitTests/Entities/TransTaskTests.cs` — 任务状态流转
  - `tests/Wms.Core.UnitTests/Entities/LocationTests.cs` — 库位分配逻辑
- **目标**：从当前 ~14 个测试扩展到 30+

### P4 验证
```bash
dotnet build --no-restore
dotnet test --verbosity normal
# Docker 构建测试
docker build -f docker/Dockerfile -t wms-api:test ..
# Docker Compose 测试
docker-compose -f docker/docker-compose.yml up -d
docker-compose -f docker/docker-compose.yml down
```

---

## 改造完成后的四层引用规则（严格 Clean Architecture）

```
WebApi ──→ Application ──→ Domain
                ↑
           Infrastructure ──→ Application ──→ Domain
                                  ↑
                             WebApi（仅 DI 注册调用）

原则：依赖只能向内。WebApi 代码中不出现任何 Infrastructure 类型。
```

| 层 | 允许引用 | 禁止引用 | 说明 |
|---|---|---|---|
| **Domain** | 无（零外部依赖） | EF Core、SqlClient | 仅含 POCO 实体、值对象、枚举、Repository 接口 |
| **Application** | Domain | Infrastructure、WebApi | 含 DTOs、AppService 接口（从 Domain 搬入）、Jobs、Validators |
| **Infrastructure** | Domain、Application | WebApi | 含 EF Core 实现、外部服务实现、WCS Bridge |
| **WebApi** | Application、Infrastructure（仅启动注册） | Domain（直接）、Infrastructure 类型（Controller/Service 中） | Controller 通过 Application 层的接口操作数据，不直接注入 WmsDbContext |

### 接口分布规则

| 位置 | 内容 | 数量 |
|---|---|---|
| **Domain** | Repository 接口（IRepository, IUserRepository, IRefreshTokenRepository）、实体接口（IAuditable）、领域接口（IPasswordHasher, IContainerCodeValidator） | 5 个保留 |
| **Application** | Application Service 接口（18 个，从 Domain 搬入）、WcsRequestHandler（已在 Application） | 19 个 |
| **Infrastructure** | 接口的实现类 | 对应 18 个实现 |

### WebApi → Infrastructure 的唯一例外

WebApi 的 `Program.cs` 中调用 `services.AddWmsCoreInfrastructure(configuration)` 是**允许的**——这是 Composition Root（组合根）模式，在启动时注册 DI，运行时 WebApi 代码不使用任何 Infrastructure 类型。

## 各阶段依赖关系

```
S0 (安全修复)              ← 独立，可立即执行
  ↓
S1 (同步阻塞修复)          ← 独立，可与 S0 并行
  ↓
P0 (构建基线)              ← 依赖 S1（测试 Mock 需更新）
  ↓
P1 (分层边界)              ← 依赖 P0（构建通过后才能动 csproj）
    P1-1 Domain 去依赖
    P1-2 WebApi 原则约束
    P1-3 接口迁移 Domain→Application  ← 最大改动点（18 接口搬家 + ~40 文件 using 更新）
    P1-4 WebApi 代码消除 Infrastructure 类型（首批 2 个 Controller）
  ↓
P2 (Application 层重塑)    ← 依赖 P1-3（接口已在 Application 后才能创建 AppService）
    P2-1 Program.cs 拆分
    P2-2 Application Service 创建 + SignalR Hub 加认证
    P2-3 统一返回格式
  ↓
P3 (多实例治理)            ← 依赖 P2（扩展方法拆分完成后才能补 Redis）
  ↓
P4 (质量提升)              ← 依赖 P2（AuthApplicationService 创建后才能补测试）
```

## 关键文件索引

| 文件 | 涉及阶段 |
|---|---|
| `src/Wms.Core.WebApi/Controllers/CodeGenerationController.cs` | S0-1 删除 |
| `src/Wms.Core.Infrastructure/Persistence/CodeGeneration/EntityGenerator.cs` | S0-1 删除 |
| `src/Wms.Core.WebApi/Program.cs` | S0-2, P2-1 |
| `src/Wms.Core.WebApi/Services/DbInitializer.cs` | S0-3, P4-7 |
| `src/Wms.Core.WebApi/Controllers/Api/WcsController.cs` | S0-4, P2-2 |
| `src/Wms.Core.WebApi/Controllers/Api/OutboundTimerController.cs` | S0-4 |
| `src/Wms.Core.WebApi/Middleware/GlobalExceptionHandler.cs` | S0-5, P2-3 |
| `src/Wms.Core.Infrastructure/Services/AuthService.cs` | S1-1, P4-4 |
| `src/Wms.Core.Domain/Repositories/IRefreshTokenRepository.cs` | S1-2 |
| `src/Wms.Core.Infrastructure/Persistence/Repositories/RefreshTokenRepository.cs` | S1-2 |
| `src/Wms.Core.Infrastructure/Handlers/WcsRequest/LocationAllocator.cs` | S1-3 |
| `src/Wms.Core.Domain/Repositories/IRepository.cs` | P1-1（移除 BulkUpdateAsync/BulkDeleteAsync） |
| `src/Wms.Core.Domain/Wms.Core.Domain.csproj` | P0-1, P1-1, P1-3 |
| `src/Wms.Core.WebApi/Wcs.Core.WebApi.csproj` | P0-1 |
| `src/Wms.Core.WebApi/Controllers/AuthController.cs` | S1-2, P2-2, P2-3 |
| `src/Wms.Core.WebApi/Hubs/WmsHub.cs` | P2-2（加认证） |
| `src/Wms.Core.WebApi/Middleware/LanguagePackMiddleware.cs` | P4-6 |
| `src/Wms.Core.Infrastructure/Services/FlowEngineService.cs` | P3-2, P4-5 |
| `src/Wms.Core.Infrastructure/Clients/DatabaseWcsTaskBridge.cs` | P3-3, P4-5 |
| `src/Wms.Core.WebApi/Services/LogCleanupService.cs` | P3-4 |
| `src/Wms.Core.Domain/Entities/Identity/User.cs` | P4-4 |
| `tests/Wms.Core.UnitTests/Services/AuthServiceTests.cs` | P0-2, P4-4, P4-8 |
| `tests/Wms.Core.UnitTests/Entities/UserTests.cs` | P0-2, P4-4 |
| `docker/Dockerfile` | P4-1 |
| `docker/docker-compose.yml` | P4-1 |
| `docker/docker-compose.prod.yml` | P4-1 |

## 深度检查发现的问题（已修正入计划）

以下是深度检查中发现的原计划遗漏/风险，已在计划中修正：

1. **P1-1 `IRepository.cs` 引用 `Microsoft.EntityFrameworkCore.Query`**（第 2 行 `SetPropertyCalls<T>`）— 如果直接移除 EF 包会编译失败。已补充：将 `BulkUpdateAsync`/`BulkDeleteAsync` 从 Domain 接口移到 Infrastructure 的 `IBulkRepository` 接口
2. **P1-2 WebApi 有 32+22 个文件引用 Domain/Infrastructure** — 直接移除 csproj 引用会导致编译失败。已调整为原则约束，不强制在 P1 改 csproj
3. **S1-3 `SplitUnitload` 是同步方法** — `.GetAwaiter().GetResult()` 在 `static void SplitUnitload` 中（第 428 行），改为 await 需要将方法签名改为 async Task 并传播到调用方
4. **S0-2/S0-4 缺少反向代理 `X-Forwarded-For` 支持** — WMS 通常部署在 Nginx 后面，RemoteIpAddress 拿到的是代理 IP。已补充 ForwardedHeaders 配置说明
5. **P2-2 遗漏了 WmsDbContext 直接注入问题** — WcsController/AuthController 直接注入 WmsDbContext，Application Service 不能这么做。已补充处理方案：通过 Repository 接口操作
6. **遗漏 FlowContext 持有 WmsDbContext** — 修改需要改动 15+ 个 NodeHandler，风险太高。标记为后续优化项，不在此轮执行
7. **遗漏 SignalR Hub 无鉴权** — 已加入 P2-2
8. **遗漏 Task.Run fire-and-forget 统一替换** — 已加入 P4-5（Channel 队列方案）
9. **遗漏 LanguagePackMiddleware 全路径拦截性能问题** — 已加入 P4-6

## 风险分析与缓解措施

### 各阶段可能引入的 BUG / 功能影响 / 安全漏洞 / 事务一致性问题

---

#### S0 安全修复阶段

| 步骤 | 风险 | 影响 | 缓解措施 |
|---|---|---|---|
| S0-1 删除 CodeGenerationController | 无功能影响 | 低 | 该接口不应在生产环境暴露 |
| S0-2 Hangfire 鉴权 | WCS 管理员在远程访问时被拒绝 | **中** | `Hangfire:AllowedIps` 配置必须包含管理员 IP。部署时需同步更新配置文件 |
| S0-3 默认账号禁用 | 首次部署 Production 时无 admin 账号 | **高** | 文档中补充：首次部署必须设置 `ADMIN_DEFAULT_PASSWORD` 环境变量，否则无法登录管理后台 |
| S0-4 WCS IP 白名单 | WCS 设备 IP 未配置 → WCS 请求被拒绝 → **生产线停工** | **极高** | 配置 `Wcs:AllowedIps` 必须在代码部署前完成。建议先在测试环境验证，回滚方案：临时注释 `[InternalIpWhitelist]` |
| S0-5 异常信息隐藏 | 前端当前解析 `Result.Fail(ex.Message)` 显示错误原因 → 改为通用消息后前端用户无法判断问题原因 | **中** | 在 `Result.Fail()` 中增加 `errorCode` 字段，前端可根据 error code 显示对应的友好提示（需前后端同步改动） |

---

#### S1 同步阻塞修复阶段

| 步骤 | 风险 | 影响 | 缓解措施 |
|---|---|---|---|
| S1-1 AuthService 去 Task.Run | `SaveChangesAsync().Wait()` → `await SaveChangesAsync()` 功能等价，但执行线程从线程池变为异步上下文 | 低 | 纯重构，不改变业务逻辑。测试登录/创建用户/改密码/重置密码 |
| S1-2 RefreshTokenRepository 异步化 | 接口签名从 sync → async 是**破坏性变更**。如有其他调用方未更新，编译失败 | **中** | Grep 确认只有 AuthController 和 ServiceCollectionExtensions 使用此接口（已确认：4 个文件）。编译即捕获 |
| S1-3 LocationAllocator 异步化 | `SplitUnitload` 从 `void` → `Task`，如果调用方忘记 `await`，操作变成 fire-and-forget → **拆盘数据丢失** | **高** | 编译器不会报错（返回值未 await 是 warning 不是 error）。必须搜索所有 `SplitUnitload` 调用点，逐一确认加了 `await` |

---

#### P0 构建基线清理

| 步骤 | 风险 | 影响 | 缓解措施 |
|---|---|---|---|
| P0-1 删除重复包 | 删除了某个包但代码运行时仍在用 → `TypeLoadException` / `FileNotFoundException` | **高** | 不能只看 csproj，需确认每个包的实际运行时引用。建议逐包删除、每次删除后 `dotnet build` + `dotnet test` |
| P0-2 测试修复 | Mock 设置不完整 → 测试通过但实际行为改变 | 低 | 测试本身就在验证行为，只要测试用例正确就不会引入 BUG |
| P0-3 警告抑制 | `CS1591` 抑制后可能掩盖真正需要 XML 注释的 public API | 低 | 仅在过渡期使用，后续逐步补 XML 注释 |

---

#### P1 分层边界阶段（最大风险）

| 步骤 | 风险 | 影响 | 缓解措施 |
|---|---|---|---|
| P1-1 Domain 去 EF 包 | `IRepository.BulkUpdateAsync` 引用 `SetPropertyCalls<T>`（EF 类型）。如果忘记移到 IBulkRepository → **编译失败** | **高** | 已在计划中明确标注。必须在删除包之前先创建 `IBulkRepository` 并移动方法 |
| P1-3 接口迁移 | namespace 变更导致 DI 注册失败 → **所有接口注入的应用在启动时报错** | **极高** | 最核心的风险。缓解步骤：(1) 移动接口文件并更新 namespace；(2) 全局搜索旧 namespace 并替换为新 namespace；(3) 更新 Infrastructure 中的 `ServiceCollectionExtensions.cs` 的 DI 注册代码；(4) `dotnet build` 验证 |
| P1-4 AuthController 去 WmsDbContext | AuthController 第 73 行用 WmsDbContext 做 JOIN 查询角色。替换为 Repository 后需确保查询逻辑一致 | **中** | AuthApplicationService 通过 UserRepository 或新建 IRoleRepository 获取角色。对比新旧查询确保返回相同数据 |
| P1-4 WcsController 去 WmsDbContext | WcsController 第 166 行用 WmsDbContext.GetRequiredService 做流程执行。FlowContext 需要 DbContext，如果 WcsRequestApplicationService 不提供 DbContext，**FlowEngine 无法运行** | **高** | P1-4 中 WcsController 的流程执行部分暂不迁移（需 FlowContext + DbContext），仅迁移日志记录和异常处理。流程执行移到 Application Service 需等 FlowContext 解耦（标记为后续项） |

---

#### P2 Application 层重塑

| 步骤 | 风险 | 影响 | 缓解措施 |
|---|---|---|---|
| P2-1 Program.cs 拆分 | 中间件注册顺序错误 → CORS、Auth、RateLimit 等行为异常 | **中** | 按当前 Program.cs 中的顺序原样提取。拆分后对比中间件注册列表确保顺序一致 |
| P2-2 WcsRequestApplicationService | 业务逻辑迁移时遗漏某个分支或条件 → **WCS 请求行为变更** | **高** | 迁移前写测试用例覆盖 WcsRequest 的关键分支（正常入库、入库双叉、无匹配 Handler、异常处理）。迁移后测试全部通过 |
| **事务一致性 — P2-2** | 当前 WcsController.WcsRequest 中 FlowEngine 执行和日志写入在同一个请求上下文。迁移后如果日志改为 Channel 队列写入，**日志与业务操作不在同一事务** | **高但可接受** | FlowEngine 自身事务不受影响（NodeHandler 内部使用 FlowContext.DbContext）。接口日志异步写入是设计选择——日志丢失不影响业务数据一致性 |
| **事务一致性 — P2-2** | AuthController.Login 创建 RefreshToken 并保存。如果迁移到 Application Service 后 SaveChanges 在不同 scope 执行 → Token 保存失败但 JWT 已生成 | **中** | AuthApplicationService 注入 IRefreshTokenRepository 使用同一个 scope（非手动 CreateScope），确保 Token 保存和 JWT 生成在同一工作单元 |

#### P3 多实例治理

| 步骤 | 风险 | 影响 | 缓解措施 |
|---|---|---|---|
| P3-1 切换缓存实现 | Redis 连接不稳定时所有缓存操作失败 | **中** | RedisExtensions 已实现回退逻辑：Redis 未启用时回退到 `AddDistributedMemoryCache` |
| P3-3 WCS 轮询锁 | 分布式锁实现有 BUG 或 Redis 不可用 → **WCS 状态同步永久阻塞** | **高** | `DistributedLockService` 已有 Lua 脚本实现（正确）。需添加锁超时保护（确保调用方传了合理 expiry 值） |

#### P4 质量提升

| 步骤 | 风险 | 影响 | 缓解措施 |
|---|---|---|---|
| P4-4 BCrypt 迁移 | `rehash on verify` 逻辑有 BUG → 所有用户登录失败 → **全系统锁定** | **极高** | 分两步走：(1) 先部署仅支持双格式验证（BCrypt + HMAC），不自动升级；(2) 验证稳定后再开启自动升级 |
| P4-7 EF Migration | `EnsureCreatedAsync` → `Migrate()`。Migration 脚本如果有破坏性 schema 变更 → **数据丢失** | **极高** | 初次 Migration 时手动编辑脚本确保不含破坏性变更（DROP TABLE、ALTER COLUMN 等）。Migration 文件必须人工审查 |

---

### 关键事务一致性保障清单

| 事务场景 | 当前状态 | 改造影响 | 是否安全 |
|---|---|---|---|
| FlowEngine 节点执行 | 同一 WmsDbContext 内隐式事务 | 不受影响（FlowContext 暂不解耦） | 安全 |
| WCS 任务创建 + WcsTask 下发 | 不在同一事务（既有问题） | 不恶化 | 安全 |
| 登录验证 + RefreshToken 创建 | 同一 scope | 迁移到 Application Service 保持同一 scope | **需确保** |
| 入库流程 + 接口日志写入 | 日志当前是 Task.Run fire-and-forget | 改为 Channel 队列，本质上等价 | 安全 |
| 库存更新 + 缓存更新 | 库存在事务内，缓存在事务外 | 不受影响 | 安全 |
