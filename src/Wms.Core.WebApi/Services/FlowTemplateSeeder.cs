using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Constants;
using Wms.Core.Domain.Entities.Flow;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.WebApi.Services;

/// <summary>
/// 流程模板种子数据 — 预设常用流程模板
/// </summary>
public static class FlowTemplateSeeder
{
    /// <summary>
    /// 种子预设模板（按 Code 匹配，不存在则插入，已存在则同步节点）
    /// 节点操作全部使用 raw SQL，绕过 EF Core change tracker
    /// </summary>
    public static async Task SeedAsync(WmsDbContext db, ILogger? logger = null)
    {
        var templates = BuildTemplates();
        logger?.LogInformation("[Seeder] ★★★ 开始同步流程模板，共 {Count} 个模板 ★★★", templates.Count);

        foreach (var template in templates)
        {
            var existing = await db.FlowTemplates
                .FirstOrDefaultAsync(t => t.Code == template.Code);

            if (existing == null)
            {
                // 不存在：新增模板（含节点），通过 EF Core 一次性插入
                template.CreatedTime = DateTime.UtcNow;
                template.IsBuiltIn = true;
                db.FlowTemplates.Add(template);
                logger?.LogInformation("[Seeder] 新增模板: {Code}，节点数={NodeCount}", template.Code, template.Nodes?.Count ?? 0);
            }
            else
            {
                // 已存在：用 raw SQL 删除旧节点 + raw SQL 插入新节点（绕过 EF Core change tracker）
                existing.IsBuiltIn = true;
                logger?.LogInformation("[Seeder] 同步模板: {Code} (Id={Id})", template.Code, existing.Id);

                // 1. 删除旧节点（raw SQL）
                var deleteResult = await db.Database.ExecuteSqlRawAsync(
                    $"DELETE FROM FlowNodes WHERE TemplateId = {existing.Id}");
                logger?.LogInformation("[Seeder] 删除旧节点: {DeletedCount} rows", deleteResult);

                // 2. 插入新节点（raw SQL，逐条插入确保可靠）
                var newNodes = template.Nodes!.OrderBy(n => n.StepOrder).ToList();
                foreach (var node in newNodes)
                {
                    var sql = BuildInsertNodeSql(existing.Id, node);
                    await db.Database.ExecuteSqlRawAsync(sql);
                }
                logger?.LogInformation("[Seeder] raw SQL 插入新节点: {Count} 个", newNodes.Count);

                // 3. 验证插入结果
                var actualCount = await db.FlowNodes.CountAsync(n => n.TemplateId == existing.Id);
                var actualNodes = await db.FlowNodes
                    .Where(n => n.TemplateId == existing.Id)
                    .OrderBy(n => n.StepOrder)
                    .Select(n => new { n.NodeType, n.StepOrder })
                    .ToListAsync();

                logger?.LogInformation("[Seeder] 验证 TemplateId={Id}: DB节点数={ActualCount}, 期望={ExpectedCount}",
                    existing.Id, actualCount, newNodes.Count);
                foreach (var n in actualNodes)
                    logger?.LogInformation("[Seeder]   DB节点 StepOrder={O}: {Type}", n.StepOrder, n.NodeType);

                if (actualCount != newNodes.Count)
                {
                    logger?.LogError("[Seeder] ★★★ 节点数不匹配！期望={Expected}, 实际={Actual}, 模板={Code} ★★★",
                        newNodes.Count, actualCount, template.Code);
                }
            }
        }

        // 只 SaveChanges EF Core 追踪的实体（新增模板的情况）
        await db.SaveChangesAsync();

        // 清理残留模板：同一 Category+Phase 下非 BuiltIn 的旧模板，停用并删除其节点
        var seedPairs = templates.Select(t => new { t.Category, t.Phase }).Distinct().ToList();
        foreach (var pair in seedPairs)
        {
            var stale = await db.FlowTemplates
                .Include(t => t.Nodes)
                .Where(t => t.Category == pair.Category && t.Phase == pair.Phase && !t.IsBuiltIn)
                .ToListAsync();

            foreach (var s in stale)
            {
                logger?.LogWarning("[Seeder] 清理残留模板: {Name}(Id={Id}, Code={Code}), Category={Cat}, Phase={Phase}",
                    s.Name, s.Id, s.Code ?? "(null)", pair.Category, pair.Phase);
                if (s.Nodes?.Count > 0)
                    await db.Database.ExecuteSqlRawAsync($"DELETE FROM FlowNodes WHERE TemplateId = {s.Id}");
                s.IsActive = false;
            }
        }
        await db.SaveChangesAsync();

        logger?.LogInformation("[Seeder] ★★★ 流程模板同步完成 ★★★");
    }

