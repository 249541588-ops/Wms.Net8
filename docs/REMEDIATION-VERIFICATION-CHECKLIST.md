# 修复验证清单（Remediation Verification Checklist）

| 元信息 | 值 |
|--------|-----|
| 文档版本 | v1.0 |
| 生成日期 | 2026-07-06 |
| 适用范围 | Wms.Core（前端 Wms.Vue + 后端 Wms.Net8） |
| 文档状态 | 待执行 |
| 关联文档 | [SECURITY-REMEDIATION-PLAN.md](./SECURITY-REMEDIATION-PLAN.md) |

---

## 使用说明

1. 在每个 PR 部署到测试/生产环境后，逐项执行本清单中的验证
2. 在 `[ ]` 中填写 `x`（如 `- [x]`）表示通过
3. 失败项立即报告，并阻塞下一个 PR 的合并
4. 完整的清单可作为发布报告归档

---

## 1. 编译验证

### 1.1 后端编译
- [ ] `dotnet build Wms.Net8/Wms.Net8.sln` → 0 error
- [ ] 警告数不超过基线（基线：__填写__）
- [ ] 所有项目（Domain/Application/Infrastructure/WebApi）均编译成功

### 1.2 前端构建
- [ ] `cd Wms.Vue && pnpm build` → 0 error
- [ ] 构建产物大小不超过基线（基线：__填写 KB__）
- [ ] `dist/` 目录中无 sourcemap 文件（除非显式启用）

### 1.3 Lint
- [ ] `cd Wms.Vue && pnpm lint` → 0 error，warning 数显著减少
- [ ] 后端无新增 EditorConfig 警告

---

## 2. 单元测试

- [ ] `dotnet test Wms.Net8/Wms.Net8.sln` → 全部通过
- [ ] 后端测试覆盖率 ≥ __填写__%（基线：0%）
- [ ] `cd Wms.Vue && pnpm test` → 全部通过（如配置）
- [ ] 新增的 12 个单测文件全部通过：
  - [ ] TokenServiceTests
  - [ ] RefreshTokenRepositoryTests
  - [ ] InternalIpWhitelistAttributeTests
  - [ ] HangfireIpAuthorizationFilterTests
  - [ ] LocationAllocationEngineTests
  - [ ] FlowEngineServiceTests
  - [ ] UsersControllerAuthTests（第三轮）
  - [ ] TransTasksControllerTests（第三轮）
  - [ ] RowVersionTests（第三轮）
  - [ ] ReportServiceSortTests（第四轮）
  - [ ] JobScheduleAuthTests（第四轮）
  - [ ] HangKeClientXxeTests（第四轮）
  - [ ] RefreshTokenFamilyTests（第五轮）
  - [ ] JwtBlacklistTests（第五轮）
  - [ ] ExportSanitizeTests（第五轮）
  - [ ] ReportExportIdorTests（第五轮）
  - [ ] WcsAuthAttributeTests（第五轮）
  - [ ] router.spec.ts（前端）
  - [ ] shared.spec.ts（前端）
  - [ ] unitloads.spec.ts（前端）

---

## 3. 启动与配置验证

- [ ] 配置 user-secrets 后启动 → 不抛 `CHANGE_ME` 异常
- [ ] 未配置 `Wcs:AllowedIps` 时启动 → 不影响（仅请求时返回 503）
- [ ] 未配置 `Jwt:SecretKey` 时启动 → **启动失败并提示**
- [ ] 未配置 `HangKe:UserName/Password` 时启动 → **启动失败并提示**
- [ ] 未配置 `ConnectionStrings` 时启动 → **启动失败并提示**

---

## 4. 认证与授权验证

### 4.1 JWT 登录
- [ ] Swagger 登录（admin/正确密码）→ 200 OK + 返回 token
- [ ] 错误密码 → 401 + 统一错误信息（不暴露用户存在性）
- [ ] 不存在的用户名 → 401 + 与错误密码相同的信息（防用户枚举）

### 4.2 Token 刷新（修复 F2 后）
- [ ] 等 token 过期 → 自动刷新成功
- [ ] 多个并发 401 → 仅触发一次 refresh 请求（修复字段名错配后）

