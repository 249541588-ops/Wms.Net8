using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wms.Core.Infrastructure.Persistence.CodeGeneration;

namespace Wms.Core.WebApi.Controllers;

/// <summary>
/// 代码生成 API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/[controller]")]
[Produces("application/json")]
[AllowAnonymous]
//[Authorize(Roles = "Admin")]
public class CodeGenerationController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CodeGenerationController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public CodeGenerationController(
        IConfiguration configuration,
        ILogger<CodeGenerationController> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取数据库中所有表名
    /// </summary>
    /// <returns>表名列表</returns>
    [HttpGet("tables")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetTables()
    {
        try
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
                return BadRequest(new { Message = "数据库连接字符串未配置" });

            var tables = new List<object>();
            using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
            connection.Open();

            var sql = @"
SELECT TABLE_SCHEMA, TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE'
ORDER BY TABLE_SCHEMA, TABLE_NAME";

            using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, connection);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                tables.Add(new
                {
                    Schema = reader.GetString(0),
                    TableName = reader.GetString(1)
                });
            }

            return Ok(new { data = tables, totalCount = tables.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取数据库表列表失败: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, new { Message = ex.Message });
        }
    }

    /// <summary>
    /// 生成指定表的实体类和 Mapping 类
    /// </summary>
    /// <param name="request">生成请求</param>
    /// <returns>生成结果</returns>
    [HttpPost("generate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Generate([FromBody] GenerateRequest request)
    {
        try
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
                return BadRequest(new { Message = "数据库连接字符串未配置" });

            // 计算输出路径：基于项目根目录
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var idx = basePath.IndexOf("src\\", StringComparison.OrdinalIgnoreCase);
            var srcRoot = idx >= 0 ? basePath[..(idx + 4)] : basePath;

            var entityPath = Path.GetFullPath(Path.Combine(srcRoot, "Wms.Core.Domain", "Entities"));
            var mappingPath = Path.GetFullPath(Path.Combine(srcRoot, "Wms.Core.Infrastructure", "Persistence", "Mappings"));

            var generator = new EntityGenerator(
                connectionString,
                entityPath,
                mappingPath);

            var result = generator.GenerateAsync(
                request.TableNames?.Length > 0 ? request.TableNames : null,
                request.Overwrite).GetAwaiter().GetResult();

            _logger.LogInformation("代码生成完成：实体类 {EntityCount} 个，映射类 {MappingCount} 个，跳过 {SkipCount} 个",
                result.GeneratedEntities.Count,
                result.GeneratedMappings.Count,
                result.Skipped.Count);

            return Ok(new
            {
                generatedEntities = result.GeneratedEntities.Select(Path.GetFileName),
                generatedMappings = result.GeneratedMappings.Select(Path.GetFileName),
                skipped = result.Skipped,
                summary = result.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "代码生成失败: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, new { Message = ex.Message });
        }
    }

    /// <summary>
    /// 生成指定表的 DTO record 类
    /// </summary>
    /// <param name="request">DTO 生成请求</param>
    /// <returns>生成结果</returns>
    [HttpPost("generate-dto")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GenerateDto([FromBody] GenerateDtoRequest request)
    {
        try
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
                return BadRequest(new { Message = "数据库连接字符串未配置" });

            if (string.IsNullOrWhiteSpace(request.MainTable))
                return BadRequest(new { Message = "主表名不能为空" });

            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var idx = basePath.IndexOf("src\\", StringComparison.OrdinalIgnoreCase);
            var srcRoot = idx >= 0 ? basePath[..(idx + 4)] : basePath;

            var dtoPath = Path.GetFullPath(Path.Combine(srcRoot, "Wms.Core.Application", "DTOs",
                $"{EntityGenerator.ToPascalCase(request.MainTable)}Dtos.cs"));

            var generator = new EntityGenerator(connectionString, "", "");

            var result = generator.GenerateDtoAsync(
                mainTable: request.MainTable,
                detailTable: request.DetailTable,
                outputPath: dtoPath,
                overwrite: request.Overwrite).GetAwaiter().GetResult();

            _logger.LogInformation("DTO 生成完成：{Count} 个文件，跳过 {SkipCount} 个",
                result.GeneratedEntities.Count,
                result.Skipped.Count);

            return Ok(new
            {
                generated = result.GeneratedEntities.Select(Path.GetFileName),
                skipped = result.Skipped,
                summary = result.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DTO 生成失败: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, new { Message = ex.Message });
        }
    }
}

#region DTOs

/// <summary>
/// 代码生成请求
/// </summary>
public record GenerateRequest
{
    /// <summary>
    /// 要生成的表名列表，为空则生成所有表
    /// </summary>
    public string[]? TableNames { get; init; }

    /// <summary>
    /// 是否覆盖已存在的文件，默认 false
    /// </summary>
    public bool Overwrite { get; init; }
}

/// <summary>
/// DTO 生成请求
/// </summary>
public record GenerateDtoRequest
{
    /// <summary>
    /// 主表名
    /// </summary>
    public string MainTable { get; init; } = string.Empty;

    /// <summary>
    /// 明细表名（可选）
    /// </summary>
    public string? DetailTable { get; init; }

    /// <summary>
    /// 是否覆盖已存在的文件，默认 false
    /// </summary>
    public bool Overwrite { get; init; }
}

#endregion
