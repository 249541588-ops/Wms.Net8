namespace Wms.Core.Application.DTOs.Mes;

/// <summary>
/// MES 接口请求模型
/// </summary>
public class MesInterfaceVM
{
    public int tenantID { get; set; }
    public string? technicsProcessCode { get; set; }
    public string? technicsProcessName { get; set; }
    public string? deviceCode { get; set; }
    public string? deviceName { get; set; }
    public string? productCode { get; set; }
    public int productCount { get; set; }
    public int productQuality { get; set; }
    public string? userAccount { get; set; }
    public string? startTime { get; set; }
    public string? endTime { get; set; }
    public string? produceDate { get; set; }
    public List<ProduceParamEntityList> produceParamEntityList { get; set; } = new();
}

/// <summary>
/// MES 生产参数实体列表
/// </summary>
public class ProduceParamEntityList
{
    public string? producode { get; set; }
    public string? technicsParamCode { get; set; }
    public string? technicsParamName { get; set; }
    public string? technicsParamValue { get; set; }
    public string? technicsParamQuality { get; set; }
}
