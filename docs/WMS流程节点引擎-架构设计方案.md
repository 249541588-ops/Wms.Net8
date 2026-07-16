# WMS 流程节点引擎 — 架构设计方案

> **文档日期：** 2026-06-08
> **适用项目：** Wms.Core (.NET 8 + EF Core + SQL Server + Vue 3)
> **快照声明：** 本文档为初始设计快照（2026-06-08，规划 15 个节点类型 / 3 个 IWcsRequestHandler）。实际落地数为：Phase 0 时 22 个 NodeHandler（含 13 个 IWcsRequestHandler），2026-07 含工艺路线相关 3 个共 25 个 NodeHandler。**实际落地情况以 [WMS底层封装与平台-项目分离方案.md](WMS底层封装与平台-项目分离方案.md) 附录 B 为准。**

---

## 一、背景与现状

### 当前问题

当前 WMS 出入库流程**硬编码在 C# 类中**，通过策略模式分发：

**请求阶段 (WCS → WMS):** `IWcsRequestHandler` — 3 个实现类
- `InboundRequestHandler`: 验证参数 → 查托盘 → 检查状态 → 工艺匹配 → 分配货位 → 创建任务 → 下发WCS（7步）
- `OutboundRequestHandler`: 验证参数 → 查托盘 → 检查状态 → 创建任务 → 下发WCS（5步）
- `MoveRequestHandler`: 验证参数 → 查托盘 → 检查状态 → 分配货位 → 创建任务 → 下发WCS（7步）

**完成阶段 (WCS完成 → WMS):** `ITaskCompletionHandler` — 3 个实现类
- `InboundCompletionHandler`: 查任务 → 更新Unitload → 工序推进 → 记录流水 → 归档
- `OutboundCompletionHandler`: 查任务 → 出库流水 → 拆盘 → 状态重置 → 归档
- `MoveCompletionHandler`: 查任务 → 更新Unitload → 记录流水 → 归档

**核心痛点：**
- 新增/修改流程必须改代码 + 重新编译部署
- 不同客户/仓库的流程差异无法配置化
- 无法灵活组合步骤（如：某些仓库入库需质检环节，某些不需要）

---

## 二、行业深度调研

### 2.1 商业 WMS 厂商方案

#### SAP EWM — 仓库流程类型 (WPT)

**核心机制：** 以 **Warehouse Process Type (WPT)** 为中心，配合 **Condition Technique**（条件技术）自动匹配流程类型。

| 维度 | 说明 |
|------|------|
| 流程定义 | WPT 包含 50+ 可配置字段（流程类别、活动类型、源/目标存储类型、上架/下架策略等） |
| 流程匹配 | 条件技术引擎：定义规则表（仓库+单据类型+物品类型 → WPT），按优先级匹配 |
| 多步流程 | **POSC（Process-Oriented Storage Control）**：定义路由步骤链（卸货→计数→拆托→上架） |
| 扩展方式 | 配置为主 + BAdI 代码兜底（`/SCWM/WPT_DET`） |
| 流程类别 | 9 大类：上架(1)、下架(2)、内部移动(3)、盘点(4)、GR posting(5)、GI posting(6)、库存变更(7)、补货(8)、监管(9) |

**架构层次：**
```
文档层 (Inbound/Outbound Delivery)
  → 匹配层 (Condition Technique → 自动匹配 WPT)
    → 流程类型层 (WPT 定义行为)
      → 存储流程层 (POSC 多步路由)
        → 任务层 (Warehouse Task)
          → 订单层 (Warehouse Order → 资源分配)
            → 执行层 (RF 设备)
```

**关键启示：**
- 条件匹配机制（按仓库/单据类型/物品属性匹配流程）值得借鉴
- POSC 多步路由概念可应用于我们的流程节点链
- 但 SAP EWM 过于复杂（50+ 字段/流程类型），不适合直接套用

---

#### Manhattan Active WM — ProActive 低代码平台

**核心机制：** 云原生微服务架构 + **ProActive 低代码平台**（全层扩展：GUI + 后端 + 数据库）。

| 维度 | 说明 |
|------|------|
| 配置方式 | 分步配置向导（Step-by-step Wizards），非代码化 |
| 扩展机制 | ProActive 平台可扩展 GUI、后端服务、数据模型、API |
| 工作流验证 | Continuous Workflow Validation (CWV) 确保配置始终有效 |
| 特色 | AI 驱动优化嵌入全流程（波次规划、上架逻辑、库位放置） |
| 出库模式 | 支持 Wave（波次）和 Waveless（Order Streaming 连续流）两种模式 |
| 授权模式 | SaaS ~$2K/license/年 |

