using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Wms.Core.WebApi.Filters;

/// <summary>
/// API 版本 Swagger 过滤器
/// </summary>
public class ApiVersionSwaggerFilter : IOperationFilter
{
    /// <summary>
    /// 应用过滤器
    /// </summary>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var apiVersion = context.ApiDescription.GetApiVersion();
        if (apiVersion == null) return;

        var versionParameter = operation.Parameters?.FirstOrDefault(p => p.Name == "api-version" && p.In == ParameterLocation.Query);
        if (versionParameter != null)
        {
            // 移除查询字符串中的 api-version 参数（因为我们在 URL 路径中使用版本）
            operation.Parameters.Remove(versionParameter);
        }
    }
}
