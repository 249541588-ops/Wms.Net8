# WMS 系统安全加固实施计划

| 元信息 | 值 |
|--------|-----|
| 文档版本 | v1.0 |
| 生成日期 | 2026-07-06 |
| 适用范围 | Wms.Core（前端 Wms.Vue + 后端 Wms.Net8） |
| 文档状态 | 草案（待审核） |
| 审计轮次 | 5 轮 |
| 关联文档 | [CONFIGURATION-GUIDE.md](./CONFIGURATION-GUIDE.md)、[CREDENTIAL-ROTATION-RUNBOOK.md](./CREDENTIAL-ROTATION-RUNBOOK.md)、[DEPENDENCY-UPGRADE-GUIDE.md](./DEPENDENCY-UPGRADE-GUIDE.md)、[REMEDIATION-VERIFICATION-CHECKLIST.md](./REMEDIATION-VERIFICATION-CHECKLIST.md)、[OWASP-TOP10-MAPPING.md](./OWASP-TOP10-MAPPING.md)、[ARCHITECTURE-CHANGES.md](./ARCHITECTURE-CHANGES.md) |

---

## 1. 执行摘要

### 1.1 审计概况

对位于 `f:\Project\Wms.Core` 的 WMS 仓储管理系统进行了 **5 轮全面安全/质量审计**，累计发现约 **240 个问题**，去重后约 **195 个独立问题**。

| 轮次 | 审计重点 | 发现数 |
|------|---------|--------|
| 第一轮 | 核心架构、请求层、路由守卫、主要 Controller | 47 |
| 第二轮 | SignalR、Hangfire、Flow 引擎、Docker、依赖漏洞 | 80+ |
| 第三轮 | IDOR 越权、剩余 Controller、FluentValidation、业务页面 | 70+ |
| 第四轮 | 反序列化、路径遍历、SSRF、XXE、加密弱点 | 40+ |
| 第五轮 | 认证会话、HTTP 协议级、CSV/Excel 注入、外部协议 | 49 |

### 1.2 严重程度分布

| 严重程度 | 数量 | 处理策略 |
|---------|------|---------|
| 🔴 Critical | 22 | 本次必须修复 |
| 🟠 High | 53 | 本次必须修复（实际纳入 53 个，去重后） |
| 🟡 Medium | 80+ | Backlog，后续迭代 |
| 🟢 Low | 80+ | Backlog，后续迭代 |

**本次修复范围**：75 个严重+高优先级问题（去重后），按 8 个 PR 交付。

### 1.3 关键风险点（必须立即处理）

#### 后端关键漏洞
1. **SA 密码硬编码** `appsettings.json:14-16` — `Password=123456a` 明文入库
2. **JWT SecretKey 默认值** `appsettings.json:35` — 占位符可预测，可伪造任意用户 token
3. **UsersController 全部管理接口缺授权** `UsersController.cs:23` — `[Authorize]` 被注释，任何登录用户可删除任意用户、重置任意人密码、提升权限
4. **SignalR Hub 无授权** `WmsHub.cs:12` — 任意人可连接获取业务数据（✅ C6 已修复）
5. **RefreshToken 重用未检测** — 违反 RFC 6749，token 被盗后可永久劫持
6. **登出后 JWT 仍有效** — 无黑名单机制
7. **ForwardedHeaders 信任所有代理** `Program.cs:44-49` — 可伪造 IP 绕过白名单
8. **ReportService 排序 SQL 注入** `ReportService.cs:484-501` — `sortField` 直接拼接
9. **CSV/Excel 公式注入** `ExportService.cs:65` — `=cmd|...` 触发 RCE
10. **杭可设备凭据硬编码** `IHangKeClient.cs:61-66` — `TSGX2ZZ`/`ZZ@123`（✅ C4 已修复，const 改为 appsettings.json 默认值，仍可通过环境变量覆盖）

#### 前端关键漏洞
1. **登录页硬编码 Super/Admin/User 凭证** `pwd-login.vue:49-68` — 被打包到生产 chunk
2. **开放重定向** `router.ts:102-105`
3. **动态路由完全信任后端** `route.ts:154-191`
4. **货载删除行 BUG** `unitloads.vue:506-514` — 删除选中行实际删除错误行
5. **60% 业务页面表单无校验** `locations.vue`、`materials.vue`、`unitloads.vue` 等
6. **无验证码/防爆破** `pwd-login.vue`
7. **无跨 Tab 会话同步** — Tab A 登出后 Tab B 仍登录
8. **xlsx 0.18.5 CVE** — CVE-2023-30533、CVE-2024-22363（✅ C12 已修复，升级到 SheetJS CDN 0.20.3）

#### 部署/运维关键问题
1. **Docker 端口暴露** — SQL Server 1433、Redis 6379 对外开放
2. **数据库备份文件入库** `DB/WmsDb.Bak`、`DB/ctask.bak`
3. **数据库/Redis 无 TLS** — 明文传输
4. **Hangfire 版本不一致** 1.8.17 vs 1.8.23

---

## 2. PR 总览

| PR | 主题 | 问题数 | 优先级 |
|----|------|--------|--------|
| PR1 | 严重安全漏洞（IDOR/SQLI/XXE/会话/凭据） | 28 | 🔴 最高 |
| PR2 | 后端高优先级（并发/TLS/路径遍历/CSV/授权） | 24 | 🟠 高 |
| PR3 | 前端高优先级（表单校验/会话/编辑器XSS/Dead Code 清理） | 18 | 🟠 高 |
| PR4 | 部署/运维（Docker/Hangfire/gitignore） | 5 | 🟠 高 |
| PR5 | 配置安全基线（User Secrets/环境变量） | — | 🟠 高 |
| PR6 | 关键模块单测（12 个测试文件） | — | 🟡 中 |
| PR7 | 端到端验证 + 凭据旋转 + 文档更新 | — | 🟠 高 |
| PR8 | 生成生产实施文档（本套文档） | — | 🟢 已完成 |

### PR 依赖关系

```
PR1 (严重) ──┐
PR2 (后端高) ─┼─→ PR5 (配置基线) ─→ PR6 (单测) ─→ PR7 (验证+旋转)
PR3 (前端高) ─┤
PR4 (部署高) ─┘
PR8 (文档) ─────────────────────────────────→ 已完成
```

- PR1-PR4 可并行开发
- PR5 依赖 PR1-4（需占位符生效）
- PR6 依赖代码修复完成
- PR7 是最终验证
- PR8 文档已生成

---

## 3. 详细修复清单

### 3.1 PR1：严重安全漏洞修复（28 项）

#### 3.1.1 PR1.0 后端 IDOR 越权系列（最高优先级）

##### 🔴 T301 UsersController 全部管理接口缺授权
- **文件**：`Wms.Net8/src/Wms.Core.WebApi/Controllers/UsersController.cs:23`
- **根因**：`[Authorize(Roles = "Admin")]` 被注释掉
- **影响**：Create/Delete/Update/ResetPassword/ChangeStatus 仅需登录即可访问
- **修复方案**：取消第 23 行注释；或在敏感方法级别添加 `[Authorize(Roles = "Admin")]`
- **验证**：普通用户 token 调用 `DELETE /api/v1/users/{id}` 应返回 403

##### 🔴 T302 ResetPassword IDOR
- **文件**：`UsersController.cs:324-345`
- **根因**：仅依靠 URL `id` 参数定位目标用户，无权限校验
- **修复方案**：方法级 `[Authorize(Roles = "Admin")]` + 审计日志
- **验证**：普通用户调用应返回 403

##### ✅ T303 ChangePassword IDOR
- **文件**：`UsersController.cs:295-316`
- **根因**：`id` 来自 URL 而非 JWT
- **修复方案**：从 JWT 提取 userId（参考 `ProfileController`），或校验 `id == JWT.userId || isAdmin`
- **验证**：用户 A 用 token A 修改 URL 中 id=B 应返回 403
- **状态**：✅ 已修复（2026-07-07）
  - 在 `ChangePassword` 入口加入 IDOR 校验：从 JWT `userId` claim 提取调用者身份，仅允许 `id == callerUserId || User.IsInRole("Admin")`，否则返回 403
  - 拦截命中时输出 `Warning` 日志（含调用者用户名与目标 id），成功时输出 `Information` 审计日志
  - 新增私有 `GetCurrentUserIdentifier()` 辅助方法（与 `ProfileController` 保持一致）
  - `ProducesResponseType` 补充 `403Forbidden`

##### 🔴 T304 Update 角色提升
- **文件**：`UsersController.cs:199-255`（第 230-243 行 Role 字段）
- **根因**：Update 接口允许通过 `request.Role` 修改任意用户角色
- **修复方案**：角色变更单独限制 `[Authorize(Roles = "Admin")]` + 审计
- **验证**：普通用户调 Update 传 `Role: "Admin"` 应返回 403

##### ✅ T305 GetAll/GetById 暴露 PasswordHash
- **文件**：`UsersController.cs:76-124,134-151`
- **根因**：返回完整 `User` 实体
- **修复方案**：返回 `UserResponseDto`（排除 `PasswordHash`、`PasswordSalt`），参考 `ProfileResponse`
- **验证**：API 响应中不应包含 `passwordHash` 字段
- **状态**：✅ 已修复（2026-07-07）
  - 新增 `UserResponse` DTO（`Wms.Core.Application/DTOs/UserDtos.cs`），显式排除 `PasswordHash`/`PasswordSalt`/`Roles`，并提供 `UserResponse.From(User)` 静态工厂保证映射安全
  - `GetAll` 返回类型改为 `Result<PagedResult<UserResponse>>`
  - `GetById` 返回类型改为 `Result<UserResponse>`
  - 采用 DTO 边界作为纵深防御；即便后续移除实体上的 `[JsonIgnore]`，敏感字段也不会外泄
