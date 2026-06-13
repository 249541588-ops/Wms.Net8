using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;

namespace Wms.Core.Domain.Entities.System;

/// <summary>
/// 应用配置
/// </summary>
[Table("AppSettings")]
public class AppSetting
{
    /// <summary>
    /// 配置名称（主键）
    /// </summary>
    public virtual string SettingName { get; set; } = string.Empty;

    /// <summary>
    /// 配置类型
    /// </summary>
    [MaxLength(10)]
    public virtual string? SettingType { get; set; }

    /// <summary>
    /// 配置值 (nvarchar(MAX))
    /// </summary>
    public virtual string Value { get; set; } = string.Empty;

    /// <summary>
    /// 备注
    /// </summary>
    public virtual string? Comment { get; set; }

    /// <summary>
    /// 分类
    /// </summary>
    [MaxLength(50)]
    public virtual string? Category { get; set; }

    /// <summary>
    /// 是否只读
    /// </summary>
    public virtual bool? IsReadOnly { get; set; }
}