**关键启示：**
- "配置向导" 模式适合 WMS 场景，用户不需要理解底层实现
- 持续验证机制确保流程配置不会出错

---

#### Korber K.Motion — Workflow DNA

**核心机制：** **Workflow DNA** — 将业务逻辑封装为"智能积木块"，可自由组装成完整流程。

| 维度 | 说明 |
|------|------|
| 核心概念 | Workflow DNA = 可复用的业务逻辑积木块 |
| 组装方式 | 将 DNA 积木块组装成流程定义（非编码） |
| 架构特点 | 模块化、可组合、适配性强 |
| 自动化集成 | Unified Control System (UCS) 统一编排人+AMR机器人 |
| 部署模式 | SaaS 或本地部署 |

**关键启示：**
- "DNA 积木块"概念与我们的"节点类型"思路高度一致
- 强调可组合性和复用性

---

#### Blue Yonder WMS — 参数驱动规则配置

**核心机制：** 参数驱动的规则配置 + Labor & Workflow Manager 工作流管理器。

| 维度 | 说明 |
|------|------|
| 配置方式 | 结构化参数屏幕 + 规则表（无可视化设计器） |
| 自定义逻辑 | 历史 Lua 脚本，新版转向参数化配置 |
| 任务调度 | Labor & Workflow Manager 管理工作分类和任务路由 |
| 授权模式 | SaaS 或本地许可（本地 ~$1M+起） |

---

#### Made4net SCExpert — 属性驱动业务规则

**核心机制：** **属性驱动（Attribute-driven）** — 基于物品/客户/供应商/库位/批次等属性自动驱动流程。

| 维度 | 说明 |
|------|------|
| 核心机制 | 属性驱动（item, customer, vendor, location, lot, expiration） |
| 规则层级 | 多级业务规则（组织 > 仓库 > 客户） |
| 扩展方式 | Code-free 自定义钩子（hooks into business logic layer） |
| 2025 地位 | Gartner WMS 魔力象限 Leader |
| 授权模式 | SaaS 或本地许可（$100-$500/用户/月） |

**关键启示：**
- 属性驱动的规则匹配思路可以借鉴（按仓库/货位类型/物品属性选择不同流程）

---

#### SnapFulfil — Workflow Rules Engine（无代码规则引擎）

**核心机制：** **无代码规则引擎** — 条件+动作 对，业务用户自助配置。

| 维度 | 说明 |
|------|------|
| 规则结构 | **Condition（条件）→ Action（动作）** 对 |
| 配置方式 | GUI 界面点击配置，秒级生效，无需停机 |
| 适用人群 | 仓库操作员（非 IT 人员） |
| 自动化编排 | SnapControl 层编排人+机器人任务分配 |
| 特色 | SnapBuddy 引导式配置助手 |

**关键启示：**
- "Condition → Action" 规则对是最直观的配置模型
- 秒级生效、无停机是配置化流程的关键用户体验
- 引导式配置助手降低使用门槛

---

#### Infor WMS (SCE) — 配置参数屏 + UDF

| 维度 | 说明 |
|------|------|
| 配置方式 | 综合配置参数屏（无拖拽设计器） |
| 特色 | 3D 仓库可视化、内置语音处理、Coleman AI |
| 任务调度 | 接近度优先的任务交叉（Proximity-based interleaving） |
| 授权模式 | 多租户 SaaS（$200-$400/用户/月） |

---

### 2.2 开源 .NET 工作流引擎

| 引擎 | Stars | 设计器 | 持久化 | License | 适用性评估 |
|------|-------|--------|--------|---------|-----------|
| **Elsa Workflows 3** | 6.5K+ | React 可视化设计器 | EF Core/MongoDB/Dapper | MIT | 功能最强但过重，React 设计器与 Vue 不兼容 |
| **Optimajet WorkflowEngine.NET** | 1.2K+ | **HTML5 设计器** | SQL Server/MySQL/PostgreSQL/Oracle | 商业（有免费版） | 设计器友好，但商业授权，API 复杂 |
| **Workflow Core** | 2.5K+ | 无（代码定义） | EF Core/MongoDB | MIT | 轻量，但缺少设计器，分支逻辑有限 |
| **CoreWF** (UiPath) | 700+ | 无 | 内置 | MIT | WF 的 .NET Core 移植，概念老旧 |
| **Stateless** | 5K+ | 无（状态图代码定义） | 无（内存） | MIT | 状态机库，不是工作流引擎，适合状态转换 |
| **Wexflow** | 800+ | Web UI | XML/SQLite/MySQL | MIT | 面向定时任务编排，非业务流程 |

