# 依赖升级指南（Dependency Upgrade Guide）

| 元信息 | 值 |
|--------|-----|
| 文档版本 | v1.0 |
| 生成日期 | 2026-07-06 |
| 适用范围 | Wms.Core（前端 Wms.Vue + 后端 Wms.Net8） |
| 文档状态 | 草案（待审核） |
| 关联文档 | [SECURITY-REMEDIATION-PLAN.md](./SECURITY-REMEDIATION-PLAN.md) |

---

## 1. 概述

本文档列出 WMS 项目中需要升级的依赖包、已知 CVE 漏洞、升级步骤、兼容性测试清单和回滚方案。

## 2. 前端依赖升级

### 2.1 ✅ xlsx 0.18.5（严重 CVE）— 已修复

> **状态**：✅ 已修复（2026-07-07）。`package.json` 已升级到 `https://cdn.sheetjs.com/xlsx-0.20.3/xlsx-0.20.3.tgz`，`node -e "require('xlsx').version"` 输出 `0.20.3`，`pnpm typecheck` 中 `excel/index.vue` 零错误。详见 [SECURITY-REMEDIATION-PLAN.md C12](./SECURITY-REMEDIATION-PLAN.md)。

#### 已知漏洞
- **CVE-2023-30533**：原型链污染（CVSS 7.5）
- **CVE-2024-22363**：ReDoS 正则灾难回溯（CVSS 7.5）

#### 升级方案

npm registry 上的 `xlsx` 包已停止更新（最后版本 0.18.5），SheetJS 转为 CDN 分发。

```bash
cd Wms.Vue

# 1. 移除旧版本
pnpm remove xlsx

# 2. 从 SheetJS CDN 安装最新版（≥0.20.2）
pnpm add https://cdn.sheetjs.com/xlsx-0.20.3/xlsx-0.20.3.tgz

# 3. 验证版本
node -e "console.log(require('xlsx').version)"
# 应输出 0.20.3 或更高
```

#### 兼容性测试

```bash
# 1. 物料导入测试
# 前端：登录 → 物料管理 → 导入 Excel → 选择测试 .xlsx 文件 → 验证导入成功

# 2. 报表导出测试（如前端使用 xlsx 导出）
# 前端：报表 → 查询 → 导出 → 打开导出文件验证内容

# 3. API 兼容性
# 验证常用 API 调用：XLSX.read、XLSX.utils.sheet_to_json、XLSX.utils.json_to_sheet、XLSX.writeFile
```

#### 回滚方案

```bash
# 如升级后有问题，回滚到 0.18.5（接受已知漏洞）
pnpm remove xlsx
pnpm add xlsx@0.18.5
```

### 2.2 🟠 wangeditor 4.7.15（XSS 漏洞）

#### 已知漏洞
- 4.x 系列存在多个 XSS 漏洞
- 4.x 已停更，官方推荐迁移到 5.x

#### 升级方案

```bash
cd Wms.Vue

# 1. 移除旧版本
pnpm remove wangeditor

# 2. 安装 5.x 版本
pnpm add @wangeditor/editor @wangeditor/editor-for-vue@5.x

# 3. 验证
node -e "console.log(require('@wangeditor/editor').version)"
```

#### 代码迁移

参考官方迁移指南：https://www.wangeditor.com/v5/guide/migrate.html

主要变化：
- 组件 API 完全不同，需要重写 `views/plugin/editor/quill/index.vue`
- 5.x 默认对粘贴内容做过滤（更安全）
- 与 DOMPurify 一起使用更安全

#### 兼容性测试

```bash
# 1. 编辑器加载测试
# 访问包含 wangeditor 的页面，确认编辑器正常显示

# 2. 粘贴测试
# 复制含 HTML 的内容粘贴，验证 XSS 过滤

# 3. 内容保存与回显测试
# 保存编辑内容 → 刷新页面 → 确认内容正确显示
```

#### 回滚方案

```bash
pnpm remove @wangeditor/editor @wangeditor/editor-for-vue
pnpm add wangeditor@4.7.15
# 然后还原 views/plugin/editor/quill/index.vue 代码
```

### 2.3 🟡 其他前端依赖（建议升级）

