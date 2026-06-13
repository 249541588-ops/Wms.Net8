namespace Wms.Core.Domain.Services;

/// <summary>
/// 容器编码验证器接口
/// </summary>
public interface IContainerCodeValidator
{
    /// <summary>
    /// 验证容器编码
    /// </summary>
    bool ValidateContainerCode(string containerCode, out string msg);
}