### 2.3 行业核心规律总结

| # | 规律 | 详情 |
|---|------|------|
| 1 | **没有厂商用可视化拖拽设计器** | 所有厂商都用配置向导/规则表/参数屏，SnapFulfil 除外（Condition-Action 规则 GUI） |
| 2 | **条件匹配 + 步骤链是主流** | SAP 的 Condition Tech + POSC，SnapFulfil 的 Rules + Chained Actions |
| 3 | **属性驱动选择流程** | Made4net 按物品/客户/仓库属性，SAP 按仓库+单据类型 |
| 4 | **代码扩展是兜底** | SAP 用 BAdI，其他用 hooks，配置为主代码为辅 |
| 5 | **秒级生效是标配** | SnapFulfil 明确"秒级生效，无需停机" |
| 6 | **开源引擎过重或过轻** | Elsa 功能强但依赖重/React 不兼容；Workflow Core 轻量但缺设计器 |

---

## 三、方案设计

### 推荐方案：轻量级 Pipeline（借鉴行业最佳实践）

**设计理念：** 融合 SAP EWM 的条件匹配 + SnapFulfil 的规则引擎 + Korber 的 DNA 积木块概念，打造贴合 WMS 领域的流程引擎。

```
                    ┌─────────────────────────────┐
 WCS Request  ────→ │  FlowMatcher（条件匹配）     │
                    │  按 RequestType + 仓库属性     │
                    │  匹配 → FlowTemplate          │
                    └──────────┬──────────────────┘
                               │
                    ┌──────────▼──────────────────┐
                    │  FlowExecutor（Pipeline 执行） │
                    │  按节点顺序逐步执行             │
                    │  节点间通过 FlowContext 传递数据 │
                    └──────────┬──────────────────┘
                               │
            ┌──────────────────┼──────────────────┐
            ▼                  ▼                  ▼
     ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
     │ Node 1       │  │ Node 2       │  │ Node 3       │
     │ 验证参数     │→│ 查托盘       │→│ 检查状态     │
     │ (DNA 积木)   │  │ (DNA 积木)   │  │ (DNA 积木)   │
     └─────────────┘  └─────────────┘  └─────────────┘
```

### 与现有方案的关键区别

| 对比项 | 之前方案 | 优化后方案 |
|--------|---------|-----------|
| 流程匹配 | 仅按 Category + Phase | **条件匹配器**（仓库+请求类型+货位属性） |
| 节点配置 | 固定 ConfigJson | **动态配置表单**（借鉴 SnapFulfil 规则 GUI） |
| 失败处理 | Stop/Skip/GotoNode | **失败策略 + 重试机制**（借鉴 Manhattan CWV） |
| 引导配置 | 无 | **SnapBuddy 式引导**（预设常用模板） |
| 可视化 | 拖拽画布 | **列表式节点编排**（行业主流，非拖拽） |

---

## 三点五、方案验证 — 代码级批判性分析

> 以下分析基于对 6 个现有 Handler 源码的逐行审查。

### 现有 Handler 实际规模

| Handler | 文件 | 行数 | 步骤数 | 状态 |
|---------|------|------|--------|------|
| `InboundRequestHandler` | `Handlers/WcsRequest/` | 213 | 7 步 | 已实现（最复杂） |
| `OutboundRequestHandler` | `Handlers/WcsRequest/` | 136 | 5 步 | 已实现 |
| `MoveRequestHandler` | `Handlers/WcsRequest/` | 145 | 7 步 | 已实现 |
| `InboundCompletionHandler` | `Handlers/TaskCompletion/` | 159 | 6 步 | 已实现 |
| `OutboundCompletionHandler` | `Handlers/TaskCompletion/` | 190 | 7 步 | 已实现 |
| `MoveCompletionHandler` | `Handlers/TaskCompletion/` | **57** | **0 步** | **空壳 TODO** |

**总代码量：约 800 行，其中 1 个是空实现。**

