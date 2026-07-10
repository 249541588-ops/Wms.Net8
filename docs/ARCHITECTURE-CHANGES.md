# 架构变更记录（Architecture Changes）

| 元信息 | 值 |
|--------|-----|
| 文档版本 | v1.0 |
| 生成日期 | 2026-07-06 |
| 适用范围 | Wms.Core（前端 Wms.Vue + 后端 Wms.Net8） |
| 文档状态 | 草案（待审核） |
| 关联文档 | [SECURITY-REMEDIATION-PLAN.md](./SECURITY-REMEDIATION-PLAN.md)、[CONFIGURATION-GUIDE.md](./CONFIGURATION-GUIDE.md) |

---

## 1. 概述

本文档记录 WMS 系统在安全加固过程中引入的 **架构级变更**。这些变更不仅是补丁，而是建立了新的安全机制，对未来开发有约束作用。

涉及的架构变更：
1. RefreshToken 家族追踪（Family-based Tracking）
2. JWT 黑名单机制（Token Revocation）
3. RowVersion 乐观并发控制
4. WCS HMAC 设备认证
5. 配置注入新机制（User Secrets + 环境变量）
6. 中间件管道重组
7. 审计日志增强
8. 仓库级数据隔离（待规划）

---

## 2. RefreshToken 家族追踪

### 2.1 背景

OAuth 2.0 RFC 6749 §10.4 要求：当检测到已使用的 RefreshToken 被再次提交时，必须吊销整个令牌家族（family）。WMS 当前实现不满足此要求，token 被盗后可永久劫持账户。

### 2.2 设计

#### 数据模型变更

```csharp
// Wms.Core.Domain/Entities/Identity/RefreshToken.cs
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }  // 新增
    public string TokenHash { get; set; }
    public string UserName { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsUsed { get; set; }
    public bool IsRevoked { get; set; }
    public string? ReplacedByToken { get; set; }  // 新增：链追踪
    public string? RevokedReason { get; set; }     // 新增
    public string? RevokedByIp { get; set; }       // 新增
}
```

#### EF Core 配置

```csharp
// RefreshTokenConfiguration.cs
public void Configure(EntityTypeBuilder<RefreshToken> builder)
{
    builder.HasIndex(x => x.TokenHash).IsUnique();
    builder.HasIndex(x => x.FamilyId);  // 新增索引
    builder.HasIndex(x => x.UserName);
}
```

#### 迁移

```bash
dotnet ef migrations add AddRefreshTokenFamily --project src/Wms.Core.Infrastructure --startup-project src/Wms.Core.WebApi
```

### 2.3 流程

#### 登录时

```csharp
public async Task<LoginResult> LoginAsync(string username, string password)
{
    // ... 密码校验 ...
    var familyId = Guid.NewGuid();
    var refreshToken = GenerateRefreshToken(familyId, username);
    await _refreshTokenRepository.Create(refreshToken);
    // ...
}
```

#### 刷新时

```csharp
public async Task<RefreshResult> RefreshAsync(string refreshToken)
{
    var hash = HashToken(refreshToken);
    var stored = await _refreshTokenRepository.GetByToken(hash);

    if (stored == null) return Fail("Invalid token");

    if (stored.IsRevoked)
    {
        // ⚠️ 检测到已吊销 token 被重用 → 吊销整个家族
        await _refreshTokenRepository.RevokeFamilyAsync(stored.FamilyId, "Reuse detected");
        _logger.LogWarning("Refresh token reuse detected for user {UserName}, family {FamilyId} revoked",
            stored.UserName, stored.FamilyId);
        return Fail("Token reuse detected, all sessions revoked");
    }

    if (stored.IsUsed)
    {
        // 同样视为重用
        await _refreshTokenRepository.RevokeFamilyAsync(stored.FamilyId, "Used token reused");
        return Fail("Token reuse detected");
    }

    if (stored.ExpiresAt < DateTime.UtcNow)
    {
        return Fail("Token expired");
    }

    // 标记已使用，生成新 token（继承 FamilyId）
    stored.IsUsed = true;
    var newToken = GenerateRefreshToken(stored.FamilyId, stored.UserName);
    stored.ReplacedByToken = newToken.TokenHash;
    await _refreshTokenRepository.Update(stored);
    await _refreshTokenRepository.Create(newToken);

    return Success(newToken);
}
```

