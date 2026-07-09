using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Entities.Identity;
using Wms.Core.Domain.Repositories;
using Wms.Core.Application.Ports;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.WebApi.Models;
using Wms.Core.Domain.Common;
using Wms.Core.WebApi.Extensions;

namespace Wms.Core.WebApi.Controllers;

/// <summary>
/// 用户管理 API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
//[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly IRepository<User, int> _repository;
    private readonly IUserRepository _userRepository;
    private readonly IAuthService _authService;
    private readonly WmsDbContext _dbContext;
    private readonly ILogger<UsersController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public UsersController(
        IRepository<User, int> repository,
        IUserRepository userRepository,
        IAuthService authService,
        WmsDbContext dbContext,
        ILogger<UsersController> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 填充用户角色名称
    /// </summary>
    private void FillUserRole(List<User> users)
    {
        var userIds = users.Select(u => u.Id).ToList();
        var roleMap = _dbContext.Set<UserRoles>()
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(_dbContext.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.RoleName })
            .ToDictionary(x => x.UserId, x => x.RoleName);
        foreach (var user in users)
        {
            user.Role = roleMap.GetValueOrDefault(user.Id);
        }
    }

    /// <summary>
    /// 从 JWT claims 提取当前用户标识（参考 ProfileController）
    /// </summary>
    private (int? UserId, string? Username) GetCurrentUserIdentifier()
    {
        var userIdStr = User.FindFirst("userId")?.Value;
        var username = User.FindFirst("username")?.Value;

        if (int.TryParse(userIdStr, out var userId))
        {
            return (userId, username);
        }

        return (null, username);
    }

    /// <summary>
    /// 获取所有用户
    /// </summary>
    /// <param name="keyword">搜索关键字（可选）</param>
    /// <param name="status">状态（可选）</param>
    /// <param name="pageNumber">页码（默认 1）</param>
    /// <param name="pageSize">每页大小（默认 20，最大 100）</param>
    /// <returns>数据列表</returns>
    [HttpGet]
    //[ResponseCache(Duration = 30, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "keyword", "status", "pageNumber", "pageSize" })]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result GetAll(string? keyword = null, string? status = null, int pageNumber = 1, int pageSize = 20)
    {
        try
        {
            // 限制每页最大大小
            pageSize = Math.Min(pageSize, 100);

            var query = _repository.GetAll();

            // 默认排除已软删除的用户（DeletedAt == null 即未删除）
            query = query.Where(m => m.DeletedAt == null);

            // 关键字搜索
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(m => m.UserName.Contains(keyword) || m.RealName.Contains(keyword));
            }
            if (!string.IsNullOrEmpty(status))
            {
                bool _isActive = status == "1" ? true : false;
                // status 过滤仅在未删除用户中区分"启用/禁用"
                query = query.Where(m => m.IsActive == _isActive);
            }


            // 获取总数
            var totalCount = query.Count();

            // 分页
            var lists = query
                .OrderBy(m => m.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            FillUserRole(lists);

            var pagedResponse = new PagedResult<UserResponse>
            {
                Data = lists.Select(UserResponse.From),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Result<PagedResult<UserResponse>>.Success(pagedResponse, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取列表失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 根据 ID 获取用户
    /// </summary>
    /// <param name="id">用户 ID</param>
    /// <returns>用户详情</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Result GetById(int id)
    {
        try
        {
            var model = _repository.GetById(id);
            if (model == null || model.DeletedAt != null)
            {
                // 已软删除用户对前端不可见，统一返回 404
                return Result.Fail("记录不存在", "404");
            }
            FillUserRole(new List<User> { model });
            return Result<UserResponse>.Success(UserResponse.From(model), "获取成功");
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
    public async Task<Result> Create([FromBody] CreateUserRequest request)
    {
        try
        {
            //if (!ModelState.IsValid)
            //{
            //    return BadRequest(ModelState);
            //}

            var user = await _authService.CreateUserAsync(
                request.Username,
                request.Password,
                request.RealName,
                request.Email,
                request.PhoneNumber,
                request.Role ?? "User",
                request.CreatedBy
            );
            return Result<User>.Success(user, "创建成功");
        }
        //catch (InvalidOperationException ex)
        //{
        //    return BadRequest(new { Message = ex.Message });
        //}
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建失败: {Message}", ex.Message);
            var innerMsg = ex.InnerException?.Message ?? ex.Message;
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
    public Result Update(int id, [FromBody] UpdateUserRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return Result.Fail("请求参数无效");
            }

            var model = _repository.GetById(id);
            if (model == null || model.DeletedAt != null)
            {
                return Result.Fail("记录不存在", "404");
            }

            // 更新字段
            if (request.RealName != null)
                model.RealName = request.RealName;
            if (request.Email != null)
                model.Email = request.Email;
            if (request.PhoneNumber != null)
                model.PhoneNumber = request.PhoneNumber;
            if (request.ModifiedBy != null)
                model.ModifiedBy = request.ModifiedBy;

            model.ModifiedTime = DateTime.Now;

            // 更新角色关联
            if (request.Role != null)
            {
                var roleEntity = _dbContext.Set<Role>().FirstOrDefault(r => r.RoleName == request.Role);
                if (roleEntity != null)
                {
                    _dbContext.Database.ExecuteSqlRaw("DELETE FROM UserRoles WHERE UserId = @userId",
                        new Microsoft.Data.SqlClient.SqlParameter("@userId", id));
                    _dbContext.Database.ExecuteSqlRaw(
                        "INSERT INTO UserRoles (UserId, RoleId) VALUES (@userId, @roleId)",
                        new Microsoft.Data.SqlClient.SqlParameter("@userId", id),
                        new Microsoft.Data.SqlClient.SqlParameter("@roleId", roleEntity.Id));
                }
            }

            _repository.Update(model);

            return Result<User>.Success(model, "更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新失败: {Message}", ex.Message);
            var innerMsg = ex.InnerException?.Message ?? ex.Message;
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 删除用户（软删除：保留数据用于审计，不可登录、不可见）
    /// </summary>
    /// <param name="id">用户 ID</param>
    /// <returns>操作结果</returns>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Result Delete(int id)
    {
        try
        {
            var model = _repository.GetById(id);
            if (model == null || model.DeletedAt != null)
            {
                // 已物理缺失或已软删除，统一返回 404（避免泄露存在性）
                return Result.Fail("记录不存在", "404");
            }

            // 内置用户禁止删除
            if (model.IsBuiltIn)
            {
                return Result.Fail("内置用户不可删除", "409");
            }

            // 软删除：保留数据与角色关联用于审计/恢复，仅标记状态
            // 注意：不再执行 DELETE FROM UserRoles，保留角色关联以便后续审计或恢复
            model.IsActive = false;
            model.DeletedAt = DateTime.Now;
            model.DeletedBy = User?.Identity?.Name;
            model.ModifiedTime = DateTime.Now;
            model.ModifiedBy = User?.Identity?.Name;
            _repository.Update(model);

            _logger.LogWarning("用户 {UserId} ({UserName}) 已被软删除，操作人：{Operator}",
                model.Id, model.UserName, model.DeletedBy);

            return Result.Success("删除成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 修改用户密码
    /// </summary>
    /// <param name="id">用户 ID</param>
    /// <param name="request">修改密码请求</param>
    /// <returns>操作结果</returns>
    [HttpPost("{id:int}/changepassword")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<Result> ChangePassword(int id, [FromBody] ChangePasswordRequest request)
    {
        try
        {
            // T303 IDOR 防护：仅允许本人或管理员修改密码
            var (callerUserId, callerUsername) = GetCurrentUserIdentifier();
            if (callerUserId == null)
            {
                return Result.Fail("未登录", "401");
            }
            var isAdmin = User.IsInRole("Admin");
            if (id != callerUserId.Value && !isAdmin)
            {
                _logger.LogWarning("IDOR 拦截: 用户 {Caller} 试图修改 id={TargetId} 的密码", callerUsername, id);
                return Result.Fail("无权修改其他用户的密码", "403");
            }

            var success = await _authService.ChangePasswordAsync(id, request.OldPassword, request.NewPassword);
            if (!success)
            {
                return Result.Fail("旧密码不正确");
            }

            _logger.LogInformation("用户 {Caller} 修改了 id={TargetId} 的密码", callerUsername, id);
            return Result.Success("密码修改成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "修改密码失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 重置密码
    /// </summary>
    /// <param name="id">用户 ID</param>
    /// <param name="request">重置密码请求</param>
    /// <returns></returns>
    [HttpPost("{id:int}/resetpassword")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<Result> ResetPassword(int id, [FromBody] ChangePasswordRequest request)
    {
        try
        {
            var success = await _authService.ResetPasswordAsync(id,  request.NewPassword);
            if (!success)
            {
                return Result.Fail("重置密码失败");
            }

            return Result.Success("密码重置成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重置密码失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 启用/禁用用户
    /// </summary>
    /// <param name="id">用户 ID</param>
    /// <param name="request">启用/禁用请求</param>
    /// <returns>操作结果</returns>
    [HttpPatch("{id:int}/changestatus")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Result ChangeStatus(int id, [FromBody] SetUserEnabledRequest request)
    {
        try
        {
            var user = _repository.GetById(id);
            if (user == null || user.DeletedAt != null)
            {
                // 已软删除用户不允许变更启用/锁定状态
                return Result.Fail("记录不存在", "404");
            }

            if (request.Type == 1)
                user.IsActive = request.Enabled;
            else
                user.IsLocked = request.Enabled;
            user.ModifiedTime = DateTime.Now;
            user.ModifiedBy = request.ModifiedBy;

            _repository.Update(user);

            return Result.Success("设置成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置用户状态失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }
}
