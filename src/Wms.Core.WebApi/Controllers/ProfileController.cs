using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Entities.Identity;
using Wms.Core.Domain.Repositories;
using Wms.Core.WebApi.Models;
using Wms.Core.WebApi.Services;

namespace Wms.Core.WebApi.Controllers;

/// <summary>
/// 个人中心 - 当前用户自助 API
/// </summary>
/// <remarks>
/// 所有端点均从 JWT 中识别当前用户（userId claim），不接受 URL/Body 中的 id 参数，
/// 从而天然避免 IDOR（不安全直接对象引用）。专用 ProfileResponse DTO 不含 PasswordHash/PasswordSalt。
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)] // V9 防缓存 PII
public class ProfileController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IRepository<User, int> _repository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ILogger<ProfileController> _logger;
    private readonly IJwtBlacklistService _jwtBlacklistService;

    // V3 改密失败计数器（per-userId，与 AuthService 登录失败计数器同样思路）
    private static readonly ConcurrentDictionary<int, (int Count, DateTime LockedUntil)> _pwdAttempts = new();
    private const int MaxFailedAttempts = 10;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(30);

    // V10 服务端密码复杂度（与前端 REG_PWD 对齐：6-18 位字母数字下划线）
    private static readonly Regex PasswordPolicy = new(@"^\w{6,18}$", RegexOptions.Compiled);

    public ProfileController(
        IUserRepository userRepository,
        IRepository<User, int> repository,
        IRefreshTokenRepository refreshTokenRepository,
        ILogger<ProfileController> logger,
        IJwtBlacklistService jwtBlacklistService)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _refreshTokenRepository = refreshTokenRepository ?? throw new ArgumentNullException(nameof(refreshTokenRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jwtBlacklistService = jwtBlacklistService ?? throw new ArgumentNullException(nameof(jwtBlacklistService));
    }

    /// <summary>
    /// 获取当前用户资料
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Result<ProfileResponse> GetProfile()
    {
        try
        {
            var (userId, _) = GetCurrentUserIdentifier();
            if (userId == null)
            {
                return Result<ProfileResponse>.Fail("未登录", "401");
            }

            var user = _userRepository.GetByUserid(userId.Value);
            if (user == null)
            {
                return Result<ProfileResponse>.Fail("用户不存在", "404");
            }

            // V6 校验账号活跃状态
            if (!user.IsActive || user.IsLocked)
            {
                return Result<ProfileResponse>.Fail("账号已停用或锁定", "403");
            }

            var response = new ProfileResponse
            {
                UserId = user.Id,
                Username = user.UserName,
                RealName = user.RealName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber
            };

            return Result<ProfileResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取个人资料失败");
            return Result<ProfileResponse>.Fail("操作失败");
        }
    }

    /// <summary>
    /// 更新当前用户资料（真实姓名 / 邮箱 / 电话）
    /// </summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Result UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        try
        {
            // V2 服务端输入校验
            if (!ModelState.IsValid)
            {
                var firstError = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault();
                return Result.Fail(firstError ?? "请求参数不合法", "400");
            }

            var (userId, username) = GetCurrentUserIdentifier();
            if (userId == null)
            {
                return Result.Fail("未登录", "401");
            }

            var user = _userRepository.GetByUserid(userId.Value);
            if (user == null)
            {
                return Result.Fail("用户不存在", "404");
            }

            // V6 校验账号活跃状态
            if (!user.IsActive || user.IsLocked)
            {
                return Result.Fail("账号已停用或锁定", "403");
            }

            // 仅更新非空字段，避免把"未提交"误改成空字符串
            if (request.RealName != null)
                user.RealName = request.RealName;
            if (request.Email != null)
                user.Email = request.Email;
            if (request.PhoneNumber != null)
                user.PhoneNumber = request.PhoneNumber;

            user.ModifiedTime = DateTime.Now;
            user.ModifiedBy = username;

            _repository.Update(user);

            _logger.LogInformation("用户 {Username} 更新个人资料成功", username);
            return Result.Success("资料更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新个人资料失败");
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 修改当前用户密码（自助，需校验旧密码）
    /// </summary>
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<Result> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var firstError = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault();
                return Result.Fail(firstError ?? "请求参数不合法", "400");
            }

            var (userId, username) = GetCurrentUserIdentifier();
            if (userId == null)
            {
                return Result.Fail("未登录", "401");
            }

            var user = _userRepository.GetByUserid(userId.Value);
            if (user == null)
            {
                // V11 不暴露账号存在与否
                return Result.Fail("旧密码不正确");
            }

            // V6 校验账号活跃状态
            if (!user.IsActive || user.IsLocked)
            {
                return Result.Fail("账号已停用或锁定", "403");
            }

            // V3 改密暴力破解保护
            if (_pwdAttempts.TryGetValue(userId.Value, out var attempt) && attempt.LockedUntil > DateTime.UtcNow)
            {
                _logger.LogWarning("改密失败: 用户 {Username} 已被临时锁定（连续失败 {Count} 次）", username, attempt.Count);
                return Result.Fail("尝试次数过多，请稍后再试", "429");
            }

            // V10 服务端密码复杂度校验（防止绕过前端提交弱密码）
            if (!PasswordPolicy.IsMatch(request.NewPassword))
            {
                return Result.Fail("密码 6-18 位，仅允许字母、数字、下划线", "400");
            }

            // 校验旧密码
            if (!user.ValidatePassword(request.OldPassword))
            {
                // 累加失败计数
                _pwdAttempts.AddOrUpdate(userId.Value,
                    _ => (1, DateTime.MinValue),
                    (_, prev) => (prev.Count + 1,
                        prev.Count + 1 >= MaxFailedAttempts ? DateTime.UtcNow.Add(LockoutDuration) : prev.LockedUntil));

                _logger.LogWarning("改密失败: 用户 {Username} 旧密码错误（累计 {Count} 次）",
                    username, _pwdAttempts[userId.Value].Count);
                return Result.Fail("旧密码不正确");
            }

            // 通过校验，清除失败计数
            _pwdAttempts.TryRemove(userId.Value, out _);

            // 设置新密码并先持久化（避免新密码未落库就被登出）
            user.SetPassword(request.NewPassword);
            user.ModifiedTime = DateTime.Now;
            user.ModifiedBy = username;
            _repository.Update(user);

            // V4 吊销该用户所有 refresh token，强制其他会话登出
            try
            {
                _refreshTokenRepository.RevokeAllUserTokens(userId.Value);
            }
            catch (Exception revEx)
            {
                // 吊销失败不应阻塞改密主流程，仅记录日志
                _logger.LogWarning(revEx, "改密后吊销 refresh token 失败，用户 {Username}", username);
            }

            // R503: 将当前 JWT 加入黑名单，使改密后当前 access token 立即失效。
            // 配合 V4 的 refresh token 吊销，实现"改密即下线"全链路保护。
            // （fail-open：缓存后端故障时不阻塞改密主流程，仅记录 [FAIL-OPEN] 日志）
            try
            {
                var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                var expClaim = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
                if (!string.IsNullOrWhiteSpace(jti) && long.TryParse(expClaim, out var expUnix))
                {
                    var expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
                    await _jwtBlacklistService.RevokeAsync(jti, expiresAtUtc, $"用户修改密码: {username}");
                }
            }
            catch (Exception blacklistEx)
            {
                _logger.LogWarning(blacklistEx,
                    "[R503] 改密后加入 JWT 黑名单失败（fail-open，不阻塞改密）。用户 {Username}", username);
            }

            _logger.LogInformation("用户 {Username} 修改密码成功", username);
            return Result.Success("密码修改成功，请使用新密码重新登录");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "修改密码失败");
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 从 JWT claims 提取当前用户标识
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
}