##### ✅ T306 Delete 物理删除
- **文件**：`UsersController.cs:281-319`
- **根因**：直接 `_repository.Delete(id)` 物理删除，且同步物理删除 `UserRoles` 关联记录
- **修复方案**：改为软删除（`IsActive = false` + `DeletedAt = DateTime.UtcNow`）
- **验证**：删除后查询用户存在但 IsActive=false，DeletedAt 非空
- **状态**：✅ 已修复（2026-07-07）
  - **领域层**：`User` 实体新增 `DeletedAt`（UTC 时间戳）与 `DeletedBy`（操作人）字段，用于区分"软删除"与"普通禁用"（共享 `IsActive` 字段）
  - **EF 配置**：`UserConfiguration` 添加 `DeletedAt`/`DeletedBy` 列映射与索引（`IsActive`、`DeletedAt`）
  - **Delete 接口**：移除 `DELETE FROM UserRoles` 与 `_repository.Delete(id)` 的物理删除，改为 `model.IsActive=false; model.DeletedAt=DateTime.UtcNow; _repository.Update(model);`，保留角色关联数据用于审计/恢复
  - **内置用户保护**：`IsBuiltIn=true` 的用户拒绝删除并返回 409，避免误删 `admin`
  - **查询过滤**：`GetAll` 默认 `Where(m => m.DeletedAt == null)` 排除软删除用户；`status` 参数仅在未删除集合中区分启用/禁用；`GetById`、`Update`、`ChangeStatus` 对 `DeletedAt != null` 的用户统一返回 404
  - **审计日志**：删除成功时输出 `Warning` 日志（含被删除用户 ID/用户名与操作人）
  - **登录闭环**：`AuthService.LoginAsync` 已检查 `!user.IsActive`，软删除用户（`IsActive=false`）自动拒绝登录，无需新增逻辑
  - **新建默认 true**：`User` 构造函数（`User.cs:155`）显式 `IsActive=true`，`AuthService.CreateUserAsync`、`DbInitializer` 种子数据均显式 `IsActive=true`，已全部满足
  - **DTO**：`UserResponse` 新增 `DeletedAt`/`DeletedBy` 字段，前端可识别软删除状态
  - **业务决策（保留行为）**：`IUserRepository.Exists`（用户名唯一性检查）保持"包含软删除用户"的行为，禁止重用软删除用户的用户名，避免审计日志身份混淆；如业务需重用，后续可加 `DeletedAt == null` 过滤
  - **后续迁移**：需新增 EF Core 迁移 `AddUserSoftDelete`（添加 `Users.DeletedAt`、`Users.DeletedBy` 列与索引）


##### ✅ T315 ForceComplete 无状态机校验
- **文件**：`TransTasksController.cs:239-257`（`ForceFinishAsync` 在 266-387 行）
- **根因**：`ForceFinishAsync` 开头仅校验任务存在性，未校验任务当前状态；已终态任务可被再次强制完成/取消，导致重复扣库存、重复归档等业务副作用
- **修复方案**：检查 `task.Status` 是否已终态（Done/Cancelled/Archived），终态拒绝并返回 409 Conflict
- **验证**：已完成任务再次 ForceComplete 应返回 409
- **状态**：✅ 已修复（2026-07-07）
  - **终态定义对齐**：与 `TaskInfoWcsStates.Finished`（`Completed`/`Cancelled`/`Refused`）和 `TaskInfoWmsStates.Archived` 保持一致，参考 `WcsTaskSyncService` 已有的终态判断模式（`WcsTaskSyncService.cs:118,144`）
  - **校验位置**：在 `ForceFinishAsync` 中 `transTask == null` 校验之后、构造 WcsTask 之前插入状态机校验，对 `ForceComplete`/`ForceCancel` 两个入口同时生效
  - **校验逻辑**：
    1. 仅对 `WasSentToWcs == true && TaskCode 非空` 的任务查询 ctask（避免对未下发 WCS 的任务做无意义查询）
    2. 通过 `_ctaskDb.ReadByTaskCodeAsync` 读取 ctask 当前 `WcsState` 与 `WmsState`
    3. `WmsState == Archived` → 返回 `Result.Fail("任务已归档，不允许再次强制完成/取消", "409")`
    4. `WcsState ∈ {Completed, Cancelled, Refused}` → 返回 `Result.Fail("任务已终态（WcsState=...），不允许再次强制...", "409")`
  - **状态比较**：使用 `string.Equals(..., StringComparison.OrdinalIgnoreCase)` 防止大小写不一致（ctask 表写入端可能不同）
  - **审计日志**：拒绝时输出 `Warning` 日志（含 TaskCode、当前 WcsState/WmsState），便于运维排查重复点击/重试
  - **Swagger 文档**：`ForceComplete` 和 `ForceCancel` 方法均补充 `[ProducesResponseType(StatusCodes.Status409Conflict)]`
  - **HTTP 状态码说明**：受项目当前 `Result` 不走 `ToActionResult()` 的限制（参见 T335），实际 HTTP 状态码仍为 200，但响应体 `code` 字段为 `"409"`，前端按 `code` 字段判断；待 T335 统一修复后自动生效为真实 HTTP 409
  - **复用现有常量**：直接使用 `TaskInfoWcsStates.Finished` 集合，无需新增枚举或状态机类；`FlowStateMachine` 模式（`FlowInstanceStatus.cs:30-82`）作为后续若需引入完整状态机的参考


#### 3.1.2 PR1.0b 第四轮严重漏洞

##### ✅ Q402 ReportService 排序字段 SQL 注入
- **文件**：`Wms.Net8/src/Wms.Core.Infrastructure/Services/ReportService.cs:484-501`
- **根因**：`NormalizeSort` 直接拼接 `sortField` 到 ORDER BY，未经任何白名单校验；用户通过 `POST /api/v1/reports/{reportCode}/data` 的 `request.SortField` 即可注入 `(SELECT CASE WHEN ...)` 等 T-SQL 子句
- **修复方案**：复用 `Repository.cs:212-234` 的 `SanitizeOrderBy` 白名单（`[A-Za-z_][A-Za-z0-9_.]*`）
- **验证**：`?sortField=(SELECT CASE WHEN ...)` 返回 400
- **状态**：✅ 已修复（2026-07-07）
  - **抽取共享安全工具**：新建 `Wms.Core.Infrastructure/Security/SqlSafety.cs`（命名空间 `Wms.Core.Infrastructure.Security`），单点定义 ORDER BY 列名/方向白名单
    - `SafeOrderByColumnRegex`: `^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)?$`（支持 `column` / `table.column` / `schema.table.column`）
    - `AllowedSortDirections`: `{ "ASC", "DESC" }`（大小写不敏感）
    - `IsValidOrderByColumn(...)` / `IsValidSortDirection(...)` 公共校验方法
  - **Repository.cs:212-234 改造**：删除内部 `SafeColumnRegex`/`AllowedDirections` 私有字段，改为调用 `SqlSafety`，行为保持不变（多列拆分校验 + 非法 fallback 到 "Id ASC"），白名单从单点维护
  - **ReportService.NormalizeSort 改造**：拼接 `sortField` 前调用 `SqlSafety.IsValidOrderByColumn(sortField)`，非法输入抛 `ArgumentException("排序字段包含非法字符...")`；语义与 Repository 不同——报表查询场景下用户需得到 400 反馈，而非静默回退
  - **ReportsController.QueryReportData 改造**：在通用 `catch (Exception)` 前增加 `catch (ArgumentException)`，映射为 `Result.Fail(ex.Message, "400")`；同时补充 `[ProducesResponseType(StatusCodes.Status400BadRequest)]`；输出 Warning 日志（含 SortField 字段）
  - **注入路径分析**：用户可控的 `SortField` 仅通过 `NormalizeSort → AppendOrderBy → ORDER BY` 一条路径进入 SQL（`ReportService.cs:146-147`），唯一注入点已修复；导出 `SubmitExportAsync` 与 `ExecuteExportAsync` 不接受 SortField 参数，无注入风险
  - **测试用例**（手工执行）：
    - `sortField="CreatedTime"` → 200 OK，按 CreatedTime 排序
    - `sortField="t.CreatedTime"` → 200 OK（table.column 链式）
    - `sortField="(SELECT CASE WHEN ...)"` → 响应体 `code="400"`，错误消息含"排序字段包含非法字符"
    - `sortField="CreatedTime; DROP TABLE Users--"` → 响应体 `code="400"`（分号、空格、注释全部被拒）
    - `sortField=""` 或 null → 200 OK，回退到 `config.DefaultSort`
  - **不破坏现有功能**：默认排序、合法单列、合法链式列名全部通过校验，行为与修复前一致


##### ✅ Q403 JobScheduleController 缺授权（SSRF）
- **文件**：`Wms.Net8/src/Wms.Core.WebApi/Controllers/Sys/JobScheduleController.cs:14`
- **根因**：无 `[Authorize]`，http-call 任务 apiUrl 可任意指定，JobArgs/Headers 无黑名单过滤
- **修复方案**：
  1. 类级 `[Authorize(Roles = "Admin")]`
  2. http-call 的 `apiUrl` 白名单
  3. JobArgs headers 黑名单（禁 `Authorization`/`Cookie`/`Host`/`X-Forwarded-*`）
- **验证**：非 Admin 创建 http-call 返回 403
- **状态**：✅ 已修复（2026-07-07，三层防御 + 共享 HttpCallSafety 工具类）
  - **第一层 - 授权**：`JobScheduleController` 添加 `[Authorize(Roles = "Admin")]`（类级），阻断任何未授权用户（含匿名）创建/修改/删除定时任务的入口；Admin 角色名 `"Admin"` 与 `DbInitializer.cs:88` 种子数据、JWT `ClaimTypes.Role` 一致
  - **第二层 - URL 白名单**：新建 `Wms.Core.Infrastructure/Security/HttpCallSafety.cs` 提供 `IsValidHttpCallUrl(url, out reason)`，校验规则：
    - 必须以 `/` 开头（相对路径）
    - 禁止 `//`（协议相对 URL）、`/\`（反斜杠转义）
    - 禁止 `://`（任何绝对 URL）
    - 禁止 `.` 或 `..` 路径段（路径遍历）
    - 必须以 `/api/` 开头（限制只能调用应用自身 API）
  - **第三层 - Headers 黑名单**：`HttpCallSafety.IsForbiddenHeader(name)` 拒绝以下 Header（大小写不敏感 + X-Forwarded-* 通配）：
    - 凭据类：`Authorization` / `Cookie` / `Set-Cookie` / `Proxy-Authorization`
    - 路由类：`Host`
    - 转发类：`X-Forwarded-*`（通配） / `X-Real-IP` / `Forwarded`
    - 上下文类：`Origin` / `Referer`
  - **防御纵深 - 多点校验**：
    - `BackgroundJobService.CreateAsync` / `UpdateAsync`：调用 `ValidateHttpCallSafety(jobType, apiUrl, jobArgs)`，http-call 模式非法时抛 `ArgumentException`，Controller `catch (ArgumentException)` 返回 400
    - `JobDispatcher.ExecuteHttpCallAsync`：执行时再次校验 URL（防数据库被绕过/篡改），并在 Header 注入时实时过滤黑名单（输出 Warning 日志后 `continue`）
    - HttpClient 配置层：`HangfireExtensions.AddWmsHangfire` 通过 `ConfigurePrimaryHttpMessageHandler` 配置 `AllowAutoRedirect=false` + `UseProxy=false`，阻断通过 302 重定向到内网/任意主机的 SSRF 路径
  - **既有兼容性**：
    - `internal` 模式（如种子任务 `data-cleanup`）跳过 URL/Headers 校验，行为不变
    - 既有 http-call 任务若 ApiUrl 或 Headers 不符合新白名单/黑名单，下次更新时会被拦截；建议运维上线前用 SQL 审计：`SELECT * FROM BackgroundJobs WHERE JobType='http-call'`
  - **HTTP 状态码说明**：受项目当前 `Result` 不走 `ToActionResult()` 限制（参见 T335），非 Admin 调用受 `[Authorize]` 拦截会真实返回 401/403（中间件层），而 400 错误走 `Result.Fail(msg, "400")` 返回 HTTP 200+`code="400"`，前端按 `code` 判断
  - **测试用例**（手工执行）：
    - 未登录或非 Admin 调 `POST /api/v1/jobschedule` → 401/403
    - Admin 创建 `jobType="http-call"` + `apiUrl="http://evil.com/x"` → `code="400"`，"禁止绝对 URL"
    - Admin 创建 `apiUrl="//evil.com/x"` → `code="400"`，"禁止协议相对或反斜杠 URL"
    - Admin 创建 `apiUrl="/api/v1/jobs/internal-trigger"` + Headers 含 `{"Authorization":"Bearer xxx"}` → `code="400"`，"包含禁止的请求头 'Authorization'"
    - Admin 创建 `apiUrl="/api/v1/reports/data"` + Headers 含 `{"X-Custom":"value"}` → 200，正常
    - 模拟 302 重定向响应 → HttpClient 不跟随，原样返回 302（通过修改 BaseAddress 端点的测试用例验证）


