namespace Wms.Core.WebApi.Helpers;

/// <summary>
/// 文件操作公共工具类
/// </summary>
public static class FileHelper
{
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
    private static readonly string[] ExcelExtensions = { ".xlsx", ".xls", ".csv" };

    /// <summary>
    /// 确保目录存在
    /// </summary>
    public static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    /// <summary>
    /// 生成唯一文件名（GUID + 时间戳 + 原始扩展名）
    /// </summary>
    public static string GenerateFileName(string originalName)
    {
        var ext = Path.GetExtension(originalName);
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        var guid = Guid.NewGuid().ToString("N")[..8];
        return $"{timestamp}_{guid}{ext}";
    }

    /// <summary>
    /// 按模块获取子目录路径
    /// </summary>
    public static string GetModulePath(string basePath, string module)
    {
        var safeModule = SanitizeFileName(module ?? "default");
        var monthDir = DateTime.Now.ToString("yyyyMM");
        var path = Path.Combine(basePath, safeModule, monthDir);
        EnsureDirectory(path);
        return path;
    }

    /// <summary>
    /// 是否为允许的图片扩展名
    /// </summary>
    public static bool IsAllowedImageExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ImageExtensions.Contains(ext);
    }

    /// <summary>
    /// 是否为允许的 Excel 扩展名
    /// </summary>
    public static bool IsAllowedExcelExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ExcelExtensions.Contains(ext);
    }

    /// <summary>
    /// 清理文件名中的非法字符
    /// </summary>
    public static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "default";
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid));
    }
}
