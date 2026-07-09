using System.IO;
using System.Xml;

namespace Wms.Core.Infrastructure.Security;

/// <summary>
/// XML 外部实体注入（XXE）防御工具 - 提供安全的 XML 解析器配置
/// </summary>
/// <remarks>
/// 适用范围：解析任何来源不可信的 XML 文档（如外部 SOAP 响应、第三方 API 返回、用户上传等）。
/// 防御策略（多层纵深防御）：
/// 1. <see cref="DtdProcessing.Prohibit"/>：完全禁止 DTD 声明，从源头阻断实体定义（包括内部实体、外部实体、参数实体）
/// 2. <see cref="MaxCharactersFromEntities"/>：限制实体扩展字符数，防御 Billion Laughs / Quadratic Blowup 等指数扩展 DoS
/// 3. <see cref="XmlResolver"/> 设为 null：禁用外部实体解析器，即便 DTD 被错误允许也不会发起外部请求（SSRF / 文件读取）
///
/// 攻击场景示例（杭可 SOAP 响应被中间人篡改或设备端被入侵）：
/// &lt;!DOCTYPE foo [&lt;!ENTITY xxe SYSTEM "file:///etc/passwd"&gt;]&gt;
/// &lt;CheckOutTrayResult&gt;&amp;xxe;&lt;/CheckOutTrayResult&gt;
///
/// 参考标准：
/// - OWASP XXE Prevention Cheat Sheet（C# / .NET）
/// - CERT: XML External Entity (XXE) Processing
/// </remarks>
public static class XmlSafety
{
    /// <summary>
    /// 实体扩展字符上限（1024 字符）。
    /// </summary>
    /// <remarks>
    /// 合法 SOAP 响应通常不依赖实体扩展；杭可接口返回的 &lt;*Result&gt; 元素内容是 JSON 字符串，
    /// 不应包含任何实体引用。设为 1024 既能兼容意外的微小实体，又能阻断 Billion Laughs（典型 payload 可扩展至 GB 级）。
    /// </remarks>
    public const long MaxCharactersFromEntities = 1024;

    /// <summary>
    /// 创建一个安全的 <see cref="XmlReaderSettings"/>，用于解析不可信 XML。
    /// </summary>
    /// <returns>已配置为禁用 DTD / 限制实体扩展 / 禁用外部解析的安全设置实例</returns>
    public static XmlReaderSettings CreateSafeReaderSettings()
    {
        return new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            MaxCharactersFromEntities = MaxCharactersFromEntities,
            XmlResolver = null
        };
    }

    /// <summary>
    /// 安全地将 XML 字符串解析为 <see cref="XDocument"/>，防御 XXE 与实体爆炸 DoS。
    /// </summary>
    /// <param name="xml">待解析的 XML 字符串（不可信来源）</param>
    /// <returns>解析后的 <see cref="XDocument"/> 实例</returns>
    /// <exception cref="System.Xml.XmlException">
    /// 当输入包含 DOCTYPE/ENTITY 声明（被 DtdProcessing.Prohibit 拒绝）、
    /// 或实体扩展超出 <see cref="MaxCharactersFromEntities"/> 上限时抛出。
    /// </exception>
    public static System.Xml.Linq.XDocument ParseSafe(string xml)
    {
        var settings = CreateSafeReaderSettings();
        using var reader = XmlReader.Create(new StringReader(xml), settings);
        return System.Xml.Linq.XDocument.Load(reader);
    }
}