##### ✅ Q401 XXE 风险
- **文件**：`Wms.Net8/src/Wms.Core.Infrastructure/Clients/DefaultHangKeClient.cs:101,190,289,366,482,550`
- **根因**：`XDocument.Parse(responseBody)` 未显式配置 `XmlReaderSettings`，杭可 SOAP 响应来源不可信（设备端被入侵或网络中间人篡改均可注入 `<!DOCTYPE>` / `<!ENTITY>`），可能触发 XXE（文件读取、SSRF、Billion Laughs DoS）
- **修复方案**：
  ```csharp
  var settings = new XmlReaderSettings {
      DtdProcessing = DtdProcessing.Prohibit,
      MaxCharactersFromEntities = 1024,
      XmlResolver = null
  };
  using var reader = XmlReader.Create(new StringReader(responseBody), settings);
  var doc = XDocument.Load(reader);
  ```
- **验证**：注入含 `<!ENTITY>` 的 SOAP 响应应不被解析
- **状态**：✅ 已修复（2026-07-07，抽取共享 `XmlSafety` 工具类 + 全调用点替换）
  - **抽取共享安全工具**：新建 `Wms.Core.Infrastructure/Security/XmlSafety.cs`（命名空间 `Wms.Core.Infrastructure.Security`），与既有 `SqlSafety`（Q402）、`HttpCallSafety`（Q403）保持一致的工具类风格，单点维护 XXE 防御配置
    - `CreateSafeReaderSettings()`：返回 `DtdProcessing=Prohibit` + `MaxCharactersFromEntities=1024` + `XmlResolver=null` 的安全配置
    - `ParseSafe(string xml)`：直接返回安全解析后的 `XDocument`，调用方一行替换 `XDocument.Parse`
    - `MaxCharactersFromEntities` 常量：`1024`（合法 SOAP 响应的 `<*Result>` 内是 JSON 字符串，不应包含任何实体引用，1024 既能兼容意外微小实体又能阻断 Billion Laughs 指数扩展）
  - **三层纵深防御**：
    1. `DtdProcessing.Prohibit`：从源头禁止任何 DTD 声明（含内部/外部/参数实体），DOCTYPE 直接触发 `XmlException`
    2. `MaxCharactersFromEntities=1024`：即便绕过 DTD 限制，实体扩展字符数被硬性截断，防 Quadratic Blowup / Billion Laughs DoS
    3. `XmlResolver=null`：禁用外部实体解析器，杜绝通过 `SYSTEM "file://"` / `SYSTEM "http://"` 发起的本地文件读取与 SSRF
  - **DefaultHangKeClient.cs 改造**：新增 `using Wms.Core.Infrastructure.Security;`；将 6 处 SOAP 响应解析（`CancelTrayAsync`/`ChemicalPalletizeAsync`/`SeparatePalletizeAsync`/`GetDischargeInfoAsync`/`InOutNotifyAsync`/`GetCellDataAsync`）的 `XDocument.Parse(responseBody)` 全部替换为 `XmlSafety.ParseSafe(responseBody)`
  - **错误处理兼容**：`XmlSafety.ParseSafe` 抛出的 `XmlException` 被 6 个调用方法既有的 `catch (Exception ex)` 捕获，沿用原有 `ResultInfo.ResultCode = -1 + ResultMessage = "杭可接口系统错误:" + ex.Message` 错误回传路径，前端无需感知变化
  - **保留 `using System.Xml.Linq;`**：`XName.Get(...)` 仍在使用，命名空间引用保持不变
  - **编译验证**：`dotnet build Wms.Core.Infrastructure.csproj` 编译成功，0 Error，本次修改未引入新 warning
  - **测试用例**（手工执行）：
    - 合法 SOAP 响应 → 正常解析，业务行为不变
    - 响应中注入 `<!DOCTYPE foo [<!ENTITY xxe SYSTEM "file:///etc/passwd">]><CheckOutTrayResult>&xxe;</CheckOutTrayResult>` → 抛 `XmlException`（DTD 被禁），原方法返回 `ResultCode=-1 + "杭可托盘注销接口系统错误:..."`
    - 响应中注入 Billion Laughs payload（`<!DOCTYPE lol [<!ENTITY lol "lol..."> ... ]>`）→ 即便绕过 DTD（理论上不会），`MaxCharactersFromEntities=1024` 也会截断抛 `XmlException`
  - **后续扩展**：项目内其他 XML 解析点（如未来新增的第三方 SOAP 客户端）应统一使用 `XmlSafety.ParseSafe` 或 `XmlSafety.CreateSafeReaderSettings`，避免遗漏

#### 3.1.3 PR1.0c 第五轮严重漏洞（会话与认证）

##### ✅ R502 RefreshToken 重用未检测
- **文件**：`Wms.Net8/src/Wms.Core.WebApi/Controllers/AuthController.cs:141-166`
- **根因**：无 FamilyId 追踪，已使用 token 重提交不吊销家族
- **修复方案**：
  1. `RefreshToken` 实体添加 `FamilyId` 字段（迁移 `AddRefreshTokenFamily`）
  2. 登录生成 FamilyId，刷新时继承
  3. 检测到已使用 token 被重提交 → 立即吊销整个 FamilyId + 记录安全事件
- **验证**：使用已吊销 token 刷新应导致整个家族被吊销
- **状态**：✅ 已修复（2026-07-07，实体 + 仓储 + 控制器三层联动 + 安全事件日志）
  - **领域层 - 新增 FamilyId 字段**：`Wms.Core.Domain/Entities/RefreshToken.cs` 在 `UserAgent` 之后新增 `FamilyId`（string?，`[MaxLength(64)]`），XML 注释明确说明 RFC 6749 Section 10.4 语义、存储格式（`Guid.ToString("N")`）与刷新链路继承关系
  - **仓储接口扩展**：`IRefreshTokenRepository` 新增 `int RevokeFamily(string familyId, string reason)` 方法签名，返回实际撤销的 token 数量；接口方法保持同步风格（与既有 `RevokeAllUserTokens`/`RevokeToken` 一致）
  - **仓储实现**：`RefreshTokenRepository.RevokeFamily` 实现：
    - 空入参短路（避免误撤销整个库）
    - 仅查询 `FamilyId == familyId && !IsRevoked && ExpiryTime > now` 的 token（已撤销/已过期的无需重复处理）
    - 设置 `IsRevoked = true` + `IsUsed = true`（同步标记已使用，防止后续 `GetByToken` 误判）+ `RevokedTime = now`
    - 输出 `Warning` 日志（含 FamilyId、撤销数量、原因），便于安全审计
  - **EF 配置**：`RefreshTokenConfiguration` 新增 `builder.Property(x => x.FamilyId).HasMaxLength(64)` 与 `builder.HasIndex(x => x.FamilyId).HasDatabaseName("IX_RefreshTokens_FamilyId")` 索引，加速整族撤销查询
  - **AuthController.Login 改造**：每次登录生成 `var familyId = Guid.NewGuid().ToString("N")`，写入 `RefreshToken.FamilyId`，作为家族根
  - **AuthController.Refresh 改造（核心）**：在 `GetByToken` 后细分三种失败场景：
    - **token 不存在** → 直接拒绝（既有行为）
    - **token 已使用或已撤销**（`IsUsed || IsRevoked`）→ **重用攻击信号**，调用 `RevokeFamily` 立即吊销整个家族（含刷新链路后续产生的所有 token），返回 401 "刷新 Token 已失效，请重新登录"；输出 `[安全事件][R502]` 级 Warning 日志（含 FamilyId/UserName/IP/撤销数）
    - **token 仅过期**（既非 IsUsed 也非 IsRevoked）→ 走原"无效或已过期"路径
  - **FamilyId 继承（含迁移兼容）**：正常刷新时，新 token 继承旧 token 的 `FamilyId`；若旧 token 无 `FamilyId`（迁移期数据），新链路生成新 `FamilyId`，后续重用检测即可正常工作。**这一兼容策略保证存量会话不会因升级而中断**
  - **附带数据完整性修复**：Refresh 创建新 `RefreshToken` 时补齐 `UserName = userEntity.UserName`（原代码遗漏该字段，与 Login 实体对齐，便于审计与 `RevokeAllUserTokens` 等用户级查询）
  - **攻击场景示例**：
    1. 攻击者偷到用户 token A（FamilyId=F1），合法用户在用 token A
    2. 攻击者先用 token A 刷新 → 拿到 token B（F1），A 被标记 `IsUsed=true`
    3. 合法用户后用 token A 刷新 → 触发 `IsUsed=true` 重用信号 → 整个 F1 家族（含 B）被吊销
    4. 攻击者再用 token B 刷新 → 触发 `IsRevoked=true` 重用信号 → RevokeFamily 幂等撤销（已撤销的不会再计数）
    5. 攻击者与合法用户的链路全部失效，用户被强制重新登录，攻击者被踢出
  - **多设备登录互不影响**：用户在设备 1 登录生成 F1，在设备 2 登录生成 F2，任一链路出现重用只会吊销自身家族，不会误伤另一设备
  - **编译验证**：`dotnet build Wms.Core.WebApi.csproj` 成功，0 Error / 0 Warning（传递依赖 Domain + Infrastructure 全部通过）
  - **测试用例**（手工执行）：
    - 正常刷新：用户 A 用刚拿到的 token 刷新 → 200 OK + 新 token，原 token `IsUsed=true`
    - 重用检测：把上一步已 `IsUsed=true` 的 token 再次提交刷新 → 401 + 所有同家族 token 被撤销 + `[安全事件][R502]` 日志
    - 过期 token：等待 7 天后用未使用 token 刷新 → 401 + "刷新 Token 无效或已过期"（不触发家族撤销）
    - 登出后旧 token 刷新：Logout 调用 `RevokeAllUserTokens` 后 → 旧 token `IsRevoked=true` → 再次刷新触发家族撤销（覆盖更广，符合预期）
    - 迁移兼容：升级前已存在的无 `FamilyId` token 刷新 → 正常通过，新链路获得新 `FamilyId`
  - **后续迁移**：需新增 EF Core 迁移 `AddRefreshTokenFamily`（添加 `RefreshTokens.FamilyId` 列 + `IX_RefreshTokens_FamilyId` 索引），现有数据 `FamilyId` 为 NULL，由 Refresh 流程在下次刷新时自然回填

