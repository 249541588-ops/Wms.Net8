namespace Wms.Core.Domain.Constants;

/// <summary>
/// 常量定义类
/// </summary>
public static class Cst
{
    /// <summary>
    /// StorageGroup 
    /// </summary>
    public const string 普通 = "普通";

    /// <summary>
    /// ContainerSpecification
    /// </summary>
    public const string 普通托盘 = "普通托盘";

    /// <summary>
    /// StockStatus
    /// </summary>
    public const string 合格 = "合格";

    /// <summary>
    /// 空字符串常量
    /// </summary>
    public const string None = "None";
    
    /// <summary>
    /// 空托盘操作类型
    /// </summary>
    public const string OpTypeEmpty = "RegisterEmpty";

    /// <summary>
    /// 任务类型：入库双叉
    /// </summary>
    public const string 入库双叉 = "入库双叉";

    /// <summary>
    /// 任务类型：入库
    /// </summary>
    public const string 入库 = "入库";

    /// <summary>
    /// 任务类型：出库
    /// </summary>
    public const string 出库 = "出库";

    /// <summary>
    /// 任务类型：移库
    /// </summary>
    public const string 移库 = "移库";

    /// <summary>
    /// 位置来源：起始位置（请求阶段）
    /// </summary>
    public const string LocationStart = "start";

    /// <summary>
    /// 位置来源：终点位置（完成阶段）
    /// </summary>
    public const string LocationEnd = "end";

    /// <summary>
    /// 流程阶段：请求阶段
    /// </summary>
    public const string PhaseRequest = "Request";

    /// <summary>
    /// 流程阶段：完成阶段
    /// </summary>
    public const string PhaseCompletion = "Completion";
}