### 问题 1：步骤间强耦合，并非真正可组合

**发现：** 读代码后发现，Handler 内的步骤通过共享 `DbContext`、`Unitload` 实体引用、事务边界紧密耦合。以 `InboundRequestHandler` 为例：

```
步骤 3e（创建 TransTask）→ 3f（更新 Unitload/Location 状态）
→ 3g（SaveChanges + Commit）→ 3h（下发 WCS）
```

`SaveChanges` 在 WCS 下发**之前**（先持久化再发 WCS，保证事务完整性）。如果拆成独立节点，需要一种机制在"创建任务"和"下发 WCS"之间插入事务提交点。

**解决：** `FlowNode.IsTransactionBoundary` 字段。Pipeline 执行器遇到此标记时自动执行 `SaveChanges + Commit`。

### 问题 2：代码重复 ≠ 需要流程引擎

**发现：** 三个 RequestHandler 之间确实有大量重复代码（ContainerCode 验证、Unitload 查询+状态检查、TransTask 创建模板、SaveChanges→Commit→WCS Send 模式）。

**结论：** 如果**唯一目的是消除重复**，提取公共方法就够了。但 Pipeline 的目标是**让流程可配置**（如质检环节可选），消除重复只是副产品。

### 问题 3：方案规模与实际需求不匹配

**发现：** 当前仅 800 行代码、5 个有效 Handler，但方案规划了 25 个新文件、15 个节点类型、4 张新表、前端编辑器。

**结论：** 用户确认一步到位实施。规模虽大，但属于一次性投入，后续扩展零成本。

### 问题 4：WCS 实时交互延迟

**发现：** 当前 Handler 直接在请求链路中执行，延迟低。Pipeline 每步需查库获取模板、记录日志。

**解决：** `IMemoryCache` 缓存模板 5 分钟（查表 ~0.01ms）+ FlowNodeLog 通过 `Task.Run` 异步写入（不阻塞主流程）。节点直接操作 FlowContext 中的 DbContext，无额外序列化开销。

---

## 四、核心领域模型

### 1. FlowTemplate（流程模板）

```csharp
[Table("FlowTemplates")]
public class FlowTemplate
{
    public int Id { get; set; }
    public string Name { get; set; }           // "标准入库"
    public string Code { get; set; }           // "INBOUND_STANDARD"
    public string Category { get; set; }       // "入库" / "出库" / "移库" / "质检" / "盘点"
    public string Phase { get; set; }         // "Request" / "Completion"
    public string Description { get; set; }
    public bool IsActive { get; set; }
    public bool IsBuiltIn { get; set; }       // 预设模板，不可删除
    public int SortOrder { get; set; }
    public string? MatchRules { get; set; }    // 条件匹配规则 JSON
    public DateTime CreatedTime { get; set; }
    public string? CreatedBy { get; set; }
}
```

**MatchRules 示例：**
```json
{
  "requestType": "入库",
  "warehouseId": null,
  "locationTags": ["标准"],
  "priority": 10
}
```

### 2. FlowNode（流程节点）

```csharp
[Table("FlowNodes")]
public class FlowNode
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public string NodeType { get; set; }       // "ValidateParams" / "FindUnitload" / ...
    public string NodeName { get; set; }       // "验证参数"
    public int StepOrder { get; set; }
    public string? ConfigJson { get; set; }    // 节点专属配置
    public bool IsEnabled { get; set; }
    public string? OnFailure { get; set; }     // "Stop" / "Skip" / "Retry:3"
    public string? SkipCondition { get; set; } // 跳过条件（可选）
    public bool IsTransactionBoundary { get; set; } // 事务断点：此节点后执行 SaveChanges + Commit
    public virtual FlowTemplate? Template { get; set; }
}
```

> **事务断点设计说明：** 现有 Handler 中 `SaveChanges → Commit → WCS Send` 是固定模式。
> 将其标记为 `IsTransactionBoundary`，Pipeline 执行器在此节点后自动执行 `SaveChanges + Commit`，
> 保留了现有事务语义，同时允许节点自由组合。入库流程中，`CreateTransTask` 之后、`SendWcsTask` 之前
> 设置事务断点，确保"先持久化再下发 WCS"的事务完整性。

### 3. FlowInstance（流程实例）

