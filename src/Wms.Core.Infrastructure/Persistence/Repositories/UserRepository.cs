using Microsoft.EntityFrameworkCore;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.Domain.Entities.Identity;
using Wms.Core.Domain.Repositories;

namespace Wms.Core.Infrastructure.Persistence.Repositories;

/// <summary>
/// 用户仓储实现
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly WmsDbContext _db;

    /// <summary>
    /// 初始化用户仓储类的新实例
    /// </summary>
    /// <param name="session">NHibernate会话</param>
    public UserRepository(WmsDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>
    /// 根据用户名获取用户
    /// </summary>
    public virtual User? GetByUsername(string username)
    {
        return _db.Set<User>()
            .Include(u => u.Roles)
            .FirstOrDefault(u => u.UserName == username);
    }

    /// <summary>
    /// 检查用户名是否存在
    /// </summary>
    public virtual bool Exists(string username)
    {
        return _db.Set<User>()
            .Any(u => u.UserName == username);
    }

    /// <summary>
    /// 获取所有用户
    /// </summary>
    public virtual IQueryable<User> GetAll()
    {
        return _db.Set<User>();
    }

    /// <summary>
    /// 根据角色获取用户列表
    /// </summary>
    public virtual IQueryable<User> GetByRole(string role)
    {
        return _db.Set<User>()
            .Where(u => u.Roles.Any(r => r.RoleName == role));
    }

    /// <summary>
    /// 根据Id获取用户
    /// </summary>
    /// <param name="userid"></param>
    /// <returns></returns>
    public User? GetByUserid(int userid)
    {
        return _db.Set<User>()
            .FirstOrDefault(u => u.Id == userid);
    }
}
