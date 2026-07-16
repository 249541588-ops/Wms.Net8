namespace Wms.Core.Domain.Common;

/// <summary>
/// 统一操作结果类型
/// </summary>
public class Result
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// 状态码
    /// </summary>
    public string Code { get; }

    protected Result(bool isSuccess, string? error, string code = "200")
    {
        IsSuccess = isSuccess;
        Error = error;
        Code = code;
    }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static Result Success(string? message = null)
    {
        return new Result(true, message, "200");
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static Result Fail(string error, string code = "500")
    {
        return new Result(false, error, code);
    }

    public static implicit operator bool(Result result) => result.IsSuccess;
}

/// <summary>
/// 带数据的统一操作结果类型
/// </summary>
public class Result<T> : Result
{
    /// <summary>
    /// 返回数据
    /// </summary>
    public T? Data { get; }

    private Result(bool isSuccess, T? data, string? error, string code = "200")
        : base(isSuccess, error, code)
    {
        Data = data;
    }

    /// <summary>
    /// 创建成功结果（带数据）
    /// </summary>
    public static Result<T> Success(T data, string? message = null)
    {
        return new Result<T>(true, data, message, "200");
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public new static Result<T> Fail(string error, string code = "500")
    {
        return new Result<T>(false, default, error, code);
    }
}
