# Wms.Core 改造实施计划

> **定位**：本文件是执行计划，不是问题清单。问题清单参见 [WMS-改造计划审查报告](WMS-改造计划审查报告.md)（53 个问题），原始改造方案参见 [WMS-综合改造计划](WMS-综合改造计划.md)。
>
> **策略**：先封口、再建基线、再治理数据、再升级安全、最后架构重构。每个阶段独立验收，阶段间可发布。
>
> **核心原则**：即使结构还不优雅，系统也不能裸奔。

---

## 问题索引（审查报告 → 本计划映射）

| 阶段 | 覆盖的审查报告问题编号 |
|------|------------------------|
| Phase 1 生产封口 | #7, #16, #18, #20, #21, #24, #30, #37, #43, #44, #11(部分) |
| Phase 2 运行基线 | #32, #42, #45, #46, #48, #49, #50, #53, #11(Docker), #17, #52, #19 |
| Phase 3 数据层治理 | #4, #5, #25, #29, #31, #10, #12, #13 |
| Phase 4 认证与安全升级 | #9, #15, #23, #34, #35, #38, #39, #40, #41, #47, #51, #27 |
| Phase 5 架构重构 | #1, #2, #3, #6, #8, #14, #22, #26, #28, #33, #36 |

---

## Phase 1：生产封口

> **目标**：优先处理能直接造成生产事故的安全点。不碰大架构，不重构分层。
>
> **验收标准**：
> - `dotnet build --no-restore` 零 error
> - 所有匿名端点有访问控制
> - 无 `ex.Message` / `exception.Message` 直接返回客户端
> - 无默认密码写入日志
> - Docker 构建可用（MES 残留已清理）
> - InboundDoubleRequestHandler 功能恢复正常

### 1.1 删除 CodeGenerationController ✅ 已完成

- **来源**：原计划 S0-1
- **操作**：
  - ~~删除 `src/Wms.Core.WebApi/Controllers/CodeGenerationController.cs`~~ 已删除
  - ~~删除 `src/Wms.Core.Infrastructure/Persistence/CodeGeneration/EntityGenerator.cs`~~ 已删除（含空目录）
- **验证**：`dotnet build --no-restore` 通过（0 errors, 457 warnings）

### 1.2 Hangfire Dashboard 加鉴权 ✅ 已完成

- **来源**：原计划 S0-2 + 审查报告 #7
- **操作**：
  - ~~新建 `src/Wms.Core.WebApi/Security/HangfireIpAuthorizationFilter.cs`~~ 已创建，null IP 拒绝（#7 修复）
  - ~~修改 `Program.cs` 中 `UseHangfireDashboard` 调用~~ 已添加 `DashboardOptions.Authorization`
  - `Program.cs` 添加 `using Wms.Core.WebApi.Security;`
- **代码**：
  ```csharp
  public bool Authorize(DashboardContext context) =>
      context.Request.LocalIpAddress != null       // #7 修复：null 时不放行
      && _allowedIps.Contains(context.Request.LocalIpAddress.ToString());
  ```
- **配置项**：`Hangfire:AllowedIps` 从 appsettings 读取，默认 `["127.0.0.1", "::1"]`
- **验证**：非白名单 IP 访问 `/hangfire` 返回 403

### 1.3 WCS/出库接口加 IP 白名单 ✅ 已完成

- **来源**：原计划 S0-4 + 审查报告 #6, #8, #26
- **操作**：
  - ~~新建 `src/Wms.Core.WebApi/Filters/InternalIpWhitelistAttribute.cs`~~ 已创建，支持 X-Forwarded-For
  - ~~`WcsController.cs` — 保留 `[AllowAnonymous]` 但添加 `[InternalIpWhitelist]`~~ 已添加
  - ~~`OutboundTimerController.cs` — 同上~~ 已添加
- **行为**：无配置时放行（避免误拦截）；有 `Wcs:AllowedIps` 配置时严格匹配
- **配置项**：`Wcs:AllowedIps` 支持逗号分隔的 IP 列表
- **注意**：如部署在反向代理后，需配合 `UseForwardedHeaders`（Phase 5 #8 统一处理，此处先按直连场景实现）
- **验证**：非白名单 IP 返回 403

### 1.4 默认账号处理 ✅ 已完成

- **来源**：原计划 S0-3
- **操作**（`DbInitializer.cs`）：
  - 默认账号创建仅在非 Production 环境执行
  - 移除第 126 行默认密码的日志输出
  - Production 从环境变量 `ADMIN_DEFAULT_PASSWORD` 读取，未配置则跳过
- **验证**：Production 模式下启动不创建默认账号、不打印密码

### 1.5 统一禁止 `ex.Message` 返回客户端 ✅ 已完成

- **来源**：原计划 S0-5 + 审查报告 #24, #20, #21
- **规模**：**~130 处** `Result.Fail(ex.Message)` 分布在 **26 个文件**（审查报告称"30+"，实际低估 4 倍）。详见下方文件清单。
- **受影响文件清单**（按数量排序）：
  - UnitloadService.cs: 6 处 | SimToolController.cs: 7 处 | RoleController.cs: 9 处 | MenusController.cs: 9 处 | LocationsController.cs: 9 处
  - UsersController.cs: 6 处 | LogController.cs: 8 处 | Sys_LanguageController.cs: 7 处 | BatteryCellsController.cs: 6 处 | BatteryCellSortingController.cs: 6 处
  - WarehousesController.cs: 5 处 | UnitloadsController.cs: 6 处 | MaterialsController.cs: 8 处 | BasicDictionaryController.cs: 7 处 | PortController.cs: 6 处
  - RacksController.cs: 5 处 | OutboundBatchController.cs: 5 处 | LanewaysController.cs: 6 处 | TransTasksController.cs: 4 处 | AuthController.cs: 1 处
  - UploadController.cs: 3 处 | ArchivedUnitloadsController.cs: 3 处 | ArchivedTasksController.cs: 2 处 | UnitloadItemDetailsController.cs: 2 处 | UnitloadsOpsController.cs: 1 处 | PortService.cs: 1 处
