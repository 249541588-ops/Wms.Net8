# WMS 智能仓储系统 — 架构优化实施计划

> **文档日期：** 2026-05-31
> **适用项目：** Wms.Core (.NET 8 + EF Core + SQL Server)
> **配套文档：** [技术选型推荐方案](WMS架构优化-最终推荐方案.md) | [市场验证审查报告](WMS架构优化方案-市场验证审查报告.md)

---

## 一、项目概况

### 当前状态
- 4 层 DDD 架构（Domain / Infrastructure / Application / WebApi）
- .NET 8 + EF Core + SQL Server + Redis
- 66 张数据库表（11 Identity + 55 WMS），实体和映射已完成
- 用户/权限/角色模块完整实现（JWT 双令牌）
- WMS 业务模块仅有数据模型，无业务代码

### 目标状态
- 安全加固：BCrypt 密码哈希、SQL 注入修复、并发控制、输入验证
- 架构升级：Mapperly 映射、Polly 弹性韧性、Dapper 高频读取、Hangfire 调度、SignalR 实时通信
- WMS 业务：库位分配、入库/出库/拣货/波次流程、Redis 缓存、WCS 集成
- 运维工具：Docker 容器化、Serilog+Seq 日志、Scalar 文档

---

## 二、实施批次总览

```
第一批：安全加固     ████████░░░░░░░░░░░░░░  4 项  — 最高优先级
第二批：架构工具     ████████████░░░░░░░░░  6 项  — 高优先级
第三批：WMS 业务     ████████████████░░░░░  4 项  — 高优先级
第四批：运维工具     ████████████████████░░  4 项  — 中优先级
第五批：长期规划     ░░░░░░░░░░░░░░░░░░░░░  5 项  — 低优先级
```

---

## 三、第一批：安全加固（最高优先级）

### 任务 1：BCrypt.Net-Next 替换密码哈希

**方案编号：** #1

**当前问题：** `TokenService.cs` 使用自实现 HMACSHA256 哈希密码

**涉及文件：**
- `src/Wms.Core.Application/Services/TokenService.cs`
- `src/Wms.Core.WebApi/Controllers/AuthController.cs`

**NuGet 安装：**
```bash
dotnet add src/Wms.Core.Infrastructure/BCrypt.Net-Next
```

**代码变更要点：**
1. `TokenService` 中的 `HashPassword()` 方法替换为 `BCrypt.Net.BCrypt.HashPassword(password)`
2. `VerifyPassword()` 方法替换为 `BCrypt.Net.BCrypt.Verify(password, hash)`
3. 移除旧的 `HMACSHA256` 相关代码
4. 需要数据迁移策略：旧密码用户下次登录时重新哈希（transparent rehash）

**验证：** 注册新用户 → 登录验证 → 确认密码哈希格式为 `$2a$11$...`

---

### 任务 2：SQL 注入修复 ✅ 已完成

**方案编号：** #3

#### 漏洞清单（代码审查确认）

| 等级 | 漏洞 | 文件:行号 | 说明 |
|:---:|------|----------|------|
| **高** | `orderBy` 字符串拼接 | `Repository.cs:190` | `ORDER BY {orderBy}` 直接拼接，`IRepository.cs:107` 接口公开 `orderBy` 参数 |
| **中** | `ExecuteSqlRaw` 格式化参数 | `UsersController.cs:236,237,274` | 使用 `"...{0}..."` 格式化，应改用 `SqlParameter` |
| **中** | `ExecuteSqlRaw` 格式化参数 | `AuthService.cs:112-114` | 同上 |

#### 已安全项（无需修复）

| 项 | 文件 | 原因 |
|------|------|------|
| 搜索过滤 | `UsersController.cs:88`、`RoleController.cs:74`、`BasicDictionaryController.cs:69`、`MenusController.cs:228`、`LogController.cs:52-54`、`Sys_LanguageController.cs:90-93` | EF Core LINQ `.Contains(keyword)` 自动参数化 |
| Repository.ExecuteSql | `Repository.cs:156-159` | 使用 `SqlParameter` 参数化 |
| MenusController 原生 SQL | `MenusController.cs:59-86,129-142` | 使用 `@roleName` 参数化 |

#### 涉及文件

- `src/Wms.Core.Infrastructure/Persistence/Repositories/Repository.cs`（第 165-199 行）
- `src/Wms.Core.Domain/Repositories/IRepository.cs`（第 91-107 行）
- `src/Wms.Core.WebApi/Controllers/UsersController.cs`（第 236、237、274 行）
- `src/Wms.Core.Infrastructure/Services/AuthService.cs`（第 112-114 行）

#### 修复方案

**修复 1：Repository.ExecuteSqlPaged — orderBy 白名单校验**

在 `Repository.cs` 中添加 `SanitizeOrderBy` 方法，校验列名仅含字母/数字/下划线，排序方向仅允许 ASC/DESC：

```csharp
private static readonly HashSet<string> AllowedDirections = new(StringComparer.OrdinalIgnoreCase) { "ASC", "DESC" };
private static readonly Regex SafeColumnRegex = new(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)?$", RegexOptions.Compiled);

private static string SanitizeOrderBy(string orderBy, string fallback = "Id ASC")
{
    // 拆分多个排序子句（逗号分隔）
    // 每个子句校验：列名匹配正则 + 方向在白名单内
    // 不合法子句丢弃，全部不合法则返回 fallback
}
```

调用处修改（`Repository.cs:188-193`）：
```csharp
var safeOrderBy = SanitizeOrderBy(orderBy);
var pagedSql = $@"{sql} ORDER BY {safeOrderBy} OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY";
```

**修复 2：UsersController — ExecuteSqlRaw 改用 SqlParameter**（第 236、237、274 行）

