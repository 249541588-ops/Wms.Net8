namespace Wms.Core.Domain.Extensions;

/// <summary>
/// 字符串扩展方法
/// </summary>
public static class StringExtensions
{
    private static readonly Random Random = new Random();
    private static readonly object _obj = new object();

    /// <summary>
    /// 检查字符串是否不为空或None
    /// </summary>
    public static bool IsNotNone(this string? value)
    {
        return !string.IsNullOrEmpty(value) && value != Constants.Cst.None;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static int GetInt(this object obj)
    {
        if (obj == null)
            return 0;
        int.TryParse(obj.ToString(), out int _number);
        return _number;

    }

    /// <summary>
    /// 获取时间戳      
    /// </summary>
    /// <returns></returns>
    public static string GenerateTimeStamp()
    {
        TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
        return Convert.ToInt64(ts.TotalSeconds).ToString() + Next(1000, 9999);
    }

    /// <summary>
    /// 返回指定范围内的随机整数。
    /// </summary>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <returns></returns>
    public static int Next(int min, int max)
    {
        lock (_obj)
        {
            return Random.Next(min, max);
        }
    }


}
