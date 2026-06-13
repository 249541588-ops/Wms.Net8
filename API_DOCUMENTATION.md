# WMS Core API 文档

## 快速开始

### 1. 访问 Swagger UI
启动应用后，访问根路径：`http://localhost:5000/`

### 2. 获取 Token
```
POST /api/auth/login
Content-Type: application/json

{
  "username": "admin",
  "password": "admin123"
}
```

响应：
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiration": "2024-02-04T12:00:00Z",
  "user": {
    "userId": "1",
    "username": "admin",
    "displayName": "系统管理员",
    "role": "Admin",
    "permissions": ["create", "read", "update", "delete"]
  }
}
```

### 3. 使用 Token 访问 API
在 Swagger UI 中：
1. 点击右上角 "Authorize" 按钮
2. 输入：`Bearer <your_token>`（注意有空格）
3. 点击 "Authorize"
4. 现在可以调用需要认证的 API

---

## 主要 API 端点

### 认证授权
| 端点 | 方法 | 说明 | 认证 |
|------|------|------|------|
| `/api/auth/login` | POST | 用户登录 | 否 |
| `/api/auth/refresh` | POST | 刷新 Token | 否 |
| `/api/auth/me` | GET | 获取当前用户信息 | 是 |
| `/api/auth/logout` | POST | 用户注销 | 是 |

### 物料管理
| 端点 | 方法 | 说明 | 认证 |
|------|------|------|------|
| `/api/materials` | GET | 获取所有物料 | 否 |
| `/api/materials/{id}` | GET | 获取物料详情 | 是 |
| `/api/materials/by-code/{code}` | GET | 根据编码获取物料 | 是 |
| `/api/materials` | POST | 创建物料 | 是 |
| `/api/materials/{id}` | PUT | 更新物料 | 是 |
| `/api/materials/{id}` | DELETE | 删除物料 | Admin |
| `/api/materials/{id}/summary` | GET | 获取物料摘要 | 是 |
| `/api/materials/{id}/stocks` | GET | 获取物料库存 | 是 |

### 用户管理（仅 Admin）
| 端点 | 方法 | 说明 | 认证 |
|------|------|------|------|
| `/api/users` | GET | 获取所有用户 | Admin |
| `/api/users/{id}` | GET | 获取用户详情 | Admin |
| `/api/users` | POST | 创建用户 | Admin |
| `/api/users/{id}/password` | PATCH | 修改密码 | Admin |
| `/api/users/{id}/enabled` | PATCH | 启用/禁用用户 | Admin |

### 审计日志（仅 Admin）
| 端点 | 方法 | 说明 | 认证 |
|------|------|------|------|
| `/api/auditlogs` | GET | 获取审计日志 | Admin |
| `/api/auditlogs/{id}` | GET | 获取日志详情 | Admin |
| `/api/auditlogs/statistics` | GET | 获取操作统计 | Admin |

### 健康检查
| 端点 | 方法 | 说明 | 认证 |
|------|------|------|------|
| `/health` | GET | 详细健康信息（JSON） | 否 |
| `/healthz` | GET | 简化健康检查 | 否 |

---

## 默认用户

### 管理员
- 用户名：`admin`
- 密码：`admin123`
- 角色：Admin

### 普通用户
- 用户名：`user`
- 密码：`user123`
- 角色：User

⚠️ **生产环境请立即修改默认密码！**

---

## 错误响应格式

```json
{
  "status": 400,
  "message": "错误消息",
  "detail": "错误详情",
  "path": "/api/materials",
  "method": "POST",
  "timestamp": "2024-02-04T12:00:00Z"
}
```

### 常见 HTTP 状态码
- `200 OK` - 请求成功
- `201 Created` - 创建成功
- `400 Bad Request` - 请求参数错误
- `401 Unauthorized` - 未认证或 Token 无效
- `403 Forbidden` - 无权限访问
- `404 Not Found` - 资源不存在
- `500 Internal Server Error` - 服务器内部错误

---

## 分页查询

支持的查询参数：
```
GET /api/materials?page=1&pageSize=50&keyword=test&enabled=true
```

参数说明：
- `page`：页码（从 1 开始）
- `pageSize`：每页大小（默认 50）
- `keyword`：搜索关键字
- `enabled`：是否启用
- 其他筛选参数...

---

## 安全说明

### JWT Token 认证
1. 调用 `/api/auth/login` 获取 Token
2. 在请求头中添加：`Authorization: Bearer <token>`
3. Token 默认有效期 60 分钟

### 权限说明
- **Admin**：所有权限（创建、读取、更新、删除）
- **User**：只读权限

### 敏感操作
- DELETE 操作仅 Admin 可执行
- 用户管理仅 Admin 可访问
- 审计日志仅 Admin 可查看

---

## 性能优化

### 缓存端点（无需认证）
- `/api/materials` - 物料列表（建议客户端缓存）
- `/health` - 健康检查（可被负载均衡器轮询）

### 批量操作
使用批量导入接口（Excel）可提升效率。

---

## 测试

### 单元测试
```bash
dotnet test tests/Wms.Core.UnitTests/Wms.Core.UnitTests.csproj
```

### 集成测试
```bash
dotnet test tests/Wms.Core.IntegrationTests/Wms.Core.IntegrationTests.csproj
```

---

## 故障排查

### Token 无效
- 检查 Token 是否过期
- 检查 Token 格式：`Bearer <token>`
- 重新登录获取新 Token

### 403 Forbidden
- 检查用户角色
- 某些操作仅 Admin 可执行

### 数据库连接失败
- 检查连接字符串配置
- 检查 SQL Server 是否运行
- 查看 `/health` 端点获取详细信息

---

## 联系方式
- Email: wms-team@example.com
- 文档: Swagger UI (根路径)
- Issues: GitHub Issues

---

*最后更新：2026-02-04*