```csharp
// 修改前
_dbContext.Database.ExecuteSqlRaw("DELETE FROM UserRoles WHERE UserId = {0}", id);
_dbContext.Database.ExecuteSqlRaw("INSERT INTO UserRoles (UserId, RoleId) VALUES ({0}, {1})", id, roleEntity.Id);

// 修改后
_dbContext.Database.ExecuteSqlRaw("DELETE FROM UserRoles WHERE UserId = @userId",
    new Microsoft.Data.SqlClient.SqlParameter("@userId", id));
_dbContext.Database.ExecuteSqlRaw("INSERT INTO UserRoles (UserId, RoleId) VALUES (@userId, @roleId)",
    new Microsoft.Data.SqlClient.SqlParameter("@userId", id),
    new Microsoft.Data.SqlClient.SqlParameter("@roleId", roleEntity.Id));
```

**修复 3：AuthService — ExecuteSqlRaw 改用 SqlParameter**（第 112-114 行）

```csharp
// 修改前
_db.Database.ExecuteSqlRaw("INSERT INTO UserRoles (UserId, RoleId) VALUES ({0}, {1})", savedUser.Id, roleEntity.Id);

// 修改后
_db.Database.ExecuteSqlRaw("INSERT INTO UserRoles (UserId, RoleId) VALUES (@userId, @roleId)",
    new Microsoft.Data.SqlClient.SqlParameter("@userId", savedUser.Id),
    new Microsoft.Data.SqlClient.SqlParameter("@roleId", roleEntity.Id));
```

#### 验证

1. 单元测试：`SanitizeOrderBy("1; DROP TABLE Users")` → 返回 `"Id ASC"`（被过滤）
2. 单元测试：`SanitizeOrderBy("Name DESC, Id ASC")` → 返回 `"Name DESC, Id ASC"`（正常通过）
3. SQL Profiler：确认所有查询使用 `@param` 参数化，无字符串拼接
4. 接口测试：分页接口传入非法 orderBy → 确认使用默认排序，无异常

---

### 任务 3：库存并发控制（RowVersion）

**方案编号：** #2

**当前问题：** `Stock` 等库存实体无并发控制

**涉及文件：**
- `src/Wms.Core.Domain/Entities/Material/Stock.cs`
- `src/Wms.Core.Infrastructure/Persistence/Configurations/StockConfiguration.cs`
- 后续创建的库存操作 Service

**NuGet 安装：** 无（EF Core 内置）

**代码变更要点：**
1. `Stock` 实体添加 `byte[]? RowVersion { get; set; }` 属性 + `[Timestamp]` 注解
2. `StockConfiguration` 添加 `.IsRowVersion()` 配置
3. 生成 EF Migration：`Add-Migration AddStockRowVersion`
4. 库存写入操作捕获 `DbUpdateConcurrencyException`，返回 409 Conflict
5. Redis 分布式锁：使用 `StackExchange.Redis` 的 `IDatabase.LockTakeAsync()`

**验证：** 并发更新同一库存 → 确认后更新者收到 409 错误

---

### 任务 4：FluentValidation 输入验证

**方案编号：** #7

**当前问题：** 项目安装了 FluentValidation 但未注册使用，Validators 文件夹为空

**涉及文件：**
- `src/Wms.Core.Application/Validators/` （新建验证器）
- `src/Wms.Core.WebApi/Program.cs`

**NuGet 安装：**
```bash
dotnet add src/Wms.Core.Application/FluentValidation.AspNetCore
```

**代码变更要点：**
1. Program.cs 注册：`builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly())`
2. 为关键 DTO 创建验证器：
   - `CreateUserRequestValidator`
   - `LoginRequestValidator`
   - 入库/出库订单 DTO 验证器（后续 WMS 业务）
3. Program.cs 添加自动验证管道（或 ActionFilter）

**验证：** 发送缺少必填字段的请求 → 确认返回 400 + 验证错误详情

---

## 四、第二批：架构工具（高优先级）

### 任务 5：Mapperly 对象映射 ✅ 已完成

**方案编号：** #6

#### 现状分析

项目当前无任何对象映射库（无 AutoMapper / Mapster / Mapperly），所有映射均为手动赋值。经代码审查，确认以下手动映射场景：

| 场景 | 文件:行号 | 映射方向 | 说明 |
|------|----------|---------|------|
| 菜单 → RoleMenuDTOs | `MenusController.cs:151-161` | Entity → DTO | 6 个属性手动赋值（父菜单） |
| 菜单 → RoleMenuDTOs | `MenusController.cs:176-186` | Entity → DTO | 6 个属性手动赋值（子菜单） |
| Excel行 → MaterialImportDto | `ExcelService.cs:65-74` | DataRow → DTO | 6 个属性手动赋值（含列名兼容逻辑） |
| 审计日志 → 匿名对象 | `LogController.cs:72-84` | Entity → dynamic | 10 个属性 Select 投影 |
| 字典 → 匿名对象 | `BasicDictionaryController.cs:151` | Entity → dynamic | Select 投影 |
| 角色功能 → 匿名对象 | `RoleController.cs:411` | Entity → dynamic | GroupBy + Select 投影 |

**不适合用 Mapperly 的场景（保留原写法）：**
- `ExcelService.cs:65-74`：从 DataRow 读取需要列名兼容逻辑（中文/英文/驼峰），Mapperly 无法处理
- `LogController.cs:72-84`、`BasicDictionaryController.cs:151`、`RoleController.cs:411`：LINQ `.Select(x => new {...})` 投影是 EF Core 查询优化的一部分，改为 Mapperly 会导致先查全字段再映射，性能下降
- `UsersController.cs:170-178`：调用 `_authService.CreateUserAsync()` 方法参数传递，非 DTO 映射

**实际可替换的场景：** `MenusController.cs` 中 2 处 `Menus → RoleMenuDTOs` 手动赋值（共约 30 行可简化为 2 行 Mapperly 调用）

#### NuGet 安装

```bash
dotnet add src/Wms.Core.Application/Riok.Mapperly
```

