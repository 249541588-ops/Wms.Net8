using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Requests;
using Wms.Core.Domain.Entities;
using Wms.Core.Domain.Entities.Identity;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Repositories;
using Wms.Core.Application.Ports;
using Wms.Core.Domain.Common;
using Wms.Core.WebApi.Extensions;
using Wms.Core.WebApi.Helpers;
using Wms.Core.WebApi.Models;

namespace Wms.Core.WebApi.Controllers;

/// <summary>
/// 角色管理 API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
//[Authorize(Roles = "Admin")]
public class RoleController : ControllerBase
{
    private readonly IRepository<Role, int> _repository;
    private readonly IRoleService _roleService;
    private readonly IRepository<Role_Menu_Funs, long> _repositoryFuns;
    private readonly IRepository<Menus, int> _repositoryMenus;
    //private readonly IUserRepository _userRepository;
    private readonly ILogger<RoleController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public RoleController(
        IRepository<Role, int> repository,
        IRoleService roleService,
        IRepository<Role_Menu_Funs, long> repositoryFuns,
        IRepository<Menus, int> repositoryMenus,
        ILogger<RoleController> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _roleService = roleService ?? throw new ArgumentNullException(nameof(roleService));
        _repositoryFuns = repositoryFuns ?? throw new ArgumentNullException(nameof(repositoryFuns));
        _repositoryMenus = repositoryMenus ?? throw new ArgumentNullException(nameof(repositoryMenus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取所有用户
    /// </summary>
    /// <param name="keyword">搜索关键字（可选）</param>
    /// <param name="pageNumber">页码（默认 1）</param>
    /// <param name="pageSize">每页大小（默认 20，最大 100）</param>
    /// <returns>数据列表</returns>
    [HttpGet]
    //[ResponseCache(Duration = 30, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "keyword", "pageNumber", "pageSize" })]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result GetAll(string? keyword = null, int pageNumber = 1, int pageSize = 20)
    {
        try
        {
            // 限制每页最大大小
            pageSize = Math.Min(pageSize, 100);

            var query = _repository.GetAll();

            // 关键字搜索
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(m => m.RoleName.Contains(keyword));
            }

            // 获取总数
            var totalCount = query.Count();

            // 分页
            var lists = query
                .OrderBy(m => m.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pagedResponse = new PagedResult<Role>
            {
                Data = lists,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Result<PagedResult<Role>>.Success(pagedResponse, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取列表失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 根据 ID 获取
    /// </summary>
    /// <param name="id">ID</param>
    /// <returns>详情</returns>
    [HttpGet("{id:int}")]
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
            return Result<Role>.Success(model, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取对象失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 创建新用户
    /// </summary>
    /// <param name="request">创建用户请求</param>
    /// <returns>创建的用户</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<Result> Create([FromBody] CreateRoleRequest request)
    {
        try
        {
            Role model = new Role()
            {
                RoleName = request.RoleName,
                Description = request.Description,
                IsBuiltIn = request.IsBuiltIn,
                CreatedBy = request.CreatedBy,
                CreatedTime = DateTime.Now,
            };
            _repository.Add(model);

            return Result<Role>.Success(model, "创建成功");
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
    public Result Update(int id, [FromBody] UpdateRoleRequest request)
    {
        try
        {
            //if (!ModelState.IsValid)
            //{
            //    return Result.Fail();
            //}

            var model = _repository.GetById(id);
            if (model == null)
            {
                return Result.Fail("记录不存在", "404");
            }

            // 更新字段
            if (request.RoleName != null)
                model.RoleName = request.RoleName;
            if (request.Description != null)
                model.Description = request.Description;

            model.IsBuiltIn = request.IsBuiltIn;

            if (request.ModifiedBy != null)
                model.ModifiedBy = request.ModifiedBy;
            model.ModifiedTime = DateTime.Now;

            _repository.Update(model);

            return Result<Role>.Success(model, "更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 删除对象
    /// </summary>
    /// <param name="id">对象 ID</param>
    /// <returns>操作结果</returns>
    [HttpDelete("{id:int}")]
    //[Authorize(Roles = "Admin")]
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
    //[Authorize(Roles = "Admin")]
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
                return Result.Success(message);
            }
            else if (successCount > 0)
            {
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
    /// 初始化 Admin 角色所有操作按钮权限
    /// </summary>
    /// <returns>操作结果</returns>
    [HttpPost("InitialConfiguration")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<Result> InitialConfiguration()
    {
        try
        {
            await _roleService.InitialConfiguration();
            return Result.Success("初始化成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 设置角色菜单
    /// </summary>
    /// <param name="request">请求</param>
    /// <returns>操作结果</returns>
    [HttpPost("SettingRoleMenus")]
    //[Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<Result> SettingRoleMenus(int id, [FromBody] List<SettingRoleMenusRequest> request)
    {
        try
        {
            if (request == null || request.Count == 0)
            {
                return Result.Fail("请选择要设置的菜单");
            }

            var model = _repository.GetById(id);
            if (model == null)
            {
                return Result.Fail("记录不存在", "404");
            }

            await _roleService.SettingRoleMenus(id, request);
            return Result.Success("设置成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 获取角色的菜单权限
    /// </summary>
    /// <param name="id">角色 ID</param>
    /// <returns>菜单权限列表</returns>
    [HttpGet("{id}/Menus")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Result GetRoleMenus(int id)
    {
        try
        {
            var role = _repository.GetById(id);
            if (role == null)
            {
                return Result.Fail("记录不存在", "404");
            }

            // 获取所有菜单信息，用于判断菜单层级
            var allMenus = _repositoryMenus.GetAll().ToList();

            // 查询角色按钮权限
            var roleButtonFuns = _repositoryFuns.GetAll()
                .Where(x => x.RoleId == id)
                .ToList();

            // 只返回二级菜单（ParentId != 0）的权限
            // 一级菜单会根据子菜单的选中状态自动显示为半选中或全选中
            var result = roleButtonFuns
                .Where(rf => {
                    var menu = allMenus.FirstOrDefault(m => m.Id == rf.MenuId);
                    return menu != null && menu.ParentId != 0; // 只返回二级菜单
                })
                .GroupBy(rf => rf.MenuId)
                .Select(g => new
                {
                    menuId = g.Key,
                    btn = g.Select(x => x.FunctionButton).ToArray()
                })
                .ToList();

            _logger.LogInformation("角色 {RoleId} 的菜单权限: {Count} 个二级菜单", id, result.Count);
            return Result<object>.Success(result, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取角色菜单失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }
}
