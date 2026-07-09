# 配置注入指南（Configuration Guide）

| 元信息 | 值 |
|--------|-----|
| 文档版本 | v1.0 |
| 生成日期 | 2026-07-06 |
| 适用范围 | Wms.Core（前端 Wms.Vue + 后端 Wms.Net8） |
| 文档状态 | 草案（待审核） |
| 关联文档 | [SECURITY-REMEDIATION-PLAN.md](./SECURITY-REMEDIATION-PLAN.md)、[CREDENTIAL-ROTATION-RUNBOOK.md](./CREDENTIAL-ROTATION-RUNBOOK.md) |

---

## 1. 背景

WMS 项目历史上有以下敏感配置直接硬编码在 `appsettings.json`：
- SQL Server SA 密码 `123456a`
- JWT SecretKey 占位符
- 杭可设备凭据 `TSGX2ZZ`/`ZZ@123`
- 默认管理员密码 `admin123`

本指南规范**敏感配置的标准注入机制**：环境变量 + dotnet user-secrets + Docker secrets。

## 2. 配置注入优先级

.NET 8 默认配置加载顺序（先加载的被后加载的覆盖）：

```
1. appsettings.json（必须，占位符）
2. appsettings.{Environment}.json（环境特定）
3. User Secrets（仅 Development，dotnet user-secrets）
4. 环境变量（EnvironmentVariables，前缀 WMS_）
5. 命令行参数（--key=value）
```

**最终生效规则**：序号大的覆盖序号小的。

## 3. 敏感配置项清单

### 3.1 后端必须注入的配置

| 配置 Key | 用途 | Development 来源 | Production 来源 |
|---------|------|------------------|----------------|
| `ConnectionStrings:DefaultConnection` | 主数据库 WmsDb | User Secrets | 环境变量 / Docker secrets |
| `ConnectionStrings:CtaskConnection` | 任务数据库 ctask | User Secrets | 环境变量 / Docker secrets |
| `ConnectionStrings:LogConnection` | 日志数据库 WmsLogsDb | User Secrets | 环境变量 / Docker secrets |
| `Jwt:SecretKey` | JWT 签名密钥（≥32 字符） | User Secrets | 环境变量 / KMS |
| `HangKe:UserName` | 杭可设备用户名 | User Secrets | 环境变量 |
| `HangKe:Password` | 杭可设备密码 | User Secrets | 环境变量 |
| `Admin:InitialPassword` | 初始化管理员密码 | User Secrets | 环境变量（首次启动） |
| `Redis:ConnectionString` | Redis 连接（含密码、SSL） | User Secrets | 环境变量 |
| `ForwardedHeaders:KnownProxies` | 反向代理 IP 列表 | appsettings.Development | 环境变量 |
| `Wcs:AllowedIps` | WCS 设备 IP 白名单 | appsettings.Development | 环境变量 |
| `Wcs:ApiKey` | WCS HMAC 共享密钥 | User Secrets | 环境变量 |

### 3.2 前端必须注入的配置

| 环境变量 | 用途 | 文件 |
|---------|------|------|
| `VITE_SERVICE_BASE_URL` | 后端 API 地址 | `.env` / `.env.prod` |
| `VITE_APIFOX_TOKEN` | Apifox 调试 token（仅开发） | `.env.development` |
| `VITE_SOURCE_MAP` | 生产 sourcemap 开关（必须 N） | `.env.prod` |
| `VITE_HTTP_PROXY` | 开发 N 代理 | `.env` |

## 4. Development 配置（User Secrets）

### 4.1 初始化

