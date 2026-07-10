using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Entities;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Repositories;
using Wms.Core.Domain.Common;
using Wms.Core.WebApi.Extensions;
using Wms.Core.WebApi.Filters;
using Wms.Core.WebApi.Helpers;
using Wms.Core.WebApi.Middleware;

namespace Wms.Core.WebApi.Controllers;

/// <summary>
/// 多语言管理 API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public class Sys_LanguageController : ControllerBase
{
    private readonly IRepository<Sys_Language, int> _repository;
    private readonly ILogger<Sys_LanguageController> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly ILanguagePackCacheTracker _cacheTracker;

    // 缓存键前缀
    private const string CACHE_KEY_PREFIX = "LanguagePack_";
    // 缓存过期时间（分钟）
    private const int CACHE_EXPIRATION_MINUTES = 30;

    /// <summary>
    /// 清除语言包缓存
    /// </summary>
    private void ClearLanguagePackCache()
    {
        try
        {
            _memoryCache.ClearLanguagePackCache(_cacheTracker, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清除缓存失败: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public Sys_LanguageController(
        IRepository<Sys_Language, int> repository,
        ILogger<Sys_LanguageController> logger,
        IMemoryCache memoryCache,
        ILanguagePackCacheTracker cacheTracker)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _cacheTracker = cacheTracker ?? throw new ArgumentNullException(nameof(cacheTracker));
    }

    /// <summary>
    /// 获取所有多语言
    /// </summary>
    /// <param name="keyword">搜索关键字（可选）</param>
    /// <param name="module">模块筛选（可选）</param>
    /// <param name="pageNumber">页码（默认 1）</param>
    /// <param name="pageSize">每页大小（默认 20，最大 100）</param>
    /// <param name="acceptLanguage">从 header 中获取的语言偏好</param>
    /// <returns>数据列表</returns>
    [HttpGet]
    //[ResponseCache(Duration = 30, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "keyword", "module", "pageNumber", "pageSize" })]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result GetAll(string? keyword = null, string? module = null, int pageNumber = 1, int pageSize = 20)
    {
        try
        {
            // 限制每页最大大小
            pageSize = Math.Min(pageSize, 100);

            var query = _repository.GetAll();

            // 根据当前语言进行关键字搜索
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(m => m.Chinese.Contains(keyword)
                    || (m.English != null && m.English.Contains(keyword))
                    || (m.Deutsch != null && m.Deutsch.Contains(keyword))
                    || (m.Indonesian != null && m.Indonesian.Contains(keyword)));
            }

            // 按模块筛选
            if (!string.IsNullOrEmpty(module))
            {
                query = query.Where(m => m.Module == module);
            }

            // 获取总数
            var totalCount = query.Count();

            // 分页
            var lists = query
                .OrderBy(m => m.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pagedResponse = new PagedResult<Sys_Language>
            {
                Data = lists,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Result<PagedResult<Sys_Language>>.Success(pagedResponse, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取列表失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 根据 ID 获取对象
    /// </summary>
    /// <param name="id">ID</param>
    /// <returns>对象详情</returns>
    [HttpGet("{id:int}")]
    //[ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Result GetById(int id)
    {
        try
        {
            var model = _repository.GetById(id);
            if (model == null)
            {
                return Result.Fail("记录不存在", "404");
            }

            return Result<Sys_Language>.Success(model, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取对象失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 创建新对象
    /// </summary>
    /// <param name="request">创建请求</param>
    /// <returns>创建的对象</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Result Create([FromBody] CreateSys_LanguageRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return Result.Fail("请求参数无效");
            }

            // 验证中文是否唯一
            if (_repository.Exists(m => m.Chinese == request.Chinese))
            {
                return Result.Fail("中文已存在");
            }

            var model = new Sys_Language
            {
                Chinese = request.Chinese,
                ChineseDesc = request.ChineseDesc,
                English = request.English,
                Deutsch = request.Deutsch,
                Indonesian = request.Indonesian,
                Module = request.Module,
                IsPackageContent = request.IsPackageContent,
                Creator = request.Creator,
                CreateDate = DateTime.Now,
            };

            var createdModel = _repository.Add(model);

            // 清除语言包缓存
            ClearLanguagePackCache();

            return Result<Sys_Language>.Success(createdModel, "创建成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 更新对象
    /// </summary>
    /// <param name="id">对象 ID</param>
    /// <param name="request">更新请求</param>
    /// <returns>更新后的对象</returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Result Update(int id, [FromBody] UpdateSys_LanguageRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return Result.Fail("请求参数无效");
            }

            var model = _repository.GetById(id);
            if (model == null)
            {
                return Result.Fail("记录不存在", "404");
            }

            // 更新字段
            if (request.Chinese != null)
                model.Chinese = request.Chinese;
            if (request.ChineseDesc != null)
                model.ChineseDesc = request.ChineseDesc;
            if (request.English != null)
                model.English = request.English;
            if (request.Deutsch != null)
                model.Deutsch = request.Deutsch;
            if (request.Indonesian != null)
                model.Indonesian = request.Indonesian;
            if (request.Module != null)
                model.Module = request.Module;
            if (request.IsPackageContent.HasValue)
                model.IsPackageContent = request.IsPackageContent.Value;
            if (request.Modifier != null)
                model.Modifier = request.Modifier;

            model.ModifyDate = DateTime.Now;

            _repository.Update(model);

            // 清除语言包缓存
            ClearLanguagePackCache();

            return Result<Sys_Language>.Success(model, "更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 删除对象
    /// </summary>
    /// <param name="id">对象 ID</param>
    /// <returns>操作结果</returns>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Result Delete(int id)
    {
        try
        {
            var model = _repository.GetById(id);
            if (model == null)
            {
                return Result.Fail("记录不存在", "404");
            }

            _repository.Delete(id);

            // 清除语言包缓存
            ClearLanguagePackCache();

            return Result.Success("删除成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 批量删除对象
    /// </summary>
    /// <param name="request">批量删除请求</param>
    /// <returns>操作结果</returns>
    [HttpPost("BatchDelete")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Result BatchDelete([FromBody] BatchDeleteRequest request)
    {
        try
        {
            if (request?.Ids == null || !request.Ids.Any())
            {
                return Result.Fail("请选择要删除的记录");
            }

            int successCount = 0;
            int failCount = 0;
            List<string> errors = new List<string>();

            foreach (int id in request.Ids)
            {
                try
                {
                    var model = _repository.GetById(id);
                    if (model != null)
                    {
                        _repository.Delete(id);
                        successCount++;
                    }
                    else
                    {
                        failCount++;
                        errors.Add($"#{id} {HttpContext.Translate("记录不存在")}");
                    }
                }
                catch (Exception ex)
                {
                    failCount++;
                    errors.Add($"#{id} {HttpContext.Translate("失败")}: {ex.Message}");
                }
            }

            string message = $"批量删除完成: 成功 {successCount} 个";
            if (failCount > 0)
            {
                message += $", 失败 {failCount} 个";
                if (errors.Any())
                {
                    message += $". {string.Join("; ", errors.Take(3))}";
                    if (errors.Count > 3)
                    {
                        message += "...";
                    }
                }
            }

            if (failCount == 0)
            {
                // 清除语言包缓存
                ClearLanguagePackCache();

                return Result.Success(message);
            }
            else if (successCount > 0)
            {
                // 清除语言包缓存
                ClearLanguagePackCache();

                return Result.Success(message);
            }
            else
            {
                return Result.Fail(message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量删除失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 获取前端语言包（键值对格式）
    /// </summary>
    /// <param name="lang">语言类型：zh-中文, en-英文, de-德文, id-印尼文（默认：zh）</param>
    /// <param name="module">模块筛选（可选，不传则获取所有模块）</param>
    /// <returns>语言包键值对</returns>
    [HttpGet("LanguagePack")]
    [AllowAnonymous]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "lang", "module" })]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result GetLanguagePack(string lang = "zh", string? module = null)
    {
        try
        {
            // 生成缓存键：包含语言和模块信息
            var cacheKey = $"{CACHE_KEY_PREFIX}{lang?.ToLower() ?? "zh"}_{module ?? "all"}";

            // 尝试从缓存获取
            if (_memoryCache.TryGetValue(cacheKey, out object? cachedResult))
            {
                _logger.LogInformation("从缓存获取语言包成功 - CacheKey: {CacheKey}", cacheKey);
                return Result<object>.Success(cachedResult, "获取成功");
            }

            // 缓存未命中，从数据库查询
            _logger.LogInformation("缓存未命中，从数据库查询 - CacheKey: {CacheKey}", cacheKey);

            var query = _repository.GetAll();

            // 按模块筛选
            if (!string.IsNullOrEmpty(module))
            {
                query = query.Where(m => m.Module == module);
            }

            var languages = query.ToList();

            var result = new
            {
                lang = lang?.ToLower() ?? "zh",
                version = DateTime.Now.ToString("yyyyMMddHHmmss"),
                data = languages,
            };

            // 存入缓存，设置过期时间
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(CACHE_EXPIRATION_MINUTES))
                .SetSlidingExpiration(TimeSpan.FromMinutes(5)) // 5分钟内没有访问则过期
                .RegisterPostEvictionCallback((key, value, reason, state) =>
                {
                    _logger.LogInformation("缓存过期移除 - Key: {Key}, Reason: {Reason}", key, reason);
                });

            _memoryCache.Set(cacheKey, result, cacheEntryOptions);
            _cacheTracker.AddKey(cacheKey); // 跟踪缓存键

            _logger.LogInformation("语言包已缓存 - CacheKey: {CacheKey}, Count: {Count}", cacheKey, languages.Count);

            return Result<object>.Success(result, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取语言包失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 手动清除语言包缓存（管理员功能）
    /// </summary>
    /// <returns>操作结果</returns>
    [HttpPost("ClearCache")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Result ClearCache()
    {
        try
        {
            ClearLanguagePackCache();

            return Result<object>.Success(new
            {
                clearedAt = DateTime.Now,
                message = $"{HttpContext.Translate("成功")}"  // "下次请求将从数据库重新加载语言包"
            }, $"{HttpContext.Translate("成功")}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清除缓存失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }
}
