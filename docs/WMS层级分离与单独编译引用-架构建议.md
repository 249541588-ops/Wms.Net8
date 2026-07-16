# Wms.Core 层级分离与单独编译引用架构建议

> 面向 1-2 人全栈团队的渐进式分离方案。核心思想：区分"底层代码"与"项目级代码"，用 ProjectReference 源码级复用，零运维成本。

## Context（背景）

Wms.Core 是一个 WMS 仓储管理系统，后端 `Wms.Net8`（.NET 8 / Clean Architecture 四层）+ 前端 `Wms.Vue`（Vue 3 + Vite SPA），当前为单体部署（IIS 同机 + docker-compose 单服务）。

**现状关键事实**（已通过代码验证）：

1. **后端 4 层**：Domain / Application / Infrastructure / WebApi，依赖方向 `WebApi → Infrastructure → Application → Domain`
2. **Infrastructure 内已存在 `Wms.Core.Engine` 命名空间**（[ServiceCollectionExtensions.cs:12-13](../src/Wms.Core.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs#L12-L13) `using Wms.Core.Engine;`），说明团队早有引擎分离意图，只差没拆项目
3. **DI 注册已模块化**：[Program.cs:88-100](../src/Wms.Core.WebApi/Program.cs#L88-L100) 已用 11 个 `Add*` 扩展方法编排，拆分骨架已成
4. **25 个节点处理器手写注册**（[ServiceCollectionExtensions.cs:89-113](../src/Wms.Core.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs#L89-L113)；Phase 0 时 22 个 + 2026-07 工艺路线相关 3 个：AdvanceOperation / ProcessTagVerification / VerifyProcessSteps），拆分时可改为程序集扫描自动注册
5. **三库物理分离已落地**：WmsDb（主库 60+ DbSet）/ WmsLogsDb（日志）/ ctask（WCS 任务，Dapper）
6. **Ports 模式已落地**：21 个端口接口在 Application，实现在 Infrastructure
7. **[Result.cs](../src/Wms.Core.Domain/Common/Result.cs) 是纯 POCO**，迁移到共享内核零风险

**适用场景**：1-2 人全栈团队，希望区分"底层代码"与"项目级代码"，用 ProjectReference 源码级复用为主，避免过度工程。

**目标**：给出最小代价、最大收益的渐进式分离路径。

---

## 一、分离粒度光谱（四种策略对比）

| 策略 | 核心思路 | 实施成本 | 编译加速 | 推荐度（1-2 人） |
|------|----------|----------|----------|------------------|
| **A. 共享内核** | 抽取 Result/Enums/Constants/IAuditable 到 Domain.Shared | ★☆☆☆☆ | ★☆☆☆☆ | ★★★★★ 立即做 |
| **B. 技术子系统** | 拆 Engine / WcsGateway / Reporting / Logging 为独立项目 | ★★★☆☆ | ★★★★☆ | ★★★★☆ 按需做 |
| **C. 业务模块化** | 按 Inbound/Outbound/Inventory 等业务域垂直切分 | ★★★★☆ | ★★★☆☆ | ★★☆☆☆ 不推荐 |
| **D. 微服务化** | 按业务域独立部署、独立数据库 | ★★★★★ | ★★☆☆☆ | ★☆☆☆☆ 不推荐 |

**针对 1-2 人全栈团队的结论**：只做 A + B（渐进式），不做 C/D。微服务化和业务模块化对单人团队是过度工程，运维成本远超收益。

---

## 二、"底层代码 vs 项目级代码"分离思路

这是本方案的核心思想：把代码按"复用层级"分为两类，物理上拆到不同项目。

### 2.1 分类定义

| 类别 | 特征 | 对应当前代码 | 复用方式 |
|------|------|--------------|----------|
| **底层代码** | 与业务无关或弱业务、跨项目可复用、变更缓慢 | Result / Enums / Constants / Flow Engine / WCS 通信抽象 / 安全工具 | 独立项目，未来可打 NuGet |
| **项目级代码** | WMS 业务特定、仅在本解决方案内有用、频繁变更 | WMS 实体 / DTO / 控制器 / 仓储实现 / 业务服务 | 留在业务项目内 |

### 2.2 推荐项目结构（最终目标）

```
Wms.Net8/src/
│
│ ========== 底层代码（可复用层）==========
├── Wms.Core.Domain.Shared/        ← 共享内核：Result/Enums/Constants/IAuditable
│   └── (无项目引用，最底层)
│
├── Wms.Core.Engine/               ← 流程引擎：IFlowEngine + 25 节点处理器（Phase 0 时 22 + 工艺路线 3）
│   └── 引用 Application.Contracts + Domain + Shared
│
├── Wms.Core.WcsGateway/           ← WCS/MES/HangKe 通信网关（按需拆）
│   └── 引用 Application.Contracts + Domain + Shared
│
├── Wms.Core.Logging/              ← 日志子系统：WmsLogDbContext + 迁移
│   └── 引用 Domain + Shared
│
│ ========== 项目级代码（业务层）==========
├── Wms.Core.Domain/               ← 领域实体 + 仓储接口（瘦身）
│   └── 引用 Shared
│
├── Wms.Core.Application/          ← DTO + Ports 接口 + Handlers
│   └── 引用 Domain + Shared
│
├── Wms.Core.Infrastructure/       ← 核心持久化 + 通用业务服务（瘦身）
│   └── 引用 Application + Domain + Shared
│
└── Wms.Core.WebApi/               ← 宿主 + 控制器 + 中间件
    └── 引用全部
```

### 2.3 引用关系图

```
        Wms.Core.Domain.Shared  ← (无引用，最底层，零依赖)
                  ↑
    ┌─────────────┴─────────────┐
    │                           │
Wms.Core.Domain          Wms.Core.Logging
    ↑                           │
Wms.Core.Application            │
    ↑                           │
    ├───────────────────────────┤
    │                           │
Wms.Core.Engine    Wms.Core.WcsGateway
    │                           │
    └───────────┬───────────────┘
                │
        Wms.Core.Infrastructure  ← (核心持久化：WmsDbContext + 通用服务)
                ↑
        Wms.Core.WebApi  ← (引用全部，组装)
```

**关键解耦点**：Engine 与 WcsGateway 之间**不直接引用**，通过 Application.Ports 接口（如 `IWcsTaskBridge`）+ DI 注入实现松耦合。WebApi 在 Program.cs 中同时注册两者，DI 容器自动解析。这是已有 Ports 模式的正确用法，无需额外工作。

---

## 三、渐进式分离路线图

### Phase 1：抽取共享内核（最低风险，立即做）

**为什么先做**：零风险（只搬类型到新项目，改 using）、为后续所有分离打基础、解决 Domain 既是领域模型又承载共享类型的职责混乱。

**迁移清单**（从 Domain 迁到 Domain.Shared）：

| 源路径 | 目标路径 | 说明 |
|--------|----------|------|
| `Domain/Common/Result.cs` | `Shared/Common/Result.cs` | 纯 POCO，零依赖 |
| `Domain/Common/ResultCodeTypes.cs` | `Shared/Common/ResultCodeTypes.cs` | 枚举 |
| `Domain/Enums/*.cs`（10 个文件） | `Shared/Enums/` | 全部枚举 |
| `Domain/Constants/CommonTypes.cs` | `Shared/Constants/` | 常量 |
| `Domain/Constants/Cst.cs` | `Shared/Constants/` | 常量 |
| `Domain/Interfaces/IAuditable.cs` | `Shared/Interfaces/` | 审计接口 |

**操作要点**：
1. 新建 `Wms.Core.Domain.Shared` 类库（net8.0，无任何 ProjectReference）
2. Domain.csproj 添加 `<ProjectReference Include="..\Wms.Core.Domain.Shared\Wms.Core.Domain.Shared.csproj" />`
3. 全局替换 `using Wms.Core.Domain.Common;` → `using Wms.Core.Domain.Shared.Common;`（IDE 全局替换）
4. 同理替换 Enums/Constants/Interfaces 的 using
5. 编译验证 + 运行测试

**可选附加**：同时新建 `Wms.Core.Application.Contracts`，把 `Application/Ports/` 下 21 个接口迁过去。Infrastructure 可只引用 Contracts 而非整个 Application，减少不必要的 DTO 依赖。但此步可延后，Phase 1 先做 Shared 即可。

---

### Phase 2：拆分 Engine + Logging（中风险，按需做）

**触发条件**：
- 改 Infrastructure 其他部分时频繁触发 Engine 重编，编译变慢
- 想为流程引擎单独写单元测试
- 希望引擎代码与业务基础设施解耦，便于未来抽成产品

**为什么这两个先拆**：
- **Engine**：命名空间已是 `Wms.Core.Engine`（设计意图明确），25 节点处理器（Phase 0 时 22 + 工艺路线 3）是独立闭环
- **Logging**：已有独立 `WmsLogDbContext` + `WmsLogsDb` + `LogMigrations`，物理边界最清晰

**Engine 拆分步骤**：

1. 新建 `Wms.Core.Engine` 类库，引用 `Application` + `Domain` + `Shared`
2. 迁移目录：
   - `Infrastructure/Flow/` → `Engine/Flow/`（含 IFlowEngine / INodeHandler / FlowContext / Nodes/ 25 个处理器）
   - `Infrastructure/Services/FlowEngineService.cs` → `Engine/FlowEngineService.cs`
3. 新建 `Engine/DependencyInjection/EngineExtensions.cs`，实现 `AddWmsEngine()`：
   ```csharp
   public static IServiceCollection AddWmsEngine(this IServiceCollection services)
   {
       services.AddScoped<IFlowEngine, FlowEngineService>();
       // 程序集扫描自动注册所有 INodeHandler（替代手写 25 行）
       var assembly = typeof(EngineExtensions).Assembly;
       var handlerTypes = assembly.GetTypes()
           .Where(t => t is { IsClass: true, IsAbstract: false }
               && typeof(INodeHandler).IsAssignableFrom(t));
       foreach (var type in handlerTypes)
           services.AddScoped(typeof(INodeHandler), type);
       return services;
   }
   ```
4. 从 [ServiceCollectionExtensions.cs:89-113](../src/Wms.Core.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs#L89-L113) 删除 Engine 相关注册（IFlowEngine + 25 个 INodeHandler）
5. WebApi.csproj 添加 `<ProjectReference Include="..\Wms.Core.Engine\..." />`
6. Program.cs 中 `builder.Services.AddWmsEngine();`

**Logging 拆分步骤**：

1. 新建 `Wms.Core.Logging` 类库，引用 `Domain` + `Shared`（需要 InterfaceLog 实体）
2. 迁移：
   - `Infrastructure/Persistence/WmsLogDbContext.cs` → `Logging/WmsLogDbContext.cs`
   - `Infrastructure/Persistence/LogMigrations/` → `Logging/LogMigrations/`
3. 新建 `Logging/DependencyInjection/LoggingExtensions.cs`，实现 `AddWmsLogging(IConfiguration)`
4. 从 [ServiceCollectionExtensions.cs:37-43](../src/Wms.Core.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs#L37-L43) 删除 WmsLogDbContext 注册
5. Program.cs 中 `builder.Services.AddWmsLogging(builder.Configuration);`

**关键注意**：Engine 中的 `SendWcsTaskHandler` 依赖 `IWcsTaskBridge`（定义在 Application/Ports）。Engine 项目**不引用** WcsGateway，通过 DI 注入接口。只要 WebApi 在 Program.cs 中同时注册了 Engine 和 WcsGateway，DI 容器自动解析。这是已有 Ports 模式的正确用法。

---

### Phase 3：拆分 WcsGateway + Reporting（较高风险，按需做）

**触发条件**：
- WCS/MES 对接频繁变更，每次改动触发 Infrastructure 全量重编
- 需要为 WCS 对接单独写集成测试
- 报表模块需要独立部署或被其他系统调用

**WcsGateway 拆分**（迁移代码分散在 7 处目录）：

| 源路径（Infrastructure 内） | 目标路径（WcsGateway 内） |
|------------------------------|---------------------------|
| `Clients/DefaultWcsClient.cs` 等 5 个 | `Clients/` |
| `Handlers/WcsRequest/`（12 个） | `Handlers/WcsRequest/` |
| `Handlers/TaskCompletion/`（3 个） | `Handlers/TaskCompletion/` |
| `Tasks/Rules/`（15 条规则 + Engine） | `Tasks/Rules/` |
| `Persistence/CtaskDbService.cs` | `Persistence/CtaskDbService.cs` |
| `WebApi/Extensions/WcsExtensions.cs` | `DependencyInjection/WcsGatewayExtensions.cs` |
| `WebApi/Extensions/MesExtensions.cs` | `DependencyInjection/MesExtensions.cs` |
| `WebApi/Services/Wcs/` | `Services/` |
| `WebApi/HealthChecks/WcsHealthCheck.cs` | `HealthChecks/` |

新建 `AddWcsGateway(IConfiguration)` 统一注册，从 Program.cs 替换原 `AddWcsServices` + `AddWcsClient` + `AddMesClient` + `AddHangKeClient`。

**Reporting 拆分**：

| 源路径 | 目标路径 |
|--------|----------|
| `Infrastructure/Services/ReportExportService.cs` | `Reporting/Services/` |
| `Infrastructure/Services/ReportProviders/`（6 个） | `Reporting/Services/ReportProviders/` |
| `Infrastructure/Services/ReportService.cs` | `Reporting/Services/` |

新建 `AddWmsReporting()` 注册。

---

## 四、单独编译引用方式选择

针对 1-2 人全栈团队，**只用 ProjectReference**，不用 NuGet。

| 方式 | 适用场景 | 运维成本 | 推荐度 |
|------|----------|----------|--------|
| **ProjectReference** | 同一 sln 内源码级复用 | 零 | ★★★★★ 本方案唯一选择 |
| **NuGet PackageReference** | 跨 sln 复用，需版本化 | 高（需私有源 + CI/CD + SemVer） | ★☆☆☆☆ 当前不推荐 |
| **Source Generators** | 编译时代码生成 | - | 不适用此场景 |

**理由**：
- ProjectReference 调试无缝、增量编译加速明显、零运维
- NuGet 化需要搭建 BaGet/Azure Artifacts + CI 流水线 + 版本管理，对单人团队是沉重负担
- 如果未来 Engine/WcsGateway 真的需要被其他解决方案复用，再转为 NuGet 包不迟（架构上已预留）

---

## 五、关键实施注意事项

### 5.1 共享类型避免循环引用

把共享类型下沉到最底层的 `Domain.Shared`，依赖方向始终向上：

```
Shared (Result, Enums, Constants, IAuditable)  ← 最底层，无引用
  ↑
Domain (Entities, Repository 接口)  ← 引用 Shared
  ↑
Application (Ports, DTOs)  ← 引用 Domain + Shared
  ↑
Infrastructure / Engine / WcsGateway  ← 引用 Application + Domain + Shared
  ↑
WebApi  ← 引用全部
```

**循环引用检测**：CI 中可用 `dotnet list package --include-transitive` 检查，或使用 [ReferenceTrimmer](https://github.com/Elders/ReferenceTrimmer) 工具。

### 5.2 EF Core DbContext 拆分陷阱

**陷阱 1：ApplyConfigurationsFromAssembly 程序集扫描**
- 当前 `WmsDbContext.OnModelCreating` 用 `ApplyConfigurationsFromAssembly(typeof(WmsDbContext).Assembly)` 自动发现配置
- 如果把部分 Configuration 移到 Engine/WcsGateway 项目，会漏掉
- **解决**：所有 `IEntityTypeConfiguration` **保持在 Infrastructure 项目内**，不随业务迁移。Configuration 与 DbContext 同项目

**陷阱 2：WmsDbContext 不拆**
- 60+ DbSet 间外键关系密集，拆 DbContext 会导致跨表查询需分布式事务
- **解决**：WmsDbContext 保持单一，注册统一在 `AddWmsCoreInfrastructure()` 中。其他项目只注入使用，不重复注册

**陷阱 3：导航属性跨项目**
- 如果 Domain Entities 按模块拆项目，`Unitload.Materials` 导航属性会跨项目引用
- **解决**：Domain Entities **不拆项目**，保持在单一 Domain 项目内

### 5.3 DI 注册重组示例

拆分后 Program.cs 的最终编排（[Program.cs:88-100](../src/Wms.Core.WebApi/Program.cs#L88-L100) 改造后）：

```csharp
// 核心持久化 + 通用业务服务（瘦身版）
builder.Services.AddWmsCoreInfrastructure(builder.Configuration);

// 底层子系统（独立项目）
builder.Services.AddWmsLogging(builder.Configuration);   // 日志 DB
builder.Services.AddWmsEngine();                         // 流程引擎 + 25 节点
builder.Services.AddWcsGateway(builder.Configuration);   // WCS 网关 + 规则（按需）
builder.Services.AddWmsReporting();                      // 报表（按需）

// 横切关注点（保持现状）
builder.Services.AddWmsRedis(builder.Configuration);
builder.Services.AddWmsHangfire(builder.Configuration);
builder.Services.AddWmsAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddWmsCors(builder.Configuration);
builder.Services.AddWmsHealthChecks(builder.Configuration);
```

### 5.4 不建议分离的部分

| 不建议分离 | 原因 |
|------------|------|
| **WmsDbContext** | 60+ DbSet 外键关系密集，拆分后跨表查询需分布式事务，迁移管理复杂度爆炸 |
| **Domain Entities 按业务模块拆项目** | 实体间导航属性跨模块引用时无法编译；DDD 聚合边界与项目边界不一致 |
| **WebApi Controllers 拆成多个项目** | ASP.NET Core 控制器发现依赖程序集扫描，多项目需配置 ApplicationPart，收益极低 |
| **前端微前端化** | 单 SPA + Soybean monorepo 已足够；qiankun/模块联邦复杂度远超收益 |
| **完全微服务化** | 单人团队无 K8s 基础设施，WMS 强事务性跨服务代价极高 |
| **MediatR** | 已有 Flow Engine 做流程编排，21 个 Ports 接口已实现解耦，MediatR 无额外收益 |
| **多目标框架 netstandard2.0** | 当前纯 .NET 8，多目标引入条件编译增加维护成本，无实际收益 |

### 5.5 接口与实现分离的命名规范

| 项目 | 职责 | 命名 |
|------|------|------|
| 共享内核 | 枚举/常量/Result/IAuditable | `Wms.Core.Domain.Shared` |
| 领域抽象 | Repository 接口 + Domain Service 接口 + Entities | `Wms.Core.Domain`（保持原名） |
| 应用契约 | Ports 接口 + DTOs（可选独立） | `Wms.Core.Application.Contracts`（可选） |
| 应用层 | Handlers / Jobs | `Wms.Core.Application` |
| 基础设施 | DbContext + Repositories 实现 + 通用服务 | `Wms.Core.Infrastructure` |
| 流程引擎 | FlowEngine + NodeHandlers | `Wms.Core.Engine` |
| WCS 网关 | Clients + Handlers + Rules + CtaskDb | `Wms.Core.WcsGateway` |
| 报表 | ReportExportService + Providers | `Wms.Core.Reporting` |
| 日志 | WmsLogDbContext + LogMigrations | `Wms.Core.Logging` |
| 宿主 | Program.cs + Controllers + Middleware | `Wms.Core.WebApi`（保持原名） |

**不推荐** `*.Abstractions` 后缀命名。理由：ABP 用 Abstractions 是因为它把 Entity/ValueObject 也拆出来了，本项目 Domain 已是纯接口+POCO，再拆 Abstractions 是过度工程。

### 5.6 前端无需配合

后端分离对前端的影响：
- **策略 A/B（水平分离）**：前端**完全不需要改动**。API 路径不变，只是后端内部项目结构变了
- 前端继续用单 SPA + Soybean monorepo（9 个 @sa/* 内部包）即可
- 不需要引入 qiankun / 模块联邦等微前端框架

---

## 六、验证方式

### 6.1 Phase 1 验证（共享内核抽取后）
- `dotnet build Wms.Net8.sln` 编译通过
- `dotnet test` 所有单元测试 + 集成测试通过
- 运行 WebApi，登录、查询物料、入库等核心业务功能正常
- 检查 `Wms.Core.Domain.Shared` 项目无任何 ProjectReference

### 6.2 Phase 2 验证（Engine 拆分后）
- `dotnet build` 编译通过
- 改 Engine 项目内某个节点处理器，增量编译只触发 Engine + WebApi，不触发 Infrastructure
- 运行流程引擎相关业务（如入库流程、出库流程）正常
- `Wms.Core.Engine` 项目不引用 `Wms.Core.WcsGateway`（验证松耦合）

### 6.3 Phase 3 验证（WcsGateway 拆分后）
- WCS 任务下发、WCS 回调、MES 上传、杭可通知等功能正常
- 改 WcsGateway 项目内代码，增量编译不触发 Infrastructure
- 健康检查 `/health` 中 WCS 健康检查正常工作

---

## 关键文件清单

实施本方案时最关键的 5 个文件（按优先级排序）：

1. [ServiceCollectionExtensions.cs](../src/Wms.Core.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs) - 当前所有 DI 注册的中枢，拆分后需删除已迁移模块的注册
2. [Program.cs](../src/Wms.Core.WebApi/Program.cs) - DI 编排入口点，拆分后改为调用各模块的 Add* 扩展方法
3. [WcsExtensions.cs](../src/Wms.Core.WebApi/Extensions/WcsExtensions.cs) - WCS 子系统 DI 注册模板，拆分 WcsGateway 时作为 AddWcsGateway 实现参考
4. [WmsDbContext.cs](../src/Wms.Core.Infrastructure/Persistence/WmsDbContext.cs) - 60+ DbSet 核心 DbContext，拆分时确保不被破坏
5. [Result.cs](../src/Wms.Core.Domain/Common/Result.cs) - 共享内核的第一个迁移目标，Phase 1 起点

---

## 总结

**核心建议**：1-2 人全栈团队，采用"底层代码 vs 项目级代码"分离思路，用 ProjectReference 源码级复用。

**渐进路径**：
1. **Phase 1（立即）**：抽取 `Domain.Shared` 共享内核 —— 零风险，为后续打基础
2. **Phase 2（按需）**：拆分 `Engine` + `Logging` —— 命名空间已就绪，物理边界清晰
3. **Phase 3（按需）**：拆分 `WcsGateway` + `Reporting` —— 当 WCS/报表变更频繁时再做

**不做**：业务模块化垂直切分、微服务化、NuGet 化、前端微前端化 —— 对单人团队均为过度工程。
