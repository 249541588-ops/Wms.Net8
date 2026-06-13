using global::System.ComponentModel.DataAnnotations;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities;

/// <summary>
/// 基础信息
/// </summary>
public class BasicDictionary : IEntity<int>, IAuditable
{
    /// <summary>
    /// 主键
    /// </summary>
    public virtual int Id { get; set; }

    /// <summary>
    /// 父级
    /// </summary>
    public virtual int ParentId { get; set; }

    /// <summary>
    /// 编码
    /// </summary>
    [Required]
    [MaxLength(100)]
    public virtual string No { get; set; } = string.Empty;

    #region IAuditable 实现

    /// <summary>
    /// 创建时间
    /// </summary>
    public virtual DateTime? CreatedTime { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    public virtual DateTime? ModifiedTime { get; set; }

    /// <summary>
    /// 创建用户
    /// </summary>
    [MaxLength(64)]
    public virtual string? CreatedBy { get; set; }

    /// <summary>
    /// 修改用户
    /// </summary>
    [MaxLength(64)]
    public virtual string? ModifiedBy { get; set; }

    #endregion

    /// <summary>
    /// 名称
    /// </summary>
    [MaxLength(150)]
    public virtual string? Name { get; set; } = string.Empty;

    /// <summary>
    /// 数值
    /// </summary>
    [Required]
    [MaxLength(250)]
    public virtual string Value { get; set; } = string.Empty;

    /// <summary>
    /// 缩写
    /// </summary>
    [MaxLength(20)]
    public virtual string? Abbreviation { get; set; }

    /// <summary>
    /// 全拼
    /// </summary>
    [MaxLength(50)]
    public virtual string? FullPinyin { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(500)]
    public virtual string? Remarks { get; set; }

    /// <summary>
    /// 排序
    /// </summary>
    public virtual int Sort { get; set; } = 1;

    /// <summary>
    /// 状态
    /// </summary>
   
    public virtual int Status { get; set; } = 1;
    
    /// <summary>
    /// 下级
    /// </summary>   
    public virtual int IsNext { get; set; } = 0;

    /// <summary>
    /// 
    /// </summary>
    [MaxLength(50)]
    public virtual string ExpandField1 { get; set; } = string.Empty;

    /// <summary>
    /// 
    /// </summary>
    [MaxLength(50)]
    public virtual string ExpandField2 { get; set; } = string.Empty;

    
    #region IEntity 成员

    object IEntity.Id => Id;

    #endregion

    /// <summary>
    /// 初始化物料类的新实例
    /// </summary>
    public BasicDictionary()
    {
        CreatedTime = DateTime.Now;
        ModifiedTime = DateTime.Now;
    }
    
}