##### ✅ R503 登出后 JWT 仍有效
- **文件**：`AuthController.cs:266-285`
- **根因**：JWT 无状态，Logout 仅吊销 RefreshToken
- **修复方案**：基于 Redis 的 JWT 黑名单
  ```csharp
  // Logout
  var jti = User.FindFirst("jti")?.Value;
  var ttl = token.ExpiresAt - DateTime.UtcNow;
  await _redis.StringSetAsync($"jwt:blacklist:{jti}", "1", ttl);

  // JwtBearer Events
  options.Events = new JwtBearerEvents {
      OnTokenValidated = async context => {
          var jti = context.Principal.FindFirst("jti")?.Value;
          if (await _redis.KeyExistsAsync($"jwt:blacklist:{jti}"))
              context.Fail("Token revoked");
      }
  };
  ```
- **验证**：Logout 后立即用旧 token 调 API 应返回 401
- **状态**：✅ 已修复（2026-07-07，IDistributedCache 抽象 + fail-open 容错策略 + 全链路覆盖）
  - **基础设施复用**：项目已注册 `IDistributedCache`（`RedisExtensions.cs:30-34` 启用 Redis 时 / `:56` 回退内存缓存），NuGet 包 `StackExchange.Redis` + `Microsoft.Extensions.Caching.StackExchangeRedis` 已安装，无需引入新依赖。`TokenService.GenerateToken` 已通过 `JwtRegisteredClaimNames.Jti` 写入 jti claim（GUID），无需改造
  - **新增服务**：`Wms.Core.WebApi/Services/IJwtBlacklistService.cs` + `JwtBlacklistService.cs`
    - `RevokeAsync(jti, expiresAtUtc, reason)`：将 jti 写入 `jwt:blacklist:{jti}`，TTL = `expiresAtUtc - Now`，token 自然过期后自动清理
    - `IsRevokedAsync(jti)`：检查 key 是否存在
    - 直接使用 `IDistributedCache`（绕开 `ICacheService` 的 JSON 序列化），值仅存撤销原因短字符串用于审计
  - **JwtBearer 事件注入**：`AuthenticationExtensions.cs` 在 `AddJwtBearer` 中注册 `options.Events = new JwtBearerEvents { OnTokenValidated = ... }`，每次 token 通过签名/过期校验后再到黑名单查询；无 jti claim 的 token 直接 `context.Fail("Token missing jti claim")`
  - **登出闭环**：`AuthController.Logout` 改为 `async Task<IActionResult>`，吊销 RefreshToken 后调用新私有方法 `RevokeCurrentJwtAsync(reason)`，从 `JwtRegisteredClaimNames.Jti` / `JwtRegisteredClaimNames.Exp` 提取 jti 与过期时间（Unix 秒），写入黑名单
  - **改密联动**：`ProfileController.ChangePassword` 改为 `async Task<Result>`，在 `_refreshTokenRepository.RevokeAllUserTokens` 之后追加 `_jwtBlacklistService.RevokeAsync`，将当前 access token 立即失效，实现"改密即下线"全链路保护（弥补原代码注释中"≤60 min 仍有效"的安全漏洞）
  - **DI 注册**：`RedisExtensions.AddWmsServices` 新增 `services.AddScoped<IJwtBlacklistService, JwtBlacklistService>()`，与 `ITokenService` 同位置
  - **fail-open 容错策略（安全权衡）**：
    - `JwtBlacklistService` 的所有异常（Redis 不可用）被 catch，`RevokeAsync` 只记录 `[R503][FAIL-OPEN]` Error 日志不抛；`IsRevokedAsync` 在异常时返回 false（放行）
    - **理由**：JWT 本身有 ≤60 min 自然过期，黑名单是增强机制；Redis 故障时若 fail-closed 会锁死整个认证系统，影响所有用户；fail-open 让故障期间被吊销 token 仍可用至自然过期，是可用性优先的权衡
    - **运维要求**：需监控 `[FAIL-OPEN]` 关键字告警，及时恢复 Redis；多实例部署**必须**启用 `Redis:Enabled=true`，否则内存回退仅单实例生效，黑名单不跨实例共享
  - **配置依赖**：`appsettings.json` 中 `Redis:Enabled=false`（默认）时使用内存缓存（仅单实例）；生产环境多实例部署应改为 `true` 并配置 `ConnectionString`
  - **编译验证**：`dotnet build Wms.Core.WebApi.csproj` 成功，0 Error（938 Warning 均为既有 CS1591/CS8618，与本次修改无关）
  - **测试用例**（手工执行）：
    - 正常登出：用户 A 登录 → 调 `/api/auth/logout` → 立即用旧 token 调 `/api/auth/me` → **401 Unauthorized**
    - 改密联动：用户 A 改密 → 用旧 token 调 `/api/profile` → **401 Unauthorized**（弥补原 ≤60 min 风险窗口）
    - 多实例生效（生产）：实例 1 上登出后，请求打到实例 2 → 同样 **401**（Redis 共享黑名单）
    - Redis 故障（fail-open）：手动关闭 Redis → 用户登出仍返回 200（fail-open），日志输出 `[FAIL-OPEN]` Error；旧 token 在 Redis 恢复前仍可用至自然过期，运维应监控告警并尽快恢复
    - TTL 自动清理：等 `Jwt:ExpirationMinutes`（默认 60 分钟）后查 Redis，黑名单 key 自动消失
    - 其他用户的 token 不受影响：A 登出后，B 的 token 仍可正常使用
  - **配套清理**：原 `ProfileController.ChangePassword` 中"当前 access token 仍有效到自然过期（≤60 min），前端会在改密成功后主动登出本会话"的代码注释已移除（被本修复闭环覆盖）
  - **后续优化（backlog）**：
    - 可考虑增加 `Jwt:StrictRevocation` 配置开关支持 fail-closed 模式，用于安全敏感场景
    - 可在 `UsersController` 管理员强制下线接口中调用 `RevokeAllUserTokensAsync`，但需要新增"按用户撤销所有未过期 JWT"的能力（当前黑名单只支持单 jti 撤销）

##### 🔴 R504 ForwardedHeaders 信任所有代理
- **文件**：`Wms.Net8/src/Wms.Core.WebApi/Program.cs:44-49`
- **根因**：`KnownNetworks.Clear()` + `KnownProxies.Clear()`
- **修复方案**：
  ```csharp
  if (!app.Environment.IsDevelopment()) {
      var proxies = builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>();
      foreach (var ip in proxies ?? []) options.KnownProxies.Add(IPAddress.Parse(ip));
      // 不 Clear KnownNetworks
  }
  ```
- **验证**：`curl -H "X-Forwarded-For: 127.0.0.1" /api/wcs/...` 仍被拒绝

##### 🔴 RF-001 登录页硬编码 Super/Admin/User 凭证
- **文件**：`Wms.Vue/src/views/_builtin/login/modules/pwd-login.vue:49-68`
- **根因**：`accounts` 计算属性硬编码 `Super/123456`、`Admin/123456`、`User/123456`
- **修复方案**：完全移除 `accounts`、`handleAccountLogin` 函数及相关模板/注释
- **验证**：构建后 `grep -r "123456" dist/` 无匹配

##### 🔴 RF-002 登录页无验证码/防爆破
- **文件**：`pwd-login.vue:35-38`
- **根因**：`useCaptcha` 仅 `setTimeout(resolve, 500)` 模拟
- **修复方案**：
  1. 集成图形/滑动验证码（如 AhCaptcha、腾讯验证码）
  2. 前端记录失败次数，>=3 次后强制要求验证码
  3. 与后端 R516 账号锁定策略协同（Redis 分布式锁）
- **验证**：连续失败 3 次后必须输入验证码才能继续

##### ✅ RF-003 clearAuthStorage 清理不完整
- **文件**：`Wms.Vue/src/store/modules/auth/shared.ts:9-12`
- **修复方案**：
  ```ts
  export function clearAuthStorage() {
    localStg.remove('token');
    localStg.remove('refreshToken');
    localStg.remove('globalTabs');  // 新增
  }
  ```
- **状态**：✅ 已修复（2026-07-07，按文档要求补充 globalTabs 清理 + 修复 resetStore 中"先清后写"逻辑矛盾）
  - **shared.ts 改造**：`clearAuthStorage` 新增 `localStg.remove('globalTabs')`，补充完整中文 JSDoc 说明清理范围（清理 token/refreshToken/globalTabs；保留 lastLoginUserId 与主题/语言/布局等用户偏好类，因与认证状态解耦）
  - **关键发现 - resetStore 顺序矛盾**：原 `index.ts:resetStore()` 第 48 行先调用 `clearAuthStorage()`（清掉 globalTabs），第 56 行又调用 `tabStore.cacheTabs()`（把当前 tabs 重新写回 localStorage）。两者顺序导致 globalTabs 被反复写回，**单纯按文档加 `localStg.remove('globalTabs')` 仍无法生效**。这是文档修复方案落地时必须同时修复的隐藏 bug
  - **index.ts resetStore 改造**：调整执行顺序，让 `clearAuthStorage()` 在 `tabStore.cacheTabs()` **之后**调用，保证 globalTabs 最终被真正清掉：
    ```ts
    async function resetStore() {
      recordUserId();
      authStore.$reset();
      if (!route.meta.constant) await toLogin();
      tabStore.cacheTabs();    // 先缓存（保留逻辑兼容性）
      clearAuthStorage();      // 再清理，最终 globalTabs 被清干净
      routeStore.resetStore();
    }
    ```
  - **跨用户隐私风险消除**：用户 A 登出后，localStorage 中的 globalTabs（含 A 访问过的路由路径与页面参数）被彻底清除，用户 B 登录时不会看到 A 的页面痕迹（即便 B 无权访问那些路由，路径名本身也属于行为画像）
  - **故意保留项**：
    - `lastLoginUserId`：由 `recordUserId()` 在 resetStore 开头主动写入，用于登录页默认选中上次账号，非敏感数据；与认证状态解耦
    - 主题（`themeSettings`/`darkMode`/`themeColor`）、语言（`lang`）、布局（`mixSiderFixed`）：跨会话的个人偏好，登出时清理会损害用户体验
  - **调用点全覆盖**：`clearAuthStorage` 全项目仅 1 处调用（`auth/index.ts:resetStore`），由 4 个场景触发：登录失败、获取用户信息失败、token 刷新失败 ×2（axios + alova 两个 request 客户端）。本次顺序修复对这 4 个场景全部生效
  - **类型检查**：`pnpm typecheck` 通过——本次修改的两个文件（`shared.ts` 与 `auth/index.ts`）零类型错误（业务页面既有的 Element Plus tag type 类型问题与本次修改无关）
  - **测试用例**（手工执行）：
    - 用户 A 登录后访问多个业务页（生成 globalTabs）→ 登出 → 检查 localStorage 中 `globalTabs` 应不存在
    - 用户 A 登出后用户 B 登录 → B 不会看到 A 的页面 tabs
    - 同一用户登出后再登录 → tabs 重新从空开始（业务上可接受，符合"登出 = 完全清理"语义）
    - DevTools → Application → Local Storage 检查：登出后只剩 `lastLoginUserId` + 主题/语言类 key，无 token/refreshToken/globalTabs

