# 凭据紧急旋转手册（Credential Rotation Runbook）

| 元信息 | 值 |
|--------|-----|
| 文档版本 | v1.0 |
| 生成日期 | 2026-07-06 |
| 适用范围 | Wms.Core（前端 Wms.Vue + 后端 Wms.Net8） |
| 文档状态 | 应急响应（PR1 合并后立即执行） |
| 关联文档 | [SECURITY-REMEDIATION-PLAN.md](./SECURITY-REMEDIATION-PLAN.md)、[CONFIGURATION-GUIDE.md](./CONFIGURATION-GUIDE.md) |

---

## ⚠️ 执行时机

**PR1（严重安全漏洞修复）合并并部署后**，必须立即执行本手册的步骤。

原因：硬编码在 git 历史中的凭据（SA 密码 `123456a`、JWT 占位符、杭可 `TSGX2ZZ`/`ZZ@123`、`admin/admin123`、Apifox Token）已经公开暴露，攻击者即使在新版本部署后仍可使用旧凭据攻击未旋转的资源。

---

## 1. 旋转优先级与时间线

| 优先级 | 任务 | 截止时间 | 负责角色 |
|--------|------|---------|---------|
| P0 | SQL Server SA 密码旋转 | 部署后 1 小时内 | DBA |
| P0 | JWT SecretKey 重新生成 | 部署后 1 小时内 | 后端运维 |
| P0 | 杭可设备凭据旋转 | 部署后 24 小时内 | 设备联络人 |
| P0 | 默认 admin 密码修改 | 部署后 30 分钟内 | 系统管理员 |
| P1 | Apifox Token 重新生成 | 部署后 24 小时内 | API 文档管理员 |
| P1 | Redis 密码设置（如未设） | 部署后 1 小时内 | 后端运维 |
| P1 | WCS HMAC ApiKey 生成 | 部署后 24 小时内 | 设备联络人 |
| P2 | Git 历史清理 | 部署后 1 周内 | DevOps |
| P2 | 数据库审计（检查异常登录） | 部署后 1 周内 | 安全工程师 |

---

## 2. SQL Server SA 密码旋转

### 2.1 生成强密码

```bash
# Linux/macOS
openssl rand -base64 24 | tr -d '/+=' | head -c 24

# Windows PowerShell
[System.Web.Security.Membership]::GeneratePassword(24, 8)
# 或
-join ((48..57) + (65..90) + (97..122) + (33,35,36,37,64) | Get-Random -Count 24 | ForEach-Object {[char]$_})
```

要求：
- 至少 16 字符（推荐 24）
- 包含大小写字母、数字、特殊字符
- 不含字典单词、人名、日期

### 2.2 修改 SQL Server SA 密码

```sql
-- 使用 sa 或 sysadmin 角色登录后执行
ALTER LOGIN sa WITH PASSWORD = '新生成的强密码';
ALTER LOGIN sa WITH CHECK_EXPIRATION = ON;
ALTER LOGIN sa WITH CHECK_POLICY = ON;

-- 验证
SELECT name, is_disabled, is_policy_checked, is_expiration_checked
FROM sys.sql_logins WHERE name = 'sa';
```

### 2.3 推荐做法：禁用 SA，使用专用账号

```sql
-- 1. 创建专用 WMS 账号（最小权限原则）
CREATE LOGIN wms_app WITH PASSWORD = '强密码', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
CREATE USER wms_app FOR LOGIN wms_app;

-- 2. 在 WmsDb 中授予必要权限
USE WmsDb;
ALTER ROLE db_datareader ADD MEMBER wms_app;
ALTER ROLE db_datawriter ADD MEMBER wms_app;
ALTER ROLE db_ddladmin ADD MEMBER wms_app;  -- EF Core 迁移需要
GRANT CREATE TABLE TO wms_app;
GRANT CREATE VIEW TO wms_app;

-- 3. 在 ctask 和 WmsLogsDb 中同样授权
USE ctask;
ALTER ROLE db_datareader ADD MEMBER wms_app;
ALTER ROLE db_datawriter ADD MEMBER wms_app;
ALTER ROLE db_ddladmin ADD MEMBER wms_app;

USE WmsLogsDb;
ALTER ROLE db_datareader ADD MEMBER wms_app;
ALTER ROLE db_datawriter ADD MEMBER wms_app;
ALTER ROLE db_ddladmin ADD MEMBER wms_app;

-- 4. 禁用 SA（生产环境推荐）
ALTER LOGIN sa DISABLE;
-- 或重命名 SA 账号
ALTER LOGIN sa WITH NAME = sysadmin_disabled;
```

