using ExcelDataReader;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Linq;
using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Entities.Material;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Repositories;
using Wms.Core.Domain.Requests;
using Wms.Core.WebApi.Extensions;
using Wms.Core.WebApi.Filters;

namespace Wms.Core.WebApi.Controllers;

/// <summary>
/// 物料管理 API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public partial class MaterialsController : ControllerBase
{
    private readonly IRepository<Materials, int> _repository;
    private readonly ILogger<MaterialsController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public MaterialsController(
        IRepository<Materials, int> repository,
        ILogger<MaterialsController> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取物料列表
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result GetAll(string? keyword = null, int pageNumber = 1, int pageSize = 20)
    {
        try
        {
            pageSize = Math.Min(pageSize, 100);

            var query = _repository.GetAll();

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(m => m.MaterialCode!.Contains(keyword)
                    || (m.Description != null && m.Description.Contains(keyword)));
            }

            var totalCount = query.Count();

            var lists = query
                .OrderBy(m => m.MaterialId)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pagedResponse = new PagedResult<Materials>
            {
                Data = lists,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Result<PagedResult<Materials>>.Success(pagedResponse, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取列表失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 获取所有物料（不分页，用于下拉选择）
    /// </summary>
    [HttpGet("all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result GetAllList()
    {
        try
        {
            var list = _repository.GetAll()
                .OrderBy(m => m.MaterialId)
                .Select(m => new { m.MaterialId, m.MaterialCode, m.Description })
                .ToList();
            return Result<object>.Success(list, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取物料列表失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 根据 ID 获取物料
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Result GetById(int id)
    {
        try
        {
            var model = _repository.GetById(id);
            if (model == null)
            {
                return Result.Fail("记录不存在", "404");
            }

            return Result<Materials>.Success(model, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取对象失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 创建物料
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Result Create([FromBody] MaterialRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.MaterialCode))
            {
                var maxId = _repository.GetAll().Max(m => (int?)m.MaterialId) ?? 0;
                request.MaterialCode = $"M{DateTime.Now:yyyyMMdd}{(maxId + 1):D4}";
            }
            else if (_repository.Exists(m => m.MaterialCode == request.MaterialCode))
            {
                return Result.Fail("物料编码已存在");
            }

            var model = new Materials
            {
                MaterialCode = request.MaterialCode,
                MaterialType = request.MaterialType,
                Description = request.Description,
                SpareCode = request.SpareCode,
                Specification = request.Specification,
                MnemonicCode = request.MnemonicCode,
                BatchEnabled = request.BatchEnabled ?? false,
                MaterialGroup = request.MaterialGroup,
                ValidDays = request.ValidDays,
                StandingTime = request.StandingTime,
                AbcClass = request.AbcClass,
                Uom = request.Uom,
                DefaultStorageGroup = request.DefaultStorageGroup,
                Barcode = request.Barcode,
                Enabled = request.Enabled,
                UnitVolume = request.UnitVolume,
                UnitLength = request.UnitLength,
                UnitWidth = request.UnitWidth,
                UnitHeight = request.UnitHeight,
                UnitWeight = request.UnitWeight,
                LowerBound = request.LowerBound,
                UpperBound = request.UpperBound,
                DefaultQuantity = request.DefaultQuantity,
                Comment = request.Comment,
                CreatedTime = DateTime.Now,
                ModifiedTime = DateTime.Now,
                CreatedBy = request.CreatedBy
            };

            _repository.Add(model);

            return Result<Materials>.Success(model, "创建成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 更新物料
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Result Update(int id, [FromBody] MaterialRequest request)
    {
        try
        {
            var model = _repository.GetById(id);
            if (model == null)
            {
                return Result.Fail("记录不存在", "404");
            }

            if (!string.IsNullOrEmpty(request.MaterialCode) && request.MaterialCode != model.MaterialCode
                && _repository.Exists(m => m.MaterialCode == request.MaterialCode))
            {
                return Result.Fail("物料编码已存在");
            }

            model.MaterialCode = request.MaterialCode ?? model.MaterialCode;
            model.MaterialType = request.MaterialType ?? model.MaterialType;
            model.Description = request.Description ?? model.Description;
            model.SpareCode = request.SpareCode ?? model.SpareCode;
            model.Specification = request.Specification ?? model.Specification;
            model.MnemonicCode = request.MnemonicCode ?? model.MnemonicCode;
            model.BatchEnabled = request.BatchEnabled ?? model.BatchEnabled;
            model.MaterialGroup = request.MaterialGroup ?? model.MaterialGroup;
            model.ValidDays = request.ValidDays ?? model.ValidDays;
            model.StandingTime = request.StandingTime ?? model.StandingTime;
            model.AbcClass = request.AbcClass ?? model.AbcClass;
            model.Uom = request.Uom ?? model.Uom;
            model.DefaultStorageGroup = request.DefaultStorageGroup ?? model.DefaultStorageGroup;
            model.Barcode = request.Barcode ?? model.Barcode;
            model.Enabled = request.Enabled ?? model.Enabled;
            model.UnitVolume = request.UnitVolume ?? model.UnitVolume;
            model.UnitLength = request.UnitLength ?? model.UnitLength;
            model.UnitWidth = request.UnitWidth ?? model.UnitWidth;
            model.UnitHeight = request.UnitHeight ?? model.UnitHeight;
            model.UnitWeight = request.UnitWeight ?? model.UnitWeight;
            model.LowerBound = request.LowerBound ?? model.LowerBound;
            model.UpperBound = request.UpperBound ?? model.UpperBound;
            model.DefaultQuantity = request.DefaultQuantity ?? model.DefaultQuantity;
            model.Comment = request.Comment ?? model.Comment;
            model.ModifiedTime = DateTime.Now;
            model.ModifiedBy = request.ModifiedBy;

            _repository.Update(model);

            return Result<Materials>.Success(model, "更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 导入物料（Excel）
    /// </summary>
    [HttpPost("import")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<Result> Import(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return Result.Fail("请选择文件");

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            using var stream = file.OpenReadStream();
            using var reader = ExcelReaderFactory.CreateReader(stream);
            using var dataset = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = true,
                    FilterRow = rowReader =>
                    {
                        for (var i = 0; i < rowReader.FieldCount; i++)
                        {
                            try
                            {
                                if (!string.IsNullOrWhiteSpace(rowReader.GetString(i)))
                                    return true;
                            }
                            catch { }
                        }
                        return false;
                    }
                }
            });

            var table = dataset.Tables[0];
            if (table == null || table.Rows.Count == 0)
                return Result.Fail("Excel 文件为空或无有效数据");

            // 构建列名索引映射
            var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataColumn col in table.Columns)
                colMap[col.ColumnName] = col.Ordinal;

            // 获取自动编号基数
            var maxId = _repository.GetAll().Max(m => (int?)m.MaterialId) ?? 0;

            int successCount = 0;
            int skipCount = 0;
            var errors = new List<string>();

            for (int i = 0; i < table.Rows.Count; i++)
            {
                var row = table.Rows[i];
                try
                {
                    var materialType = GetCellValue(row, colMap, "类型");
                    var description = GetCellValue(row, colMap, "描述");
                    var specification = GetCellValue(row, colMap, "规格");
                    var uom = GetCellValue(row, colMap, "单位");
                    var spareCode = GetCellValue(row, colMap, "备用代码");
                    var mnemonicCode = GetCellValue(row, colMap, "助记码");
                    var comment = GetCellValue(row, colMap, "备注");

                    if (string.IsNullOrWhiteSpace(description))
                    {
                        skipCount++;
                        errors.Add($"第{i + 2}行：描述不能为空，已跳过");
                        continue;
                    }

                    maxId++;
                    var materialCode = $"M{DateTime.Now:yyyyMMdd}{maxId:D4}";

                    var entity = new Materials
                    {
                        MaterialCode = materialCode,
                        MaterialType = materialType,
                        Description = description,
                        Specification = specification,
                        Uom = uom,
                        SpareCode = spareCode,
                        MnemonicCode = mnemonicCode,
                        Comment = comment,
                        Enabled = true,
                        BatchEnabled = false,
                        CreatedTime = DateTime.Now,
                        ModifiedTime = DateTime.Now
                    };

                    _repository.Add(entity);
                    successCount++;
                }
                catch (Exception ex)
                {
                    skipCount++;
                    errors.Add($"第{i + 2}行：{ex.Message}");
                }
            }

            _logger.LogInformation("物料导入完成: 成功 {Success}, 跳过 {Skip}", successCount, skipCount);
            return Result<object>.Success(new { successCount, skipCount, errors }, $"导入完成，成功{successCount}条，跳过{skipCount}条");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "物料导入失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }

    private static string? GetCellValue(DataRow row, Dictionary<string, int> colMap, string colName)
    {
        if (!colMap.TryGetValue(colName, out var idx)) return null;
        if (idx >= row.Table.Columns.Count) return null;
        return row[idx]?.ToString();
    }

    /// <summary>
    /// 删除物料
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Result Delete(int id)
    {
        try
        {
            var model = _repository.GetById(id);
            if (model == null)
            {
                return Result.Fail("记录不存在", "404");
            }

            _repository.Delete(id);
            return Result.Success("删除成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }
}
