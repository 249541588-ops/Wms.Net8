namespace Wms.Core.Application.DTOs.Mes;

/// <summary>
/// K值验证请求模型
/// </summary>
public class VerifyKValueVM
{
    /// <summary>
    /// 托盘编码
    /// </summary>
    public string? tpCode { get; set; }

    /// <summary>
    /// 工序号
    /// </summary>
    public int? type { get; set; }

    /// <summary>
    /// 电芯条码（逗号分隔）
    /// </summary>
    public string? productCodes { get; set; }
}