    /// <summary>
    /// 生成插入节点的 raw SQL（所有值来自硬编码种子数据，无 SQL 注入风险）
    /// </summary>
    /// <summary>
    /// 转义 SQL 字符串值：单引号 → 两个单引号（SQL 安全），花括号 → 双花括号（避免 ExecuteSqlRawAsync 的 Format 解析）
    /// </summary>
    private static string EscapeForFormat(string value)
    {
        return value.Replace("'", "''").Replace("{", "{{").Replace("}", "}}");
    }

    private static string BuildInsertNodeSql(int templateId, FlowNode node)
    {
        var configJson = node.ConfigJson != null ? "'" + EscapeForFormat(node.ConfigJson) + "'" : "NULL";
        var onFailure = node.OnFailure != null ? $"'{node.OnFailure}'" : "NULL";
        var skipCondition = node.SkipCondition != null ? "'" + EscapeForFormat(node.SkipCondition) + "'" : "NULL";
        var enabled = node.IsEnabled ? 1 : 0;
        var txBoundary = node.IsTransactionBoundary ? 1 : 0;

        return $"""
            INSERT INTO FlowNodes (TemplateId, NodeType, NodeName, StepOrder, ConfigJson, IsEnabled, OnFailure, SkipCondition, IsTransactionBoundary)
            VALUES ({templateId}, '{node.NodeType}', '{node.NodeName}', {node.StepOrder}, {configJson}, {enabled}, {onFailure}, {skipCondition}, {txBoundary})
            """;
    }

