
namespace Wms.Core.Domain.ValueObjects
{
    /// <summary>
    /// 缓存Key类型
    /// </summary>
    public static class CacheKeyTypes
    {
        /// <summary>
        /// Echarts缓存Key时间（分钟）
        /// </summary>
        public const int TestCacheKeyTime = 1;

        /// <summary>
        /// 缓存Key时间（分钟）
        /// </summary>
        public const int CacheKeyTime = 30;

        /// <summary>
        /// 库区货位使用率缓存Key时间（分钟）
        /// </summary>
        public const int KqhwsylCacheKeyTime = 30;

        /// <summary>
        /// Echarts缓存Key时间（分钟）
        /// </summary>
        public const int EchartsCacheKeyTime = 4;

        /// <summary>
        /// Echarts_Houre缓存Key时间（分钟）
        /// </summary>
        public const int EchartsCacheKeyHour = 60;

        /// <summary>
        /// 物料类型缓存Key
        /// </summary>
        public const string MaterialCacheKey = "Material-";

        /// <summary>
        /// 所在库区缓存Key
        /// </summary>
        public const string LanewayListCacheKey = "LanewayList-";

        /// <summary>
        /// 所在库区缓存Key
        /// </summary>
        public const string LanewayArrayCacheKey = "LanewayArray-";

        /// <summary>
        /// 当前工艺缓存Key
        /// </summary>
        public const string CurrentOperationCacheKey = "CurrentOperation-";

        /// <summary>
        /// 电芯数量缓存Key
        /// </summary>
        public const string ElectricCodeCountCacheKey = "ElectricCodeCount-";

        /// <summary>
        /// 电芯数量 - 天缓存Key
        /// </summary>
        public const string ElectricCodeCountCacheKey_Day = "ElectricCodeCountDay-";

        /// <summary>
        /// 电芯数量 - 小时缓存Key
        /// </summary>
        public const string ElectricCodeCountCacheKey_Hour = "ElectricCodeCountHour-";

        /// <summary>
        /// 托盘数量 - 小时缓存Key
        /// </summary>
        public const string UnitLoadCodeCountCacheKey_Hour = "ElectricCodeCountHour-";

        /// <summary>
        /// 货载数量缓存Key
        /// </summary>
        public const string UnitloadCountCacheKey = "UnitloadCount-";

        /// <summary>
        /// 库区使用率缓存Key
        /// </summary>
        public const string KqsylCacheKey = "Kqsyl-";

        /// <summary>
        /// 看板首页货载情况缓存Key
        /// </summary>
        public const string HzqkCacheKey = "Hzqk-";

        /// <summary>
        /// 库存统计缓存Key
        /// </summary>
        public const string KctjCacheKey = "Kctj-";

        /// <summary>
        ///  库区出入库率报表缓存Key
        /// </summary>
        public const string KqtasktjCacheKey = "Kqtasktj-";

        /// <summary>
        /// 数据字典Value缓存Key
        /// </summary>
        public const string BasicValueCacheKey = "BasicValue-";

        /// <summary>
        /// 位置缓存Key
        /// </summary>
        public const string LocationCacheKey = "Location-";

        /// <summary>
        /// 电芯分档缓存Key
        /// </summary>
        public const string BatteryCacheKey = "Battery-";
    }
}
