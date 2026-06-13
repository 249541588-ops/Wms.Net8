using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Wms.Core.Domain.Entities;
using Wms.Core.Domain.Entities.Identity;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Repositories;
using Wms.Core.Domain.Services;
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

    /// <summary>
    /// 构造函数
    /// </summary>
    public AuthController(
        ITokenService tokenService,
        IAuthService authService,
        IRefreshTokenRepository refreshTokenRepository,
        ILogger<AuthController> logger,
        WmsDbContext dbContext)
    {
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _refreshTokenRepository = refreshTokenRepository ?? throw new ArgumentNullException(nameof(refreshTokenRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = dbContext;
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

            // 保存 RefreshToken 到数据库（有效期 7 天）
            var refreshTokenEntity = new RefreshToken
            {
                Token = refreshTokenString,
                JwtTokenId = tokenId,
                UserId = userEntity.Id,
                UserName = userEntity.UserName,
                CreatedTime = DateTime.UtcNow,
                ExpiryTime = DateTime.UtcNow.AddDays(7),
                IpAddress = HttpContext.Connection?.RemoteIpAddress?.ToString(),
                UserAgent = HttpContext.Request?.Headers["User-Agent"].ToString()
            };

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
            return Result.Fail(ex.Message);
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
            if (refreshToken == null || !refreshToken.IsValid())
            {
                _logger.LogWarning("刷新 Token 无效或已过期");
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

            // 保存新的 RefreshToken
            var newRefreshToken = new RefreshToken
            {
                Token = newRefreshTokenString,
                JwtTokenId = tokenId,
                UserId = userEntity.Id,
                CreatedTime = DateTime.UtcNow,
                ExpiryTime = DateTime.UtcNow.AddDays(7),
                IpAddress = HttpContext.Connection?.RemoteIpAddress?.ToString(),
                UserAgent = HttpContext.Request?.Headers["User-Agent"].ToString()
            };

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
            return StatusCode(StatusCodes.Status500InternalServerError, new { Message = ex.Message });
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
            return StatusCode(StatusCodes.Status500InternalServerError, new { Message = ex.Message });
        }
    }

    /// <summary>
    /// 用户注销
    /// </summary>
    /// <returns>操作结果</returns>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Logout()
    {
        try
        {
            var userIdClaim = User.FindFirst("userId")?.Value;
            if (int.TryParse(userIdClaim, out var userId))
            {
                // 撤销该用户的所有 RefreshToken
                _refreshTokenRepository.RevokeAllUserTokens(userId);
            }

            _logger.LogInformation("用户 {Username} 注销", User.FindFirst("username")?.Value);
            return Ok(new { Message = "注销成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "注销失败: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, new { Message = ex.Message });
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