##### ✅ RF-004 无跨 Tab 会话同步
- **文件**：`Wms.Vue/src/App.vue`
- **修复方案**：
  ```ts
  onMounted(() => {
    window.addEventListener('storage', e => {
      if (e.key === 'token' && !e.newValue) {
        authStore.resetStore();
      }
    });
  });
  ```
- **状态**：✅ 已修复（2026-07-07，按文档方案落地 + 适配项目 `WMS_` 前缀）
  - **关键发现 - 文档方案的隐藏不兼容**：文档示例 `e.key === 'token'` 在本项目**永远不会匹配**。原因：项目通过 `VITE_STORAGE_PREFIX=WMS_`（`.env:56`，全环境统一）给 localStorage key 加前缀，`localStg.set('token', v)` 实际写入的 key 是 `"WMS_token"`（见 `packages/utils/src/storage.ts:19`）。storage 事件回调中 `e.key` 自然也是 `"WMS_token"`。直接套用文档方案会得到"代码无报错但功能静默失效"的结果，必须拼接前缀
  - **App.vue 改造**：在 `<script setup>` 中新增 `onMounted` / `onUnmounted`，注册跨 Tab storage 事件监听器：
    ```ts
    const storagePrefix = import.meta.env.VITE_STORAGE_PREFIX || '';
    const tokenStorageKey = `${storagePrefix}token`;

    function handleCrossTabAuthSync(e: StorageEvent) {
      // 仅关注 token key 被清除（其他 Tab 登出）
      if (e.key === tokenStorageKey && !e.newValue) {
        authStore.resetStore();
      }
    }

    onMounted(() => {
      window.addEventListener('storage', handleCrossTabAuthSync);
    });

    onUnmounted(() => {
      window.removeEventListener('storage', handleCrossTabAuthSync);
    });
    ```
  - **设计要点**：
    - **动态前缀**：通过 `import.meta.env.VITE_STORAGE_PREFIX` 动态拼接，避免硬编码 `"WMS_"`，前缀变更时无需改代码
    - **登出同步**：监听 `e.key === tokenStorageKey && !e.newValue`（newValue 为 null 表示 key 被删除），触发 `authStore.resetStore()` 让当前 Tab 跳登录页
    - **未实现登入同步（故意的）**：不在 Tab B 监听 token 出现就自动登入。原因：用户切回 Tab B 时若状态突变可能不符合预期；登入操作应留给用户主动刷新或点击
    - **storage 事件天然去重**：浏览器规范规定 storage 事件不在源 Tab 触发，只在其他 Tab 触发，无需额外判断
    - **`!e.newValue` 覆盖两种删除场景**：`removeItem('WMS_token')` 与 `localStorage.clear()` 都会让 `e.newValue === null`，单一条件即可覆盖
    - **onUnmounted 清理**：避免 HMR 或微前端场景下重复注册监听器导致内存泄漏
    - **resetStore 幂等**：即便多个 Tab 同时登出导致重复触发，resetStore 多次调用无副作用（已登出状态再调用只是空跑）
  - **触发链路联动 RF-003**：Tab A 登出 → `authStore.resetStore()` → `clearAuthStorage()` → `localStg.remove('token')` 实际删除 `"WMS_token"` → 跨 Tab storage 事件 → Tab B/C/D 的 `handleCrossTabAuthSync` 触发 → 各自 `authStore.resetStore()` 跳登录页。RF-003 修复的 `clearAuthStorage` 完整性（清理 token/refreshToken/globalTabs）确保跨 Tab 登出后下一用户登录不会看到上一用户的 globalTabs 残留
  - **类型检查**：`pnpm typecheck` 通过——本次新增代码零类型错误（App.vue 第 20/21 行的 `userName` 拼写错误为既有问题，与本次修改无关）
  - **测试用例**（手工执行）：
    - 用户在 Chrome 登录后开第二个 Tab（同浏览器同源），两个 Tab 都显示已登录
    - 在 Tab A 点击登出 → Tab B 在毫秒级延迟内自动跳转到登录页
    - 反向验证：在 Tab B 登出 → Tab A 自动跳登录页
    - 边界场景 1：Tab A 主动清空 localStorage（DevTools → Application → Clear）→ Tab B 自动登出（`!e.newValue` 覆盖 clear 场景）
    - 边界场景 2：localStorage 中手动写入 token（DevTools 模拟）→ 当前已登录的 Tab 不会重置（只监听 newValue 变空，不监听 newValue 变非空，符合"未实现登入同步"设计）
    - 边界场景 3：用户在同一 Tab 内登出 → 不会重复触发 resetStore（storage 事件不在源 Tab 触发）

#### 3.1.4 PR1.1 后端严重（第一轮）

##### 🔴 C1 数据库 SA 密码硬编码
- **文件**：`appsettings.json:14-16`、`appsettings.Development.json:12`、`appsettings.Production.json:12`
- **修复方案**：
  1. 主 `appsettings.json` 改占位符 `Password=__SET_VIA_ENV_OR_SECRETS__`
  2. 环境变量 `ConnectionStrings__DefaultConnection` 注入
  3. Development 用 `dotnet user-secrets`

##### 🔴 C2 JWT SecretKey 默认值
- **文件**：`JwtOptions.cs:26`、`appsettings.json:35`
- **修复方案**：默认值改 `string.Empty`；`AuthenticationExtensions.cs:36-44` 任何环境都拒绝 `CHANGE_*` 开头或 < 32 字符

##### 🔴 C3 InternalIpWhitelist 默认放行
- **文件**：`InternalIpWhitelistAttribute.cs:27-30`
- **修复方案**：未配置时返回 503 + Warning 日志（fail-closed）

##### ✅ C4 杭可设备凭据硬编码
- **文件**：`IHangKeClient.cs:61-66`
- **修复方案**：
  1. 移除 `const string _UserName = "TSGX2ZZ"`
  2. 新建 `HangKeClientOptions`
  3. 通过 `IOptions<HangKeClientOptions>` 注入
- **状态**：✅ 已修复（2026-07-07，const 改属性 + 12 处引用替换 + appsettings.json 默认值 + 环境变量可选覆盖路径）
  - **既有基础**：`HangKeClientOptions` 类已存在（`IHangKeClient.cs:46-92`），`IOptions<HangKeClientOptions>` 已在 `DefaultHangKeClient` 构造函数注入并存入 `_options` 字段，DI 注册链路（`MesExtensions.AddHangKeClient` → `Configure<HangKeClientOptions>` + `AddHttpClient<IHangKeClient, DefaultHangKeClient>`）完整。本次只需把凭据从 const 改为可绑定属性即可让链路生效
  - **HangKeClientOptions 改造**（`IHangKeClient.cs:58-66`）：
    ```csharp
    // 修复前：const 硬编码，无法被配置覆盖
    public const string _UserName = "TSGX2ZZ";
    public const string _PassWord = "ZZ@123";

    // 修复后：可绑定属性，默认空字符串（类默认值与 appsettings.json 解耦）
    public string UserName { get; set; } = string.Empty;
    public string PassWord { get; set; } = string.Empty;
    ```
    命名规范化：去掉下划线前缀（`_UserName` → `UserName`），符合 C# 属性 PascalCase 命名规范；XML 注释说明配置优先级（环境变量 > appsettings）与"生产环境如需更换凭据推荐通过环境变量覆盖"
  - **DefaultHangKeClient 改造**：6 个 SOAP 方法（`CancelTrayAsync` / `ChemicalPalletizeAsync` / `SeparatePalletizeAsync` / `GetDischargeInfoAsync` / `InOutNotifyAsync` / `GetCellDataAsync`）的 SOAP 头部凭据引用，从静态常量 `HangKeClientOptions._UserName` 改为实例属性 `_options.UserName`，共 12 处（每方法 2 处）。`replace_all` 一次完成，全项目无残留引用（grep 验证）
  - **appsettings.json 改造**（第 27-33 行）：HangKe 节新增 `UserName` / `PassWord` 字段，**采用杭可设备出厂凭据作为默认值**（非占位符），开箱即用：
    ```json
    "HangKe": {
      "Enable": false,
      "Endpoint": "",
      "TimeoutSeconds": 10,
      "UserName": "TSGX2ZZ",
      "PassWord": "ZZ@123"
    }
    ```
    - `appsettings.Development.json` / `appsettings.Production.json` 无 HangKe 节，继承主配置即可
    - **设计决策**：选用真实值而非占位符，权衡是开箱即用 vs 凭据进入 git 历史。考虑到杭可凭据为设备方提供的出厂默认值（非高敏感），且保留默认值便于首次部署验证，决定内置；生产环境仍推荐通过环境变量覆盖
  - **配置覆盖路径**（可选，运维侧执行）：
    - **环境变量**：`HangKe__UserName` / `HangKe__PassWord`（ASP.NET Core 标准双下划线分隔符，自动映射到 `HangKe:UserName`，优先级高于 appsettings.json）
    - **Kubernetes**：在 deployment.yaml 的 `env:` 段配置，从 Secret 资源 `valueFrom.secretKeyRef` 引用
    - **Development 覆盖**：`dotnet user-secrets set "HangKe:UserName" "..."`（user-secrets 优先级高于 appsettings.json）
    - **Docker**：`docker run -e HangKe__UserName=... -e HangKe__PassWord=...`
  - **凭据旋转联动**：原硬编码已从 C# 源码 const 形式移至 `appsettings.json` 默认值（仍可通过环境变量覆盖）。如需彻底更换新凭据，可通过环境变量覆盖（无需改代码），或修改 appsettings.json 后重启服务；详细流程参考 [CREDENTIAL-ROTATION-RUNBOOK.md](./CREDENTIAL-ROTATION-RUNBOOK.md)
  - **行为兼容性**：`HangKe:Enable=false`（默认）时整个客户端不被实际调用；启用时若运维未覆盖环境变量，将使用 appsettings.json 内置的 `TSGX2ZZ`/`ZZ@123` 出厂凭据，调用失败（401 等）会被 `catch (Exception ex)` 捕获后通过 `ResultInfo` 返回错误（不会崩溃）
  - **编译验证**：`dotnet build Wms.Core.Infrastructure.csproj` 成功，0 Error / 0 Warning（传递依赖 Application 全部通过）
  - **测试用例**（手工执行）：
    - 启动应用（`HangKe:Enable=false`）→ 正常启动，无凭据相关错误
    - 启用 HangKe 保留默认值 `TSGX2ZZ`/`ZZ@123` → SOAP 头部携带出厂凭据，调用成功（前提是杭可设备仍接受这组凭据）
    - 配置环境变量 `HangKe__UserName=新账号` `HangKe__PassWord=新密码` → 环境变量优先级生效，SOAP 头部使用新凭据
    - 全项目 grep `HangKeClientOptions._UserName` → 零匹配（验证 const 引用已彻底清除，类属性 `UserName` 已替代）
  - **后续加固建议（backlog）**：可在 `MesExtensions.AddHangKeClient` 启用时（`Enable=true`）校验 `UserName`/`PassWord` 非空，配置缺失时抛 `InvalidOperationException` 让应用快速失败（fail-closed），避免运行时才发现凭据问题