| 包 | 当前版本 | 建议版本 | 原因 |
|----|---------|---------|------|
| `vue-router` | 5.0.4 | 4.5.x（最新稳定） | 版本号异常，5.0.4 不存在正式版 |
| `dhtmlx-gantt` | 9.1.3 | 评估保留 | GPL 合规风险（确认是否有商业许可） |

#### vue-router 版本修正

```bash
# 确认实际安装的版本
pnpm list vue-router

# 如确认为 4.x，更新 package.json
pnpm remove vue-router
pnpm add vue-router@^4.5.0

# 测试所有路由功能
```

## 3. 后端依赖升级

### 3.1 🟠 Hangfire.Core 1.8.17（版本不一致）

#### 问题
- `Wms.Core.Infrastructure.csproj` 引用 `Hangfire.Core 1.8.17`
- `Wms.Core.WebApi.csproj` 引用 `Hangfire.AspNetCore 1.8.23` / `Hangfire.SqlServer 1.8.23`
- 版本不一致可能导致运行时行为不一致

#### 升级方案

```bash
cd Wms.Net8

# 升级所有 Hangfire 包到最新 1.8.x
dotnet add src/Wms.Core.Infrastructure/Wms.Core.Infrastructure.csproj package Hangfire.Core --version 1.8.23
dotnet add src/Wms.Core.WebApi/Wms.Core.WebApi.csproj package Hangfire.AspNetCore --version 1.8.23
dotnet add src/Wms.Core.WebApi/Wms.Core.WebApi.csproj package Hangfire.SqlServer --version 1.8.23

# 验证所有项目使用相同版本
grep -r "Hangfire" --include="*.csproj"
```

#### 兼容性测试

```bash
# 1. 启动应用，访问 /hangfire Dashboard
# 2. 触发一个 RecurringJob，确认正常执行
# 3. 检查 Hangfire 数据库表结构是否需要更新
```

### 3.2 🟡 NLog 版本不一致

#### 问题
- Infrastructure: `NLog 5.3.2` + `NLog.Extensions.Logging 5.3.2`
- WebApi: `NLog 5.3.4` + `NLog.Extensions.Logging 5.3.14` + `NLog.Web.AspNetCore 5.3.14`

#### 升级方案

```bash
# 统一到 5.3.14+
dotnet add src/Wms.Core.Infrastructure/Wms.Core.Infrastructure.csproj package NLog --version 5.3.14
dotnet add src/Wms.Core.Infrastructure/Wms.Core.Infrastructure.csproj package NLog.Extensions.Logging --version 5.3.14
```

### 3.3 🟡 已停更的包

| 包 | 当前版本 | 状态 | 建议 |
|----|---------|------|------|
| `Microsoft.AspNetCore.Mvc.Versioning` | 5.1.0 | 已停更 | 迁移到 `Asp.Versioning.Mvc`（包名变化，API 兼容） |
| `AspNetCoreRateLimit` | 5.0.0 | 已归档 | 评估迁移到 .NET 7+ 内置 `AddRateLimiter` |

#### Microsoft.AspNetCore.Mvc.Versioning 迁移

```bash
# 1. 移除旧包
dotnet remove src/Wms.Core.WebApi/Wms.Core.WebApi.csproj package Microsoft.AspNetCore.Mvc.Versioning

# 2. 安装新包
dotnet add src/Wms.Core.WebApi/Wms.Core.WebApi.csproj package Asp.Versioning.Mvc

# 3. 修改 using
# 旧：using Microsoft.AspNetCore.Mvc.Versioning;
# 新：using Asp.Versioning;
```

## 4. 后端依赖安全扫描

### 4.1 使用 dotnet list package --vulnerable

```bash
cd Wms.Net8

# 扫描已知漏洞
dotnet list src/Wms.Core.WebApi/Wms.Core.WebApi.csproj package --vulnerable

# 扫描过时包
dotnet list src/Wms.Core.WebApi/Wms.Core.WebApi.csproj package --outdated

# 扫描整个解决方案
dotnet list Wms.Net8.sln package --vulnerable
```

### 4.2 建议添加到 CI/CD