### 4.3 IDOR 越权防护（修复 T301-T306 后）
- [ ] 普通用户 token 调 `GET /api/v1/users` → 403 Forbidden
- [ ] 普通用户 token 调 `DELETE /api/v1/users/{id}` → 403
- [ ] 普通用户 token 调 `POST /api/v1/users/{id}/reset-password` → 403
- [ ] Admin token 调上述接口 → 200/204
- [ ] `GET /api/v1/users` 响应中**不含** `passwordHash` 字段
- [ ] T306 软删除：`DELETE /api/v1/users/{id}` 后数据库仍保留该 User 记录，且 `IsActive=false`、`DeletedAt` 非空
- [ ] T306 软删除：已软删除用户不在 `GET /api/v1/users` 列表中出现，`GET /api/v1/users/{id}` 返回 404
- [ ] T306 软删除：已软删除用户尝试登录 → 失败（账户已禁用）
- [ ] T306 软删除：删除内置用户（`IsBuiltIn=true`，如 `admin`）→ 返回 409
- [ ] T306 软删除：新建用户 `IsActive` 默认为 `true`

### 4.4 ForceComplete 状态机（修复 T315 后）
- [ ] 已完成的任务（`WcsState=completed`）再次 ForceComplete → 响应体 `code="409"`，消息"任务已终态"
- [ ] 已取消的任务（`WcsState=cancelled`）再次 ForceCancel → 响应体 `code="409"`
- [ ] WCS 拒绝的任务（`WcsState=refused`）再次 ForceComplete/ForceCancel → 响应体 `code="409"`
- [ ] 已归档的任务（`WmsState=archived`）再次 ForceComplete/ForceCancel → 响应体 `code="409"`，消息"任务已归档"
- [ ] 未发送 WCS 的任务（`WasSentToWcs != true`）不触发状态校验，可正常强制完成/取消
- [ ] 服务端日志中可见 `[ForceComplete] 拒绝执行：任务已终态 ...` Warning 记录
- [ ] 状态校验通过后，业务逻辑（扣库存、归档、ctask 更新）仅执行一次
- [ ] 并发 ForceComplete 同一任务 → 至多一次成功，另一次返回 `code="409"` 或业务异常

### 4.5 RefreshToken 家族追踪（修复 R502 后）
- [ ] 使用已吊销的 refresh token 再次刷新 → 整个 FamilyId 被吊销，用户被强制下线
- [ ] 同一家族的活跃 token 在重用事件后全部失效

### 4.6 JWT 黑名单（修复 R503 后）
- [ ] Logout 后立即用旧 token 调 API → 401 Unauthorized
- [ ] 修改密码后当前 token 立即失效

### 4.7 其他 Controller 授权
- [ ] 普通用户调 `POST /api/v1/waste-batch-setting` → 403（修复 T307）
- [ ] 普通用户调 `DELETE /api/v1/menus/{id}` → 403（修复 T309）
- [ ] 普通用户调 `DELETE /api/v1/roles/{id}` → 403（修复 T310）
- [ ] 普通用户调 `POST /api/v1/sim-tool/force-move` → 403（修复 T311）
- [ ] 普通用户调 `POST /api/v1/flow/templates` → 403（修复 T312）
- [ ] 普通用户调 `POST /api/v1/job-schedule` → 403（修复 Q403 类级 [Authorize(Roles="Admin")]）
- [ ] 匿名（无 token）调 `POST /api/v1/job-schedule` → 401（修复 Q403）
- [ ] Admin 创建 `jobType="http-call"` + `apiUrl="http://evil.com/x"` → 响应体 `code="400"`（修复 Q403 URL 白名单）
- [ ] Admin 创建 `apiUrl="/api/v1/foo"` + Headers 含 `{"Authorization":"Bearer xxx"}` → 响应体 `code="400"`，错误含"禁止的请求头"（修复 Q403 Headers 黑名单）
- [ ] Admin 创建 `apiUrl="/api/v1/foo"` + Headers 含 `{"X-Custom":"v"}` → 200 正常（合法 Header 通过）
- [ ] 已存在的非法 http-call 任务（apiUrl 是绝对 URL）执行时被 JobDispatcher 拒绝，日志含 `ApiUrl 校验失败`（防御纵深）
- [ ] 模拟 http-call 目标端点返回 302 → HttpClient 不跟随重定向，原样返回 302（修复 Q403 重定向 SSRF）
- [ ] `internal` 模式任务（如 data-cleanup）不受影响，正常调度执行（兼容性回归）

