using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.StockFlow;

/// <summary>
/// 月度报表 - 复合主键 (Month + v)
/// </summary>
[Table("MonthlyReports")]
public class MonthlyReport : IEntity
{
    /// <summary>
    /// 月份
    /// </summary>
    public virtual DateTime Month { get; set; }

    /// <summary>
    /// 版本号
    /// </summary>
    public virtual int v { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public virtual DateTime ctime { get; set; }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回第一个主键字段 Month
    /// </summary>
    object IEntity.Id => Month;

    #endregion
}
