using Microsoft.Data.SqlClient;
using Wms.Core.Domain.Entities.Warehouse;

namespace Wms.Core.Domain.Tasks;

/// <summary>
/// 入库货物存储信息（库位分配查询参数）
/// </summary>
public class UnitloadStorageInfo
{
    /// <summary>
    /// 重量
    /// </summary>
    public decimal Weight { get; set; }

    /// <summary>
    /// 高度
    /// </summary>
    public decimal Height { get; set; }

    /// <summary>
    /// 存储组
    /// </summary>
    public string StorageGroup { get; set; } = string.Empty;

    /// <summary>
    /// 子存储组
    /// </summary>
    public string? SubStorageGroup { get; set; }

    /// <summary>
    /// 容器规格
    /// </summary>
    public string? ContainerSpecification { get; set; }

    /// <summary>
    /// 出库标志
    /// </summary>
    public string? OutFlag { get; set; }
}

/// <summary>
/// 库位分配规则接口
/// </summary>
public interface ILocationAllocationRule
{
    /// <summary>
    /// 规则名称
    /// </summary>
    string RuleName { get; }

    /// <summary>
    /// 是否适用于双深巷道
    /// </summary>
    bool DoubleDeep { get; }

    /// <summary>
    /// 规则执行优先级（越小越优先）
    /// </summary>
    int Order { get; }

    /// <summary>
    /// 在指定巷道中分配一个库位
    /// </summary>
    /// <param name="connection">数据库连接</param>
    /// <param name="lanewayId">巷道ID</param>
    /// <param name="storageInfo">入库货物信息</param>
    /// <param name="excludedIds">排除的库位ID</param>
    /// <param name="excludedColumns">排除的列</param>
    /// <param name="excludedLevels">排除的层</param>
    /// <param name="orderBy">排序字段</param>
    /// <param name="sortMethod">排序方式（ASC/DESC）</param>
    /// <returns>分配的库位，null 表示未找到合适库位</returns>
    Task<Location?> SelectAsync(
        System.Data.IDbConnection connection,
        int lanewayId,
        UnitloadStorageInfo storageInfo,
        int[] excludedIds,
        int[] excludedColumns,
        int[] excludedLevels,
        string orderBy,
        string sortMethod);
}
