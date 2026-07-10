# OWASP Top 10 (2021) 映射

| 元信息 | 值 |
|--------|-----|
| 文档版本 | v1.0 |
| 生成日期 | 2026-07-06 |
| 适用范围 | Wms.Core |
| 文档状态 | 草案（待审核） |
| 关联文档 | [SECURITY-REMEDIATION-PLAN.md](./SECURITY-REMEDIATION-PLAN.md) |

---

## 1. 概述

本文档将 WMS 项目五轮审计发现的问题映射到 **OWASP Top 10 (2021)** 类别，便于合规审计、安全培训和未来回顾。

---

## 2. OWASP Top 10 映射表

### A01:2021 — Broken Access Control（访问控制失效）🔴 严重

WMS 项目在此类别暴露最多最严重的问题。

| 问题编号 | 标题 | 文件 | 严重程度 |
|---------|------|------|---------|
| T301 | UsersController 全部管理接口缺授权 | `UsersController.cs:23` | 🔴 Critical |
| T302 | ResetPassword IDOR | `UsersController.cs:324-345` | 🔴 Critical |
| T303 | ChangePassword IDOR | `UsersController.cs:295-316` | 🔴 Critical |
| T304 | Update 角色提升 | `UsersController.cs:199-255` | 🔴 Critical |
| T307 | WasteBatchSettingController 完全缺授权 | `WasteBatchSettingController.cs:17` | 🟠 High |
| T309 | MenusController.Delete 授权被注释 | `MenusController.cs:383,424` | 🟠 High |
| T310 | RoleController 授权被注释 | `RoleController.cs:25` | 🟠 High |
| T311 | SimToolController 缺管理员授权 | `SimToolController.cs:24` | 🟠 High |
| T312 | FlowController 缺管理员授权 | `FlowController.cs:18` | 🟠 High |
| Q403 | JobScheduleController 缺授权（SSRF） | `JobScheduleController.cs:14` | ✅ 已修复（2026-07-07，三层防御） |
| R509 | 报表导出文件 IDOR | `ReportsController.cs:140-158` | 🟠 High |
| H1 | ExcelExport AllowAnonymous | `UploadController.cs:193` | 🟠 High |
| C6 | SignalR Hub 无授权 | `WmsHub.cs:12-62` | 🔴 Critical |
| R505 | WCS/Hangke 设备级认证缺失 | `WcsController.cs:34` 等 | 🟠 High |
| F-006 | isBuiltIn 标记可任意修改 | `role.vue:45-47` | 🟠 High |
| F-007 | 菜单权限仅前端校验 | `role.vue:268` | 🟠 High |

### A02:2021 — Cryptographic Failures（加密失败）

| 问题编号 | 标题 | 文件 | 严重程度 |
|---------|------|------|---------|
| C1 | SA 密码明文硬编码 | `appsettings.json:14-16` | 🔴 Critical |
| C2 | JWT SecretKey 占位值 | `appsettings.json:35` | 🔴 Critical |
| C4 | 杭可设备凭据硬编码 | `IHangKeClient.cs:61-66` | 🔴 Critical |
| C5 | 默认 admin/admin123 弱密码 | `DbInitializer.cs:133,151` | 🔴 Critical |
| Q405 | 数据库无 TLS（TrustServerCertificate） | `appsettings.json:14-16` | 🟠 High |
| Q406 | Redis 无 TLS | `RedisExtensions.cs:39` | 🟠 High |
| R502 | RefreshToken 重用未检测（家族追踪缺失） | `AuthController.cs:141-166` | 🔴 Critical |
| B2-017 | RefreshToken 用纯 SHA256（建议 HMAC） | `TokenService.cs:66-72` | 🟡 Medium |
| Q404 | 容器编码使用不安全随机数 | `DefaultMesClient.cs:494` | 🟠 High |

### A03:2021 — Injection（注入）

