
namespace Wms.Core.Domain.Enums
{
    /// <summary>
    /// 接口枚举
    /// </summary>
    public class UnitloadOps_Enum
    {
        /// <summary>
        /// 类型
        /// </summary>
        public enum OpType
        {
            人工,
            自动,
            化成,
            分容,
            OCV3,
            OCV4,
            DCIR,
        }

        /// <summary>
        /// 货位状态
        /// </summary>
        public enum Direction
        {
            入库,
            出库,
            叠盘,
            拆盘,
            其他,
            移动
        }
    }
}
