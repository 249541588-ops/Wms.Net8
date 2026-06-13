# WMS ↔ WCS 共享数据库中间表通信方案

## 1. 概述

### 1.1 背景

WMS（仓储管理系统）与 WCS（仓储控制系统）需要实时交换搬运任务数据。当前采用**共享数据库中间表**模式，通过 ctask 数据库中的 `wcs_tasks` 表进行通信。后期将支持 **HTTP API** 模式作为替代方案。

### 1.2 通信流程

```
┌───────────┐     ①请求      ┌───────────┐     ②写入      ┌──────────────┐
│           │ ────────────→  │           │ ────────────→  │              │
│   WCS     │                │   WMS     │                │ ctask 数据库  │
│           │  ③轮询读取     │           │  ⑤轮询读取     │ wcs_tasks 表  │
│           │ ←────────────  │           │ ←────────────  │              │
│           │     ④回写状态   │           │                │              │
└───────────┘ ────────────→ └───────────┘                └──────────────┘
```

**流程说明**：

1. WCS 通过 HTTP API 请求 WMS（`POST /api/v1/Wcs`）
2. WMS 处理业务逻辑后，将搬运任务写入 `ctask.dbo.wcs_tasks`
3. WCS 定时轮询 `wcs_tasks`，读取待执行任务
4. WCS 执行完毕后，回写 `wcs_state`、`act_end_loc`、`completed_at` 等字段
5. WMS 定时轮询 `wcs_tasks`，同步状态到 `WmsDb.dbo.TransTasks` 并通过 SignalR 推送前端

---

## 2. 数据库表结构

### 2.1 wcs_tasks（ctask 数据库）

| 字段名 | 类型 | 可空 | 默认值 | 说明 |
|--------|------|------|--------|------|
| **task_code** | nvarchar(20) | NO | NULL | 任务编码（主键） |
| task_type | nvarchar(20) | NO | NULL | 任务类型（入库/出库/移库） |
| cont_code | nvarchar(20) | NO | NULL | 容器编码 |
| cont_type | nvarchar(10) | NO | NULL | 容器类型 |
| start_loc | nvarchar(20) | NO | NULL | 起始库位编码 |
| end_loc | nvarchar(20) | NO | NULL | 目标库位编码 |
| act_end_loc | nvarchar(20) | YES | NULL | 实际到达库位编码 |
| prio | int | NO | 0 | 优先级 |
| sent_at | datetime2 | NO | NULL | 任务下发时间 |
| **wms_state** | nvarchar(20) | NO | NULL | WMS 端任务状态 |
| wms_failuir_times | int | NO | 0 | WMS 失败次数（原表拼写） |
| **wcs_state** | nvarchar(20) | NO | NULL | WCS 端任务状态 |
| completed_at | datetime2 | YES | NULL | 完成时间 |
| location_group | nvarchar(50) | YES | NULL | 库位组 |
| updated_at | datetime2 | NO | NULL | 最后更新时间 |
| err_code | int | NO | 0 | 错误码 |
| err_msg | nvarchar(max) | YES | NULL | 错误信息 |
| wms_note | nvarchar(max) | YES | NULL | WMS 备注 |
| wcs_note | nvarchar(max) | YES | NULL | WCS 备注 |
| ex1 | nvarchar(max) | YES | NULL | 扩展字段 1 |
| ex2 | nvarchar(max) | YES | NULL | 扩展字段 2 |
| warehouse | nvarchar(20) | YES | NULL | 仓库 |

### 2.2 TransTasks（WmsDb 数据库）— 已有表

关键字段与 wcs_tasks 的映射关系：

| TransTasks 字段 | wcs_tasks 字段 | 说明 |
|------------------|----------------|------|
| WcsTaskId | task_code | 任务编码关联 |
| WcsTaskState | wcs_state | WCS 任务状态镜像 |
| WcsLastUpdateTime | updated_at | 最后同步时间 |
| TaskState | wms_state | WMS 任务状态 |

---

## 3. 架构设计

### 3.1 技术选型

| 决策点 | 选择 | 理由 |
|--------|------|------|
| 访问 ctask 数据库 | **Dapper + SqlConnection** | 轻量、无迁移、外部表不应被 WMS 管理 schema |
| 轮询方式 | **Hangfire RecurringJob** | 项目已集成，有仪表盘监控 |
| 通信模式扩展 | **适配器模式（IWcsTaskBridge）** | 支持 Database / HTTP 两种模式切换 |

### 3.2 分层架构

