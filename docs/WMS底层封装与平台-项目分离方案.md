# WMS 底层封装与平台/项目分离方案

> 与 [WMS层级分离与单独编译引用-架构建议.md](WMS层级分离与单独编译引用-架构建议.md) 配套阅读。原文档聚焦"编译加速 + 代码组织"（ProjectReference 源码分层），本文档聚焦"**代码封装 + 权限隔离**"（DLL 二进制引用）。
>
> 适用场景：1-2 人底层维护团队 + 多人项目层开发团队，希望把 WMS 核心平台代码封装保护，仅以 DLL 形式提供给项目层使用。

## Context（为什么需要这个变更）

### 起因

已有文档 [WMS层级分离与单独编译引用-架构建议.md](WMS层级分离与单独编译引用-架构建议.md) 提出了完整的 Clean Architecture 分层方案，但进一步明确的真实诉求是：**把底层封装，不易被窃取，分为底层和项目层**。

通过需求澄清确认的四个关键约束：

| 维度 | 选择 |
|------|------|
| 威胁场景 | **内部权限隔离**（少数人维护底层，多数人只看项目层） |
| 底层边界 | **包含领域实体**（Domain 全部算底层，项目层只做客户定制） |
| 运维成本 | **低运维**（本地脚本 + DLL 引用，不搭 NuGet 私有源） |
| 复用模式 | **多客户共用，底层较少改** |

### 原文档的根本局限

原文档所有 Phase 1-3 全部基于 **ProjectReference（源码同仓库）**，这种模式下：

1. 所有源码仍在同一个 `.sln`，内部开发者打开 VS 即可看到全部源码
2. **完全无法满足"内部权限隔离"** 的核心诉求
3. 即便拆 10 个项目，对内部成员而言仍是透明的

真正能满足需求的是 **物理仓库分离 + 二进制引用**，类似 ABP Framework（框架/项目分层）、Orchard Core（Shell/Module 分层）等工厂级 WMS 项目的通行做法。

### 调研中发现的额外问题（原文档未充分讨论）

代码探索暴露出 6 个比"分层"更紧迫的技术债，必须先解决才能实现物理分离：

1. **`FlowContext` 直接持有 `WmsDbContext`**：22 个节点处理器（Phase 0 时；2026-07 增至 25 个）通过 `context.Db` 访问 70 个 DbSet，是 Engine 拆分的最大障碍（[FlowContext.cs](../src/Wms.Core.Infrastructure/Flow/FlowContext.cs)）
2. **请求阶段无事务保护**：`FlowEngineService.ExecuteAsync` 仅用 `IsTransactionBoundary` 做中间 SaveChanges，失败无法回滚
3. **WebApi 层承担基础设施职责**：`IBackgroundTaskQueue`、`ITranslationService` 实现在 WebApi 层，依赖方向倒置
4. **WCS 网关分散在 3 个项目 8 目录**（Application/Ports、Infrastructure 的 Clients/Flow/Nodes/Handlers 两类/Services、WebApi 的 Extensions/Services）：拆分工作量被原文档低估
5. **35+ 处 `BeginTransaction` 散布，无 `IUnitOfWork` 抽象**：其中 UnitloadService.cs 中 6 处是同步调用，存在线程池风险
6. **节点数量与文档不符**：实际 22 个节点处理器（Phase 0 时；非原文档笔记中的 26 或 25——"25"系长期计数错误，2026-07-15 修订时方才修正）

### 业界参考实践

| 实践 | 出处 | 本方案如何采纳 |
|------|------|----------------|
| 读写不对称分层 | Jason Taylor ADR-001、Grzybecki ADR-0009 | 底层提供查询服务接口，项目层实现 Dapper 直查 |
| 已有 Flow Engine 不用 MediatR | Cezary Piatek、Julio Casal | 不引入 MediatR，沿用现有 Flow Engine |
| Modular Monolith 模块内结构 | kgrzybek/modular-monolith-with-ddd | 底层模块结构参考其 Application/Domain/Infrastructure 划分 |
| ABP 多 DbContext 协调 | abpframework/abp | 多数据访问上下文（WmsDbContext + WmsLogDbContext 两个 EF DbContext，外加 CtaskDbService 这个 Dapper 直连服务）通过 IUnitOfWork 统一 |
| Bounded Context 逻辑划分 | Grzybecki ADR-0004 | 实体目录已按 Warehouse/Container/Material 等划分，无需物理拆项目 |
| Platform/Shell 双仓库 | ABP Framework、Orchard Core | 平台代码独立仓库，项目层通过二进制引用 |

---

## 一、推荐方案核心：物理仓库分离 + DLL 二进制引用

### 1.1 两个 Git 仓库

```
wms-platform/          # 底层平台仓库（受保护，少数人写权限）
  src/
    Wms.Core.Domain.Shared/
    Wms.Core.Domain/
    Wms.Core.Application.Contracts/
    Wms.Core.Application/
    Wms.Core.Engine/
    Wms.Core.Logging/
  artifacts/           # 编译产出的 DLL（可走 artifacts 分支或目录）
  pack.ps1
  PLATFORM_VERSION.txt

wms-project-xxx/       # 项目层仓库（多数人写权限）
  src/
    Wms.Core.Infrastructure/   # 瘦身后
    Wms.Core.WebApi/
    Wms.Core.Customer.XXX/     # 客户定制模块
  lib/platform/        # 从 platform 同步过来的 DLL
  sync-platform.ps1
```

### 1.2 同步机制选型

| 方式 | 项目层能否看到源码 | 是否满足"内部权限隔离" | 推荐 |
|------|--------------------|------------------------|------|
| git submodule | 能 | 否 | 否 |
| git subtree | 能 | 否 | 否 |
| **artifacts 分支 + DLL 引用** | **不能** | **是** | **推荐** |
| NuGet 私有源 | 不能 | 是 | 否（运维成本超用户接受度） |

**推荐 artifacts 分支模式**：底层仓库 push 时 CI 编译 DLL 推送到 `artifacts` 分支，项目层用 `sync-platform.ps1` 拉到 `lib/platform/`，项目层开发者只能看到 DLL 和嵌入的调试符号，看不到源码。

### 1.3 防护强度对比

| 防护手段 | 强度 | 备注 |
|----------|------|------|
| ProjectReference 源码分层 | 0 | 原文档做法，对内部成员完全透明 |
| **DLL 引用 + EmbedAllSources** | **中** | **本方案**：看不到 .cs 文件，但 dnSpy 反编译可见 |
| NuGet 私有源 | 中 | 等同本方案，但有版本管理 |
| 代码混淆（Obfuscar/.NET Reactor） | 强 | 客户拿到 DLL 也难反编译 |
| Native AOT 编译 | 极强 | 性能敏感 + 极强保护 |

本方案选 DLL 引用是因用户明确选"低运维"且威胁场景是"内部权限隔离"（不是反编译防护）。

---

## 二、底层 DLL 清单（受保护）与项目层（开放）划分

| 项目 | 职责 | 所在仓库 |
|------|------|----------|
| **Wms.Core.Domain.Shared** | Result / Enums / Constants / IAuditable / IUnitOfWork（从 Domain 抽取） | platform |
| **Wms.Core.Domain** | 全部领域实体（按 Warehouse/Container/Material/Transport/Flow 等 12 个 Bounded Context 子目录划分，**69 个实体文件**；整个项目含 Enums/Requests/Constants/Services 等共 116 个 .cs；**2026-07 增量**：新增 ProcessRoute Bounded Context，6 实体 + 1 值对象 ProcessRouteGraph）、Domain 接口 | platform |
| **Wms.Core.Application.Contracts** | 21 个 Ports 接口（纯接口） | platform |
| **Wms.Core.Application** | DTOs、Jobs、请求/响应模型 | platform |
| **Wms.Core.Engine** | FlowContext、IFlowEngine、INodeHandler、25 节点处理器（Phase 0 时 22 个 + 2026-07 工艺路线相关 3 个：AdvanceOperation/ProcessTagVerification/VerifyProcessSteps）、库位分配规则（核心 IP） | platform |
| **Wms.Core.Logging** | 日志领域接口（如 `IInterfaceLogStore`）、日志 DTO | platform |
| **Wms.Core.Infrastructure** | WmsDbContext + **WmsLogDbContext + LogMigrations** + 61 Configurations + 70 DbSet + Repositories 实现 + Services 实现 + Clients + Caching | **project** |
| **Wms.Core.WebApi** | Controllers、Middleware、Program.cs（宿主） | **project** |
| **Wms.Core.Customer.XXX** | 客户定制（可选） | **project** |

**依赖方向**：`project.Infrastructure → platform.Engine → platform.Application → platform.Application.Contracts → platform.Domain → platform.Domain.Shared`

**关键决策**：WcsGateway 不整体进底层。原因：WCS 通信涉及具体客户设备对接，本身就有"项目定制"成分。处理方式：
- 抽象层（`IWcsClient` / `IWcsTaskBridge` / `IMesClient` / `IHangKeClient`）放 `Application.Contracts`（底层）
- 实现层（`DefaultWcsClient` / `DatabaseWcsTaskBridge` 等）留 `Infrastructure`（项目层）

**关键决策**：`WmsLogDbContext` 留 Infrastructure（项目层），不进底层 Logging 项目。原因：DbContext 是基础设施关注点，应与 `WmsDbContext` 保持一致的分层归属；底层项目不应直接依赖 EF Core 具体实现（否则底层变"重"）。处理方式：
- Logging 底层项目只含日志领域接口（如 `IInterfaceLogStore`）和 DTO
- `WmsLogDbContext` + `LogMigrations` 留 `Infrastructure`（项目层），与 `WmsDbContext` 同处
- `InterfaceLog` 实体已在 Domain/Entities/Transport（底层 Domain），无需迁移

**关键决策**：工艺路线模块（ProcessRoute）按方案默认规则随 Domain 进底层。具体切分：
- 进底层 platform：Domain 实体（[ProcessRoute/Version/Step/Transition/MaterialBinding/UnitloadProcessRouteLog](../src/Wms.Core.Domain/Entities/ProcessRoute/) 6 个 + [ProcessRouteGraph](../src/Wms.Core.Domain/ValueObjects/ProcessRouteGraph.cs) 值对象）、`IProcessRouteService` 接口（Phase 2 时随 Application.Contracts）
- 留项目层 project：`ProcessRouteService` 实现、6 个 EF Configuration、[ProcessRouteController.cs](../src/Wms.Core.WebApi/Controllers/Sys/ProcessRouteController.cs)、[ProcessRouteSeeder.cs](../src/Wms.Core.WebApi/Services/ProcessRouteSeeder.cs)、[DbInitializer.cs](../src/Wms.Core.WebApi/Services/DbInitializer.cs) 中的建表 SQL
- **客户特异性通过数据配置实现，不破坏底层稳定性**：电池行业工序（化成、分容、OCV3/4 等）只在 Seeder 种子数据中体现，新客户通过覆盖项目层 Seeder + 修改 MaterialBinding 表即可定制，无需触碰底层 .cs

---

## 三、必须的前置重构（不可跳过）

### 重构 A：引入 IUnitOfWork / IFlowDbContext 抽象

**目的**：让 FlowContext 和 22 个节点处理器（Phase 0 时；2026-07 增至 25 个）不再直接依赖 `WmsDbContext` 具体类型。

**A.1** 在 `Wms.Core.Domain.Shared/Abstractions/IUnitOfWork.cs` 定义：

```csharp
namespace Wms.Core.Domain.Shared.Abstractions;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
```