```csharp
[Table("FlowInstances")]
public class FlowInstance
{
    public int Id { get; set; }
    public string InstanceCode { get; set; }
    public int TemplateId { get; set; }
    public string BusinessType { get; set; }    // "WcsRequest" / "TaskCompletion"
    public string BusinessId { get; set; }
    public string Status { get; set; }         // "Running" / "Completed" / "Failed"
    public int CurrentNodeOrder { get; set; }
    public string? ContextJson { get; set; }
    public string? ErrorMsg { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime? CompletedTime { get; set; }
}
```

### 4. FlowNodeLog（节点执行日志）

```csharp
[Table("FlowNodeLogs")]
public class FlowNodeLog
{
    public int Id { get; set; }
    public int InstanceId { get; set; }
    public int NodeOrder { get; set; }
    public string NodeType { get; set; }
    public string NodeName { get; set; }
    public string Status { get; set; }         // "Success" / "Skipped" / "Failed"
    public long DurationMs { get; set; }
    public string? InputJson { get; set; }
    public string? OutputJson { get; set; }
    public string? ErrorMsg { get; set; }
    public DateTime CreatedTime { get; set; }
}
```

---

## 五、节点处理器（INodeHandler 注册表）

```csharp
public interface INodeHandler
{
    string NodeType { get; }              // "FindUnitload"
    string DisplayName { get; }           // "查托盘"
    string Category { get; }            // "数据查询" / "状态更新" / "外部交互" / "业务逻辑"
    string Description { get; }          // 节点功能描述（用于前端提示）
    string? ConfigSchema { get; }        // 配置 JSON Schema（用于前端动态表单）
    Task<NodeResult> ExecuteAsync(FlowContext context, string? configJson);
}
```

### 首批 15 个节点

| # | NodeType | DisplayName | Category | 说明 |
|---|----------|------------|----------|------|
| 1 | `ValidateParams` | 验证参数 | 基础 | 检查请求参数、位置状态 |
| 2 | `FindUnitload` | 查托盘 | 数据查询 | 按容器码查 Unitload |
| 3 | `CheckUnitloadStatus` | 检查托盘状态 | 验证 | BeingMoved/Allocated/Location |
| 4 | `MatchTag` | 工艺匹配 | 验证 | Location.Tag vs Unitload.NextOperation |
| 5 | `AllocateLocation` | 分配货位 | 业务逻辑 | 调用 LocationAllocator |
| 6 | `CheckLocationLimit` | 检查库位限制 | 验证 | InboundLimit/OutboundLimit |
| 7 | `CreateTransTask` | 创建运输任务 | 业务逻辑 | 生成 TaskCode，创建 TransTask |
| 8 | `SendWcsTask` | 下发WCS | 外部交互 | 调用 IWcsTaskBridge.SendTaskAsync |
| 9 | `UpdateUnitload` | 更新托盘 | 状态更新 | BeingMoved/Allocated/LocationId |
| 10 | `UpdateLocationCount` | 更新库位计数 | 状态更新 | InboundCount/OutboundCount |
| 11 | `RecordFlow` | 记录流水 | 数据持久化 | 创建 Flow 记录 |
| 12 | `ArchiveTask` | 归档任务 | 业务逻辑 | TransTask → ArchivedTask |
| 13 | `SplitUnitload` | 拆盘 | 业务逻辑 | 出库后拆盘 |
| 14 | `AdvanceOperation` | 工序推进 | 业务逻辑 | NextOperation → CurrentOperation |
| 15 | `HttpCallback` | HTTP回调 | 外部交互 | 调用 MES/第三方系统 (预留) |

### 后续扩展节点
- `WaitApproval` — 等待人工审批（需 Signal 机制）
- `ConditionBranch` — 条件分支（需节点连线）
- `CreateInspection` — 创建质检单

---

## 六、Pipeline 执行器

