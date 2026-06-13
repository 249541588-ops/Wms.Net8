using Microsoft.EntityFrameworkCore;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Repositories;
using Wms.Core.Domain.Requests;
using Wms.Core.Domain.Services;

namespace Wms.Core.Infrastructure.Services;

/// <summary>
/// 电芯服务实现
/// </summary>
public class BatteryCellService : IBatteryCellService
{
    private readonly IRepository<BatteryCell, int> _repository;

    public BatteryCellService(IRepository<BatteryCell, int> repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public (IEnumerable<BatteryCell> Data, int TotalCount) GetPagedList(string? keyword, int pageNumber, int pageSize)
    {
        pageSize = Math.Min(pageSize, 100);
        IQueryable<BatteryCell> query = _repository.GetAll().Include(s => s.Material);

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(s => s.BarCode!.Contains(keyword)
                || s.Batch!.Contains(keyword)
                || s.Sequence!.Contains(keyword));
        }

        var totalCount = query.Count();
        var data = query
            .OrderBy(s => s.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (data, totalCount);
    }

    public BatteryCell? GetById(int id)
    {
        return _repository.GetById(id);
    }

    public BatteryCell Create(BatteryCellRequest request)
    {
        var entity = new BatteryCell
        {
            MaterialId = request.MaterialId ?? 0,
            IsSendPack = request.IsSendPack ?? 0,
            Batch = request.Batch ?? string.Empty,
            BarCode = request.BarCode ?? string.Empty,
            xLevel = request.XLevel ?? string.Empty,
            OCV3 = request.OCV3 ?? 0,
            IR3 = request.IR3 ?? 0,
            V3KeYa = request.V3KeYa ?? 0,
            OCV4 = request.OCV4 ?? 0,
            IR4 = request.IR4 ?? 0,
            V4KeYa = request.V4KeYa ?? 0,
            Capacity = request.Capacity ?? 0,
            KVal = request.KVal ?? 0,
            CCP = request.CCP ?? 0,
            Dcirnz = request.Dcirnz ?? 0,
            Sequence = request.Sequence ?? string.Empty,
            LocIndex = request.LocIndex ?? 0,
            Status = request.Status ?? "(0)",
            Comment = request.Comment ?? string.Empty,
            OperationNumber = request.OperationNumber ?? 1,
            IsAdvance = request.IsAdvance ?? 0,
            ContainerCode = request.ContainerCode ?? string.Empty,
            CreatedTime = DateTime.Now,
            ModifiedTime = DateTime.Now,
            CreatedBy = request.CreatedBy
        };

        _repository.Add(entity);
        return entity;
    }

    public BatteryCell? Update(int id, BatteryCellRequest request)
    {
        var entity = _repository.GetById(id);
        if (entity == null) return null;

        if (request.MaterialId.HasValue) entity.MaterialId = request.MaterialId.Value;
        if (request.IsSendPack.HasValue) entity.IsSendPack = request.IsSendPack.Value;
        if (request.Batch != null) entity.Batch = request.Batch;
        if (request.BarCode != null) entity.BarCode = request.BarCode;
        if (request.XLevel != null) entity.xLevel = request.XLevel;
        if (request.OCV3.HasValue) entity.OCV3 = request.OCV3.Value;
        if (request.IR3.HasValue) entity.IR3 = request.IR3.Value;
        if (request.V3KeYa.HasValue) entity.V3KeYa = request.V3KeYa.Value;
        if (request.OCV4.HasValue) entity.OCV4 = request.OCV4.Value;
        if (request.IR4.HasValue) entity.IR4 = request.IR4.Value;
        if (request.V4KeYa.HasValue) entity.V4KeYa = request.V4KeYa.Value;
        if (request.Capacity.HasValue) entity.Capacity = request.Capacity.Value;
        if (request.KVal.HasValue) entity.KVal = request.KVal.Value;
        if (request.CCP.HasValue) entity.CCP = request.CCP.Value;
        if (request.Dcirnz.HasValue) entity.Dcirnz = request.Dcirnz.Value;
        if (request.Sequence != null) entity.Sequence = request.Sequence;
        if (request.LocIndex.HasValue) entity.LocIndex = request.LocIndex.Value;
        if (request.Status != null) entity.Status = request.Status;
        if (request.Comment != null) entity.Comment = request.Comment;
        if (request.OperationNumber.HasValue) entity.OperationNumber = request.OperationNumber.Value;
        if (request.IsAdvance.HasValue) entity.IsAdvance = request.IsAdvance.Value;
        if (request.ContainerCode != null) entity.ContainerCode = request.ContainerCode;
        entity.ModifiedTime = DateTime.Now;
        entity.ModifiedBy = request.ModifiedBy;

        _repository.Update(entity);
        return entity;
    }

    public bool Delete(int id)
    {
        var entity = _repository.GetById(id);
        if (entity == null) return false;

        _repository.Delete(id);
        return true;
    }
}
