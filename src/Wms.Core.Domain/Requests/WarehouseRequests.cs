namespace Wms.Core.Domain.Requests;

/// <summary>
/// 创建仓库请求
/// </summary>
public class CreateWarehouseRequest
{
    /// <summary>
    /// 用户编码
    /// </summary>
    public string? UserCode { get; set; }

    /// <summary>
    /// 仓库名称
    /// </summary>
    public string? xName { get; set; }

    /// <summary>
    /// 电话
    /// </summary>
    public string? Telephone { get; set; }

    /// <summary>
    /// 区域编码
    /// </summary>
    public string? AreaCode { get; set; }

    /// <summary>
    /// 地址
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// 邮编
    /// </summary>
    public string? PostCode { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Comments { get; set; }

    /// <summary>
    /// 创建用户
    /// </summary>
    public string? CreatedBy { get; set; }
}

/// <summary>
/// 更新仓库请求
/// </summary>
public class UpdateWarehouseRequest
{
    /// <summary>
    /// 用户编码
    /// </summary>
    public string? UserCode { get; set; }

    /// <summary>
    /// 仓库名称
    /// </summary>
    public string? xName { get; set; }

    /// <summary>
    /// 电话
    /// </summary>
    public string? Telephone { get; set; }

    /// <summary>
    /// 区域编码
    /// </summary>
    public string? AreaCode { get; set; }

    /// <summary>
    /// 地址
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// 邮编
    /// </summary>
    public string? PostCode { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Comments { get; set; }

    /// <summary>
    /// 修改用户
    /// </summary>
    public string? ModifiedBy { get; set; }
}