```
┌─────────────────────────────────────────────────────────┐
│                    WcsController                       │  API 层
│               (接收 WCS 请求，触发任务下发)                  │
├─────────────────────────────────────────────────────────┤
│                WcsTaskSyncService                        │  应用服务层
│     (下发任务 / 同步状态 / SignalR 推送通知)               │
├─────────────────────────────────────────────────────────┤
│                  IWcsTaskBridge                          │  适配器接口
│  ┌──────────────────┐    ┌──────────────────┐            │
│  │DatabaseWcsTaskBridge│    │HttpWcsTaskBridge│            │  基础设施层
│  │ (Dapper 访问 ctask) │    │  (HTTP API 调用) │            │
│  └──────────────────┘    └──────────────────┘            │
├─────────────────────────────────────────────────────────┤
│              ICtaskDbService                             │  数据访问层
│          (Dapper CRUD 操作 wcs_tasks)                     │
├─────────────────────────────────────────────────────────┤
│    ctask 数据库 (wcs_tasks)  ←→  WmsDb (TransTasks)       │  数据库
└─────────────────────────────────────────────────────────┘
```

### 3.3 状态流转

```
           WMS 端                           WCS 端
          wms_state                         wcs_state
              │                                  │
  ┌─────┐  ②已下发  ┌─────────┐  ③执行中  ┌─────────┐
  │待下发│ ───────→ │  已下发  │ ←─────── │  执行中  │
  └─────┘         └─────────┘         └─────────┘
                                          │
                                          │ ④
                                    ┌─────┴─────┐
                                    │           │
                              ┌─────┴───┐ ┌───┴─────┐
                              │ 已完成   │ │  失败   │
                              └─────┬───┘ └───┬─────┘
                                    │         │
                              ⑤同步     ⑤同步
                                    ▼         ▼
                              TransTask  TransTask
                              更新状态    更新状态
```

---

## 4. 接口定义

### 4.1 ICtaskDbService — ctask 数据库访问

```csharp
namespace Wms.Core.Domain.Interfaces;

/// <summary>
/// ctask 数据库访问接口（Dapper 实现）
/// </summary>
public interface ICtaskDbService
{
    /// <summary>
    /// 写入搬运任务到 wcs_tasks
    /// </summary>
    Task WriteTaskAsync(WcsTask task);

    /// <summary>
    /// 根据 task_code 查询任务
    /// </summary>
    Task<WcsTask?> ReadByTaskCodeAsync(string taskCode);

    /// <summary>
    /// 查询所有未完成的任务（wcs_state 不等于 已完成/失败/已取消）
    /// </summary>
    Task<IReadOnlyList<WcsTask>> ReadPendingTasksAsync();

    /// <summary>
    /// 查询 updated_at 大于指定时间的任务（增量同步）
    /// </summary>
    Task<IReadOnlyList<WcsTask>> ReadTasksUpdatedAfterAsync(DateTime since);

    /// <summary>
    /// 更新 WMS 端状态
    /// </summary>
    Task UpdateWmsStateAsync(string taskCode, string state);
}
```

### 4.2 IWcsTaskBridge — 通信适配器

```csharp
namespace Wms.Core.Domain.Interfaces;

/// <summary>
/// WCS 任务通信适配器接口（支持 Database / Http 两种模式）
/// </summary>
public interface IWcsTaskBridge
{
    /// <summary>
    /// 下发任务到 WCS（写入中间表或调用 HTTP API）
    /// </summary>
    Task SendTaskAsync(TransTask transTask);

    /// <summary>
    /// 轮询 WCS 任务状态变更
    /// </summary>
    Task<IReadOnlyList<WcsTask>> PollStatusChangesAsync();
}
```

---

## 5. 配置说明

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=.;Initial Catalog=WmsDb;...",
    "CtaskConnection": "Data Source=YOUR_SQL_SERVER;Initial Catalog=ctask;Persist Security Info=True;User ID=YOUR_DB_USER;Password=YOUR_DB_PASSWORD;TrustServerCertificate=True;"
  },
  "Wcs": {
    "Mode": "Database",
    "Endpoint": ""
  }
}
```

- `Wcs:Mode`: `"Database"` 使用共享数据库模式（默认），`"Http"` 使用 HTTP API 模式
- `Wcs:Endpoint`: HTTP 模式下的 WCS 服务地址

---

## 6. 跨库事务策略

由于 WMS 写入 WmsDb 和 ctask 涉及两个不同数据库，无法使用单一 EF Core 事务。

**策略：主事务 + 最佳努力**

```
1. WmsDb 事务内：创建/更新 TransTask（核心业务）
2. WmsDb 事务提交后：写入 wcs_tasks（通信层）
3. 写入 wcs_tasks 失败：记录日志，由 Hangfire 轮询任务重试
```

**重试机制**：WcsTaskSyncService 在同步时检查是否存在"已创建 TransTask 但未写入 wcs_tasks"的记录，自动补发。

---

## 7. 任务完成处理器（策略模式）

WCS 完成搬运后，不同任务类型（入库/出库/移库/叠盘/拆盘）需要不同的业务处理逻辑。采用**策略模式**解耦。

### 7.1 接口定义

**文件**: `Wms.Core.Domain/Interfaces/ITaskCompletionHandler.cs`

```csharp
/// <summary>
/// 任务完成处理器接口（策略模式）
/// </summary>
public interface ITaskCompletionHandler
{
    /// <summary>
    /// 处理的任务类型（对应 wcs_tasks.task_type）
    /// </summary>
    string TaskType { get; }

