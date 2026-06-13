using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;

namespace Wms.Core.Domain.Entities.Warehouse;

/// <summary>
/// 单元 - 表示通道中的单元信息
/// </summary>
[Table("Cells")]
public class Cell
{
    /// <summary>
    /// 单元编号（主键）
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public virtual int CellId { get; set; }

    /// <summary>
    /// 通道编号
    /// </summary>
    public virtual int LanewayId { get; set; }

    /// <summary>
    /// 侧面
    /// </summary>
    public virtual int Side { get; set; }

    /// <summary>
    /// 列号
    /// </summary>
    public virtual int xColumn { get; set; }

    /// <summary>
    /// 层号
    /// </summary>
    public virtual int xLevel { get; set; }

    /// <summary>
    /// 形状
    /// </summary>
    [MaxLength(10)]
    public virtual string? Shape { get; set; }

    /// <summary>
    /// 按形状入库数量
    /// </summary>
    public virtual int iByShape { get; set; }

    /// <summary>
    /// 按形状出库数量
    /// </summary>
    public virtual int oByShape { get; set; }

    /// <summary>
    /// 入库数量1
    /// </summary>
    public virtual int i1 { get; set; }

    /// <summary>
    /// 出库数量1
    /// </summary>
    public virtual int o1 { get; set; }

    /// <summary>
    /// 入库数量2
    /// </summary>
    public virtual int i2 { get; set; }

    /// <summary>
    /// 出库数量2
    /// </summary>
    public virtual int o2 { get; set; }

    /// <summary>
    /// 入库数量3
    /// </summary>
    public virtual int i3 { get; set; }

    /// <summary>
    /// 出库数量3
    /// </summary>
    public virtual int o3 { get; set; }

    /// <summary>
    /// 层
    /// </summary>
    public virtual int? Level { get; set; }

    /// <summary>
    /// 按形状入库排序
    /// </summary>
    public virtual int? InboundOrderByShape { get; set; }

    /// <summary>
    /// 按形状出库排序
    /// </summary>
    public virtual int? OutboundOrderByShape { get; set; }

    /// <summary>
    /// 入库1
    /// </summary>
    public virtual int? In1 { get; set; }

    /// <summary>
    /// 出库1
    /// </summary>
    public virtual int? Out1 { get; set; }

    /// <summary>
    /// 入库2
    /// </summary>
    public virtual int? In2 { get; set; }

    /// <summary>
    /// 出库2
    /// </summary>
    public virtual int? Out2 { get; set; }

    /// <summary>
    /// 入库3
    /// </summary>
    public virtual int? In3 { get; set; }

    /// <summary>
    /// 出库3
    /// </summary>
    public virtual int? Out3 { get; set; }
}