- **方案**（架构级，非逐个修复，因为 130 处逐个改不现实）：
  1. **`GlobalExceptionHandler.cs`**：`exception.Message` 替换为 "服务器内部错误，请联系管理员"。Development 环境通过 `IWebHostEnvironment` 判断可返回详情（**不要**用 `Environment.GetEnvironmentVariable`）
  2. **`LanguagePackMiddleware.cs:134`**（#20）：`ex.Message` → "语言包获取失败"
  3. **Health Check 端点**（#21）：`Program.cs:583` 过滤 `exception` 字段
  4. **~130 处 `Result.Fail(ex.Message)`**（#24）：**逐文件正则替换** — 在每个 catch 块中将 `Result.Fail(ex.Message)` 替换为 `Result.Fail("操作失败")` + 紧跟 `ILogger.LogError(ex, ...)`。建议编写脚本批量处理，人工复查关键 Controller（AuthController、WcsController、UsersController）
  5. **`GlobalExceptionHandler.cs` 本身**：第 57 行 `Message = exception.Message` 改为按异常类型返回安全消息（已有 `GetErrorDetail` 方法，但 `Message` 字段仍泄露原始信息）
- **验证**：触发异常后响应中不包含 SQL/路径/堆栈信息

### 1.6 `throw new Exception()` → `InvalidRequestException` ✅ 已完成

- **来源**：审查报告 #30（审查称"23+ 处"，实际验证为 **21 处，3 个文件**）
- **实际分布**：UnitloadService.cs (14 处) | WcsController.cs (3 处) | RoleService.cs (4 处)
- **操作**：批量替换全项目 `throw new Exception("业务验证消息")` 为 `throw new InvalidRequestException("业务验证消息")`
- **注意**：WcsController 的 3 处 `throw new Exception` 是 WCS 请求参数校验，改为 `InvalidRequestException` 后 WCS 设备会收到 400 而非 500，避免误判为系统故障重试
- **验证**：`grep -r "throw new Exception" src/` 结果为零

### 1.7 修复 InboundDoubleRequestHandler 未注册 ✅ 已完成

- **来源**：审查报告 #16
- **操作**（`Program.cs`）：添加一行
  ```csharp
  builder.Services.AddScoped<WcsReqHandler, WcsReqHandlers.InboundDoubleRequestHandler>();
  ```
- **验证**：入库双叉 WCS 请求正常处理

### 1.8 Docker 配置 MES 残留修复 ✅ 已完成

- **来源**：原计划 P4-1 + 审查报告 #11
- **操作**：
  - `Dockerfile`：`Mes.Core.*` → `Wms.Core.*`，`mesuser` → `wmsuser`
  - `docker-compose.yml`：容器名、网络名、健康检查（Redis 加 `-a ${REDIS_PASSWORD}`）
  - `docker-compose.prod.yml`：同上
  - API 容器 healthcheck 用 `wget --spider http://localhost/healthz`（替代可能不存在的 `curl`）
  - **csproj MES 残留**（代码验证发现）：
    - `Application.csproj`：`<Authors>Mes Team</Authors>` → `Wms Team`，`<Description>Mes Core Application Layer...` → `Wms Core Application Layer...`
    - `Infrastructure.csproj`：同上 `<Authors>` 和 `<Description>` 中 "Mes" → "Wms"
    - `Domain.csproj`：同上
- **验证**：`docker build` 成功

### 1.9 生产配置紧急修复 ✅ 已完成

- **来源**：审查报告 #37, #43, #44
- **操作**：
  - `appsettings.Production.json`：`AllowedHosts` 从 `"*"` 改为具体域名列表（#37）
  - `appsettings.json`：`Connect Timeout=500` → `Connect Timeout=30`（#43）
  - `appsettings.json`：`Persist Security Info=True` → `false`（#44）
- **验证**：连接字符串参数正确

### Phase 1 验收检查清单

| # | 检查项 | 通过标准 |
|---|--------|----------|
| 1 | `dotnet build --no-restore` | 0 errors |
| 2 | CodeGenerationController 路由 | 404 |
| 3 | `/hangfire` 非本地访问 | 403 |
| 4 | WCS 接口非白名单 IP | 403 |
| 5 | 异常响应内容 | 不含 SQL/路径/堆栈 |
| 6 | `throw new Exception()` 残留 | 0 处（业务验证类） |
| 7 | 默认密码日志 | Production 不输出 |
| 8 | InboundDoubleRequestHandler | DI 已注册 |
| 9 | Docker 构建 | 成功 |
| 10 | Production AllowedHosts | 非 `"*"` |

---

## Phase 2：运行基线

> **目标**：把项目变成"可验证、可部署、可回归"。代码能编译、测试能跑通、容器能启动、健康检查真实可用。
>
> **验收标准**：
> - `dotnet build` 0 errors，warnings ≤ 200
> - `dotnet test` 全部通过
> - `docker-compose up` 一键启动完整环境
> - `/health` 端点返回真实健康状态
> - 测试 bin/obj 已清理，`.gitignore` 已补全
> - .NET 10 升级票据已建立

### 2.1 修复单元测试失败 ✅ 已完成