    private static List<FlowTemplate> BuildTemplates()
    {
        var list = new List<FlowTemplate>();

        // ========== 标准入库（请求阶段）==========
        list.Add(new FlowTemplate
        {
            Name = "标准入库",
            Code = "INBOUND_STANDARD_REQUEST",
            Category = "入库",
            Phase = Cst.PhaseRequest,
            Description = "标准入库请求流程：验证参数→查托盘→检查状态→工艺匹配→分配货位→创建任务→下发WCS",
            IsActive = true,
            IsBuiltIn = false, // 由 SeedAsync 设置
            SortOrder = 1,
            Priority = 10,
            MatchRules = """{"requestType":"入库","priority":10}""",
            Nodes = new List<FlowNode>
            {
                new() { NodeType = "ValidateParams", NodeName = "验证参数", StepOrder = 1, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "FindUnitload", NodeName = "查托盘", StepOrder = 2, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "CheckUnitloadStatus", NodeName = "检查托盘状态", StepOrder = 3, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "MatchTag", NodeName = "工艺匹配", StepOrder = 4, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "CheckLocationLimit", NodeName = "检查库位限制", StepOrder = 5, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "AllocateLocation", NodeName = "分配货位", StepOrder = 6, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "CreateTransTask", NodeName = "创建运输任务", StepOrder = 7, IsEnabled = true, OnFailure = "Stop", IsTransactionBoundary = true },
                new() { NodeType = "UpdateUnitload", NodeName = "更新托盘状态", StepOrder = 8, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "UpdateLocationCount", NodeName = "更新库位计数", StepOrder = 9, IsEnabled = true, OnFailure = "Skip" },
                new() { NodeType = "SendWcsTask", NodeName = "下发WCS", StepOrder = 10, IsEnabled = true, OnFailure = "Stop" },
            }
        });

        // ========== 标准入库（完成阶段）==========
        list.Add(new FlowTemplate
        {
            Name = "标准入库完成",
            Code = "INBOUND_STANDARD_COMPLETION",
            Category = "入库",
            Phase = Cst.PhaseCompletion,
            Description = "入库完成流程：更新Unitload→工序推进→更新库位→记录流水→归档",
            IsActive = true,
            SortOrder = 1,
            Priority = 10,
            MatchRules = """{"requestType":"入库","phase":"Completion"}""",
            Nodes = new List<FlowNode>
            {
                new() { NodeType = "UpdateUnitload", NodeName = "更新托盘到位", StepOrder = 1, IsEnabled = true, OnFailure = "Stop", IsTransactionBoundary = true },
                new() { NodeType = "AdvanceOperation", NodeName = "工序推进", StepOrder = 2, IsEnabled = true, OnFailure = "Skip" },
                new() { NodeType = "UpdateLocationCount", NodeName = "更新库位计数", StepOrder = 3, IsEnabled = true, OnFailure = "Skip" },
                new() { NodeType = "RecordFlow", NodeName = "记录流水", StepOrder = 4, IsEnabled = true, OnFailure = "Skip" },
                new() { NodeType = "ArchiveTask", NodeName = "归档任务", StepOrder = 5, IsEnabled = true, OnFailure = "Stop" },
            }
        });

        // ========== 入库双叉（请求阶段）==========
        list.Add(new FlowTemplate
        {
            Name = "入库双叉",
            Code = "INBOUND_DOUBLE_REQUEST",
            Category = "入库双叉",
            Phase = Cst.PhaseRequest,
            Description = "入库双叉请求流程：多容器逐个处理，第二个容器优先分配同层邻列货位",
            IsActive = true,
            SortOrder = 2,
            Priority = 10,
            MatchRules = """{"requestType":"入库双叉","priority":10}""",
            Nodes = new List<FlowNode>
            {
                new() { NodeType = "ValidateParams", NodeName = "验证参数", StepOrder = 1, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "FindUnitload", NodeName = "查托盘", StepOrder = 2, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "CheckUnitloadStatus", NodeName = "检查托盘状态", StepOrder = 3, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "MatchTag", NodeName = "工艺匹配", StepOrder = 4, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "CheckLocationLimit", NodeName = "检查库位限制", StepOrder = 5, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "AllocateLocation", NodeName = "分配货位", StepOrder = 6, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "CreateTransTask", NodeName = "创建运输任务", StepOrder = 7, IsEnabled = true, OnFailure = "Stop", IsTransactionBoundary = true },
                new() { NodeType = "UpdateUnitload", NodeName = "更新托盘状态", StepOrder = 8, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "UpdateLocationCount", NodeName = "更新库位计数", StepOrder = 9, IsEnabled = true, OnFailure = "Skip" },
                new() { NodeType = "SendWcsTask", NodeName = "下发WCS", StepOrder = 10, IsEnabled = true, OnFailure = "Stop" },
            }
        });

        // ========== 入库双叉（完成阶段）==========
        list.Add(new FlowTemplate
        {
            Name = "入库双叉完成",
            Code = "INBOUND_DOUBLE_COMPLETION",
            Category = "入库双叉",
            Phase = Cst.PhaseCompletion,
            Description = "入库双叉完成流程：更新Unitload→工序推进→更新库位→记录流水→归档",
            IsActive = true,
            SortOrder = 2,
            Priority = 10,
            MatchRules = """{"requestType":"入库双叉","phase":"Completion"}""",
            Nodes = new List<FlowNode>
            {
                new() { NodeType = "UpdateUnitload", NodeName = "更新托盘到位", StepOrder = 1, IsEnabled = true, OnFailure = "Stop", IsTransactionBoundary = true },
                new() { NodeType = "AdvanceOperation", NodeName = "工序推进", StepOrder = 2, IsEnabled = true, OnFailure = "Skip" },
                new() { NodeType = "UpdateLocationCount", NodeName = "更新库位计数", StepOrder = 3, IsEnabled = true, OnFailure = "Skip" },
                new() { NodeType = "RecordFlow", NodeName = "记录流水", StepOrder = 4, IsEnabled = true, OnFailure = "Skip" },
                new() { NodeType = "ArchiveTask", NodeName = "归档任务", StepOrder = 5, IsEnabled = true, OnFailure = "Stop" },
            }
        });

        // ========== 标准出库（请求阶段）==========
        list.Add(new FlowTemplate
        {
            Name = "标准出库",
            Code = "OUTBOUND_STANDARD_REQUEST",
            Category = "出库",
            Phase = Cst.PhaseRequest,
            Description = "标准出库请求流程：验证参数→查托盘→检查状态→创建任务→下发WCS",
            IsActive = true,
            SortOrder = 1,
            Priority = 10,
            MatchRules = """{"requestType":"出库","priority":10}""",
            Nodes = new List<FlowNode>
            {
                new() { NodeType = "ValidateParams", NodeName = "验证参数", StepOrder = 1, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "FindUnitload", NodeName = "查托盘", StepOrder = 2, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "CheckUnitloadStatus", NodeName = "检查托盘状态", StepOrder = 3, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "CreateTransTask", NodeName = "创建运输任务", StepOrder = 4, IsEnabled = true, OnFailure = "Stop", IsTransactionBoundary = true },
                new() { NodeType = "UpdateUnitload", NodeName = "更新托盘状态", StepOrder = 5, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "UpdateLocationCount", NodeName = "更新库位计数", StepOrder = 6, IsEnabled = true, OnFailure = "Skip" },
                new() { NodeType = "SendWcsTask", NodeName = "下发WCS", StepOrder = 7, IsEnabled = true, OnFailure = "Stop" },
            }
        });

        // ========== 标准出库（完成阶段）==========
        list.Add(new FlowTemplate
        {
            Name = "标准出库完成",
            Code = "OUTBOUND_STANDARD_COMPLETION",
            Category = "出库",
            Phase = Cst.PhaseCompletion,
            Description = "出库完成流程：记录流水→拆盘→归档",
            IsActive = true,
            SortOrder = 1,
            Priority = 10,
            MatchRules = """{"requestType":"出库","phase":"Completion"}""",
            Nodes = new List<FlowNode>
            {
                new() { NodeType = "RecordFlow", NodeName = "记录出库流水", StepOrder = 1, IsEnabled = true, OnFailure = "Skip" },
                new() { NodeType = "UpdateUnitload", NodeName = "重置托盘状态", StepOrder = 2, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "UpdateLocationCount", NodeName = "更新库位计数", StepOrder = 3, IsEnabled = true, OnFailure = "Skip" },
                new() { NodeType = "SplitUnitload", NodeName = "拆盘归档", StepOrder = 4, IsEnabled = true, OnFailure = "Skip" },
                new() { NodeType = "ArchiveTask", NodeName = "归档任务", StepOrder = 5, IsEnabled = true, OnFailure = "Stop" },
            }
        });

        // ========== 标准移库（请求阶段）==========
        list.Add(new FlowTemplate
        {
            Name = "标准移库",
            Code = "MOVE_STANDARD_REQUEST",
            Category = "移库",
            Phase = Cst.PhaseRequest,
            Description = "标准移库请求流程：验证参数→查托盘→检查状态→检查库位限制→分配货位→创建任务→下发WCS",
            IsActive = false, // 默认关闭，启用后替代硬编码 Handler
            SortOrder = 1,
            Priority = 10,
            MatchRules = """{"requestType":"移库","priority":10}""",
            Nodes = new List<FlowNode>
            {
                new() { NodeType = "ValidateParams", NodeName = "验证参数", StepOrder = 1, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "FindUnitload", NodeName = "查托盘", StepOrder = 2, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "CheckUnitloadStatus", NodeName = "检查托盘状态", StepOrder = 3, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "CheckLocationLimit", NodeName = "检查库位限制", StepOrder = 4, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "AllocateLocation", NodeName = "分配货位", StepOrder = 5, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "CreateTransTask", NodeName = "创建运输任务", StepOrder = 6, IsEnabled = true, OnFailure = "Stop", IsTransactionBoundary = true },
                new() { NodeType = "UpdateUnitload", NodeName = "更新托盘状态", StepOrder = 7, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "UpdateLocationCount", NodeName = "更新库位计数", StepOrder = 8, IsEnabled = true, OnFailure = "Skip" },
                new() { NodeType = "SendWcsTask", NodeName = "下发WCS", StepOrder = 9, IsEnabled = true, OnFailure = "Stop" },
            }
        });

        return list;
    }
}
