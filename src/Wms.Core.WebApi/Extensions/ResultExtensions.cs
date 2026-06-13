using Wms.Core.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace Wms.Core.WebApi.Extensions;

/// <summary>
/// Result 类型到 ActionResult 的转换扩展
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// 将 Result 转换为 IActionResult
    /// </summary>
    public static IActionResult ToActionResult(this Result result)
    {
        if (result.IsSuccess)
        {
            return new OkObjectResult(new
            {
                status = true,
                code = result.Code,
                message = result.Error,
                data = (object?)null
            });
        }

        return result.Code switch
        {
            "404" => new NotFoundObjectResult(new
            {
                status = false,
                code = result.Code,
                message = result.Error
            }),
            "400" => new BadRequestObjectResult(new
            {
                status = false,
                code = result.Code,
                message = result.Error
            }),
            _ => new ObjectResult(new
            {
                status = false,
                code = result.Code,
                message = result.Error
            })
            {
                StatusCode = 500
            }
        };
    }

    /// <summary>
    /// 将 Result&lt;T&gt; 转换为 IActionResult
    /// </summary>
    public static IActionResult ToActionResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return new OkObjectResult(new
            {
                status = true,
                code = result.Code,
                message = result.Error,
                data = result.Data
            });
        }

        return result.Code switch
        {
            "404" => new NotFoundObjectResult(new
            {
                status = false,
                code = result.Code,
                message = result.Error
            }),
            "400" => new BadRequestObjectResult(new
            {
                status = false,
                code = result.Code,
                message = result.Error
            }),
            _ => new ObjectResult(new
            {
                status = false,
                code = result.Code,
                message = result.Error
            })
            {
                StatusCode = 500
            }
        };
    }
}