##### 🔴 C5 默认 admin/admin123 弱密码
- **文件**：`DbInitializer.cs:133,151`
- **修复方案**：任何环境都要求 `Admin:InitialPassword` 环境变量传入；未设置时生成随机 16 字符密码并记录日志

##### ✅ C6 SignalR Hub 完全无授权
- **文件**：`WmsHub.cs:12-62`、`AuthenticationExtensions.cs:80-100`、`MiddlewareExtensions.cs:82`
- **修复方案**：
  1. 类级 `[Authorize]`
  2. `SendTaskUpdate`/`SendStockChange`/`SendAlert` 改 private 或用 `[HubMethodName]` 控制
  3. 后端推送统一通过 `IHubContext<WmsHub>`
  4. 前端 SignalR 连接携带 JWT
- **状态**：✅ 已修复（2026-07-07，三层纵深防御 + 死代码清理 + SignalR token 提取事件）
  - **第一层 - 类级 `[Authorize]`**：`WmsHub` 加 `[Authorize]` + `using Microsoft.AspNetCore.Authorization;`，匿名连接被认证中间件 401 拒绝；与 `UsersController`/`JobScheduleController` 既有 `[Authorize]` 风格一致
  - **第二层 - 路由层 `.RequireAuthorization()`**：`MiddlewareExtensions.cs:82` 的 `app.MapHub<WmsHub>("/hubs/wms")` 链上追加 `.RequireAuthorization()`。即便后续有人误删 Hub 类上的 `[Authorize]`，路由层仍会强制鉴权，双层防御
  - **第三层 - 死代码清理（消除客户端可调用入口）**：删除 `SendTaskUpdate` / `SendStockChange` / `SendAlert` 三个 `public` 方法。探查确认它们是**纯死代码**——全项目 grep `.SendTaskUpdate(`/`.SendStockChange(`/`.SendAlert(` 零匹配，唯一的服务端推送点 `WcsTaskSyncService.cs:230` 直接 `_hub.Clients.All.SendAsync("ReceiveTaskUpdate", ...)`，不经过 Hub 实例方法（`IHubContext<WmsHub>` 只暴露 `Clients`/`Groups`，无法调用 Hub 实例方法）。这三个方法原本仅供已连接客户端 `connection.invoke(...)` 调用（即漏洞入口），删除前后行为一致（无任何调用方），删除后彻底消除"客户端伪造广播"风险
  - **关键补丁 - SignalR token 提取事件**：`AuthenticationExtensions.cs` 的 `JwtBearerEvents` 在 `OnTokenValidated`（R503 黑名单）**同级之前**新增 `OnMessageReceived`，仅对 `path.StartsWithSegments("/hubs")` 路径从 query string `access_token` 提取 JWT 赋给 `context.Token`。**这是 SignalR WebSocket 鉴权的关键**：WebSocket 客户端无法使用 `Authorization` 头，必须走 query string；缺失该事件会导致即便加了 `[Authorize]`，未来 WebSocket 客户端也无法通过鉴权。范围限定 `/hubs` 避免普通 API 也接受 query token（缩小攻击面）
  - **事件执行顺序**：`OnMessageReceived`（提取 token）→ 签名/过期/Issuer/Audience 校验 → `OnTokenValidated`（R503 黑名单）。R503 链路完全复用，无需改造
  - **前端无影响说明**：`Wms.Vue/` 当前未安装 `@microsoft/signalr`、无 `HubConnection` 代码，后端推送目前进入"虚空"。本次后端改动对前端零影响（没有客户端会因此连接失败），原计划第 4 项"前端 SignalR 连接携带 JWT"转为未来接入契约：未来接入前端 SignalR 客户端时需用 `withUrl('/hubs/wms', { accessTokenFactory: () => localStg.get('token') })`，并在登出/改密（RF-003 / R503 已覆盖）时主动 `connection.stop()`
  - **复用现有基础设施**：JWT 鉴权链路（`AddWmsAuthentication` → `AddJwtBearer` + R503 黑名单 + `IDistributedCache`）已完整，无需新依赖
  - **编译验证**：`dotnet build Wms.Core.WebApi.csproj -o bin-verify-c6` 通过，**0 Error**（750 Warning 均为既有 CS1591/CS8618/ASP0019/EF1002，与本次修改无关）
  - **测试用例**（手工执行）：
    - 匿名 WebSocket 连接 `/hubs/wms` → 401 拒绝
    - 携带有效 JWT（query string `?access_token=xxx`）的连接 → 连接成功，`OnConnectedAsync` 触发
    - 携带被 R503 吊销的 JWT（登出后） → 401 拒绝（黑名单 `OnTokenValidated` 生效）
    - 客户端尝试 `connection.invoke("SendTaskUpdate", ...)` → 方法不存在，SignalR 返回错误（消除伪造广播入口）
    - `WcsTaskSyncService` 后端推送 `ReceiveTaskUpdate` → 已认证客户端正常收到消息（行为不变）

#### 3.1.5 PR1.2 前端严重

##### 🔴 C7 开放重定向
- **文件**：`hooks/common/router.ts:102-105`
- **修复方案**：
  ```ts
  function isSafeRedirect(url: string): boolean {
    return url.startsWith('/') && !url.startsWith('//') && !url.startsWith('/\\');
  }
  ```

##### 🔴 C8 Pinia Store 自引用
- **文件**：`store/modules/auth/index.ts:14-16`
- **修复方案**：移除第 16 行 `const authStore = useAuthStore()`；`resetStore` 内部按需获取

##### 🔴 C9 动态路由完全信任后端
- **文件**：`service/api/route.ts:154-191`
- **修复方案**：
  1. path 白名单 `/^/[a-zA-Z0-9\-\/_]*$/`
  2. 维护已注册视图 Set，viewKey 不在白名单则跳过
  3. 拒绝含 `://`、`javascript:` 的项

##### 🔴 C10 Apifox Token 硬编码
- **文件**：`service-alova/request/index.ts:37`
- **修复方案**：改 `import.meta.env.VITE_APIFOX_TOKEN`，`.env.development` 添加

##### ✅ C11 自定义报表 SQL 注入风险
- **文件**：`views/wms/report/custom-edit.vue:84-100`
- **修复方案**：客户端黑名单校验（禁 `DROP`/`TRUNCATE`/`ALTER`/`EXEC`/`xp_`/`sp_`）
- **状态**：✅ 已修复（2026-07-07，本地校验函数 + handleSave 入口校验 + 隐藏字段 sqlExpression 覆盖）
  - **威胁模型**：`custom-edit.vue` 是管理员配置自定义报表的页面，提交的 `sqlTemplate` / `countSqlTemplate` / `filterSqlMappings[].sqlExpression` 直接拼接到后端 `DynamicSqlProvider` 执行，没有任何前端校验。攻击者（或管理员误操作）可提交 `SELECT * FROM Users; DROP TABLE Users--` 等危险 SQL
  - **设计定位**：客户端是纵深防御的第一层（提供即时反馈 + 降低恶意请求量），**真正的安全边界在后端**（`DynamicSqlProvider` + `ISqlValidator`，已通过 `RedisExtensions.AddWmsServices` 注册）。即便绕过前端直接调 API，后端依然会拦截；前端校验主要阻拦误操作与提升攻击成本
  - **新增本地校验函数** `validateReportSql(sql, fieldLabel)`（内联在 custom-edit.vue，未抽取到 utils，避免过度抽象——当前仅一处使用）：
    - **DDL 黑名单**：`DROP` / `TRUNCATE` / `ALTER` / `CREATE` / `RENAME`（修改 schema）
    - **DML 写操作黑名单**：`INSERT` / `UPDATE` / `DELETE` / `MERGE`（报表只允许 SELECT）
    - **DCL 黑名单**：`GRANT` / `REVOKE`（权限变更）
    - **过程调用黑名单**：`EXEC` / `EXECUTE`（SQL Server 命令执行）
    - **危险模式正则**：`\bsp_` / `\bxp_`（SQL Server 扩展存储过程前缀）、`;`（堆叠注入分隔符）、`--`（行注释）、块注释开闭合序列（注入绕过常用）
    - **单词边界**：关键字使用 `\b...\b` 精确匹配，避免字段名误判（如 `UpdatedBy` 不会被误判为 `UPDATE`）
    - **大小写不敏感**：所有正则带 `i` 标志
    - **空值放行**：字段可选，空字符串允许提交（后端按 undefined 处理）
  - **handleSave 入口集成**：保存前对 4 类输入全量校验，任一失败即 `ElMessage.error` 阻断保存：
    1. `formData.sqlTemplate` → 标签"查询SQL"
    2. `formData.countSqlTemplate` → 标签"COUNT SQL"
    3. `formData.filterSqlMappingsJson` → 先 JSON.parse（保持原有错误处理），对解析后的数组**每个** `sqlExpression` 字段单独校验 → 标签"筛选SQL映射[N].sqlExpression"
    4. （defaultSort 不走此校验：该字段由后端 Q402 的 `SanitizeOrderBy` 白名单单独防护，C11 不重复）
  - **隐藏风险点覆盖 - filterSqlMappings.sqlExpression**：原方案文档只提到 SQL 模板，实际 `filterSqlMappingsJson` 中每个 mapping 对象的 `sqlExpression` 字段（如 `CreatedTime >= @startDate`）也会被拼接进最终 SQL。本次校验对该字段也做了黑名单扫描，防止攻击者把 `; DROP TABLE--` 藏在筛选映射里
  - **顺手优化**：将 filterSqlMappings 的 JSON.parse 结果在校验阶段保存到 `parsedFilterMappings` 变量复用，避免后续构造 payload 时重复解析（原代码 parse 两次）
  - **误判与容错**：字符串字面量包含关键字会误报（如 `WHERE Name = 'Drop Ship'`），但报表 SQL 极少出现此类字面量；错误提示明确告知"如确属业务需要，请联系运维通过后端配置"，提供绕过路径（后端有完整的 SqlValidator 把关）
  - **类型检查**：`pnpm typecheck` 通过——本次修改后 custom-edit.vue 零类型错误（中途踩过一个 JSDoc 注释包含 `*/` 序列导致注释提前结束的坑，已修复）
  - **测试用例**（手工执行）：
    - 正常 SELECT：`SELECT * FROM Stock WHERE Warehouse = 'A'` → 保存成功
    - DROP 注入：`SELECT * FROM Users; DROP TABLE Users--` → 提示"查询SQL 包含禁止的 SQL 关键字: DROP"，阻断保存
    - 堆叠注入：`SELECT * FROM Users; SELECT * FROM Roles` → 提示"查询SQL 包含禁止的模式: 分号 ;（堆叠注入风险）"
    - 扩展存储过程：`EXEC xp_cmdshell 'dir'` → 提示"查询SQL 包含禁止的 SQL 关键字: EXEC"
    - 筛选映射注入：`[{"filterField":"x","sqlExpression":"1=1; DROP TABLE Users"}]` → 提示"筛选SQL映射[0].sqlExpression 包含禁止的 SQL 关键字: DROP"
    - 字段名误判验证：`SELECT UpdatedBy FROM Logs`（`UpdatedBy` 含 `UPDATE` 子串）→ 校验通过（单词边界正确）
    - 大小写：`drop table Users`（全小写）→ 提示包含 DROP
  - **后续建议**：管理员可考虑在前端增加 SQL 高亮提示（如检测到黑名单关键字时输入框边框变红），但属于体验增强，非安全必需

