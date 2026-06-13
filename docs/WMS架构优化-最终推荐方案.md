# WMS 智能仓储系统 — 最终技术选型推荐方案

> **文档日期：** 2026-05-31（v2 — 补充 Polly、Dapper）
> **适用项目：** Wms.Core (.NET 8 + EF Core + SQL Server)
> **审查基准：** 原方案 + 2025 年市场数据验证 + 二次补充审查

---

## 一、技术选型总览（27 项）

### P0：安全与性能关键

| # | 领域 | 推荐方案 | 推荐理由 |
|:---:|------|---------|---------|
| 1 | **密码哈希** | **BCrypt.Net-Next** | NuGet 50M+ 下载，配置简单，安全足够。Argon2id 虽更强但配置复杂、生态小众（6.8M），BCrypt 是最佳性价比 |
| 2 | **库存并发控制** | **RowVersion 乐观锁 + Redis 分布式锁** | WMS 核心，库存操作必须防并发。RowVersion 处理单记录冲突，Redis 锁处理跨服务场景 |
| 3 | **SQL 注入修复** | **白名单校验 + 参数化查询** | orderBy 拼接必须修，用白名单枚举所有允许的排序字段 |
| 4 | **UnitOfWork** | **不引入** | EF Core 的 DbContext + IDbContextTransaction 已足够。额外封装 IUnitOfWork 在当前项目规模下是过度抽象 |

### P1：架构模式

| # | 领域 | 推荐方案 | 推荐理由 |
|:---:|------|---------|---------|
| 5 | **命令查询分离** | **自建轻量 Mediator** | MediatR 已转商业授权（2025.4），且对 WMS 项目来说太重。一个 100 行的 IMediator 接口 + DI 注册就能满足 90% 需求 |
| 6 | **对象映射** | **Mapperly** | 2025 年 .NET 社区增长最快的映射库。零分配、源码生成、NativeAOT 兼容、免费 MIT。比 Mapster 更现代，ABP Framework 已迁移 |
| 7 | **输入验证** | **FluentValidation**（独立注册） | .NET 8/9 Minimal API 无内置验证支持。FluentValidation 是唯一成熟方案，复杂验证（查数据库）只有它能做 |
| 8 | **领域事件** | **自建 IEventBus + Redis Pub/Sub** | MassTransit 也转商业授权了。WMS 场景用 Redis Pub/Sub 处理领域事件完全够用，轻量且已有 Redis 基础设施 |
| 9 | **查询规约** | **不引入** | EF Core 的 IQueryable + 扩展方法更直接。Specification 模式在这个项目里只会增加复杂度，多数 .NET 专家认为过度工程化 |
| 10 | **弹性韧性** | **Polly**（重试 + 熔断 + 超时） | WMS 必须对接外部系统（ERP、MES、WCS），网络故障时无重试/熔断会导致业务中断。Polly 是 .NET 生态唯一的弹性韧性库，Microsoft 官方推荐配合 IHttpClientFactory 使用 |
| 11 | **高性能读取** | **Dapper**（与 EF Core 互补） | 条码扫描查询是 WMS 最高频操作，Dapper 无变更跟踪开销，比 EF Core 快 2-3x。社区共识：EF Core 负责写入，Dapper 负责高频读取 |

### P2：基础设施

| # | 领域 | 推荐方案 | 推荐理由 |
|:---:|------|---------|---------|
| 12 | **任务调度** | **Hangfire** | .NET 生态唯一有内置仪表盘的调度器。SQL Server 持久化、自动重试、持久化队列。Quartz 太重，Coravel 太轻 |
| 13 | **实时通信** | **SignalR** | 浏览器实时推送最佳选择。自动传输降级（WebSocket→SSE→Long Polling）、Hub 编程模型、.NET 一等公民 |
| 14 | **消息队列** | **Redis Stream** | 项目已有 Redis，用 Redis Stream 做消息队列零额外基础设施。RabbitMQ 更强但需运维新中间件，对当前项目规模不值得 |
| 15 | **可观测性** | **Serilog + Seq** | 小型/中型项目最务实方案：Serilog 结构化日志 + Seq 一键 Docker 部署 + Web UI 查询。OpenTelemetry 更适合大规模分布式系统 |
| 16 | **容器化** | **Docker + docker-compose** | 通用标准，语言无关。.NET Aspire 2025 年还不够成熟，且绑定 .NET 生态 |

### P3：升级与优化

| # | 领域 | 推荐方案 | 推荐理由 |
|:---:|------|---------|---------|
| 17 | **运行时版本** | **保留 .NET 8 → 计划升级 .NET 10 LTS** | .NET 9 是 STS（仅 18 个月支持），不是 LTS。直接等 .NET 10 LTS（2025.11 发布）跳升，避免双次升级 |
| 18 | **JWT 签名** | **RS256 非对称密钥** | 生产环境标配。HS256 对称密钥泄露后所有 token 可伪造，RS256 私钥仅在服务端 |
| 19 | **批量操作** | **EF Core ExecuteUpdate/ExecuteDelete** | .NET 8 原生支持，无需 SaveChanges 循环，性能提升显著 |
| 20 | **API 文档** | **Scalar** | UI 比 Swashbuckle 好看得多，开发者体验更好，配置简单 |
| 21 | **速率限制** | **ASP.NET Core 内置 RateLimiting** | .NET 7+ 内置，单机场景够用。不需要 AspNetCoreRateLimit 第三方库 |

