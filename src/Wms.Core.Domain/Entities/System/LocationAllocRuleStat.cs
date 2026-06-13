using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;

namespace Wms.Core.Domain.Entities.System;

/// <summary>
/// 库位分配规则统计
/// </summary>
[Table("LocationAllocRuleStats")]
public class LocationAllocRuleStat
{
    /// <summary>
    /// 规则名称（主键）
    /// </summary>
    public virtual string RuleName { get; set; } = string.Empty;

    /// <summary>
    /// 总次数
    /// </summary>
    public virtual int? TotalTimes { get; set; }

    /// <summary>
    /// 总时长
    /// </summary>
    public virtual double? TotalDuration { get; set; }

    /// <summary>
    /// 成功次数
    /// </summary>
    public virtual int? SuccessTimes { get; set; }

    /// <summary>
    /// 最近运行时间
    /// </summary>
    public virtual DateTime? LastRunTime { get; set; }

    /// <summary>
    /// 最近运行是否成功
    /// </summary>
    public virtual bool? LastRunSuccess { get; set; }

    /// <summary>
    /// 最近运行目标
    /// </summary>
    [MaxLength(20)]
    public virtual string? LastRunTarget { get; set; }

    /// <summary>
    /// 最近运行时长
    /// </summary>
    public virtual double? LastRunDuration { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(500)]
    public virtual string? Comment { get; set; }
}
