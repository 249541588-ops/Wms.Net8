using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Services;

namespace Wms.Core.Infrastructure.Services;

/// <summary>
/// 容器编码验证器实现
/// </summary>
public class ContainerCodeValidator : IContainerCodeValidator
{
    private readonly ILogger<ContainerCodeValidator> _logger;

    /// <summary>
    /// 初始化容器编码验证器的新实例
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public ContainerCodeValidator(ILogger<ContainerCodeValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 验证容器编码
    /// </summary>
    /// <param name="containerCode">容器编码</param>
    /// <param name="msg">错误消息</param>
    /// <returns>验证是否通过</returns>
    public virtual bool ValidateContainerCode(string containerCode, out string msg)
    {
        if (string.IsNullOrWhiteSpace(containerCode))
        {
            msg = "容器编码不能为空。";
            _logger.LogWarning("容器编码验证失败: 容器编码为空");
            return false;
        }

        if (containerCode.Trim() != containerCode)
        {
            msg = "容器编码首尾不能有空白字符。";
            _logger.LogWarning("容器编码验证失败: 首尾有空白字符, 值: '{ContainerCode}'", containerCode);
            return false;
        }

        if (containerCode.ToUpper() != containerCode)
        {
            msg = "容器编码中的字符必须大写。";
            _logger.LogWarning("容器编码验证失败: 字符非大写, 值: '{ContainerCode}'", containerCode);
            return false;
        }

        msg = ValidateContainerCodeEx(containerCode);

        if (msg != "OK")
        {
            _logger.LogWarning("容器编码扩展验证失败: {Message}, 值: '{ContainerCode}'", msg, containerCode);
            return false;
        }

        _logger.LogDebug("容器编码验证通过: '{ContainerCode}'", containerCode);
        return true;
    }

    /// <summary>
    /// 验证容器编码的扩展规则
    /// 子类可重写此方法以实现自定义验证规则
    /// </summary>
    /// <param name="containerCode">容器编码</param>
    /// <returns>成功返回 "OK"，失败返回错误消息</returns>
    protected virtual string ValidateContainerCodeEx(string containerCode)
    {
        return "OK";
    }
}
