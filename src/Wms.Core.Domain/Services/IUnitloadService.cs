using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Common;
using Wms.Core.Domain.Entities;
using Wms.Core.Domain.Requests;
using Wms.Core.Domain.ValueObjects;

namespace Wms.Core.Domain.Services;

/// <summary>
/// 货载数据接口
/// </summary>
public interface IUnitloadService
{
    /// <summary>
    /// 从电芯条码获取批次：第5位取3个，第14位开始取4个，共7位
    /// </summary>
    string? GetBatchFromBarcode(string barcode);

    /// <summary>
    /// 根据当前工艺获取下一工艺
    /// </summary>
    /// <param name="currentOperation"></param>
    /// <returns></returns>
    string? GetNextOperation(string currentOperation);

    /// <summary>
    /// 验证托盘码是否存在
    /// </summary>
    /// <param name="containerCode"></param>
    /// <returns></returns>
    bool IsUnitloadExist(string containerCode);

    /// <summary>
    /// 手工创建货载
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    Result CreateUnitloadManual(UnitloadRequest request);

    /// <summary>
    /// 自动创建货载
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    Result CreateUnitloadAutomatic(WcsRequest request);

    /// <summary>
    /// 删除
    /// </summary>
    /// <param name="unitloadId"></param>
    /// <returns></returns>
    Result Delete(int unitloadId);

    /// <summary>
    /// 归档
    /// </summary>
    /// <param name="unitloadId"></param>
    /// <returns></returns>
    Result Archive(int unitloadId, string? modifiedBy = null);
    
    /// <summary>
    /// 还原
    /// </summary>
    /// <param name="unitloadId"></param>
    /// <returns></returns>
    Result Recover(int unitloadId, string? modifiedBy = null);

    /// <summary>
    /// 更新货载（条码明细 + 可选容器编码）
    /// </summary>
    Result UpdateUnitload(UpdateUnitloadRequest request);

    /// <summary>
    /// 添加托盘操作日志
    /// </summary>
    void AddUnitloadOp(string containerCode, string opType, string direction, string? comment = null, string? createdBy = null);

    /// <summary>
    /// 生成随机电芯条码（24位格式）
    /// </summary>
    /// <param name="number">生成数量</param>
    /// <param name="month">月份（1-12）</param>
    /// <param name="day">日（1-30）</param>
    /// <param name="start">起始序号</param>
    /// <returns>位置→条码字典</returns>
    Dictionary<int, string> GenerateBatteryBarcodes(int number, int month, int day, int start);

}
