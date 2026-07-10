namespace Wms.Core.Application.DTOs.HangKe;

/// <summary>
/// 杭可排废电芯回调数据
/// </summary>
public class CellCallback
{
    public List<JxsCellData>? JXSData { get; set; }
}

/// <summary>
/// 杭可 JXS 电芯数据项
/// </summary>
public class JxsCellData
{
    public string? CellSn { get; set; }
    public int? Status { get; set; }
    public string? Pick { get; set; }
}
