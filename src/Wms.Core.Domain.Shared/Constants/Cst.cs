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
    /// 任务类型：入库空托
    /// </summary>
    public const string 入库空托 = "入库空托";

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
    /// 任务类型：叠盘
    /// </summary>
    public const string 叠盘 = "叠盘";

    /// <summary>
    /// 任务类型：排废
    /// </summary>
    public const string 排废 = "排废";

    /// <summary>
    /// 任务类型：排废更新
    /// </summary>
    public const string 排废更新 = "排废更新";

    /// <summary>
    /// 任务类型：批次验证
    /// </summary>
    public const string 批次验证 = "批次验证";

    /// <summary>
    /// 任务类型：工艺验证
    /// </summary>
    public const string 工艺验证 = "工艺验证";

    /// <summary>
    /// 任务类型：工艺次数验证
    /// </summary>
    public const string 工艺次数验证 = "工艺次数验证";

    /// <summary>
    /// 任务类型：档位验证
    /// </summary>
    public const string 档位验证 = "档位验证";

    /// <summary>
    /// 任务类型：托盘类型验证
    /// </summary>
    public const string 托盘类型验证 = "托盘类型验证";

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

    /// <summary>
    /// 字典分类编码：托盘类型验证 - 工序结果码映射
    /// 父级字典 No，子项 Name=工序名，Value=返回的 resultcode
    /// </summary>
    public const string 托盘类型验证映射 = "VERFIYPALLETTYPE_MAP";

    /// <summary>
    /// 字典特殊键：托盘类型验证 - 托盘不存在时的返回码（VERFIYPALLETTYPE_MAP 下的子项 No）
    /// </summary>
    public const string 托盘类型验证_NOT_EXIST = "NOT_EXIST";
}
