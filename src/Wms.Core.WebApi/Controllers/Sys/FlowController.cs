using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wms.Core.Domain.Entities.Flow;
using Wms.Core.Engine;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.WebApi.Controllers.Sys;

/// <summary>
/// 流程引擎管理 API
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public class FlowController : ControllerBase
{
    private readonly WmsDbContext _db;
    private readonly IFlowEngine _flowEngine;
    private readonly IEnumerable<INodeHandler> _nodeHandlers;
    private readonly ILogger<FlowController> _logger;

    public FlowController(
        WmsDbContext db,
        IFlowEngine flowEngine,
        IEnumerable<INodeHandler> nodeHandlers,
        ILogger<FlowController> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _flowEngine = flowEngine ?? throw new ArgumentNullException(nameof(flowEngine));
        _nodeHandlers = nodeHandlers ?? throw new ArgumentNullException(nameof(nodeHandlers));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ==================== 模板管理 ====================

    /// <summary>
    /// 获取模板列表
    /// </summary>
    [HttpGet("Templates")]
    public async Task<IActionResult> GetTemplates([FromQuery] string? category)
    {
        var query = _db.FlowTemplates
            .Include(t => t.Nodes!.OrderBy(n => n.StepOrder))
            .AsQueryable();

        if (!string.IsNullOrEmpty(category))
            query = query.Where(t => t.Category == category);

        var templates = await query
            .OrderBy(t => t.SortOrder)
            .ToListAsync();

        return Ok(new { data = templates, total = templates.Count });
    }

    /// <summary>
    /// 获取模板详情
    /// </summary>
    [HttpGet("Templates/{id}")]
    public async Task<IActionResult> GetTemplate(int id)
    {
        var template = await _db.FlowTemplates
            .Include(t => t.Nodes!.OrderBy(n => n.StepOrder))
            .FirstOrDefaultAsync(t => t.Id == id);

        if (template == null)
            return NotFound(new { message = "模板不存在" });

        return Ok(template);
    }

    /// <summary>
    /// 创建模板
    /// </summary>
    [HttpPost("Templates")]
    public async Task<IActionResult> CreateTemplate([FromBody] FlowTemplate template)
    {
        template.CreatedTime = DateTime.UtcNow;

        _db.FlowTemplates.Add(template);
        await _db.SaveChangesAsync();

        _flowEngine.ClearCache();

        return CreatedAtAction(nameof(GetTemplate), new { id = template.Id }, template);
    }

    /// <summary>
    /// 更新模板
    /// </summary>
    [HttpPut("Templates/{id}")]
    public async Task<IActionResult> UpdateTemplate(int id, [FromBody] FlowTemplate updated)
    {
        var template = await _db.FlowTemplates.FindAsync(id);
        if (template == null)
            return NotFound(new { message = "模板不存在" });

        template.Name = updated.Name;
        template.Code = updated.Code;
        template.Category = updated.Category;
        template.Phase = updated.Phase;
        template.Description = updated.Description;
        template.IsActive = updated.IsActive;
        template.SortOrder = updated.SortOrder;
        template.MatchRules = updated.MatchRules;
        template.Priority = updated.Priority;

        await _db.SaveChangesAsync();

        _flowEngine.ClearCache();

        return Ok(template);
    }

    /// <summary>
    /// 删除模板
    /// </summary>
    [HttpDelete("Templates/{id}")]
    public async Task<IActionResult> DeleteTemplate(int id)
    {
        var template = await _db.FlowTemplates
            .Include(t => t.Nodes)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (template == null)
            return NotFound(new { message = "模板不存在" });

        if (template.IsBuiltIn)
            return BadRequest(new { message = "内置模板不可删除" });

        _db.FlowNodes.RemoveRange(template.Nodes!);
        _db.FlowTemplates.Remove(template);
        await _db.SaveChangesAsync();

        _flowEngine.ClearCache();

        return NoContent();
    }

    // ==================== 节点管理 ====================

    /// <summary>
    /// 添加节点到模板
    /// </summary>
    [HttpPost("Templates/{templateId}/Nodes")]
    public async Task<IActionResult> AddNode(int templateId, [FromBody] FlowNode node)
    {
        var template = await _db.FlowTemplates.FindAsync(templateId);
        if (template == null)
            return NotFound(new { message = "模板不存在" });

        node.TemplateId = templateId;
        _db.FlowNodes.Add(node);
        await _db.SaveChangesAsync();

        _flowEngine.ClearCache();

        return Ok(node);
    }

    /// <summary>
    /// 更新节点
    /// </summary>
    [HttpPut("Templates/Nodes/{id}")]
    public async Task<IActionResult> UpdateNode(int id, [FromBody] FlowNode updated)
    {
        var node = await _db.FlowNodes.FindAsync(id);
        if (node == null)
            return NotFound(new { message = "节点不存在" });

        node.NodeType = updated.NodeType;
        node.NodeName = updated.NodeName;
        node.StepOrder = updated.StepOrder;
        node.ConfigJson = updated.ConfigJson;
        node.IsEnabled = updated.IsEnabled;
        node.OnFailure = updated.OnFailure;
        node.SkipCondition = updated.SkipCondition;
        node.IsTransactionBoundary = updated.IsTransactionBoundary;

        await _db.SaveChangesAsync();

        _flowEngine.ClearCache();

        return Ok(node);
    }

    /// <summary>
    /// 删除节点
    /// </summary>
    [HttpDelete("Templates/Nodes/{id}")]
    public async Task<IActionResult> DeleteNode(int id)
    {
        var node = await _db.FlowNodes.FindAsync(id);
        if (node == null)
            return NotFound(new { message = "节点不存在" });

        _db.FlowNodes.Remove(node);
        await _db.SaveChangesAsync();

        _flowEngine.ClearCache();

        return NoContent();
    }

    /// <summary>
    /// 节点排序
    /// </summary>
    [HttpPost("Templates/{templateId}/Nodes/Reorder")]
    public async Task<IActionResult> ReorderNodes(int templateId, [FromBody] int[] nodeIds)
    {
        var nodes = await _db.FlowNodes
            .Where(n => n.TemplateId == templateId)
            .ToListAsync();

        for (int i = 0; i < nodeIds.Length; i++)
        {
            var node = nodes.FirstOrDefault(n => n.Id == nodeIds[i]);
            if (node != null)
                node.StepOrder = i + 1;
        }

        await _db.SaveChangesAsync();

        _flowEngine.ClearCache();

        return Ok(new { message = "排序成功" });
    }

    // ==================== 节点类型工具箱 ====================

    /// <summary>
    /// 获取所有可用节点类型
    /// </summary>
    [HttpGet("NodeTypes")]
    public IActionResult GetNodeTypes()
    {
        var types = _nodeHandlers.Select(h => new
        {
            h.NodeType,
            h.DisplayName,
            h.Category,
            h.Description,
            h.ConfigSchema
        }).OrderBy(h => h.Category).ThenBy(h => h.DisplayName).ToList();

        return Ok(types);
    }

    // ==================== 流程实例监控 ====================

    /// <summary>
    /// 获取实例列表
    /// </summary>
    [HttpGet("Instances")]
    public async Task<IActionResult> GetInstances(
        [FromQuery] string? status,
        [FromQuery] int? templateId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _db.FlowInstances.AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(i => i.Status == status);

        if (templateId.HasValue)
            query = query.Where(i => i.TemplateId == templateId.Value);

        var total = await query.CountAsync();
        var instances = await query
            .OrderByDescending(i => i.CreatedTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new { data = instances, total, page, pageSize });
    }

    /// <summary>
    /// 获取实例详情（含节点日志）
    /// </summary>
    [HttpGet("Instances/{id}")]
    public async Task<IActionResult> GetInstance(int id)
    {
        var instance = await _db.FlowInstances.FindAsync(id);
        if (instance == null)
            return NotFound(new { message = "实例不存在" });

        var logs = await _db.FlowNodeLogs
            .Where(l => l.InstanceId == id)
            .OrderBy(l => l.NodeOrder)
            .ToListAsync();

        return Ok(new { instance, logs });
    }
}