---

## 5. 安全漏洞验证

### 5.1 开放重定向
- [ ] 访问 `/login?redirect=https://evil.com` → 不跳转外部 URL（修复 C7）
- [ ] 访问 `/login?redirect=//evil.com` → 不跳转（修复 C7）
- [ ] 访问 `/login?redirect=/dashboard` → 正常跳转（合法路径）

### 5.2 动态路由注入（修复 C9）
- [ ] 后端返回 `path: "javascript:alert(1)"` → 该路由被拒绝
- [ ] 后端返回 `viewKey: "malicious"` → 该路由被跳过

### 5.3 SQL 注入（修复 Q402）
- [ ] `POST /api/v1/reports/{reportCode}/data` body 含 `sortField="(SELECT CASE WHEN 1=1 THEN name ELSE '' END)"` → 响应体 `code="400"`，错误消息含"排序字段包含非法字符"
- [ ] `sortField="name;DROP TABLE Users--"` → 响应体 `code="400"`（分号、空格、注释被拒）
- [ ] `sortField="name"` + `sortDirection="ASC"` → 200 正常
- [ ] `sortField="t.CreatedTime"` → 200 正常（schema.table.column 链式合法）
- [ ] `sortField=null` 或空字符串 → 200，回退到 `config.DefaultSort`
- [ ] 服务端 Warning 日志包含 `报表查询参数非法` 与 SortField 值
- [ ] 仓储路径（如 `IRepository<T>.GetAllAsync` 接收 `orderBy`）仍正常工作（多列、单列、非法回退到 "Id ASC"）

### 5.4 XXE 防御（修复 Q401）
- [ ] Mock 杭可设备返回 `<!DOCTYPE foo [<!ENTITY xxe SYSTEM "file:///etc/passwd">]><foo>&xxe;</foo>` → 不被解析
- [ ] 正常 SOAP 响应仍可解析

### 5.5 路径遍历（修复 Q409）
- [ ] DB 中 filePath 改为 `/etc/passwd` → DownloadExport 返回 403
- [ ] DB 中 filePath 改为 `../../../etc/passwd` → 403
- [ ] 合法 filePath 在导出目录内 → 200 正常下载

### 5.6 CSV/Excel 公式注入（修复 R508）
- [ ] 物料编码设为 `=cmd|'/c calc'!A1` → 导出后单元格以 `'` 开头
- [ ] 物料编码设为 `+1+cmd` → 同上
- [ ] 物料编码设为 `-1+cmd` → 同上
- [ ] 物料编码设为 `@SUM(A1:A2)` → 同上
- [ ] 正常物料编码（如 `MAT-001`）→ 不加前缀

### 5.7 报表导出 IDOR（修复 R509）
- [ ] 用户 A 用自己 token + B 的 taskId 调 download → 403
- [ ] 用户 A 用自己 token + 自己的 taskId 调 download → 200
- [ ] Admin 用任意 taskId → 200

### 5.8 文件上传安全
- [ ] 上传伪装为 `.jpg` 的可执行文件 → 被拒绝（Magic Number 校验）
- [ ] 上传超过 5MB 的图片 → 被拒绝
- [ ] 上传超过 10MB 的 Excel → 被拒绝

### 5.9 Excel 导出鉴权（修复 H1）
- [ ] 未登录调 `/api/upload/excel-export` → 401 Unauthorized
- [ ] 已登录用户调 → 200 OK

### 5.10 Hangfire 限制（修复 H8）
- [ ] 未授权 IP 访问 `/hangfire` → 403 Forbidden
- [ ] 授权 IP 访问 → Dashboard 正常显示

### 5.11 健康检查限制（修复 H9）
- [ ] 未认证访问 `/health` → 401
- [ ] 已认证访问 `/health` → 详细信息
- [ ] 未认证访问 `/healthz` → 200 + 简单状态（用于 LB）

### 5.12 货位分配并发（修复 H7、Q410）
- [ ] 并发 2 个请求分配同一货位 → 仅一个成功
- [ ] 并发 2 个入库请求 + InboundLimit=1 → 仅一个 200，另一个 409
- [ ] 同一 Unitload 被两个操作员同时 Update → 一个 200，一个 409（修复 T316）