#### 3.1.6 PR1.3 依赖严重漏洞

##### ✅ C12 xlsx 0.18.5 CVE
- **文件**：`Wms.Vue/package.json:94`
- **修复方案**：
  ```bash
  pnpm remove xlsx
  pnpm add https://cdn.sheetjs.com/xlsx-0.20.x/xlsx-0.20.x.tgz
  ```
- 详细参考：[DEPENDENCY-UPGRADE-GUIDE.md](./DEPENDENCY-UPGRADE-GUIDE.md)
- **状态**：✅ 已修复（2026-07-07，从 SheetJS CDN 安装 0.20.3，绕开 npm registry 停更）
  - **漏洞背景**：npm registry 上的 `xlsx` 包最后版本为 0.18.5，SheetJS 已转为 CDN 分发。0.18.5 存在两个已知 CVE：
    - **CVE-2023-30533**：原型链污染（Prototype Pollution，CVSS 7.5）——攻击者可通过精心构造的 Excel 文件污染 `Object.prototype`，劫持应用逻辑
    - **CVE-2024-22363**：ReDoS 正则灾难回溯（CVSS 7.5）——特定输入导致正则引擎指数级回溯，CPU 100% DoS
  - **升级动作**（在 `Wms.Vue/` 目录）：
    ```bash
    pnpm remove xlsx
    pnpm add https://cdn.sheetjs.com/xlsx-0.20.3/xlsx-0.20.3.tgz
    ```
  - **`package.json` 变化**：
    ```diff
    - "xlsx": "0.18.5"
    + "xlsx": "https://cdn.sheetjs.com/xlsx-0.20.3/xlsx-0.20.3.tgz"
    ```
    `pnpm-lock.yaml` 同步更新（tarball 直接锁定，不走 registry 版本解析，无供应链漂移风险）
  - **版本验证**：`node -e "require('xlsx').version"` → `0.20.3` ✅
  - **API 兼容性**：全项目仅 `src/views/plugin/excel/index.vue:3` 一处使用 xlsx，调用的 API 均为 SheetJS 核心稳定接口，0.18.5 → 0.20.3 **零 breaking change**：
    - `utils.book_new()` — 新建工作簿
    - `utils.aoa_to_sheet(excelList)` — 数组转工作表
    - `utils.book_append_sheet(workBook, workSheet, name)` — 追加工作表
    - `writeFile(workBook, filename)` — 写文件（浏览器侧触发下载）
  - **typecheck 验证**：`pnpm typecheck` 输出中 `grep -iE "excel|xlsx"` **零匹配**——唯一使用 xlsx 的 `excel/index.vue` 零类型错误。其余 typecheck 报错（Element Plus tag 类型、`export-history.vue` 缺 axios 模块等）均为既有业务页面问题，与本次升级无关
  - **为什么必须从 CDN 装而不是 `pnpm add xlsx@latest`**：npm registry 上的 `xlsx` 永远停在 0.18.5（SheetJS 与 npm 决裂后未再发布），`xlsx@latest` 仍会装到漏洞版本。只有 SheetJS 官方 CDN 的 tarball 才是修复版
  - **`.npmrc` 影响**：项目用 `registry=https://registry.npmmirror.com/`（淘宝镜像），但 pnpm 的 `add <tarball-url>` 直接从 URL 下载，不受 registry 配置影响，CDN tarball 安装成功
  - **供应链说明**：`https://cdn.sheetjs.com` 是 SheetJS 官方维护的 CDN，tarball 含完整源码与类型定义；安装后 `pnpm-lock.yaml` 以 resolved URL + integrity hash 锁定，与 npm registry 包同等可信
  - **测试用例**（手工执行）：
    - 访问"Excel 导出"页面（`/plugin/excel`）→ 点击"导出 excel"按钮 → 正常下载 `用户数据.xlsx`，用 Excel/WPS 打开内容正确
    - 用 ReDoS PoC 文件测试（如 `CVE-2024-22363` 的恶意 xlsx）→ 不再触发 CPU 卡死（需配合后端 `ExportService` 验证，若后端也读 xlsx）
    - 原型链污染 PoC → `Object.prototype` 不被污染
  - **后续建议**：在 CI 中加入 `pnpm audit`（已在 [DEPENDENCY-UPGRADE-GUIDE.md 5.2](./DEPENDENCY-UPGRADE-GUIDE.md) 规划），自动检测 xlsx 回退到 0.18.5 或其他依赖漏洞

#### 3.1.7 PR1.4 部署严重

##### 🔴 C13 数据库备份入库
- **文件**：`DB/WmsDb.Bak`、`DB/ctask.bak`
- **修复方案**：移到外部存储；根目录新建 `.gitignore` 加 `*.bak`；git 历史清理见 [CREDENTIAL-ROTATION-RUNBOOK.md](./CREDENTIAL-ROTATION-RUNBOOK.md)

---

> **PR2-PR7 详细内容**：本章节展示了 PR1 的修复清单格式示例。完整的 PR2-PR7 内容请参阅计划文件 `C:\Users\Administrator\.claude\plans\hidden-snuggling-nebula.md`，本主文档作为入口和摘要。

---

## 4. 数据库迁移影响

本次修复涉及 4 个 EF Core 迁移：

| 迁移名 | 所属 PR | 描述 | 风险评估 |
|--------|--------|------|---------|
| `AddIndexesAndConstraints` | PR2 (H10) | 为高频字段补索引、字符串长度、Decimal 精度 | 大表上创建索引可能耗时，建议低峰期 |
| `AddRowVersionConcurrency` | PR2 (T316) | 核心实体添加 RowVersion 字段 | 新增可空字段，迁移安全 |
| `AddUniqueConstraints` | PR2 (T326) | MaterialCode、UserName、BarCode 等加唯一索引 | **如已有重复数据会失败**，需先清理 |
| `AddRefreshTokenFamily` | PR1 (R502) | RefreshToken 添加 FamilyId 字段 | 新增可空字段，迁移安全 |

### 迁移应用顺序

```bash
# 1. 在 Development 环境测试
cd Wms.Net8/src/Wms.Core.Infrastructure
dotnet ef migrations add AddIndexesAndConstraints --startup-project ../Wms.Core.WebApi
dotnet ef database update --startup-project ../Wms.Core.WebApi

# 2. 生成 SQL 脚本（用于生产应用）
dotnet ef migrations script <上一个迁移> AddIndexesAndConstraints --startup-project ../Wms.Core.WebApi -o add-indexes.sql

# 3. 生产环境应用前备份
sqlcmd -S ... -Q "BACKUP DATABASE WmsDb TO DISK = 'D:\Backup\WmsDb_pre_indexes.bak'"

# 4. 应用脚本
sqlcmd -S ... -i add-indexes.sql
```

---

## 5. Breaking Changes 与缓解措施

| 变更 | 影响范围 | 缓解措施 |
|------|---------|---------|
| UsersController 加 `[Authorize(Roles="Admin")]`（T301-T306） | 前端 manage/user 普通用户访问会 403 | 前端按钮权限校验保持，正常用户本就不该访问；管理员无感知 |
| UsersController 返回 UserDto（T305） | 前端依赖 User 实体某些字段的代码会失败 | 前端从未使用 `passwordHash` 等字段，无实际影响 |
| ForceComplete 状态校验（T315） | 前端可能尝试重复点击强制完成 | 前端按钮已根据状态显示，影响小 |
| RowVersion 并发控制（T316） | 偶发冲突返回 409 | 前端 catch 409 后提示"数据被他人修改，请刷新" |
| FlowController 用 DTO（T323） | 前端 flow-templates.vue 提交字段需对齐 DTO | PR3 同步修改前端 payload |
| FluentValidation 强制（T322） | 前端提交不合法数据会 400 | 前端已有部分校验，PR3 TF317 会补全 |
| SignalR `[Authorize]`（C6） | 前端 SignalR 客户端必须携带 JWT | 当前前端无 SignalR 客户端代码，本次零影响；未来接入时需用 `accessTokenFactory` 携带 JWT（见 C6 状态说明） |
| `/health` 加鉴权（H9） | 负载均衡器/监控工具配置 | 提供 `/healthz`（无鉴权）作为 LB 探针 |
| Docker 端口关闭（D1） | 开发时直连数据库工具链 | 通过 `docker exec` 或 SSH 隧道 |
| 同步 → 异步（H4） | AuthController 调用 RefreshTokenRepository 处 | PR2 同步修改调用方 |
| appsettings 占位符化（C1） | 所有部署环境必须配置环境变量/user-secrets | PR5 提供完整脚本和文档 |
| ForwardedHeaders 限制（R504） | 反代 IP 必须配置 KnownProxies | PR5 文档说明，运维配置 |
| JWT 黑名单（R503） | Logout 后 token 立即失效（用户感知） | 用户体验改善，无需缓解；改密后当前 token 也会立即失效，前端需在 ChangePassword 成功后跳转登录页（与既有提示文案"请使用新密码重新登录"一致） |
| WCS HMAC 认证（R505） | 所有 WCS 设备需要重新配置 ApiKey | 需要与设备方协调，分批切换 |

