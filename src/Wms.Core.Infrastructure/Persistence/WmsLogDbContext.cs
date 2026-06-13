using Microsoft.EntityFrameworkCore;
using Wms.Core.Domain.Entities.Transport;

namespace Wms.Core.Infrastructure.Persistence;

/// <summary>
/// 独立日志数据库上下文（WmsLogsDb）
/// 用于存储接口通信日志（InterfaceLogs），与主库分离避免影响性能
/// </summary>
public class WmsLogDbContext : DbContext
{
    /// <summary>
    /// 接口通信日志
    /// </summary>
    public DbSet<InterfaceLog> InterfaceLogs { get; set; }

    /// <summary>
    /// 初始化独立日志数据库上下文
    /// </summary>
    public WmsLogDbContext(DbContextOptions<WmsLogDbContext> options)
        : base(options)
    {
    }
}