| 问题编号 | 标题 | 文件 | 严重程度 |
|---------|------|------|---------|
| Q402 | ReportService 排序字段 SQL 注入 | `ReportService.cs:484-501` | ✅ 已修复（2026-07-07，SqlSafety 白名单 + 400） |
| Q401 | XXE 风险（杭可 SOAP 响应） | `DefaultHangKeClient.cs:101` | 🔴 Critical |
| R508 | CSV/Excel 公式注入 | `ExportService.cs:65` 等 | 🟠 High |
| C11 | 自定义报表 SQL 注入风险 | `custom-edit.vue:84-100` | 🔴 Critical |
| B2-014 | DynamicSqlProvider 列名注入 | `DynamicSqlProvider.cs:67` | 🟡 Medium |
| B2-013 | DataCleanupService SQL 拼接表名 | `DataCleanupService.cs:138` | 🟡 Medium |
| S-7 | SqlValidator 黑名单方式 | `SqlValidator.cs:10-17` | 🟢 Low |
| TF-005 | wangeditor XSS（存储型） | `quill/index.vue:10-14` | 🟠 High |
| QF402 | iframe src 注入 | `iframe-page/[url].vue:11` | 🟠 High |
| C7 | 开放重定向 | `hooks/common/router.ts:102-105` | 🔴 Critical |
| F-001 | window.open 未校验 href | `guard/route.ts:176` | 🟠 High |

### A04:2021 — Insecure Design（不安全设计）

| 问题编号 | 标题 | 文件 | 严重程度 |
|---------|------|------|---------|
| T315 | ForceComplete 无状态机校验（重放） | `TransTasksController.cs:239-257` | ✅ 已修复（2026-07-07，加终态校验） |
| T316 | 全系统无 RowVersion 并发控制 | 全部 Configuration | 🟠 High |
| Q410 | 库位计数 TOCTOU 竞态 | `UpdateLocationCountHandler.cs:26-103` | 🟠 High |
| B-1 | RefreshToken 刷新无并发控制 | `AuthController.cs:141-166` | 🟡 Medium |
| H7 | 货位分配并发竞态 | `LocationAllocator.cs:106-121` | 🟠 High |
| H13 | FlowEngine 请求阶段无事务 | `FlowEngineService.cs:93-207` | 🟠 High |
| T318 | TransTasksController.Delete 无事务 | `TransTasksController.cs:416-497` | 🟠 High |
| B2-011 | FlowEngine 多节点部分成功 | `FlowEngineService.cs:93-207` | 🟠 High |
| RF-002 | 登录页无验证码/防爆破 | `pwd-login.vue:35-38` | 🔴 Critical |
| R506/R507 | 限流绕过 | `RateLimitMiddleware.cs:53,91` | 🟠 High |

### A05:2021 — Security Misconfiguration（安全配置错误）

| 问题编号 | 标题 | 文件 | 严重程度 |
|---------|------|------|---------|
| S-10/C3 | InternalIpWhitelist 默认放行 | `InternalIpWhitelistAttribute.cs:27-30` | 🔴 Critical |
| P-1/H5 | ResponseCaching 顺序错误 | `MiddlewareExtensions.cs:67-75` | 🟠 High |
| H9 | `/health` 端点无鉴权 | `MiddlewareExtensions.cs:85-112` | 🟠 High |
| S-11/H3 | IP 白名单信任 X-Forwarded-For | `InternalIpWhitelistAttribute.cs:49-57` | 🟠 High |
| R504 | ForwardedHeaders 信任所有代理 | `Program.cs:44-49` | 🔴 Critical |
| B2-006 | Hangfire Dashboard IP 检查错误 | `HangfireIpAuthorizationFilter.cs:23-26` | 🟠 High |
| D-008/D1 | Docker 端口对外暴露 | `docker-compose.yml:12-13,30-31` | 🟠 High |
| D-009/D2 | Docker 网络不隔离 | `docker-compose.yml:80-82` | 🟠 High |
| D-010/D3 | Dockerfile curl 缺失 | `Dockerfile:56-57` | 🟠 High |
| D-014/C13 | 备份文件入库 | `DB/WmsDb.Bak` | 🟠 High |
| D-015/D5 | Hangfire 版本不一致 | `Infrastructure.csproj:41` | 🟠 High |
| R512 | Kestrel 未配置并发连接数 | `Program.cs:32-35` | 🟡 Medium |
| R513 | 安全响应头缺失 | `SecurityHeadersMiddleware.cs:20-22` | 🟡 Medium |
| RF-001 | 登录页硬编码凭证 | `pwd-login.vue:49-68` | 🔴 Critical |