> 注：只需安装 `Riok.Mapperly` 一个包，它已包含 Abstractions

#### 涉及文件

| 操作 | 文件 |
|------|------|
| 新建 | `src/Wms.Core.Application/Mappers/WmsMapper.cs` |
| 修改 | `src/Wms.Core.WebApi/Controllers/MenusController.cs`（第 144-200 行） |
| 新建 | `src/Wms.Core.Application/DTOs/MenuDtos.cs`（菜单响应 DTO，与请求 DTO 分离） |
| 修改 | `src/Wms.Core.Application/Wms.Core.Application.csproj`（Mapperly NuGet 引用） |
| 修改 | `src/Wms.Core.WebApi/Program.cs`（DI 注册 Mapper） |

#### 代码变更要点

**步骤 1：创建 Mapper**

```csharp
// src/Wms.Core.Application/Mappers/WmsMapper.cs
using Riok.Mapperly.Abstractions;
using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Entities;

namespace Wms.Core.Application.Mappers;

[Mapper]
public static partial class WmsMapper
{
    // Menus → MenuResponseDto（平铺映射，忽略 FunctionButton）
    public static partial MenuResponseDto ToDto(this Menus menu);
}
```

**步骤 2：定义响应 DTO**

```csharp
// src/Wms.Core.Application/DTOs/MenuDtos.cs
namespace Wms.Core.Application.DTOs;

/// <summary>
/// 菜单响应 DTO（用于角色菜单查询）
/// </summary>
public record MenuResponseDto
{
    public int Id { get; init; }
    public int ParentId { get; init; }
    public int Sort { get; init; }
    public string? Name { get; init; }
    public string? EnglishName { get; init; }
    public string? GermanName { get; init; }
    public string? Url { get; init; }
    public string? ImgUrl { get; init; }
    public string FunBtns { get; init; } = string.Empty;
    public List<MenuResponseDto> Child { get; init; } = new();
}
```

**步骤 3：注册 DI**（`Program.cs`）

```csharp
// Mapperly 生成的 Mapper 是静态类，无需 DI 注册
// 但如需实例方法版本，可注册：
builder.Services.AddSingleton<WmsMapper>(WmsMapper.Instance);
```

**步骤 4：替换 MenusController 手动映射**

```csharp
// 修改前（MenusController.cs:151-161，约 10 行）
RoleMenuDTOs menuDTOs = new RoleMenuDTOs()
{
    Id = pMenu.Id,
    Name = pMenu.Name,
    EnglishName = pMenu.EnglishName,
    GermanName = pMenu.GermanName,
    Url = pMenu.Url,
    ImgUrl = pMenu.ImgUrl,
    ParentId = pMenu.ParentId,
    Sort = pMenu.Sort,
};

// 修改后（1 行）
var menuDto = pMenu.ToDto();
```

**步骤 5：后续 WMS 业务统一规范**

后续新建的 WMS 业务 DTO 遵循以下规范：
- 请求 DTO（`XxxRequest`）：record 类型，`init` 属性，放在 `DTOs/` 目录
- 响应 DTO（`XxxResponse`）：record 类型，`init` 属性，与 Entity 分离
- 映射统一使用 `WmsMapper`，通过 `[Mapper]` partial 方法扩展
- 命名约定：`entity.ToDto()` / `mapper.ToEntity(request)`

#### Mapperly 使用规范

| 场景 | 用 Mapperly | 不用 Mapperly |
|------|:-:|:-:|
| Entity → ResponseDto | ✅ | |
| RequestDto → Entity | ✅ | |
| List\<Entity\> → List\<Dto\> | ✅（自动支持） | |
| Entity → 匿名 Select 投影 | | ✅（EF Core 查询优化） |
| DataRow / Excel → DTO | | ✅（含业务逻辑） |
| 属性名不一致映射 | ✅（`[MapProperty(Source, Target)]`） | |
| 嵌套对象映射 | ✅（自动递归） | |
| 自定义类型转换 | ✅（`[UserMapping]`） | |

#### 验证

1. 编译通过 → `obj/` 目录下生成 `WmsMapper.g.cs` 源码文件
2. 菜单接口调用 → 返回数据结构与修改前一致
3. 性能：Mapperly 源码生成 = 编译时确定，运行时零反射开销
4. 后续新建 WMS DTO 时 → 验证 Mapperly 编译生成正确

---

### 任务 6：Polly 弹性韧性

**方案编号：** #10

**NuGet 安装：**
```bash
dotnet add src/Wms.Core.Infrastructure/Microsoft.Extensions.Http.Polly
```

**涉及文件：**
- `src/Wms.Core.WebApi/Program.cs`
- `src/Wms.Core.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`

**代码变更要点：**
1. Program.cs 注册标准弹性管道：
   ```csharp
   builder.Services.AddHttpClient<WcsClient>()
       .AddPolicyHandler(GetRetryPolicy())
       .AddPolicyHandler(GetCircuitBreakerPolicy());
   ```
2. 策略参数：重试 3 次 + 指数退避（2s/4s/8s），熔断 5 次失败后断路 30 秒
3. 为 WCS/ERP/MES 等外部接口客户端应用

**验证：** 模拟外部 API 不可用 → 确认重试 3 次后熔断 → 恢复后自动重连

---

### 任务 7：Dapper 高频读取层 ✅ 已完成

**方案编号：** #11

**NuGet 安装：**
```bash
dotnet add src/Wms.Core.Infrastructure/Wms.Core.Infrastructure.csproj package Dapper
# 已安装 Dapper 2.1.79
```

#### 涉及文件

| 操作 | 文件 |
|------|------|
| 新建 | `src/Wms.Core.Domain/Interfaces/IDapperReadService.cs` |
| 新建 | `src/Wms.Core.Infrastructure/Persistence/DapperReadService.cs` |
| 修改 | `src/Wms.Core.Infrastructure/Wms.Core.Infrastructure.csproj`（Dapper 2.1.79） |
| 修改 | `src/Wms.Core.WebApi/Program.cs`（DI 注册） |