```csharp
public class FlowEngineService : IFlowEngine
{
    private readonly WmsDbContext _db;
    private readonly IDictionary<string, INodeHandler> _handlers;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FlowEngineService> _logger;

    /// <summary>
    /// 匹配模板（带 IMemoryCache 缓存，避免每次 WCS 请求查库）
    /// </summary>
    public async Task<FlowTemplate?> MatchTemplateAsync(
        string requestType, string phase, int? warehouseId = null, string? locationTag = null)
    {
        string cacheKey = $"flow:{requestType}:{phase}:{warehouseId}:{locationTag}";
        if (_cache.TryGetValue(cacheKey, out FlowTemplate? cached))
            return cached;

        var template = await _db.FlowTemplates
            .Include(t => t.Nodes.OrderBy(n => n.StepOrder))
            .Where(t => t.Category == requestType && t.Phase == phase && t.IsActive)
            .OrderByDescending(t => t.Priority)
            .FirstOrDefaultAsync();

        _cache.Set(cacheKey, template, TimeSpan.FromMinutes(5));
        return template;
    }

    /// <summary>
    /// 执行流程（管理事务边界 + 异步日志）
    /// </summary>
    public async Task<FlowResult> ExecuteAsync(FlowTemplate template, FlowContext context)
    {
        var nodes = template.Nodes.Where(n => n.IsEnabled).OrderBy(n => n.StepOrder).ToList();
        var instance = new FlowInstance { ... }; // 记录实例
        _db.FlowInstances.Add(instance);
        var nodeLogs = new List<FlowNodeLog>();

        foreach (var node in nodes)
        {
            // 检查跳过条件
            if (!string.IsNullOrEmpty(node.SkipCondition) && EvaluateCondition(node.SkipCondition, context))
            {
                nodeLogs.Add(new FlowNodeLog { Status = "Skipped", ... });
                continue;
            }

            var handler = _handlers[node.NodeType];
            var sw = Stopwatch.StartNew();
            try
            {
                var result = await handler.ExecuteAsync(context, node.ConfigJson);
                context.Merge(result.Output);

                // ★ 事务断点：此节点后执行 SaveChanges + Commit
                if (node.IsTransactionBoundary)
                {
                    await context.Db.SaveChangesAsync();
                    await context.Db.Database.CommitTransactionAsync();
                }

                if (result.Stop) break;
            }
            catch (Exception ex)
            {
                nodeLogs.Add(new FlowNodeLog { Status = "Failed", ErrorMsg = ex.Message, ... });
                switch (node.OnFailure)
                {
                    case "Stop": break;
                    case "Skip": continue;
                    default: break;
                }
            }
            finally
            {
                nodeLogs.Add(new FlowNodeLog { Status = "Success", DurationMs = sw.ElapsedMilliseconds, ... });
            }
        }

        // ★ 异步写入节点日志（不影响主流程性能）
        _ = Task.Run(() => SaveNodeLogsAsync(nodeLogs));

        return result;
    }
}
```

> **性能保障措施：**
> 1. **模板缓存**：`IMemoryCache` 缓存模板 5 分钟，避免每次 WCS 请求查库（~0.01ms vs ~2-5ms）
> 2. **异步日志**：FlowNodeLog 通过 `Task.Run` 异步写入，不阻塞 WCS 响应
> 3. **节点处理器无额外开销**：节点直接操作 FlowContext 中的 DbContext，与现有 Handler 性能一致

---

## 七、FlowContext（节点间共享数据）

```csharp
public class FlowContext
{
    // 共享 DbContext（所有节点操作同一被跟踪的实体实例）
    public WmsDbContext Db { get; }

    // 原始请求数据
    public WcsRequest? WcsRequest { get; set; }
    public WcsTask? WcsTask { get; set; }

    // 节点执行产出（EF Core 跟踪的实体引用）
    public Location? StartLocation { get; set; }
    public Location? TargetLocation { get; set; }
    public Unitload? Unitload { get; set; }
    public TransTask? TransTask { get; set; }

    // 当前遍历的容器码（foreach 循环内传递）
    public string? CurrentContainerCode { get; set; }

    // 通用字典
    public Dictionary<string, object> Data { get; } = new();

    // 元信息
    public string? BusinessType { get; set; }
    public string? BusinessId { get; set; }
    public string? CurrentUser { get; set; }

    public FlowContext(WmsDbContext db) { Db = db; }
}
```

> **设计说明：** FlowContext 持有 `WmsDbContext` 引用，确保所有节点操作的是同一被 EF Core 跟踪的
> 实体实例。节点内对 `context.Unitload.BeingMoved = true` 的修改会被 DbContext 自动跟踪，
> 无需在节点间传递 detached 实体。事务边界由 FlowExecutor 统一管理。

---

## 八、适配层（兼容现有 Handler）

**不删除现有 Handler，优先流程引擎，后备硬编码 Handler：**