### 2.4 集成测试

```csharp
[Fact]
public async Task ReusingRevokedToken_RevokesEntireFamily()
{
    // 1. 登录获取 family token1
    var login = await _authService.LoginAsync("user", "pass");
    var token1 = login.RefreshToken;

    // 2. 用 token1 刷新获取 token2
    var refresh1 = await _authService.RefreshAsync(token1);
    var token2 = refresh1.RefreshToken;

    // 3. 再次使用 token1（重用）应触发吊销
    var refresh2 = await _authService.RefreshAsync(token1);
    Assert.False(refresh2.Success);
    Assert.Contains("reuse", refresh2.Error.ToLower());

    // 4. 此时 token2 也应被吊销
    var refresh3 = await _authService.RefreshAsync(token2);
    Assert.False(refresh3.Success);
}
```

---

## 3. JWT 黑名单机制

### 3.1 背景

JWT 是无状态的，登出后到自然过期前（60 分钟）仍可用。需要基于 Redis 的黑名单机制实现即时吊销。

### 3.2 设计

#### TokenService 调整

```csharp
public class TokenService
{
    public string GenerateAccessToken(User user, List<Claim> additionalClaims)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),  // 唯一 ID
            // ...
        };
        // ...
    }
}
```

#### Redis 黑名单服务

```csharp
public interface IJwtBlacklistService
{
    Task RevokeAsync(string jti, TimeSpan ttl);
    Task<bool> IsRevokedAsync(string jti);
}

public class JwtBlacklistService : IJwtBlacklistService
{
    private readonly IConnectionMultiplexer _redis;
    private const string KeyPrefix = "jwt:blacklist:";

    public async Task RevokeAsync(string jti, TimeSpan ttl)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync($"{KeyPrefix}{jti}", "1", ttl);
    }

    public async Task<bool> IsRevokedAsync(string jti)
    {
        var db = _redis.GetDatabase();
        return await db.KeyExistsAsync($"{KeyPrefix}{jti}");
    }
}
```

#### JwtBearer Events 集成

```csharp
// AuthenticationExtensions.cs
options.Events = new JwtBearerEvents
{
    OnTokenValidated = async context =>
    {
        var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (string.IsNullOrEmpty(jti)) return;

        var blacklist = context.HttpContext.RequestServices
            .GetRequiredService<IJwtBlacklistService>();
        if (await blacklist.IsRevokedAsync(jti))
        {
            context.Fail("Token has been revoked");
        }
    }
};
```

#### Logout 实现

```csharp
[HttpPost("logout")]
public async Task<IActionResult> Logout()
{
    var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
    var expClaim = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
    if (long.TryParse(expClaim, out var exp))
    {
        var ttl = DateTimeOffset.FromUnixTimeSeconds(exp) - DateTimeOffset.UtcNow;
        if (ttl > TimeSpan.Zero)
            await _blacklist.RevokeAsync(jti, ttl.Value);
    }

    var userId = User.FindFirst("userId")?.Value;
    await _refreshTokenRepository.RevokeAllUserTokens(userId);

    return NoContent();
}
```

### 3.3 Redis 数据库规划

- 使用专用 DB（如 DB 1）存储黑名单
- key TTL 自动过期，无需手动清理
- key 格式：`jwt:blacklist:{jti}`
- 预估内存：每个 jti 约 50 字节，假设每日 1000 次登出 × 60 分钟 = 1000 条同时存在，约 50KB

---

## 4. RowVersion 乐观并发控制

### 4.1 背景

WMS 系统无任何并发控制，多操作员同时操作同一资源（货载/任务/库位）时会导致数据不一致。

