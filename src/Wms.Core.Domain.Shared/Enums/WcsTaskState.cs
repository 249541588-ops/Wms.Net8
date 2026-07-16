namespace Wms.Core.Domain.Enums;

///// <summary>
///// WCS 任务状态常量
///// </summary>
///// <remarks>
///// 状态流转：待下发 → 已下发 → 执行中 → 已完成 / 失败 / 已取消
///// </remarks>
//public static class WcsTaskState
//{
//    public const string 待下发 = "待下发";
//    public const string 已下发 = "已下发";
//    public const string 执行中 = "执行中";
//    public const string 已完成 = "已完成";
//    public const string 失败 = "失败";
//    public const string 已取消 = "已取消";
//}

/// <summary>
/// 提供任务接口表wcs_state列的状态值常量。
/// </summary>
public static class TaskInfoWcsStates
{
    /// <summary>
    /// 任务未由wcs读取。
    /// </summary>
    public const string Unread = "unread";

    /// <summary>
    /// 任务已由wcs读取。
    /// </summary>
    public const string Read = "read";

    /// <summary>
    /// 任务被wcs拒绝。需要wms处理。
    /// </summary>
    public const string Refused = "refused";

    /// <summary>
    /// 任务正在由wcs执行。
    /// </summary>
    public const string Running = "running";

    /// <summary>
    /// 任务已由wcs执行完成，需要wms处理。
    /// </summary>
    public const string Completed = "completed";

    /// <summary>
    /// 任务已由wcs取消，需要wms处理。
    /// </summary>
    public const string Cancelled = "cancelled";

    public static readonly IEnumerable<string> Finished = new[] { Completed, Cancelled, Refused };

}

/// <summary>
/// 提供任务接口表wms_state列的状态值常量。
/// </summary>
public static class TaskInfoWmsStates
{
    /// <summary>
    /// 任务由wms刚刚下发，wcs尚未读取。
    /// </summary>
    public const string Sent = "sent";

    /// <summary>
    /// 任务已由wms归档，需要wms清理。
    /// </summary>
    public const string Archived = "archived";

    /// <summary>
    /// wms在处理已完成的任务时失败，需要稍后重新处理。
    /// </summary>
    public const string Failed = "failed";

}
