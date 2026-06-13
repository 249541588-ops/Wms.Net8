using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.StockFlow;

/// <summary>
/// 月度报表明细 - 复合主键 (Month + Material + Batch + StockStatus + Uom)
/// </summary>
[Table("MonthlyReportEntries")]
public class MonthlyReportEntry : IEntity
{
    /// <summary>
    /// 月份
    /// </summary>
    public virtual DateTime Month { get; set; }

    /// <summary>
    /// 物料ID
    /// </summary>
    public virtual int Material { get; set; }

    /// <summary>
    /// 批次
    /// </summary>
    [MaxLength(20)]
    public virtual string? Batch { get; set; }

    /// <summary>
    /// 库存状态
    /// </summary>
    [MaxLength(10)]
    public virtual string? StockStatus { get; set; }

    /// <summary>
    /// 计量单位
    /// </summary>
    [MaxLength(8)]
    public virtual string? Uom { get; set; }

    /// <summary>
    /// 版本号
    /// </summary>
    public virtual int v { get; set; }

    /// <summary>
    /// 期初
    /// </summary>
    public virtual decimal Beginning { get; set; }

    /// <summary>
    /// 入库
    /// </summary>
    public virtual decimal Incoming { get; set; }

    /// <summary>
    /// 出库
    /// </summary>
    public virtual decimal Outgoing { get; set; }

    /// <summary>
    /// 期末
    /// </summary>
    public virtual decimal Ending { get; set; }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回第一个主键字段 Month
    /// </summary>
    object IEntity.Id => Month;

    #endregion
}