#### 接口方法列表（IDapperReadService）

| 方法 | 说明 | SQL |
|------|------|-----|
| `GetBatteryCellByBarcode(string barcode)` | 条码扫描查询电芯 | `SELECT * FROM BatteryCells WHERE BarCode = @barcode` |
| `GetStocksByLocation(int locationId)` | 库位实时库存 | `SELECT * FROM Stocks WHERE LocationId = @locationId` |
| `GetStockByMaterialAndLocation(int materialId, int locationId)` | 精确库存查询 | `SELECT * FROM Stocks WHERE MaterialId = @materialId AND LocationId = @locationId` |
| `GetLocationByCode(string locationCode)` | 库位编码查询 | `SELECT * FROM Locations WHERE LocationCode = @locationCode` |
| `GetStocksByBatch(string batch)` | 批次库存查询 | `SELECT * FROM Stocks WHERE Batch = @batch` |
| `GetUnitloadByCode(string containerCode)` | 托盘编码查询 | `SELECT * FROM Unitloads WHERE ContainerCode = @containerCode` |

#### 设计决策

- **接口放在 Domain 层**：`IDapperReadService` 定义在 `Domain/Interfaces/`，符合 DDD 依赖规则（Infrastructure → Domain）
- **复用 EF Core 连接**：注入 `WmsDbContext`，通过 `_db.Database.GetDbConnection()` 获取 Dapper 所需连接，共享连接池
- **只读设计**：仅实现读取方法，写入仍走 EF Core Repository，保持变更跟踪（Change Tracker）能力
- **实体直接映射**：返回值直接使用 Domain 实体类型（BatteryCell/Stock/Location/Unitload），Dapper 自动映射，与 EF Core 实体属性名一致
- **SQL 注入防护**：所有查询使用 `@param` 参数化 SQL

#### DI 注册

```csharp
// Program.cs
builder.Services.AddScoped<IDapperReadService, DapperReadService>();
```

#### 使用示例

```csharp
// 在 Controller 或 Service 中注入 IDapperReadService
public class BarcodeController : ControllerBase
{
    private readonly IDapperReadService _dapperRead;

    public BarcodeController(IDapperReadService dapperRead)
    {
        _dapperRead = dapperRead;
    }

    [HttpGet("{barcode}")]
    public IActionResult Scan(string barcode)
    {
        var cell = _dapperRead.GetBatteryCellByBarcode(barcode);
        return cell == null ? NotFound() : Ok(cell);
    }
}
```

#### 验证

1. `dotnet build` 编译通过 0 错误 ✅
2. 后续业务开发时在条码扫描接口调用 → 确认响应时间 < 10ms
3. SQL Profiler → 确认查询使用 `@param` 参数化

---

### 任务 8：Hangfire 任务调度 ✅ 已完成

**方案编号：** #12

**NuGet 安装：**
```bash
dotnet add src/Wms.Core.WebApi/Wms.Core.WebApi.csproj package Hangfire.AspNetCore   # 已安装 1.8.23
dotnet add src/Wms.Core.WebApi/Wms.Core.WebApi.csproj package Hangfire.SqlServer     # 已安装 1.8.23
```

#### 涉及文件

| 操作 | 文件 |
|------|------|
| 新建 | `src/Wms.Core.Application/Jobs/StockReconciliationJob.cs` |
| 新建 | `src/Wms.Core.Application/Jobs/TimeoutOrderCleanupJob.cs` |
| 修改 | `src/Wms.Core.WebApi/Wms.Core.WebApi.csproj`（Hangfire NuGet） |
| 修改 | `src/Wms.Core.Application/Wms.Core.Application.csproj`（Logging.Abstractions） |
| 修改 | `src/Wms.Core.WebApi/Program.cs`（Hangfire 注册） |

#### 代码变更要点

1. Program.cs 注册 Hangfire：
   ```csharp
   builder.Services.AddHangfire(x => x
       .UseSqlServerStorage(hangfireConnectionString));
   builder.Services.AddHangfireServer();
   app.UseHangfireDashboard("/hangfire");
   ```
2. 创建定时任务 Job（具体业务逻辑待后续 WMS 业务开发时填充）：
   - `StockReconciliationJob` — 库存对账任务
   - `TimeoutOrderCleanupJob` — 超时订单清理任务
3. 后续可通过 `RecurringJob.AddOrUpdate()` 注册定时任务

#### 验证

1. `dotnet build` 编译通过 0 错误 ✅
2. 启动后访问 `/hangfire` → 确认 Hangfire 仪表盘可用
3. 后续添加 `RecurringJob.AddOrUpdate()` → 确认定时执行

---

### 任务 9：SignalR 实时通信 ✅ 已完成

**方案编号：** #13

**NuGet 安装：** 无（ASP.NET Core 内置）

#### 涉及文件

| 操作 | 文件 |
|------|------|
| 新建 | `src/Wms.Core.WebApi/Hubs/WmsHub.cs` |
| 修改 | `src/Wms.Core.WebApi/Program.cs`（SignalR 注册） |

#### 代码变更要点

1. Program.cs 注册：
   ```csharp
   builder.Services.AddSignalR();
   app.MapHub<WmsHub>("/hubs/wms");
   ```
2. 创建 `WmsHub.cs`，提供 3 个消息推送方法：
   - `SendTaskUpdate(taskId, status)` — 任务状态推送
   - `SendStockChange(locationId, materialId, qty)` — 库存变更推送
   - `SendAlert(message, level)` — 告警推送