### 4.2 设计

#### 实体添加 RowVersion

```csharp
// 核心实体：Unitload, TransTask, Location, Stock, OutboundBatch, BatteryCell
public class Unitload
{
    // ... 其他字段 ...
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
```

#### EF Core 配置

```csharp
public void Configure(EntityTypeBuilder<Unitload> builder)
{
    // ...
    builder.Property(x => x.RowVersion).IsRowVersion();
}
```

#### Controller 处理并发冲突

```csharp
[HttpPut("{id}")]
public async Task<IActionResult> Update(int id, [FromBody] UpdateDto dto)
{
    try
    {
        var entity = await _repository.GetById(id);
        // 更新字段
        await _repository.SaveChangesAsync();
        return Ok(entity);
    }
    catch (DbUpdateConcurrencyException ex)
    {
        _logger.LogWarning(ex, "并发冲突：Unitload {Id}", id);
        return Conflict(new { code = 409, message = "数据已被其他人修改，请刷新后重试" });
    }
}
```

#### 前端处理

```ts
try {
  await api.update(id, data);
} catch (e: any) {
  if (e.response?.status === 409) {
    ElMessageBox.alert('数据已被其他人修改，请刷新后重试', '冲突', { type: 'warning' });
    loadData();
  } else {
    throw e;
  }
}
```

### 4.3 涉及实体

| 实体 | 是否需要 | 原因 |
|------|---------|------|
| Unitload | ✅ 必需 | 多操作员同时操作货载 |
| TransTask | ✅ 必需 | ForceComplete/ForceCancel 重放 |
| Location | ✅ 必需 | 入库/出库并发修改计数 |
| Stock | ✅ 必需 | 库存增减 |
| OutboundBatch | ✅ 必需 | 数量修改 |
| BatteryCell | ✅ 必需 | 分选/状态修改 |
| Material | 🟡 可选 | 不常并发修改 |
| User | 🟡 可选 | 主要风险已在认证层处理 |
| Warehouse | ⚪ 不需要 | 极少修改 |

### 4.4 迁移

```bash
dotnet ef migrations add AddRowVersionConcurrency --project src/Wms.Core.Infrastructure --startup-project src/Wms.Core.WebApi
```

迁移会添加 nullable varbinary(max) 列，对现有数据无影响（NULL = 无版本控制，首次 Update 后填充）。

---

## 5. WCS HMAC 设备认证

### 5.1 背景

当前 WCS/Hangke/OutboundTimer 控制器仅靠可绕过的 IP 白名单，无设备级认证。需要引入 HMAC 签名机制。

### 5.2 设计

#### 配置

```json
// appsettings.json
{
  "Wcs": {
    "AllowedIps": ["10.0.0.100", "10.0.0.101"],
    "ApiKey": "共享密钥（每个设备相同或不同）",
    "RequestWindowSeconds": 300
  }
}
```