### 2.4 更新应用连接字符串

```bash
# Development
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Data Source=...;User ID=wms_app;Password=新密码;Encrypt=True;TrustServerCertificate=False"

# Production（环境变量）
[System.Environment]::SetEnvironmentVariable('ConnectionStrings__DefaultConnection', '...', 'Machine')
```

### 2.5 验证

```bash
# 旧密码应失败
sqlcmd -S ... -U sa -P '123456a' -Q "SELECT 1"
# 应返回：Login failed

# 新密码应成功
sqlcmd -S ... -U wms_app -P '新密码' -Q "SELECT @@VERSION"
```

---

## 3. JWT SecretKey 重新生成

### 3.1 生成新密钥

```bash
# 生成 32 字节 Base64 密钥（256-bit，符合 HS256 要求）
openssl rand -base64 32

# 或使用 .NET
dotnet dev-certs https --help  # 仅展示工具
# 或 PowerShell
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))
```

### 3.2 更新配置

```bash
# Development
dotnet user-secrets set "Jwt:SecretKey" "新生成的密钥"

# Production
[System.Environment]::SetEnvironmentVariable('Jwt__SecretKey', '新密钥', 'Machine')
```

### 3.3 重启服务

```bash
# Windows 服务
Restart-Service WmsApi

# Linux systemd
sudo systemctl restart wms-api

# Docker
docker-compose restart wms-api
```

### 3.4 影响范围

⚠️ **所有用户需要重新登录**：
- 所有现有 JWT 立即失效（签名密钥改变）
- 所有 RefreshToken 需要重新颁发（用户必须用密码重新登录）

### 3.5 验证

```bash
# 旧 token 应失败
curl -H "Authorization: Bearer 旧token" https://api.example.com/api/v1/auth/me
# 应返回 401 Unauthorized

# 新登录应成功
curl -X POST https://api.example.com/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"admin","password":"新密码"}'
# 应返回新 token
```

---

## 4. 杭可设备凭据旋转

### 4.1 联系杭可设备方

**邮件/电话联系杭可设备供应商**，请求：
1. 修改对接账号密码（提供新密码给设备方配置）
2. 或申请新的对接账号

### 4.2 在 WMS 中更新配置

```bash
# Development
dotnet user-secrets set "HangKe:UserName" "新杭可用户名"
dotnet user-secrets set "HangKe:Password" "新杭可密码"

# Production
[System.Environment]::SetEnvironmentVariable('HangKe__UserName', '新用户名', 'Machine')
[System.Environment]::SetEnvironmentVariable('HangKe__Password', '新密码', 'Machine')

# 重启
Restart-Service WmsApi
```

### 4.3 验证

```bash
# 调用杭可相关接口测试
curl -X POST https://api.example.com/api/v1/hangke/sync -H "Authorization: Bearer ..."
# 应成功同步，无认证错误

# 检查日志
Get-Content C:\Logs\wms-api-*.log | Select-String "杭可"
```

---

## 5. 默认 admin 密码修改

### 5.1 通过 API 修改（推荐）

```bash
# 1. 使用 admin/admin123 登录（如还是默认密码）
curl -X POST https://api.example.com/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"admin","password":"admin123"}'

# 2. 使用返回的 token 修改密码
curl -X POST https://api.example.com/api/v1/profile/change-password \
  -H "Authorization: Bearer ..." \
  -H "Content-Type: application/json" \
  -d '{"oldPassword":"admin123","newPassword":"新强密码"}'
```

### 5.2 或通过 SQL（如 API 不可用）

```sql
-- 1. 获取 admin 用户的盐
SELECT UserName, PasswordHash, PasswordSalt FROM [User] WHERE UserName = 'admin';

-- 2. 使用 BCrypt 生成新密码哈希
-- （需要在外部工具中执行，如 Node.js 或 .NET 控制台）
-- Node.js 示例：
-- node -e "console.log(require('bcryptjs').hashSync('新密码', 10))"

-- 3. 更新密码
UPDATE [User] SET PasswordHash = '新生成的BCrypt哈希', PasswordSalt = NULL WHERE UserName = 'admin';
```

