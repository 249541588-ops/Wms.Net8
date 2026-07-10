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
                // 已存在：增量同步节点，保留用户修改的字段（IsEnabled、OnFailure、IsTransactionBoundary、ConfigJson）
                // 软删除的节点（IsDeleted=true）不会被恢复
                existing.IsBuiltIn = true;
                logger?.LogInformation("[Seeder] 同步模板: {Code} (Id={Id})", template.Code, existing.Id);

                // 加载所有节点（含已软删除的），用于区分"用户删除"和"种子新增"
                // GroupBy 去重：同 NodeType 多条记录时优先取未删除的
                var existingNodes = await db.FlowNodes
                    .Where(n => n.TemplateId == existing.Id)
                    .GroupBy(n => n.NodeType)
                    .Select(g => g.OrderBy(n => n.IsDeleted ? 0 : 1).First())
                    .ToDictionaryAsync(n => n.NodeType);

                var seedNodes = template.Nodes!.OrderBy(n => n.StepOrder).ToList();
                int inserted = 0, updated = 0, skipped = 0;

                foreach (var seedNode in seedNodes)
                {
                    if (existingNodes.TryGetValue(seedNode.NodeType, out var dbNode))
                    {
                        if (dbNode.IsDeleted)
                        {
                            // 用户已软删除此节点，跳过不同步
                            skipped++;
                            continue;
                        }
                        // 已有活跃节点：只同步结构字段，保留用户配置
                        dbNode.NodeName = seedNode.NodeName;
                        dbNode.StepOrder = seedNode.StepOrder;
                        dbNode.IsPostTransaction = seedNode.IsPostTransaction;
                        if (!string.IsNullOrEmpty(seedNode.SkipCondition))
                            dbNode.SkipCondition = seedNode.SkipCondition;
                        updated++;
                    }
                    else
                    {
                        // DB 中完全不存在此 NodeType → 种子新增节点，插入
                        dbNode = new FlowNode
                        {
                            TemplateId = existing.Id,
                            NodeType = seedNode.NodeType,
                            NodeName = seedNode.NodeName,
                            StepOrder = seedNode.StepOrder,
                            IsEnabled = seedNode.IsEnabled,
                            OnFailure = seedNode.OnFailure,
                            ConfigJson = seedNode.ConfigJson,
                            IsTransactionBoundary = seedNode.IsTransactionBoundary,
                            IsPostTransaction = seedNode.IsPostTransaction,
                            SkipCondition = seedNode.SkipCondition
                        };
                        db.FlowNodes.Add(dbNode);
                        inserted++;
                    }
                }

                logger?.LogInformation("[Seeder] 同步完成: 新增={Inserted}, 更新={Updated}, 跳过(已删除)={Skipped}",
                    inserted, updated, skipped);
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
                    await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM FlowNodes WHERE TemplateId = {s.Id}");
                s.IsActive = false;
            }
        }
        await db.SaveChangesAsync();

        logger?.LogInformation("[Seeder] ★★★ 流程模板同步完成 ★★★");
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
                new() { NodeType = "UpdateLocationCount", NodeName = "更新库位计数", StepOrder = 9, IsEnabled = true, OnFailure = "Skip", IsTransactionBoundary = true },
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
            Description = "入库完成流程：更新Unitload→工序推进→更新库位→记录流水→归档→MES上传→杭可通知",
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
                new() { NodeType = "UploadMes", NodeName = "上传MES", StepOrder = 6, IsEnabled = true, OnFailure = "Skip", IsPostTransaction = true },
                new() { NodeType = "NotifyHangKe", NodeName = "通知杭可", StepOrder = 7, IsEnabled = true, OnFailure = "Skip", IsPostTransaction = true },
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
                new() { NodeType = "UpdateLocationCount", NodeName = "更新库位计数", StepOrder = 9, IsEnabled = true, OnFailure = "Skip", IsTransactionBoundary = true },
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
            Description = "入库双叉完成流程：更新Unitload→工序推进→更新库位→记录流水→归档→MES上传→杭可通知",
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
                new() { NodeType = "UploadMes", NodeName = "上传MES", StepOrder = 6, IsEnabled = true, OnFailure = "Skip", IsPostTransaction = true },
                new() { NodeType = "NotifyHangKe", NodeName = "通知杭可", StepOrder = 7, IsEnabled = true, OnFailure = "Skip", IsPostTransaction = true },
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
                new() { NodeType = "UpdateLocationCount", NodeName = "更新库位计数", StepOrder = 6, IsEnabled = true, OnFailure = "Skip", IsTransactionBoundary = true },
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
            Description = "出库完成流程：记录流水→拆盘→归档→MES上传→杭可通知",
            IsActive = true,
            SortOrder = 1,
            Priority = 10,
            MatchRules = """{"requestType":"出库","phase":"Completion"}""",
            Nodes = new List<FlowNode>
            {
                new() { NodeType = "RecordFlow", NodeName = "记录出库流水", StepOrder = 1, IsEnabled = true, OnFailure = "Skip" },
                new() { NodeType = "UpdateUnitload", NodeName = "重置托盘状态", StepOrder = 2, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "CleanupEmptyTray", NodeName = "清理空托盘", StepOrder = 3, IsEnabled = true, OnFailure = "Skip" },
                new() { NodeType = "UpdateLocationCount", NodeName = "更新库位计数", StepOrder = 4, IsEnabled = true, OnFailure = "Skip" },
                new() { NodeType = "SplitUnitload", NodeName = "拆盘归档", StepOrder = 5, IsEnabled = true, OnFailure = "Skip" },
                new() { NodeType = "ArchiveTask", NodeName = "归档任务", StepOrder = 6, IsEnabled = true, OnFailure = "Stop" },
                new() { NodeType = "UploadMes", NodeName = "上传MES", StepOrder = 7, IsEnabled = true, OnFailure = "Skip", IsPostTransaction = true },
                new() { NodeType = "NotifyHangKe", NodeName = "通知杭可", StepOrder = 8, IsEnabled = true, OnFailure = "Skip", IsPostTransaction = true },
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
                new() { NodeType = "UpdateLocationCount", NodeName = "更新库位计数", StepOrder = 8, IsEnabled = true, OnFailure = "Skip", IsTransactionBoundary = true },
                new() { NodeType = "SendWcsTask", NodeName = "下发WCS", StepOrder = 9, IsEnabled = true, OnFailure = "Stop" },
            }
        });

        // ========== 排废验证（请求阶段）==========
        list.Add(new FlowTemplate
        {
            Name = "排废验证",
            Code = "WASTE_DISPOSAL_REQUEST",
            Category = "排废",
            Phase = Cst.PhaseRequest,
            Description = "排废验证请求流程：验证容器→MES/杭可可选→组装排废数据",
            IsActive = false, // 默认关闭，手动启用后替代硬编码 Handler
            SortOrder = 1,
            Priority = 10,
            MatchRules = """{"requestType":"排废","priority":10}""",
            Nodes = new List<FlowNode>
            {
                new() { NodeType = "WasteDisposalRequest", NodeName = "排废验证", StepOrder = 1, IsEnabled = true, OnFailure = "Stop" },
            }
        });

        // ========== 排废完成处理（请求阶段）==========
        list.Add(new FlowTemplate
        {
            Name = "排废完成处理",
            Code = "WASTE_DISPOSAL_CAPTURE_REQUEST",
            Category = "排废更新",
            Phase = Cst.PhaseRequest,
            Description = "排废完成请求流程：删除NG电芯→级联清理→杭可注销",
            IsActive = false,
            SortOrder = 1,
            Priority = 10,
            MatchRules = """{"requestType":"排废更新","priority":10}""",
            Nodes = new List<FlowNode>
            {
                new() { NodeType = "WasteDisposalCapture", NodeName = "排废完成处理", StepOrder = 1, IsEnabled = true, OnFailure = "Stop" },
            }
        });

        return list;
    }
}