**A.2** 在 `Wms.Core.Application.Contracts/Persistence/IFlowDbContext.cs` 定义节点处理器实际需要的 DbSet（不是全部 70 个，而是节点实际用到的子集，初步估计 15-20 个）。**最终清单以 Phase 0 任务 6 的 grep 结果为准**（grep 22 个节点处理器（Phase 0 时）的 `context.Db.` 引用）。下方示例仅供示意。

```csharp
public interface IFlowDbContext
{
    DbSet<Location> Locations { get; }
    DbSet<Unitload> Unitloads { get; }
    DbSet<UnitloadItem> UnitloadItems { get; }
    DbSet<UnitloadItemDetail> UnitloadItemDetails { get; }
    DbSet<UnitloadOp> UnitloadOps { get; }
    DbSet<TransTask> TransTasks { get; }
    DbSet<Stock> Stocks { get; }
    DbSet<Materials> Materials { get; }
    DbSet<Flow> Flows { get; }
    DbSet<FlowInstance> FlowInstances { get; }
    DbSet<FlowNodeLog> FlowNodeLogs { get; }
    DbSet<FlowTemplate> FlowTemplates { get; }
    DbSet<LocationOp> LocationOps { get; }
    DbSet<LocationAllocRuleStat> LocationAllocRuleStats { get; }
    // ... 根据节点处理器实际使用情况逐步添加
}
```

> ⚠️ 上表为示意清单。**实际清单必须由 Phase 0 任务 6 的 grep 结果确定**，避免遗漏节点用到的 DbSet。建议同时保留一个兜底方法，防止清单遗漏导致节点编译失败：
>
> ```csharp
> // 兜底：允许节点访问未在接口中显式列出的 DbSet
> DbSet<T> Set<T>() where T : class;
> ```

**A.3** [FlowContext.cs](../src/Wms.Core.Infrastructure/Flow/FlowContext.cs) 的 `public WmsDbContext Db` 改为：

```csharp
public IFlowDbContext Db { get; }
public IUnitOfWork UnitOfWork { get; }

public FlowContext(IFlowDbContext db, IUnitOfWork unitOfWork)
{
    Db = db ?? throw new ArgumentNullException(nameof(db));
    UnitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
}
```

由于 IFlowDbContext 暴露同名 DbSet 属性，**22 个节点处理器代码不需要改**，只需重新编译。

**A.4** [WmsDbContext.cs](../src/Wms.Core.Infrastructure/Persistence/WmsDbContext.cs) 实现两个接口：

```csharp
public class WmsDbContext : DbContext, IFlowDbContext, IUnitOfWork
{
    // 现有 DbSet 不变，IFlowDbContext 的属性由现有 DbSet 隐式实现

    // IUnitOfWork 实现
    public Task BeginTransactionAsync(CancellationToken ct = default)
        => Database.BeginTransactionAsync(ct);

    public async Task CommitAsync(CancellationToken ct = default)
    {
        await SaveChangesAsync(ct);
        await Database.CommitTransactionAsync(ct);
    }

    public Task RollbackAsync(CancellationToken ct = default)
        => Database.RollbackTransactionAsync(ct);
}
```

**A.5** DI 注册（[ServiceCollectionExtensions.cs](../src/Wms.Core.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs)）：

```csharp
services.AddScoped<IFlowDbContext>(sp => sp.GetRequiredService<WmsDbContext>());
services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<WmsDbContext>());
```

**影响范围**：FlowContext.cs、FlowEngineService.cs、DI 注册扩展方法。22 个节点处理器（Phase 0 时）仅重新编译，代码不变。

### 重构 B：EF Core Configuration 跨程序集（可跳过）

WmsDbContext 留在 Infrastructure（项目层），59 个 Configuration 也留 Infrastructure，**当前阶段不需要改动**。未来如果 Engine 需要定义自己的 Configuration，再扩展 `OnModelCreating` 扫描多个程序集：

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(WmsDbContext).Assembly);
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(IFlowDbContext).Assembly);
}
```

### 重构 C：WebApi 层依赖倒置修复（必须做）

**问题**：两个接口的实现在 WebApi 层，但被 Infrastructure 层引用。这违反了分层方向。分离后 Infrastructure 无法引用 WebApi。

需移动的文件：

| 文件 | 从 | 到 |
|------|----|----|
| [BackgroundTaskQueue.cs](../src/Wms.Core.WebApi/Services/BackgroundTaskQueue.cs) | WebApi/Services | Infrastructure/Services |
| [BackgroundTaskQueueHostedService.cs](../src/Wms.Core.WebApi/Services/BackgroundTaskQueueHostedService.cs) | WebApi/Services | Infrastructure/Services |
| [TranslationService.cs](../src/Wms.Core.WebApi/Services/TranslationService.cs) | WebApi/Services | Infrastructure/Services |

移动后 Infrastructure.csproj 需补 `Microsoft.Extensions.Hosting.Abstractions` 包引用。

### 重构 D：事务统一管理（必须做）

**D.1** [FlowEngineService.cs](../src/Wms.Core.Infrastructure/Services/FlowEngineService.cs) 的 `ExecuteAsync` 采用**分段事务**模式，而非"整个请求阶段一个大事务"。

> ⚠️ **为何不能用一个大事务包裹整个请求阶段**：请求阶段包含多个调用外部系统的节点（见下方"事务边界与外部调用协调规则"）。整个阶段一个大事务会导致：
> 1. **长事务 + 锁争用**：事务持有期间跨网络调用，WCS/MES 响应慢则事务长时间打开
> 2. **不可恢复的不一致**：外部调用成功（如 WCS 已实际派出搬运任务）但后续节点失败回滚 → DB 回滚了，外部系统却已执行 → 无法通过 Rollback 恢复

**事务边界与外部调用协调规则**（必须遵守）：

Flow 节点分为两类：
- **DB 节点**（只读写数据库）：`ValidateParamsHandler`、`FindUnitloadHandler`、`AllocateLocationHandler`、`UpdateUnitloadHandler`、`RecordFlowHandler` 等 —— 可在事务内
- **外部调用节点**（调用 WCS / MES / 行架 / HTTP）：`SendWcsTaskHandler`、`NotifyHangKeHandler`、`UploadMesHandler`、`HttpCallbackHandler` —— **必须在事务外**

`IsTransactionBoundary` 的语义从"中间 SaveChanges 点"**升级**为"事务提交点 + 外部调用前置提交点"：在到达外部调用节点之前，其前的 `IsTransactionBoundary` 必须先 Commit 本地数据库状态，确保即使后续外部调用失败或后续节点失败，已发出的外部调用对应的数据也已落库。

分段事务伪代码：

```csharp
public async Task<WcsResult> ExecuteAsync(FlowTemplate template, FlowContext context)
{
    await context.UnitOfWork.BeginTransactionAsync(); // 开启第一段
    foreach (var node in template.Nodes)
    {
        if (node.IsTransactionBoundary)
        {
            // 提交当前段：把累积的本地写操作持久化，为后续外部调用提供"已落库"保证
            await context.UnitOfWork.CommitAsync();
            await context.UnitOfWork.BeginTransactionAsync(); // 开启下一段
        }
        try
        {
            await ExecuteNodeAsync(node, context);
        }
        catch
        {
            await context.UnitOfWork.RollbackAsync(); // 只回滚当前段
            throw;
        }
    }
    await context.UnitOfWork.CommitAsync(); // 提交最后一段
}
```

**关键约束**：Flow 模板设计时，外部调用节点必须位于 `IsTransactionBoundary=true` 之后（即先提交本地状态，再发外部调用）。这样：
- 外部调用前的数据已 Commit，外部调用失败也不会丢失前置业务状态
- 外部调用成功后若后续 DB 节点失败，已发出的外部调用对应的数据仍可追溯（通过补偿机制或人工处理）

**模板审计任务**（Phase 0 必做）：grep 所有 FlowTemplate 配置（`Flows` / `FlowTemplates` 表 + JSON 定义），确认 `SendWcsTaskHandler` / `NotifyHangKeHandler` / `UploadMesHandler` / `HttpCallbackHandler` 前都设置了 `IsTransactionBoundary=true`。若未设置，需先修正模板再启用事务保护。

**D.2** [UnitloadService.cs](../src/Wms.Core.Infrastructure/Services/UnitloadService.cs) 中 6 处同步 `BeginTransaction()` 改为 `await BeginTransactionAsync()`：
- 第 114、361、567、962、1026、1175 行
- 同步将 `using var transaction` 改为 `await using var transaction`

**D.3** [PortService.cs](../src/Wms.Core.Infrastructure/Services/PortService.cs) 第 52 行同理。

---

## 四、打包与同步脚本

### 4.1 底层 `pack.ps1`

```powershell
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0",
    [string]$ArtifactsDir = "$PSScriptRoot\artifacts"
)

$ErrorActionPreference = "Stop"

# 版本号写入
$versionJson = @{
    version = $Version
    buildTime = (Get-Date -Format "o")
    commit = (git rev-parse HEAD 2>$null)
    branch = (git rev-parse --abbrev-ref HEAD 2>$null)
} | ConvertTo-Json -Depth 3
$versionJson | Out-File "$ArtifactsDir\version.json" -Encoding UTF8

Write-Host "[pack] Building platform DLLs v$Version..."

$projects = @(
    "src\Wms.Core.Domain.Shared",
    "src\Wms.Core.Domain",
    "src\Wms.Core.Application.Contracts",
    "src\Wms.Core.Application",
    "src\Wms.Core.Engine",
    "src\Wms.Core.Logging"
)