- **来源**：原计划 P0-2
- **操作**：
  - `AuthServiceTests.cs`（4 个测试）：Mock 适配 — `SaveChangesAsync().Wait()` 修复（Phase 3 后）后需更新 Mock
  - `UserTests.cs`（12 个测试）：检查失败原因并修复
- **注意**：如果 Phase 1 的 `InvalidRequestException` 替换影响测试预期，需同步更新
- **验证**：`dotnet test --verbosity normal` 全部通过

### 2.2 构建警告压降 ✅ 已完成（0 warnings）

- **来源**：原计划 P0-3
- **分类处理**：
  - CS1591 XML 注释缺失：`<NoWarn>CS1591</NoWarn>` 全局抑制
  - CA/CS 安全类警告：优先修复
  - EF Core Raw SQL 警告：逐个参数化
- **目标**：628 warnings → ≤ 200
- **验证**：`dotnet build` 统计 warnings 数

### 2.3 Docker Compose 环境验证 ⏸ 需运行环境

- **来源**：原计划验证部分
- **操作**：
  - 确保 `docker-compose.yml` 可一键启动（SQL Server + Redis + API）
  - API 容器启动后能连接数据库和 Redis
  - 环境变量配置完整（连接字符串、Redis 地址、Admin 密码等）
- **验证**：`docker-compose up -d` 后所有服务 healthy

### 2.4 健康检查端点验证 ⏸ 需运行环境

- **操作**：
  - 确认 `/health` 端点真实检查数据库连接 + Redis 连接
  - Phase 1 已过滤 exception 字段，此处验证端点返回格式正确
  - 确认健康检查在 `appsettings.json` 中可配置启用/禁用
- **验证**：`curl http://localhost/health` 返回 JSON 状态

### 2.5 清理工程卫生 ✅ 已完成

- **来源**：审查报告 #32, #53, #19, #52, #17, #18, #11(遗漏)
- **操作**：
  - 清理 `tests/` 下 `bin/` 和 `obj/` 目录（#32）
  - 删除或移走临时脚本（#53）：`test-db.csx`、`update_versions.csx`、`update-controllers.bat`
  - 补充 `.gitignore`（#11, #15）：
    ```
    DataProtection-Keys/
    uploads/
    *.csproj.user
    .vs/
    bin/
    obj/
    ```
  - 修复 `appsettings.json` 配置结构错误（#17）：`Upload` 和 `Wms` 从 `HealthChecks` 下提升为顶层
  - Program.cs MES 残留替换（#18）："MES API" → "WMS API"，"MES Team" → "WMS Team"
  - 清理死配置（#52）：`DefaultEventBus` 引用
  - 清理 NHibernate 死配置（#19）：`appsettings.Production.json`、`appsettings.Development.json`
  - 清理过时注释（#19）：Repository 类中 "NHibernate会话" 注释
- **验证**：`grep` 确认残留已清除

### 2.6 运行时配置优化 ✅ 已完成

- **来源**：审查报告 #42, #45, #46, #48, #49, #50
- **操作**：
  - NLog（#42）：生产环境减少日志目标，建议使用 `<AsyncWrapper>` 包裹文件 target
  - `AddDbContext` → `AddDbContextPool`（#45）：评估 FlowContext 跨 scope 使用是否兼容
  - Hangfire 默认 cron（#46）：`*/30 * * * * *` → `0 */5 * * * *`
  - DateTime 统一（#48）：标记为待办，暂不批量修改（影响面大，属于架构级）
  - Kestrel 配置（#49）：添加 `KeepAliveTimeout`、`MinRequestBodyDataRate`
  - 全局 MaxRequestBodySize（#50）：对认证端点添加 `[RequestSizeLimit(1MB)]`
- **验证**：`dotnet build` + 运行时确认

### 2.7 建立 .NET 10 升级票据 ⏸ 需项目管理者建立 Issue

- **操作**：
  - 当前保留 .NET 8
  - 创建 issue/任务记录 .NET 10 升级所需的工作项（包兼容性检查、breaking changes 评估等）
  - 评估周期：每季度检查一次 .NET 10 就绪情况

### Phase 2 验收检查清单

| # | 检查项 | 通过标准 |
|---|--------|----------|
| 1 | `dotnet build` | 0 errors, ≤ 200 warnings |
| 2 | `dotnet test` | 全部通过 |
| 3 | Docker Compose 启动 | 一键 healthy |
| 4 | `/health` 端点 | 返回真实状态 |
| 5 | `.gitignore` | DataProtection-Keys/ 等已排除 |
| 6 | MES/NHibernate 残留 | 0 处 |
| 7 | 死配置 | 0 处 |
| 8 | .NET 10 升级票据 | 已建立 |

---

## Phase 3：数据层治理

> **目标**：不搬大架构，先把数据库相关风险压住。确保写入有事务、异步不阻塞、迁移可控。
>
> **验收标准**：
> - `EnsureCreatedAsync` 已替换为 Migrations
> - 关键多步写入有事务保护
> - 无 `.Wait()` / `.GetAwaiter().GetResult()` 同步阻塞
> - Repository 同步 SaveChanges 问题已标注或开始改造
> - EF Core 写入 + Dapper 只读的职责边界已明确

### 3.1 同步阻塞修复 ✅ 已完成

- **来源**：原计划 S1-1, S1-2, S1-3 + 审查报告 #4, #5, #25
- **实际规模**（代码验证）：`.Wait()` / `.GetAwaiter().GetResult()` 共 **21 处，3 个文件**（审查报告称 14 处，实际更多）
  - UnitloadService.cs: **14 处**（最大集中点）
  - RoleService.cs: 4 处（含第 99 行的 `_db.SaveChangesAsync().Wait()`）
  - LocationAllocator.cs: 3 处 + WcsController.cs: 未单独统计（含在 LocationAllocator 的异步传播链中）