### 5.13 SignalR 授权（修复 C6）
- [ ] 未携带 JWT 连接 `/hubs/wms` → 连接被拒（401）
- [ ] 携带有效 JWT 连接 → 连接成功
- [ ] 恶意客户端调用 `SendTaskUpdate` 方法 → 方法不存在或被拒绝

### 5.14 ForwardedHeaders（修复 R504）
- [ ] `curl -H "X-Forwarded-For: 127.0.0.1" /api/wcs/inbound` → 仍被拒绝
- [ ] 配置的实际代理 IP 请求 → 正常

### 5.15 WCS HMAC 认证（修复 R505）
- [ ] 无签名调 `/api/wcs/*` → 401
- [ ] 错误签名调 → 401
- [ ] 过期签名（>5 分钟）调 → 401
- [ ] 正确签名调 → 200

### 5.16 限流绕过防护（修复 R506）
- [ ] 登录接口加 `?_=1` `?_=2` 连发 20 次 → 第 11 次起 429
- [ ] 不带查询参数连发 → 同样限流

### 5.17 iframe 沙箱（修复 QF402）
- [ ] 访问 `/iframe-page/javascript:alert(1)` → 不执行 JS
- [ ] 访问 `/iframe-page/data:text/html,...` → 不加载
- [ ] 访问 `/iframe-page/https://valid-url.com` → 在 sandbox 中加载

---

## 6. 前端业务验证

### 6.1 登录页（修复 RF-001、RF-002、RF-006、RF-007）
- [ ] 构建后 `grep -r "123456" dist/` → 无匹配（移除硬编码凭证）
- [ ] 登录页密码字段有 `autocomplete="current-password"`
- [ ] 登录失败后密码字段被清空（用户名保留）
- [ ] 连续失败 3 次后出现验证码（修复 RF-002 后）

### 6.2 跨 Tab 同步（修复 RF-004）
- [ ] 打开 Tab A 和 Tab B 都登录
- [ ] Tab A 手动登出
- [ ] Tab B 自动跳转登录页（storage 事件触发）

### 6.3 会话超时（修复 RF-008）
- [ ] 登录后 30 分钟无操作
- [ ] 自动触发登出 + 跳转登录页

### 6.4 货载删除行（修复 TF308）
- [ ] 在货载编辑弹窗中选中第 2、4、6 行
- [ ] 点击删除选中行
- [ ] 验证实际删除的是第 2、4、6 行（而非错误行）

### 6.5 表单校验（修复 TF317）
- [ ] locations 新增/编辑：必填字段留空 → 提示必填
- [ ] materials 新增/编辑：必填字段留空 → 提示必填
- [ ] unitloads 新建：容器编码留空 → 提示必填
- [ ] 数值字段（容量、限制）输入负数 → 提示非法

### 6.6 状态切换二次确认（修复 TF301）
- [ ] 库位状态开关切换 → 弹出确认对话框
- [ ] 确认 → 提交；取消 → 不变更

### 6.7 列表分页竞态（修复 TF311）
- [ ] 快速翻页 5 次
- [ ] 最终显示的是最后一次请求的数据（非较早的）

### 6.8 wangeditor XSS（修复 RF-005）
- [ ] 在 wangeditor 中粘贴含 `<script>alert(1)</script>` 的内容
- [ ] 保存后查看其他页面渲染 → script 不执行

---

## 7. Docker 与部署验证

- [ ] `docker-compose up` → SQL Server/Redis/API 都健康
- [ ] `nmap localhost` → 1433（SQL Server）不暴露
- [ ] `nmap localhost` → 6379（Redis）不暴露
- [ ] `docker exec wms-api id` → 非 root（uid > 0）
- [ ] `curl http://localhost/healthz` → 200 OK
- [ ] `curl http://localhost/health` → 401（要求认证）
- [ ] Redis 健康检查命令正确（修复 D-018）：`docker exec wms-redis redis-cli -a $REDIS_PASSWORD ping` 返回 PONG

---

## 8. 配置与密钥验证