foreach ($proj in $projects) {
    $csproj = Join-Path $PSScriptRoot $proj
    Write-Host "[pack] Building $csproj"

    dotnet build $csproj -c $Configuration `
        /p:Version=$Version `
        /p:EmbedAllSources=true `
        /p:DebugType=embedded `
        /p:IncludeSymbols=false `
        /p:CopyLocalLockFileAssemblies=true

    if ($LASTEXITCODE -ne 0) { throw "Build failed for $proj" }
}

# 收集 DLL + PDB 到 artifacts
if (Test-Path $ArtifactsDir) { Remove-Item $ArtifactsDir -Recurse -Force }
New-Item -ItemType Directory -Path $ArtifactsDir -Force | Out-Null

foreach ($proj in $projects) {
    $projName = Split-Path $proj -Leaf
    $outputPath = Join-Path $PSScriptRoot "$proj\bin\$Configuration\net8.0"
    Get-ChildItem "$outputPath\$projName.dll" | Copy-Item -Destination $ArtifactsDir
    Get-ChildItem "$outputPath\$projName.pdb" -ErrorAction SilentlyContinue | Copy-Item -Destination $ArtifactsDir
}

Write-Host "[pack] Done. Version=$Version"
```

**关键属性说明**：
- `EmbedAllSources=true`：把 .cs 源码嵌入到 PDB 中，调试器能步入底层代码看源码
- `DebugType=embedded`：源码嵌入模式
- 项目层开发者拿到的 DLL 没有 .cs 文件，但调试时能看到源码行号和代码内容

### 4.2 项目层 csproj 引用方式

```xml
<ItemGroup>
  <Reference Include="Wms.Core.Domain.Shared">
    <HintPath>..\lib\platform\Wms.Core.Domain.Shared.dll</HintPath>
    <Private>true</Private>
  </Reference>
  <Reference Include="Wms.Core.Domain">
    <HintPath>..\lib\platform\Wms.Core.Domain.dll</HintPath>
    <Private>true</Private>
  </Reference>
  <Reference Include="Wms.Core.Application.Contracts">
    <HintPath>..\lib\platform\Wms.Core.Application.Contracts.dll</HintPath>
    <Private>true</Private>
  </Reference>
  <Reference Include="Wms.Core.Application">
    <HintPath>..\lib\platform\Wms.Core.Application.dll</HintPath>
    <Private>true</Private>
  </Reference>
  <Reference Include="Wms.Core.Engine">
    <HintPath>..\lib\platform\Wms.Core.Engine.dll</HintPath>
    <Private>true</Private>
  </Reference>
  <Reference Include="Wms.Core.Logging">
    <HintPath>..\lib\platform\Wms.Core.Logging.dll</HintPath>
    <Private>true</Private>
  </Reference>
</ItemGroup>
```

不用 `<PackageReference>`（不需要 NuGet 源），不用 `<ProjectReference>`（不能看到源码）。

### 4.3 项目层 `sync-platform.ps1`

```powershell
[CmdletBinding()]
param(
    [string]$PlatformRepo = "..\wms-platform",
    [string]$LibDir = "$PSScriptRoot\lib\platform"
)

$ErrorActionPreference = "Stop"

$sourceArtifacts = Join-Path $PlatformRepo "artifacts"

if (-not (Test-Path $sourceArtifacts)) {
    Write-Host "[sync] Artifacts dir not found, pulling from artifacts branch..."
    Push-Location $PlatformRepo
    git fetch origin artifacts
    git checkout origin/artifacts -- artifacts
    Pop-Location
}

if (-not (Test-Path $LibDir)) {
    New-Item -ItemType Directory -Path $LibDir -Force | Out-Null
}

# 对比版本
$sourceVersion = Get-Content "$sourceArtifacts\version.json" | ConvertFrom-Json
$destVersionFile = Join-Path $LibDir "version.json"

if (Test-Path $destVersionFile) {
    $destVersion = Get-Content $destVersionFile | ConvertFrom-Json
    if ($sourceVersion.version -eq $destVersion.version -and $sourceVersion.commit -eq $destVersion.commit) {
        Write-Host "[sync] Already up to date (v$($sourceVersion.version))"
        return
    }
}

Write-Host "[sync] Updating platform DLLs to v$($sourceVersion.version)..."
Copy-Item "$sourceArtifacts\*.dll" $LibDir -Force
Copy-Item "$sourceArtifacts\*.pdb" $LibDir -Force -ErrorAction SilentlyContinue
Copy-Item "$sourceArtifacts\version.json" $LibDir -Force

Write-Host "[sync] Done."
```

---

## 五、渐进式路线图

### Step 0：固化方案文档（本文档）

将本方案以正式文档形式保存到 `Wms.Net8/docs/`，与原文档 [WMS层级分离与单独编译引用-架构建议.md](WMS层级分离与单独编译引用-架构建议.md) 形成"渐进分层 vs 二进制封装"双方案对照。

### Phase 0：前置重构（不可跳过）

**目标**：解决 4 个核心技术债 + 3 项前置审计，为物理分离铺路。

任务清单：
1. 重构 A：IUnitOfWork + IFlowDbContext 抽象 + FlowContext 改造 + DI 注册
2. 重构 C：移动 BackgroundTaskQueue 等 3 个文件到 Infrastructure
3. 重构 D.1：FlowEngineService.ExecuteAsync **分段事务**保护（不是整体大事务，见 D.1 事务边界与外部调用协调规则）
4. 重构 D.2 + D.3：7 处同步 BeginTransaction 改异步
5. **审计 28 处 async BeginTransaction 的边界**：grep `BeginTransactionAsync(` 全部 22 个文件，确认每处都有对应的 Commit/Rollback 且事务范围合理（不长于必要），避免现有 async 调用存在未发现的长事务
6. **生成 IFlowDbContext 精确 DbSet 清单**：grep 22 个节点（Phase 0 时）的 `context.Db.` 调用，汇总实际用到的 DbSet，作为 A.2 接口的最终定义依据（A.2 示例清单仅供示意，以本次 grep 结果为准）
7. **审计 FlowTemplate 中外部调用节点的事务边界**：确认 `SendWcsTaskHandler` / `NotifyHangKeHandler` / `UploadMesHandler` / `HttpCallbackHandler` 前都设置了 `IsTransactionBoundary=true`（D.1 约束，否则分段事务无法保证外部调用前数据已落库）

**风险**：中。事务行为变更可能影响并发性能，需压测验证。建议在分支开发，灰度环境验证后再合入。

### Phase 1：抽取 Domain.Shared（低风险）

新项目 `Wms.Core.Domain.Shared` 装载：
- `Common/Result.cs`、`Common/ResultCodeTypes.cs`
- `Constants/` 全部
- `Enums/` 全部
- `Interfaces/IAuditable.cs`
- `Abstractions/IUnitOfWork.cs`（Phase 0 重构产物）

Domain 引用 Shared。仍在单 sln 内，用 ProjectReference。

### Phase 2：抽取 Application.Contracts（低风险）

`Wms.Core.Application/Ports/` 下 21 个接口移到新项目 `Wms.Core.Application.Contracts`：
- `IWcsClient` / `IWcsTaskBridge` / `IMesClient` / `IHangKeClient`
- `ICtaskDbService` / `ITaskCompletionHandler`
- `IBackgroundTaskQueue` / `ICacheService` / `IDistributedLockService`
- `IInventoryCacheService` / `IDapperReadService`
- `IAuthService` / `ILocationService` / `IUnitloadService` 等
- `Persistence/IFlowDbContext.cs`（Phase 0 重构产物）

Application 引用 Contracts，Infrastructure 改为引用 Contracts 而非整个 Application。

### Phase 3：抽取 Engine（中风险）

迁移内容：

| 源路径（Infrastructure 内） | 目标路径（Engine 内） |
|------------------------------|----------------------|
| `Flow/FlowContext.cs` | `Flow/FlowContext.cs` |
| `Flow/IFlowEngine.cs` + `INodeHandler.cs` | `Flow/` |
| `Flow/Nodes/` 25 个处理器（Phase 0 时 22 + 工艺路线 3） | `Flow/Nodes/` |
| `Services/FlowEngineService.cs` | `FlowEngineService.cs` |
| `Tasks/Rules/`（11 条 SSRule：SSRule01-10 + SSRule04HcLx 客户变体；4 条 SDRule） | `Rules/`（仅通用规则；客户特定规则留项目层，详见下方"规则分层说明"） |
| `Domain/Tasks/LocationAllocationEngine.cs` + `Domain/Tasks/ILocationAllocationRule.cs` | 无需迁移（已在 Domain 层，Engine 引用 Domain 即可直接使用） |

Engine 引用 Application.Contracts + Domain + Shared，**不引用 Infrastructure**。

#### 规则分层说明（客户定制边界）

库位分配规则（SSRule/SDRule）存在强客户定制属性（如 `SSRule04HcLx` 就是某客户的行列限制变体），全部放底层会导致每次客户定制都要更新底层 DLL，违背"底层较少改"约束。采用分层策略：

| 层级 | 内容 | 所在仓库 |
|------|------|----------|
| 接口 + 引擎本体 | `ILocationAllocationRule`、`LocationAllocationEngine`（已在 Domain/Tasks/） | platform |
| 通用规则 | SSRule01-03 等基础策略（与具体客户无关） | platform（Engine/Rules/） |
| **客户特定规则** | **SSRule04HcLx 等带客户/场景标识的变体** | **project** |

Engine 提供规则注册扩展点，项目层按需注册客户规则：

```csharp
public class EngineOptions
{
    public List<Type> AdditionalLocationRules { get; } = new();
    public EngineOptions AddLocationRule<T>() where T : ILocationAllocationRule
    {
        AdditionalLocationRules.Add(typeof(T));
        return this;
    }
}

public static IServiceCollection AddWmsEngine(this IServiceCollection services, Action<EngineOptions>? configure = null)
{
    services.AddScoped<IFlowEngine, FlowEngineService>();
    // ... 节点处理器扫描注册 ...
    var options = new EngineOptions();
    configure?.Invoke(options);
    foreach (var ruleType in options.AdditionalLocationRules)
        services.AddScoped(typeof(ILocationAllocationRule), ruleType);
    return services;
}

// 项目层注册客户规则
services.AddWmsEngine(opt => opt.AddLocationRule<SSRule04HcLx>());
```

新建 `Engine/DependencyInjection/EngineExtensions.cs`，用程序集扫描自动注册 25 个节点处理器（替换 [ServiceCollectionExtensions.cs:77-101](../src/Wms.Core.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs#L77-L101) 的手写 25 行）：

```csharp
public static IServiceCollection AddWmsEngine(this IServiceCollection services)
{
    services.AddScoped<IFlowEngine, FlowEngineService>();
    var assembly = typeof(EngineExtensions).Assembly;
    var handlerTypes = assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false }
            && typeof(INodeHandler).IsAssignableFrom(t));
    foreach (var type in handlerTypes)
        services.AddScoped(typeof(INodeHandler), type);
    return services;
}
```

### Phase 4：物理仓库分离（最终目标，可回退）

步骤：
1. 创建底层仓库 `wms-platform`，拷贝 6 个底层项目代码
2. 编写 `pack.ps1`，验证 DLL 产出
3. 项目层 csproj 切换：`ProjectReference` → `Reference HintPath`
4. 编写 `sync-platform.ps1`，验证同步
5. 拆 sln 文件，配置权限隔离

**回退方式**：csproj 中 `<Reference HintPath>` 改回 `<ProjectReference>` 即可恢复单体仓库。

---

## 六、必须做 vs 可跳过

| 步骤 | 状态 | 理由 |
|------|------|------|
| 重构 A（IUnitOfWork / IFlowDbContext） | **必须** | 否则 Engine 直接依赖 WmsDbContext，无法拆分 |
| 重构 B（EF Configuration 跨程序集） | 跳过 | WmsDbContext 和 Configuration 同在 Infrastructure，无问题 |
| 重构 C（WebApi 依赖倒置） | **必须** | 否则 Infrastructure 无法引用 WebApi，编译断 |
| 重构 D（分段事务 + 同步改异步） | **必须** | 数据一致性风险 + IUnitOfWork 配套 |
| Phase 1（Domain.Shared） | **推荐** | 低风险高收益 |
| Phase 2（Application.Contracts） | **推荐** | 接口/实现分离 |
| Phase 3（Engine 抽取） | **必须** | 核心价值所在 |
| Phase 4（物理仓库分离） | 最终目标 | 前三阶段完成后风险大幅降低 |

---

## 七、与原方案的差异总结

| 维度 | 原文档方案 | 本方案 |
|------|------------|--------|
| 核心目标 | 编译加速 + 代码组织 | **内部权限隔离 + 代码保护** |
| 引用方式 | ProjectReference（源码级） | **Reference HintPath（DLL 二进制）** |
| 仓库结构 | 单仓库单 sln | **双仓库：platform + project** |
| 同步机制 | 无需（同仓库） | **artifacts 分支 + sync 脚本** |
| 前置重构 | 未提及 | **IUnitOfWork + IFlowDbContext + 依赖倒置 + 分段事务保护**（必须） |
| Engine 边界 | 仅 Flow Engine | Flow Engine + 引擎本体 + 通用库位分配规则（**客户特定规则留项目层**） |
| WcsGateway | 拆为独立项目 | **不整体拆**：抽象在底层 Contracts，实现留项目层 Infrastructure |
| 调试体验 | ProjectReference 原生支持 | **EmbedAllSources 嵌入源码**（接近原生体验） |
| 节点处理器数 | 26（有误） | 22（Phase 0 时）→ 25（2026-07 含工艺路线相关 3 个） |
| 工厂参考 | 未明确 | ABP Framework、Orchard Core 的 Platform/Shell 分层 |

---

## 八、验证方式

### Phase 0 验证
- `dotnet build` 编译通过
- grep `BeginTransaction\(\)` （同步、非 Async）零结果
- 全流程回归：入库 / 出库 / 移库 / 质检 / 盘点 各跑一遍
- FlowEngineService.ExecuteAsync 中间节点失败时，前面 SaveChanges 的数据能回滚（构造失败用例验证）

### Phase 1 验证
- Domain.Shared 项目零 ProjectReference
- 下游项目编译通过

### Phase 2 验证
- Application.Contracts 只含接口，零实现代码
- Infrastructure 改为引用 Contracts 后编译通过

### Phase 3 验证
- Engine 项目零引用 Infrastructure 程序集（用 `dotnet list package --include-transitive` 核查）
- 改 Engine 内任意节点处理器，增量编译只触发 Engine + WebApi，不触发 Infrastructure
- 全流程回归测试通过

### Phase 4 验证
- 底层仓库独立编译产出 6 个 DLL + PDB + version.json
- 项目层用 HintPath 引用 DLL，编译通过
- VS 调试器能步入底层源码（验证 EmbedAllSources 生效：F11 步入能看到源码行号）
- `sync-platform.ps1` 能识别版本变化并更新
- 项目层仓库 `git ls-files | grep "\.cs$" | xargs grep -l "Wms.Core.Engine"` 不应返回底层 .cs 源文件

---

## 九、关键文件清单

实施本方案时最关键的 8 个文件（按优先级排序）：

1. [FlowContext.cs](../src/Wms.Core.Infrastructure/Flow/FlowContext.cs) - 核心改造目标：WmsDbContext → IFlowDbContext + IUnitOfWork，影响 22 个节点处理器（Phase 0 时；2026-07 后为 25 个）
2. [WmsDbContext.cs](../src/Wms.Core.Infrastructure/Persistence/WmsDbContext.cs) - 实现 IFlowDbContext + IUnitOfWork，OnModelCreating 保持原样
3. [FlowEngineService.cs](../src/Wms.Core.Infrastructure/Services/FlowEngineService.cs) - 请求阶段加事务保护，迁移到 Engine
4. [ServiceCollectionExtensions.cs](../src/Wms.Core.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs) - DI 注册重组，删除 Engine 相关注册
5. [Program.cs](../src/Wms.Core.WebApi/Program.cs) - 改为调用各模块的 Add* 扩展方法
6. [UnitloadService.cs](../src/Wms.Core.Infrastructure/Services/UnitloadService.cs) - 6 处同步 BeginTransaction 改异步
7. [BackgroundTaskQueue.cs](../src/Wms.Core.WebApi/Services/BackgroundTaskQueue.cs) - 从 WebApi 移到 Infrastructure
8. [Result.cs](../src/Wms.Core.Domain/Common/Result.cs) - Phase 1 起点，迁移到 Domain.Shared

---

## 十、风险与缓解

### 风险 1：事务行为变更导致死锁或超时
**触发**：Phase 0 的 D.1 重构把请求阶段从事务边界模式改为完整事务包裹，长流程可能持有事务更久。
**缓解**：在分支开发，灰度环境压测后再合入；保留 IsTransactionBoundary 作为事务内 SaveChanges 点。

### 风险 2：DLL 版本不一致
**触发**：项目层引用过期底层 DLL，与最新基础设施代码不匹配。
**缓解**：sync 脚本对比 version.json，CI 中加入版本一致性检查。

### 风险 3：调试体验下降
**触发**：底层代码异常时看不到源码。
**缓解**：`EmbedAllSources=true` 嵌入源码到 PDB；如有需要可叠加 SourceLink 指向内部源码服务器。

### 风险 4：第三方包传递依赖丢失
**触发**：底层 DLL 依赖的第三方包在项目层未还原。
**缓解**：pack.ps1 中设置 `CopyLocalLockFileAssemblies=true`；或在底层项目生成 packages.lock.json，sync 时一并同步。

### 风险 5：节点处理器遗漏 DbSet
**触发**：IFlowDbContext 清单不全，某节点用到的 DbSet 未暴露。
**缓解**：Phase 0 先用 grep 全面扫描 22 个节点（Phase 0 时）的 `context.Db.` 调用；保留 `DbSet<T> Set<T>()` 方法作为兜底。

### 风险 6：artifacts 分支的 Git 仓库膨胀
**触发**：DLL 是二进制 blob，每次更新产生全新 git 对象，无法 delta 压缩。几十次发布后 artifacts 分支体积膨胀，底层仓库 clone/fetch 变慢。
**缓解**（三选一）：
1. **orphan 分支 + force push**：artifacts 作为孤儿分支，每次发布覆盖整个分支历史，不累积（`git push origin artifacts --force`）
2. **定期清理**：用 `git filter-repo` 周期性清理 artifacts 分支历史
3. **替代方案**：DLL 不入 git，通过共享文件夹 / 内网文件服务器 / SMB 同步（内部 1-2 人维护场景最简单，彻底绕开 git 二进制问题）

---

## 附录 A：Phase 0 执行记录（2026-07-10 完成）

分支 `feature/phase0-refactor`，9 个 commit（`19de7b1` → `e6d5fb3`），编译 0 errors，25 单测通过 + 4 skipped。Final review 无阻塞。

### 与计划的偏差（执行中发现）

1. **IFlowDbContext 规模扩大**（A.2）
   - 计划：约 15-20 个 DbSet
   - 实际：17 个 DbSet（Locations/Unitloads/UnitloadItems/UnitloadItemDetails/TransTasks/Flows/FlowInstances/FlowNodeLogs/FlowTemplates/FlowNodes + Racks/Laneways/UnitloadOps/ArchivedTask/ArchivedUnitload/ArchivedUnitloadItem/ArchivedUnitloadItemDetail）+ Set<T> 兜底 + Database + SaveChangesAsync/SaveChanges + Entry<TEntity>
   - 原因：LocationAllocator 辅助类（被节点处理器调用）用了 Racks/Laneways/Archived*/UnitloadOps 等，必须通过接口暴露
   - 教训：IFlowDbContext 设计时不能只看节点处理器，还要看节点调用的辅助类（LocationAllocator 等）

2. **LocationAllocator 全类迁移到 IFlowDbContext**（A 的范围扩展）
   - 计划：只改 FlowContext + 7 处实例化点
   - 实际：LocationAllocator 的 8 个静态方法 + 构造函数 + 字段都改为 IFlowDbContext（因为节点处理器把 context.Db 传给 LocationAllocator）

3. **MergeUnitloadsHandler / WasteDisposalCaptureNode 移除内部事务**（D.1 运行时冲突的解决）
   - 计划：确保这两个节点前有 IsTransactionBoundary，让外层段先 Commit，节点可自建事务
   - 实际：直接移除节点内部自建事务，依赖外层分段事务保护
   - 语义变化：节点失败回滚范围从"节点内"扩大到"整个段（回到上个 boundary）"
   - WasteDisposalCaptureNode 的 HangKe.CancelTrayAsync 外部调用现在在最终事务提交前执行（模板 9 IsActive=false，暂可接受；长期应拆分为 DB 清理 + PostTransaction 通知两节点）

4. **重构 C 只移 2 文件**（执行确认）
   - BackgroundTaskQueue + HostedService 移到 Infrastructure/Services
   - TranslationService 留 WebApi（HTTP 上下文关注点，依赖 LanguagePackHelper，留符合 DIP）

5. **Seeder 补 4 处 IsTransactionBoundary**（任务 11）
   - INBOUND_STANDARD_REQUEST / INBOUND_DOUBLE_REQUEST / OUTBOUND_STANDARD_REQUEST / MOVE_STANDARD_REQUEST 的 UpdateLocationCount 节点设为 boundary（SendWcsTask 前的提交点）
   - 已部署环境需手工核对（seeder 增量同步不覆盖已有数据的 IsTransactionBoundary 字段）

6. **LocationAllocator 补 2 处 SaveChangesAsync**（commit `9dd99f2`，分段事务落地后发现）
   - `CleanupEmptyTrayItemsAsync`（第 702-703 行）：4 条 FK 清理 SQL 之后、`ArchiveUnitloadAsync` 之前补 `SaveChangesAsync`
   - `SplitUnitloadAsync`（第 768-771 行）：items 快照之后、4 条 FK 清理 SQL 之前补 `SaveChangesAsync`
   - 原因：分段事务落地后，节点不再自建事务，ChangeTracker 在多个节点间累积 Added 实体；当后续节点的 `ExecuteSqlRawAsync` 绕过 ChangeTracker 清理 FK 引用时，未 flush 的实体导致 UPDATE 命中 0 行或 DELETE 触发 FK 约束冲突
   - 在事务内调用安全：数据仍受当前段保护，失败可 Rollback
   - 主要触发场景：出库完成阶段的 `CleanupEmptyTray` / `SplitUnitload` 节点（活跃模板必经路径）

### 合并状态（2026-07-10 已合并 main）

PR #1 已通过 "Create a merge commit" 方式合并到 main（merge commit `de17e36`），全部 12 个 commit hash 保留。下述 4 项 Gate 检查均已通过：

- [x] **全流程手工回归测试**：入库（单托盘/双叉）、出库、移库、叠盘、排废；成功路径数据一致 + 失败路径未提交段数据被回滚 —— 未发现异常
- [x] Review 关键 commit：`dba93dc`（分段事务）+ `e6d5fb3`（移除内部事务 + seeder 补 boundary）+ `9dd99f2`（LocationAllocator SaveChanges 修复）—— 4 个 commit checklist 全部通过
- [x] 已部署环境核对 4 个模板的 IsTransactionBoundary 配置 —— 一致
- [ ] （可选）引入 EF Core InMemory，让 4 个 skipped 测试可运行，验证 ExecuteAsync 分段事务调用序列 —— **推迟到 Phase 1 后**（FlowEngineService 构造函数将在 Phase 1 解耦为 IFlowDbContext，届时再做更省事）

### 下一步

Phase 0 已合并 main，可进入 Phase 1（Domain.Shared 抽取，低风险）。Phase 0 的 IUnitOfWork/IFlowDbContext 已暂放 Domain/Application，Phase 1 时迁移到 Domain.Shared/Application.Contracts。

---

## 附录 B：Phase 0 后新增模块评估（2026-07-15）

Phase 0 合并 main（2026-07-10）后，项目继续迭代，新增了工艺路线模块和扩展了 WCS 请求处理器系列。本附录评估这些新增内容对原方案的影响，确认无需调整 Phase 1-4 路径，仅修订若干计数与分层说明。

### B.1 长期计数错误的修正

本次修订发现并修正了一个长期存在的计数错误：**Phase 0 合并时实际只有 22 个 NodeHandler**，但原文档（含第三章调研发现、第五章 Phase 3 任务清单、第七章差异表等多处）长期记为 25 个。

修正依据：[ServiceCollectionExtensions.cs](../src/Wms.Core.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs#L89-L113) 当前注册了 25 个 INodeHandler，其中 [AdvanceOperationHandler](../src/Wms.Core.Infrastructure/Flow/Nodes/AdvanceOperationHandler.cs)、[ProcessTagVerificationHandler](../src/Wms.Core.Infrastructure/Flow/Nodes/ProcessTagVerificationHandler.cs)、[VerifyProcessStepsHandler](../src/Wms.Core.Infrastructure/Flow/Nodes/VerifyProcessStepsHandler.cs) 这 3 个属于工艺路线模块（B.2），是 Phase 0 之后增量。所以 Phase 0 时点 = 25 - 3 = 22 个。

本次修订对所有描述 Phase 0 时点状态的位置（第三章调研 #1/#6、重构 A、Phase 0 任务 #6、风险 6、关键文件 #1）改为"22 个节点处理器（Phase 0 时）"；对所有描述当前/未来状态的位置（第二章 Engine 行、Phase 3 迁移表、第七章差异表）改为"25 个节点处理器（Phase 0 时 22 + 工艺路线相关 3）"。

### B.2 工艺路线模块（ProcessRoute）

**覆盖范围**：跨层 Bounded Context。

| 层 | 内容 | 文件 |
|----|------|------|
| Domain | 6 实体 + 1 值对象 | [Entities/ProcessRoute/](../src/Wms.Core.Domain/Entities/ProcessRoute/)（ProcessRoute / Version / Step / Transition / MaterialBinding / UnitloadProcessRouteLog）+ [ValueObjects/ProcessRouteGraph.cs](../src/Wms.Core.Domain/ValueObjects/ProcessRouteGraph.cs) |
| Application | DTOs + Ports | [DTOs/ProcessRouteDtos.cs](../src/Wms.Core.Application/DTOs/ProcessRouteDtos.cs) + [Ports/IProcessRouteService.cs](../src/Wms.Core.Application/Ports/IProcessRouteService.cs) |
| Infrastructure | Service + 6 EF Configuration + 3 NodeHandler | [Services/ProcessRouteService.cs](../src/Wms.Core.Infrastructure/Services/ProcessRouteService.cs) + [Persistence/Configurations/ProcessRoute*.cs](../src/Wms.Core.Infrastructure/Persistence/Configurations/) + 3 个 Flow/Nodes/ |
| WebApi | Controller + Seeder + DbInitializer 建表 | [Controllers/Sys/ProcessRouteController.cs](../src/Wms.Core.WebApi/Controllers/Sys/ProcessRouteController.cs) + [Services/ProcessRouteSeeder.cs](../src/Wms.Core.WebApi/Services/ProcessRouteSeeder.cs) + [Services/DbInitializer.cs#L321](../src/Wms.Core.WebApi/Services/DbInitializer.cs#L321) |

**分层决策**：详见第二章新增的"关键决策：工艺路线模块"段落。**实体随 Domain 进底层 platform**，客户特异性通过数据配置解决（MaterialBinding 表 + 项目层 Seeder 覆盖）。

**对实施计划的影响**：
- **节点处理器数 22 → 25**：3 个新 NodeHandler（AdvanceOperation/ProcessTagVerification/VerifyProcessSteps）已预设命名空间 `Wms.Core.Engine.Nodes`，Phase 3 物理迁移成本与原有 22 个等价（仅 `git mv` + 调整 csproj，不改命名空间）
- **IFlowDbContext 不需要扩展 ProcessRoute DbSet**：`ProcessRouteService` 留项目层 Infrastructure，直接注入 `WmsDbContext` 即可，不进入 Engine 的 IFlowDbContext 清单
- **Phase 1（Domain.Shared 抽取）任务清单不变**：Phase 1 只装 Result/Enums/Constants/IAuditable/IUnitOfWork，与工艺路线无关

### B.3 IWcsRequestHandler 系列（13 个策略处理器）

**覆盖范围**：[Infrastructure/Handlers/WcsRequest/](../src/Wms.Core.Infrastructure/Handlers/WcsRequest/) 下 13 个文件，包括本次问到的 [VerfiyPalletTypeRequestHandler.cs](../src/Wms.Core.Infrastructure/Handlers/WcsRequest/VerfiyPalletTypeRequestHandler.cs) 及同类：VerfiyBatch / VerfiyLevel / VerfiyProcess / VerfiyProcessSteps / StackingPallet / Outbound / Inbound / InboundDouble / InboundEmpty / Move / WasteDisposal / WasteDisposalCapture。

**架构定位**：策略模式实现 [IWcsRequestHandler](../src/Wms.Core.Application/Handlers/WcsRequest/IWcsRequestHandler.cs)（**非 INodeHandler**），按 `RequestType` 字符串路由。是 [WcsController.cs](../src/Wms.Core.WebApi/Controllers/Api/WcsController.cs) 中 FlowEngine 模板匹配失败时的后备通道。

**分层决策**：**全部留项目层 Infrastructure**。理由：
1. 直接注入 `WmsDbContext`（具体类），不走 IFlowDbContext —— 与 Engine 抽象边界不符
2. 处理具体业务（叠盘/排废/各类验证），是项目层关注点
3. 接口已在 Application 层（Phase 2 时随 Application.Contracts 进底层），实现留项目层，符合 DIP

**对实施计划的影响**：**零影响**。本系列不参与 Phase 3 的 Engine 抽取，节点数不变。

### B.4 已识别但本次不修的技术债

| 技术债 | 影响 | 处理建议 |
|--------|------|----------|
| ProcessRoute 表通过 DbInitializer 手工 SQL 建表（[L321-472](../src/Wms.Core.WebApi/Services/DbInitializer.cs#L321)），未走 EF Migration | 数据库 schema 与 ModelSnapshot 不一致，新环境部署依赖脚本 | 不阻塞 Phase 1-4。建议作为独立技术债任务在 Phase 4 完成后处理 |
| `ProcessRouteService` 注入 `WmsDbContext` 而非抽象 | 项目层代码，本身不进底层，无拆分障碍 | 无需改 |
| 同目录 `LocationAllocator` 注入 `IFlowDbContext` 与 `IWcsRequestHandler` 注入 `WmsDbContext` 混用 | 看似不一致，实则是合理设计：前者随 Engine 进底层，后者留项目层 | 记录即可，无需改 |

### B.5 结论

- 节点处理器数：**Phase 0 时 22 个（原文档长期误记为 25）→ 2026-07 含工艺路线相关 3 个至 25 个**
- 工艺路线模块：**随 Domain 进底层**，客户特异性通过数据配置解决
- IWcsRequestHandler 系列：**全部留项目层**，不影响拆分
- Phase 1（Domain.Shared 抽取）路径与任务清单：**不变**，可立即启动

---

## 附录 C：Phase 1 执行记录（2026-07-15 完成）

Phase 0 合并 main（2026-07-10）后，按方案第五章 Phase 1 任务清单执行 Domain.Shared 抽取。本附录记录实际执行情况、与计划的偏差，以及下一步。

### C.1 实施概况

| 维度 | 实际情况 |
|------|---------|
| 实施日期 | 2026-07-15 |
| 仓库状态 | 当前为非 git 仓库（环境检测 "Is directory a git repo: No"），未走分支/PR 流程，直接在工作目录修改 |
| 新增项目 | `Wms.Core.Domain.Shared`（GUID `{D422C1CF-A8D8-4D36-BF14-24727F22E9ED}`） |
| 迁移文件数 | **16 个 .cs 文件**（与计划一致） |
| 下游源码改动 | **0 处**（命名空间保留策略生效） |
| 编译结果 | 5 个主项目 0 errors / 0 warnings；测试项目受 E-SafeNet 加密阻塞（详见 C.5） |

### C.2 迁移文件清单（16 个）

| 目录 | 文件 | 命名空间 |
|------|------|---------|
| Abstractions/ | IUnitOfWork.cs | `Wms.Core.Domain.Abstractions` |
| Common/ | Result.cs、ResultCodeTypes.cs | `Wms.Core.Domain.Common` |
| Constants/ | Cst.cs、CommonTypes.cs | `Wms.Core.Domain.Constants` |
| Enums/ | EnumEntity.cs、EnumHelper.cs、FlowInstanceStatus.cs、InOutType_Enum.cs、Interface_Enum.cs、Location_Enum.cs、ResponseType.cs、Unitload_Enum.cs、UnitloadOps_Enum.cs、WcsTaskState.cs | `Wms.Core.Domain.Enums` |
| Interfaces/ | IAuditable.cs（含 `IAuditable` + `IVersioned` + `IEntity` + `IEntity<TKey>`） | `Wms.Core.Domain.Interfaces` |

### C.3 关键决策（执行中确认或新增）

除原方案第五章 Phase 1 任务清单外，执行中新增以下决策：

| 决策 | 理由 |
|------|------|
| **保留原命名空间** `Wms.Core.Domain.*`（不改用 `Wms.Core.Domain.Shared.*`） | 下游零源码改动、零 using 变更，重新编译即可；符合 ABP（Volo.Abp）等框架拆分 Shared 项目的通行实践。代价：项目名与命名空间不完全对应，但 C# 项目名与命名空间本就不强制一致 |
| Domain.Shared 的 `RootNamespace` 设为 `Wms.Core.Domain`（而非 `Wms.Core.Domain.Shared`） | 让 IDE 新建 .cs 文件时默认命名空间与待迁移文件保持一致；与"保留原命名空间"策略配套 |
| Domain.Shared 零 PackageReference | 所有待迁移文件均为 POCO / 接口 / 枚举 / 静态常量类，仅依赖 `System.*` BCL。Domain.csproj 原有的 `Microsoft.Extensions.Logging.Abstractions` 留在 Domain（供 Entities/Services 等使用），不下沉 |
| 删除 `dotnet sln add` 自动创建的 `src` solution folder | 原 sln 中其他 4 个 src 项目（Application/Domain/Infrastructure/WebApi）都在根级别，不在任何 solution folder；为保持一致手工删除自动生成的 src folder |

### C.4 与计划的偏差

1. **`dotnet sln add` 副作用**（未在计划中提及）
   - `dotnet sln add src\Wms.Core.Domain.Shared\...` 自动创建了一个名为 "src" 的 solution folder 并把新项目嵌套其中
   - 这与其他 4 个 src 项目（在根级别）不一致
   - 处理：手工编辑 sln 删除 src solution folder 及对应的 NestedProjects 条目

2. **测试项目命令行编译阻塞**（环境问题，非计划缺陷）
   - 详见 C.5

### C.5 编译验证结果

| 项目 | 命令 | 结果 |
|------|------|------|
| Wms.Core.Domain.Shared | `dotnet build` | **0 errors / 0 warnings** ✓ |
| Wms.Core.Domain | `dotnet build` | **0 errors / 0 warnings** ✓ |
| Wms.Core.Application | `dotnet build` | **0 errors / 0 warnings** ✓ |
| Wms.Core.Infrastructure | `dotnet build` | **0 errors / 0 warnings** ✓ |
| Wms.Core.WebApi | `dotnet build`（首次失败：obj/Debug/net8.0/.NETCoreApp,Version=v8.0.AssemblyAttributes.cs 被识别为二进制；清理 obj/Debug/net8.0 后重编译通过） | **0 errors / 0 warnings** ✓ |
| Wms.Core.UnitTests + IntegrationTests | `dotnet build` | ⚠ **环境阻塞**（见下） |

**阻塞根因**：**亿赛通（E-SafeNet）企业文件加密软件**对 `%USERPROFILE%\.nuget\packages\microsoft.net.test.sdk\17.9.0\build\netcoreapp3.1\Microsoft.NET.Test.Sdk.Program.cs` 透明加密。文件 hex 开头特征 `62 14 23 65 19 00 c1 00` + 内嵌 `E-SafeNet` 字符串。命令行 `dotnet.exe` 未被列入信任进程列表 → 读到密文 → `CSC : error CS2015: ... is a binary file instead of a text file`。`dotnet restore --force` 无效（还原时立即被再次加密）。

**Phase 1 改动正确性已确认**：5 个主项目零错误零警告，`Wms.Core.Domain.Shared.dll` 已正确生成并传递到 WebApi 输出目录（依赖链通畅）。

### C.6 Gate 检查清单

- [x] **Domain.Shared 项目零 ProjectReference / 零 PackageReference** —— 已通过
- [x] **下游项目编译通过** —— Domain/Application/Infrastructure/WebApi 全部 0 errors / 0 warnings
- [x] **下游源码零改动** —— Application/Infrastructure/WebApi 的 .cs 文件零改动、零 using 变更（命名空间保留策略生效）
- [x] **Wms.Core.Domain.Shared.dll 正确产出** —— 已确认传递到 `src/Wms.Core.WebApi/bin/Debug/net8.0/`
- [ ] **`dotnet test` 通过（保持 25 pass + 4 skipped 基线）** —— **待用户在 VS 中验证**（命令行受 E-SafeNet 阻塞）

### C.7 下一步

Phase 1 主项目改动已全部完成并通过编译验证，仅剩测试项目需在 VS 中验证单测基线（VS 进程被 E-SafeNet 信任，能读取明文）。

验证通过后即可进入 Phase 2（Application.Contracts 抽取）。Phase 2 时将完成：
- `Wms.Core.Application/Ports/` 下 21 个接口（含 `IWcsClient` / `IWcsTaskBridge` / `IMesClient` / `IHangKeClient` / `IFlowDbContext` 等）迁移到新项目 `Wms.Core.Application.Contracts`
- Application 引用 Contracts，Infrastructure 改为引用 Contracts 而非整个 Application
- 同样采用"保留原命名空间 `Wms.Core.Application.Ports.*`"策略

### C.8 Phase 0 遗留项的状态更新

附录 A "下一步" 中提到：

> Phase 0 的 IUnitOfWork/IFlowDbContext 已暂放 Domain/Application，Phase 1 时迁移到 Domain.Shared/Application.Contracts。

本次 Phase 1 完成情况：
- ✅ **IUnitOfWork**：已从 `Domain/Abstractions/` 迁移到 `Domain.Shared/Abstractions/`（保留命名空间 `Wms.Core.Domain.Abstractions`）
- ⏳ **IFlowDbContext**：留待 Phase 2 时随 Application.Contracts 一起迁移（本次不动）

附录 A 中标记为"推迟到 Phase 1 后"的可选项（引入 EF Core InMemory 让 4 个 skipped 测试可运行）继续推迟到 Phase 2 后，原因：FlowEngineService 仍在 Infrastructure，Phase 3 抽取 Engine 时统一处理更省事。

---

## 附录 D：Phase 2 执行记录（2026-07-15 完成）

Phase 1（Domain.Shared 抽取）完成后，按方案第五章 Phase 2 任务清单执行 Application.Contracts 抽取。本附录记录实际执行情况、与计划的偏差，以及下一步。

### D.1 实施概况

| 维度 | 实际情况 |
|------|---------|
| 实施日期 | 2026-07-15 |
| 仓库状态 | 非 git 仓库，直接在工作目录修改 |
| 新增项目 | `Wms.Core.Application.Contracts`（GUID `{874576CF-DF29-4449-936A-47B5A8498BDD}`） |
| 迁移文件数 | **21 个 .cs 文件**（Ports/ 目录，原计划 22 个，少 1 个 IProcessRouteService） |
| 留在 Application 的接口 | 2 个：`IProcessRouteService`（依赖 DTOs）、`IFlowDbContext`（依赖 EF Core，在 Persistence/） |
| 下游源码改动 | **0 处**（命名空间保留策略生效） |
| 编译结果 | 6 个主项目 0 errors；测试项目需 VS 验证 |

### D.2 与原方案的偏差（关键决策变更）

原方案第五章 Phase 2 描述："`Wms.Core.Application/Ports/` 下 21 个接口移到新项目 `Wms.Core.Application.Contracts`"，并列出 `Persistence/IFlowDbContext.cs` 作为 Phase 0 重构产物一并迁移。

**实际执行中的关键决策变更**（与用户澄清后确认）：

| 接口 | 原方案 | 实际 | 变更原因 |
|------|--------|------|---------|
| `IFlowDbContext` | 迁到 Contracts | **留 Application/Persistence/** | 重度依赖 EF Core（3 个 using：DbSet/EntityEntry/DatabaseFacade），迁到 Contracts 会让纯接口项目被迫引入 EF Core 包。Phase 3 抽 Engine 时再处理（Engine 是 IFlowDbContext 的真正消费者） |
| `IProcessRouteService` | 迁到 Contracts | **留 Application/Ports/** | 真实依赖 `BranchOptionDto` 和 `PagedResult<>`（在 `Application/DTOs/`），迁到 Contracts 会造成循环依赖（Contracts → Application → Contracts） |

最终 Contracts 承载 **21 个**接口（Ports/ 目录的 22 个减去 IProcessRouteService）。

### D.3 命名空间混乱的发现（执行中遇到）

执行中发现 `src/Wms.Core.Domain/Requests/` 目录下存在**命名空间混乱**：

| 文件 | 物理位置 | 实际命名空间 |
|------|---------|------------|
| `UnitloadRequest.cs` | Domain/Requests/ | `Wms.Core.Application.DTOs` ⚠ |
| `WcsRequest.cs` | Domain/Requests/ | `Wms.Core.Application.DTOs` ⚠ |
| `SimToolRequests.cs` | Domain/Requests/ | `Wms.Core.Application.DTOs` ⚠ |
| 其他 Requests 文件（如 PortRequests.cs） | Domain/Requests/ | `Wms.Core.Domain.Requests` ✓ |

**影响**：`IUnitloadService` 的方法签名使用了 `UnitloadRequest`/`WcsRequest`/`UpdateUnitloadRequest`，这些类型在 Domain 程序集但命名空间是 `Wms.Core.Application.DTOs`。因此 IUnitloadService **真实依赖** `using Wms.Core.Application.DTOs;`，不能清理。

**处理**：
- 计划阶段曾误判 ILocationService / IPortService / IUnitloadService 三个文件的 `using Wms.Core.Application.DTOs;` 都是冗余 using
- 实际执行时首次编译失败（CS0246: UnitloadRequest/WcsRequest/UpdateUnitloadRequest 找不到），诊断后发现 IUnitloadService 的 using 必须保留
- 最终清理：ILocationService / IPortService 清理冗余 using（这 2 个文件的方法签名确实只用 Domain 类型）；IUnitloadService 保留 using（类型虽在 Domain 程序集，但命名空间是 `Wms.Core.Application.DTOs`）

**技术债记录**：Domain 项目中有 3 个文件用了 `Wms.Core.Application.DTOs` 命名空间，违反了"下层不依赖上层命名空间"原则。建议作为独立技术债在 Phase 4 后处理（重命名空间为 `Wms.Core.Domain.Requests`，同步修改所有引用点）。

### D.4 关键决策汇总

| 决策 | 理由 |
|------|------|
| 保留原命名空间 `Wms.Core.Application.Ports` | 与 Phase 1 策略一致，下游 67 个文件（Infrastructure 43 + WebApi 23 + UnitTests 1）零 using 改动 |
| Contracts 的 RootNamespace 设为 `Wms.Core.Application` | 与 Domain.Shared 设为 `Wms.Core.Domain` 同理，让 IDE 新建文件默认命名空间为 `Wms.Core.Application.Ports` |
| Contracts 仅引用 Domain，零 PackageReference | 21 个接口文件全部仅依赖 Domain 类型，无 EF Core / Dapper / 第三方包 |
| IFlowDbContext 不迁移 | 避免引入 EF Core 包到纯接口项目；IFlowDbContext 的消费者在 Infrastructure 的 Flow 节点，Phase 3 统一处理 |
| IProcessRouteService 不迁移 | 避免循环依赖：它依赖 BranchOptionDto / PagedResult（在 Application/DTOs/） |
| ILocationService / IPortService 清理冗余 using | 这 2 个文件的方法签名确实未使用 Application.DTOs 中任何类型，清理后编译通过 |
| IUnitloadService 保留 using Wms.Core.Application.DTOs | 该 using 实际必要（虽然类型在 Domain 程序集，但命名空间是 Application.DTOs） |

### D.5 编译验证结果

| 项目 | 命令 | 结果 |
|------|------|------|
| Wms.Core.Domain.Shared | `dotnet build` | 0 errors / 0 warnings ✓（Phase 1 回归） |
| Wms.Core.Domain | `dotnet build` | 0 errors / 0 warnings ✓（Phase 1 回归） |
| **Wms.Core.Application.Contracts** | `dotnet build` | **0 errors / 5 warnings**（仅 XML 注释警告）✓ |
| Wms.Core.Application | `dotnet build` | 0 errors / 142 warnings（XML 注释）✓ |
| Wms.Core.Infrastructure | `dotnet build` | 0 errors / 692 warnings（XML 注释）✓ |
| Wms.Core.WebApi | `dotnet build` | 0 errors / 133 warnings ✓（清理 obj 后通过） |
| UnitTests + IntegrationTests | `dotnet build` | 需 VS 验证（E-SafeNet 阻塞，同 Phase 1） |

**DLL 传递验证**：
- `Wms.Core.Application.Contracts.dll` 已生成在 `src/Wms.Core.Application.Contracts/bin/Debug/net8.0/`
- 已传递到 `src/Wms.Core.WebApi/bin/Debug/net8.0/`（依赖链通畅）
- `Wms.Core.Domain.Shared.dll` 仍在 WebApi 输出目录（Phase 1 回归）

### D.6 Gate 检查清单

- [x] Application.Contracts 零 PackageReference
- [x] Application.Contracts 仅 1 个 ProjectReference（Domain）
- [x] 6 个主项目编译通过（0 errors）
- [x] 下游项目零源码改动、零 using 改动
- [x] Wms.Core.Application.Contracts.dll 正确产出并传递到 WebApi 输出目录
- [x] Application/Ports/ 只剩 IProcessRouteService.cs
- [ ] `dotnet test` 保持 25 pass + 4 skipped 基线 —— **需 VS 验证**（命令行受 E-SafeNet 阻塞）

### D.7 下一步

Phase 2 主项目改动已全部完成并通过编译验证，仅剩测试项目需在 VS 中验证单测基线。

**Phase 3（Engine 抽取）准备**：Phase 3 将处理：
- IFlowDbContext 从 Application/Persistence/ 迁移到 Contracts（或新建专门的 Persistence.Abstractions 项目）—— 需要评估 Contracts 是否引入 EF Core 包，或单独建项目
- FlowEngineService 和 25 个 Flow 节点处理器从 Infrastructure 迁移到新项目 `Wms.Core.Engine`
- 引入 EF Core InMemory 让 4 个 skipped 测试可运行（FlowEngineService 构造函数解耦后更省事）
- 客户特定库位分配规则（如 SSRule04HcLx）留项目层，通过 `EngineOptions.AddLocationRule<T>()` 扩展点注册

### D.8 Phase 1 遗留项的状态更新

附录 C "下一步" 中提到 IFlowDbContext 留待 Phase 2 时随 Application.Contracts 一起迁移。本次 Phase 2 完成情况：
- ⏳ **IFlowDbContext**：因依赖 EF Core，经用户决策**继续留 Application**，推迟到 Phase 3 时处理（与 Engine 抽取一起决策）

---

## 附录 E：Phase 3 执行记录（2026-07-15 完成）

### E.1 实施概况

- **新项目**：`src/Wms.Core.Engine/Wms.Core.Engine.csproj`
- **sln GUID**：`{0FBE961B-C0B0-41CA-B92D-B1F39612777B}`
- **RootNamespace**：`Wms.Core.Engine`
- **迁移文件总数**：46 个（详见 E.4 决策表）
- **新建文件**：1 个（`Engine/DependencyInjection/EngineExtensions.cs`）
- **最终编译结果**：7 个主项目全部 0 errors

| 项目 | Errors | Warnings | 说明 |
|------|--------|----------|------|
| Domain.Shared | 0 | 0 | 未改动 |
| Domain | 0 | 0 | 未改动 |
| Application.Contracts | 0 | 5 | 未改动（Phase 2 基线） |
| Application | 0 | 142 | 未改动（Phase 2 基线） |
| **Engine** | **0** | **277** | 新项目；全 XML 注释警告 |
| Infrastructure | 0 | 273 | 迁出 46 文件后 warnings 下降（Phase 2 为 692） |
| WebApi | 0 | 133 | 与 Phase 2 基线一致 |

### E.2 与原方案的偏差（关键决策变更）

#### 偏差 1：LocationAllocator 是补充项（原方案遗漏）

原方案第五章 Phase 3 迁移表列出 32 个文件（Flow 核心 3 + 节点 25 + FlowEngineService 1 + 规则 16 含基类），但**遗漏了 LocationAllocator.cs**（998 行，位于 `Infrastructure/Handlers/WcsRequest/`）。

执行中探索发现 LocationAllocator 被 **9 个节点处理器**直接依赖（1 个实例注入 + 8 个静态方法调用），必须随 Engine 迁移，否则 Engine 无法编译。最终迁移文件总数为 **46 个**（32 + LocationAllocator 1 + 节点处理器目录结构 + ... 实际为 30 个 Flow 相关 + 16 个规则）。

#### 偏差 2：Infrastructure 引用 Engine（D11，关键架构调整）

**原方案隐含假设**：Engine 是底层平台，Infrastructure 是项目层，两者平行，Engine 不被 Infrastructure 引用。

**执行中发现**：附录 B.3 决策 `IWcsRequestHandler` 系列（InboundRequestHandler 等 12 个）**留 Infrastructure**，但这些 handler 注入 `LocationAllocator`（附录 B.4 已识别"同目录混用"但说"无需改"）。LocationAllocator 迁 Engine 后，Infrastructure 编译失败（9 个 CS0246 错误）。同理 `SSRule04HcLx` 依赖 `LocationAllocationRuleBase`（已迁 Engine）。

**决策 D11**：Infrastructure.csproj 添加 Engine 引用。
- 依赖方向：`Infrastructure → Engine → Application → Domain`（单向，无循环）
- Phase 3 Gate "**Engine 不引 Infrastructure**" 仍成立（核心约束保持）
- 与 ABP 框架"项目层引用底层平台"模式一致
- 原方案"Engine 不被 Infrastructure 引用"的隐含假设被打破，但文字层面的 Gate（"Engine 零引用 Infrastructure"）不违反

#### 偏差 3：Engine 需引 Relational 包（R11）

原方案 R10 只列 `Microsoft.EntityFrameworkCore` 基础包。执行中发现 `ExecuteSqlRawAsync` 是 `RelationalDatabaseFacadeExtensions` 的扩展方法，定义在 `Microsoft.EntityFrameworkCore.Relational` 包（非基础包）。LocationAllocator 和 WasteDisposalCaptureNode 使用 Raw SQL API。

修正：Engine csproj 补 `Microsoft.EntityFrameworkCore.Relational` 8.0.11。

### E.3 关键技术发现

| # | 发现 | 影响 |
|---|------|------|
| R11 | ExecuteSqlRawAsync 在 Relational 包（非基础 EFCore 包） | Engine 补引 Relational 包 |
| R12 | LocationAllocator.cs + FindUnitloadHandler.cs 有冗余 `using Wms.Core.Infrastructure.Persistence;`（历史遗留） | 迁移后删除冗余 using |
| R13 | Infrastructure 必须引 Engine（IWcsRequestHandler + SSRule04HcLx 依赖 Engine 类型） | 触发 D11 决策 |

### E.4 关键决策汇总（D1-D11）

| # | Decision | Status |
|---|----------|--------|
| D1 | IFlowDbContext 留 Application/Persistence/，Engine 引 Application | ✓ Phase 2 沿用 |
| D2 | LocationAllocator.cs 随 Engine 迁移（原方案遗漏项） | ✓ 已定 |
| D3 | LocationAllocator 命名空间保留 `Wms.Core.Infrastructure.Handlers.WcsRequest` | ✓ 用户确认 |
| D4 | 规则文件命名空间保留 `Wms.Core.Infrastructure.Tasks.Rules` | ✓ 用户确认 |
| D5 | FlowEngineService 改写为依赖 IFlowDbContext（三处 WmsDbContext 替换） | ✓ 已定 |
| D6 | SSRule04HcLx 留项目层，通过 `EngineOptions.AddLocationRule<T>()` 注册 | ✓ 已定 |
| D7 | Engine 用程序集扫描注册 25 个 INodeHandler + 14 条通用规则 | ✓ 已定 |
| D8 | Engine RootNamespace=`Wms.Core.Engine` | ✓ 已定 |
| D9 | Engine 引 EFCore（非 SqlServer）+ Relational（R11 修正） | ✓ 已定 |
| D10 | sln 中 Engine 项目在根级别（删除自动 src folder） | ✓ 已定 |
| **D11** | **Infrastructure 引用 Engine**（原方案未预见，见 E.2 偏差 2） | ✓ 执行中确认 |

### E.5 迁移文件清单（46 个）

| 分类 | 数量 | 源路径 → 目标路径 | 命名空间处理 |
|------|------|-------------------|------------|
| Flow 核心 | 3 | `Infrastructure/Flow/{FlowContext,IFlowEngine,INodeHandler}.cs` → `Engine/Flow/` | 已是 `Wms.Core.Engine`（Phase 0 预备） |
| 节点处理器 | 25 | `Infrastructure/Flow/Nodes/*.cs` → `Engine/Flow/Nodes/` | 已是 `Wms.Core.Engine.Nodes`（Phase 0 预备） |
| Engine 服务 | 1 | `Infrastructure/Services/FlowEngineService.cs` → `Engine/FlowEngineService.cs` | 改为 `Wms.Core.Engine`（原 Infrastructure.Services） |
| 辅助类 | 1 | `Infrastructure/Handlers/WcsRequest/LocationAllocator.cs` → `Engine/Flow/LocationAllocator.cs` | 保留 `Wms.Core.Infrastructure.Handlers.WcsRequest`（D3） |
| 规则基类 + 通用规则 | 15 | `Infrastructure/Tasks/Rules/{Base,SS01-10,SD01-04}.cs` → `Engine/Rules/` | 保留 `Wms.Core.Infrastructure.Tasks.Rules`（D4） |
| **客户特定规则** | **1** | `Infrastructure/Tasks/Rules/SSRule04HcLx.cs` **留原地** | `Wms.Core.Infrastructure.Tasks.Rules`（D6 留项目层） |

### E.6 DI 重组（AddWmsEngine 扩展方法）

新建 `Engine/DependencyInjection/EngineExtensions.cs`：

```csharp
public static IServiceCollection AddWmsEngine(
    this IServiceCollection services,
    Action<EngineOptions>? configure = null)
{
    services.AddScoped<IFlowEngine, FlowEngineService>();
    // 程序集扫描注册 INodeHandler（25 个）
    // 程序集扫描注册 ILocationAllocationRule（14 条通用）
    // 注册 LocationAllocator + LocationAllocationEngine
    // 应用 EngineOptions（客户特定规则）
}

public class EngineOptions
{
    public EngineOptions AddLocationRule<T>() where T : ILocationAllocationRule { ... }
}
```

调用点（`WebApi/Extensions/WcsExtensions.cs`）：

```csharp
services.AddWmsEngine(opt => opt.AddLocationRule<SSRule04HcLx>());
```

**改造影响**：
- `Infrastructure/ServiceCollectionExtensions.cs`：删除 IFlowEngine + 25 个 INodeHandler 注册（30 行）+ 删除 2 个 using
- `WebApi/WcsExtensions.cs`：删除 14 条通用规则 + LocationAllocator + LocationAllocationEngine 注册（17 行），改为调用 AddWmsEngine

### E.7 Gate 检查清单

| Gate | 预期 | 实际 | 状态 |
|------|------|------|------|
| 7 主项目编译 | 全部 0 errors | 全部 0 errors | ✓ |
| Engine 零引用 Infrastructure | Engine.csproj 无 Infrastructure ProjectReference | 仅 Application + Domain | ✓ |
| Engine NuGet 包不含 Infrastructure | `dotnet list package --include-transitive` 无 Infrastructure | 无 | ✓ |
| DLL 传递 | Engine.dll 到 WebApi/bin | 已传递 | ✓ |
| sln 完整编译 | dotnet build sln 通过 | **CS2015 失败**（E-SafeNet 加密 Test.Sdk.Program.cs） | ⏳ 需 VS 验证 |
| 增量编译 Gate 调整 | 原方案"Engine 改动不触发 Infrastructure" | **因 D11 不再适用**（Infrastructure 引 Engine，Engine 改动会触发下游） | ℹ️ Gate 重新定义 |

### E.8 下一步

1. **用户在 VS 中验证**：
   - sln 完整编译（绕过 E-SafeNet 命令行问题）
   - `dotnet test` 保持 25 pass + 4 skipped 基线
   - 运行时验证 FlowEngine + 节点处理器 + 库位分配规则正常工作
2. **Phase 4（最终目标）：物理仓库分离**
   - Engine / Application / Domain 独立 Git 仓库
   - 通过 DLL 二进制引用（满足"内部权限隔离"诉求）
   - 可回退

### E.9 Phase 2 遗留项的状态更新

附录 D "下一步" 中提到的 Phase 3 准备项：
- ✓ **IFlowDbContext**：继续留 Application/Persistence/（D1 决策维持），Engine 引用 Application 获得
- ✓ **FlowEngineService 解耦**：已完成（D5，WmsDbContext → IFlowDbContext）
- ✓ **客户特定规则扩展点**：已实现（EngineOptions.AddLocationRule<T>()）
- ⏳ **EF Core InMemory 让 4 个 skipped 测试可运行**：推迟到 Phase 3 后（用户验证运行时再处理）

---

## 附录 F：Phase 4 执行记录（2026-07-16，步骤 ①②③ 完成）

Phase 3 合并 main（2026-07-15，commit 938d413 + d0d4b9b）后，按方案第五章 Phase 4 执行物理仓库分离。本次完成步骤 ①②③（建底层目录 + pack.ps1 + 项目层 csproj 切换），步骤 ④（sync-platform.ps1）和 ⑤（拆 sln + 权限隔离）遗留后续。

### F.1 实施概况

| 维度 | 实际情况 |
|------|---------|
| 实施日期 | 2026-07-16 |
| 仓库状态 | Wms.Net8 是 git 仓库（remote: github.com/249541588-ops/Wms.Net8.git），Phase 1/2/3 的 122 处改动在本次会话开头提交到 main（938d413 + d0d4b9b），建立 Phase 4 干净起点 |
| wms-platform 形态 | **本地目录暂不建 git**（用户选择）；位置 `f:/Project/Wms.Core/wms-platform/`（与 Wms.Net8 同级） |
| 底层项目数 | **5 个**（原方案 6 个，Wms.Core.Logging 是 aspirational 尚未抽取） |
| 项目层切换 | Infrastructure + WebApi 两个 csproj 的 ProjectReference → Reference HintPath |
| 编译结果 | 7 主项目全部 0 errors；wms-platform 独立编译 0 errors；5 DLL 正确传递到 WebApi/bin |

### F.2 与原方案的偏差

#### 偏差 1：5 个底层项目而非 6 个（Wms.Core.Logging 未创建）

**原方案**：第六章列出 6 个底层项目，含 `Wms.Core.Logging`（日志领域接口 + DTO）。

**执行中探索**（Explore agent 彻底搜索）发现：
- **IInterfaceLogStore 接口不存在**：方案提到的核心抽象尚未创建
- **SaveInterfaceLogAsync 代码重复 3 处**：WcsController（L490-503）、HangkeController（L278-291）、DatabaseWcsTaskBridge 各有一份，应统一到 IInterfaceLogStore
- **无任何 Log DTO**：LogController 直接返回匿名类型
- **日志实体已在 Domain**：InterfaceLog、SystemLog、FlowNodeLog、UnitloadProcessRouteLog 都在 Domain/Entities/，无需迁移

**决策 P1**：创建 Wms.Core.Logging 需要新建 IInterfaceLogStore 接口 + 重构 3 处重复代码，属于**新功能开发**，不是迁移。本次跳过，作为独立技术债在 Phase 4 完全落地后处理。

#### 偏差 2：wms-platform 本地目录暂不建 git

**原方案**：第五章 Phase 4 步骤 1 "创建底层仓库 `wms-platform`"——隐含 git 仓库 + 远程。

**实际**：用户选择"本地目录暂不建 git"——先验证 pack.ps1 + HintPath 切换能跑通，git 仓库与 remote 后续再搭。

**影响**：
- pack.ps1 中 git 字段（commit/branch）兜底为 "no-git"
- sync-platform.ps1（步骤 ④）中的 `git fetch origin artifacts` 逻辑需要调整（改为本地路径复制）
- "内部权限隔离"诉求本次**未真正实现**——底层源码仍在 Wms.Net8/src/ 下（步骤 ⑤ 拆 sln 时才移除）

#### 偏差 3：测试项目未切换（遗留项）

**原方案**：Phase 4 步骤 3 "项目层 csproj 切换"——隐含所有项目层项目。

**实际**：测试项目（UnitTests + IntegrationTests）**保留 ProjectReference**。理由：
1. 用户本次范围限 ①②③（主项目层）
2. 测试项目受 E-SafeNet 阻塞，切换后无法命令行验证
3. UnitTests 直接引 Domain，切换为 DLL 引用需评估 Domain 类型在测试代码中的使用面

**后续**：步骤 ⑤（拆 sln）时统一处理。

### F.3 关键技术发现

| # | 发现 | 影响 |
|---|------|------|
| R1 | Wms.Core.Logging 是 aspirational（IInterfaceLogStore 不存在） | 本次 5 个底层项目，Logging 作为独立技术债 |
| R2 | `powershell -File` 调用时 `$PSScriptRoot` 在 param 默认值中为空 | pack.ps1 用 `$PSCommandPath` + `Split-Path` 兜底 |
| R3 | **Reference HintPath 不传递 NuGet analyzer**（关键） | Infrastructure 显式引 Riok.Mapperly 4.3.1；通用规则：切换时识别原传递依赖中的 analyzer 包 |
| R4 | DebugType=embedded + EmbedAllSources=true 生效 | artifacts/ 无独立 .pdb，源码内嵌 DLL，满足"无 .cs 但能调试步入" |
| R5 | 测试项目未切换（保留 ProjectReference） | 遗留项，步骤 ⑤ 统一处理 |
| R6 | lib/platform 的 gitignore 策略 | DLL 是构建产物不入库；version.json + README 入库作参考 |

### F.4 R3 详解：Reference HintPath 不传递 analyzer

本次最重要的技术发现。`Riok.Mapperly` 4.3.1 是源代码生成器（analyzer），在 Application.csproj 中引用。

**切换前**（ProjectReference）：
```
Infrastructure → ProjectReference → Application.csproj
                                  → Application 引用 Riok.Mapperly
                                  → 编译 Infrastructure 时，Riok.Mapperly analyzer 自动加载
                                  → WmsMapper.cs 的 [Mapper] partial class 生成代码成功
```

**切换后**（Reference HintPath）：
```
Infrastructure → Reference Application.dll
              → DLL 类型引用正常 ✓
              → 但 Riok.Mapperly analyzer 的 Roslyn 配置不传递 ✗
              → WmsMapper.cs 的 [Mapper] partial class 无生成代码
              → CS8795: 分部方法必须具有实现部分
```

**修正**：Infrastructure.csproj 显式添加：
```xml
<PackageReference Include="Riok.Mapperly" Version="4.3.1" />
```

**通用规则**：从 ProjectReference 切换到 DLL 引用时，必须识别原传递依赖中的 analyzer/源代码生成器包，并在项目层 csproj 显式引用。识别方法：grep `<PackageReference>` 中 `IncludeAssets` 含 `analyzers` 的包，或包类型为 "source generator"。

### F.5 关键决策汇总（P1-P7）

| # | Decision | Status |
|---|----------|--------|
| P1 | 5 个底层项目（不含 Wms.Core.Logging） | ✓ 已定 |
| P2 | wms-platform 本地目录暂不建 git | ✓ 用户确认 |
| P3 | pack.ps1 用 $PSCommandPath 兜底 $PSScriptRoot | ✓ 已实施 |
| P4 | Infrastructure 显式引 Riok.Mapperly 4.3.1 | ✓ 已实施 |
| P5 | 测试项目保留 ProjectReference | ✓ 已定（遗留项） |
| P6 | lib/platform/*.dll 加入 .gitignore | ✓ 已实施 |
| P7 | DebugType=embedded + EmbedAllSources=true | ✓ 已验证 |

### F.6 产出物清单

**wms-platform 目录**（`f:/Project/Wms.Core/wms-platform/`）：
- `src/`：5 个底层项目源码（Domain.Shared / Domain / Application.Contracts / Application / Engine）
- `artifacts/`：5 个 DLL + version.json
- `pack.ps1`：打包脚本
- `PLATFORM_VERSION.txt`：版本号（1.0.0）
- `README.md`：底层仓库说明

**Wms.Net8 仓库变更**：
- `lib/platform/`：5 DLL（gitignore）+ version.json + README.md
- `src/Wms.Core.Infrastructure/Wms.Core.Infrastructure.csproj`：ProjectReference → Reference HintPath + 显式 Riok.Mapperly
- `src/Wms.Core.WebApi/Wms.Core.WebApi.csproj`：同上
- `.gitignore`：添加 lib/platform/*.dll + *.pdb + *.xml

### F.7 Gate 检查清单

| Gate | 预期 | 实际 | 状态 |
|------|------|------|------|
| wms-platform 独立编译 | 5 项目 0 errors | 0 errors / 656 warnings（XML 注释） | ✓ |
| pack.ps1 产出 5 DLL | artifacts/ 含 5 个 .dll | ✓ Domain.Shared/Domain/Contracts/Application/Engine | ✓ |
| EmbedAllSources 生效 | 无独立 .pdb 文件 | ✓ 仅 5 .dll + version.json | ✓ |
| Infrastructure DLL 引用编译 | 0 errors | 0 errors / 273 warnings | ✓ |
| WebApi DLL 引用编译 | 0 errors | 0 errors / 133 warnings | ✓ |
| DLL 传递到 WebApi/bin | 5 底层 DLL 在输出目录 | ✓ 全部存在 | ✓ |
| 回退可行 | HintPath 改回 ProjectReference | 未实际回退验证，csproj 改动可逆 | ℹ️ 设计保证 |
| sln 完整编译 | dotnet build sln 通过 | 未验证（E-SafeNet 阻塞测试项目，已知问题） | ⏳ 需 VS |
| 运行时验证 | WebApi 启动 + Flow 引擎工作 | 未验证 | ⏳ 需用户 |

### F.8 下一步

1. **用户在 VS 中验证**：
   - sln 完整编译（绕过 E-SafeNet 命令行问题）
   - WebApi 运行时启动 + 简单 API 调用验证底层 DLL 加载正常
   - 调试器 F11 步入底层代码（验证 EmbedAllSources 源码嵌入生效）
2. **Phase 4 步骤 ④ sync-platform.ps1**（后续会话）：
   - 编写同步脚本（本地目录模式，不走 git artifacts 分支）
   - 加入版本比对逻辑（对比 version.json）
3. **Phase 4 步骤 ⑤ 拆 sln + 权限隔离**（后续会话）：
   - 从 Wms.Net8.sln 移除 5 个底层项目
   - 测试项目切换为 DLL 引用
   - src/ 下 5 个底层项目目录移除（或归档）
   - 配置 GitHub 仓库权限（如建独立 wms-platform repo）
4. **Wms.Core.Logging**（独立技术债）：
   - 新建 IInterfaceLogStore 接口
   - 重构 WcsController / HangkeController / DatabaseWcsTaskBridge 的 SaveInterfaceLogAsync 重复代码
   - 创建 Wms.Core.Logging 项目（仅接口 + DTO）
   - 作为底层 platform 的第 6 个项目

### F.9 Phase 3 遗留项的状态更新

附录 E "下一步" 中提到的 Phase 4 准备项：
- ✓ **底层仓库建立**：wms-platform 本地目录已建（P2 本地模式）
- ✓ **pack.ps1 + DLL 产出**：5 DLL + version.json 正确产出
- ✓ **项目层 csproj 切换**：Infrastructure + WebApi 已切（主项目层）
- ⏳ **sync-platform.ps1**：遗留步骤 ④
- ⏳ **拆 sln + 权限隔离**：遗留步骤 ⑤

附录 E 中标记为"推迟到 Phase 3 后"的 EF Core InMemory（让 4 个 skipped 测试可运行）继续推迟到 Phase 4 完全落地后。

---

## 总结

**核心建议**：用户明确诉求是"内部权限隔离"，必须用物理仓库分离 + DLL 二进制引用，原文档的 ProjectReference 方案无法满足。

**渐进路径**：
1. **Step 0（本文档）**：固化方案为 docs，与原文档形成双方案对照
2. **Phase 0（不可跳过）**：前置重构 —— IUnitOfWork、IFlowDbContext、WebApi 依赖倒置、分段事务保护（+ 3 项前置审计）
3. **Phase 1（推荐）**：抽取 Domain.Shared —— 低风险，为后续打基础
4. **Phase 2（推荐）**：抽取 Application.Contracts —— 接口/实现分离
5. **Phase 3（必须）**：抽取 Engine —— 核心价值所在
6. **Phase 4（最终目标）**：物理仓库分离 —— 可回退

**与原文档的关系**：本文档不否定原文档，而是补足"代码保护"维度。Phase 1-3 与原文档方案有重合（都涉及项目抽取），但目的不同（原文档为编译加速，本方案为权限隔离）。若团队最终选择物理分离路径，原文档可作为 Phase 1-3 的详细操作手册。

**不做**：
- 不搭建 NuGet 私有源（用户明确排除）
- 不引入 MediatR（已有 Flow Engine）
- 不引入代码混淆（内部隔离场景不需要）
- 不做业务模块化垂直切分（与原文档共识）
- 不做微服务化（与原文档共识）
