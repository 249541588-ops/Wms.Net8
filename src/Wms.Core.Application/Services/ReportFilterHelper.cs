namespace Wms.Core.Application.Services;

/// <summary>
/// 报表筛选参数工具 — 将前端传入的 JsonElement 值解包为 Dapper 可识别的原生类型
/// </summary>
public static class ReportFilterHelper
{
    /// <summary>
    /// 解包 JsonElement 值为原生类型
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static object? UnwrapValue(object? value)
    {
        if (value is System.Text.Json.JsonElement je)
        {
            return je.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => je.GetString(),
                System.Text.Json.JsonValueKind.Number => je.TryGetInt32(out var i) ? i : je.GetDouble(),
                System.Text.Json.JsonValueKind.True => true,
                System.Text.Json.JsonValueKind.False => false,
                System.Text.Json.JsonValueKind.Null => null,
                _ => value,
            };
        }
        return value;
    }
}