- [ ] `dotnet user-secrets list` → 含所有敏感配置
- [ ] `git log --all -p | grep "123456a"` → PR7 清理后无密码泄漏
- [ ] `git log --all -p | grep "TSGX2ZZ"` → 无匹配
- [ ] `git log --all -p | grep "XL299LiMEDZ0H5h3A29PxwQXdMJqWyY2"` → 无匹配（Apifox Token）
- [ ] 检查 `.gitignore` 包含：`*.bak`、`.claude/`、`appsettings.Development.json`、`appsettings.Production.json`
- [ ] 检查 `DB/WmsDb.Bak` 已移出代码库
- [ ] 检查 `appsettings.json` 中连接字符串含 `__SET_VIA_ENV_OR_SECRETS__` 占位符

---

## 9. 凭据旋转完成验证

详见 [CREDENTIAL-ROTATION-RUNBOOK.md](./CREDENTIAL-ROTATION-RUNBOOK.md)

- [ ] SQL Server SA 密码已旋转（或禁用 SA + 创建 wms_app）
- [ ] JWT SecretKey 已重新生成
- [ ] 杭可设备凭据已旋转
- [ ] admin 默认密码已修改
- [ ] Apifox Token 已重新生成
- [ ] Redis 密码已设置
- [ ] WCS HMAC ApiKey 已生成并通知设备方

---

## 10. 性能回归验证

### 10.1 关键接口性能
- [ ] 登录接口 P99 < 500ms（修复 H4 同步阻塞后应改善）
- [ ] 物料查询接口 P99 < __填写__ ms
- [ ] 货位分配接口 P99 < __填写__ ms
- [ ] 报表查询 P99 < __填写__ ms

### 10.2 并发性能
- [ ] 50 并发用户登录 → 全部成功，无 5xx
- [ ] 100 并发查询 → RPS 不下降超过 20%

### 10.3 EF Core 索引性能（修复 H10 后）
- [ ] `SELECT * FROM Unitload WHERE ContainerCode = 'X'` 执行计划使用索引
- [ ] `SELECT * FROM Material WHERE MaterialCode = 'X'` 执行计划使用索引

---

## 11. 监控与日志验证

- [ ] 日志中无密码泄露（搜索 `Password=` 在新日志中无明文）
- [ ] OperationLogFilter 脱敏覆盖：`password`、`token`、`secret`、`apiKey`、`hash`、`salt`
- [ ] NLog 输出正常（Infrastructure 和 WebApi 版本统一）
- [ ] 异常日志含完整堆栈（开发） / 仅通用消息（生产）
- [ ] GET 请求对敏感接口（Users/Materials/Unitloads）有审计日志

---

## 12. 文档与流程验证

- [ ] README.md 部署章节引用 [CONFIGURATION-GUIDE.md](./CONFIGURATION-GUIDE.md)
- [ ] API_DOCUMENTATION.md 移除 `admin/admin123` 测试凭据
- [ ] JWT-Testing-Guide.md 移除测试凭据
- [ ] 测试模板/UserLogin.txt 已删除或更新
- [ ] PROJECT_STATUS.md 含安全修复进度章节
- [ ] 团队成员已知晓新配置流程

---

## 验证结果汇总

| 阶段 | 总项数 | 通过 | 失败 | 通过率 |
|------|--------|------|------|--------|
| 1. 编译 | 7 | 0 | 0 | 0% |
| 2. 单元测试 | 23 | 0 | 0 | 0% |
| 3. 启动配置 | 5 | 0 | 0 | 0% |
| 4. 认证授权 | 18 | 0 | 0 | 0% |
| 5. 安全漏洞 | 17 | 0 | 0 | 0% |
| 6. 前端业务 | 8 | 0 | 0 | 0% |
| 7. Docker | 7 | 0 | 0 | 0% |
| 8. 配置密钥 | 7 | 0 | 0 | 0% |
| 9. 凭据旋转 | 7 | 0 | 0 | 0% |
| 10. 性能 | 5 | 0 | 0 | 0% |
| 11. 监控日志 | 5 | 0 | 0 | 0% |
| 12. 文档 | 6 | 0 | 0 | 0% |
| **合计** | **115** | **0** | **0** | **0%** |

**执行人**：______________
**执行日期**：______________
**审核人**：______________
**审核结果**：⬜ 通过 ⬜ 需修复 ⬜ 阻塞发布