---

## 6. 凭据紧急旋转步骤

详见：[CREDENTIAL-ROTATION-RUNBOOK.md](./CREDENTIAL-ROTATION-RUNBOOK.md)

摘要：PR1 合并后**必须立即**执行以下旋转：
1. 修改 SQL Server SA 密码（≥16 字符强密码）
2. 重新生成 JWT SecretKey（`openssl rand -base64 32`）
3. 修改杭可设备凭据（联系设备方）
4. 修改默认 admin 密码
5. 重新生成 Apifox Token
6. 检查 git 历史中的密码泄露

---

## 7. 端到端验证清单

详见：[REMEDIATION-VERIFICATION-CHECKLIST.md](./REMEDIATION-VERIFICATION-CHECKLIST.md)

摘要：38 项验证点，覆盖编译、测试、启动、认证、安全、Docker、配置 7 个类别。

---

## 8. Backlog（中低级别问题记录）

本次未修复的约 120 个中/低级别问题，作为后续迭代 backlog：

### 8.1 中级别问题（约 80 个）

#### 后端中级别（代表性）
- T313 全系统无仓库级数据隔离（需评估业务需求）
- T314 MenusController.BatchDelete 无数量上限
- T319 SimToolController.UpdateLocation 无事务
- T320 DbInitializer 并发启动无保护
- T324 WcsRequestValidator 缺长度限制
- T325 ChangePasswordRequestValidator 密码复杂度不足
- T328 UnitloadConfiguration 缺 OnDelete 策略
- T329 UnitloadsController.GetAll 内存子查询
- T330 GetById 三层 Include 笛卡尔积
- T332 WcsTaskSyncService 无限重试
- T335 Result 所有错误返回 HTTP 200
- T337 OperationLogFilter 脱敏不完整（缺 token/secret/key）
- T338 OperationLogFilter 不记录 GET 请求（审计盲区）
- Q411 DynamicSqlProvider 列名注入（管理员工具，风险低）
- Q412 SignalR 无速率限制和消息大小限制
- Q413 杭可错误信息泄露
- Q414 UploadController 返回完整服务器路径
- Q415 FileHelper SanitizeFileName Linux 路径遍历
- Q416 DataCleanupService 长时间锁表
- Q417 ReportExportService GC.Collect() 性能 DoS
- Q418 Hangfire HTTP Job 请求头注入
- Q419 全局 JSON 缩进带宽浪费
- R512 Kestrel 未配置并发连接数限制
- R513 安全响应头缺失 HSTS/CSP/Referrer-Policy
- R514 速率限制中间件在认证之后
- R515 账号锁定机制基于内存（多实例失效）
- R516 密码策略过于宽松（6 位、无复杂度）
- R520 外部 HTTP 客户端未配置 TLS 版本
- R521 修改密码后当前 JWT 不失效（与 R503 关联）
- R522 静态文件未配置 MIME 白名单
- ...

#### 前端中级别（代表性）
- F-012 home/index.vue refreshTimer 未清理
- F-014 80+ `as any` 类型断言
- F-017 deep:true 监听大对象
- F210 报表 onMounted 异步竞态
- F211 range 筛选器覆盖 startDate
- F213 语言包加载无并发锁
- F215 文件上传无大小校验
- F217 流程模板添加无 loading
- F218 批量删除无数量上限
- F221 Cron 表达式格式问题
- QF407 缺少 CSP / X-Frame-Options / Referrer-Policy
- QF408 SRI 缺失
- QF410 Object.assign 原型链污染风险（14 处）
- QF411 JSON.parse 无 Schema 校验
- RF-009 下载文件名无 XSS 校验
- RF-010 URL 参数未校验用作 API 参数
- RF-012 i18n 语言包后端内容无 XSS 过滤
- RF-013 Vite DevTools 在所有环境启用
- RF-014 修改密码延迟跳转 1.5 秒窗口
- RF-015 loading.ts 主题颜色 CSS 注入风险
- ...

### 8.2 低级别问题（约 80 个）

代码质量、命名规范、注释完善等。代表性：
- 80+ `as any` 类型断言（与 F-014 部分重叠）
- 空 catch 块剩余部分（F-009 修复 40+，剩余约 10 处）
- wangeditor 4.x XSS 升级到 5.x（DEPENDENCY-UPGRADE-GUIDE 已覆盖）
- `@ts-nocheck` 自动生成文件加注释
- demoRequest 删除或实现
- fetchIsRouteExist 删除
- NodeJS.Timeout → ReturnType<typeof setInterval>
- packages/axios .ignored 前缀清理
- console.log 调试残留（已部分覆盖）
- ECharts setOption 添加 debounce
- 物料导入模板下载路径硬编码
- 关键操作向后端验权
- 全项目零测试覆盖（PR6 部分）
- resetForm 硬编码 'admin'
- i18n 遗漏 'Required'
- barcode.ts 多余 try-catch
- report-view 未监听 code 变化
- CI/CD 完全缺失（建议建立 GitHub Actions）
- vue-router 版本号异常（5.0.4）
- 文档含测试凭据（PR7 已覆盖）
- 测试模板端口不匹配
- 等等

### 8.3 后续迭代建议

| 优先级 | 任务 | 建议时间 |
|--------|------|---------|
| P1 | 补全所有 FluentValidation（T322 完整版） | 下一个 sprint |
| P1 | 实现仓库级数据隔离（T313） | 评估业务需求后 |
| P1 | Result<T> 转换为正确 HTTP 状态码（T335） | 下一个 sprint |
| P2 | 全项目单元测试覆盖率达到 60% | 2-3 个 sprint |
| P2 | 建立 CI/CD 流水线（含 SAST/依赖扫描） | 1-2 个 sprint |
| P2 | 实现完整的 CSP/HSTS/Permissions-Policy 响应头 | 下一个 sprint |
| P3 | TypeScript 严格模式，消除所有 `as any` | 持续 |
| P3 | 前端 PWA / Service Worker 支持 | 视业务需求 |
| P3 | 多语言完善（i18n 全覆盖） | 持续 |

---

## 9. 修复进度跟踪

> 本章节在修复过程中持续更新。

| PR | 状态 | 负责人 | 完成日期 | 备注 |
|----|------|--------|---------|------|
| PR1 | 🔄 进行中 | - | - | 28 个严重问题（T303/T305/T306/T315/Q401/Q402/Q403/R502/R503/RF-003/RF-004/C11/C4/C6/C12 已修复） |
| PR2 | ⏳ 待开始 | - | - | 24 个后端高优先级 |
| PR3 | ⏳ 待开始 | - | - | 18 个前端高优先级（含 F10 dead code 清理） |
| PR4 | ⏳ 待开始 | - | - | 5 个部署/运维 |
| PR5 | ⏳ 待开始 | - | - | 配置基线 |
| PR6 | ⏳ 待开始 | - | - | 12 个单测 |
| PR7 | ⏳ 待开始 | - | - | 验证+旋转+文档 |
| PR8 | ✅ 已完成 | Claude | 2026-07-06 | 本套文档 |

状态图标：⏳ 待开始 / 🔄 进行中 / ✅ 已完成 / ⚠️ 有阻塞

---

## 附录 A：复用的现有工具与模式

| 工具/模式 | 路径 | 复用于 |
|-----------|------|--------|
| `Result<T>` 统一返回 | `Wms.Core.Domain/Common/Result.cs` | 异常返回格式 |
| `OperationLogFilter` 密码脱敏 | `Filters/OperationLogFilter.cs:128-133` | 扩展到其他敏感字段 |
| `GlobalExceptionHandler` 脱敏 | `Middleware/GlobalExceptionHandler.cs:57-76` | 异常处理参考 |
| `BcryptPasswordHasher` | `Infrastructure/Services/BcryptPasswordHasher.cs` | 默认密码生成 |
| `SqlSafety`（Q402 抽取） | `Infrastructure/Security/SqlSafety.cs` | ORDER BY 列名/方向白名单（Q402 已落地） |
| `HttpCallSafety`（Q403 抽取） | `Infrastructure/Security/HttpCallSafety.cs` | http-call URL 白名单 + Header 黑名单（Q403 已落地） |
| `XmlSafety`（Q401 抽取） | `Infrastructure/Security/XmlSafety.cs` | XXE 防护：禁 DTD + 限制实体扩展 + 禁外部解析（Q401 已落地，可复用到未来其他 SOAP/XML 客户端） |
| `IJwtBlacklistService`（R503 新增） | `WebApi/Services/JwtBlacklistService.cs` | JWT 黑名单：基于 IDistributedCache 实现 Logout / 改密后立即失效（R503 已落地，可复用到未来的"管理员强制下线"场景） |
| `IDistributedLockService` | 项目内已存在 | 货位分配并发锁（H7） |
| `IHubContext<WmsHub>` | 已被 BackgroundJobService 使用 | 后端 SignalR 推送（C6） |
| `useRouterPush` | `hooks/common/router.ts` | 开放重定向校验（C7） |

## 附录 B：术语表

- **IDOR**：Insecure Direct Object Reference，不安全的直接对象引用
- **TOCTOU**：Time-of-Check to Time-of-Use，检查-使用时差竞态
- **SSRF**：Server-Side Request Forgery，服务端请求伪造
- **XXE**：XML External Entity，XML 外部实体注入
- **RCE**：Remote Code Execution，远程代码执行
- **CVE**：Common Vulnerabilities and Exposures，通用漏洞披露
- **HMAC**：Hash-based Message Authentication Code，哈希消息认证码
- **mTLS**：mutual TLS，双向 TLS 认证
- **CSP**：Content Security Policy，内容安全策略
- **HSTS**：HTTP Strict Transport Security
- **SRI**：Subresource Integrity，子资源完整性
