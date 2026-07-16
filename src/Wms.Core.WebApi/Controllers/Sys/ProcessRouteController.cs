using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wms.Core.Application.DTOs;
using Wms.Core.Application.Ports;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Entities.ProcessRoute;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.WebApi.Controllers.Sys;

/// <summary>
/// 工艺路线管理 API
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/ProcessRoute")]
[Produces("application/json")]
[Authorize]
public class ProcessRouteController : ControllerBase
{
    private readonly WmsDbContext _db;
    private readonly IProcessRouteService _processRouteService;
    private readonly ILogger<ProcessRouteController> _logger;

    public ProcessRouteController(
        WmsDbContext db,
        IProcessRouteService processRouteService,
        ILogger<ProcessRouteController> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _processRouteService = processRouteService ?? throw new ArgumentNullException(nameof(processRouteService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ==================== 路线管理 ====================

    /// <summary>
    /// 获取路线列表（支持 keyword 搜索）
    /// </summary>
    [HttpGet("Routes")]
    public async Task<IActionResult> GetRoutes([FromQuery] string? keyword)
    {
        var query = _db.ProcessRoutes.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(r =>
                r.Code.Contains(keyword) ||
                r.Name.Contains(keyword) ||
                (r.Description != null && r.Description.Contains(keyword)));
        }

        var routes = await query
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Code)
            .ToListAsync();

        return Ok(new { data = routes, total = routes.Count });
    }

    /// <summary>
    /// 获取路线详情（含版本列表）
    /// </summary>
    [HttpGet("Routes/{id}")]
    public async Task<IActionResult> GetRoute(int id)
    {
        _logger.LogInformation("[ProcessRoute] GetRoute called, id={Id}", id);

        var route = await _db.ProcessRoutes
            .Include(r => r.Versions)
            .FirstOrDefaultAsync(r => r.ProcessRouteId == id);

        if (route == null)
        {
            _logger.LogWarning("[ProcessRoute] Route not found, id={Id}", id);
            return NotFound(new { message = "路线不存在" });
        }

        return Ok(route);
    }

    /// <summary>
    /// 创建路线
    /// </summary>
    [HttpPost("Routes")]
    public async Task<IActionResult> CreateRoute([FromBody] ProcessRoute route)
    {
        route.ProcessRouteId = 0;
        route.CreatedTime = DateTime.UtcNow;
        route.CurrentVersion = 1;

        _db.ProcessRoutes.Add(route);
        await _db.SaveChangesAsync();

        _processRouteService.ClearCache();

        return CreatedAtAction(nameof(GetRoute), new { id = route.ProcessRouteId }, route);
    }

    /// <summary>
    /// 更新路线基本信息
    /// </summary>
    [HttpPut("Routes/{id}")]
    public async Task<IActionResult> UpdateRoute(int id, [FromBody] ProcessRoute updated)
    {
        var route = await _db.ProcessRoutes.FindAsync(id);
        if (route == null)
            return NotFound(new { message = "路线不存在" });

        route.Code = updated.Code;
        route.Name = updated.Name;
        route.Description = updated.Description;
        route.IsActive = updated.IsActive;
        route.SortOrder = updated.SortOrder;
        route.Priority = updated.Priority;
        route.ModifiedTime = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _processRouteService.ClearCache();

        return Ok(route);
    }

    /// <summary>
    /// 删除路线（非内置）
    /// </summary>
    [HttpDelete("Routes/{id}")]
    public async Task<IActionResult> DeleteRoute(int id)
    {
        var route = await _db.ProcessRoutes
            .Include(r => r.Versions!)
                .ThenInclude(v => v.Steps)
            .Include(r => r.Versions!)
                .ThenInclude(v => v.Transitions)
            .Include(r => r.MaterialBindings)
            .FirstOrDefaultAsync(r => r.ProcessRouteId == id);

        if (route == null)
            return NotFound(new { message = "路线不存在" });

        if (route.IsBuiltIn)
            return BadRequest(new { message = "内置路线不可删除" });

        // 级联删除版本下的步骤、转移，以及物料绑定
        // 注意顺序：必须先删 Transitions 再删 Steps，因为 Transition.FromStepId/ToStepId
        // 是非可空 FK 且配置为 OnDelete(Restrict)，若先删 Steps 会触发 EF Core 关系 severed 异常
        if (route.Versions != null)
        {
            foreach (var version in route.Versions)
            {
                if (version.Transitions != null)
                    _db.ProcessRouteTransitions.RemoveRange(version.Transitions);
                if (version.Steps != null)
                    _db.ProcessRouteSteps.RemoveRange(version.Steps);
            }
            _db.ProcessRouteVersions.RemoveRange(route.Versions);
        }

        if (route.MaterialBindings != null)
            _db.ProcessRouteMaterialBindings.RemoveRange(route.MaterialBindings);

        _db.ProcessRoutes.Remove(route);
        await _db.SaveChangesAsync();

        _processRouteService.ClearCache();

        return NoContent();
    }

    // ==================== 版本管理 ====================

    /// <summary>
    /// 获取版本列表
    /// </summary>
    [HttpGet("Routes/{routeId}/Versions")]
    public async Task<IActionResult> GetVersions(int routeId)
    {
        var route = await _db.ProcessRoutes.FindAsync(routeId);
        if (route == null)
            return NotFound(new { message = "路线不存在" });

        var versions = await _db.ProcessRouteVersions
            .Where(v => v.ProcessRouteId == routeId)
            .OrderByDescending(v => v.Version)
            .ToListAsync();

        return Ok(new { data = versions, total = versions.Count });
    }

    /// <summary>
    /// 获取版本详情（含步骤和转移）
    /// </summary>
    [HttpGet("Versions/{versionId}")]
    public async Task<IActionResult> GetVersion(int versionId)
    {
        _logger.LogInformation("[ProcessRoute] GetVersion called, versionId={VersionId}", versionId);

        var version = await _db.ProcessRouteVersions
            .Include(v => v.Steps!.OrderBy(s => s.SortOrder))
            .Include(v => v.Transitions!.OrderBy(t => t.SortOrder))
            .Include(v => v.Route)
            .FirstOrDefaultAsync(v => v.Id == versionId);

        if (version == null)
        {
            _logger.LogWarning("[ProcessRoute] Version not found, versionId={VersionId}", versionId);
            return NotFound(new { message = "版本不存在" });
        }

        return Ok(version);
    }

    /// <summary>
    /// 创建新版本（基于当前 Published 版本深拷贝步骤和转移，使用 stepIdMap 映射旧/新 StepId）
    /// </summary>
    [HttpPost("Routes/{routeId}/Versions")]
    public async Task<IActionResult> CreateVersion(int routeId)
    {
        var route = await _db.ProcessRoutes.FindAsync(routeId);
        if (route == null)
            return NotFound(new { message = "路线不存在" });

        // 获取当前 Published 版本
        var publishedVersion = await _db.ProcessRouteVersions
            .Where(v => v.ProcessRouteId == routeId && v.Status == "Published")
            .OrderByDescending(v => v.Version)
            .FirstOrDefaultAsync();

        // 计算新版本号
        var maxVersion = await _db.ProcessRouteVersions
            .Where(v => v.ProcessRouteId == routeId)
            .MaxAsync(v => (int?)v.Version) ?? 0;

        var newVersion = new ProcessRouteVersion
        {
            ProcessRouteId = routeId,
            Version = maxVersion + 1,
            Status = "Draft",
            PublishedTime = null,
            PublishedBy = null,
            CreatedTime = DateTime.UtcNow,
            ChangeLog = publishedVersion != null ? $"基于版本 {publishedVersion.Version} 创建" : "初始版本"
        };

        _db.ProcessRouteVersions.Add(newVersion);
        await _db.SaveChangesAsync();

        // 如果存在已发布版本，深拷贝步骤和转移
        if (publishedVersion != null)
        {
            var sourceSteps = await _db.ProcessRouteSteps
                .Where(s => s.VersionId == publishedVersion.Id)
                .OrderBy(s => s.SortOrder)
                .ToListAsync();

            var sourceTransitions = await _db.ProcessRouteTransitions
                .Where(t => t.VersionId == publishedVersion.Id)
                .OrderBy(t => t.SortOrder)
                .ToListAsync();

            // stepIdMap: 旧 StepId -> 新 StepId
            var stepIdMap = new Dictionary<int, int>();

            // 拷贝步骤
            foreach (var srcStep in sourceSteps)
            {
                var newStep = new ProcessRouteStep
                {
                    VersionId = newVersion.Id,
                    OperationCode = srcStep.OperationCode,
                    DisplayName = srcStep.DisplayName,
                    StepType = srcStep.StepType,
                    IsStart = srcStep.IsStart,
                    IsEnd = srcStep.IsEnd,
                    SortOrder = srcStep.SortOrder,
                    Description = srcStep.Description,
                    CreatedTime = DateTime.UtcNow
                };

                _db.ProcessRouteSteps.Add(newStep);
                await _db.SaveChangesAsync();

                stepIdMap[srcStep.Id] = newStep.Id;
            }

            // 拷贝转移（使用 stepIdMap 映射 FromStepId / ToStepId）
            foreach (var srcTrans in sourceTransitions)
            {
                var newTrans = new ProcessRouteTransition
                {
                    VersionId = newVersion.Id,
                    FromStepId = stepIdMap.TryGetValue(srcTrans.FromStepId, out var newFromId) ? newFromId : 0,
                    ToStepId = stepIdMap.TryGetValue(srcTrans.ToStepId, out var newToId) ? newToId : 0,
                    TransitionType = srcTrans.TransitionType,
                    Label = srcTrans.Label,
                    IsDefault = srcTrans.IsDefault,
                    SortOrder = srcTrans.SortOrder,
                    CreatedTime = DateTime.UtcNow
                };

                _db.ProcessRouteTransitions.Add(newTrans);
            }

            await _db.SaveChangesAsync();
        }

        _processRouteService.ClearCache();

        // 重新加载包含步骤和转移的完整版本
        var result = await _db.ProcessRouteVersions
            .Include(v => v.Steps!.OrderBy(s => s.SortOrder))
            .Include(v => v.Transitions!.OrderBy(t => t.SortOrder))
            .FirstOrDefaultAsync(v => v.Id == newVersion.Id);

        return CreatedAtAction(nameof(GetVersion), new { versionId = newVersion.Id }, result);
    }

    /// <summary>
    /// 发布版本（原 Published 版本变 Archived）
    /// </summary>
    [HttpPut("Versions/{versionId}/Publish")]
    public async Task<IActionResult> PublishVersion(int versionId)
    {
        var version = await _db.ProcessRouteVersions
            .Include(v => v.Route)
            .FirstOrDefaultAsync(v => v.Id == versionId);

        if (version == null)
            return NotFound(new { message = "版本不存在" });

        if (version.Status == "Published")
            return BadRequest(new { message = "该版本已是发布状态" });

        // 将同路线下当前 Published 版本改为 Archived
        var currentPublished = await _db.ProcessRouteVersions
            .Where(v => v.ProcessRouteId == version.ProcessRouteId && v.Status == "Published")
            .ToListAsync();

        foreach (var v in currentPublished)
        {
            v.Status = "Archived";
            v.ModifiedTime = DateTime.UtcNow;
        }

        // 发布新版本
        version.Status = "Published";
        version.PublishedTime = DateTime.UtcNow;
        version.PublishedBy = User.Identity?.Name;
        version.ModifiedTime = DateTime.UtcNow;

        // 更新路线的 CurrentVersion
        if (version.Route != null)
        {
            version.Route.CurrentVersion = version.Version;
            version.Route.ModifiedTime = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        _processRouteService.ClearCache();

        return Ok(version);
    }

    /// <summary>
    /// 归档版本
    /// </summary>
    [HttpPut("Versions/{versionId}/Archive")]
    public async Task<IActionResult> ArchiveVersion(int versionId)
    {
        var version = await _db.ProcessRouteVersions.FindAsync(versionId);
        if (version == null)
            return NotFound(new { message = "版本不存在" });

        if (version.Status == "Archived")
            return BadRequest(new { message = "该版本已是归档状态" });

        version.Status = "Archived";
        version.ModifiedTime = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _processRouteService.ClearCache();

        return Ok(version);
    }

    /// <summary>
    /// 删除草稿版本
    /// </summary>
    [HttpDelete("Versions/{versionId}")]
    public async Task<IActionResult> DeleteVersion(int versionId)
    {
        var version = await _db.ProcessRouteVersions
            .Include(v => v.Steps)
            .Include(v => v.Transitions)
            .FirstOrDefaultAsync(v => v.Id == versionId);

        if (version == null)
            return NotFound(new { message = "版本不存在" });

        if (version.Status != "Draft")
            return BadRequest(new { message = "只能删除草稿状态的版本" });

        // 注意顺序：必须先删 Transitions 再删 Steps（同 DeleteRoute，避免 FK severed 异常）
        if (version.Transitions != null)
            _db.ProcessRouteTransitions.RemoveRange(version.Transitions);
        if (version.Steps != null)
            _db.ProcessRouteSteps.RemoveRange(version.Steps);

        _db.ProcessRouteVersions.Remove(version);
        await _db.SaveChangesAsync();

        _processRouteService.ClearCache();

        return NoContent();
    }

    // ==================== 步骤管理 ====================

    /// <summary>
    /// 添加步骤
    /// </summary>
    [HttpPost("Versions/{versionId}/Steps")]
    public async Task<IActionResult> AddStep(int versionId, [FromBody] ProcessRouteStep step)
    {
        var version = await _db.ProcessRouteVersions.FindAsync(versionId);
        if (version == null)
            return NotFound(new { message = "版本不存在" });

        if (version.Status != "Draft")
            return BadRequest(new { message = "只能修改草稿状态的版本" });

        step.Id = 0;
        step.VersionId = versionId;
        step.CreatedTime = DateTime.UtcNow;

        _db.ProcessRouteSteps.Add(step);
        await _db.SaveChangesAsync();

        _processRouteService.ClearCache();

        return Ok(step);
    }

    /// <summary>
    /// 更新步骤
    /// </summary>
    [HttpPut("Steps/{id}")]
    public async Task<IActionResult> UpdateStep(int id, [FromBody] ProcessRouteStep updated)
    {
        var step = await _db.ProcessRouteSteps
            .Include(s => s.Version)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (step == null)
            return NotFound(new { message = "步骤不存在" });

        if (step.Version != null && step.Version.Status != "Draft")
            return BadRequest(new { message = "只能修改草稿状态的版本中的步骤" });

        step.OperationCode = updated.OperationCode;
        step.DisplayName = updated.DisplayName;
        step.StepType = updated.StepType;
        step.IsStart = updated.IsStart;
        step.IsEnd = updated.IsEnd;
        step.SortOrder = updated.SortOrder;
        step.Description = updated.Description;
        step.ModifiedTime = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _processRouteService.ClearCache();

        return Ok(step);
    }

    /// <summary>
    /// 删除步骤
    /// </summary>
    [HttpDelete("Steps/{id}")]
    public async Task<IActionResult> DeleteStep(int id)
    {
        var step = await _db.ProcessRouteSteps
            .Include(s => s.Version)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (step == null)
            return NotFound(new { message = "步骤不存在" });

        if (step.Version != null && step.Version.Status != "Draft")
            return BadRequest(new { message = "只能删除草稿状态的版本中的步骤" });

        // 删除关联的转移
        var relatedTransitions = await _db.ProcessRouteTransitions
            .Where(t => t.FromStepId == id || t.ToStepId == id)
            .ToListAsync();

        _db.ProcessRouteTransitions.RemoveRange(relatedTransitions);
        _db.ProcessRouteSteps.Remove(step);
        await _db.SaveChangesAsync();

        _processRouteService.ClearCache();

        return NoContent();
    }

    // ==================== 转移/分支管理 ====================

    /// <summary>
    /// 添加转移
    /// </summary>
    [HttpPost("Versions/{versionId}/Transitions")]
    public async Task<IActionResult> AddTransition(int versionId, [FromBody] ProcessRouteTransition transition)
    {
        var version = await _db.ProcessRouteVersions.FindAsync(versionId);
        if (version == null)
            return NotFound(new { message = "版本不存在" });

        if (version.Status != "Draft")
            return BadRequest(new { message = "只能修改草稿状态的版本" });

        // 校验源步骤和目标步骤属于同一版本
        var fromStep = await _db.ProcessRouteSteps
            .FirstOrDefaultAsync(s => s.Id == transition.FromStepId && s.VersionId == versionId);
        if (fromStep == null)
            return BadRequest(new { message = "源步骤不存在或不属于该版本" });

        var toStep = await _db.ProcessRouteSteps
            .FirstOrDefaultAsync(s => s.Id == transition.ToStepId && s.VersionId == versionId);
        if (toStep == null)
            return BadRequest(new { message = "目标步骤不存在或不属于该版本" });

        transition.Id = 0;
        transition.VersionId = versionId;
        transition.CreatedTime = DateTime.UtcNow;

        _db.ProcessRouteTransitions.Add(transition);
        await _db.SaveChangesAsync();

        _processRouteService.ClearCache();

        return Ok(transition);
    }

    /// <summary>
    /// 更新转移
    /// </summary>
    [HttpPut("Transitions/{id}")]
    public async Task<IActionResult> UpdateTransition(int id, [FromBody] ProcessRouteTransition updated)
    {
        var transition = await _db.ProcessRouteTransitions
            .Include(t => t.Version)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (transition == null)
            return NotFound(new { message = "转移不存在" });

        if (transition.Version != null && transition.Version.Status != "Draft")
            return BadRequest(new { message = "只能修改草稿状态的版本中的转移" });

        transition.FromStepId = updated.FromStepId;
        transition.ToStepId = updated.ToStepId;
        transition.TransitionType = updated.TransitionType;
        transition.Label = updated.Label;
        transition.IsDefault = updated.IsDefault;
        transition.SortOrder = updated.SortOrder;
        transition.ModifiedTime = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _processRouteService.ClearCache();

        return Ok(transition);
    }

    /// <summary>
    /// 删除转移
    /// </summary>
    [HttpDelete("Transitions/{id}")]
    public async Task<IActionResult> DeleteTransition(int id)
    {
        var transition = await _db.ProcessRouteTransitions
            .Include(t => t.Version)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (transition == null)
            return NotFound(new { message = "转移不存在" });

        if (transition.Version != null && transition.Version.Status != "Draft")
            return BadRequest(new { message = "只能删除草稿状态的版本中的转移" });

        _db.ProcessRouteTransitions.Remove(transition);
        await _db.SaveChangesAsync();

        _processRouteService.ClearCache();

        return NoContent();
    }

    // ==================== 物料绑定 ====================

    /// <summary>
    /// 物料绑定列表（关联 Materials 表）
    /// </summary>
    [HttpGet("Routes/{routeId}/Materials")]
    public async Task<IActionResult> GetMaterialBindings(int routeId)
    {
        var route = await _db.ProcessRoutes.FindAsync(routeId);
        if (route == null)
            return NotFound(new { message = "路线不存在" });

        var bindings = await _db.ProcessRouteMaterialBindings
            .Where(b => b.ProcessRouteId == routeId)
            .Join(_db.Materials,
                b => b.MaterialId,
                m => m.MaterialId,
                (b, m) => new MaterialBindingDto
                {
                    Id = b.Id,
                    ProcessRouteId = b.ProcessRouteId,
                    MaterialId = b.MaterialId,
                    MaterialCode = m.MaterialCode,
                    MaterialType = m.MaterialType,
                    Description = m.Description,
                    Priority = b.Priority,
                    IsActive = b.IsActive
                })
            .OrderByDescending(b => b.Priority)
            .ToListAsync();

        return Ok(new { data = bindings, total = bindings.Count });
    }

    /// <summary>
    /// 添加物料绑定（校验路线/物料存在、不重复绑定、清缓存）
    /// </summary>
    [HttpPost("Routes/{routeId}/Materials")]
    public async Task<IActionResult> AddMaterialBinding(int routeId, [FromBody] CreateMaterialBindingDto dto)
    {
        // 校验路线存在
        var route = await _db.ProcessRoutes.FindAsync(routeId);
        if (route == null)
            return NotFound(new { message = "路线不存在" });

        // 校验物料存在
        var material = await _db.Materials.FindAsync(dto.MaterialId);
        if (material == null)
            return NotFound(new { message = "物料不存在" });

        // 校验不重复绑定
        var existing = await _db.ProcessRouteMaterialBindings
            .AnyAsync(b => b.ProcessRouteId == routeId && b.MaterialId == dto.MaterialId);

        if (existing)
            return BadRequest(new { message = "该物料已绑定到此路线" });

        var binding = new ProcessRouteMaterialBinding
        {
            ProcessRouteId = routeId,
            MaterialId = dto.MaterialId,
            Priority = dto.Priority,
            IsActive = true,
            CreatedTime = DateTime.UtcNow
        };

        _db.ProcessRouteMaterialBindings.Add(binding);
        await _db.SaveChangesAsync();

        _processRouteService.ClearCache();

        var result = new MaterialBindingDto
        {
            Id = binding.Id,
            ProcessRouteId = binding.ProcessRouteId,
            MaterialId = binding.MaterialId,
            MaterialCode = material.MaterialCode,
            MaterialType = material.MaterialType,
            Description = material.Description,
            Priority = binding.Priority,
            IsActive = binding.IsActive
        };

        return Ok(result);
    }

    /// <summary>
    /// 删除物料绑定（清缓存）
    /// </summary>
    [HttpDelete("Materials/{bindingId}")]
    public async Task<IActionResult> DeleteMaterialBinding(int bindingId)
    {
        var binding = await _db.ProcessRouteMaterialBindings.FindAsync(bindingId);
        if (binding == null)
            return NotFound(new { message = "物料绑定不存在" });

        _db.ProcessRouteMaterialBindings.Remove(binding);
        await _db.SaveChangesAsync();

        _processRouteService.ClearCache();

        return NoContent();
    }

    // ==================== 路线图查询 ====================

    /// <summary>
    /// 获取路线图（G6 格式，返回 RouteGraphDto）
    /// </summary>
    [HttpGet("Versions/{versionId}/Graph")]
    public async Task<IActionResult> GetRouteGraph(int versionId)
    {
        var version = await _db.ProcessRouteVersions
            .Include(v => v.Route)
            .FirstOrDefaultAsync(v => v.Id == versionId);

        if (version == null)
            return NotFound(new { message = "版本不存在" });

        var graph = await _processRouteService.GetRouteGraphAsync(versionId);

        // 版本没有步骤数据时返回空图（新创建的 Draft 版本可能没有步骤）
        var dto = new RouteGraphDto
        {
            VersionId = versionId,
            RouteName = version.Route?.Name ?? "",
            Version = version.Version,
            Nodes = graph?.Steps.Select(s => new GraphNodeDto
            {
                Id = s.Id.ToString(),
                Name = s.DisplayName,
                OperationCode = s.OperationCode,
                StepType = s.StepType,
                IsStart = s.IsStart,
                IsEnd = s.IsEnd,
                SortOrder = s.SortOrder,
                IsCurrent = false
            }).ToList() ?? new List<GraphNodeDto>(),
            Edges = graph?.Transitions.Select(t => new GraphEdgeDto
            {
                Id = t.Id.ToString(),
                Source = t.FromStepId.ToString(),
                Target = t.ToStepId.ToString(),
                Label = t.Label,
                TransitionType = t.TransitionType,
                IsDefault = t.IsDefault
            }).ToList()
        };

        return Ok(dto);
    }

    // ==================== 托盘轨迹 ====================

    /// <summary>
    /// 解析托盘：支持按 ID（数字）或托盘码（字符串）查找
    /// </summary>
    private async Task<Unitload?> ResolveUnitloadAsync(string unitloadIdOrCode)
    {
        if (int.TryParse(unitloadIdOrCode, out var id))
            return await _db.Unitloads.FindAsync(id);
        return await _db.Unitloads.FirstOrDefaultAsync(u => u.ContainerCode == unitloadIdOrCode);
    }

    /// <summary>
    /// 托盘工艺状态（不含历史轨迹，支持按托盘ID（数字）或托盘码（字符串）查询）
    /// </summary>
    [HttpGet("Unitloads/{unitloadIdOrCode}/TrackStatus")]
    public async Task<IActionResult> GetUnitloadTrackStatus(string unitloadIdOrCode)
    {
        var unitload = await ResolveUnitloadAsync(unitloadIdOrCode);
        if (unitload == null)
            return NotFound(new { message = "托盘不存在" });

        var dto = new UnitloadTrackStatusDto
        {
            UnitloadId = unitload.UnitloadId,
            ContainerCode = unitload.ContainerCode,
            CurrentOperation = unitload.CurrentOperation,
            NextOperation = unitload.NextOperation,
            IsAwaitingBranchSelection = unitload.IsAwaitingBranchSelection ?? false,
            CurrentStepId = unitload.CurrentStepId
        };

        // 如果绑定路线，补充路线名称和版本号
        if (unitload.ProcessRouteVersionId.HasValue)
        {
            var version = await _db.ProcessRouteVersions
                .Include(v => v.Route)
                .FirstOrDefaultAsync(v => v.Id == unitload.ProcessRouteVersionId.Value);

            if (version != null)
            {
                dto.RouteName = version.Route?.Name;
                dto.Version = version.Version;
            }
        }

        // 如果等待分支选择，补充下一步选项
        if (unitload.IsAwaitingBranchSelection == true)
        {
            dto.NextOptions = await _processRouteService.GetNextStepOptionsAsync(unitload.UnitloadId);
        }

        return Ok(dto);
    }

    /// <summary>
    /// 托盘历史轨迹（分页，按创建时间倒序）
    /// </summary>
    [HttpGet("Unitloads/{unitloadIdOrCode}/TrackHistory")]
    public async Task<IActionResult> GetUnitloadTrackHistory(
        string unitloadIdOrCode,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        var unitload = await ResolveUnitloadAsync(unitloadIdOrCode);
        if (unitload == null)
            return NotFound(new { message = "托盘不存在" });

        pageSize = Math.Clamp(pageSize, 1, 100);
        if (pageNumber < 1) pageNumber = 1;

        var pagedLogs = await _processRouteService
            .GetUnitloadTrackAsync(unitload.UnitloadId, pageNumber, pageSize);

        var dto = PagedResult.Create(
            pagedLogs.Data.Select(l => new TrackEntryDto
            {
                Id = l.Id,
                OperationCode = l.OperationCode,
                ActionType = l.ActionType,
                FromOperation = l.FromOperation,
                ToOperation = l.ToOperation,
                Operator = l.Operator,
                CreatedTime = l.CreatedTime
            }).ToList(),
            pagedLogs.PageNumber,
            pagedLogs.PageSize,
            pagedLogs.TotalCount
        );

        return Ok(dto);
    }

    /// <summary>
    /// 托盘当前路线图（位置高亮）
    /// </summary>
    [HttpGet("Unitloads/{unitloadIdOrCode}/Graph")]
    public async Task<IActionResult> GetUnitloadGraph(string unitloadIdOrCode)
    {
        var unitload = await ResolveUnitloadAsync(unitloadIdOrCode);
        if (unitload == null)
            return NotFound(new { message = "托盘不存在" });

        if (!unitload.ProcessRouteVersionId.HasValue)
            return Ok(new RouteGraphDto { Nodes = new List<GraphNodeDto>(), Edges = new List<GraphEdgeDto>() });

        var versionId = unitload.ProcessRouteVersionId.Value;

        var version = await _db.ProcessRouteVersions
            .Include(v => v.Route)
            .FirstOrDefaultAsync(v => v.Id == versionId);

        if (version == null)
            return Ok(new RouteGraphDto { Nodes = new List<GraphNodeDto>(), Edges = new List<GraphEdgeDto>() });

        var graph = await _processRouteService.GetRouteGraphAsync(versionId);

        var currentStepId = unitload.CurrentStepId ?? 0;

        var dto = new RouteGraphDto
        {
            VersionId = versionId,
            RouteName = version.Route?.Name ?? "",
            Version = version.Version,
            Nodes = graph?.Steps.Select(s => new GraphNodeDto
            {
                Id = s.Id.ToString(),
                Name = s.DisplayName,
                OperationCode = s.OperationCode,
                StepType = s.StepType,
                IsStart = s.IsStart,
                IsEnd = s.IsEnd,
                SortOrder = s.SortOrder,
                IsCurrent = s.Id == currentStepId
            }).ToList() ?? new List<GraphNodeDto>(),
            Edges = graph?.Transitions.Select(t => new GraphEdgeDto
            {
                Id = t.Id.ToString(),
                Source = t.FromStepId.ToString(),
                Target = t.ToStepId.ToString(),
                Label = t.Label,
                TransitionType = t.TransitionType,
                IsDefault = t.IsDefault
            }).ToList()
        };

        return Ok(dto);
    }

    /// <summary>
    /// 下一步选项（调用 _processRouteService.GetNextStepOptionsAsync）
    /// </summary>
    [HttpGet("Unitloads/{unitloadIdOrCode}/NextOptions")]
    public async Task<IActionResult> GetNextOptions(string unitloadIdOrCode)
    {
        var unitload = await ResolveUnitloadAsync(unitloadIdOrCode);
        if (unitload == null)
            return NotFound(new { message = "托盘不存在" });

        if (!unitload.ProcessRouteVersionId.HasValue)
            return Ok(new { data = new List<BranchOptionDto>(), total = 0 });

        var options = await _processRouteService.GetNextStepOptionsAsync(unitload.UnitloadId);

        return Ok(new { data = options, total = options.Count });
    }

    /// <summary>
    /// 人工选择分支（请求体含 transitionId）
    /// </summary>
    [HttpPost("Unitloads/{unitloadIdOrCode}/SelectBranch")]
    public async Task<IActionResult> SelectBranch(string unitloadIdOrCode, [FromBody] SelectBranchRequest request)
    {
        if (request == null || request.TransitionId <= 0)
            return BadRequest(new { message = "无效的 transitionId" });

        var unitload = await ResolveUnitloadAsync(unitloadIdOrCode);
        if (unitload == null)
            return NotFound(new { message = "托盘不存在" });

        var operatorName = User.Identity?.Name;
        var result = await _processRouteService.SelectBranchAsync(unitload.UnitloadId, request.TransitionId, operatorName);

        if (!result.IsSuccess)
            return BadRequest(new { message = result.Error });

        return Ok(new { message = "分支选择成功" });
    }
}

/// <summary>
/// 分支选择请求体
/// </summary>
public class SelectBranchRequest
{
    /// <summary>
    /// 选择的转移ID
    /// </summary>
    public int TransitionId { get; set; }
}