    /// <summary>
    /// 任务完成后的业务处理
    /// </summary>
    Task HandleAsync(WcsTask wcsTask);
}
```

### 7.2 处理器实现

每个 Handler 负责"通用状态同步之后"的具体业务逻辑。

| Handler | task_type | 完成后处理流程 |
|---------|-----------|----------------|
| **InboundCompletionHandler** | 入库 | 更新库位 → 创建入库记录 → 更新 Unitload.LocationId → StockFlow |
| **OutboundCompletionHandler** | 出库 | 更新库位 → 扣减库存 → 更新出库单状态 → StockFlow |
| **MoveCompletionHandler** | 移库 | 更新 Unitload.LocationId（起始→目标）→ StockFlow |
| **StackCompletionHandler** | 叠盘 | 合并 Unitload → 更新数量 → StockFlow |
| **UnstackCompletionHandler** | 拆盘 | 拆分 Unitload → 创建新 Unitload → StockFlow |

### 7.3 同步服务调用流程

```
WcsTaskSyncService.SyncStatusAsync():
  1. Bridge.PollStatusChangesAsync() → 获取已变更的 wcs_tasks
  2. 遍历每个变更任务:
     a. 通用同步: 更新 TransTask.TaskState / WcsTaskState / WcsLastUpdateTime
     b. 分发处理: 根据 task_type 找到对应 ITaskCompletionHandler → handler.HandleAsync(wcsTask)
     c. SignalR 推送前端