- **操作**：

#### 3.1.1 AuthService 去 Task.Run + .Wait()
- `LoginAsync`、`CreateUserAsync`、`ChangePasswordAsync`、`ResetPasswordAsync`
- 移除 `Task.Run(() => { ... })` 包装
- `_db.SaveChangesAsync().Wait()` → `await _db.SaveChangesAsync()`

#### 3.1.2 RoleService 去 .Wait()
- `RoleService.cs:99`：`_db.SaveChangesAsync().Wait()` → `await _db.SaveChangesAsync()`（#4）
- 方法签名改为 async，向上传播

#### 3.1.3 RefreshTokenRepository 异步化
- 接口方法全部改为 async（`GetByTokenAsync`、`RevokeAllUserTokensAsync` 等）
- `AuthController` 调用处同步改为 await

#### 3.1.4 LocationAllocator 去 .GetAwaiter().GetResult()
- `SplitUnitload` 方法（#5 + 原计划提到处）
- **`MergeUnitload` 方法**（#5 审查补充，约第 540 行，2 处）
- `static void SplitUnitload` → `static async Task SplitUnitloadAsync`，传播到调用方

#### 3.1.5 Repository 同步 SaveChanges 评估与改造
- `Repository.cs` 的 6 个方法使用同步 `SaveChanges()`：`Add()`、`AddRange()`、`Update()`、`Delete(entity)`、`Delete(id)`、`DeleteRange()`（#25）
- **实际规模**（代码验证）：全项目 `SaveChanges()`（同步）共 **35 处，9 个文件**
  - Repository.cs: 6 处（基类，影响所有通过 Repository 写入的调用方）
  - UnitloadService.cs: 16 处
  - LocationAllocator.cs: 6 处
  - RoleService.cs: 1 处
  - PortService.cs: 2 处
  - PortController.cs、ArchivedUnitloadsController.cs、LogController.cs、LocationsController.cs: 各 1 处
- **本阶段决策**：
  - Repository 基类的 6 个同步方法是**影响面最大**的：所有通过 `_repository.Add(entity)` 的调用方都在同步阻塞
  - **建议**：Phase 3 先将 `IRepository<T,TKey>` 的增删改接口改为 async 版本（`AddAsync`、`UpdateAsync`、`DeleteAsync`），同步方法标记 `[Obsolete]`。同步改造量大但模式统一，可批量处理
  - UnitloadService 的 16 处需要逐个评估（部分可能是合理的同步场景）
- **最低要求**：至少将 Repository 基类的 6 个方法改为 async + 文档记录其余 29 处的影响范围

### 3.2 事务保护 ✅ 已完成

- **来源**：审查报告 #29
- **操作**：
  - `RoleService.SettingRoleMenus`（#29）：添加显式事务
    ```csharp
    using var tx = await _db.Database.BeginTransactionAsync();
    try { /* 多步操作 */ await tx.CommitAsync(); }
    catch { await tx.RollbackAsync(); throw; }
    ```
  - 审查其他多步写入场景（如 WcsController 的任务创建 + 日志写入），补充事务
  - 标记 `TransactionMiddleware` 不存在的问题（当前注释声称有但实际不存在）

### 3.3 EnsureCreatedAsync → Migrations ⏸ Migration 已生成，待人工审查后替换 EnsureCreatedAsync

- **来源**：原计划 P4-7
- **操作**：
  1. `dotnet ef migrations add InitialCreate --project src/Wms.Core.Infrastructure`
  2. 人工审查 Migration 脚本，确保不含破坏性 schema 变更
  3. 修改 `DbInitializer.cs`：`EnsureCreatedAsync()` → `Migrate()`
  4. Flow 表 seed 数据通过 Migration 的 `SeedData` 方法插入
- **风险**：极高 — Migration 脚本错误可致数据丢失。**必须人工审查**

### 3.4 SQL 注入风险修复 ✅ 已完成

- **来源**：审查报告 #31
- **操作**：
  - `FlowTemplateSeeder.cs:94`：`ExecuteSqlRawAsync($"...{s.Id}")` → `ExecuteSqlInterpolatedAsync($"DELETE FROM FlowNodes WHERE TemplateId = {s.Id}")`
- **验证**：`grep "ExecuteSqlRawAsync.*\$\""` 结果为零

### 3.5 EF Core / Dapper 职责明确 ✅ 已确认（Dapper 服务仅用于只读）

- **操作**：
  - 文档明确：EF Core 负责所有写入操作 + 一般查询，Dapper 负责高频只读（`IDapperReadService`）
  - 检查 `IDapperReadService` 的使用场景，确认只用于只读
  - 检查 Repository 中是否存在不合理的 Raw SQL 写入，评估是否需要迁移到 EF Core

### 3.6 补充关键 Application Service（数据层视角） ⏸ 推迟至 Phase 5 架构重构

- **来源**：审查报告 #12
- **操作**：
  - `UsersController.cs` 直接注入 `WmsDbContext` + `ExecuteSqlRaw`（高风险）
  - 创建 `UserManagementApplicationService` 接管用户管理逻辑
  - 本阶段只做数据访问路径的安全封装，不做完整的应用层重构（Phase 5）

### Phase 3 验收检查清单

| # | 检查项 | 通过标准 |
|---|--------|----------|
| 1 | `.Wait()` / `.GetAwaiter().GetResult()` | 0 处 |
| 2 | 同步 `SaveChanges` | 已评估并文档化 |
| 3 | 关键多步写入 | 有事务保护 |
| 4 | 数据库初始化 | 使用 Migrations，非 EnsureCreated |
| 5 | SQL 注入风险 | `ExecuteSqlRawAsync` + 字符串插值 = 0 |
| 6 | `dotnet test` | 全部通过（Mock 需更新） |
| 7 | EF/Dapper 职责 | 文档明确 |

