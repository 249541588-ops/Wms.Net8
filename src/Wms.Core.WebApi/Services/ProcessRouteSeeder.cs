using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Entities.ProcessRoute;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.WebApi.Services;

/// <summary>
/// 工艺路线种子数据 — 预设默认工艺路线
/// </summary>
public static class ProcessRouteSeeder
{
    private const string RouteCode = "DEFAULT_ROUTE";

    /// <summary>
    /// 期望的转移定义（12条）—— 创建和补齐共用
    /// </summary>
    private static readonly (string From, string To, string Type, bool IsDefault, int SortOrder)[] TransitionDefinitions =
    {
        // 两个重组装盘的跳跃（侧支汇合到主线，类型为 Branch）
        ("化成重组装盘", "化成",         "Branch", false, 1),
        ("分容重组装盘", "分容",         "Branch", false, 2),
        // 主线序列
        ("一注装盘",    "高温浸润",      "Sequential", false, 3),
        ("高温浸润",    "化成",          "Sequential", false, 4),
        ("化成",        "清洗装盘",      "Sequential", false, 5),
        ("清洗装盘",    "分容",          "Sequential", false, 6),
        ("分容",        "常温一天",      "Sequential", false, 7),
        ("常温一天",    "OCV3",         "Sequential", false, 8),
        ("OCV3",       "常温七天",      "Sequential", false, 9),
        ("常温七天",    "OCV4",         "Sequential", false, 10),
        ("OCV4",       "分档",          "Sequential", false, 11),
        ("分档",        "成品",          "Sequential", false, 12),
    };

    /// <summary>
    /// 种子默认工艺路线（按 Code 匹配，不存在则创建，已存在则检查并补齐转移）
    /// </summary>
    public static async Task SeedAsync(WmsDbContext db, ILogger? logger = null)
    {
        logger?.LogInformation("[Seeder] ★★★ 开始同步工艺路线种子数据 ★★★");

        var existing = await db.ProcessRoutes
            .FirstOrDefaultAsync(r => r.Code == RouteCode);

        if (existing != null)
        {
            logger?.LogInformation("[Seeder] 工艺路线 {Code} 已存在(Id={Id})，检查并补齐转移", RouteCode, existing.ProcessRouteId);
            await RepairTransitionsAsync(db, existing.ProcessRouteId, logger);
            return;
        }

        await CreateNewRouteAsync(db, logger);
    }

    /// <summary>
    /// 创建全新的默认工艺路线（路线不存在时调用）
    /// </summary>
    private static async Task CreateNewRouteAsync(WmsDbContext db, ILogger? logger)
    {
        // 1. 创建路线
        var route = new ProcessRoute
        {
            Code = RouteCode,
            Name = "默认工艺路线",
            Description = "从硬编码工序序列导入的默认路线",
            IsBuiltIn = true,
            IsActive = true,
            CurrentVersion = 1,
            SortOrder = 1,
            Priority = 10,
            CreatedTime = DateTime.UtcNow,
            CreatedBy = "System"
        };
        db.ProcessRoutes.Add(route);
        await db.SaveChangesAsync();
        logger?.LogInformation("[Seeder] 新增工艺路线: {Code}(Id={Id})", route.Code, route.ProcessRouteId);

        // 2. 创建版本
        var version = new ProcessRouteVersion
        {
            ProcessRouteId = route.ProcessRouteId,
            Version = 1,
            Status = "Published",
            ChangeLog = "初始导入",
            PublishedTime = DateTime.UtcNow,
            PublishedBy = "System",
            CreatedTime = DateTime.UtcNow,
            CreatedBy = "System"
        };
        db.ProcessRouteVersions.Add(version);
        await db.SaveChangesAsync();
        logger?.LogInformation("[Seeder] 新增版本: v{Version}(Id={Id}), Status={Status}", version.Version, version.Id, version.Status);

        // 3. 创建步骤（14个，对应 Unitload_Enum.CurrentOperation 枚举）
        var steps = new List<ProcessRouteStep>();

        var stepDefinitions = new[]
        {
            //new { Code = "空托绑盘",     DisplayName = "空托绑盘",     SortOrder = 1  },
            new { Code = "化成重组装盘",  DisplayName = "化成重组装盘",  SortOrder = 1  },
            new { Code = "分容重组装盘",  DisplayName = "分容重组装盘",  SortOrder = 2  },
            new { Code = "一注装盘",     DisplayName = "一注装盘",     SortOrder = 3  },
            new { Code = "高温浸润",     DisplayName = "高温浸润",     SortOrder = 4  },
            new { Code = "化成",         DisplayName = "化成",         SortOrder = 5  },
            new { Code = "清洗装盘",     DisplayName = "清洗装盘",     SortOrder = 6  },
            new { Code = "分容",         DisplayName = "分容",         SortOrder = 7  },
            new { Code = "常温一天",     DisplayName = "常温一天",     SortOrder = 8  },
            new { Code = "OCV3",        DisplayName = "OCV3",        SortOrder = 9 },
            new { Code = "常温七天",     DisplayName = "常温七天",     SortOrder = 10 },
            new { Code = "OCV4",        DisplayName = "OCV4",        SortOrder = 11 },
            new { Code = "分档",         DisplayName = "分档",         SortOrder = 12 },
            new { Code = "成品",         DisplayName = "成品",         SortOrder = 13 },
        };

        foreach (var def in stepDefinitions)
        {
            var step = new ProcessRouteStep
            {
                VersionId = version.Id,
                OperationCode = def.Code,
                DisplayName = def.DisplayName,
                IsStart = false,
                IsEnd = false,
                StepType = "Normal",
                SortOrder = def.SortOrder,
                CreatedTime = DateTime.UtcNow,
                CreatedBy = "System"
            };
            steps.Add(step);
            db.ProcessRouteSteps.Add(step);
        }

        // 设置起始/终止步骤
        steps[2].IsStart = true;  // 化成重组装盘
        steps[2].StepType = "Start";
        steps[12].IsEnd = true;   // 成品
        steps[12].StepType = "End";

        await db.SaveChangesAsync();
        logger?.LogInformation("[Seeder] 新增步骤: {Count} 个", steps.Count);

        // 4. 创建转移（15条）
        var stepMap = steps.ToDictionary(s => s.OperationCode, s => s.Id);
        foreach (var (from, to, type, isDefault, sortOrder) in TransitionDefinitions)
        {
            db.ProcessRouteTransitions.Add(new ProcessRouteTransition
            {
                VersionId = version.Id,
                FromStepId = stepMap[from],
                ToStepId = stepMap[to],
                TransitionType = type,
                IsDefault = isDefault,
                SortOrder = sortOrder,
                CreatedTime = DateTime.UtcNow,
                CreatedBy = "System"
            });
        }

        await db.SaveChangesAsync();
        logger?.LogInformation("[Seeder] 新增转移: {Count} 条", TransitionDefinitions.Length);

        logger?.LogInformation("[Seeder] ★★★ 工艺路线种子数据同步完成 ★★★");
    }

