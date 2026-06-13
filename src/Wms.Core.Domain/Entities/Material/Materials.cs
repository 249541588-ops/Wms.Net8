using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Material;

/// <summary>
/// 物料
/// </summary>
[Table("Materials")]
public class Materials : IEntity<int>, IAuditable
{
    /// <summary>
    /// 物料ID
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public virtual int MaterialId { get; set; }

    /// <summary>
    /// 版本号
    /// </summary>
    public virtual int Version { get; set; }

    /// <summary>
    /// 物料编码
    /// </summary>
    [MaxLength(255)]
    public virtual string? MaterialCode { get; set; }

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
    [MaxLength(255)]
    public virtual string? CreatedBy { get; set; }

    /// <summary>
    /// 修改用户
    /// </summary>
    [MaxLength(255)]
    public virtual string? ModifiedBy { get; set; }

    #endregion

    /// <summary>
    /// 物料类型
    /// </summary>
    [MaxLength(255)]
    public virtual string? MaterialType { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    [MaxLength(255)]
    public virtual string? Description { get; set; }

    /// <summary>
    /// 备用编码
    /// </summary>
    [MaxLength(255)]
    public virtual string? SpareCode { get; set; }

    /// <summary>
    /// 规格
    /// </summary>
    [MaxLength(255)]
    public virtual string? Specification { get; set; }

    /// <summary>
    /// 助记码
    /// </summary>
    [MaxLength(255)]
    public virtual string? MnemonicCode { get; set; }

    /// <summary>
    /// 是否启用批次管理
    /// </summary>
    public virtual bool? BatchEnabled { get; set; } = false;

    /// <summary>
    /// 物料组
    /// </summary>
    [MaxLength(255)]
    public virtual string? MaterialGroup { get; set; }

    /// <summary>
    /// 有效天数
    /// </summary>
    public virtual decimal? ValidDays { get; set; }

    /// <summary>
    /// 停留时间
    /// </summary>
    public virtual decimal? StandingTime { get; set; }

    /// <summary>
    /// ABC分类
    /// </summary>
    [MaxLength(255)]
    public virtual string? AbcClass { get; set; }

    /// <summary>
    /// 计量单位
    /// </summary>
    [MaxLength(255)]
    public virtual string? Uom { get; set; }

    /// <summary>
    /// 默认存储组
    /// </summary>
    [MaxLength(255)]
    public virtual string? DefaultStorageGroup { get; set; }

    /// <summary>
    /// 条码
    /// </summary>
    [MaxLength(255)]
    public virtual string? Barcode { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public virtual bool? Enabled { get; set; }

    /// <summary>
    /// 单位体积
    /// </summary>
    public virtual decimal? UnitVolume { get; set; }

    /// <summary>
    /// 单位长度
    /// </summary>
    public virtual decimal? UnitLength { get; set; }

    /// <summary>
    /// 单位宽度
    /// </summary>
    public virtual decimal? UnitWidth { get; set; }

    /// <summary>
    /// 单位高度
    /// </summary>
    public virtual decimal? UnitHeight { get; set; }

    /// <summary>
    /// 单位重量
    /// </summary>
    public virtual decimal? UnitWeight { get; set; }

    /// <summary>
    /// 下限
    /// </summary>
    public virtual decimal? LowerBound { get; set; }

    /// <summary>
    /// 上限
    /// </summary>
    public virtual decimal? UpperBound { get; set; }

    /// <summary>
    /// 默认数量
    /// </summary>
    public virtual decimal? DefaultQuantity { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(255)]
    public virtual string? Comment { get; set; }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 MaterialId
    /// </summary>
    int IEntity<int>.Id { get => MaterialId; set => MaterialId = value; }

    object IEntity.Id => MaterialId;

    #endregion
}
