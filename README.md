# WMS Core - .NET 8.0 迁移项目

这是 WMS Core 从 .NET Framework 4.8 迁移到 .NET 8.0 的项目。

## 项目结构

```
Wms.Net8/
├── src/
│   ├── Wms.Core.Domain/          # 领域层（核心业务逻辑）
│   ├── Wms.Core.Infrastructure/  # 基础设施层（数据访问、外部服务）
│   └── Wms.Core.WebApi/          # Web API（RESTful API）
└── tests/
    ├── Wms.Core.UnitTests/       # 单元测试
    └── Wms.Core.IntegrationTests/ # 集成测试
```

## 技术栈

- **.NET 8.0** - 目标框架
- **ASP.NET Core 8.0** - Web 框架
- **NHibernate 5.4.2** - ORM 框架
- **SQL Server** - 数据库
- **NLog 5.3.2** - 日志框架
- **Swagger** - API 文档
- **xUnit** - 测试框架

## 前置要求

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server 2012+ 或 LocalDB
- Visual Studio 2022 或 VS Code

## 快速开始

### 1. 还原 NuGet 包

```bash
cd Wms.Net8
dotnet restore
```

### 2. 配置数据库连接

编辑 `src/Wms.Core.WebApi/appsettings.json`：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=WmsDb;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

### 3. 构建项目

```bash
dotnet build
```

### 4. 运行 Web API

```bash
cd src/Wms.Core.WebApi
dotnet run
```

API 将在以下地址启动：
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`
- Swagger UI: `https://localhost:5001` (根路径)

### 5. 运行测试

```bash
# 运行所有测试
dotnet test

# 运行单元测试
dotnet test tests/Wms.Core.UnitTests

# 运行集成测试
dotnet test tests/Wms.Core.IntegrationTests
```

## API 端点示例

### 位置管理

- `GET /api/locations` - 获取所有位置
- `GET /api/locations/{id}` - 获取指定位置
- `POST /api/locations` - 创建新位置
- `PUT /api/locations/{id}` - 更新位置
- `DELETE /api/locations/{id}` - 删除位置
- `GET /api/locations/{id}/can-inbound` - 检查位置是否可以入站

### 健康检查

- `GET /health` - 健康检查端点

## 迁移进度

### ✅ 已完成
- [x] 创建 .NET 8.0 解决方案结构
- [x] 配置 NHibernate 5.4.2
- [x] 配置内置 DI 容器
- [x] 创建基础实体接口
- [x] 创建 Location 示例实体
- [x] 创建 LocationsController 示例
- [x] 配置 Swagger/OpenAPI

### 🚧 进行中
- [ ] 迁移 Domain 层实体（117个文件）
- [ ] 迁移 Repository 层
- [ ] 迁移领域服务
- [ ] 迁移事件总线（CallContext → AsyncLocal）
- [ ] 配置 NLog 日志系统
- [ ] 实现事务中间件

### 📋 待办
- [ ] 实现认证授权（JWT）
- [ ] 迁移 Excel 处理（OleDb → ExcelDataReader）
- [ ] 迁移货位分配规则引擎（14个规则）
- [ ] 实现后台服务（WCS 任务调度）
- [ ] Docker 化应用
- [ ] 编写单元测试和集成测试

## 从旧项目迁移

### 关键变更

1. **配置系统**
   - 旧: `System.Configuration.ConfigurationManager`
   - 新: `Microsoft.Extensions.Configuration.IOptions`

2. **DI 容器**
   - 旧: Autofac
   - 新: .NET 内置 DI

3. **异步上下文**
   - 旧: `CallContext`
   - 新: `AsyncLocal<T>`

4. **Web 框架**
   - 旧: ASP.NET MVC 5
   - 新: ASP.NET Core Web API

5. **Excel 处理**
   - 旧: `System.Data.OleDb`
   - 新: `ExcelDataReader` (跨平台)

## 开发规范

### 代码规范
- 遵循 C# 编码约定
- 使用 XML 文档注释
- 启用 Nullable 引用类型
- 使用 ImplicitUsings

### 提交规范
- feat: 新功能
- fix: 修复 Bug
- refactor: 重构
- test: 测试
- docs: 文档
- chore: 构建/工具

## 性能目标

- API 响应时间 < 200ms (P95)
- 数据库查询 < 100ms (P95)
- 并发用户 > 100

## 许可证

[待定]

## 联系方式

- WMS Team
- Email: wms-team@example.com

---

**注意**: 这是一个迁移项目，目前处于开发阶段。许多功能尚未完成。
