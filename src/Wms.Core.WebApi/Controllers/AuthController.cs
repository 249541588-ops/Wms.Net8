using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;
using Wms.Core.Domain.Entities;
using Wms.Core.Domain.Entities.Identity;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Repositories;
using Wms.Core.Application.Ports;
using Wms.Core.Domain.Common;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.WebApi.Extensions;
using Wms.Core.WebApi.Configuration;
using Wms.Core.WebApi.Models;
using Wms.Core.WebApi.Services;

namespace Wms.Core.WebApi.Controllers;

/// <summary>
/// 认证授权 API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly ITokenService _tokenService;
    private readonly IAuthService _authService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ILogger<AuthController> _logger;
    private readonly WmsDbContext _dbContext;
    private readonly IJwtBlacklistService _jwtBlacklistService;

    /// <summary>
    /// 构造函数
    /// </summary>
    public AuthController(
        ITokenService tokenService,
        IAuthService authService,
        IRefreshTokenRepository refreshTokenRepository,
        ILogger<AuthController> logger,
        WmsDbContext dbContext,
        IJwtBlacklistService jwtBlacklistService)
    {
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _refreshTokenRepository = refreshTokenRepository ?? throw new ArgumentNullException(nameof(refreshTokenRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = dbContext;
        _jwtBlacklistService = jwtBlacklistService ?? throw new ArgumentNullException(nameof(jwtBlacklistService));
    }

    /// <summary>
    /// 用户登录
    /// </summary>
    /// <param name="request">登录请求</param>
    /// <returns>登录响应（Token 和用户信息）</returns>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<Result> Login([FromBody] LoginRequest request)
    {
        try
        {
            var userEntity = await _authService.LoginAsync(request.Username, request.Password);
            if (userEntity == null)
            {
                _logger.LogWarning("登录失败: 用户名 {Username}", request.Username);
                return Result.Fail("用户名或密码错误", "401");
            }

            _logger.LogInformation("用户 {Username} 登录成功", request.Username);

            // 直接通过 UserRoles 表查询角色，避免导航属性问题
            var primaryRole = _dbContext.Set<UserRoles>()
                .Where(ur => ur.UserId == userEntity.Id)
                .Join(_dbContext.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.RoleName)
                .FirstOrDefault() ?? "User";
            var userInfo = new UserInfo
            {
                UserId = userEntity.Id.ToString(),
                Username = userEntity.UserName,
                DisplayName = userEntity.RealName,
                Role = primaryRole,
                Permissions = GetPermissionsForRole(primaryRole)
            };

            // 生成 Token
            var (token, expiration, tokenId) = _tokenService.GenerateToken(
                userInfo.UserId,
                userInfo.Username,
                userInfo.Role,
                userInfo.Permissions
            );

            var refreshTokenString = _tokenService.GenerateRefreshToken();

            // R502: 每次登录生成新的 Token 家族 ID，刷新链路上的所有 token 共享此 ID。
            // 一旦发现 token 重用（已使用/已撤销的 token 被再次提交），立即吊销整个家族。
            var familyId = Guid.NewGuid().ToString("N");

            // 保存 RefreshToken 到数据库（有效期 7 天）
            var refreshTokenEntity = new RefreshToken
            {
                JwtTokenId = tokenId,
                UserId = userEntity.Id,
                UserName = userEntity.UserName,
                FamilyId = familyId,
                CreatedTime = DateTime.UtcNow,
                ExpiryTime = DateTime.UtcNow.AddDays(7),
                IpAddress = HttpContext.Connection?.RemoteIpAddress?.ToString(),
                UserAgent = HttpContext.Request?.Headers["User-Agent"].ToString()
            };
            refreshTokenEntity.SetTokenHash(refreshTokenString);

            _refreshTokenRepository.Create(refreshTokenEntity);

            var response = new LoginResponse
            {
                Token = token,
                RefreshToken = refreshTokenString,
                Expiration = expiration,
                User = userInfo
            };

            return Result<LoginResponse>.Success(response, "登录成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登录失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 刷新 Token
    /// </summary>
    /// <param name="request">刷新 Token 请求</param>
    /// <returns>新的登录响应</returns>
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        try
        {
            // 验证刷新 Token
            var refreshToken = _refreshTokenRepository.GetByToken(request.RefreshToken);

            // R502 RefreshToken 重用检测（RFC 6749 Section 10.4）
            // 1) Token 不存在 → 直接拒绝
            // 2) Token 已过期/已撤销/已使用 → 视为重用攻击信号，吊销整个家族后拒绝
            // 3) Token 有效 → 正常刷新，新 token 继承同一 FamilyId
            if (refreshToken == null)
            {
                _logger.LogWarning("刷新 Token 不存在或已被清理");
                return Unauthorized(new { Message = "刷新 Token 无效或已过期" });
            }

            if (!refreshToken.IsValid())
            {
                var ip = HttpContext.Connection?.RemoteIpAddress?.ToString();

                // 区分"自然过期"与"已使用/已撤销（重用信号）"
                if (refreshToken.IsUsed || refreshToken.IsRevoked)
                {
                    // 该 token 已经走完过正常生命周期，现在又被提交 → 可能被盗用。
                    // 立即吊销整个 FamilyId 家族（包括刷新链路上后续产生的所有 token），
                    // 强制用户重新登录，同时让攻击者手中被盗的链路立即失效。
                    if (!string.IsNullOrWhiteSpace(refreshToken.FamilyId))
                    {
                        var revokedCount = _refreshTokenRepository.RevokeFamily(
                            refreshToken.FamilyId,
                            "检测到 RefreshToken 重用（token 已使用或已撤销后被再次提交）");

                        _logger.LogWarning(
                            "[安全事件][R502] RefreshToken 重用被检测并阻断。FamilyId={FamilyId}, UserName={UserName}, IP={IP}, 撤销数={Count}",
                            refreshToken.FamilyId, refreshToken.UserName, ip, revokedCount);
                    }
                    else
                    {
                        // 迁移兼容：旧 token 没有 FamilyId，无法批量撤销家族。
                        // 仍记录告警，便于运维识别异常。
                        _logger.LogWarning(
                            "[安全事件][R502] 检测到 RefreshToken 重用，但 token 无 FamilyId（迁移期旧 token），仅记录。UserName={UserName}, IP={IP}, IsUsed={IsUsed}, IsRevoked={IsRevoked}",
                            refreshToken.UserName, ip, refreshToken.IsUsed, refreshToken.IsRevoked);
                    }

                    return Unauthorized(new { Message = "刷新 Token 已失效，请重新登录" });
                }

                // 走到这里说明：!IsValid() 但既非 IsUsed 也非 IsRevoked → 仅可能 IsExpired
                _logger.LogWarning("刷新 Token 已过期。UserName={UserName}", refreshToken.UserName);
                return Unauthorized(new { Message = "刷新 Token 无效或已过期" });
            }

            if (string.IsNullOrWhiteSpace(refreshToken.UserName))
            {
                _logger.LogWarning("用户为空");
                return Unauthorized(new { Message = "用户为空" });
            }

            // 获取用户信息
            var userEntity = await _authService.LoginAsync(
                refreshToken.UserName.ToString(),
                string.Empty); // 密码验证跳过，因为有有效的 RefreshToken

            if (userEntity == null)
            {
                return Unauthorized(new { Message = "用户不存在" });
            }

            // 标记旧的 RefreshToken 为已使用
            refreshToken.IsUsed = true;
            _refreshTokenRepository.RevokeToken(refreshToken);

            // 生成新的 Token
            var primaryRole = _dbContext.Set<UserRoles>()
                .Where(ur => ur.UserId == userEntity.Id)
                .Join(_dbContext.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.RoleName)
                .FirstOrDefault() ?? "User";
            var userInfo = new UserInfo
            {
                UserId = userEntity.Id.ToString(),
                Username = userEntity.UserName,
                DisplayName = userEntity.RealName,
                Role = primaryRole,
                Permissions = GetPermissionsForRole(primaryRole)
            };

            var (token, expiration, tokenId) = _tokenService.GenerateToken(
                userInfo.UserId,
                userInfo.Username,
                userInfo.Role,
                userInfo.Permissions
            );

            var newRefreshTokenString = _tokenService.GenerateRefreshToken();

            // R502: 新 token 继承旧 token 的 FamilyId。
            // 迁移兼容：旧 token 无 FamilyId 时为新链路生成一个新 FamilyId，
            // 后续重用检测即可正常工作。
            var inheritedFamilyId = !string.IsNullOrWhiteSpace(refreshToken.FamilyId)
                ? refreshToken.FamilyId
                : Guid.NewGuid().ToString("N");

            // 保存新的 RefreshToken
            var newRefreshToken = new RefreshToken
            {
                JwtTokenId = tokenId,
                UserId = userEntity.Id,
                UserName = userEntity.UserName,
                FamilyId = inheritedFamilyId,
                CreatedTime = DateTime.UtcNow,
                ExpiryTime = DateTime.UtcNow.AddDays(7),
                IpAddress = HttpContext.Connection?.RemoteIpAddress?.ToString(),
                UserAgent = HttpContext.Request?.Headers["User-Agent"].ToString()
            };
            newRefreshToken.SetTokenHash(newRefreshTokenString);

            _refreshTokenRepository.Create(newRefreshToken);

            _logger.LogInformation("用户 {Username} 刷新 Token 成功", userEntity.UserName);

            var response = new LoginResponse
            {
                Token = token,
                RefreshToken = newRefreshTokenString,
                Expiration = expiration,
                User = userInfo
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刷新 Token 失败: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "操作失败" });
        }
    }

    /// <summary>
    /// 获取当前用户信息
    /// </summary>
    /// <returns>当前用户信息</returns>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetCurrentUser()
    {
        try
        {
            var userId = User.FindFirst("userId")?.Value;
            var username = User.FindFirst("username")?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var permissions = User.FindAll("permission").Select(c => c.Value).ToArray();

            var user = new UserInfo
            {
                UserId = userId ?? string.Empty,
                Username = username ?? string.Empty,
                DisplayName = username ?? string.Empty,
                Role = role ?? string.Empty,
                Permissions = permissions
            };

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取当前用户失败: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "操作失败" });
        }
    }

    /// <summary>
    /// 用户注销
    /// </summary>
    /// <returns>操作结果</returns>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        try
        {
            var userIdClaim = User.FindFirst("userId")?.Value;
            if (int.TryParse(userIdClaim, out var userId))
            {
                // 撤销该用户的所有 RefreshToken
                _refreshTokenRepository.RevokeAllUserTokens(userId);
            }

            // R503: 将当前 JWT 加入黑名单，使其立即失效
            // （否则 token 仍可使用至自然过期，最多 60 分钟）
            await RevokeCurrentJwtAsync("用户登出");

            _logger.LogInformation("用户 {Username} 注销", User.FindFirst("username")?.Value);
            return Ok(new { Message = "注销成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "注销失败: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "操作失败" });
        }
    }

    /// <summary>
    /// R503: 将当前请求的 JWT 加入黑名单。从 claims 中提取 jti 与 exp，
    /// 计算剩余 TTL 后写入分布式缓存，使后续请求的 OnTokenValidated 事件拒绝该 token。
    /// </summary>
    /// <param name="reason">撤销原因（写入缓存值与日志，便于审计）</param>
    private async Task RevokeCurrentJwtAsync(string reason)
    {
        var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (string.IsNullOrWhiteSpace(jti))
        {
            _logger.LogWarning("[R503] 当前 JWT 缺少 jti claim，无法加入黑名单");
            return;
        }

        var expClaim = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
        if (long.TryParse(expClaim, out var expUnix))
        {
            var expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
            await _jwtBlacklistService.RevokeAsync(jti, expiresAtUtc, reason);
        }
        else
        {
            // 没有 exp claim 时取配置的默认过期时间作为兜底（TTL 偏长不影响安全）
            var fallbackExpiresAt = DateTime.UtcNow.AddMinutes(60);
            _logger.LogWarning("[R503] 当前 JWT 缺少 exp claim，使用 60 分钟兜底 TTL。Jti={Jti}", jti);
            await _jwtBlacklistService.RevokeAsync(jti, fallbackExpiresAt, reason);
        }
    }

    /// <summary>
    /// 根据角色获取权限列表
    /// </summary>
    private static string[] GetPermissionsForRole(string role)
    {
        return role switch
        {
            "Admin" => new[] { "create", "read", "update", "delete" },
            "User" => new[] { "read" },
            _ => Array.Empty<string>()
        };
    }
}