```bash
cd f:/Project/Wms.Core/Wms.Net8/src/Wms.Core.WebApi

# 初始化 user-secrets（仅首次）
dotnet user-secrets init

# 设置所有敏感配置
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Data Source=127.0.0.1;Initial Catalog=WmsDb;User ID=sa;Password=你的强密码;Encrypt=True;TrustServerCertificate=True"
dotnet user-secrets set "ConnectionStrings:CtaskConnection" "Data Source=127.0.0.1;Initial Catalog=ctask;User ID=sa;Password=你的强密码;Encrypt=True;TrustServerCertificate=True"
dotnet user-secrets set "ConnectionStrings:LogConnection" "Data Source=127.0.0.1;Initial Catalog=WmsLogsDb;User ID=sa;Password=你的强密码;Encrypt=True;TrustServerCertificate=True"

# JWT 密钥（使用 OpenSSL 生成）
$jwtKey = openssl rand -base64 32
dotnet user-secrets set "Jwt:SecretKey" $jwtKey

# 杭可凭据
dotnet user-secrets set "HangKe:UserName" "你的杭可用户名"
dotnet user-secrets set "HangKe:Password" "你的杭可密码"

# 管理员初始密码
dotnet user-secrets set "Admin:InitialPassword" "你的管理员强密码"

# Redis（如启用）
dotnet user-secrets set "Redis:ConnectionString" "localhost:6789,password=你的Redis密码,ssl=False"

# WCS HMAC 密钥
dotnet user-secrets set "Wcs:ApiKey" "你的WCS共享密钥"

# 验证
dotnet user-secrets list
```

### 4.2 User Secrets 存储位置

User Secrets 存储在用户目录下（不入版本库）：
- Windows：`%APPDATA%\Microsoft\UserSecrets\{userSecretsId}\secrets.json`
- Linux/macOS：`~/.microsoft/usersecrets/{userSecretsId}/secrets.json`

`{userSecretsId}` 在 `.csproj` 中定义：
```xml
<PropertyGroup>
  <UserSecretsId>your-unique-guid</UserSecretsId>
</PropertyGroup>
```

## 5. Production 配置（环境变量）

### 5.1 Windows 服务 / IIS 部署

#### PowerShell 设置环境变量（永久）

```powershell
[System.Environment]::SetEnvironmentVariable('ConnectionStrings__DefaultConnection', '...', 'Machine')
[System.Environment]::SetEnvironmentVariable('Jwt__SecretKey', '...', 'Machine')
[System.Environment]::SetEnvironmentVariable('HangKe__UserName', '...', 'Machine')
[System.Environment]::SetEnvironmentVariable('HangKe__Password', '...', 'Machine')
[System.Environment]::SetEnvironmentVariable('Admin__InitialPassword', '...', 'Machine')
[System.Environment]::SetEnvironmentVariable('Redis__ConnectionString', '...', 'Machine')
[System.Environment]::SetEnvironmentVariable('ForwardedHeaders__KnownProxies__0', '反向代理IP', 'Machine')
[System.Environment]::SetEnvironmentVariable('Wcs__AllowedIps__0', 'WCS设备IP', 'Machine')
[System.Environment]::SetEnvironmentVariable('Wcs__ApiKey', '...', 'Machine')

# 重启服务使环境变量生效
Restart-Service WmsApi  # 或 iisreset
```

> **注意**：环境变量中 `:` 用 `__`（双下划线）替代，因为 `:` 在某些环境无效。

### 5.2 Linux / Kubernetes 部署

#### systemd 服务

`/etc/systemd/system/wms-api.service`：
```ini
[Service]
Environment=ConnectionStrings__DefaultConnection=...
Environment=ConnectionStrings__CtaskConnection=...
Environment=ConnectionStrings__LogConnection=...
Environment=Jwt__SecretKey=...
Environment=HangKe__UserName=...
Environment=HangKe__Password=...
Environment=Admin__InitialPassword=...
Environment=Redis__ConnectionString=...
Environment=ForwardedHeaders__KnownProxies__0=反向代理IP
Environment=Wcs__AllowedIps__0=WCS设备IP
Environment=Wcs__ApiKey=...
Environment=ASPNETCORE_ENVIRONMENT=Production
ExecStart=/usr/bin/dotnet /opt/wms/Wms.Core.WebApi.dll
```

