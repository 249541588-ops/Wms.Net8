using Microsoft.AspNetCore.Mvc;
using Wms.Core.Domain.Requests;
using Wms.Core.WebApi.Jobs;
using Wms.Core.WebApi.Services;

namespace Wms.Core.WebApi.Controllers.Sys;

/// <summary>
/// 定时任务管理控制器（DB 驱动）
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class JobScheduleController : ControllerBase
{
    private readonly BackgroundJobService _jobService;
    private readonly IJobDispatcher _dispatcher;
    private readonly ILogger<JobScheduleController> _logger;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="jobService"></param>
    /// <param name="dispatcher"></param>
    /// <param name="logger"></param>
    public JobScheduleController(
        BackgroundJobService jobService, 
        IJobDispatcher dispatcher, 
        ILogger<JobScheduleController> logger)
    {
        _jobService = jobService;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有定时任务列表
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetJobs()
    {
        try
        {
            var jobs = await _jobService.GetAllAsync();
            var result = jobs.Select(j => new
            {
                id = j.Id,
                name = j.Name,
                jobType = j.JobType,
                cron = j.CronExpression,
                description = j.Description,
                state = j.State,
                createdTime = j.CreatedTime,
                modifiedTime = j.ModifiedTime
            }).ToList();
            return Ok(new { status = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取定时任务列表失败");
            return StatusCode(500, new { status = false, msg = "获取定时任务列表失败: " + ex.Message });
        }
    }

    /// <summary>
    /// 获取可用的内部方法列表（供 internal 模式选择）
    /// </summary>
    [HttpGet("types")]
    public IActionResult GetJobTypes()
    {
        var methods = _dispatcher.GetInternalMethods().Select(m => new
        {
            methodId = m.MethodId,
            displayName = m.DisplayName,
            description = m.Description,
            defaultCron = m.DefaultCron
        }).ToList();
        return Ok(new { status = true, data = methods });
    }

    /// <summary>
    /// 创建新定时任务
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateJob([FromBody] CreateJobRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.JobType))
                return BadRequest(new { status = false, msg = "执行模式不能为空" });
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { status = false, msg = "任务名称不能为空" });
            if (string.IsNullOrWhiteSpace(request.Cron))
                return BadRequest(new { status = false, msg = "Cron 表达式不能为空" });

            var job = await _jobService.CreateAsync(
                request.JobType, request.Name, request.Cron, request.Description,
                request.ApiUrl, request.RequestMethod, request.Payload, request.Headers);
            return Ok(new { status = true, msg = $"任务 {job.Name} 已创建", data = new { id = job.Id } });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { status = false, msg = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建定时任务失败");
            return StatusCode(500, new { status = false, msg = "创建失败: " + ex.Message });
        }
    }

    /// <summary>
    /// 修改任务执行频率
    /// </summary>
    [HttpPut("{id}/cron")]
    public async Task<IActionResult> UpdateCron(Guid id, [FromBody] UpdateCronRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Cron))
                return BadRequest(new { status = false, msg = "cron 表达式不能为空" });

            await _jobService.UpdateCronAsync(id, request.Cron);
            return Ok(new { status = true, msg = "频率已更新" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { status = false, msg = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "修改任务频率失败");
            return StatusCode(500, new { status = false, msg = "修改失败: " + ex.Message });
        }
    }

    /// <summary>
    /// 暂停任务
    /// </summary>
    [HttpPost("{id}/pause")]
    public async Task<IActionResult> PauseJob(Guid id)
    {
        try
        {
            await _jobService.PauseAsync(id);
            return Ok(new { status = true, msg = "任务已暂停" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { status = false, msg = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "暂停任务失败");
            return StatusCode(500, new { status = false, msg = "暂停失败: " + ex.Message });
        }
    }

    /// <summary>
    /// 恢复任务
    /// </summary>
    [HttpPost("{id}/resume")]
    public async Task<IActionResult> ResumeJob(Guid id, [FromQuery] string? cron)
    {
        try
        {
            await _jobService.ResumeAsync(id, cron);
            return Ok(new { status = true, msg = "任务已恢复" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { status = false, msg = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复任务失败");
            return StatusCode(500, new { status = false, msg = "恢复失败: " + ex.Message });
        }
    }

    /// <summary>
    /// 手动触发执行
    /// </summary>
    [HttpPost("{id}/trigger")]
    public async Task<IActionResult> TriggerJob(Guid id)
    {
        try
        {
            await _jobService.TriggerAsync(id);
            return Ok(new { status = true, msg = "任务已触发" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { status = false, msg = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "触发任务失败");
            return StatusCode(500, new { status = false, msg = "触发失败: " + ex.Message });
        }
    }

    /// <summary>
    /// 删除任务
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteJob(Guid id)
    {
        try
        {
            await _jobService.DeleteAsync(id);
            return Ok(new { status = true, msg = "任务已删除" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { status = false, msg = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除任务失败");
            return StatusCode(500, new { status = false, msg = "删除失败: " + ex.Message });
        }
    }
}