3. 后端 Service 使用方式：
   ```csharp
   public class SomeService
   {
       private readonly IHubContext<WmsHub> _hub;
       public SomeService(IHubContext<WmsHub> hub) { _hub = hub; }

       public async Task NotifyStockChange(int locationId, int materialId, decimal qty)
       {
           await _hub.Clients.All.SendAsync("ReceiveStockChange",
               new { locationId, materialId, qty, timestamp = DateTime.Now });
       }
   }
   ```

#### 验证

1. `dotnet build` 编译通过 0 错误 ✅
2. 前端连接 `ws://host/hubs/wms` → 确认 WebSocket 连接成功
3. 后端调用 `_hub.Clients.All.SendAsync()` → 确认前端实时收到推送

---

### 任务 10：自建轻量 Mediator（可选）

**方案编号：** #5

**涉及文件：**
- `src/Wms.Core.Application/Mediator/` （新建）

**代码变更要点：**
1. 定义接口：
   ```csharp
   public interface IRequest<TResponse> { }
   public interface IRequestHandler<TRequest, TResponse> { TResponse Handle(TRequest request); }
   ```
2. 实现 `IMediator` 类（约 100 行），通过 DI 解析 Handler
3. 注册到 DI 容器
4. 适用于有明确读写分离需求的大型业务流程（如出库分配）

**验证：** 定义一个 Command + Handler → 通过 Mediator 调用 → 正确执行并返回结果

---

## 五、第三批：WMS 业务（高优先级）

### 任务 11：库位分配策略引擎 ✅ 已完成

**方案编号：** #22

#### 架构决策

| 层 | 内容 | 原因 |
|----|------|------|
| Domain | `ILocationAllocationRule` 接口 + `UnitloadStorageInfo` DTO + `LocationAllocationEngine` 引擎 | 符合 DDD，Domain 不依赖 Infrastructure |
| Infrastructure | 15 个规则实现（Dapper SQL） | 规则依赖 Dapper 进行数据库查询 |

#### 涉及文件

| 操作 | 文件 |
|------|------|
| 新建 | `src/Wms.Core.Domain/Tasks/ILocationAllocationRule.cs`（接口 + DTO） |
| 新建 | `src/Wms.Core.Domain/Tasks/LocationAllocationEngine.cs`（策略引擎） |
| 新建 | `src/Wms.Core.Infrastructure/Tasks/Rules/LocationAllocationRuleBase.cs`（规则基类） |
| 新建 | `src/Wms.Core.Infrastructure/Tasks/Rules/SSRule01.cs` ~ `SSRule10.cs`（10 个单深规则） |
| 新建 | `src/Wms.Core.Infrastructure/Tasks/Rules/SSRule04HcLx.cs`（单深特殊规则） |
| 新建 | `src/Wms.Core.Infrastructure/Tasks/Rules/SDRule01.cs` ~ `SDRule04.cs`（4 个双深规则） |
| 删除 | `src/Wms.Core.Domain/Tasks/Rules/` 下 15 个旧 NHibernate 规则文件 |
| 修改 | `src/Wms.Core.WebApi/Program.cs`（DI 注册） |
| 修改 | `src/Wms.Core.Domain/Wms.Core.Domain.csproj`（Logging.Abstractions + SqlClient） |

#### 规则清单

| 规则 | 类型 | Order | 说明 |
|------|:----:|:-----:|------|
| SSRule01 | 单深 | 100 | 空库位优先分配 |
| SSRule02 | 单深 | 200 | 同工艺匹配（loc2有货、loc1空） |
| SSRule03 | 单深 | 300 | 全新货位（loc1+loc2都空） |
| SSRule04 | 单深 | 100 | 一头进一头出 |
| SSRule04HcLx | 单深 | 100 | 行列限制特殊规则 |
| SSRule05 | 单深 | 150 | 同物料集中存放 |
| SSRule06 | 单深 | 120 | 靠端口优先 |
| SSRule07 | 单深 | 110 | 按重量上限递增 |
| SSRule08 | 单深 | 130 | 指定列分配 |
| SSRule09 | 单深 | 140 | 指定层分配 |
| SSRule10 | 单深 | 105 | 靠近地面优先 |
| SDRule01 | 双深 | 100 | 一深已有货（01→11） |
| SDRule02 | 双深 | 200 | 全空（00→01） |
| SDRule03 | 双深 | 300 | 二深有指定出库标记 |
| SDRule04 | 双深 | 400 | 全空按重量递增 |

#### 适配要点
- NHibernate HQL → Dapper + T-SQL
- `IRule` → `ILocationAllocationRule`
- `:param` → `@param`
- 移除 `RetainedBy()` 扩展，改为 C# 条件拼接
- `LocationAllocationRuleBase` 抽象基类提供通用 SQL 构建和查询执行

#### 引擎工作流
1. 接收 `AllocateAsync()` 请求
2. 按 `doubleDeep` 过滤出适用规则
3. 按 `Order` 排序，依次执行每条规则
4. 首个命中（返回非 null Location）即为结果
5. 记录每条规则的执行时间和命中情况

#### 验证
1. `dotnet build` 编译通过 0 错误 ✅
2. 后续上架入库时调用 `LocationAllocationEngine.AllocateAsync()` → 确认分配到符合策略的库位

---

### 任务 12：入库/出库/拣货/波次业务流程

**涉及文件：**
- `src/Wms.Core.Application/Services/InboundService.cs` （新建）
- `src/Wms.Core.Application/Services/OutboundService.cs` （新建）
- `src/Wms.Core.Application/Services/PickingService.cs` （新建）
- `src/Wms.Core.Application/Services/WaveService.cs` （新建）
- `src/Wms.Core.WebApi/Controllers/InboundController.cs` （新建）
- `src/Wms.Core.WebApi/Controllers/OutboundController.cs` （新建）
- 对应的 DTO 和验证器

**代码变更要点：**
1. 入库流程：收货 → 质检 → 上架 → 库存增加
2. 出库流程：创建出库单 → 库存分配 → 拣货 → 发货确认 → 库存扣减
3. 拣货策略：按波次、按库区、按路径优化
4. 波次管理：手动/自动创建波次 → 分配拣货任务