### 5.3 验证

```bash
# 旧密码应失败
curl -X POST https://api.example.com/api/v1/auth/login \
  -d '{"userName":"admin","password":"admin123"}'
# 应返回 401

# 新密码应成功
curl -X POST https://api.example.com/api/v1/auth/login \
  -d '{"userName":"admin","password":"新密码"}'
```

---

## 6. Apifox Token 重新生成

### 6.1 在 Apifox 中操作

1. 登录 Apifox
2. 进入项目设置 → API 访问令牌
3. 删除旧 Token
4. 创建新 Token
5. 复制新 Token

### 6.2 更新前端配置

```bash
# 仅 Development 需要
# 编辑 Wms.Vue/.env.development
echo "VITE_APIFOX_TOKEN=新Token" >> Wms.Vue/.env.development
```

### 6.3 验证

```bash
# 启动前端开发服务器
cd Wms.Vue
pnpm dev

# Apifox 请求应成功（在浏览器 Network 中检查 apifoxToken 头）
```

---

## 7. Redis 密码设置（如未设）

### 7.1 修改 Redis 配置

```bash
# 编辑 redis.conf
requirepass 你的强Redis密码
# 推荐启用 TLS（参考 Redis 文档）

# 重启 Redis
sudo systemctl restart redis
# 或 Docker
docker-compose restart redis
```

### 7.2 更新应用配置

```bash
# Development
dotnet user-secrets set "Redis:ConnectionString" "localhost:6789,password=新密码,ssl=False,allowAdmin=True"

# Production（启用 TLS）
[System.Environment]::SetEnvironmentVariable('Redis__ConnectionString', 'redis:6379,password=新密码,ssl=True', 'Machine')
```

### 7.3 验证

```bash
# 旧的无密码连接应失败
redis-cli -h localhost -p 6789 ping
# 应返回 NOAUTH

# 新密码应成功
redis-cli -h localhost -p 6789 -a 新密码 ping
# 应返回 PONG
```

---

## 8. WCS HMAC ApiKey 生成

### 8.1 生成 ApiKey

```bash
# 生成 32 字符的随机 ApiKey
openssl rand -hex 24
```

### 8.2 更新配置

```bash
# Development
dotnet user-secrets set "Wcs:ApiKey" "新生成的ApiKey"

# Production
[System.Environment]::SetEnvironmentVariable('Wcs__ApiKey', '新ApiKey', 'Machine')
```

### 8.3 通知 WCS 设备方

将 ApiKey 通过安全渠道（加密邮件、密钥管理系统）发送给 WCS 集成商，要求其在 WCS 端配置：
- 所有请求添加 `X-Wcs-Api-Key: 新ApiKey`
- 所有请求添加 `X-Wcs-Signature: HMACSHA256(ApiKey, body + timestamp)`
- 所有请求添加 `X-Wcs-Timestamp: 当前时间戳`

### 8.4 验证

```bash
# 无签名的请求应失败
curl -X POST https://api.example.com/api/wcs/inbound -d '{...}'
# 应返回 401

# 正确签名的请求应成功（参考 WcsAuthAttribute 文档）
```

---

## 9. Git 历史清理

### 9.1 评估泄露范围

```bash
# 检查 git 历史中的所有敏感字符串
git log --all -p | grep -E "(Password=123456|TSGX2ZZ|ZZ@123|admin123|CHANGE_ME_USE|XL299LiMEDZ0H5h3A29PxwQXdMJqWyY2)" | head -100

# 查看具体在哪些提交中
git log --all -S "123456a" --source --remotes --oneline
git log --all -S "TSGX2ZZ" --source --remotes --oneline
git log --all -S "admin123" --source --remotes --oneline
```

### 9.2 评估外部暴露

回答以下问题：
- [ ] 此 git 仓库是否曾经推送到公开仓库（GitHub public、Gitee）？
- [ ] 是否有非核心团队成员克隆过此仓库？
- [ ] 是否有 CI/CD 工具或第三方服务（如 SonarQube、Snyk）曾经访问此仓库？
- [ ] 是否有 fork 或 mirror？

