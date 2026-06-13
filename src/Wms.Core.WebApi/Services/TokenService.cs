using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Wms.Core.WebApi.Configuration;
using Wms.Core.WebApi.Models;

namespace Wms.Core.WebApi.Services;

/// <summary>
/// Token 服务实现
/// </summary>
public class TokenService : ITokenService
{
    private readonly JwtOptions _jwtOptions;

    public TokenService(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    /// <summary>
    /// 生成 JWT Token
    /// </summary>
    public (string token, DateTime expiration, string tokenId) GenerateToken(string userId, string username, string role, string[] permissions)
    {
        var expiration = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes);
        var tokenId = Guid.NewGuid().ToString();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new(JwtRegisteredClaimNames.Jti, tokenId),
            new(ClaimTypes.Role, role),
            new("userId", userId),
            new("username", username)
        };

        // 添加权限声明
        foreach (var permission in permissions)
        {
            claims.Add(new Claim("permission", permission));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expiration,
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return (tokenString, expiration, tokenId);
    }

    /// <summary>
    /// 生成刷新 Token
    /// </summary>
    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    /// <summary>
    /// 验证 Token 并提取用户信息
    /// </summary>
    public UserInfo? ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtOptions.SecretKey);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _jwtOptions.Issuer,
                ValidAudience = _jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);

            return new UserInfo
            {
                UserId = principal.FindFirst("userId")?.Value ?? string.Empty,
                Username = principal.FindFirst("username")?.Value ?? string.Empty,
                Role = principal.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty,
                Permissions = principal.FindAll("permission").Select(c => c.Value).ToArray()
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 从 Token 中获取用户 ID
    /// </summary>
    public string? GetUserIdFromToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jsonToken = tokenHandler.ReadJwtToken(token);
            return jsonToken.Claims.FirstOrDefault(c => c.Type == "userId")?.Value;
        }
        catch
        {
            return null;
        }
    }
}
