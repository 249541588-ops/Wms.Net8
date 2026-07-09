using Microsoft.EntityFrameworkCore;
using Wms.Core.Domain.Entities.Container;
using Wms.Core.Domain.Repositories;
using Wms.Core.Domain.Requests;
using Wms.Core.Application.Ports;

namespace Wms.Core.Infrastructure.Services;

/// <summary>
/// 电芯分选服务实现
/// </summary>
public class BatteryCellSortingService : IBatteryCellSortingService
{
    private readonly IRepository<BatteryCellSorting, int> _repository;

    public BatteryCellSortingService(IRepository<BatteryCellSorting, int> repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public (IEnumerable<BatteryCellSorting> Data, int TotalCount) GetPagedList(string? keyword, short? isEnable, int? materialId, int pageNumber, int pageSize)
    {
        pageSize = Math.Min(pageSize, 100);
        IQueryable<BatteryCellSorting> query = _repository.GetAll().Include(s => s.Material);

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(s => s.PickName!.Contains(keyword)
                || s.PickId!.Contains(keyword)
                || s.Passageway!.Contains(keyword));
        }

        if (isEnable.HasValue)
        {
            query = query.Where(s => s.IsEnable == isEnable.Value);
        }

        if (materialId.HasValue)
        {
            query = query.Where(s => s.MaterialId == materialId.Value);
        }

        var totalCount = query.Count();
        var data = query
            .OrderBy(s => s.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (data, totalCount);
    }

    public BatteryCellSorting? GetById(int id)
    {
        return _repository.GetById(id);
    }

    public BatteryCellSorting Create(BatteryCellSortingRequest request)
    {
        var entity = new BatteryCellSorting
        {
            MaterialId = request.MaterialId ?? 0,
            PickName = request.PickName ?? string.Empty,
            PickId = request.PickId ?? string.Empty,
            XSpecification = request.XSpecification ?? string.Empty,
            CapacityMin = request.CapacityMin ?? 0,
            CapacityMax = request.CapacityMax ?? 0,
            OCV4Min = request.OCV4Min ?? 0,
            OCV4Max = request.OCV4Max ?? 0,
            IR4Min = request.IR4Min ?? 0,
            IR4Max = request.IR4Max ?? 0,
            KValMin = request.KValMin ?? 0,
            KValMax = request.KValMax ?? 0,
            DcirnzMin = request.DcirnzMin ?? 0,
            DcirnzMax = request.DcirnzMax ?? 0,
            Passageway = request.Passageway ?? string.Empty,
            IsEnable = request.IsEnable ?? 1,
            CreatedTime = DateTime.Now,
            ModifiedTime = DateTime.Now,
            CreatedBy = request.CreatedBy
        };

        _repository.Add(entity);
        return entity;
    }

    public BatteryCellSorting? Update(int id, BatteryCellSortingRequest request)
    {
        var entity = _repository.GetById(id);
        if (entity == null) return null;

        if (request.MaterialId.HasValue) entity.MaterialId = request.MaterialId.Value;
        if (request.PickName != null) entity.PickName = request.PickName;
        if (request.PickId != null) entity.PickId = request.PickId;
        if (request.XSpecification != null) entity.XSpecification = request.XSpecification;
        if (request.CapacityMin.HasValue) entity.CapacityMin = request.CapacityMin.Value;
        if (request.CapacityMax.HasValue) entity.CapacityMax = request.CapacityMax.Value;
        if (request.OCV4Min.HasValue) entity.OCV4Min = request.OCV4Min.Value;
        if (request.OCV4Max.HasValue) entity.OCV4Max = request.OCV4Max.Value;
        if (request.IR4Min.HasValue) entity.IR4Min = request.IR4Min.Value;
        if (request.IR4Max.HasValue) entity.IR4Max = request.IR4Max.Value;
        if (request.KValMin.HasValue) entity.KValMin = request.KValMin.Value;
        if (request.KValMax.HasValue) entity.KValMax = request.KValMax.Value;
        if (request.DcirnzMin.HasValue) entity.DcirnzMin = request.DcirnzMin.Value;
        if (request.DcirnzMax.HasValue) entity.DcirnzMax = request.DcirnzMax.Value;
        if (request.Passageway != null) entity.Passageway = request.Passageway;
        if (request.IsEnable.HasValue) entity.IsEnable = request.IsEnable.Value;
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
