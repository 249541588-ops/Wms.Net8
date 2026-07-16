using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Threading;
using Wms.Core.Application.DTOs;
using Wms.Core.Application.Ports;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.ProcessRoute;
using Wms.Core.Domain.ValueObjects;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.Infrastructure.Services;

/// <summary>
/// 工艺路线服务实现
/// </summary>
public class ProcessRouteService : IProcessRouteService
{
    private readonly WmsDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ProcessRouteService> _logger;

    /// <summary>
    /// 缓存版本号（static，所有实例共享，ClearCache 时递增使旧键失效）
    /// </summary>
    private static int _cacheVersion = 0;

    private const string GraphCachePrefix = "processroute:graph:v";
    private const string MaterialCachePrefix = "processroute:material:v";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public ProcessRouteService(
        WmsDbContext db,
        IMemoryCache cache,
        ILogger<ProcessRouteService> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<ProcessRouteVersion?> MatchRouteByMaterialAsync(int materialId)
    {
        if (materialId <= 0) return null;

        var cacheKey = $"{MaterialCachePrefix}{_cacheVersion}:{materialId}";
        if (_cache.TryGetValue(cacheKey, out int versionId))
        {
            return await _db.ProcessRouteVersions.FindAsync(versionId);
        }

        // 查询物料的绑定记录（按 Priority 降序）
        var binding = await _db.ProcessRouteMaterialBindings
            .Where(b => b.MaterialId == materialId && b.IsActive)
            .Join(_db.ProcessRoutes.Where(r => r.IsActive),
                b => b.ProcessRouteId, r => r.ProcessRouteId,
                (b, r) => new { Binding = b, Route = r })
            .OrderByDescending(x => x.Binding.Priority)
            .FirstOrDefaultAsync();

        if (binding == null) return null;

        // 获取该路线的最新 Published 版本
        var version = await _db.ProcessRouteVersions
            .Where(v => v.ProcessRouteId == binding.Route.ProcessRouteId
                && v.Status == "Published")
            .OrderByDescending(v => v.Version)
            .FirstOrDefaultAsync();

        if (version != null)
        {
            _cache.Set(cacheKey, version.Id, new MemoryCacheEntryOptions
            {
                SlidingExpiration = CacheDuration
            });
        }

        return version;
    }

    /// <inheritdoc/>
    public async Task<ProcessRouteGraph?> GetRouteGraphAsync(int versionId)
    {
        if (versionId <= 0) return null;

        var cacheKey = $"{GraphCachePrefix}{_cacheVersion}:{versionId}";
        if (_cache.TryGetValue(cacheKey, out ProcessRouteGraph? cached))
        {
            return cached;
        }

        var steps = await _db.ProcessRouteSteps
            .Where(s => s.VersionId == versionId)
            .OrderBy(s => s.SortOrder)
            .Select(s => new ProcessRouteStepInfo
            {
                Id = s.Id,
                OperationCode = s.OperationCode,
                DisplayName = s.DisplayName,
                StepType = s.StepType,
                IsStart = s.IsStart,
                IsEnd = s.IsEnd,
                SortOrder = s.SortOrder
            })
            .ToListAsync();

        if (steps.Count == 0) return null;

        var transitions = await _db.ProcessRouteTransitions
            .Where(t => t.VersionId == versionId)
            .OrderBy(t => t.SortOrder)
            .Select(t => new ProcessRouteTransitionInfo
            {
                Id = t.Id,
                FromStepId = t.FromStepId,
                ToStepId = t.ToStepId,
                TransitionType = t.TransitionType,
                Label = t.Label,
                IsDefault = t.IsDefault,
                SortOrder = t.SortOrder
            })
            .ToListAsync();

        var graph = new ProcessRouteGraph
        {
            VersionId = versionId,
            Steps = steps,
            Transitions = transitions
        };

        _cache.Set(cacheKey, graph, new MemoryCacheEntryOptions
        {
            SlidingExpiration = CacheDuration
        });

        return graph;
    }

    /// <inheritdoc/>
    public async Task BindRouteAsync(Unitload unitload, int materialId)
    {
        var version = await MatchRouteByMaterialAsync(materialId);
        if (version == null)
        {
            _logger.LogDebug("[ProcessRoute] 物料 {MaterialId} 无匹配路线，保持硬编码模式", materialId);
            return;
        }

        // 根据 CurrentOperation 定位当前步骤（先查找，找到后才更新路线 ID，避免数据不一致）
        var graph = await GetRouteGraphAsync(version.Id);
        if (graph == null) return;

        var step = graph.FindStepByOperation(unitload.CurrentOperation ?? "");
        if (step == null)
        {
            _logger.LogWarning("[ProcessRoute] 工序 {Operation} 在路线版本 {VersionId} 中未找到对应步骤，跳过路线更新",
                unitload.CurrentOperation, version.Id);
            return;
        }

        // 步骤匹配成功后才更新路线绑定
        unitload.ProcessRouteId = version.ProcessRouteId;
        unitload.ProcessRouteVersionId = version.Id;
        unitload.CurrentStepId = step.Id;

        // 计算下一步
        var outgoing = graph.GetOutgoingTransitions(step.Id);
        if (outgoing.Count == 1)
        {
            unitload.NextStepId = outgoing[0].ToStepId;
            var nextStep = graph.FindStep(outgoing[0].ToStepId);
            unitload.NextOperation = nextStep?.OperationCode;
            unitload.IsAwaitingBranchSelection = false;
        }
        else if (outgoing.Count > 1)
        {
            // 分支节点：取默认分支，无默认则等待人工选择
            var defaultBranch = outgoing.FirstOrDefault(t => t.IsDefault);
            if (defaultBranch != null)
            {
                unitload.NextStepId = defaultBranch.ToStepId;
                var nextStep = graph.FindStep(defaultBranch.ToStepId);
                unitload.NextOperation = nextStep?.OperationCode;
            }
            else
            {
                unitload.NextStepId = null;
                unitload.NextOperation = null;
            }
            unitload.IsAwaitingBranchSelection = defaultBranch == null;
        }
        else
        {
            // 无出边（终止节点）
            unitload.NextStepId = null;
            unitload.NextOperation = null;
            unitload.IsAwaitingBranchSelection = false;
        }

        // 记录进入工序日志
        _db.UnitloadProcessRouteLogs.Add(new UnitloadProcessRouteLog
        {
            UnitloadId = unitload.UnitloadId,
            VersionId = version.Id,
            StepId = step.Id,
            OperationCode = step.OperationCode,
            ActionType = "Enter",
            Operator = "System",
            CreatedTime = DateTime.Now
        });

        _logger.LogInformation("[ProcessRoute] 托盘 {ContainerCode} 绑定路线 {RouteCode} 版本 {Version}, 当前步骤={Step}",
            unitload.ContainerCode, version.ProcessRouteId, version.Version, step.OperationCode);
    }

    /// <inheritdoc/>
    public async Task<bool> AdvanceOperationAsync(Unitload unitload, string? operatorName)
    {
        if (!unitload.ProcessRouteVersionId.HasValue || !unitload.CurrentStepId.HasValue)
        {
            return false;
        }

        var graph = await GetRouteGraphAsync(unitload.ProcessRouteVersionId.Value);
        if (graph == null)
        {
            _logger.LogWarning("[ProcessRoute] 路线图不存在，版本ID={VersionId}", unitload.ProcessRouteVersionId);
            return false;
        }

        var fromStepId = unitload.CurrentStepId.Value;
        var toStepId = unitload.NextStepId;

        if (!toStepId.HasValue)
        {
            _logger.LogWarning("[ProcessRoute] 托盘 {UnitloadId} 无 NextStepId，可能等待分支选择", unitload.UnitloadId);
            return true; // 有路线但等待选择，不算失败
        }

        var toStep = graph.FindStep(toStepId.Value);
        if (toStep == null)
        {
            _logger.LogError("[ProcessRoute] 目标步骤 {StepId} 不存在于路线图中", toStepId);
            return false;
        }

        var fromStep = graph.FindStep(fromStepId);

        // 记录轨迹日志
        _db.UnitloadProcessRouteLogs.Add(new UnitloadProcessRouteLog
        {
            UnitloadId = unitload.UnitloadId,
            VersionId = unitload.ProcessRouteVersionId,
            StepId = toStepId,
            OperationCode = toStep.OperationCode,
            ActionType = "Advance",
            FromOperation = fromStep?.OperationCode,
            ToOperation = toStep.OperationCode,
            Operator = operatorName,
            CreatedTime = DateTime.Now
        });

        // 更新托盘状态
        unitload.CurrentStepId = toStepId;
        unitload.CurrentOperation = toStep.OperationCode;

        // 计算新的下一步
        var outgoing = graph.GetOutgoingTransitions(toStepId.Value);
        if (outgoing.Count == 0)
        {
            // 终止节点
            unitload.NextStepId = null;
            unitload.NextOperation = null;
            unitload.IsAwaitingBranchSelection = false;
        }
        else if (outgoing.Count == 1)
        {
            unitload.NextStepId = outgoing[0].ToStepId;
            var nextStep = graph.FindStep(outgoing[0].ToStepId);
            unitload.NextOperation = nextStep?.OperationCode;
            unitload.IsAwaitingBranchSelection = false;
        }
        else
        {
            // 分支节点：取默认分支或等待选择
            var defaultBranch = outgoing.FirstOrDefault(t => t.IsDefault);
            if (defaultBranch != null)
            {
                unitload.NextStepId = defaultBranch.ToStepId;
                var nextStep = graph.FindStep(defaultBranch.ToStepId);
                unitload.NextOperation = nextStep?.OperationCode;
            }
            else
            {
                unitload.NextStepId = null;
                unitload.NextOperation = null;
            }
            unitload.IsAwaitingBranchSelection = defaultBranch == null;
        }

        _logger.LogInformation("[ProcessRoute] 托盘 {ContainerCode} 工序推进: {From} → {To}, 下一步={Next}",
            unitload.ContainerCode, fromStep?.OperationCode, toStep.OperationCode, unitload.NextOperation);

        return true;
    }

    /// <inheritdoc/>
    public async Task<List<BranchOptionDto>> GetNextStepOptionsAsync(int unitloadId)
    {
        var unitload = await _db.Unitloads.FindAsync(unitloadId);
        if (unitload == null || !unitload.ProcessRouteVersionId.HasValue || !unitload.CurrentStepId.HasValue)
            return new List<BranchOptionDto>();

        var graph = await GetRouteGraphAsync(unitload.ProcessRouteVersionId.Value);
        if (graph == null) return new List<BranchOptionDto>();

        var transitions = graph.GetOutgoingTransitions(unitload.CurrentStepId.Value);

        return transitions.Select(t => new BranchOptionDto
        {
            TransitionId = t.Id,
            FromStepId = t.FromStepId,
            ToStepId = t.ToStepId,
            ToStepName = graph.FindStep(t.ToStepId)?.DisplayName ?? "",
            ToOperationCode = graph.FindStep(t.ToStepId)?.OperationCode ?? "",
            Label = t.Label,
            IsDefault = t.IsDefault
        }).ToList();
    }

    /// <inheritdoc/>
    public async Task<Result> SelectBranchAsync(int unitloadId, int transitionId, string? operatorName)
    {
        var unitload = await _db.Unitloads.FindAsync(unitloadId);
        if (unitload == null)
            return Result.Fail("托盘不存在");

        if (!unitload.ProcessRouteVersionId.HasValue || !unitload.CurrentStepId.HasValue)
            return Result.Fail("托盘未绑定工艺路线");

        var graph = await GetRouteGraphAsync(unitload.ProcessRouteVersionId.Value);
        if (graph == null)
            return Result.Fail("路线版本不存在");

        var transition = graph.Transitions.FirstOrDefault(t => t.Id == transitionId
            && t.FromStepId == unitload.CurrentStepId.Value);
        if (transition == null)
            return Result.Fail("无效的分支选择");

        var fromStep = graph.FindStep(transition.FromStepId);
        var toStep = graph.FindStep(transition.ToStepId);

        // 记录日志
        _db.UnitloadProcessRouteLogs.Add(new UnitloadProcessRouteLog
        {
            UnitloadId = unitloadId,
            VersionId = unitload.ProcessRouteVersionId,
            StepId = unitload.CurrentStepId,
            OperationCode = fromStep?.OperationCode,
            ActionType = "BranchSelect",
            FromOperation = fromStep?.OperationCode,
            ToOperation = toStep?.OperationCode,
            SelectedTransitionId = transitionId,
            Operator = operatorName,
            CreatedTime = DateTime.Now
        });

        // 更新托盘下一步
        unitload.NextStepId = transition.ToStepId;
        unitload.NextOperation = toStep?.OperationCode;
        unitload.IsAwaitingBranchSelection = false;

        await _db.SaveChangesAsync();
        return Result.Success("分支选择成功");
    }

    /// <inheritdoc/>
    public async Task<PagedResult<UnitloadProcessRouteLog>> GetUnitloadTrackAsync(
        int unitloadId, int pageNumber, int pageSize)
    {
        // 返回该托盘的全部工艺轨迹（含历史轮次），按创建时间倒序分页
        var query = _db.UnitloadProcessRouteLogs
            .Where(l => l.UnitloadId == unitloadId)
            .OrderByDescending(l => l.CreatedTime);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return PagedResult.Create(items, pageNumber, pageSize, totalCount);
    }

    /// <inheritdoc/>
    public void ClearCache()
    {
        Interlocked.Increment(ref _cacheVersion);
        _logger.LogInformation("[ProcessRoute] 缓存已清除 (version → {Version})", _cacheVersion);
    }
}