```csharp
// WcsController 修改（最小改动）
public async Task<WcsResult> WcsRequest(WcsRequest requestInfo)
{
    var location = _locationService.GetLocation(requestInfo.LocationCode);

    // 1. 查找匹配的流程模板（条件匹配器）
    var template = await _flowEngine.MatchTemplateAsync(
        requestType: location.RequestType,
        phase: "Request",
        warehouseId: location.WarehouseId,
        locationTag: location.Tag
    );

    if (template != null)
    {
        var context = new FlowContext { WcsRequest = requestInfo, StartLocation = location };
        return await _flowEngine.ExecuteAsync(template, context);
    }

    // 2. 后备：硬编码 Handler（保证兼容）
    var handler = _requestHandlers.FirstOrDefault(h => h.RequestType == location.RequestType);
    return await handler.HandleAsync(requestInfo, location);
}
```

---

## 九、前端设计

### 1. 流程模板管理页面 (`flow-templates.vue`)

**设计风格：列表式节点编排（非拖拽，符合行业主流）**

| 功能 | 说明 |
|------|------|
| 模板列表 | 按分类筛选（入库/出库/移库），显示名称、节点数、状态、匹配规则 |
| 节点编排区 | 已添加的节点列表，支持上移/下移/启用/禁用/删除 |
| 节点工具箱 | 从工具箱点击添加节点到流程（分类：基础/数据/业务/交互/记录） |
| 节点配置 | 选中节点后右侧显示配置表单（基于 ConfigSchema 动态生成） |
| 预设模板 | 系统内置常用模板（标准入库/标准出库/标准移库），一键复制后可自定义 |

### 2. 流程实例监控页面 (`flow-instances.vue`)

| 功能 | 说明 |
|------|------|
| 实例列表 | 按状态/时间/模板筛选，显示业务ID、状态、耗时 |
| 节点执行详情 | 展开查看每个节点的执行状态/耗时/输入输出 |
| 失败重试 | 对失败实例支持重新执行 |