如**有任何一项为是**，按**数据泄露事件**响应：
1. 立即通知安全负责人和管理层
2. 假设所有硬编码凭据已被攻击者获取
3. 在 24 小时内完成 P0 凭据旋转
4. 监控系统日志，检查是否有未授权访问痕迹
5. 视情况向相关监管机构报告

### 9.3 Git 历史清理（可选但推荐）

> ⚠️ **警告**：git 历史重写是破坏性操作，需团队协调。

#### 方法 1：使用 git-filter-repo（推荐）

```bash
# 安装
pip install git-filter-repo

# 备份当前仓库
cp -r wms-core wms-core-backup

# 创建密码替换文件
cat > passwords.txt << EOF
123456a==>REDACTED_PASSWORD
TSGX2ZZ==>REDACTED_USER
ZZ@123==>REDACTED_PASSWORD
admin123==>REDACTED_PASSWORD
CHANGE_ME_USE_USER_SECRETS_OR_ENVIRONMENT_VARIABLE_32_CHARS_MIN==>REDACTED_JWT_KEY
XL299LiMEDZ0H5h3A29PxwQXdMJqWyY2==>REDACTED_APIFOX_TOKEN
EOF

# 执行替换
git filter-repo --replace-text passwords.txt

# 强制推送（需团队协调，所有人重新克隆）
git push --force origin --all
git push --force origin --tags
```

#### 方法 2：使用 BFG Repo-Cleaner

```bash
# 下载 BFG（https://rtyley.github.io/bfg-repo-cleaner/）
java -jar bfg.jar --replace-text passwords.txt wms-core.git

cd wms-core.git
git reflog expire --expire=now --all
git gc --prune=now --aggressive
git push --force
```

### 9.4 通知所有仓库用户

```
主题：【紧急】WMS 仓库历史清理，请重新克隆

大家好，

WMS 项目仓库已进行历史清理（移除敏感凭据）。
所有现有克隆必须重新克隆，旧的提交历史将失效。

操作步骤：
1. 删除本地仓库：rm -rf wms-core
2. 重新克隆：git clone <repo-url>
3. 重新配置 user-secrets（参考 CONFIGURATION-GUIDE.md）

如有疑问，请联系 DevOps。

DevOps
```

---

## 10. 数据库审计

### 10.1 检查异常登录

```sql
-- 检查最近 30 天的登录失败记录
SELECT *
FROM sys.fn_trace_gettable(
  (SELECT REVERSE(SUBSTRING(REVERSE(path), CHARINDEX('\', REVERSE(path)), 128)) + 'log.trc'
   FROM sys.traces WHERE is_default = 1), default)
WHERE EventClass = 20  -- Audit Login Failed
  AND StartTime > DATEADD(day, -30, GETUTCDATE())
ORDER BY StartTime DESC;

-- 检查 SA 账户的最近活动
SELECT *
FROM sys.fn_trace_gettable(
  (SELECT path FROM sys.traces WHERE is_default = 1), default)
WHERE LoginName = 'sa'
  AND StartTime > DATEADD(day, -30, GETUTCDATE())
ORDER BY StartTime DESC;
```

### 10.2 检查异常数据访问

```sql
-- 检查用户表的所有查询（需要启用查询审计）
SELECT TOP 100 *
FROM WmsLogsDb.dbo.SystemLogs
WHERE RequestPath LIKE '%/api/v1/users%'
  AND LogTime > DATEADD(day, -30, GETUTCDATE())
ORDER BY LogTime DESC;

-- 检查非常规时间的 admin 操作
SELECT *
FROM WmsLogsDb.dbo.SystemLogs
WHERE UserName = 'admin'
  AND DATEPART(HOUR, LogTime) NOT IN (8,9,10,11,12,13,14,15,16,17,18)  -- 非工作时间
  AND LogTime > DATEADD(day, -90, GETUTCDATE());
```

### 10.3 检查数据导出记录