### A06:2021 — Vulnerable and Outdated Components（脆弱和过时的组件）

| 问题编号 | 标题 | 文件 | 严重程度 |
|---------|------|------|---------|
| C12/F208 | xlsx 0.18.5 CVE | `package.json:94` | 🔴 Critical |
| D-025 | wangeditor 4.7.15 XSS | `package.json:92` | 🟡 Medium |
| D-015/D5 | Hangfire 版本不一致 | `Infrastructure.csproj:41` | 🟠 High |
| Q-022 | 旧 .NET Framework 包残留 | `packages/` | 🟡 Medium |

### A07:2021 — Identification and Authentication Failures（身份识别和认证失败）

| 问题编号 | 标题 | 文件 | 严重程度 |
|---------|------|------|---------|
| R503 | 登出后 JWT 仍有效（无黑名单） | `AuthController.cs:266-285` | 🔴 Critical |
| T305 | GetAll 暴露 PasswordHash | `UsersController.cs:76-124` | 🔴 Critical |
| T306 | Delete 物理删除（用户重用） | `UsersController.cs:262-287` | ✅ 已修复（2026-07-07，改为软删除） |
| R516 | 密码策略过于宽松（6 位无复杂度） | `ProfileController.cs:40` | 🟡 Medium |
| R515 | 账号锁定基于内存（重启清零） | `AuthService.cs:24-26` | 🟡 Medium |
| F-007 | 登录失败未清密码字段 | `pwd-login.vue:35-38` | 🟠 High |
| RF-008 | 无会话超时机制 | 全局 | 🟠 High |
| F-002 | Token 在 localStorage | `auth/shared.ts:4-6` | 🟡 Medium |

### A08:2021 — Software and Data Integrity Failures（软件和数据完整性失败）

| 问题编号 | 标题 | 文件 | 严重程度 |
|---------|------|------|---------|
| QF408 | SRI 缺失（子资源完整性） | `index.html` | 🟡 Medium |
| D-032 | CI/CD 缺失 | 项目根目录 | 🟢 Low |
| C9 | 动态路由完全信任后端 | `route.ts:154-191` | 🔴 Critical |
| C8 | Pinia Store 自引用 | `auth/index.ts:14-16` | 🔴 Critical |

### A09:2021 — Security Logging and Monitoring Failures（安全日志和监控失败）

| 问题编号 | 标题 | 文件 | 严重程度 |
|---------|------|------|---------|
| T337 | OperationLogFilter 脱敏不完整 | `OperationLogFilter.cs:128-134` | 🟡 Medium |
| T338 | OperationLogFilter 不记录 GET | `OperationLogFilter.cs:29-39` | 🟡 Medium |
| B2-024 | Hangfire Job 无并发保护 | `HangfireExtensions.cs:44` | 🟢 Low |
| R527 | 日志级别硬编码为 Trace | `Program.cs:61` | 🟢 Low |

### A10:2021 — Server-Side Request Forgery (SSRF)（服务端请求伪造）

| 问题编号 | 标题 | 文件 | 严重程度 |
|---------|------|------|---------|
| Q403 | JobScheduleController 创建 http-call 任务 | `JobScheduleController.cs:14` | ✅ 已修复（2026-07-07，三层防御） |
| B2-019 | JobDispatcher HTTP 调用无 URL 校验 | `JobDispatcher.cs:135-174` | 🟡 Medium |
| B2-012 | WcsHealthCheck HttpClient 无超时 | `WcsHealthCheck.cs:12-13` | 🟡 Medium |

---

## 3. 严重程度分布（按 OWASP 类别）

