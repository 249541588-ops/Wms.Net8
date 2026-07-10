# WMS 综合修复实时追踪计划

> **文档定位**：基于 6 轮深度审查（架构 / 性能 / 安全 / 业务逻辑 / 基础设施）共 **179 项发现** 的实时修复追踪文档。
>
> **更新规则**：每次完成一项修复后，更新该项的 `状态`、`完成时间`、`验证人` 字段，并在 [更新日志](#更新日志) 追加一行。统计仪表盘每周一同步。
>
> **配套阅读**：
> - [WMS-改造实施计划.md](WMS-改造实施计划.md) — 既有架构重构计划（Phase 1-5）
> - [WMS底层封装与平台-项目分离方案.md](Wms底层封装与平台-项目分离方案.md) — 底层 DLL 分离方案
> - [SECURITY-REMEDIATION-PLAN.md](SECURITY-REMEDIATION-PLAN.md) — 安全加固专项（部分项重叠）
> - [WMS-改造计划审查报告.md](WMS-改造计划审查报告.md) — 早期 53 项审查（已部分合并到本计划）

---

## 一、概览仪表盘

### 1.1 按严重度统计（实时）

| 严重度 | 总数 | 已完成 | 进行中 | 已验证 | 待处理 | 完成率 |
|--------|------|--------|--------|--------|--------|--------|
| **P0 致命** | 24 | 0 | 0 | 0 | 24 | 0% |
| **P1 严重** | 38 | 0 | 0 | 0 | 38 | 0% |
| **P2 中等** | 67 | 0 | 0 | 0 | 67 | 0% |
| **P3 低** | 50 | 0 | 0 | 0 | 50 | 0% |
| **合计** | **179** | **0** | **0** | **0** | **179** | **0%** |

> 最近更新：2026-07-09

### 1.2 按维度统计

| 维度 | 总数 | P0 | P1 | P2 | P3 |
|------|------|----|----|----|----|
| 架构（含分离方案审查） | 89 | 6 | 18 | 35 | 30 |
| 性能 | 32 | 5 | 8 | 15 | 4 |
| 安全（注入/认证/加密） | 28 | 4 | 9 | 11 | 4 |
| 业务逻辑 | 17 | 9 | 5 | 3 | 0 |
| 基础设施 | 13 | 0 | 3 | 3 | 7 |

### 1.3 当前阶段

**阶段**：尚未启动
**下一里程碑**：Phase S0 完成（24 项 P0 致命修复）
**阻塞项**：无

---

## 二、修复阶段路线图

```
┌─────────────────────────────────────────────────────────────────────┐
│ Phase S0：紧急修复（P0 致命，立即）                                   │
│ - 安全 P0：4 项（认证绕过、SQL 注入、默认账号）                        │
│ - 业务 P0：9 项（超卖、库位超载、双深位、OutboundTimerController）     │
│ - 性能 P0：5 项（Repository、N+1、GC.Collect、Version、库位锁）        │
│ - 架构 P0：6 项（Logging、Engine 归属、Controller 越层）              │
│ - 阻塞：必须先于此完成，才能进入项目分离 Phase 0                      │
├─────────────────────────────────────────────────────────────────────┤
│ Phase S1：严重修复（P1，1-2 周）                                     │
│ - 安全 P1：9 项（HTTPS、凭据脱敏、SQL Validator、WCS 签名）           │
│ - 业务 P1：5 项（状态机、删除引用、批量审计、幂等性）                  │
│ - 性能 P1：8 项（NLog async、AsNoTracking、AsSplitQuery）             │
│ - 架构 P1：18 项（包治理、工程基线、DI 重组）                         │
├─────────────────────────────────────────────────────────────────────┤
│ Phase S2：中期治理（P2，1 个月）                                     │
│ - 完成所有中等优先级问题，启动项目分离 Phase 1-3                      │
├─────────────────────────────────────────────────────────────────────┤
│ Phase S3：长期治理（P3，季度）                                       │
│ - 低优先级问题 + 工程现代化（SourceLink、CPM、Analyzer）              │
│ - 启动项目分离 Phase 4（物理仓库分离）                                │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 三、Phase S0 — 致命修复（24 项，必须立即执行）

### 安全 P0（4 项）

#### S0-SEC-001 · JWT SecretKey 占位符绕过生产检查

| 字段 | 值 |
|------|-----|
| **严重度** | P0 致命（CVSS 10.0） |
| **维度** | 安全 - 认证 |
| **来源** | 第二轮 S0-1 |
| **位置** | [AuthenticationExtensions.cs:38-46](../src/Wms.Core.WebApi/Extensions/AuthenticationExtensions.cs#L38-L46), [appsettings.Production.json:17](../src/Wms.Core.WebApi/appsettings.Production.json#L17) |
| **依赖** | 无 |
| **状态** | 待处理 |

**现象**：启动检查 `secretKey.StartsWith("CHANGE_ME")`，但生产配置是 `"CHANGE_THIS_IN_PRODUCTION..."`，以 `CHANGE_THIS` 开头**不匹配检查**。占位符密钥被直接使用。

**攻击场景**：任何拿到源码的攻击者可用相同 SecretKey 签发 Admin 角色 JWT，实现完全认证绕过。

**修复步骤**：
1. 改启动检查为强制验证：`SecretKey.Length >= 32 && !IsKnownPlaceholder(SecretKey)`
2. 已知占位符黑名单：`CHANGE_ME_*`、`CHANGE_THIS_*`、`YourSuperSecretKey*`、`DevelopmentSecretKey*`
3. 生产环境强制从环境变量 / Key Vault 读取，移除 appsettings.Production.json 中的默认值
4. 立即轮换 SecretKey 到强随机值（`RandomNumberGenerator.GetBytes(32)`）
5. 强制所有已登录用户重新登录

**验证方式**：
- 单元测试：占位符密钥在所有环境下启动抛异常
- 渗透测试：尝试用源码中的占位符签发 JWT，应被拒绝

---

#### S0-SEC-002 · 默认账号 admin/admin123

| 字段 | 值 |
|------|-----|
| **严重度** | P0 致命（CVSS 9.8） |
| **维度** | 安全 - 认证 |
| **来源** | 第二轮 S0-2 |
| **位置** | [DbInitializer.cs:133](../src/Wms.Core.WebApi/Services/DbInitializer.cs#L133) |
| **依赖** | 无 |
| **状态** | 待处理 |

**现象**：`ADMIN_DEFAULT_PASSWORD` 环境变量未设置时默认密码 `admin123`，且 `isProduction` 判断使用 `!_env.IsDevelopment() && !_env.IsEnvironment("Local")`，自定义环境名（Staging/QA）会创建默认管理员。

**攻击场景**：攻击者直接 `admin / admin123` 登录获取 Admin 权限。

**修复步骤**：
1. 移除默认密码 fallback：环境变量未设置时**拒绝启动**而非使用默认值
2. 启动时检查 admin 账号的密码哈希是否为已知弱密码（BCrypt 比对），命中则强制改密
3. 生产环境首次启动强制改密（`MustChangePassword` 字段）
4. 立即对所有现有部署执行凭据旋转（参见 [CREDENTIAL-ROTATION-RUNBOOK.md](CREDENTIAL-ROTATION-RUNBOOK.md)）

**验证方式**：
- 启动测试：未设环境变量时启动失败
- 现有部署：admin 密码已变更

---

#### S0-SEC-003 · UsersController / RoleController Admin 角色限制被注释

| 字段 | 值 |
|------|-----|
| **严重度** | P0 致命（CVSS 9.1） |
| **维度** | 安全 - 授权 |
| **来源** | 第二轮 S0-3 |
| **位置** | [UsersController.cs:22-24](../src/Wms.Core.WebApi/Controllers/UsersController.cs#L22-L24), [RoleController.cs:24-25](../src/Wms.Core.WebApi/Controllers/RoleController.cs#L24-L25), 以及 RoleController 内多处（215、246、347），BasicDictionaryController:268, MenusController:383,424 |
| **依赖** | 无 |
| **状态** | 待处理 |

**现象**：`[Authorize(Roles = "Admin")]` 在 7 个位置被注释掉。任何已认证的普通用户可以创建/删除用户、重置任意用户密码（[UsersController.cs:375-396](../src/Wms.Core.WebApi/Controllers/UsersController.cs#L375-L396) 不验证旧密码、不检查角色）。

**攻击场景**：普通用户重置 admin 密码 → 接管系统。

**修复步骤**：
1. 恢复所有注释的 `[Authorize(Roles = "Admin")]`：
   - UsersController 类级
   - RoleController 类级 + 第 215、246、347 行
   - BasicDictionaryController:268
   - MenusController:383, 424
2. ResetPassword 端点：增加权限校验 + 操作日志 + 强制通知原账号邮箱（如有）
3. 增加单元测试：非 Admin 角色调用应返回 403

**验证方式**：
- 渗透测试：普通用户 Token 调用 `/api/users/{id}/resetpassword` 应返回 403

---

#### S0-SEC-004 · DynamicSqlProvider `{columns}` SQL 注入

| 字段 | 值 |
|------|-----|
| **严重度** | P0 致命（CVSS 9.0） |
| **维度** | 安全 - 注入 |
| **来源** | 第二轮 S0-4 |
| **位置** | [DynamicSqlProvider.cs:67-69](../src/Wms.Core.Infrastructure/Services/ReportProviders/DynamicSqlProvider.cs#L67-L69) |
| **依赖** | 无 |
| **状态** | 待处理 |

**现象**：前端传入的 `_columns` 数组直接拼入 SQL SELECT 子句。SqlValidator 只验证模板（含 `{columns}` 占位符），**不验证替换后的最终 SQL**，存在 TOCTOU 漏洞。

**利用 Payload**（已登录用户即可）：
```json
{
  "Filters": {
    "_columns": "[\"(SELECT TOP 1 UserName + CHAR(124) + PasswordHash FROM Users) AS data\"]"
  }
}
```

**修复步骤**：
1. `columns` 列表必须在 `ReportConfig.AvailableColumns` 白名单内校验
2. SqlValidator 改为验证**最终拼接后的 SQL**，而非仅验证模板
3. 引入二次防御：数据库连接使用**只读账号** + 限制 schema 访问
4. 短期缓解：禁用 `ReportType = "Custom"` 报表（如有）

**验证方式**：
- 注入测试：上述 payload 应被拒绝
- 单元测试：白名单外的列名抛 `ArgumentException`

---

### 业务逻辑 P0（9 项）

#### S0-BIZ-001 · OutboundTimerController 完全无认证 ⚠️

| 字段 | 值 |
|------|-----|
| **严重度** | P0 致命（CVSS 9.8） |
| **维度** | 业务 - 越权 |
| **来源** | 第四轮 BL-6 |
| **位置** | [OutboundTimerController.cs:17](../src/Wms.Core.WebApi/Controllers/Api/OutboundTimerController.cs#L17) |
| **依赖** | S0-SEC-005（IP 白名单修复） |
| **状态** | 待处理 |

**现象**：`[AllowAnonymous]` + 仅 IP 白名单（且可被 X-Forwarded-For 绕过），**任何匿名用户可触发批量出库**（高温/HC/FR/空托盘）。一次调用即可清空库位。

**修复步骤**：
1. 移除 `[AllowAnonymous]`
2. 加 `[InternalIpWhitelist]`（修复后）
3. 增加 HMAC 签名认证：定时器调用必须携带 `X-Timer-Signature` 头
4. 短期缓解：在网络层（防火墙/反向代理）限制访问

**验证方式**：
- 匿名调用应返回 401
- 错误签名应返回 403

---

#### S0-BIZ-002 · IP 白名单空配置放行

| 字段 | 值 |
|------|-----|
| **严重度** | P0 致命 |
| **维度** | 业务 - 越权 |
| **来源** | 第四轮 BL-17 |
| **位置** | [InternalIpWhitelistAttribute.cs:27-29](../src/Wms.Core.WebApi/Filters/InternalIpWhitelistAttribute.cs#L27-L29) |
| **依赖** | 无 |
| **状态** | 待处理 |

**现象**：当 `Wcs:AllowedIps` 未配置时直接 `return` 放行所有请求。

**修复步骤**：
1. 空配置时 `Deny All` 而非 `Allow All`
2. 启动时强制校验 `Wcs:AllowedIps` 至少包含一个 IP
3. 单元测试：空配置时所有请求返回 403

**验证方式**：移除配置后调用 WCS 接口应 403

---

#### S0-BIZ-003 · Stock.Quantity 无原子性 + 无负库存防护

| 字段 | 值 |
|------|-----|
| **严重度** | P0 致命 |
| **维度** | 业务 - 数据完整性 |
| **来源** | 第四轮 BL-1 |
| **位置** | [Stock.cs:69](../src/Wms.Core.Domain/Entities/Material/Stock.cs#L69) |
| **依赖** | S0-PER-004（Version 并发令牌） |
| **状态** | 待处理 |

**现象**：`decimal? Quantity` 无原子操作、无 CHECK 约束、无负数校验。并发出库导致超卖。

**修复步骤**：
1. EF Configuration 增加 `.IsRowVersion()` 或 `.IsConcurrencyToken()` 配置
2. 数据库 Migration 添加 `CHECK (Quantity >= 0)` 约束
3. 库存操作改为原子 `UPDATE Stocks SET Quantity = Quantity - @delta WHERE Id = @id AND Quantity >= @delta`，根据返回行数判断成功
4. 增加 DbUpdateConcurrencyException 处理与重试

**验证方式**：
- 并发测试：100 个线程同时减库存，最终数量正确且不为负
- 数据库测试：直接 UPDATE 负值应被 CHECK 拒绝

---

#### S0-BIZ-004 · 入库完成不检查库位容量上限

| 字段 | 值 |
|------|-----|
| **严重度** | P0 致命 |
| **维度** | 业务 - 物理约束 |
| **来源** | 第四轮 BL-10 |
| **位置** | [InboundCompletionHandler.cs:135](../src/Wms.Core.Infrastructure/Handlers/TaskCompletion/InboundCompletionHandler.cs#L135) |
| **依赖** | 无 |
| **状态** | 待处理 |

**现象**：`targetLocation.UnitloadCount += 1` 不与 `InboundLimit` 比较，导致库位物理超载。

**修复步骤**：
1. 在递增前校验 `UnitloadCount < InboundLimit`
2. 超出抛 `BusinessRuleViolationException("库位容量已满")`
3. FlowEngine 捕获后回滚事务

**验证方式**：单元测试覆盖边界条件

---

#### S0-BIZ-005 · 双深位物理约束未实现

| 字段 | 值 |
|------|-----|
| **严重度** | P0 致命 |
| **维度** | 业务 - 物理约束 |
| **来源** | 第四轮 BL-11 |
| **位置** | [Location.cs:227](../src/Wms.Core.Domain/Entities/Warehouse/Location.cs#L227)（DoubleIn 字段）, 所有 LocationAllocationRule |
| **依赖** | 无 |
| **状态** | 待处理 |

**现象**：`DoubleIn` 字段存在，但所有 Handler 和 AllocationEngine 的 SQL 中没有双深位物理约束（外位有托盘时不能直接出库内位）。违反仓储业务核心安全约束。

**修复步骤**：
1. LocationAllocationRuleBase 提供抽象方法 `ValidateDoubleDeepConstraint`
2. 所有 SDRule 实现该方法，校验外位状态
3. 出库流程增加"外位阻挡检查"

**验证方式**：业务场景测试（双深位内/外位互斥）

---

#### S0-BIZ-006 · archived 标记失败导致重复处理

| 字段 | 值 |
|------|-----|
| **严重度** | P0 致命 |
| **维度** | 业务 - 幂等性 |
| **来源** | 第四轮 BL-14 |
| **位置** | [WcsTaskSyncService.cs:190-195](../src/Wms.Core.WebApi/Services/Wcs/WcsTaskSyncService.cs#L190-L195) |
| **依赖** | 无 |
| **状态** | 待处理 |

**现象**：archived 标记写入失败时，下次轮询重复执行 InboundCompletionHandler，导致 `UnitloadCount` 重复递增、Flow 记录重复创建。

**修复步骤**：
1. WcsTask 增加唯一约束 `(TaskCode, WmsState)` 防止重复状态
2. InboundCompletionHandler 内部检查 `IsAlreadyProcessed(taskCode)`
3. archived 标记与业务操作在同一事务内

**验证方式**：模拟 archived 写入失败，下次轮询不重复处理

---

#### S0-BIZ-007 · WCS 回调幂等性仅内存级

| 字段 | 值 |
|------|-----|
| **严重度** | P0 致命 |
| **维度** | 业务 - 幂等性 |
| **来源** | 第四轮 BL-13 |
| **位置** | [WcsController.cs:150-157](../src/Wms.Core.WebApi/Controllers/Api/WcsController.cs#L150-L157) |
| **依赖** | 无 |
| **状态** | 待处理 |

**现象**：使用 `ConcurrentDictionary` 做进程内 5 秒去重，多实例失效，5 秒后窗口过期可重复处理。

**修复步骤**：
1. 改为数据库唯一约束：`WcsTasks(TaskCode, WmsState)` 唯一
2. 或使用 Redis 分布式锁 + 持久化去重集合
3. 处理逻辑改为幂等：先 SELECT 检查状态，已处理则直接返回原结果

**验证方式**：相同请求重复发送 100 次，业务数据无重复

---

#### S0-BIZ-008 · BulkUpdate/BulkDelete 绕过审计

| 字段 | 值 |
|------|-----|
| **严重度** | P0 致命 |
| **维度** | 业务 - 审计 |
| **来源** | 第四轮 BL-7 |
| **位置** | [Repository.cs:238-255](../src/Wms.Core.Infrastructure/Persistence/Repositories/Repository.cs#L238-L255) |
| **依赖** | 无 |
| **状态** | 待处理 |

**现象**：`ExecuteUpdateAsync`/`ExecuteDeleteAsync` 绕过 ChangeTracker，不触发 IAuditable 自动填充，不记录操作日志。

**修复步骤**：
1. BulkUpdate/Delete 前 SELECT 待修改实体（记录旧值）
2. 操作后写入 SystemLog（包含 old/new value）
3. 或在 Repository 基类中拦截，强制走 ChangeTracker

**验证方式**：审计日志检查

---

#### S0-BIZ-009 · 删除 Material 不检查引用

| 字段 | 值 |
|------|-----|
| **严重度** | P0 致命 |
| **维度** | 业务 - 数据完整性 |
| **来源** | 第四轮 BL-5 |
| **位置** | [MaterialsController.cs:382](../src/Wms.Core.WebApi/Controllers/MaterialsController.cs#L382) |
| **依赖** | 无 |
| **状态** | 待处理 |

**现象**：直接 `_repository.Delete(id)`，不检查 UnitloadItem/Stock 引用。删除导致悬空 FK。

**修复步骤**：
1. 删除前查询引用计数：`UnitloadItems.Where(u => u.MaterialId == id).Any()`
2. 有引用抛 `ReferentialIntegrityException`
3. 或改为软删除（`IsDeleted` 字段）

**验证方式**：尝试删除被引用物料应失败

---

### 性能 P0（5 项）

#### S0-PER-001 · Repository.Add/Update/Delete 内嵌 SaveChanges

| 字段 | 值 |
|------|-----|
| **严重度** | P0 |
| **维度** | 性能 - EF Core |
| **来源** | 第四轮 P0-1 |
| **位置** | [Repository.cs:76,86,96,105,117,127](../src/Wms.Core.Infrastructure/Persistence/Repositories/Repository.cs#L76) |
| **依赖** | 无 |
| **状态** | 待处理 |

**现象**：每次 Add/Update/Delete 立即 SaveChanges，无法批量操作，无事务边界控制。

**修复步骤**：
1. 移除 Repository 内的 SaveChanges 调用
2. 调用方控制 SaveChangesAsync（Unit of Work 模式）
3. 短期：增加 `AddWithoutSave`、`UpdateWithoutSave` 重载，渐进迁移调用方

**验证方式**：批量添加 100 个实体，DB 往返 ≤ 2 次

---

#### S0-PER-002 · 循环内 N+1 查询 + 循环内 SaveChanges

| 字段 | 值 |
|------|-----|
| **严重度** | P0 |
| **维度** | 性能 - EF Core |
| **来源** | 第四轮 P0-2 |
| **位置** | [OutboundTimerService.cs:291-301, 768-776, 197-249](../src/Wms.Core.Infrastructure/Services/OutboundTimerService.cs#L291-L301) |
| **依赖** | S0-PER-001 |
| **状态** | 待处理 |

**现象**：3 处典型 N+1，循环内逐条查询 + 逐条提交。100 条记录产生 100+ 次 DB 往返。

**修复步骤**：
1. 循环内 FirstOrDefault 改为批量 `Where(id => ids.Contains(id.Id)).ToDictionary()`
2. 循环内 SaveChanges 改为事务内单次提交
3. 添加 EF Core 日志监控 N+1

**验证方式**：DB 往返次数从 N+1 降到常数

---

#### S0-PER-003 · 显式 GC.Collect()

| 字段 | 值 |
|------|-----|
| **严重度** | P0 |
| **维度** | 性能 - 内存 |
| **来源** | 第四轮 P0-3 |
| **位置** | [ReportExportService.cs:176](../src/Wms.Core.Infrastructure/Services/ReportExportService.cs#L176) |
| **依赖** | 无 |
| **状态** | 待处理 |

**现象**：每 50000 行触发完整 GC（含 LOH），影响所有并发请求。

**修复步骤**：
1. 删除 `GC.Collect()` 调用
2. 改用 ClosedXML 的 `XLWorkbook` using 包裹确保释放
3. 大数据量改用流式写入（如 OpenXML SDK 的 SAX 模式）

**验证方式**：压测期间无手动 GC 触发

---

#### S0-PER-004 · Version 字段未配置为并发令牌

| 字段 | 值 |
|------|-----|
| **严重度** | P0 |
| **维度** | 性能 - 并发 |
| **来源** | 第四轮 P0-4 |
| **位置** | 7 个实体的 Configuration 文件（Stock/Unitload/Location/Materials/InboundOrder/OutboundOrder/Laneway） |
| **依赖** | 无 |
| **状态** | 待处理 |

**现象**：7 个实体有 `int Version` 字段但未配置 `.IsRowVersion()`，并发更新直接覆盖。

**修复步骤**：
1. 对 7 个 Configuration 文件添加 `.IsRowVersion()`（byte[]）或 `.IsConcurrencyToken()`（int）
2. 全局异常处理捕获 `DbUpdateConcurrencyException`
3. UI 层提示"数据已被其他用户修改，请刷新重试"

**验证方式**：并发更新同一实体，后者抛异常

---

#### S0-PER-005 · 库位分配完全无锁

| 字段 | 值 |
|------|-----|
| **严重度** | P0 |
| **维度** | 性能 - 并发 |
| **来源** | 第四轮 P0-5 |
| **位置** | [LocationAllocator.cs:107](../src/Wms.Core.Infrastructure/Handlers/WcsRequest/LocationAllocator.cs#L107) |
| **依赖** | S0-PER-004 |
| **状态** | 待处理 |

**现象**：通过 Dapper `SELECT TOP 1` 查询可用库位，无锁无乐观并发兜底。两个并发请求可能选中同一库位。

**修复步骤**：
1. SQL 查询加 `WITH (UPDLOCK, HOLDLOCK)` 行级锁
2. 或分配后立即 UPDATE 标记 `Allocated = true`，其他请求跳过
3. 叠加乐观并发：Version 字段校验

**验证方式**：100 并发入库请求分配到不同库位

---

### 架构 P0（6 项）

#### S0-ARCH-001 · Wms.Core.Logging 项目实际不存在

| 字段 | 值 |
|------|-----|
| **严重度** | P0 |
| **维度** | 架构 - 分离方案 |
| **来源** | 第二轮 A1 |
| **位置** | 方案文档第 116 行 |
| **依赖** | 无 |
| **状态** | 待处理 |

**现象**：分离方案第二章把 `Wms.Core.Logging` 列为底层 6 个 DLL 之一，但该项目不存在。WmsLogDbContext 在 Infrastructure，InterfaceLog 在 Domain，仅有 1 个 LogMigration。

**修复步骤**：
1. 决策：放弃独立 Logging 项目（保持现状）或抽取
2. 若抽取：迁移 3 个日志实体（SystemLog/FlowNodeLog/InterfaceLog）+ 2 个 DbContext 到新项目
3. 更新分离方案文档第二章 DLL 清单

**验证方式**：方案与代码现实一致

---

#### S0-ARCH-002 · LocationAllocationEngine 已在 Domain 层

| 字段 | 值 |
|------|-----|
| **严重度** | P0 |
| **维度** | 架构 - 分离方案 |
| **来源** | 第二轮 A2 |
| **位置** | [Domain/Tasks/LocationAllocationEngine.cs](../src/Wms.Core.Domain/Tasks/LocationAllocationEngine.cs), 方案文档 Phase 3 表格 |
| **依赖** | 无 |
| **状态** | 待处理 |

**现象**：方案 Phase 3 写"LocationAllocationEngine 从 Infrastructure 迁移到 Engine"，**实际已在 Domain 层**。Rules 实现在 Infrastructure，依赖很干净（仅 Domain + Dapper）。

**修复步骤**：
1. 删除方案 Phase 3 表格中"迁移 LocationAllocationEngine"行
2. 澄清：Engine 和 ILocationAllocationRule 留 Domain，Rules 实现留 Infrastructure（项目层）
3. 修正方案第二章 DLL 清单

**验证方式**：方案描述与代码一致

---

#### S0-ARCH-003 · ctask 不是 DbContext（多 DbContext 协调论证基础错误）

| 字段 | 值 |
|------|-----|
| **严重度** | P0 |
| **维度** | 架构 - 分离方案 |
| **来源** | 第二轮 A3 |
| **位置** | 方案文档第 50 行 |
| **依赖** | 无 |
| **状态** | 待处理 |

**现象**：方案引用 ABP 多 DbContext 协调（WmsDb/WmsLogsDb/ctask），但实际只有 2 个 EF DbContext，ctask 用 Dapper 访问。

**修复步骤**：
1. 修正方案论证：IUnitOfWork 只需协调 2 个 EF Context
2. Dapper 操作不参与 UnitOfWork（独立连接）

**验证方式**：方案描述准确

---

#### S0-ARCH-004 · Application 层结构被低估

| 字段 | 值 |
|------|-----|
| **严重度** | P0 |
| **维度** | 架构 - 分离方案 |
| **来源** | 第二轮 A4 |
| **位置** | 方案文档第 113 行 |
| **依赖** | 无 |
| **状态** | 待处理 |

**现象**：方案说 Application 装"DTOs、Jobs、请求/响应模型"，实际还有 21 个 Ports 接口、12 个 WcsRequestHandler 实现、5 个 ReportProvider、2 个 Jobs。

**修复步骤**：更新方案 Application 项目描述

**验证方式**：方案描述完整

---

#### S0-ARCH-005 · 16 个 Controller 直连 DbContext

| 字段 | 值 |
|------|-----|
| **严重度** | P0 |
| **维度** | 架构 - 分层 |
| **来源** | 第二轮 A5 |
| **位置** | 16 个 Controller 文件（详见审查报告） |
| **依赖** | S0-ARCH-006 |
| **状态** | 待处理 |

**现象**：16 个 Controller 直接注入 `WmsDbContext`，绕过 Application 层。项目层开发者本来就要写 Controller + Infrastructure，**与"权限隔离"目标根本矛盾**。

**修复步骤**：
1. 重构这 16 个 Controller，改为通过 Application Service 接口
2. 短期：把直查逻辑下沉到 Infrastructure 的 Service
3. 长期：建立 Controller 不能引用 EF Core 的架构规则（Roslyn Analyzer）

**验证方式**：grep `_db.` 在 Controllers 目录应为零

---

#### S0-ARCH-006 · 与既有 WMS-改造实施计划.md Phase 5 严重重叠

| 字段 | 值 |
|------|-----|
| **严重度** | P0 |
| **维度** | 架构 - 文档协同 |
| **来源** | 第三轮 E2 |
| **位置** | [WMS-改造实施计划.md](WMS-改造实施计划.md) Phase 5 |
| **依赖** | 无 |
| **状态** | 待处理 |

**现象**：既有 Phase 5 已规划"接口迁移 18 个、Controller 去 WmsDbContext、FluentValidation"等，与本方案 Phase 0-3 严重重叠但未交叉引用。

**修复步骤**：
1. 合并两份方案为单一权威方案
2. 或明确：本方案 Phase 0 = 既有 Phase 5 完成之后才启动
3. 评审并梳理重叠项的依赖顺序

**验证方式**：两份方案无冲突，明确执行顺序

---

## 四、Phase S1 — 严重修复（38 项，1-2 周内）

### 安全 P1（9 项）

| ID | 标题 | 位置 | 状态 |
|----|------|------|------|
| S1-SEC-001 | RequireHttps=false（所有环境） | appsettings.json:42, appsettings.Production.json:22 | 待处理 |
| S1-SEC-002 | 数据库 SA 密码 `123456a` 明文 | appsettings.json:14-16 | 待处理 |
| S1-SEC-003 | HangKe 凭据 `TSGX2ZZ/ZZ@123` 明文 | appsettings.json:30-33 | 待处理 |
| S1-SEC-004 | InternalIpWhitelist + ForwardedHeaders 联合绕过 | InternalIpWhitelistAttribute.cs:46-60, Program.cs:47-49 | 待处理 |
| S1-SEC-005 | SqlValidator 黑名单遗漏 UNION/子查询 | SqlValidator.cs | 待处理 |
| S1-SEC-006 | FilterSqlMappingItem.SqlExpression 存储型注入 | DynamicSqlProvider.cs:55,81,87 | 待处理 |
| S1-SEC-007 | UploadController.ExcelExport [AllowAnonymous] | UploadController.cs:193 | 待处理 |
| S1-SEC-008 | WCS/HangKe 接口无签名认证，可重放 | WcsController, HangkeController, OutboundTimerController | 待处理 |
| S1-SEC-009 | DateTime.Now vs Utc 混用 | 全代码库 | 待处理 |

### 业务 P1（5 项）

| ID | 标题 | 位置 | 状态 |
|----|------|------|------|
| S1-BIZ-001 | TransTask 无显式状态字段，跨 DB 状态分裂 | TransTask.cs | 待处理 |
| S1-BIZ-002 | 无非法状态转移防护 | WcsTaskSyncService.cs:118 | 待处理 |
| S1-BIZ-003 | 物料导入无去重/无事务/无上限 | MaterialsController.cs:262-367 | 待处理 |
| S1-BIZ-004 | 状态变更未记录前后值 | SystemLog.cs | 待处理 |
| S1-BIZ-005 | 无多租户隔离（与方案目标矛盾） | 全局 | 待处理 |

### 性能 P1（8 项）

| ID | 标题 | 位置 | 状态 |
|----|------|------|------|
| S1-PER-001 | NLog 未配置 async + keepFileOpen=false | nlog.config | 待处理 |
| S1-PER-002 | SetMinimumLevel(Trace) 覆盖生产配置 | Program.cs:61 | 待处理 |
| S1-PER-003 | WriteIndented=true 全局启用 | Program.cs:74 | 待处理 |
| S1-PER-004 | 无响应压缩 | Program.cs | 待处理 |
| S1-PER-005 | 多级 ThenInclude 笛卡尔积炸弹 | OutboundCompletionHandler.cs:70-73 等 | 待处理 |
| S1-PER-006 | AsNoTracking 覆盖率仅 9.8% | Repository.cs:49-52 | 待处理 |
| S1-PER-007 | BackgroundTaskQueue DropWrite + 单消费者串行 | BackgroundTaskQueue.cs | 待处理 |
| S1-PER-008 | Hangfire job-http 无超时无 Polly | HangfireExtensions.cs:29-41 | 待处理 |

### 架构 P1（16 项，详细列表）

| ID | 标题 | 状态 |
|----|------|------|
| S1-ARCH-001 | 测试项目（UnitTests + IntegrationTests）归属未定 | 待处理 |
| S1-ARCH-002 | EF Core Migration 多客户分发策略缺失 | 待处理 |
| S1-ARCH-003 | 本地双仓库 F5 调试工作流未规划 | 待处理 |
| S1-ARCH-004 | DTO 与 AutoMapper/Mapperly 归属 | 待处理 |
| S1-ARCH-005 | 版本兼容矩阵与 Breaking Change 流程 | 待处理 |
| S1-ARCH-006 | IFlowDbContext 反模式（泄露式接口） | 待处理 |
| S1-ARCH-007 | 事务可见性问题未识别 | 待处理 |
| S1-ARCH-008 | Set<T>() 兜底削弱隔离意义 | 待处理 |
| S1-ARCH-009 | DI 注册重组工作量低估 | 待处理 |
| S1-ARCH-010 | TokenService/JwtBlacklistService 归属 | 待处理 |
| S1-ARCH-011 | OperationLogFilter 全局 Filter 写日志 | 待处理 |
| S1-ARCH-012 | LanguagePackMiddleware 依赖倒置 | 待处理 |
| S1-ARCH-013 | Mapperly 实际未广泛使用 | 待处理 |
| S1-ARCH-014 | Domain 层引用 BCrypt（架构泄露） | 待处理 |
| S1-ARCH-015 | Application 层引用 Excel 包（架构泄露） | 待处理 |
| S1-ARCH-016 | 跨层包版本不一致（NLog/Hangfire/Excel） | 待处理 |

---

## 五、Phase S2 — 中等优先级（67 项，1 个月内）

### 安全 P2（11 项）

| ID | 标题 |
|----|------|
| S2-SEC-001 | JwtBlacklistService fail-open |
| S2-SEC-002 | RateLimit 基于 IMemoryCache，多实例失效 |
| S2-SEC-003 | AuthService 登录失败计数器内存级，重启清零 |
| S2-SEC-004 | BCrypt work factor 默认 11 |
| S2-SEC-005 | 安全头缺失 HSTS/CSP/Referrer-Policy |
| S2-SEC-006 | UploadController module 参数路径遍历 |
| S2-SEC-007 | /uploads 认证逻辑 bug（运算符优先级） |
| S2-SEC-008 | FlowController 缺少 Admin 角色授权 |
| S2-SEC-009 | SignalR Token 通过 Query String |
| S2-SEC-010 | 密码策略过弱（6 位无复杂度） |
| S2-SEC-011 | RefreshToken 端点不验证密码 |

### 业务 P2（3 项）

| ID | 标题 |
|----|------|
| S2-BIZ-001 | TransTask WasSentToWcs / WcsState 一致性风险 |
| S2-BIZ-002 | Unitload 状态字段分散无统一状态机 |
| S2-BIZ-003 | Archive 操作 Recover 后 ID 变化导致引用断裂 |

### 性能 P2（15 项）

| ID | 标题 |
|----|------|
| S2-PER-001 | 启动 9+ 次数据库往返 |
| S2-PER-002 | IMemoryCache 无 SizeLimit |
| S2-PER-003 | 缓存无防击穿（仅 Dashboard 有 SemaphoreSlim） |
| S2-PER-004 | 缓存不写空值（穿透风险） |
| S2-PER-005 | SignalR Clients.All 广播无分组 |
| S2-PER-006 | SignalR 未启用 MessagePack |
| S2-PER-007 | Kestrel 无 MaxConcurrentConnections |
| S2-PER-008 | 连接字符串无 Max Pool Size |
| S2-PER-009 | wcs-task-sync 每 30 秒密集 |
| S2-PER-010 | 同步 SaveChanges 在异步上下文（UnitloadService 16 处） |
| S2-PER-011 | ClosedXML 全内存模式导出 OOM |
| S2-PER-012 | ExcelDataReader AsDataSet 全量加载 |
| S2-PER-013 | HangkeController SemaphoreSlim 多实例失效 |
| S2-PER-014 | WCS 调用在事务内 + 36 秒重试 |
| S2-PER-015 | OperationLogFilter/SqlValidator 正则未 Compiled |

### 架构 P2（35 项）

详见前几轮审查报告，包括：
- 工程基线（CPM、Directory.Build.props、SourceLink、EditorConfig）
- 客户定制代码识别与剥离（DefaultMesClient/HangKe 行业耦合）
- Hangfire 体系归属（JobDispatcher、BackgroundJobService）
- Docker 改造（curl 缺失、HTTPS 证书、镜像 tag）
- SignalR 事件契约类型化
- 国际化资源归属
- 报表 Provider 客户定制下沉
- 过时文档清理清单
- DLL 完整性验证

### 基础设施 P2（3 项）

| ID | 标题 |
|----|------|
| S2-INF-001 | Redis 未启用 TLS |
| S2-INF-002 | DataProtection 密钥磁盘未加密 |
| S2-INF-003 | 无 Secret 管理系统（Docker/K8s Secrets） |

---

## 六、Phase S3 — 长期治理（50 项，季度）

包含所有 P3 低优先级问题：
- 代码质量基线（Analyzer、TreatWarningsAsErrors）
- 工程现代化（SourceLink、Deterministic Build）
- 微小性能优化（CompiledQuery、MaxBatchSize）
- 文档完善（Mermaid 架构图、业务术语表）
- 命名统一（节点处理器 Handler/Node 二选一）
- 遗留代码清理（MonthlyReport、ExcelService 注释注册）

完整清单略，按需在 [附录 A](#附录-a完整-p3-清单) 补充。

---

## 七、状态管理规则

### 7.1 状态字段定义

| 状态 | 含义 | 颜色 |
|------|------|------|
| 待处理 | 未开始 | ⚪ |
| 进行中 | 已分配负责人，开发中 | 🔵 |
| 已完成 | 代码已合并 | 🟡 |
| 已验证 | 测试通过 + 代码审查通过 | 🟢 |
| 已阻塞 | 遇到阻塞，记录阻塞原因 | 🔴 |
| 已推迟 | 决策推迟到下个 Phase | ⚪ |

### 7.2 更新流程

1. 修复开始：状态改为 `进行中`，记录 `负责人`
2. 提交 PR：状态改为 `已完成`，记录 `PR 链接`
3. 验证通过：状态改为 `已验证`，记录 `验证人` + `验证时间`
4. 每周一次：同步统计仪表盘

### 7.3 任务模板

```markdown
#### S?-{DIM}-{NNN} · {标题}

| 字段 | 值 |
|------|-----|
| **严重度** | P? |
| **维度** | 安全/业务/性能/架构/基础设施 |
| **来源** | 第N轮 ID |
| **位置** | [文件:行号](路径) |
| **依赖** | 无 / S?-XXX-NNN |
| **状态** | 待处理 |
| **负责人** | - |
| **PR** | - |
| **验证人** | - |
| **验证时间** | - |

**现象**：...
**攻击场景 / 影响**：...
**修复步骤**：
1. ...
**验证方式**：...
```

---

## 八、与既有文档的关系

```
                    ┌─────────────────────────┐
                    │ WMS-综合修复实时追踪计划 │  ← 本文档（实时）
                    │   179 项 P0-P3 追踪      │
                    └────────────┬─────────────┘
                                 │
                ┌────────────────┼─────────────────┐
                │                │                 │
   ┌────────────▼─────────┐  ┌──▼────────────┐  ┌─▼──────────────────┐
   │ WMS-改造实施计划.md  │  │ 项目分离方案  │  │ SECURITY-REMEDIATION│
   │  既有 Phase 1-5      │  │  Phase 0-4    │  │  安全加固专项       │
   │  架构重构            │  │  DLL 封装     │  │                     │
   └──────────────────────┘  └───────────────┘  └─────────────────────┘
```

**职责划分**：
- **本文档**：所有致命/严重问题的实时修复追踪（横跨架构/性能/安全/业务）
- **WMS-改造实施计划.md**：架构重构的 5 Phase 详细执行
- **项目分离方案.md**：DLL 物理分离的设计文档
- **SECURITY-REMEDIATION-PLAN.md**：安全漏洞专项（与本文档安全部分重叠，以本文档为准）

**冲突处理**：若文档间存在冲突，以**本文档**为准（基于最新代码核查）。

---

## 九、执行建议

### 9.1 推荐执行顺序

```
Week 1：Phase S0 安全类（4 项）
  - S0-SEC-001 JWT SecretKey
  - S0-SEC-002 默认账号
  - S0-SEC-003 Admin 角色恢复
  - S0-SEC-004 SQL 注入

Week 2：Phase S0 业务类（9 项）
  - S0-BIZ-001 OutboundTimerController（最紧急）
  - S0-BIZ-002 IP 白名单
  - S0-BIZ-003~009 业务约束与幂等性

Week 3-4：Phase S0 性能 + 架构类（11 项）
  - S0-PER-001~005
  - S0-ARCH-001~006

Week 5-8：Phase S1（38 项）

Week 9-12：Phase S2（67 项）

季度后：Phase S3 + 启动项目分离
```

### 9.2 团队分工建议

| 角色 | 负责范围 |
|------|---------|
| 安全工程师 | S0/S1-SEC 全部 + 渗透测试 |
| 后端 Lead | S0/S1-BIZ + S0/S1-PER |
| 架构师 | S0/S1-ARCH + 文档协同 |
| DBA | Migration + 数据完整性 + 性能调优 |
| QA | 验证 + 自动化测试覆盖 |

### 9.3 阻塞决策点

修复过程中需做出的关键决策：

1. **BL-12 多租户策略**：独立部署 vs 共享实例（影响 Phase S2 启动）
2. **S0-ARCH-001 Logging 项目**：保留现状 vs 抽取（影响分离方案）
3. **S1-ARCH-006 IFlowDbContext 设计**：Repository 模式 vs DbSet 清单
4. **S1-SEC-008 WCS 签名**：HMAC vs mTLS vs OAuth2 Client Credentials

---

## 十、关键指标（KPI）

每月统计：

| 指标 | 目标 | 当前 |
|------|------|------|
| P0 致命问题修复率 | 100% | 0% |
| P1 严重问题修复率 | ≥ 90% | 0% |
| 单元测试覆盖率 | ≥ 60% | < 5% |
| 渗透测试通过率 | 100% | 未测 |
| 安全漏洞复发率 | ≤ 5% | - |
| 平均修复时长（P0） | ≤ 7 天 | - |
| 系统可用性 | ≥ 99.5% | - |

---

## 附录 A：完整 P3 清单

> 季度内补充完善，按需展开。

P3 项包括（约 50 项）：
- 工程基线（.editorconfig、global.json、nuget.config）
- 代码规范（节点处理器命名、控制器标准化）
- 文档（README 更新、API_DOCUMENTATION.md 清理默认密码）
- 性能微优化（CompiledQuery、MaxBatchSize、JSON 预编译选项）
- 部署（Docker 镜像签名、HealthCheck 信息脱敏）
- 监控（APM 集成、慢查询监控、日志告警）

---

## 附录 B：审查历史与发现索引

### 第一轮：架构方案审查（17 项）
聚焦：WMS底层封装与平台-项目分离方案.md 的事实核查、设计反模式、文档矛盾

### 第二轮：深度架构审查（33 项）
聚焦：Logging 项目不存在、LocationAllocationEngine 已在 Domain、ctask 不是 DbContext、Controller 直连 DbContext、工程基线、包治理、客户定制耦合

### 第三轮：补充深度审查（28 项）
聚焦：文档体系混乱、Hangfire 归属、SignalR 契约、Dockerfile curl、nlog.config 幽灵类、报表行业耦合

### 第四轮：最终覆盖（11 项）
聚焦：当前目录非 git、前端 Wms.Vue 独立、API 契约稳定性、代码度量、Analyzer 空白、sync-over-async

### 第五轮：性能审查（28 项）
聚焦：EF Core 反模式、并发与锁、内存与 GC、启动性能、JSON 序列化、SignalR、Kestrel、日志性能

### 第六轮：安全 + 业务 + 基础设施审查（62 项）
聚焦：JWT、认证绕过、SQL 注入、业务逻辑漏洞（17 项）、基础设施加密（13 项）、反序列化（已确认安全）

**累计**：179 项发现

---

## 附录 C：参考资料

### 业界参考
- OWASP Top 10 (2021): https://owasp.org/Top10/
- OWASP SQL Injection Prevention Cheat Sheet
- OWASP XXE Prevention Cheat Sheet
- OWASP Authentication Cheat Sheet
- Microsoft ASP.NET Core Security Best Practices
- EF Core Performance Best Practices
- NLog Performance Optimization

### 内部文档
- [WMS-改造实施计划.md](WMS-改造实施计划.md)
- [WMS-改造计划审查报告.md](WMS-改造计划审查报告.md)
- [SECURITY-REMEDIATION-PLAN.md](SECURITY-REMEDIATION-PLAN.md)
- [ARCHITECTURE-CHANGES.md](ARCHITECTURE-CHANGES.md)
- [CREDENTIAL-ROTATION-RUNBOOK.md](CREDENTIAL-ROTATION-RUNBOOK.md)
- [OWASP-TOP10-MAPPING.md](OWASP-TOP10-MAPPING.md)
- [REMEDIATION-VERIFICATION-CHECKLIST.md](REMEDIATION-VERIFICATION-CHECKLIST.md)
- [CONFIGURATION-GUIDE.md](CONFIGURATION-GUIDE.md)
- [WMS底层封装与平台-项目分离方案.md](Wms底层封装与平台-项目分离方案.md)

---

## 更新日志

| 日期 | 操作 | 负责人 | 说明 |
|------|------|--------|------|
| 2026-07-09 | 文档创建 | Claude Code | 基于 6 轮深度审查，初始 179 项发现入库 |

<!-- 后续更新追加于此 -->

---

**文档维护说明**：
- 每周一定期同步仪表盘
- 每项状态变更需追加更新日志
- 季度评审是否需要新增发现
- 年度归档已完成且验证超 6 个月的项
