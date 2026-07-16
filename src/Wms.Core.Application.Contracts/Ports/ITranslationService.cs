namespace Wms.Core.Application.Ports;

/// <summary>
/// 翻译服务接口 - 提供多语言翻译功能
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// 根据键获取翻译值
    /// </summary>
    /// <param name="key">翻译键（中文原文）</param>
    /// <param name="defaultValue">默认值（如果找不到翻译）</param>
    /// <returns>翻译后的值</returns>
    string? Translate(string key, string? defaultValue = null);
}