---

## Phase 4：认证与安全升级

> **目标**：系统化升级认证与安全机制。密码算法升级、Token 安全强化、防护措施补齐。
>
> **验收标准**：
> - 密码哈希使用 BCrypt（兼容旧 HMAC）
> - RefreshToken 仅存储 hash
> - 登录有暴力破解防护
> - JWT/RefreshToken 生命周期规范化
> - DataProtection 密钥外部持久化，项目目录不含密钥文件
> - 安全响应头已配置
> - 上传文件目录有访问控制

### 4.1 密码哈希迁移：HMACSHA256 → BCrypt ✅ 已完成

- **来源**：原计划 P4-4 + 审查报告 #9
- **现状**：项目已有 `IPasswordHasher` 接口 + `BcryptPasswordHasher` 实现，但未注册到 DI，`User.cs` 直接内联 HMACSHA256
- **操作**：
  1. DI 注册：`services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>()`
  2. 修改 `User.cs` `ValidatePassword()`：BCrypt hash 以 `$2` 开头 → `BCrypt.Verify()`，否则旧 HMAC 验证
  3. `SetPassword()`：改用 `BCrypt.HashPassword()`
  4. **设计问题**（#9）：`SetPassword()` 如果注入 `IPasswordHasher` 会引入 DI 到领域实体。**推荐**：密码验证逻辑放在 `AuthService.LoginAsync()` 中处理，`User` 实体只存 hash 值
  5. 登录时检测旧 hash，自动升级为 BCrypt（rehash on verify）
- **分步执行**：
  - 第一步：仅部署双格式验证（BCrypt + HMAC），不自动升级
  - 第二步：验证稳定后开启自动 rehash
- **验证**：旧密码用户可登录；新用户密码存储为 BCrypt 格式

### 4.2 RefreshToken 安全存储 ✅ 已完成

- **来源**：审查报告 #40
- **操作**：
  - `RefreshToken.cs`：`Token` 字段改为存储 token 的 SHA256 hash
  - 验证时：对传入 token 做 hash 后比对
  - 迁移：现有明文 token 需要一次性脚本转换为 hash

### 4.3 登录暴力破解防护 ✅ 已完成

- **来源**：审查报告 #34
- **操作**：
  - 使用 `AspNetCoreRateLimit` 或自实现中间件
  - 策略：同一 IP 5 次失败后锁定 15 分钟；同一账号 10 次失败后锁定 30 分钟
  - 计数器使用 Redis（多实例兼容）或内存缓存
  - 锁定状态通过 `ICacheService` 或 `IDistributedCache` 存储
- **验证**：连续失败登录后返回 429

### 4.4 JWT/RefreshToken 生命周期规范化 ✅ 已确认（JWT 60min, Refresh 7天, TokenService 已有失效逻辑）

- **操作**：
  - 文档明确当前 Token 配置（过期时间、Issuer、Audience）
  - 确认 RefreshToken 过期时间合理（建议 7 天）
  - 确认 JWT 过期时间合理（建议 15-30 分钟）
  - 确保 Token 刷新时旧 Token 立即失效

### 4.5 DataProtection 密钥治理 ✅ 已确认（密钥未被 Git 跟踪，.gitignore 已排除）

- **来源**：审查报告 #15, #23
- **现状确认**（代码验证）：
  - `DataProtection-Keys/` 目录存在于项目目录下，包含 2 个 XML 密钥文件
  - **`git ls-files -- "*DataProtection*"` 返回空** — 密钥文件**未被 Git 跟踪**
  - 审查报告 #15 的"已提交到 Git（极高安全）"结论**不准确**，实际风险等级为"中"
  - `.gitignore` 中可能已有排除规则
- **操作**：
  1. 确认 `.gitignore` 包含 `DataProtection-Keys/`（Phase 2.5 中已补充）
  2. Docker 部署：通过 Volume 挂载持久化密钥目录（不放在容器内）
  3. 多实例场景：密钥目录需要共享 Volume 或 Redis 密钥存储（#23）
  4. **无需** BFG/filter-repo 清理 Git 历史（文件从未被跟踪）
- **最低要求**：密钥文件不在 Git 历史中 + `.gitignore` 排除

### 4.6 安全响应头配置 ✅ 已完成

- **来源**：审查报告 #47
- **操作**：在中间件管道中添加：
  - `X-Content-Type-Options: nosniff`
  - `X-Frame-Options: DENY`
  - `Strict-Transport-Security`（HSTS）
  - `Content-Security-Policy`（基础策略）
- **方式**：自定义中间件 `app.UseSecurityHeaders()` 或使用 `NetEscapades.AspNetCore.SecurityHeaders` 包
- **验证**：`curl -I` 确认响应头存在

### 4.7 日志安全加固 ✅ 已完成

- **来源**：审查报告 #41, #38
- **操作**：
  - NLog JSON target：`includeAllProperties="true"` → `includeAllProperties="false"`（#41）
  - 日志最低级别：确认 Production 环境为 `Information`（非 `Trace`/`Debug`）（#38）

### 4.8 上传文件访问控制 ✅ 已完成

- **来源**：审查报告 #27
- **操作**：
  - `Program.cs:526-530`：`/uploads` 静态文件服务需加鉴权
  - 方案：Excel 存档目录（`uploads/excel/`）从静态文件服务中排除
  - 或添加中间件保护 `/uploads/excel/` 路径