#### Kubernetes Secret + Deployment

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: wms-secrets
type: Opaque
stringData:
  ConnectionStrings__DefaultConnection: "..."
  ConnectionStrings__CtaskConnection: "..."
  ConnectionStrings__LogConnection: "..."
  Jwt__SecretKey: "..."
  HangKe__UserName: "..."
  HangKe__Password: "..."
  Admin__InitialPassword: "..."
  Redis__ConnectionString: "..."
  Wcs__ApiKey: "..."
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: wms-api
spec:
  template:
    spec:
      containers:
      - name: wms-api
        image: wms-api:latest
        envFrom:
        - secretRef:
            name: wms-secrets
```

### 5.3 Docker Compose 部署

`docker-compose.prod.yml`：
```yaml
services:
  wms-api:
    image: wms-api:latest
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Data Source=sqlserver;Initial Catalog=WmsDb;User ID=sa;Password=${DB_PASSWORD};Encrypt=True;TrustServerCertificate=False
      - Jwt__SecretKey=${JWT_SECRET_KEY}
      - HangKe__UserName=${HANGKE_USERNAME}
      - HangKe__Password=${HANGKE_PASSWORD}
      - Admin__InitialPassword=${ADMIN_PASSWORD}
      - Redis__ConnectionString=redis:6379,password=${REDIS_PASSWORD},ssl=True
      - ForwardedHeaders__KnownProxies__0=${REVERSE_PROXY_IP}
      - Wcs__AllowedIps__0=${WCS_IP}
      - Wcs__ApiKey=${WCS_API_KEY}
    depends_on:
      - sqlserver
      - redis
```

`.env`（同目录，不入库）：
```
DB_PASSWORD=你的强SA密码
JWT_SECRET_KEY=你的JWT密钥
HANGKE_USERNAME=你的杭可用户名
HANGKE_PASSWORD=你的杭可密码
ADMIN_PASSWORD=你的管理员密码
REDIS_PASSWORD=你的Redis密码
REVERSE_PROXY_IP=10.0.0.1
WCS_IP=10.0.0.100
WCS_API_KEY=你的WCS共享密钥
```

### 5.4 Docker Secrets（更安全）

```yaml
services:
  wms-api:
    secrets:
      - db_password
      - jwt_secret
      - hangke_password

secrets:
  db_password:
    file: ./secrets/db_password.txt
  jwt_secret:
    file: ./secrets/jwt_secret.txt
  hangke_password:
    file: ./secrets/hangke_password.txt
```

代码中读取：
```csharp
// Program.cs 中添加
builder.Configuration.AddKeyPerFile(directoryPath: "/run/secrets", optional: true);
```

## 6. 主 appsettings.json 占位符规范

修复后的 `appsettings.json` 应该是这样的（所有敏感值占位符化）：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=__DB_HOST__;Initial Catalog=WmsDb;User ID=__DB_USER__;Password=__SET_VIA_ENV_OR_SECRETS__;Encrypt=True;TrustServerCertificate=False",
    "CtaskConnection": "Data Source=__DB_HOST__;Initial Catalog=ctask;User ID=__DB_USER__;Password=__SET_VIA_ENV_OR_SECRETS__;Encrypt=True;TrustServerCertificate=False",
    "LogConnection": "Data Source=__DB_HOST__;Initial Catalog=WmsLogsDb;User ID=__DB_USER__;Password=__SET_VIA_ENV_OR_SECRETS__;Encrypt=True;TrustServerCertificate=False"
  },
  "Jwt": {
    "Issuer": "Wms.Core.WebApi",
    "Audience": "Wms.Client",
    "SecretKey": "",
    "ExpirationMinutes": 60,
    "RefreshExpirationDays": 7
  },
  "HangKe": {
    "UserName": "",
    "Password": ""
  },
  "Admin": {
    "InitialPassword": ""
  },
  "Redis": {
    "Enabled": false,
    "ConnectionString": ""
  },
  "ForwardedHeaders": {
    "KnownProxies": []
  },
  "Wcs": {
    "AllowedIps": [],
    "ApiKey": ""
  }
}
```