#### WcsAuthAttribute

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class WcsAuthAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var config = httpContext.RequestServices.GetRequiredService<IOptions<WcsAuthOptions>>().Value;

        // 1. 提取签名头
        var apiKey = httpContext.Request.Headers["X-Wcs-Api-Key"].FirstOrDefault();
        var signature = httpContext.Request.Headers["X-Wcs-Signature"].FirstOrDefault();
        var timestamp = httpContext.Request.Headers["X-Wcs-Timestamp"].FirstOrDefault();

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(timestamp))
        {
            context.Result = new UnauthorizedObjectResult("Missing authentication headers");
            return;
        }

        // 2. 时间窗口校验（防重放）
        if (!long.TryParse(timestamp, out var ts) ||
            Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts) > config.RequestWindowSeconds)
        {
            context.Result = new UnauthorizedObjectResult("Request timestamp out of window");
            return;
        }

        // 3. 校验 ApiKey
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(apiKey),
                Encoding.UTF8.GetBytes(config.ApiKey)))
        {
            context.Result = new UnauthorizedObjectResult("Invalid API key");
            return;
        }

        // 4. 读取请求体并验证签名
        httpContext.Request.EnableBuffering();
        using var reader = new StreamReader(httpContext.Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        httpContext.Request.Body.Position = 0;

        var payload = $"{body}{timestamp}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(config.ApiKey));
        var expectedSignature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));

        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(signature),
                Convert.FromBase64String(expectedSignature)))
        {
            context.Result = new UnauthorizedObjectResult("Invalid signature");
            return;
        }

        await next();
    }
}
```

#### 应用到 Controller

```csharp
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[WcsAuth]                              // 新增 HMAC 认证
[InternalIpWhitelist]                  // 保留 IP 白名单
[AllowAnonymous]                       // 不需要 JWT（设备无用户身份）
public class WcsController : ControllerBase
{
    // ...
}
```

### 5.3 客户端集成

WCS 设备需要在每个请求中添加：
```http
POST /api/v1/wcs/inbound HTTP/1.1
Host: wms.example.com
Content-Type: application/json
X-Wcs-Api-Key: 共享密钥
X-Wcs-Timestamp: 1690000000
X-Wcs-Signature: Base64(HMACSHA256(共享密钥, requestBody + timestamp))

{"containerCode": "C001", ...}
```

---

## 6. 配置注入新机制

详见 [CONFIGURATION-GUIDE.md](./CONFIGURATION-GUIDE.md)。

### 6.1 关键变化

```
旧：appsettings.json 含明文密码 → 入库 → 泄露
新：appsettings.json 占位符 → 环境变量 / User Secrets / Docker secrets 注入
```

### 6.2 SecureConfigurationExtensions

提供 `GetSecureConnectionString(key)` 和 `ValidateJwtSecret()` 方法（详见配置指南）。

---

## 7. 中间件管道重组

### 7.1 旧顺序（错误）

```
1. ForwardedHeaders（信任所有代理）
2. GlobalExceptionHandler
3. SecurityHeaders
4. Swagger（开发）
5. HttpsRedirection
6. 静态文件 + uploads 认证
7. CORS
8. ResponseCaching       ← 错误位置
9. LanguagePack
10. Authentication
11. Authorization
12. RateLimit            ← 错误位置
13. Endpoints
```

### 7.2 新顺序（正确）

```
1. ForwardedHeaders（限制 KnownProxies）
2. GlobalExceptionHandler
3. SecurityHeaders（含 HSTS, CSP, Referrer-Policy 等）
4. Swagger（仅开发）
5. HttpsRedirection
6. 静态文件（MIME 白名单 + Content-Disposition）
7. CORS（生产环境白名单）
8. RateLimit（移到认证前，对未认证请求也限流）
9. LanguagePack
10. Authentication
11. Authorization
12. ResponseCaching（移到授权后，仅缓存公开内容）
13. Endpoints
```

### 7.3 关键变化

1. **ResponseCaching** 移到认证之后：防止缓存命中后跳过授权检查
2. **RateLimit** 移到认证之前：未认证的暴力破解请求也限流
3. **ForwardedHeaders** 配置 KnownProxies：防止 IP 伪造
4. **SecurityHeaders** 增强：添加 HSTS, CSP, Permissions-Policy

---

## 8. 审计日志增强

### 8.1 脱敏字段扩展

```csharp
// OperationLogFilter.cs
private static readonly string[] SensitiveFieldPatterns = new[]
{
    "password", "pwd", "token", "secret", "apiKey", "api_key",
    "hash", "salt", "refreshToken", "authorization"
};