**验证：** 完整入库→出库流程 → 库存数据一致性

---

### 任务 13：Redis 库存缓存 + 分布式锁 ✅ 已完成

**方案编号：** #24

**NuGet 安装：**
```bash
dotnet add src/Wms.Core.Infrastructure/Wms.Core.Infrastructure.csproj package StackExchange.Redis   # 已安装 2.13.17
# WebApi 层同步升级到 2.13.17
```

#### 涉及文件

| 操作 | 文件 |
|------|------|
| 新建 | `src/Wms.Core.Domain/Interfaces/IInventoryCacheService.cs` |
| 新建 | `src/Wms.Core.Domain/Interfaces/IDistributedLockService.cs` |
| 新建 | `src/Wms.Core.Infrastructure/Caching/InventoryCacheService.cs` |
| 新建 | `src/Wms.Core.Infrastructure/Caching/DistributedLockService.cs` |
| 修改 | `src/Wms.Core.Infrastructure/Wms.Core.Infrastructure.csproj`（StackExchange.Redis） |
| 修改 | `src/Wms.Core.WebApi/Wms.Core.WebApi.csproj`（StackExchange.Redis 升级） |
| 修改 | `src/Wms.Core.WebApi/Program.cs`（DI 注册 + IConnectionMultiplexer） |

#### InventoryCacheService 接口方法

| 方法 | 说明 |
|------|------|
| `GetStockQtyAsync(locationId, materialId)` | 获取库存数量（缓存未命中返回 null） |
| `SetStockQtyAsync(locationId, materialId, qty, expiration)` | 设置库存到缓存 |
| `RemoveStockAsync(locationId, materialId)` | 删除单条库存缓存 |
| `RemoveByLocationAsync(locationId)` | 按库位批量删除 |

- Key 格式：`wms:stock:{locationId}:{materialId}`
- 默认过期时间：30 分钟
- Redis 未配置时自动降级

#### DistributedLockService 接口方法

| 方法 | 说明 |
|------|------|
| `LockTakeAsync(key, expiry, out token)` | 获取锁（SET NX EX） |
| `LockReleaseAsync(key, token)` | 释放锁（Lua 原子操作） |
| `ExecuteWithLockAsync(key, expiry, action)` | 在锁内执行操作（自动获取+释放） |

- 锁 Key 格式：`wms:lock:{key}`
- Lua 脚本保证释放原子性（仅持有者可释放）

#### 使用示例

```csharp
// 库存查询（Cache-Aside）
public async Task<decimal> GetStock(int locationId, int materialId)
{
    var cached = await _cache.GetStockQtyAsync(locationId, materialId);
    if (cached.HasValue) return cached.Value;

    var stock = await _db.Stocks.FindAsync(locationId, materialId);
    if (stock != null)
        await _cache.SetStockQtyAsync(locationId, materialId, stock.Qty);
    return stock?.Qty ?? 0;
}

// 库存扣减（分布式锁保证原子性）
public async Task<bool> DeductStock(int locationId, int materialId, decimal qty)
{
    return await _lock.ExecuteWithLockAsync(
        $"stock:{locationId}:{materialId}", TimeSpan.FromSeconds(10),
        async () =>
        {
            var stock = await _repository.Find(s => s.LocationId == locationId && s.MaterialId == materialId).FirstOrDefaultAsync();
            if (stock == null || stock.Qty < qty) return;
            stock.Qty -= qty;
            _repository.Update(stock);
            await _cache.RemoveStockAsync(locationId, materialId);
        });
}
```

#### 验证
1. `dotnet build` 编译通过 0 错误 ✅
2. Redis 连接后 → 库存查询命中 Redis → DB 无查询
3. 高并发库存扣减 → 无超卖

---

### 任务 14：WCS 通信适配器层 ✅ 已完成

**方案编号：** #23

**涉及文件：**

| 操作 | 文件 |
|------|------|
| 新建 | `src/Wms.Core.Domain/Interfaces/IWcsClient.cs` |
| 新建 | `src/Wms.Core.Infrastructure/Clients/DefaultWcsClient.cs` |
| 修改 | `src/Wms.Core.Infrastructure/Wms.Core.Infrastructure.csproj`（Microsoft.Extensions.Http.Polly 10.0.8） |
| 修改 | `src/Wms.Core.WebApi/Program.cs`（DI 注册 + Polly 策略） |

**代码变更要点：**

1. **IWcsClient 接口**（Domain 层）— 定义 3 个核心方法：
   - `Task<WcsResult> SendTaskAsync(TransTask task)` — 下发搬运任务
   - `Task<WcsResult> GetEquipmentStatusAsync(string equipmentId)` — 查询设备状态
   - `Task<WcsResult> UploadMesInfoAsync(UploadMesInfo info)` — 上传 MES 信息
   - 返回类型复用已有 `WcsResult`（`Domain/Utilities/Response/ApiResultHelper.cs`）
   - 参数类型复用已有 `TransTask`、`UploadMesInfo` 实体

2. **WcsClientOptions 配置类**（Domain 层）— 与 IWcsClient 同文件：
   - `Endpoint`、`TimeoutSeconds`(10)、`RetryCount`(3)、`CircuitBreakerFailureThreshold`(5)、`CircuitBreakerDurationSeconds`(30)

3. **DefaultWcsClient 实现**（Infrastructure 层）：
   - 基于 `IHttpClientFactory`，注入 `HttpClient`
   - POST/GET 请求，`System.Text.Json` 序列化
   - 异常兜底返回 `ApiResultHelper.WcsFail()`
   - 复用已有 `ApiResultHelper.WcsSuccess/WcsFail` 静态方法

4. **Polly 策略**（Program.cs）：
   - 重试：指数退避 2s → 4s → 8s，默认 3 次
   - 熔断：5 次失败后熔断 30 秒
   - 超时：默认 10 秒

