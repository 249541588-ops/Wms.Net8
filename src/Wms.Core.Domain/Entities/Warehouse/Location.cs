using global::System.ComponentModel.DataAnnotations;
using global::System.ComponentModel.DataAnnotations.Schema;
using Wms.Core.Domain.Interfaces;

namespace Wms.Core.Domain.Entities.Warehouse;

/// <summary>
/// 库位 - 表示仓库中的库位信息
/// </summary>
[Table("Locations")]
public class Location : IEntity<int>, IAuditable
{
    /// <summary>
    /// 库位编号（主键）
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public virtual int LocationId { get; set; }

    /// <summary>
    /// 版本号
    /// </summary>
    public virtual int Version { get; set; }

    /// <summary>
    /// 库位编码
    /// </summary>
    [MaxLength(16)]
    public virtual string? LocationCode { get; set; }

    /// <summary>
    /// 库位类型
    /// </summary>
    [MaxLength(4)]
    public virtual string? LocationType { get; set; }

    /// <summary>
    /// 入库数量
    /// </summary>
    public virtual int InboundCount { get; set; }

    /// <summary>
    /// 入库上限
    /// </summary>
    public virtual int InboundLimit { get; set; }

    /// <summary>
    /// 是否禁止入库
    /// </summary>
    public virtual bool InboundDisabled { get; set; }

    /// <summary>
    /// 禁止入库备注
    /// </summary>
    [MaxLength(255)]
    public virtual string? InboundDisabledComment { get; set; }

    /// <summary>
    /// 出库数量
    /// </summary>
    public virtual int OutboundCount { get; set; }

    /// <summary>
    /// 出库上限
    /// </summary>
    public virtual int OutboundLimit { get; set; }

    /// <summary>
    /// 是否禁止出库
    /// </summary>
    public virtual bool OutboundDisabled { get; set; }

    /// <summary>
    /// 禁止出库备注
    /// </summary>
    [MaxLength(255)]
    public virtual string? OutboundDisabledComment { get; set; }

    /// <summary>
    /// 是否存在
    /// </summary>
    public virtual bool xExists { get; set; }

    /// <summary>
    /// 重量上限
    /// </summary>
    public virtual decimal WeightLimit { get; set; }

    /// <summary>
    /// 高度上限
    /// </summary>
    public virtual decimal HeightLimit { get; set; }

    /// <summary>
    /// 规格
    /// </summary>
    [MaxLength(16)]
    public virtual string? xSpecification { get; set; }

    /// <summary>
    /// 货架编号
    /// </summary>
    public virtual int? RackId { get; set; }

    /// <summary>
    /// 列号
    /// </summary>
    public virtual int xColumn { get; set; }

    /// <summary>
    /// 层号
    /// </summary>
    public virtual int xLevel { get; set; }

    /// <summary>
    /// 存储组
    /// </summary>
    [MaxLength(10)]
    public virtual string? StorageGroup { get; set; }

    /// <summary>
    /// 子存储组
    /// </summary>
    [MaxLength(50)]
    public virtual string? SubStorageGroup { get; set; }

    /// <summary>
    /// 操作序号
    /// </summary>
    public virtual int? OperationNumber { get; set; }

    /// <summary>
    /// 批次
    /// </summary>
    [MaxLength(20)]
    public virtual string? Batch { get; set; }

    /// <summary>
    /// 单元载荷数量
    /// </summary>
    public virtual int UnitloadCount { get; set; }

    /// <summary>
    /// 单元编号
    /// </summary>
    public virtual int? CellId { get; set; }

    /// <summary>
    /// 标签
    /// </summary>
    [MaxLength(50)]
    public virtual string? Tag { get; set; }

    /// <summary>
    /// 请求类型
    /// </summary>
    [MaxLength(16)]
    public virtual string? RequestType { get; set; }

    /// <summary>
    /// 仓库编号
    /// </summary>
    public virtual int? WarehouseId { get; set; }

    /// <summary>
    /// 区域名称
    /// </summary>
    [MaxLength(20)]
    public virtual string? AreaName { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(200)]
    public virtual string? Comment { get; set; }

    /// <summary>
    /// 其他编码
    /// </summary>
    [MaxLength(16)]
    public virtual string? AnotherCode { get; set; }

    /// <summary>
    /// HK定位状态
    /// </summary>
    public virtual int? HKPosintionState { get; set; }

    /// <summary>
    /// HK定位检查
    /// </summary>
    public virtual int? HKPosintionCK { get; set; }

    /// <summary>
    /// 通道编码集合
    /// </summary>
    [MaxLength(100)]
    public virtual string? LanewayCodes { get; set; }

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
    /// 双深标记
    /// </summary>
    public virtual int? DoubleIn { get; set; }

    /// <summary>
    /// 临时重量上限
    /// </summary>
    public virtual decimal? WeightLimitTemp { get; set; }

    /// <summary>
    /// 货架导航属性
    /// </summary>
    public virtual Rack? Rack { get; set; }

    /// <summary>
    /// 仓库导航属性
    /// </summary>
    public virtual Warehouse? Warehouse { get; set; }

    /// <summary>
    /// 初始化库位类的新实例
    /// </summary>
    public Location()
    {
        CreatedTime = DateTime.Now;
        ModifiedTime = DateTime.Now;
    }

    #region IEntity 成员

    /// <summary>
    /// 显式接口实现 - 返回 LocationId
    /// </summary>
    int IEntity<int>.Id { get => LocationId; set => LocationId = value; }

    /// <summary>
    /// 显式接口实现 - 返回 LocationId
    /// </summary>
    object IEntity.Id => LocationId;

    #endregion
}