- **验证**：直接访问 `/uploads/excel/xxx.xlsx` 返回 401/403

### 4.9 输入验证恢复 ✅ 已完成

- **来源**：审查报告 #39, #35
- **操作**：
  - `ChangePasswordRequest.OldPassword` 恢复 `[Required]`（#39）
  - 检查其他 DTO 的验证属性是否完整（#35）
- **验证**：发送缺少必填字段的请求返回 400

### 4.10 AuthController 路由统一 ⏸ 需协调前端更新

- **来源**：审查报告 #51
- **操作**：`AuthController` 路由从 `[Route("api/[controller]")]` → `api/v{version:apiVersion}/[controller]`
- **注意**：此变更影响前端/客户端调用地址，需协调更新
- **评估**：如果前端暂不能改，可标记为待办

### Phase 4 验收检查清单

| # | 检查项 | 通过标准 |
|---|--------|----------|
| 1 | 密码哈希 | BCrypt 格式（兼容旧 HMAC） |
| 2 | RefreshToken 存储 | 仅 hash，无明文 |
| 3 | 暴力破解防护 | 连续失败后返回 429 |
| 4 | JWT 生命周期 | Access Token ≤ 30min, Refresh ≤ 7d |
| 5 | DataProtection 密钥 | 不在 Git 中，有持久化方案 |
| 6 | 安全响应头 | X-Content-Type-Options 等存在 |
| 7 | 日志安全 | JSON target 不含敏感属性 |
| 8 | 上传文件保护 | Excel 路径需鉴权 |
| 9 | DTO 验证 | 必填字段有 [Required] |
| 10 | `dotnet test` | 全部通过 |

---

## Phase 5：架构重构

> **目标**：等安全、测试、部署都稳了，再处理架构层面的问题。Controller 变薄、Application 层补齐、Domain 纯净、返回模型统一。
>
> **前置条件**：Phase 1-4 全部验收通过
>
> **验收标准**：
> - Domain 层零外部依赖（无 EF Core、无 SqlClient）
> - Controller 不直接注入 WmsDbContext
> - Application 层有主要业务模块的 Service
> - 返回模型统一为 `Result<T>`
> - FluentValidation 对关键接口生效
> - Redis 支撑多实例缓存、限流、WCS 去重

### 5.1 UseForwardedHeaders 中间件

- **来源**：审查报告 #8 + Phase 1 中 WCS 白名单的反向代理需求
- **操作**：
  - `Program.cs` 中间件管道最前面添加 `app.UseForwardedHeaders()`
  - 配置 `ForwardedHeadersOptions`（`ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto`）
  - 配置 `KnownNetworks` / `KnownProxies` 按实际部署环境设置

### 5.2 Domain 去除所有基础设施依赖

- **来源**：原计划 P1-1 + 审查报告 #2
- **前置**：Phase 3 已完成异步化，Domain 层不再直接使用 EF 方法
- **根因**（代码验证）：`IRepository.cs:2` 的 `using Microsoft.EntityFrameworkCore.Query` 引用 `SetPropertyCalls<T>` 类型，仅用于 `BulkUpdateAsync` 和 `BulkDeleteAsync` 两个方法
- **架构决策**：`IRepository<T,TKey>` 中的 `IQueryable<T>` 返回类型可**保留在 Domain**（它是查询契约，不是 EF 依赖）。真正需要移除的只有 `BulkUpdateAsync`/`BulkDeleteAsync`
- **操作**：
  - `IRepository<T,TKey>` 的 `BulkUpdateAsync`/`BulkDeleteAsync` 移到 Infrastructure 的 `IBulkRepository<T,TKey>`
  - 从 `Domain.csproj` 移除：`Microsoft.EntityFrameworkCore`、`Microsoft.EntityFrameworkCore.Relational`、`Microsoft.Data.SqlClient`、`System.Configuration.ConfigurationManager`
  - 同时移除 `Domain.csproj` 中的空 `<Folder Include="Helpers\" />` 和 `<Folder Include="Models\" />`
  - 确认实体上的 `[Table]`/`[Column]`/`[NotMapped]` 来自 `System.ComponentModel.DataAnnotations.Schema`（.NET 基础库），无需 EF 包
- **验证**：`dotnet build src/Wms.Core.Domain/` 通过

### 5.3 接口迁移 Domain → Application

- **来源**：原计划 P1-3 + 审查报告 #1（矛盾修复）, #3（计数修正）
- **现状**（代码验证）：Application 层目前**几乎为空** — 仅有 8 个 DTO、`IExcelService`/`ExcelService`、2 个 `BackgroundJob`、`IWcsRequestHandler` 接口。迁移后 Application 层将从 ~14 个文件增加到 ~32 个文件（+18 接口文件）
- **操作**：
  - 从 `Domain/Services/` 迁移到 `Application/Ports/`（11 个接口，不含 `IPasswordHasher` 和 `IContainerCodeValidator`）
    - `IAuthService`、`ILocationService`、`IUnitloadService`
    - `IBasicDictionaryService`、`IBatteryCellService`、`IBatteryCellSortingService`
    - `ICacheService`、`IOutboundTimerService`、`IPortService`
    - `IRoleService`、`ITranslationService`
  - 从 `Domain/Interfaces/` 迁移到 `Application/Ports/`（7 个接口）
    - `IWcsClient`、`IWcsTaskBridge`、`ICtaskDbService`
    - `IDistributedLockService`、`IInventoryCacheService`
    - `IDapperReadService`、`ITaskCompletionHandler`
  - **保留在 Domain**（5 个）：`IAuditable`、`IPasswordHasher`、`IContainerCodeValidator`、`IRepository<T,TKey>`、`IUserRepository`、`IRefreshTokenRepository`
  - namespace：`Wms.Core.Domain.Services` → `Wms.Core.Application.Ports`
  - 更新 ~40 个文件的 `using` 语句
