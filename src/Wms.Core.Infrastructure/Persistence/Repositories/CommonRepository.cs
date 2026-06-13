using Wms.Core.Domain.Entities;
using Wms.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Wms.Core.Infrastructure.Persistence;

namespace Wms.Core.Infrastructure.Persistence.Repositories;

/// <summary>
/// 公共仓储实现
/// </summary>
public class CommonRepository : ICommonRepository
{
    private readonly WmsDbContext _db;

    /// <summary>
    /// 初始化仓储类的新实例
    /// </summary>
    public CommonRepository(WmsDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>
    /// 获取电芯批次
    /// </summary>
    /// <param name="barcode">电芯条码</param>
    /// <returns></returns>
    public string GetBattertBatch(string barcode)
    {
        string result = "";
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return result;
        }
        if (barcode.Length < 24)
        {
            return result;
        }
        result = barcode.Trim().Substring(5, 3);
        result += barcode.Trim().Substring(14, 4);
        return result;
    }
}