```sql
-- 检查导出任务（可能含敏感数据外泄）
SELECT UserId, ReportCode, Status, CreatedAt
FROM WmsDb.dbo.ReportExportTasks
WHERE Status = 'Success'
  AND CreatedAt > DATEADD(day, -30, GETUTCDATE())
ORDER BY CreatedAt DESC;

-- 重点检查非 admin 用户导出的报表
SELECT t.UserId, t.ReportCode, t.CreatedAt, u.UserName, u.Role
FROM WmsDb.dbo.ReportExportTasks t
JOIN WmsDb.dbo.[User] u ON t.UserId = u.Id
WHERE t.Status = 'Success'
  AND t.CreatedAt > DATEADD(day, -30, GETUTCDATE())
  AND u.Role != 'Admin'
ORDER BY t.CreatedAt DESC;
```

---

## 11. 检查清单（Checklist）

完成所有项后，勾选确认：

### P0 紧急（1 小时内）
- [ ] SQL Server SA 密码已旋转（或禁用 SA + 创建 wms_app 账号）
- [ ] 应用连接字符串已更新为新密码
- [ ] JWT SecretKey 已重新生成
- [ ] 应用已重启，新 JWT 密钥生效
- [ ] admin 默认密码已修改
- [ ] 旧 token / 旧密码验证失败

### P0 紧急（24 小时内）
- [ ] 杭可设备凭据已旋转（联系设备方）
- [ ] Apifox Token 已重新生成
- [ ] WCS HMAC ApiKey 已生成并通知设备方
- [ ] Redis 密码已设置（如之前无密码）

### P1 重要（1 周内）
- [ ] Git 历史泄露范围已评估
- [ ] 数据库审计已完成（检查异常登录/操作）
- [ ] 如有外部暴露，已启动数据泄露事件响应
- [ ] 所有团队成员已重新克隆仓库（如历史已清理）

### P2 后续
- [ ] .gitignore 已添加 `*.bak`、`.claude/`、`appsettings.*.json` 等
- [ ] 现有 .bak 文件已移到外部存储
- [ ] CI/CD 已配置依赖扫描（防止再次引入有漏洞依赖）
- [ ] 团队已进行安全培训（不提交密码到代码库）

---

## 12. 联系人

| 角色 | 姓名 | 联系方式 |
|------|------|---------|
| 安全负责人 | __填写__ | __填写__ |
| DBA | __填写__ | __填写__ |
| 后端运维 | __填写__ | __填写__ |
| 杭可设备联络 | __填写__ | __填写__ |
| WCS 设备联络 | __填写__ | __填写__ |
| DevOps | __填写__ | __填写__ |

---

## 附录 A：常见问题

### Q1：旋转 JWT SecretKey 后，移动端 APP 用户怎么办？

A：所有移动端用户会被强制登出。需要：
1. 在 APP 启动时检测 401，自动跳转登录页
2. 通过推送通知告知用户"系统升级，请重新登录"
3. 选择业务低谷期操作

### Q2：如果使用了 Redis 缓存 token 黑名单，旋转 JWT 时需要清理吗？

A：是的。旧 token 的 `jti` 已经无效（签名错误），但黑名单中的 jti 占用内存。执行 `FLUSHDB` 清空黑名单 DB（确保只清空专用 DB）。

### Q3：杭可设备方说他们没有"修改对接账号"的功能怎么办？

A：方案：
1. 在 WMS 端用一个 Map 层做新旧凭据映射（短期）
2. 要求设备方提供管理员账号，自行在设备系统中创建新账号
3. 或在 WMS 反向代理层做凭据转换（如 Nginx Lua 脚本）

### Q4：Git 历史清理后，团队成员的本地分支怎么办？

A：本地分支已经无法推送到清理后的仓库。所有成员必须：
1. 备份本地未提交的修改（如有）
2. 删除本地仓库：`rm -rf wms-core`
3. 重新克隆：`git clone <repo-url>`
4. 重新应用未提交的修改（如适用）

### Q5：如果发现已被攻击者利用旧凭据入侵怎么办？

A：立即启动**事件响应（IR）流程**：
1. **隔离**：断开受影响系统网络（保留内存和磁盘证据）
2. **取证**：保留所有日志、数据库快照、磁盘镜像
3. **通知**：法务、管理层、相关监管机构
4. **修复**：完成所有凭据旋转 + 清理后门 + 修复被利用的漏洞
5. **复盘**：编写事件报告，改进安全基线
