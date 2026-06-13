# JWT 认证测试指南

## 概述

WMS Core API 现已启用 JWT 认证保护：
- ✅ **GET 操作**：匿名访问（开放读取）
- ✅ **POST/PUT/PATCH**：需要认证（需要登录）
- ✅ **DELETE 操作**：需要认证

## 测试账号

| 用户名 | 密码 | 角色 | 权限 |
|--------|------|------|------|
| admin | admin123 | Admin | create, read, update, delete |
| user | user123 | User | read |

## API 测试步骤

### 1. 启动应用

```bash
cd f:\Wms.Core\Wms.Net8
dotnet run --project src/Wms.Core.WebApi
```

应用将在 `http://localhost:5000` 启动。

### 2. 测试匿名访问（GET 请求）

GET 请求不需要认证，可以直接访问：

```bash
# 获取所有物料
curl http://localhost:5000/api/materials

# 获取指定物料
curl http://localhost:5000/api/materials/1

# 获取所有货载
curl http://localhost:5000/api/unitloads

# 获取库存信息
curl http://localhost:5000/api/stock
```

### 3. 测试认证访问（POST/PUT/PATCH/DELETE）

#### 3.1 登录获取 Token

```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "password": "admin123"
  }'
```

**响应示例**：
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIiwidW5pcXVlX25hbWUiOiJhZG1pbiIsianRpIjoiZjW5NzgyMy00YjU4LTQ3YTEtOWIxMy0xNzA2Y2E5ZmEwMTMiLCJodHRwOi8vc2NoZW1hcy5taWNyb3NvZnQuY29tL3dzLzIwMDgvMDYvaWRlbnRpdHkvY2xhaW1zL3JvbGUiOiJBZG1pbiIsInVzZXJJZCI6IjEiLCJ1c2VybmFtZSI6ImFkbWluIiwiZXhwIjoxNzM4NjQ4MTQzLCJpc3MiOiJXbXMuQ29yZS5XZWJBcGkiLCJhdWQiOiJXbXMuQ2xpZW50In0.HEADER.SIGNATURE",
  "refreshToken": "U2FsdGVkX1...",
  "expiration": "2026-02-04T12:00:00Z",
  "user": {
    "userId": "1",
    "username": "admin",
    "displayName": "系统管理员",
    "role": "Admin",
    "permissions": ["create", "read", "update", "delete"]
  }
}
```

#### 3.2 使用 Token 访问受保护的 API

```bash
# 创建物料（需要认证）
curl -X POST http://localhost:5000/api/materials \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN_HERE" \
  -d '{
    "materialCode": "MAT001",
    "description": "测试物料",
    "uom": "PCS",
    "createdBy": "admin"
  }'

# 更新物料（需要认证）
curl -X PUT http://localhost:5000/api/materials/1 \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN_HERE" \
  -d '{
    "description": "更新的描述",
    "modifiedBy": "admin"
  }'

# 删除物料（需要认证）
curl -X DELETE http://localhost:5000/api/materials/1 \
  -H "Authorization: Bearer YOUR_TOKEN_HERE"
```

### 4. 测试未授权访问

尝试不带 Token 访问需要认证的端点，应该返回 401 Unauthorized：

```bash
# 尝试不带 Token 创建物料
curl -X POST http://localhost:5000/api/materials \
  -H "Content-Type: application/json" \
  -d '{
    "materialCode": "MAT001",
    "description": "测试物料"
  }'
```

**预期响应**：
```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Unauthorized",
  "status": 401,
  "traceId": "00-..."
}
```

## Swagger UI 测试

### 1. 访问 Swagger UI

打开浏览器访问：`http://localhost:5000`

### 2. 配置认证

1. 点击右上角的 **Authorize** 按钮（或 🔒 图标）
2. 在弹出的对话框中输入：`Bearer YOUR_TOKEN_HERE`
   - 注意：`Bearer` 和 Token 之间有一个空格
3. 点击 **Authorize**
4. 关闭对话框

### 3. 测试 API

现在可以调用任何 API：
- **GET 请求**：无需认证（可以直接试）
- **POST/PUT/PATCH/DELETE**：自动附带 Token

### 4. 示例：创建物料

1. 找到 `POST /api/materials` 端点
2. 点击 **Try it out**
3. 在 Request body 中输入：
   ```json
   {
     "materialCode": "MAT001",
     "description": "测试物料",
     "uom": "PCS",
     "createdBy": "admin"
   }
   ```
4. 点击 **Execute**
5. 查看响应（应该返回 201 Created）

## Power Scripts

### 使用环境变量存储 Token

```bash
# Linux/Mac
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}' \
  | jq -r '.token')

# Windows PowerShell
$TOKEN = (Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" `
  -Method Post -ContentType "application/json" `
  -Body '{"username":"admin","password":"admin123"}').token

# 使用 Token
curl -X GET http://localhost:5000/api/auth/me \
  -H "Authorization: Bearer $TOKEN"
```

### 批量测试脚本

```bash
#!/bin/bash

API_URL="http://localhost:5000"

# 登录
echo "=== 登录 ==="
LOGIN_RESPONSE=$(curl -s -X POST $API_URL/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}')

echo $LOGIN_RESPONSE | jq

TOKEN=$(echo $LOGIN_RESPONSE | jq -r '.token')
echo "Token: $TOKEN"

# 获取当前用户
echo -e "\n=== 获取当前用户 ==="
curl -s -X GET $API_URL/api/auth/me \
  -H "Authorization: Bearer $TOKEN" | jq

# 创建物料
echo -e "\n=== 创建物料 ==="
curl -s -X POST $API_URL/api/materials \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "materialCode": "TEST001",
    "description": "测试物料",
    "uom": "PCS",
    "createdBy": "admin"
  }' | jq

# 获取所有物料
echo -e "\n=== 获取所有物料 ==="
curl -s -X GET $API_URL/api/materials | jq '. | length'
```

## 常见问题

### 1. Token 过期

Token 默认有效期为 60 分钟。过期后需要重新登录：

```bash
# 重新登录获取新 Token
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}'
```

### 2. 401 Unauthorized

如果收到 401 错误：
- 检查 Token 是否正确（包括 `Bearer ` 前缀）
- 检查 Token 是否过期
- 确认使用的是正确的测试账号

### 3. 修改 JWT 配置

编辑 `appsettings.json`：

```json
{
  "Jwt": {
    "Issuer": "Wms.Core.WebApi",
    "Audience": "Wms.Client",
    "SecretKey": "CHANGE_ME_USE_A_STRONG_RANDOM_SECRET_32_CHARS_MIN",
    "ExpirationMinutes": 120,  // 修改有效期
    "RefreshExpirationDays": 7
  }
}
```

⚠️ **生产环境注意事项**：
- 修改 `SecretKey` 为强密码（至少 32 字符）
- 使用 HTTPS（修改 `RequireHttpsMetadata = true`）
- 定期轮换密钥

## 下一步

- [ ] 实现真实的用户数据库验证
- [ ] 实现刷新 Token 机制
- [ ] 添加 Token 黑名单（使用 Redis）
- [ ] 实现基于角色的细粒度权限控制
- [ ] 添加 API 请求限流