```yaml
# .github/workflows/security-scan.yml
name: Security Scan
on: [push, pull_request]
jobs:
  dotnet-scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      - run: dotnet list Wms.Net8/Wms.Net8.sln package --vulnerable --include-transitive
```

## 5. 前端依赖安全扫描

### 5.1 使用 pnpm audit

```bash
cd Wms.Vue

# 扫描已知漏洞
pnpm audit

# 仅显示严重和高危
pnpm audit --audit-level high

# 自动修复（谨慎使用）
pnpm audit --fix
```

### 5.2 建议添加到 CI/CD

```yaml
# .github/workflows/security-scan.yml
jobs:
  pnpm-scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: pnpm/action-setup@v2
        with:
          version: 8
      - uses: actions/setup-node@v3
        with:
          node-version: '20'
          cache: 'pnpm'
          cache-dependency-path: Wms.Vue/pnpm-lock.yaml
      - run: cd Wms.Vue && pnpm install --frozen-lockfile
      - run: cd Wms.Vue && pnpm audit --audit-level high
```

## 6. 升级测试清单

每次依赖升级后，执行以下测试：

### 6.1 编译测试

```bash
# 后端
dotnet build Wms.Net8/Wms.Net8.sln
# 应：0 error, 0 warning

# 前端
cd Wms.Vue && pnpm build
# 应：构建成功
```

### 6.2 单元测试

```bash
# 后端
dotnet test Wms.Net8/Wms.Net8.sln

# 前端
cd Wms.Vue && pnpm test
```

### 6.3 关键功能测试

| 模块 | 测试项 | 验证 |
|------|--------|------|
| 物料导入 | Excel 文件上传解析 | 数据正确入库 |
| 报表导出 | Excel 文件下载 | 文件能正常打开 |
| 富文本编辑 | 内容保存/回显 | HTML 正确渲染 |
| 后台任务 | Hangfire 任务执行 | 任务状态正常 |
| API 版本 | v1/v2 端点访问 | 路由正确 |
| 限流 | 登录限流生效 | 失败 N 次后锁定 |
| 日志 | NLog 写入 | 日志正常生成 |

### 6.4 性能回归（重要升级后）

```bash
# 使用 Apache Bench 或 k6 做性能压测
ab -n 1000 -c 50 https://api.example.com/api/v1/materials

# 对比升级前后的 RPS 和 P99 延迟，应无明显回归
```

## 7. 紧急回滚流程

### 7.1 后端回滚

```bash
# 1. 还原 csproj 文件
git diff HEAD~1 -- '*.csproj'  # 查看变更
git checkout HEAD~1 -- src/Wms.Core.WebApi/Wms.Core.WebApi.csproj
git checkout HEAD~1 -- src/Wms.Core.Infrastructure/Wms.Core.Infrastructure.csproj

# 2. 还原包
dotnet restore

# 3. 重新编译
dotnet build

# 4. 部署
```

### 7.2 前端回滚

```bash
# 1. 还原 package.json 和 pnpm-lock.yaml
git checkout HEAD~1 -- Wms.Vue/package.json Wms.Vue/pnpm-lock.yaml

# 2. 重新安装
cd Wms.Vue && pnpm install

# 3. 重新构建
pnpm build

# 4. 部署
```

## 8. 升级日志模板

每次升级维护时记录：

```
## 升级日期：YYYY-MM-DD

### 升级内容
- 包名：旧版本 → 新版本
- 原因：CVE-XXXX-XXXXX / 性能优化 / 功能需要

### 测试结果
- [x] 编译通过
- [x] 单测通过
- [x] 功能测试
- [x] 性能回归

### 知名问题
- 无 / 描述问题

### 执行人
- 姓名
```

## 9. 长期建议

1. **建立每月依赖扫描机制**：使用 Dependabot / Renovate Bot 自动检查
2. **强制 CI 通过**：依赖漏洞扫描结果阻断 PR 合并
3. **关注 CVE 通知**：订阅 NVD/NVD Feed，关注使用的包是否有新漏洞
4. **包锁定文件入库**：确保 `pnpm-lock.yaml` 和 `packages.lock.json` 在版本控制中
5. **避免使用 npm registry 中停止维护的包**：优先选择活跃维护的替代品