```

### 7.4 DI 注册

```csharp
// Program.cs
services.AddScoped<ITaskCompletionHandler, InboundCompletionHandler>();
services.AddScoped<ITaskCompletionHandler, OutboundCompletionHandler>();
services.AddScoped<ITaskCompletionHandler, MoveCompletionHandler>();
// ... 按需注册更多
```

WcsTaskSyncService 通过 `IEnumerable<ITaskCompletionHandler>` 注入，按 `task_type` 匹配对应 Handler。

---

## 7. 文件清单

| 操作 | 文件 | 说明 |
|------|------|------|
| 新增 | `Wms.Core.Domain/Entities/Transport/WcsTask.cs` | 中间表映射实体（纯 POCO，无 EF 特性） |
| 新增 | `Wms.Core.Domain/Enums/WcsTaskState.cs` | 任务状态枚举 |
| 新增 | `Wms.Core.Domain/Interfaces/ICtaskDbService.cs` | ctask 数据访问接口 |
| 新增 | `Wms.Core.Domain/Interfaces/IWcsTaskBridge.cs` | 通信适配器接口 |
| 新增 | `Wms.Core.Domain/Interfaces/ITaskCompletionHandler.cs` | 完成阶段策略接口 |
| 新增 | `Wms.Core.Infrastructure/Persistence/CtaskDbService.cs` | Dapper 实现（独立 SqlConnection） |
| 新增 | `Wms.Core.Infrastructure/Clients/DatabaseWcsTaskBridge.cs` | 数据库模式实现 |
| 新增 | `Wms.Core.Infrastructure/Clients/HttpWcsTaskBridge.cs` | HTTP 模式（壳） |
| 新增 | `Wms.Core.Application/Handlers/WcsRequest/IWcsRequestHandler.cs` | 请求阶段策略接口 |
| 新增 | `Wms.Core.Application/Handlers/WcsRequest/InboundRequestHandler.cs` | 入库请求处理 |
| 新增 | `Wms.Core.Application/Handlers/WcsRequest/OutboundRequestHandler.cs` | 出库请求处理 |
| 新增 | `Wms.Core.Application/Handlers/WcsRequest/MoveRequestHandler.cs` | 移库请求处理 |
| 新增 | `Wms.Core.Application/Handlers/TaskCompletion/InboundCompletionHandler.cs` | 入库完成处理 |
| 新增 | `Wms.Core.Application/Handlers/TaskCompletion/OutboundCompletionHandler.cs` | 出库完成处理 |
| 新增 | `Wms.Core.Application/Handlers/TaskCompletion/MoveCompletionHandler.cs` | 移库完成处理 |
| 新增 | `Wms.Core.WebApi/Services/Wcs/WcsTaskSyncService.cs` | 状态同步服务（编排层） |
| 新增 | `Wms.Core.WebApi/Jobs/WcsTaskSyncJob.cs` | Hangfire 定时任务 |
| 新增 | `Wms.Core.WebApi/Controllers/Sys/JobScheduleController.cs` | 定时任务管理 API |
| 新增 | `Wms.Vue/src/views/wms/sys/job-schedule.vue` | 定时任务管理页面 |
| 修改 | `Wms.Vue/src/service/api/wms-sys.ts` | 追加任务管理 API 函数 |
| 修改 | `appsettings.json` | 添加 CtaskConnection + Wcs:Mode |
| 修改 | `Program.cs` | 注册 DI + Hangfire RecurringJob |
| 修改 | `WcsController.cs` | 改为策略分发模式 |
| 修改 | `Wms.Core.Application/Wms.Core.Application.csproj` | 添加 Domain 项目引用 |
| 修改 | `Wms.Core.Infrastructure/Wms.Core.Infrastructure.csproj` | 添加 Logging/Configuration 包 |

---

## 8. 定时任务管理页面

### 8.1 设计目标

提供前端页面统一管理所有 Hangfire RecurringJob，支持启动/暂停、手动触发、修改频率、查看执行历史。

### 8.2 后端 API

**文件**: `Wms.Core.WebApi/Controllers/Sys/JobScheduleController.cs`

通过 Hangfire `IBackgroundJobClient` + `RecurringJobDto` 提供管理接口：

| 接口 | 方法 | 路径 | 说明 |
|------|------|------|------|
| 任务列表 | GET | `/api/v1/JobSchedule` | 获取所有 RecurringJob |
| 手动触发 | POST | `/api/v1/JobSchedule/{id}/trigger` | 立即执行一次 |
| 暂停 | POST | `/api/v1/JobSchedule/{id}/pause` | 暂停任务 |
| 恢复 | POST | `/api/v1/JobSchedule/{id}/resume` | 恢复任务 |
| 修改频率 | PUT | `/api/v1/JobSchedule/{id}/cron` | 修改 Cron 表达式 |
| 执行历史 | GET | `/api/v1/JobSchedule/{id}/history` | 最近执行记录 |

**实现要点**：
- Hangfire 1.8.23 内置 `RecurringJobDto` 提供 ID、Cron、LastExecution、NextExecution 等字段
- 使用 `JobStorage.GetConnection().GetRecurringJobs()` 获取任务列表
- 使用 `BackgroundJobClient.Trigger()` / `AddOrUpdate()` 实现触发和修改
- 执行历史通过 `JobStorage.GetConnection().GetJobDetails()` 或查询 Hangfire 内置表获取

### 8.3 前端页面

**文件**: `Wms.Vue/src/views/wms/sys/job-schedule.vue`

**页面布局**：
```
┌────────────────────────────────────────────────────────────┐
│  定时任务管理                                              │
├────────────────────────────────────────────────────────────┤
│  [查询] [刷新]                                               │
├────────────────────────────────────────────────────────────┤
│  名称    │ Cron   │ 上次执行       │ 下次执行       │ 状态 │ 操作       │
│ ─────────┼────────┼───────────────┼───────────────┼──────┼────────── │
│  WCS任务 │ */30 * *│ 10:30:05     │ 10:30:35     │ 运行 │ 触发 暂停 │
│  超时清理 │ 0 * * * │ 09:00:00     │ 10:00:00     │ 运行 │ 触发 暂停 │
│  库存对账 │ 0 2 * * │ 02:00:00     │ 明天 02:00   │ 运行 │ 触发 暂停 │
├────────────────────────────────────────────────────────────┤
│  [执行历史] ← 点击可查看最近 50 条执行记录（弹窗）             │
└────────────────────────────────────────────────────────────┘
```

**API 函数**（追加到 `wms-system.ts`）：
```typescript
fetchGetJobSchedules()
fetchTriggerJob(id)
fetchPauseJob(id)
fetchResumeJob(id)
fetchUpdateJobCron(id, cron)
fetchGetJobHistory(id)
```

### 8.4 Cron 表达式参考

| 表达式 | 说明 |
|--------|------|
| `*/30 * * * * *` | 每 30 秒 |
| `0 * * * * *` | 每分钟 |
| `0 0 * * * *` | 每小时 |
| `0 2 * * *` | 每天凌晨 2 点 |
| `0 0 ? * MON` | 每周一 |

---

## 9. 验证步骤

1. `dotnet build` 编译通过
2. 创建 TransTask → ctask.wcs_tasks 出现新记录
3. 手动修改 wcs_tasks.wcs_state → WMS 轮询后 TransTask.WcsTaskState 同步
4. Hangfire 仪表盘 `/hangfire` 看到 `wcs-task-sync` 任务
5. 切换 `Wcs:Mode=Http` → 应用启动正常（调用时抛 NotImplemented）
