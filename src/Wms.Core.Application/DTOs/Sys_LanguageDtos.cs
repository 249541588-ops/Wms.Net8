namespace Wms.Core.Application.DTOs;

/// <summary>
/// 创建多语言请求
/// </summary>
public record CreateSys_LanguageRequest
{
    /// <summary>
    /// 中文
    /// </summary>
    public string Chinese { get; init; } = string.Empty;

    /// <summary>
    /// 中文描述
    /// </summary>
    public string? ChineseDesc { get; init; }

    /// <summary>
    /// 英文
    /// </summary>
    public string? English { get; init; }

    /// <summary>
    /// 德文
    /// </summary>
    public string? Deutsch { get; init; }

    /// <summary>
    /// 印尼文
    /// </summary>
    public string? Indonesian { get; init; }

    /// <summary>
    /// 模块
    /// </summary>
    public string? Module { get; init; }

    /// <summary>
    /// 是否包内容
    /// </summary>
    public int IsPackageContent { get; init; } = 0;

    /// <summary>
    /// 创建者
    /// </summary>
    public string? Creator { get; init; }
}

/// <summary>
/// 更新多语言请求
/// </summary>
public record UpdateSys_LanguageRequest
{
    /// <summary>
    /// 中文
    /// </summary>
    public string? Chinese { get; init; }

    /// <summary>
    /// 中文描述
    /// </summary>
    public string? ChineseDesc { get; init; }

    /// <summary>
    /// 英文
    /// </summary>
    public string? English { get; init; }

    /// <summary>
    /// 德文
    /// </summary>
    public string? Deutsch { get; init; }

    /// <summary>
    /// 印尼文
    /// </summary>
    public string? Indonesian { get; init; }

    /// <summary>
    /// 模块
    /// </summary>
    public string? Module { get; init; }

    /// <summary>
    /// 是否包内容
    /// </summary>
    public int? IsPackageContent { get; init; }

    /// <summary>
    /// 修改者
    /// </summary>
    public string? Modifier { get; init; }
}
