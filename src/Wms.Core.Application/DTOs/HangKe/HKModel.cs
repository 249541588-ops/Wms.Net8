namespace Wms.Core.Application.DTOs.HangKe;

/// <summary>
/// 杭可化成组盘请求模型
/// </summary>
public class HKModel
{
    public int? LoadNum { get; set; }
    public string? TrayCode { get; set; }
    public int? IsCheck { get; set; }
    public List<HKModelList>? CellList { get; set; }
}

/// <summary>
/// 杭可电芯列表项
/// </summary>
public class HKModelList
{
    public string? CellSn { get; set; }
    public int? Channel { get; set; }
}