    /// <summary>
    /// 补齐已存在路线的转移关系（路线已存在时调用）
    /// 对比期望的15条转移，新增缺失的，更新属性不一致的，保留多余的。
    /// 不会删除任何已有转移，避免影响已绑定托盘的分支选择。
    /// </summary>
    private static async Task RepairTransitionsAsync(WmsDbContext db, int routeId, ILogger? logger)
    {
        // 获取 Published 版本（默认取 v1）
        var version = await db.ProcessRouteVersions
            .FirstOrDefaultAsync(v => v.ProcessRouteId == routeId && v.Status == "Published");

        if (version == null)
        {
            logger?.LogWarning("[Seeder] 路线 {RouteId} 没有 Published 版本，跳过补齐", routeId);
            return;
        }

        // 获取步骤，构建 OperationCode -> StepId 映射
        var steps = await db.ProcessRouteSteps
            .Where(s => s.VersionId == version.Id)
            .ToListAsync();
        var stepMap = steps.ToDictionary(s => s.OperationCode, s => s.Id);

        // 检查所有涉及的步骤是否存在
        var requiredOperations = new HashSet<string>();
        foreach (var (from, to, _, _, _) in TransitionDefinitions)
        {
            requiredOperations.Add(from);
            requiredOperations.Add(to);
        }
        var missing = requiredOperations.Where(op => !stepMap.ContainsKey(op)).ToList();
        if (missing.Count > 0)
        {
            logger?.LogWarning("[Seeder] 路线 {RouteId} 缺少步骤 [{Ops}]，无法补齐转移", routeId, string.Join(", ", missing));
            return;
        }

        // 获取已存在的转移
        var existingTransitions = await db.ProcessRouteTransitions
            .Where(t => t.VersionId == version.Id)
            .ToListAsync();

        int added = 0, updated = 0;

        foreach (var (from, to, type, isDefault, sortOrder) in TransitionDefinitions)
        {
            var fromStepId = stepMap[from];
            var toStepId = stepMap[to];

            var existing = existingTransitions.FirstOrDefault(t => t.FromStepId == fromStepId && t.ToStepId == toStepId);

            if (existing == null)
            {
                // 新增缺失的转移
                db.ProcessRouteTransitions.Add(new ProcessRouteTransition
                {
                    VersionId = version.Id,
                    FromStepId = fromStepId,
                    ToStepId = toStepId,
                    TransitionType = type,
                    IsDefault = isDefault,
                    SortOrder = sortOrder,
                    CreatedTime = DateTime.UtcNow,
                    CreatedBy = "System"
                });
                added++;
                logger?.LogInformation("[Seeder] 补齐转移: {From} → {To} ({Type}, Default={IsDefault})", from, to, type, isDefault);
            }
            else
            {
                // 更新属性不一致的转移
                bool changed = false;
                if (existing.TransitionType != type) { existing.TransitionType = type; changed = true; }
                if (existing.IsDefault != isDefault) { existing.IsDefault = isDefault; changed = true; }
                if (existing.SortOrder != sortOrder) { existing.SortOrder = sortOrder; changed = true; }

                if (changed)
                {
                    updated++;
                    logger?.LogInformation("[Seeder] 更新转移: {From} → {To} (Type={Type}, Default={IsDefault}, SortOrder={SortOrder})", from, to, type, isDefault, sortOrder);
                }
            }
        }

        if (added > 0 || updated > 0)
        {
            await db.SaveChangesAsync();
            logger?.LogInformation("[Seeder] 路线 {RouteId} 转移补齐完成：新增 {Added} 条，更新 {Updated} 条", routeId, added, updated);
            logger?.LogInformation("[Seeder] ⚠️ 提示：如果应用已在运行，请清除路线图缓存（重启或刷新缓存）以获取最新数据");
        }
        else
        {
            logger?.LogInformation("[Seeder] 路线 {RouteId} 转移已完整，无需补齐", routeId);
        }

        logger?.LogInformation("[Seeder] ★★★ 工艺路线种子数据同步完成 ★★★");
    }
}
