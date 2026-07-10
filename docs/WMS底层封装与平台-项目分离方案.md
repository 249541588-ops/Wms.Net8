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

1. **`FlowContext` 直接持有 `WmsDbContext`**：25 个节点处理器通过 `context.Db` 访问 70 个 DbSet，是 Engine 拆分的最大障碍（[FlowContext.cs](../src/Wms.Core.Infrastructure/Flow/FlowContext.cs)）
2. **请求阶段无事务保护**：`FlowEngineService.ExecuteAsync` 仅用 `IsTransactionBoundary` 做中间 SaveChanges，失败无法回滚
3. **WebApi 层承担基础设施职责**：`IBackgroundTaskQueue`、`ITranslationService` 实现在 WebApi 层，依赖方向倒置
4. **WCS 网关分散在 3 个项目 8 目录**（Application/Ports、Infrastructure 的 Clients/Flow/Nodes/Handlers 两类/Services、WebApi 的 Extensions/Services）：拆分工作量被原文档低估
5. **35+ 处 `BeginTransaction` 散布，无 `IUnitOfWork` 抽象**：其中 UnitloadService.cs 中 6 处是同步调用，存在线程池风险
6. **节点数量与文档不符**：实际 25 个节点处理器（非 26 个）

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
| **Wms.Core.Domain** | 全部领域实体（按 Warehouse/Container/Material/Transport/Flow 等 12 个 Bounded Context 子目录划分，**69 个实体文件**；整个项目含 Enums/Requests/Constants/Services 等共 116 个 .cs）、Domain 接口 | platform |
| **Wms.Core.Application.Contracts** | 21 个 Ports 接口（纯接口） | platform |
| **Wms.Core.Application** | DTOs、Jobs、请求/响应模型 | platform |
| **Wms.Core.Engine** | FlowContext、IFlowEngine、INodeHandler、25 节点处理器、库位分配规则（核心 IP） | platform |
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

---

## 三、必须的前置重构（不可跳过）

### 重构 A：引入 IUnitOfWork / IFlowDbContext 抽象

**目的**：让 FlowContext 和 25 个节点处理器不再直接依赖 `WmsDbContext` 具体类型。

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

**A.2** 在 `Wms.Core.Application.Contracts/Persistence/IFlowDbContext.cs` 定义节点处理器实际需要的 DbSet（不是全部 70 个，而是节点实际用到的子集，初步估计 15-20 个）。**最终清单以 Phase 0 任务 6 的 grep 结果为准**（grep 25 个节点处理器的 `context.Db.` 引用）。下方示例仅供示意。

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

由于 IFlowDbContext 暴露同名 DbSet 属性，**25 个节点处理器代码不需要改**，只需重新编译。

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

**影响范围**：FlowContext.cs、FlowEngineService.cs、DI 注册扩展方法。25 个节点处理器仅重新编译，代码不变。

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
6. **生成 IFlowDbContext 精确 DbSet 清单**：grep 25 个节点的 `context.Db.` 调用，汇总实际用到的 DbSet，作为 A.2 接口的最终定义依据（A.2 示例清单仅供示意，以本次 grep 结果为准）
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
| `Flow/Nodes/` 25 个处理器 | `Flow/Nodes/` |
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
| 节点处理器数 | 26（有误） | 25 |
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

1. [FlowContext.cs](../src/Wms.Core.Infrastructure/Flow/FlowContext.cs) - 核心改造目标：WmsDbContext → IFlowDbContext + IUnitOfWork，影响 25 个节点处理器
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
**缓解**：Phase 0 先用 grep 全面扫描 25 个节点的 `context.Db.` 调用；保留 `DbSet<T> Set<T>()` 方法作为兜底。

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