### P4：WMS 业务专项

| # | 领域 | 推荐方案 | 推荐理由 |
|:---:|------|---------|---------|
| 22 | **库位分配** | **ABC 分类 + 策略模式自建规则** | ABC 分类是 WMS 行业基础。用策略模式（Strategy Pattern）实现分配规则，比引入规则引擎轻量得多。后期可叠加 ML.NET 需求预测 |
| 23 | **WCS 设备集成** | **IWcsClient 接口 + 适配器模式** | 标准做法。UploadMesInfo/InterfaceOps 表已有，抽象出接口后可对接不同设备厂商 |
| 24 | **库存缓存** | **Redis Hash** | 实时库存查询走 Redis 缓存，写操作双写 DB+Cache。防超卖用 Redis 分布式锁 |
| 25 | **报表导出** | **MiniExcel**（大数据量）/ ClosedXML（复杂格式） | MiniExcel 流式读写，10 万行数据内存仅几 MB。ClosedXML 适合需要图表、复杂格式的报表 |
| 26 | **多语言** | **保留现有 LanguagePack** | 已实现，无需改动 |
| 27 | **Secrets 管理** | **User Secrets（开发）/ Azure Key Vault 或环境变量（生产）** | JWT 密钥、数据库密码、Redis 连接字符串不应硬编码在 appsettings.json |

---

## 二、推荐实施顺序

### 第一批：安全加固（最高优先级）

| 序号 | 任务 | 涉及方案 |
|:---:|------|---------|
| 1 | BCrypt.Net-Next 替换自实现 HMACSHA256 密码哈希 | #1 |
| 2 | orderBy 白名单校验修复 SQL 注入 | #3 |
| 3 | 库存相关实体添加 RowVersion 并发控制 | #2 |
| 4 | FluentValidation 验证器实现 | #7 |

### 第二批：架构工具（高优先级）

| 序号 | 任务 | 涉及方案 |
|:---:|------|---------|
| 5 | Mapperly 对象映射配置 | #6 |
| 6 | Polly 弹性韧性（HttpClient 重试+熔断+超时） | #10 |
| 7 | Dapper 高频读取层（条码扫描、库存查询） | #11 |
| 8 | Hangfire 任务调度集成（SQL Server 存储） | #12 |
| 9 | SignalR 实时通信 Hub | #13 |
| 10 | 自建轻量 Mediator（如需要） | #5 |

### 第三批：WMS 业务（高优先级）

| 序号 | 任务 | 涉及方案 |
|:---:|------|---------|
| 11 | 库位分配策略引擎（ABC + 策略模式） | #22 |
| 12 | 入库/出库/拣货/波次业务流程 | — |
| 13 | Redis 库存缓存 + 分布式锁 | #24 |
| 14 | WCS 通信适配器层 | #23 |

### 第四批：运维工具（中优先级）

| 序号 | 任务 | 涉及方案 |
|:---:|------|---------|
| 15 | Docker + docker-compose 容器化 | #16 |
| 16 | Serilog + Seq 日志系统 | #15 |
| 17 | Scalar API 文档替换 Swashbuckle | #20 |
| 18 | EF Core ExecuteUpdate/ExecuteDelete 批量操作优化 | #19 |

### 第五批：长期规划（低优先级）

| 序号 | 任务 | 涉及方案 |
|:---:|------|---------|
| 19 | JWT RS256 非对称签名迁移 | #18 |
| 20 | Secrets 管理（移除硬编码密钥） | #27 |
| 21 | 自建领域事件总线 + Redis Pub/Sub | #8 |
| 22 | ML.NET 需求预测（库位分配增强） | #22 进阶 |
| 23 | 升级 .NET 10 LTS（待 2025.11 正式发布） | #17 |

---

## 三、NuGet 包清单

需要安装的新 NuGet 包：

| 包名 | 用途 | 许可证 |
|------|------|------|
| `BCrypt.Net-Next` | 密码哈希 | MIT |
| `Riok.Mapperly` | 对象映射（源码生成） | MIT |
| `FluentValidation.AspNetCore` | 输入验证 | Apache-2.0 |
| `Microsoft.Extensions.Http.Polly` | HttpClient 弹性韧性（重试+熔断） | MIT（微软官方） |
| `Dapper` | 高性能读取（条码扫描、库存查询） | Apache-2.0 |
| `Hangfire.SqlServer` | 任务调度（SQL Server 存储） | LGPL-3.0 |
| `MiniExcel` | 大数据量 Excel 导出 | MIT |
| `Serilog.Sinks.Seq` | Seq 日志接收 | Apache-2.0 |

已有 / 内置（无需额外安装）：

| 包名/功能 | 用途 |
|------|------|
| `Microsoft.AspNetCore.SignalR` | 实时通信（.NET 内置） |
| `Microsoft.AspNetCore.RateLimiting` | 速率限制（.NET 7+ 内置） |
| `StackExchange.Redis` | Redis 缓存/锁/Pub/Sub（项目已引用） |
| `Microsoft.EntityFrameworkCore` | ExecuteUpdate/ExecuteDelete（EF Core 内置） |

---

## 四、相关文档

- [WMS架构优化分析与技术选型排名.md](WMS架构优化分析与技术选型排名.md) — 原始方案（含市场调研和开源对比）
- [WMS架构优化方案-市场验证审查报告.md](WMS架构优化方案-市场验证审查报告.md) — 市场验证审查报告（7 项修正详情 + 数据来源）
