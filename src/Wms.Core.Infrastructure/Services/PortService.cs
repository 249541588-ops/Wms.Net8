using Wms.Core.Domain.Requests;
using Wms.Core.Domain.Entities;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Extensions;
using Wms.Core.Domain.Repositories;
using Wms.Core.Domain.Services;
using Wms.Core.Domain.Common;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Wms.Core.Infrastructure.Persistence;
using System.Text;
using Wms.Core.Domain.Entities.Warehouse;

namespace Wms.Core.Infrastructure.Services;

/// <summary>
/// 端口服务
/// </summary>
public class PortService : IPortService
{
    private readonly WmsDbContext _db;
    private readonly ILogger<PortService> _logger;
    private readonly IRepository<Port, int> _repository;
    private readonly ITranslationService _translationService;
    private readonly IMemoryCache _cache;

    public PortService(
        WmsDbContext db,
        IRepository<Port, int> repository,
        ITranslationService translationService,
        ILogger<PortService> logger,
        IMemoryCache cache)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _translationService = translationService ?? throw new ArgumentNullException(nameof(translationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <summary>
    /// 创建或更新端口，同时处理端口与巷道的关联关系
    /// </summary>
    public Result CreatePort(CreatePortRequest request)
    {
        if (request == null)
        {
            return Result.Fail("请求参数不能为空");
        }

        using var transaction = _db.Database.BeginTransaction();

        try
        {
            Port port;

            if (request.Id.HasValue)
            {
                // 编辑模式
                port = _repository.GetById(request.Id.Value);
                if (port == null)
                {
                    return Result.Fail("端口不存在", "404");
                }

                // 校验 PortCode 重复（排除自身）
                if (!string.IsNullOrEmpty(request.PortCode) && request.PortCode != port.PortCode
                    && _repository.Exists(m => m.PortCode == request.PortCode))
                {
                    return Result.Fail("端口编码已存在");
                }

                port.PortCode = request.PortCode ?? port.PortCode;
                port.PortName = request.PortName ?? port.PortName;
                port.PortType = request.PortType ?? port.PortType;
                port.IsAvailable = request.IsAvailable ?? port.IsAvailable;
                port.Comment = request.Comment ?? port.Comment;
                port.KP1 = request.KP1 ?? port.KP1;
                port.KP2 = request.KP2 ?? port.KP2;
                port.ModifiedTime = DateTime.Now;
                port.ModifiedBy = request.ModifiedBy;

                _repository.Update(port);
            }
            else
            {
                // 新建模式
                if (string.IsNullOrEmpty(request.PortCode))
                {
                    return Result.Fail("端口编码不能为空");
                }

                if (_repository.Exists(m => m.PortCode == request.PortCode))
                {
                    return Result.Fail("端口编码已存在");
                }

                port = new Port
                {
                    PortCode = request.PortCode,
                    PortName = request.PortName,
                    PortType = request.PortType,
                    IsAvailable = request.IsAvailable ?? false,
                    Comment = request.Comment,
                    KP1 = request.KP1,
                    KP2 = request.KP2,
                    CheckedAt = DateTime.Now,
                    CreatedTime = DateTime.Now,
                    ModifiedTime = DateTime.Now,
                    CreatedBy = request.CreatedBy
                };

                _repository.Add(port);
                _db.SaveChanges(); // 先保存获取自增 Id
            }

            // 处理巷道关联（仅当 Items 不为 null 时才处理，null 表示不修改关联）
            if (request.Items != null)
            {
                var existingLinks = _db.Set<LanewayPort>().Where(m => m.PortId == port.Id).ToList();
                if (existingLinks.Any())
                {
                    _db.Set<LanewayPort>().RemoveRange(existingLinks);
                }

                if (request.Items.Count > 0)
                {
                    var validItems = request.Items.Where(m => m.LanewayId.HasValue).ToList();
                    foreach (var item in validItems)
                    {
                        _db.Set<LanewayPort>().Add(new LanewayPort
                        {
                            PortId = port.Id,
                            LanewayId = item.LanewayId.Value
                        });
                    }
                }
            }

            _db.SaveChanges();
            transaction.Commit();

            return Result<Port>.Success(port, request.Id.HasValue ? "更新成功" : "创建成功");
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "创建/更新端口失败: {Message}", ex.Message);
            return Result.Fail(ex.Message);
        }
    }
}