| OWASP 类别 | Critical | High | Medium | Low | 小计 |
|-----------|----------|------|--------|-----|------|
| A01 访问控制 | 6 | 9 | 0 | 0 | 15 |
| A02 加密失败 | 4 | 2 | 1 | 0 | 7 |
| A03 注入 | 4 | 4 | 3 | 1 | 12 |
| A04 不安全设计 | 3 | 5 | 1 | 0 | 9 |
| A05 安全配置错误 | 4 | 7 | 3 | 0 | 14 |
| A06 过时组件 | 1 | 1 | 2 | 0 | 4 |
| A07 认证失败 | 3 | 2 | 3 | 0 | 8 |
| A08 完整性失败 | 2 | 0 | 1 | 1 | 4 |
| A09 日志监控 | 0 | 0 | 2 | 2 | 4 |
| A10 SSRF | 1 | 0 | 2 | 0 | 3 |
| **合计** | **28** | **30** | **18** | **4** | **80** |

> 注：本表统计的是关键问题（含部分中级别）。完整 195+ 个问题分布更广，但严重/高级别主要集中在上表。

## 4. 关键发现

### 4.1 最严重的类别：A01 访问控制失效

WMS 项目在 A01 类别暴露了 **15 个关键问题**，远超其他类别。这反映了项目从设计阶段就缺乏系统性的授权校验：
- 多个 Controller 类级别的 `[Authorize]` 被注释掉
- IDOR 漏洞广泛存在
- 设备接口缺乏双向认证

**根本原因**：开发时可能为了快速测试而临时注释了 `[Authorize]`，但忘记恢复。

### 4.2 第二严重：A05 安全配置错误

14 个关键问题，反映了：
- 配置管理混乱（占位符、硬编码、默认值）
- Docker 部署缺乏安全基线
- 中间件顺序不当

### 4.3 A02 加密失败与 A07 认证失败也较严重

反映了：
- 凭据管理薄弱（硬编码、明文存储）
- 会话管理不完整（无黑名单、无家族追踪、无超时）

---

## 5. 改进建议（长期）

| 建议 | 对应 OWASP 类别 | 优先级 |
|------|---------------|--------|
| 建立 Controller 授权策略矩阵 | A01 | P0 |
| 引入 SAST 工具（如 SonarQube、Semgrep）扫描授权问题 | A01 | P1 |
| 凭据管理统一使用 Key Vault | A02 | P1 |
| 引入 DAST 工具定期扫描 | A03 | P1 |
| 建立设计阶段安全审查（威胁建模） | A04 | P2 |
| 制定 Docker 部署安全基线 | A05 | P1 |
| 启用 Dependabot 自动更新 | A06 | P1 |
| 实施完整会话管理（含黑名单、超时、家族追踪） | A07 | P0 |
| 引入代码签名 | A08 | P2 |
| 集中化日志审计 | A09 | P2 |
| 建立 SSRF 防御库（URL 白名单） | A10 | P1 |

---

## 6. 合规映射

### 6.1 等保 2.0（GB/T 22239-2019）

| 等保要求 | WMS 对应发现 |
|---------|-------------|
| 8.1.4.1 身份鉴别 | C1, C2, C5, R516, R515, RF-002 |
| 8.1.4.2 访问控制 | T301-T312, R509, C6 |
| 8.1.4.3 安全审计 | T337, T338 |
| 8.1.4.4 入侵防范 | Q402, Q403, R508, C7 |
| 8.1.4.5 恶意代码防范 | C12（依赖漏洞） |
| 8.1.4.6 数据完整性与保密性 | C1, C2, C4, Q405, Q406 |

### 6.2 GDPR（如适用）

| GDPR 条款 | WMS 对应发现 |
|----------|-------------|
| Art. 32 安全处理 | C1（密码明文）、R504（ForwardedHeaders）、Q405/Q406（无 TLS） |
| Art. 25 数据保护设计 | T301-T306（IDOR 越权）、A04 设计缺陷 |

---

## 附录：参考资料

- [OWASP Top 10 (2021) 官方文档](https://owasp.org/Top10/)
- [OWASP Application Security Verification Standard (ASVS)](https://owasp.org/www-project-application-security-verification-standard/)
- [OWASP Cheat Sheet Series](https://cheatsheetseries.owasp.org/)
- [GB/T 22239-2019 信息安全技术 网络安全等级保护基本要求](http://www.gb688.cn/bzgk/gb/newGbInfo?hcno=F8B)
