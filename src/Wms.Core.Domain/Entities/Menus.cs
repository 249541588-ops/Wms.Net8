using global::System.ComponentModel.DataAnnotations;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities;

/// <summary>
/// 应用设置 - 系统配置参数
/// </summary>
public class Menus : IEntity<int>
{
    /// <summary>
    /// 主键（设置名称）
    /// </summary>
    public virtual int Id { get; set; } 

    /// <summary>
    /// 父级
    /// </summary>
    public virtual int ParentId { get; set; } 

    /// <summary>
    /// 排序
    /// </summary>
    public virtual int Sort { get; set; } 

    /// <summary>
    /// 名称
    /// </summary>
    [MaxLength(50)]
    public virtual string? Name { get; set; }

    /// <summary>
    /// 英文名
    /// </summary>
    [MaxLength(50)]
    public virtual string? EnglishName { get; set; }

    /// <summary>
    /// 德语
    /// </summary>
    [MaxLength(50)]
    public virtual string? GermanName { get; set; }

    /// <summary>
    /// 路径
    /// </summary>
    [MaxLength(150)]
    public virtual string? Url { get; set; }

    /// <summary>
    /// 图标
    /// </summary>
    public virtual string? ImgUrl { get; set; }

    /// <summary>
    /// 状态： 0 显示 1 不显示
    /// </summary>    
    public virtual int IsDisplay { get; set; } = 0;


    /// <summary>
    ///
    /// </summary>
    public virtual string? FunctionButton { get; set; }

    /// <summary>
    ///
    /// </summary>
    public virtual string? Creator { get; set; }

    /// <summary>
    ///
    /// </summary>
    public virtual string? Editor { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public virtual DateTime CreateTime { get; set; } 

    /// <summary>
    /// 
    /// </summary>
    public virtual DateTime? EditTime{get;set;}
        

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 Id
    /// </summary>
    object IEntity.Id => Id;

    #endregion

    /// <summary>
    /// 初始化应用设置类的新实例
    /// </summary>
    public Menus()
    {
        CreateTime = DateTime.Now;
    }
    
}
