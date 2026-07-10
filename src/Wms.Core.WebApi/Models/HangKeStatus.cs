namespace Wms.Core.WebApi.Models;

/// <summary>
/// 杭可货位状态变更请求
/// </summary>
public class HangKeStatus
{
    /// <summary>
    /// 货位编码
    /// </summary>
    public string? LocationCode { get; set; }

    /// <summary>
    /// 杭可状态：1可入库 2可出库 3异常维护 4作业中 5温度报警 6烟雾报警 7作业完成 8移库
    /// </summary>
    public int HKState { get; set; }
}
