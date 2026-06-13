using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Wms.Core.WebApi.Helpers;

/// <summary>
/// 语言包缓存键跟踪器 - 用于跟踪所有语言包相关的缓存键
/// </summary>
public interface ILanguagePackCacheTracker
{
    /// <summary>
    /// 添加缓存键到跟踪列表
    /// </summary>
    void AddKey(string key);

    /// <summary>
    /// 获取所有已跟踪的缓存键
    /// </summary>
    IReadOnlyList<string> GetTrackedKeys();

    /// <summary>
    /// 清除所有跟踪的缓存键
    /// </summary>
    void ClearKeys();
}

/// <summary>
/// 语言包缓存键跟踪器实现
/// </summary>
public class LanguagePackCacheTracker : ILanguagePackCacheTracker
{
    private readonly ConcurrentDictionary<string, bool> _trackedKeys = new();
    private readonly ILogger<LanguagePackCacheTracker>? _logger;

    public LanguagePackCacheTracker(ILogger<LanguagePackCacheTracker>? logger = null)
    {
        _logger = logger;
    }

    public void AddKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        // 只添加语言包相关的缓存键
        if (key.StartsWith("GlobalLanguagePack_") || key.StartsWith("LanguagePack_"))
        {
            _trackedKeys.TryAdd(key, true);
            _logger?.LogDebug("添加缓存键到跟踪列表: {Key}", key);
        }
    }

    public IReadOnlyList<string> GetTrackedKeys()
    {
        return _trackedKeys.Keys.ToList();
    }

    public void ClearKeys()
    {
        var count = _trackedKeys.Count;
        _trackedKeys.Clear();
        _logger?.LogInformation("已清除缓存键跟踪列表，共 {Count} 个键", count);
    }
}