5. **适配器模式扩展**：后续按设备厂商创建不同 `IWcsClient` 实现，DI 切换即可

6. **NuGet 版本升级**：Infrastructure 层多个包从 8.0.0 升级到 10.0.8（Logging.Abstractions、Options、Configuration.Abstractions、Configuration.Binder）

**验证：** `dotnet build` — 0 错误 0 警告

---

## 六、第四批：运维工具（中优先级）

### 任务 15：Docker + docker-compose 容器化

**方案编号：** #16

**涉及文件：**
- `Dockerfile` （新建，项目根目录）
- `docker-compose.yml` （新建，项目根目录）
- `.dockerignore` （新建）

**代码变更要点：**
1. Dockerfile：基于 `mcr.microsoft.com/dotnet/aspnet:8.0` 运行时镜像
2. docker-compose.yml 包含：
   - `wms-api` — WMS WebApi 服务
   - `sqlserver` — SQL Server 2019（开发用）
   - `redis` — Redis 7（缓存/队列/锁）
   - `seq` — Seq 日志（可选）
3. 环境变量管理连接字符串

**验证：** `docker-compose up -d` → 访问 Swagger → 健康检查通过

---

### 任务 16：Serilog + Seq 日志系统

**方案编号：** #15

**NuGet 安装：**
```bash
dotnet add src/Wms.Core.WebApi/Serilog.AspNetCore
dotnet add src/Wms.Core.WebApi/Serilog.Sinks.Seq
```

**涉及文件：**
- `src/Wms.Core.WebApi/Program.cs`
- `appsettings.json`

**代码变更要点：**
1. Program.cs 替换默认日志：
   ```csharp
   builder.Host.UseSerilog((context, services, configuration) =>
       configuration.ReadFrom.Configuration(context.Configuration));
   ```
2. appsettings.json 添加 Seq 配置：
   ```json
   "Serilog": {
     "WriteTo": [{ "Name": "Seq", "Args": { "serverUrl": "http://localhost:5341" } }]
   }
   ```

**验证：** 启动应用 → Seq UI 中查看结构化日志

---

### 任务 17：Scalar API 文档

**方案编号：** #20

**NuGet 安装：**
```bash
dotnet add src/Wms.Core.WebApi/Scalar.AspNetCore
```

**涉及文件：**
- `src/Wms.Core.WebApi/Program.cs`

**代码变更要点：**
1. Program.cs 添加：
   ```csharp
   app.MapScalarApiReference();
   ```
2. 保留现有 Swashbuckle XML 注释（Scalar 可复用）
3. 可选：移除 Swashbuckle Swagger UI（保留 OpenAPI 生成）

**验证：** 访问 `/scalar/v1` → 确认 API 文档页面展示

---

### 任务 18：EF Core 批量操作优化 ✅ 已完成

**方案编号：** #19

**涉及文件：**

| 操作 | 文件 |
|------|------|
| 修改 | `src/Wms.Core.Domain/Repositories/IRepository.cs` |
| 修改 | `src/Wms.Core.Infrastructure/Persistence/Repositories/Repository.cs` |
| 修改 | `src/Wms.Core.Domain/Wms.Core.Domain.csproj`（Microsoft.EntityFrameworkCore 8.0.0 + Microsoft.EntityFrameworkCore.Relational 8.0.0） |

**代码变更要点：**

1. **IRepository 接口**（Domain 层）— 新增 2 个批量方法：
   - `Task<int> BulkUpdateAsync(predicate, setPropertyCalls)` — 批量更新，直接在数据库执行
   - `Task<int> BulkDeleteAsync(predicate)` — 批量删除，直接在数据库执行
   - 使用 `SetPropertyCalls<T>` 类型（EF Core 7+ 内置）

2. **Repository 实现**（Infrastructure 层）：
   - `BulkUpdateAsync` → `_db.Set<T>().Where(predicate).ExecuteUpdateAsync(setPropertyCalls)`
   - `BulkDeleteAsync` → `_db.Set<T>().Where(predicate).ExecuteDeleteAsync()`

3. **注意事项**：
   - `ExecuteUpdateAsync` / `ExecuteDeleteAsync` 绕过 Change Tracker，不触发 `WmsDbContext.SaveChangesAsync` 中的审计字段自动填充
   - 方法命名为 `BulkUpdateAsync` / `BulkDeleteAsync`，与现有基于 Change Tracker 的 `Update` / `DeleteRange` 方法明确区分

4. **用法示例**：
   ```csharp
   // 批量更新库存状态
   var count = await _stockRepo.BulkUpdateAsync(
       x => x.WarehouseId == warehouseId,
       s => s.SetProperty(x => x.Status, "锁定"));

   // 批量删除
   var deleted = await _stockRepo.BulkDeleteAsync(
       x => x.Quantity <= 0 && x.WarehouseId == warehouseId);
   ```

**验证：** `dotnet build` — 0 错误 0 警告

---

## 七、第五批：长期规划（低优先级）

### 任务 19：JWT RS256 非对称签名迁移
- 生成 RSA 密钥对
- 更新 TokenService 使用 RS256
- 方案编号：#18

### 任务 20：Secrets 管理
- 开发环境：`dotnet user-secrets`
- 生产环境：环境变量 / Azure Key Vault
- 方案编号：#27

### 任务 21：自建领域事件总线 + Redis Pub/Sub
- `IEventBus` / `RedisEventBus`
- 库存变更、任务完成等事件发布/订阅
- 方案编号：#8

### 任务 22：ML.NET 需求预测
- 基于 `Flow` 表历史数据分析出库趋势
- 预测物料需求辅助库位分配
- 方案编号：#22 进阶

### 任务 23：升级 .NET 10 LTS — 回退至 .NET 8.0 LTS ✅ 已完成

**方案编号：** #17

