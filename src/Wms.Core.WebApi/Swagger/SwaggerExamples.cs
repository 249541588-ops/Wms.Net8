using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Wms.Core.WebApi.Swagger;

/// <summary>
/// Swagger 示例过滤器
/// </summary>
public class SwaggerExamplesFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Placeholder for Swagger examples filtering
    }
}

/// <summary>
/// Swagger 文档过滤器
/// </summary>
public class SwaggerDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // Placeholder for Swagger document filtering
    }
}