## 7. 配置类定义

### 7.1 HangKeClientOptions

`Wms.Core.WebApi/Configuration/HangKeClientOptions.cs`：
```csharp
public class HangKeClientOptions
{
    public const string SectionName = "HangKe";

    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrEmpty(UserName))
            throw new InvalidOperationException("HangKe:UserName 未配置");
        if (string.IsNullOrEmpty(Password))
            throw new InvalidOperationException("HangKe:Password 未配置");
    }
}
```

### 7.2 AdminOptions

```csharp
public class AdminOptions
{
    public const string SectionName = "Admin";

    public string InitialPassword { get; set; } = string.Empty;
}
```

### 7.3 WcsAuthOptions

```csharp
public class WcsAuthOptions
{
    public const string SectionName = "Wcs";

    public string[] AllowedIps { get; set; } = [];
    public string ApiKey { get; set; } = string.Empty;
}
```

### 7.4 注册到 DI 容器

`Program.cs`：
```csharp
builder.Services.Configure<HangKeClientOptions>(builder.Configuration.GetSection(HangKeClientOptions.SectionName));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));
builder.Services.Configure<WcsAuthOptions>(builder.Configuration.GetSection(WcsAuthOptions.SectionName));

// 启动时验证
var app = builder.Build();
app.Services.GetRequiredService<IOptions<HangKeClientOptions>>().Value.Validate();
```

## 8. SecureConfigurationExtensions 辅助方法

新建 `Wms.Core.WebApi/Configuration/SecureConfigurationExtensions.cs`：

```csharp
using Microsoft.Extensions.Configuration;

namespace Wms.Core.WebApi.Configuration;

public static class SecureConfigurationExtensions
{
    /// <summary>
    /// 安全获取连接字符串（优先环境变量）
    /// </summary>
    public static string GetSecureConnectionString(this IConfiguration config, string key)
    {
        var envKey = $"ConnectionStrings__{key}";
        var value = Environment.GetEnvironmentVariable(envKey)
                    ?? config[$"ConnectionStrings:{key}"];
        if (string.IsNullOrEmpty(value) || value.Contains("__SET_VIA_ENV_OR_SECRETS__"))
            throw new InvalidOperationException($"连接字符串 {key} 未配置（环境变量 {envKey} 或 User Secrets）");
        return value;
    }

    /// <summary>
    /// 校验 JWT 密钥安全
    /// </summary>
    public static void ValidateJwtSecret(this IConfiguration config)
    {
        var key = config["Jwt:SecretKey"];
        if (string.IsNullOrEmpty(key) || key.StartsWith("CHANGE_") || key.Length < 32)
            throw new InvalidOperationException(
                "Jwt:SecretKey 未正确配置。要求：≥32 字符，不能以 CHANGE_ 开头。" +
                "请通过环境变量 Jwt__SecretKey 或 dotnet user-secrets 注入。");
    }
}
```

## 9. Program.cs 修改

```csharp
var builder = WebApplication.CreateBuilder(args);

// 添加 User Secrets（仅 Development）
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

// 添加环境变量（前缀 WMS_）
builder.Configuration.AddEnvironmentVariables(prefix: "WMS_");

// 添加 Docker Secrets（如使用）
builder.Configuration.AddKeyPerFile(directoryPath: "/run/secrets", optional: true);

// ... 其他服务注册

// 启动前验证
builder.Configuration.ValidateJwtSecret();

var app = builder.Build();
// ...
```

## 10. 配置注入验证

### 10.1 启动验证

应用启动时，如缺失必填配置应立即失败（fail-fast），不允许进入运行态。

