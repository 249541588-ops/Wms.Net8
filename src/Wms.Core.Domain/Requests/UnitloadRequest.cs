using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.DataContracts;

namespace Wms.Core.Application.DTOs
{
    /// <summary>
    /// 
    /// </summary>
    public class UnitloadRequest
    {
        /// <summary>
        /// 容器编码。
        /// </summary>     
        public string[] ContainerCode { get; set; }

        //// <summary>
        /// 当前工艺
        /// </summary>  
        public string? CurrentOperation { get; set; } = string.Empty;

        /// <summary>
        /// 工艺次数
        /// </summary>   
        public int? OperationNumber { get; set; } = 1;       

        /// <summary>
        /// 物料
        /// </summary>     
        public int? MaterialId { get; set; }
       
        /// <summary>
        /// 当前位置
        /// </summary>  
        public string? LocationCode { get; set; }

        //// <summary>
        /// 分容类型
        /// </summary>   
        public int? IsAdvance { get; set; } = 0;

        /// <summary>
        /// 档位
        /// </summary>   
        public string? Level { get; set; } = string.Empty;

        /// <summary>
        /// 
        /// </summary>
        public string? CreatedBy { get; set; } = string.Empty;

        /// <summary>
        /// 
        /// </summary>
        public string? ModifiedBy { get; set; } = string.Empty;


        /// <summary>
        /// 明细
        /// </summary>
        public List<UnitloadRequestItem> Items { get; set; } = new List<UnitloadRequestItem>();

    }

    /// <summary>
    /// 
    /// </summary>
    public class UnitloadRequestItem
    {
        /// <summary>
        /// 位置
        /// </summary>
        public int? LocIndex { get; set; }

        /// <summary>
        /// 条码
        /// </summary>
        public string? BatteryCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// 更新货载请求
    /// </summary>
    public class UpdateUnitloadRequest
    {
        /// <summary>
        /// 货载ID
        /// </summary>
        public int UnitloadId { get; set; }

        /// <summary>
        /// 新容器编码（可选，不传则不更新）
        /// </summary>
        public string? NewContainerCode { get; set; }

        /// <summary>
        /// 更新的物料明细列表
        /// </summary>
        public List<UpdateUnitloadItemRequest> UnitloadItems { get; set; } = new();

        /// <summary>
        /// 修改人
        /// </summary>
        public string? ModifiedBy { get; set; }
    }

    /// <summary>
    /// 更新物料明细请求
    /// </summary>
    public class UpdateUnitloadItemRequest
    {
        /// <summary>
        /// 物料明细ID
        /// </summary>
        public int UnitloadItemId { get; set; }

        /// <summary>
        /// 新的电芯明细
        /// </summary>
        public List<UnitloadRequestItem> Items { get; set; } = new();
    }

}