- **验证**：`dotnet build --no-restore` 通过

### 5.4 Program.cs 拆分

- **来源**：原计划 P2-1
- **操作**：
  - 新建 `Extensions/AuthenticationExtensions.cs` — JWT + CORS
  - 新建 `Extensions/HangfireExtensions.cs` — Hangfire 注册 + Dashboard
  - 新建 `Extensions/WcsExtensions.cs` — WCS 客户端 + Bridge + Handler
  - 新建 `Extensions/RedisExtensions.cs` — Redis/缓存/SignalR
  - 新建 `Extensions/HealthCheckExtensions.cs` — 健康检查
  - 新建 `Extensions/MiddlewareExtensions.cs` — 中间件管道
  - Swagger 注册包裹在 `if (builder.Environment.IsDevelopment())` 中（#28）
- **目标**：`Program.cs` 从 ~620 行 → ~60 行
- **验证**：中间件注册顺序与原 Program.cs 一致

### 5.5 Application 层补充业务服务

- **来源**：原计划 P2-2 + 审查报告 #12
- **现状**（代码验证）：**13 个 Controller 直接注入 `WmsDbContext`**（审查报告称"32 个文件引用"，实际 Controller 层为 13 个）：
  - AuthController、WcsController、UsersController
  - UnitloadsController、UnitloadItemDetailsController、ArchivedUnitloadsController
  - TransTasksController、ArchivedTasksController
  - PortController、LocationsController、LogController
  - SimToolController、FlowController
- **豁免**（可保留 WmsDbContext）：Program.cs（DI 注册）、DbInitializer（启动初始化）、BackgroundJobService、JobDispatcher、FlowTemplateSeeder、WmsHealthCheck、OperationLogFilter、WcsTaskSyncService、LogCleanupService
- **操作**：
  - `WcsRequestApplicationService` — 从 `WcsController.WcsRequest()` (200+ 行) 下沉
  - `AuthApplicationService` — 从 `AuthController.Login()` / `Refresh()` 下沉
  - `UserManagementApplicationService` — 从 `UsersController` 下沉（#12）
  - Controller 只做参数绑定 + 调用 ApplicationService + 返回 `Result<T>`
- **注意**：
  - FlowContext 直接持有 `WmsDbContext`（违反分层），本阶段暂不处理（需改 15+ 个 NodeHandler）
  - Application Service 通过 Repository 接口操作数据，不直接接触 DbContext

### 5.6 统一返回格式

- **来源**：原计划 P2-3
- **操作**：
  - `Result<T>` 作为标准响应（已有）
  - `WcsResult` 保留（WCS 设备接口需固定格式）
  - `AuthController`：`Ok(response)` / `StatusCode(...)` → `Result<T>`
  - `GlobalExceptionHandler`：返回 `Result.Fail(errorCode)`
- **验证**：所有 API 返回 `Result<T>` 或 `WcsResult`

### 5.7 FluentValidation 落地

- **来源**：原计划 P4-3
- **操作**：
  - `LoginRequestValidator`
  - `ChangePasswordRequestValidator`
  - `WcsRequestValidator`
  - 注册：`services.AddValidatorsFromAssembly(...)`
  - 与 ASP.NET Core 的 `[ApiController]` 自动验证集成
- **验证**：发送无效参数返回 400 + 具体验证错误

### 5.8 Task.Run → Channel 队列

- **来源**：原计划 P4-5
- **操作**：
  - 新建 `BackgroundTaskQueue.cs`（`Channel<Func<Task>>` 实现）
  - 新建 `BackgroundTaskQueueHostedService.cs`
  - 优先替换 FlowEngineService 的 2 处（审计数据不可靠风险高）
  - 再替换 WcsController（5 处）、DatabaseWcsTaskBridge（1 处）、OperationLogFilter（1 处）
- **验证**：fire-and-forget 操作改为有序队列处理

### 5.9 Redis 多实例支撑

- **来源**：原计划 P3-1 ~ P3-4 + 审查报告 #33, #22
- **操作**：
  - `RedisExtensions`：Redis 启用时切换 `ICacheService` 为 `DistributedCacheService`
  - SignalR Redis Backplane
  - 速率限制：`AspNetCoreRateLimit` + Redis（替代自实现 `RateLimitMiddleware`）— 注意版本兼容性（#22）
  - WCS 重复请求检测迁移到 Redis `SETNX`（#33）— 替换 `ConcurrentDictionary`
  - `DatabaseWcsTaskBridge` 加分布式锁（P3-3）
  - `LogCleanupService` 多实例协调（P3-4）
  - `FlowEngineService` 模板缓存通过 Redis pub/sub 通知失效
  - WCS 速率限制白名单配置（#26）

### 5.10 状态机统一建模

- **来源**：原计划提及
- **操作**：
  - `TransTask` 状态流转（创建→分配→执行→完成）统一建模
  - 使用状态模式或显式状态机，替代散落的 `if/else` 状态判断
  - 关键路径补充单元测试

### Phase 5 验收检查清单

| # | 检查项 | 通过标准 |
|---|--------|----------|
| 1 | Domain.csproj 外部包 | 无 EF Core、无 SqlClient |
| 2 | Controller 直接注入 WmsDbContext | 0 处（豁免：Program.cs、DbInitializer） |
| 3 | Application Service | 覆盖主要业务模块 |
| 4 | 返回格式 | 统一 `Result<T>` / `WcsResult` |
| 5 | FluentValidation | 关键接口有 Validator |
| 6 | Program.cs 行数 | ≤ 100 行 |
| 7 | Task.Run | 0 处 |
| 8 | Redis 多实例 | 缓存/限流/去重/锁 均可用 |
| 9 | WCS 重复请求检测 | Redis SETNX |
| 10 | `dotnet test` | 全部通过 |
| 11 | Docker 多实例部署 | `--scale api=2` 正常工作 |

