namespace Wms.Core.Application.DTOs;

#region 聚合响应 DTO

/// <summary>
/// Dashboard 首页聚合数据（summary 接口返回）
/// </summary>
public class DashboardSummaryDto
{
    /// <summary>6 个统计卡片数据</summary>
    public DashboardStatsDto Stats { get; set; } = new();

    /// <summary>库存预警列表（Top 10 低库存物料）</summary>
    public List<AlertItemDto> LowStockAlerts { get; set; } = new();

    /// <summary>最近入库单（Top 5）</summary>
    public List<RecentOrderDto> RecentInboundOrders { get; set; } = new();

    /// <summary>最近出库单（Top 5）</summary>
    public List<RecentOrderDto> RecentOutboundOrders { get; set; } = new();

    /// <summary>待办任务（Top 10 待执行 TransTask）</summary>
    public List<PendingTaskDto> PendingTasks { get; set; } = new();

    /// <summary>数据生成时间（用于前端显示"最后更新"）</summary>
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// 6 个统计卡片数据
/// </summary>
public class DashboardStatsDto
{
    /// <summary>总库存记录数（Stock 表行数）</summary>
    public int TotalStockCount { get; set; }

    /// <summary>库存总数量（Stock.Quantity 之和）</summary>
    public decimal TotalStockQuantity { get; set; }

    /// <summary>待入库单数</summary>
    public int PendingInboundCount { get; set; }

    /// <summary>待出库单数</summary>
    public int PendingOutboundCount { get; set; }

    /// <summary>待执行任务数（未发送 WCS 的 TransTask）</summary>
    public int PendingTaskCount { get; set; }

    /// <summary>库位总数</summary>
    public int TotalLocationCount { get; set; }

    /// <summary>已占用库位数</summary>
    public int OccupiedLocationCount { get; set; }

    /// <summary>库位利用率（百分比，0~100，保留 2 位小数）</summary>
    public decimal LocationUtilizationRate { get; set; }

    /// <summary>库存告警物料种类数（低于安全库存下限）</summary>
    public int StockAlertCount { get; set; }
}

#endregion

#region 趋势图 DTO

/// <summary>
/// 趋势图响应（trend 接口返回）
/// </summary>
public class TrendResultDto
{
    /// <summary>天数（7 或 30）</summary>
    public int Days { get; set; }

    /// <summary>数据点列表</summary>
    public List<TrendPointDto> Points { get; set; } = new();

    /// <summary>生成时间</summary>
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// 单日趋势数据点
/// </summary>
public class TrendPointDto
{
    /// <summary>日期（yyyy-MM-dd）</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>入库量（BizType 为 入库/入库双叉 的 Quantity 之和）</summary>
    public decimal InboundQuantity { get; set; }

    /// <summary>出库量（BizType 为 出库 的 Quantity 之和）</summary>
    public decimal OutboundQuantity { get; set; }

    /// <summary>入库托盘数（去重 ContainerCode）</summary>
    public int InboundTrayCount { get; set; }

    /// <summary>出库托盘数（去重 ContainerCode）</summary>
    public int OutboundTrayCount { get; set; }
}

/// <summary>
/// 趋势图原生 SQL 查询中间类型（EF Core SqlQueryRaw 映射用）
/// </summary>
public class TrendRow
{
    public DateTime D { get; set; }
    public string Dir { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public int Trays { get; set; }
}

#endregion

#region 列表面板 DTO

/// <summary>
/// 库存预警项
/// </summary>
public class AlertItemDto
{
    /// <summary>物料 ID</summary>
    public int MaterialId { get; set; }

    /// <summary>物料编码</summary>
    public string? MaterialCode { get; set; }

    /// <summary>物料描述</summary>
    public string? Description { get; set; }

    /// <summary>当前库存总量</summary>
    public decimal CurrentStock { get; set; }

    /// <summary>安全库存下限（Materials.LowerBound）</summary>
    public decimal? LowerBound { get; set; }

    /// <summary>缺口数量 = LowerBound - CurrentStock</summary>
    public decimal Shortage { get; set; }

    /// <summary>告警级别："critical"（低于下限 50%）/ "warning"</summary>
    public string AlertLevel { get; set; } = "warning";
}

/// <summary>
/// 最近订单（入库/出库通用）
/// </summary>
public class RecentOrderDto
{
    /// <summary>订单 ID</summary>
    public int Id { get; set; }

    /// <summary>订单编号</summary>
    public string? OrderCode { get; set; }

    /// <summary>业务类型</summary>
    public string? BizType { get; set; }

    /// <summary>业务订单号</summary>
    public string? BizOrder { get; set; }

    /// <summary>状态</summary>
    public string? Status { get; set; }

    /// <summary>是否已关闭</summary>
    public bool? Closed { get; set; }

    /// <summary>创建时间</summary>
    public DateTime? CreatedTime { get; set; }

    /// <summary>创建人</summary>
    public string? CreatedBy { get; set; }
}

/// <summary>
/// 待办任务
/// </summary>
public class PendingTaskDto
{
    /// <summary>任务 ID</summary>
    public int Id { get; set; }

    /// <summary>任务编码</summary>
    public string? TaskCode { get; set; }

    /// <summary>任务类型（入库/出库/移库...）</summary>
    public string? TaskType { get; set; }

    /// <summary>托盘编码（来自 Unitload.ContainerCode）</summary>
    public string? ContainerCode { get; set; }

    /// <summary>起始库位编码</summary>
    public string? StartLocationCode { get; set; }

    /// <summary>目标库位编码</summary>
    public string? EndLocationCode { get; set; }

    /// <summary>是否已发送 WCS</summary>
    public bool? WasSentToWcs { get; set; }

    /// <summary>订单编号</summary>
    public string? OrderCode { get; set; }

    /// <summary>创建时间</summary>
    public DateTime? CreatedTime { get; set; }
}

#endregion
