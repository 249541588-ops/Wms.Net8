namespace Wms.Core.Domain.Enums;

/// <summary>
/// 返回结果
/// </summary>
public enum ResponseType
{
    /// <summary>
    /// 成功
    /// </summary>
    OK = 200,

    /// <summary>
    /// 找不到
    /// </summary>
    None = 404,

    /// <summary>
    /// 失败
    /// </summary>
    Error = 500,

}
