namespace Wms.Core.Domain.Requests;

/// <summary>
/// 创建/更新出货口请求
/// </summary>
public class CreatePortRequest
{
    /// <summary>
    /// 端口ID（编辑时传入）
    /// </summary>
    public int? Id { get; set; }

    /// <summary>
    /// 编码
    /// </summary>
    public string? PortCode { get; set; }

    /// <summary>
    /// 名称
    /// </summary>
    public string? PortName { get; set; }

    /// <summary>
    /// 类型
    /// </summary>
    public string? PortType { get; set; }

    /// <summary>
    /// 是否可用
    /// </summary>
    public bool? IsAvailable { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// KP1
    /// </summary>
    public int? KP1 { get; set; }

    /// <summary>
    /// KP2
    /// </summary>
    public int? KP2 { get; set; }

    /// <summary>
    /// 修改用户
    /// </summary>
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// 创建用户
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// 关联的巷道列表
    /// </summary>
    public List<CreatePortRequestItem>? Items { get; set; }
}

/// <summary>
/// 端口-巷道关联项
/// </summary>
public class CreatePortRequestItem
{
    /// <summary>
    /// 端口ID
    /// </summary>
    public int? PortId { get; set; }

    /// <summary>
    /// 巷道ID
    /// </summary>
    public int? LanewayId { get; set; }
}
