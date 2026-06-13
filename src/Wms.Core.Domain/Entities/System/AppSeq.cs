using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;

namespace Wms.Core.Domain.Entities.System;

/// <summary>
/// 应用序列号
/// </summary>
[Table("AppSeqs")]
public class AppSeq
{
    /// <summary>
    /// 序列名称（主键）
    /// </summary>
    public virtual string SeqName { get; set; } = string.Empty;

    /// <summary>
    /// 下一个值
    /// </summary>
    public virtual int? NextVal { get; set; }

    /// <summary>
    /// 增量
    /// </summary>
    public virtual int? Increment { get; set; }

    /// <summary>
    /// 最小值
    /// </summary>
    public virtual int? MinValue { get; set; }

    /// <summary>
    /// 最大值
    /// </summary>
    public virtual int? MaxValue { get; set; }

    /// <summary>
    /// 是否循环
    /// </summary>
    public virtual bool? Cycle { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(255)]
    public virtual string? Comment { get; set; }
}
