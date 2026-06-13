using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.DataContracts;

namespace Wms.Core.Application.DTOs
{
    /// <summary>
    /// 
    /// </summary>
    public class WcsRequest
    {
        /// <summary>
        /// 当前位置
        /// </summary>            
        public string? LocationCode { get; set; }

        /// <summary>
        /// 容器编码。
        /// </summary>     
        public string[] ContainerCode { get; set; }

        /// <summary>
        /// 参数1
        /// </summary>  
        public string? Ex1 { get; set; } = string.Empty;

        /// <summary>
        /// 参数2
        /// </summary>   
        public string? Ex2 { get; set; } = string.Empty;

        /// <summary>
        /// 明细
        /// </summary>
        public Dictionary<int, string> Battery { get; set; } = new Dictionary<int, string>();

    }   

}