### 3. API 端点

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/v1/Flow/Templates` | 获取模板列表 |
| POST | `/api/v1/Flow/Templates` | 创建模板 |
| PUT | `/api/v1/Flow/Templates/{id}` | 更新模板 |
| DELETE | `/api/v1/Flow/Templates/{id}` | 删除模板 |
| GET | `/api/v1/Flow/Templates/{id}/Nodes` | 获取模板节点列表 |
| POST | `/api/v1/Flow/Templates/{id}/Nodes` | 添加节点 |
| PUT | `/api/v1/Flow/Templates/Nodes/{id}` | 更新节点 |
| DELETE | `/api/v1/Flow/Templates/Nodes/{id}` | 删除节点 |
| POST | `/api/v1/Flow/Templates/{id}/Nodes/Reorder` | 节点排序 |
| GET | `/api/v1/Flow/Templates/NodeTypes` | 获取所有可用节点类型（工具箱） |
| GET | `/api/v1/Flow/Instances` | 获取实例列表 |
| GET | `/api/v1/Flow/Instances/{id}` | 获取实例详情（含节点日志） |
| POST | `/api/v1/Flow/Instances/{id}/Retry` | 重试失败实例 |

---

## 十、文件清单

### 后端新建

| # | 文件路径 | 说明 |
|---|---------|------|
| 1 | `Domain/Entities/Flow/FlowTemplate.cs` | 流程模板实体 |
| 2 | `Domain/Entities/Flow/FlowNode.cs` | 流程节点实体 |
| 3 | `Domain/Entities/Flow/FlowInstance.cs` | 流程实例实体 |
| 4 | `Domain/Entities/Flow/FlowNodeLog.cs` | 节点执行日志实体 |
| 5 | `Domain/Flow/INodeHandler.cs` | 节点处理器接口 + NodeResult |
| 6 | `Domain/Flow/FlowContext.cs` | 流程上下文 |
| 7 | `Domain/Flow/IFlowEngine.cs` | 流程引擎接口 |
| 8 | `Infrastructure/Services/FlowEngineService.cs` | 流程引擎实现（含条件匹配器 + 执行器） |
| 9 | `Infrastructure/Flow/Nodes/ValidateParamsHandler.cs` | 验证参数节点 |
| 10 | `Infrastructure/Flow/Nodes/FindUnitloadHandler.cs` | 查托盘节点 |
| 11 | `Infrastructure/Flow/Nodes/CheckUnitloadStatusHandler.cs` | 检查状态节点 |
| 12 | `Infrastructure/Flow/Nodes/MatchTagHandler.cs` | 工艺匹配节点 |
| 13 | `Infrastructure/Flow/Nodes/AllocateLocationHandler.cs` | 分配货位节点 |
| 14 | `Infrastructure/Flow/Nodes/CreateTransTaskHandler.cs` | 创建任务节点 |
| 15 | `Infrastructure/Flow/Nodes/SendWcsTaskHandler.cs` | 下发WCS节点 |
| 16 | `Infrastructure/Flow/Nodes/UpdateUnitloadHandler.cs` | 更新托盘节点 |
| 17 | `Infrastructure/Flow/Nodes/RecordFlowHandler.cs` | 记录流水节点 |
| 18 | `Infrastructure/Flow/Nodes/ArchiveTaskHandler.cs` | 归档任务节点 |
| 19 | `Infrastructure/Flow/Nodes/SplitUnitloadHandler.cs` | 拆盘节点 |
| 20 | `Infrastructure/Flow/Nodes/AdvanceOperationHandler.cs` | 工序推进节点 |
| 21 | `Infrastructure/Flow/Nodes/CheckLocationLimitHandler.cs` | 检查库位限制节点 |
| 22 | `Infrastructure/Flow/Nodes/UpdateLocationCountHandler.cs` | 更新库位计数节点 |
| 23 | `Infrastructure/Flow/Nodes/HttpCallbackHandler.cs` | HTTP回调节点（预留） |
| 24 | `WebApi/Controllers/FlowController.cs` | 流程管理 API |
| 25 | `WebApi/Services/FlowTemplateSeeder.cs` | 预设模板种子数据 |

### 后端修改

| # | 文件路径 | 改动 |
|---|---------|------|
| 1 | `Infrastructure/Persistence/WmsDbContext.cs` | 添加 Flow 相关 DbSet |
| 2 | `WebApi/Controllers/Api/WcsController.cs` | 注入 FlowEngine，优先走流程引擎 |
| 3 | `WebApi/Services/Wcs/WcsTaskSyncService.cs` | 完成阶段优先走流程引擎 |
| 4 | `Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs` | 注册 FlowEngine + 所有 NodeHandler |
| 5 | `WebApi/Program.cs` | 注册 FlowEngine 服务 |

### 前端新建

| # | 文件路径 | 说明 |
|---|---------|------|
| 1 | `views/wms/sys/flow-templates.vue` | 流程模板管理 + 节点编排 |
| 2 | `views/wms/sys/flow-instances.vue` | 流程实例监控 |

### 前端修改

| # | 文件路径 | 改动 |
|---|---------|------|
| 1 | `service/api/wms-sys.ts` | 新增 Flow API 函数 |
| 2 | `router/elegant/imports.ts` | 新页面懒加载 |
| 3 | `service/api/route.ts` | URL_TO_VIEW_KEY 添加映射 |

---

## 十一、实施分期

### 第一期：核心框架 + 入库流程迁移

1. 新建 4 个实体 + 数据库表（FlowTemplates, FlowNodes, FlowInstances, FlowNodeLogs）
2. 实现 IFlowEngine + FlowEngineService（条件匹配器 + Pipeline 执行器）
3. 实现入库 Request 阶段所需的 7 个节点
4. WcsController 注入 FlowEngine，条件匹配优先、硬编码后备
5. 预设"标准入库"模板种子数据
6. 基础前端：流程模板列表 + 节点编排页面

### 第二期：完成阶段 + 出库/移库迁移

1. 入库 Completion 流程迁移（6 个节点）
2. 出库 Request + Completion 流程迁移
3. 移库 Request + Completion 流程迁移
4. 补齐剩余 8 个节点实现
5. 流程实例监控页面

### 第三期：增强功能

1. 节点动态配置表单（基于 ConfigSchema）
2. 条件跳过节点（SkipCondition）
3. 失败重试机制
4. 预设更多模板（质检入库、急件出库等）

---

## 十二、验证清单

1. `dotnet build` 编译通过
2. 数据库自动创建 Flow 相关 4 张表
3. 预设"标准入库"模板正确写入数据库
4. 模拟 WCS 入库请求，流程引擎按节点顺序执行并记录日志
5. 前端模板管理页面可查看/编辑节点配置
6. 新增自定义模板（如"质检入库"），无需改代码即可生效
7. 现有硬编码 Handler 作为后备仍正常工作
8. FlowInstances 表正确记录实例状态和节点执行日志
9. 失败实例可通过 API 重试