**说明：** .NET 10 LTS 需要Visual Studio 2026（v18.x），当前 VS 2022 不支持。已回退至 .NET 8.0 LTS，待安装 VS 2026 后再升级。

**涉及文件：**

| 操作 | 文件 | 变更内容 |
|------|------|----------|
| 删除 | `global.json` | 移除 SDK 版本锁定 |
| 修改 | `src/Wms.Core.WebApi/Wms.Core.WebApi.csproj` | net10.0→net8.0，ASP.NET/JWT/HealthChecks/EF Tools→8.0.x |
| 修改 | `src/Wms.Core.Application/Wms.Core.Application.csproj` | net10.0→net8.0，Logging.Abstractions→8.0.2 |
| 修改 | `src/Wms.Core.Domain/Wms.Core.Domain.csproj` | net10.0→net8.0，EF Core/Relational→8.0.11，SqlClient→5.2.2 |
| 修改 | `src/Wms.Core.Infrastructure/Wms.Core.Infrastructure.csproj` | net10.0→net8.0，EF SqlServer/Design→8.0.11，SqlClient→5.2.2 |
| 修改 | `tests/Wms.Core.UnitTests/Wms.Core.UnitTests.csproj` | net10.0→net8.0 |
| 修改 | `tests/Wms.Core.IntegrationTests/Wms.Core.IntegrationTests.csproj` | net10.0→net8.0，Mvc.Testing→8.0.11 |
| 修改 | `src/Wms.Core.Domain/Repositories/IRepository.cs` | UpdateSettersBuilder→SetPropertyCalls（还原 EF Core 8 API） |
| 修改 | `src/Wms.Core.Infrastructure/Persistence/Repositories/Repository.cs` | BulkUpdateAsync 还原 EF Core 8 API |

**回退详情：**

1. **删除 global.json**：移除 .NET 10 SDK 版本锁定
2. **TargetFramework**：6 个 csproj 全部从 `net10.0` → `net8.0`
3. **NuGet 包降级**：
   - EF Core 系列：10.0.0 → 8.0.11
   - ASP.NET Core：10.0.0 → 8.0.x
   - Microsoft.Extensions：10.0.x → 8.0.x
   - Microsoft.Data.SqlClient：6.1.1 → 5.2.2
4. **EF Core API 还原**：
   - `UpdateSettersBuilder<T>` → `SetPropertyCalls<T>`（EF Core 8）
   - `Action<UpdateSettersBuilder<T>>` → `Expression<Func<SetPropertyCalls<T>, SetPropertyCalls<T>>>`

**验证：** `dotnet build` — 0 错误，381 个警告

**后续计划：** 安装 Visual Studio 2026 后重新升级至 .NET 10 LTS

---

## 八、NuGet 安装命令汇总

```bash
# 第一批：安全加固
dotnet add src/Wms.Core.Infrastructure/BCrypt.Net-Next
dotnet add src/Wms.Core.Application/FluentValidation.AspNetCore

# 第二批：架构工具
dotnet add src/Wms.Core.Application/Riok.Mapperly.Abstractions
dotnet add src/Wms.Core.Application/Riok.Mapperly
dotnet add src/Wms.Core.Infrastructure/Microsoft.Extensions.Http.Polly
dotnet add src/Wms.Core.Infrastructure/Dapper
dotnet add src/Wms.Core.WebApi/Hangfire.AspNetCore
dotnet add src/Wms.Core.WebApi/Hangfire.SqlServer

# 第三批：WMS 业务（无额外包）

# 第四批：运维工具
dotnet add src/Wms.Core.WebApi/Serilog.AspNetCore
dotnet add src/Wms.Core.WebApi/Serilog.Sinks.Seq
dotnet add src/Wms.Core.WebApi/Scalar.AspNetCore
```

---

## 九、验证检查清单

每完成一批后执行以下检查：

### 第一批验证
- [ ] 新用户注册后密码哈希格式为 `$2a$11$...`
- [ ] SQL 注入测试：非法 orderBy 参数不产生拼接 SQL
- [ ] 并发更新库存：后更新者收到 409 Conflict
- [ ] 缺少必填字段的请求返回 400 + 验证详情

### 第二批验证
- [ ] Mapperly 编译生成代码存在
- [ ] 外部 API 不可用时重试 3 次 + 熔断
- [ ] Dapper 条码查询 < 10ms
- [ ] Hangfire 仪表盘 `/hangfire` 可访问
- [ ] SignalR Hub 连接正常 + 实时推送

### 第三批验证
- [ ] 入库流程完整执行（收货→上架→库存增加）
- [ ] 出库流程完整执行（分配→拣货→发货→库存扣减）
- [ ] 库存缓存命中率 > 80%
- [ ] WCS 任务发送成功

### 第四批验证
- [ ] `docker-compose up -d` 一键启动全部服务
- [ ] Seq UI 中查看结构化日志
- [ ] Scalar API 文档可访问
- [ ] 批量操作性能达标

### 全局验证
- [ ] `dotnet build` 零错误零警告
- [ ] `dotnet test` 全部通过（如有测试）
- [ ] Swagger / Scalar API 文档正常展示

---

## 十、相关文档索引

| 文档 | 用途 |
|------|------|
| [WMS架构优化-最终推荐方案.md](WMS架构优化-最终推荐方案.md) | 27 项技术选型推荐 + 推荐理由 |
| [WMS架构优化分析与技术选型排名.md](WMS架构优化分析与技术选型排名.md) | 原始调研（行业趋势 + 开源对比 + 排名） |
| [WMS架构优化方案-市场验证审查报告.md](WMS架构优化方案-市场验证审查报告.md) | 市场验证（7 项修正 + NuGet 数据 + 来源） |
| [日志审计方案.md](日志审计方案.md) | 已有的日志审计方案 |
| [JWT-Testing-Guide.md](JWT-Testing-Guide.md) | JWT 双令牌测试指南 |