```bash
# Development 启动（应成功）
cd Wms.Net8/src/Wms.Core.WebApi
dotnet run

# 故意清空 User Secrets 测试（应启动失败）
dotnet user-secrets remove "Jwt:SecretKey"
dotnet run  # 应抛 "Jwt:SecretKey 未正确配置"
```

### 10.2 配置完整性检查

```bash
# 列出所有 user-secrets（Development）
dotnet user-secrets list

# 列出所有环境变量
# Linux
env | grep -E "(ConnectionStrings|Jwt|HangKe|Wcs)"
# Windows PowerShell
Get-ChildItem env: | Where-Object Name -Match "(ConnectionStrings|Jwt|HangKe|Wcs)"
```

### 10.3 配置不进入 Git 的验证

```bash
# 检查 git 历史中是否有密码泄露
git log --all -p | grep -E "(Password=123456|TSGX2ZZ|ZZ@123|admin123|CHANGE_ME)" | head -50

# 检查 .gitignore 是否排除敏感文件
cat .gitignore | grep -E "(appsettings|\.env|\.bak)"
```

## 11. 故障排查

### 11.1 启动异常 "Jwt:SecretKey 未正确配置"

**原因**：环境变量或 User Secrets 未设置。

**解决**：
```bash
# Development
dotnet user-secrets set "Jwt:SecretKey" "$(openssl rand -base64 32)"

# Production
export Jwt__SecretKey="你的密钥"
```

### 11.2 启动异常 "连接字符串 DefaultConnection 未配置"

**原因**：环境变量 `ConnectionStrings__DefaultConnection` 未设置。

**解决**：
```bash
# Linux
export ConnectionStrings__DefaultConnection="Data Source=...;Password=..."

# Windows PowerShell
[System.Environment]::SetEnvironmentVariable('ConnectionStrings__DefaultConnection', '...', 'Machine')
```

### 11.3 Docker 启动时环境变量未生效

**检查**：
```bash
docker exec -it wms-api env | grep -E "(ConnectionStrings|Jwt)"
```

**修复**：确认 `docker-compose.yml` 中 `environment:` 段正确引用了 `${VAR}` 占位符，且 `.env` 文件存在。

## 12. 配置轮换流程

### 12.1 JWT SecretKey 轮换

> ⚠️ **警告**：轮换 SecretKey 会导致所有现有 JWT 立即失效，所有用户需要重新登录。

```bash
# 1. 生成新密钥
NEW_KEY=$(openssl rand -base64 32)

# 2. 更新环境变量
[System.Environment]::SetEnvironmentVariable('Jwt__SecretKey', $NEW_KEY, 'Machine')

# 3. 清空 Redis 中的 JWT 黑名单（如启用）
redis-cli -a $REDIS_PASSWORD FLUSHDB  # 仅在专用 Redis DB 上执行

# 4. 重启服务
Restart-Service WmsApi

# 5. 通知所有用户重新登录
```

### 12.2 数据库密码轮换

详见：[CREDENTIAL-ROTATION-RUNBOOK.md](./CREDENTIAL-ROTATION-RUNBOOK.md)

---

## 附录：配置项完整模板

新建 `appsettings.json.template`：
```
# 复制此文件为 .env（Linux）或 set-env.ps1（Windows），填入实际值

# 数据库
DB_HOST=127.0.0.1
DB_USER=sa
DB_PASSWORD=你的强SA密码

# JWT（≥32 字符，使用 openssl rand -base64 32 生成）
JWT_SECRET_KEY=

# 杭可设备
HANGKE_USERNAME=
HANGKE_PASSWORD=

# 管理员初始密码（≥12 字符）
ADMIN_PASSWORD=

# Redis
REDIS_PASSWORD=

# 网络
REVERSE_PROXY_IP=
WCS_IP=

# WCS HMAC
WCS_API_KEY=
```
