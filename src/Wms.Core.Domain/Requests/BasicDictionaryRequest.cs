using System;
using global::System.Collections.Generic;
using global::System.Linq;
using global::System.Text;
using global::System.Threading.Tasks;

namespace Wms.Core.Domain.Requests
{
    /// <summary>
    /// 创建请求
    /// </summary>
    public record CreateBasicDictionaryRequest
    {
        /// <summary>
        /// 
        /// </summary>
        public int ParentId { get; init; } = 0;

        /// <summary>
        /// 
        /// </summary>
        public int Sort { get; init; } = 1;

        /// <summary>
        /// 
        /// </summary>
        public int IsNext { get; init; } = 0;
        
        /// <summary>
        /// 
        /// </summary>
        public int Status { get; init; } = 1;

        /// <summary>
        /// 
        /// </summary>
        public string? No { get; init; }

        /// <summary>
        /// 
        /// </summary>
        public string? Name { get; init; }

        /// <summary>
        /// 
        /// </summary>
        public string? Value { get; init; }

        /// <summary>
        ///
        /// </summary>
        public string? Remarks { get; init; }

        /// <summary>
        /// 扩展字段1
        /// </summary>
        public string? ExpandField1 { get; init; }

        /// <summary>
        /// 扩展字段2
        /// </summary>
        public string? ExpandField2 { get; init; }

        /// <summary>
        /// 创建用户
        /// </summary>
        public string? CreatedBy { get; init; }
    }

    /// <summary>
    /// 更新请求
    /// </summary>
    public record UpdateBasicDictionaryRequest
    {
        /// <summary>
        /// 
        /// </summary>
        public int ParentId { get; init; } = 0;

        /// <summary>
        /// 
        /// </summary>
        public int Sort { get; init; } = 1;

        /// <summary>
        /// 
        /// </summary>
        public int IsNext { get; init; } = 0;

        /// <summary>
        /// 
        /// </summary>
        public int Status { get; init; } = 1;

        /// <summary>
        /// 
        /// </summary>
        public string? No { get; init; }

        /// <summary>
        /// 
        /// </summary>
        public string? Name { get; init; }

        /// <summary>
        /// 
        /// </summary>
        public string? Value { get; init; }

        /// <summary>
        ///
        /// </summary>
        public string? Remarks { get; init; }

        /// <summary>
        /// 扩展字段1
        /// </summary>
        public string? ExpandField1 { get; init; }

        /// <summary>
        /// 扩展字段2
        /// </summary>
        public string? ExpandField2 { get; init; }

        /// <summary>
        /// 修改用户
        /// </summary>
        public string? ModifiedBy { get; init; }
    }

    /// <summary>
    /// 设置启用状态请求
    /// </summary>
    public record SetBasicDictionaryEnabledRequest
    {
        /// <summary>
        /// 是否启用
        /// </summary>
        public bool Enabled { get; init; }

        /// <summary>
        /// 修改用户
        /// </summary>
        public string? ModifiedBy { get; init; }
    }
}