---

## 依赖关系图

```
Phase 1: 生产封口           ← 立即开始，无前置依赖
  ↓
Phase 2: 运行基线            ← 依赖 Phase 1（Docker 配置已修复）
  ↓
Phase 3: 数据层治理           ← 依赖 Phase 2（测试可运行，可验证数据层改动）
  ↓
Phase 4: 认证与安全升级        ← 依赖 Phase 3（数据层稳定后再改认证机制）
  ↓
Phase 5: 架构重构             ← 依赖 Phase 1-4（安全、测试、部署都稳了再动架构）
```

**注意**：Phase 3 中的部分同步阻塞修复（3.1）与 Phase 2 的测试修复（2.1）存在交叉。实际执行时可以先做 3.1 的修复，再在 2.1 中验证测试通过。Phase 3 和 Phase 2 的部分工作可并行推进。

---

## 风险矩阵（高亮需要额外关注的项）

| 阶段 | 风险项 | 影响 | 缓解措施 |
|------|--------|------|----------|
| Phase 1 | WCS IP 白名单配置遗漏 | **生产线停工** | 部署前在测试环境验证，回滚方案：注释 `[InternalIpWhitelist]` |
| Phase 1 | **130 处** `Result.Fail(ex.Message)` 替换 | 前端无法判断错误原因，且替换量大 | 建议用脚本批量替换 + 人工复查关键接口。增加 `errorCode` 字段 |
| Phase 1 | GlobalExceptionHandler 本身泄露 | 第 57 行 `Message = exception.Message` 仍通过响应返回 | Phase 1.5 第 5 步统一处理 |
| Phase 3 | Repository 基类 6 个同步方法 async 化 | 影响所有通过 Repository 写入的调用方 | 先改接口，`[Obsolete]` 标记旧方法，逐步迁移 |
| Phase 3 | UnitloadService 14 处 `.Wait()` + 16 处同步 SaveChanges | 改造量集中，业务逻辑复杂 | 分批处理，每批编译+测试 |
| Phase 3 | Migrations 脚本错误 | **数据丢失** | 人工审查每个 Migration，不含破坏性变更 |
| Phase 3 | `SplitUnitload` async 传播 | 调用方遗漏 `await` → 数据丢失 | 搜索所有调用点，逐一确认 |
| Phase 4 | BCrypt rehash 逻辑错误 | **所有用户登录失败** | 分两步部署：先双格式验证，再开启 rehash |
| Phase 5 | 接口迁移 namespace 变更（18 个接口） | DI 注册失败 → 启动报错 | 分批迁移，每批 `dotnet build` 验证 |
| Phase 5 | 13 个 Controller 去 WmsDbContext | 业务逻辑迁移时遗漏分支 → 功能回归 | 逐 Controller 迁移，每迁移一个编译+手工测试 |

---

## 各阶段预估工作量（基于代码验证修正）

| 阶段 | 涉及文件数 | 新建文件 | 核心复杂度 | 关键数据 |
|------|-----------|---------|-----------|---------|
| Phase 1 生产封口 | **~30** | ~4 | 低-中 | 130 处 ex.Message 替换是最大工作量 |
| Phase 2 运行基线 | ~15 | 0 | 低 | 清理 + 配置 + 测试修复 |
| Phase 3 数据层治理 | **~15** | ~3 | 中-高 | 21 处 .Wait() + 35 处同步 SaveChanges |
| Phase 4 认证与安全升级 | ~15 | ~5 | 中 | 密码迁移需要双格式兼容 |
| Phase 5 架构重构 | **~55** | ~18 | 高 | 18 个接口迁移 + 13 个 Controller 改造 + 多实例 |

## 架构现状快照（代码验证数据）

```
项目引用关系：
  WebApi → Application, Domain, Infrastructure
  Infrastructure → Application, Domain
  Application → Domain（仅此一个）

Domain.csproj 外部包（需在 Phase 5 移除）：
  Microsoft.EntityFrameworkCore 8.0.11
  Microsoft.EntityFrameworkCore.Relational 8.0.11
  Microsoft.Data.SqlClient 5.2.2
  System.Configuration.ConfigurationManager 8.0.1

Application 层现状（Phase 5 前几乎为空）：
  DTOs: 8 个文件
  Services: IExcelService, ExcelService
  Jobs: StockReconciliationJob, TimeoutOrderCleanupJob
  Handlers: IWcsRequestHandler

Controller 直接注入 WmsDbContext：13 个
  AuthController, WcsController, UsersController, UnitloadsController,
  UnitloadItemDetailsController, ArchivedUnitloadsController,
  TransTasksController, ArchivedTasksController, PortController,
  LocationsController, LogController, SimToolController, FlowController

密码体系：
  Domain: IPasswordHasher 接口（已有，未注册 DI）
  Infrastructure: BcryptPasswordHasher 实现（已有，未注册 DI）
  User.cs: 内联 HMACSHA256（SetPassword + ValidatePassword，需迁移）

关键代码热点：
  Result.Fail(ex.Message): ~130 处 / 26 文件
  throw new Exception(): 21 处 / 3 文件
  .Wait()/.GetAwaiter(): 21 处 / 3 文件
  同步 SaveChanges(): 35 处 / 9 文件（Repository 基类 6 处）
```
