# Platform DLL 同步目录

此目录存放从 `wms-platform` 底层仓库同步过来的 DLL。

## 同步方式

从 `wms-platform` 打包并复制：

```bash
# 在 wms-platform 目录打包
cd ..\wms-platform
.\pack.ps1 -Version 1.0.0

# 复制到本目录
copy artifacts\*.dll ..\Wms.Net8\lib\platform\
copy artifacts\*.pdb ..\Wms.Net8\lib\platform\ 2>$null  # 可选，DebugType=embedded 时不生成
copy artifacts\version.json ..\Wms.Net8\lib\platform\
```

或者后续使用 `sync-platform.ps1`（待编写）。

## 版本追踪

查看 [version.json](version.json) 了解当前 DLL 的版本、commit、build time。

## Git 策略

DLL 文件本身被 `.gitignore` 排除，不入库。version.json 入库作为版本参考。

## EmbedAllSources 说明

底层 DLL 编译时设置了 `EmbedAllSources=true` + `DebugType=embedded`，源码已嵌入 DLL 内部。
项目层开发者拿到 DLL 后：
- 没有 .cs 源文件（满足内部权限隔离诉求）
- VS 调试器 F11 步入底层代码时能看到源码行号（调试体验接近原生）
