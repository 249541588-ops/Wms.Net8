using Wms.Core.Domain.Services;

namespace Wms.Core.Infrastructure.Services;

/// <summary>
/// BCrypt 密码哈希实现
/// </summary>
public class BcryptPasswordHasher : IPasswordHasher
{
    /// <summary>
    /// 哈希密码
    /// </summary>
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    /// <summary>
    /// 验证密码
    /// </summary>
    public bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
