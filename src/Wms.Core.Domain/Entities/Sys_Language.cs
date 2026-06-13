using global::System.ComponentModel.DataAnnotations;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities;

/// <summary>
/// 多语言
/// </summary>
public class Sys_Language : IEntity<int>
{
    /// <summary>
    /// 主键（设置名称）
    /// </summary>
    public virtual int Id { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [Required]
    [MaxLength(50)]
    public virtual string Chinese { get; set; } = string.Empty;

    /// <summary>
    ///
    /// </summary>
    public virtual string? ChineseDesc { get; set; }

    /// <summary>
    ///
    /// </summary>
    public virtual string? English { get; set; }

    /// <summary>
    ///
    /// </summary>
    public virtual string? Deutsch { get; set; }

    /// <summary>
    ///
    /// </summary>
    public virtual string? Indonesian { get; set; }

    /// <summary>
    /// 德语
    /// </summary>
    public virtual string? Module { get; set; }

    /// <summary>
    ///
    /// </summary>
    public virtual int IsPackageContent { get; set; }

    /// <summary>
    ///
    /// </summary>
    public virtual string? Creator { get; set; }

    /// <summary>
    ///
    /// </summary>
    public virtual string? Modifier { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public virtual DateTime CreateDate { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public virtual DateTime? ModifyDate { get; set; }


    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion

    /// <summary>
    /// 初始化应用设置类的新实例
    /// </summary>
    public Sys_Language()
    {
        CreateDate = DateTime.Now;
    }

}
