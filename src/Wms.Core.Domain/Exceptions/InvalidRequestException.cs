using global::System.Runtime.Serialization;

namespace Wms.Core.Domain.Exceptions;

/// <summary>
/// 无效请求异常 - 当请求参数不符合要求时抛出
/// </summary>
[Serializable]
public class InvalidRequestException : Exception
{
    /// <summary>
    /// 初始化无效请求异常类的新实例
    /// </summary>
    public InvalidRequestException()
        : base()
    {
    }

    /// <summary>
    /// 初始化无效请求异常类的新实例
    /// </summary>
    /// <param name="message">错误消息</param>
    public InvalidRequestException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 初始化无效请求异常类的新实例
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <param name="innerException">内部异常</param>
    public InvalidRequestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// 初始化无效请求异常类的新实例（用于序列化）
    /// </summary>
    protected InvalidRequestException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}