private static string SanitizeRequestBody(string json)
{
    foreach (var pattern in SensitiveFieldPatterns)
    {
        var regex = new Regex($@"""(\w*{pattern}\w*)""\s*:\s*""[^""]*""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        json = regex.Replace(json, @"""$1"":""***""");
    }
    return json;
}
```

### 8.2 GET 请求审计

对敏感 Controller（Users, Materials, Unitloads）记录 GET 请求访问：

```csharp
// 仅对指定 Controller 启用 GET 审计
private static readonly HashSet<string> AuditedReadControllers = new()
{
    "UsersController", "MaterialsController", "UnitloadsController",
    "BatteryCellsController", "ReportsController"
};

public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
{
    var isWriteMethod = ...;
    var isAuditedRead = !isWriteMethod &&
        AuditedReadControllers.Contains(context.Controller.GetType().Name);

    if (!isWriteMethod && !isAuditedRead)
    {
        await next();
        return;
    }
    // 记录日志
}
```

---

## 9. 仓库级数据隔离（待规划）

### 9.1 背景

当前所有列表/详情接口不做仓库级/租户级数据隔离，多仓库场景下普通用户能查询所有仓库的数据。

### 9.2 设计方向（中长期）

#### 数据模型

```csharp
public class User
{
    // ...
    public List<int> AccessibleWarehouseIds { get; set; } = new();
}

public class Warehouse
{
    public int Id { get; set; }
    public string Code { get; set; }
    // ...
}
```

#### 全局查询过滤器

```csharp
// WmsDbContext.cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Material>().HasQueryFilter(m =>
        _userContext.AccessibleWarehouseIds.Contains(m.WarehouseId));
    // 对 Location, Unitload, TransTask 等同样添加
}
```

#### IUserContext 注入

```csharp
public interface IUserContext
{
    int UserId { get; }
    string UserName { get; }
    List<int> AccessibleWarehouseIds { get; }
    bool IsAdmin { get; }
}

public class HttpUserContext : IUserContext
{
    private readonly IHttpContextAccessor _accessor;

    public List<int> AccessibleWarehouseIds
    {
        get
        {
            var claims = _accessor.HttpContext?.User?.FindFirst("warehouses")?.Value;
            return string.IsNullOrEmpty(claims) ? new() : JsonSerializer.Deserialize<List<int>>(claims);
        }
    }
    // ...
}
```

#### JWT Claims 添加

```csharp
// TokenService.cs
var claims = new List<Claim>
{
    // ...
    new("warehouses", JsonSerializer.Serialize(user.AccessibleWarehouseIds))
};
```

> 此项变更为中长期规划，需要评估业务需求后实施。

---

## 10. 总结

本次安全加固引入的架构变更：

| 变更 | 影响范围 | 可逆性 | 文档 |
|------|---------|--------|------|
| RefreshToken 家族追踪 | 后端 Auth | 数据库迁移，需谨慎回滚 | 本文档 §2 |
| JWT 黑名单 | 后端 Auth + Redis | Redis 数据可清空，可逆 | 本文档 §3 |
| RowVersion 并发 | 后端核心实体 | 数据库迁移，可逆（删除字段） | 本文档 §4 |
| WCS HMAC 认证 | 后端 WcsController + WCS 设备 | 需要与设备方协调，回滚需移除 Attribute | 本文档 §5 |
| 配置注入机制 | 后端 Program.cs + 配置文件 | 完全可逆 | CONFIGURATION-GUIDE.md |
| 中间件重组 | 后端 MiddlewareExtensions | 完全可逆 | 本文档 §7 |
| 审计日志增强 | 后端 OperationLogFilter | 完全可逆 | 本文档 §8 |
| 仓库级数据隔离（规划中） | 全栈 | 重大变更，需评估 | 本文档 §9 |

---

## 附录：架构图（建议补绘）

1. **认证流程图**：登录 → JWT + RefreshToken(FamilyId) → 刷新链 → 检测重用 → 吊销家族
2. **并发控制流程图**：操作员 A 读 → 操作员 B 读 → A 写（成功）→ B 写（409 Conflict）
3. **WCS HMAC 认证时序图**：WMS 客户端 → 设备端 → HMAC 签名 → WMS 验证
4. **中间件管道顺序图**：请求 → 各中间件顺序 → Controller

> 建议使用 draw.io 或 PlantUML 绘制后插入本文档。
