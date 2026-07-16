
namespace Wms.Core.Domain.Enums
{
    /// <summary>
    /// 接口枚举
    /// </summary>
    public class Interface_Enum
    {
        /// <summary>
        /// 类型
        /// </summary>
        public enum OpType
        {
            WCS,
            昊宸,
            ABB,
            宏伟,
            杭可,
            擎天
        }

        /// <summary>
        /// 货位状态
        /// </summary>
        public enum LocationStatus
        {
            正在入库,
            已入库,
            尾盘,
            正在出库,
            已出库,
            有货,
            无货
        }


        /// <summary>
        /// 货位状态
        /// </summary>
        public enum HKLocationStatus
        {
            可入库=1,
            可出库=2,
            异常维护=3,
            作业中=4,
            温度警报=5,
            烟雾报警=6,
            作业完成=7,
            移库=8,
        }
        /// <summary>
        /// 托盘类型
        /// </summary>
        public enum ContainerSpecification
        {
            正常托盘,
            负压工装,
            检线工装,
            校准工装,
            插吸嘴工装,
            拔吸嘴工装,
        }
    }
}
