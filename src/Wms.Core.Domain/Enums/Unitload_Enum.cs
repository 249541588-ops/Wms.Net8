
namespace Wms.Core.Domain.Enums
{
    /// <summary>
    /// 货载枚举
    /// </summary>
    public class Unitload_Enum
    {
        /// <summary>
        /// 当前工艺
        /// </summary>
        public enum CurrentOperation
        {

            /// <summary>
            /// 
            /// </summary>
            空托绑盘 = 1,

            /// <summary>
            /// 
            /// </summary>
            化成重组装盘 = 2,

            /// <summary>
            /// 
            /// </summary>
            分容重组装盘 = 3,            

            /// <summary>
            /// 
            /// </summary>
            一注装盘 = 10,

            /// <summary>
            /// 
            /// </summary>
            高温浸润 = 11,

            /// <summary>
            /// 
            /// </summary>
            化成 = 12,

            /// <summary>
            /// 
            /// </summary>
            清洗装盘 = 13,
            
            /// <summary>
            /// 
            /// </summary>
            分容 = 14,

            /// <summary>
            /// 
            /// </summary>
            常温一天 = 15,

            /// <summary>
            /// 
            /// </summary>
            OCV3 = 16,

            /// <summary>
            /// 
            /// </summary>
            常温七天 = 17,

            /// <summary>
            /// 
            /// </summary>
            OCV4 = 18,

            /// <summary>
            /// 
            /// </summary>
            分档 = 19,

            /// <summary>
            /// 
            /// </summary>
            成品 = 20,

        }

        /// <summary>
        /// 工艺次数
        /// </summary>
        public enum OperationNumber
        {

            /// <summary>
            /// 
            /// </summary>
            一次 = 1,

            /// <summary>
            /// 
            /// </summary>
            二次 = 2,

            /// <summary>
            /// 
            /// </summary>
            V3 = 31,

            /// <summary>
            /// 
            /// </summary>
            V4 = 32,

            /// <summary>
            /// 
            /// </summary>
            DCIR = 33,
        }

        /// <summary>
        /// 是否补电
        /// </summary>
        public enum IsSupplement
        {

            /// <summary>
            /// 
            /// </summary>
            默认 = 1,

            /// <summary>
            /// 
            /// </summary>
            补电 = 2,
        }

        /// <summary>
        /// 
        /// </summary>
        public enum UnitLoadErrMsg
        {

            /// <summary>
            /// 
            /// </summary>
            电芯异常,

            /// <summary>
            /// 
            /// </summary>
            托盘重码,

            /// <summary>
            /// 
            /// </summary>
            换盘异常,
        }

        /// <summary>
        /// 电芯状态
        /// </summary>
        public enum UnitloadItemDetailStatus
        {

            /// <summary>
            /// 
            /// </summary>
            正常,

            /// <summary>
            /// 
            /// </summary>
            漏码,

            /// <summary>
            /// 
            /// </summary>
            重码,

            /// <summary>
            /// 
            /// </summary>
            混批,

            /// <summary>
            /// 
            /// </summary>
            假电芯,

            /// <summary>
            /// 
            /// </summary>
            NG,

            /// <summary>
            /// 
            /// </summary>
            无档位,

            /// <summary>
            /// 
            /// </summary>
            混档,

            /// <summary>
            /// 
            /// </summary>
            条码异常,

            /// <summary>
            /// 
            /// </summary>
            混型,
        }

        /// <summary>
        /// NG 类型
        /// </summary>
        public enum UnitloadItemDetailLevel
        {

            /// <summary>
            /// 
            /// </summary>
            T,

            /// <summary>
            /// 
            /// </summary>
            H,

            /// <summary>
            /// 
            /// </summary>
            M,

            /// <summary>
            /// 
            /// </summary>
            HC_NG,

            /// <summary>
            /// 
            /// </summary>
            K,

            /// <summary>
            /// 
            /// </summary>
            ACIR_NG,
        }

        /// <summary>
        /// 分组
        /// </summary>
        public enum UnitloadStorageGroup
        {

            /// <summary>
            /// 
            /// </summary>
            普通,

            /// <summary>
            /// 
            /// </summary>
            化成,

            /// <summary>
            /// 
            /// </summary>
            分容,

            /// <summary>
            /// 
            /// </summary>
            二次化成,

            /// <summary>
            /// 
            /// </summary>
            二次分容,

            /// <summary>
            /// 
            /// </summary>
            空托盘,
        }


        /// <summary>
        /// 分容类型
        /// </summary>
        public enum UnitloadAdvance
        {

            /// <summary>
            /// 
            /// </summary>
            默认 = 0,

            /// <summary>
            /// 
            /// </summary>
            预测 = 1,

            /// <summary>
            /// 
            /// </summary>
            抽检 = 2,

            /// <summary>
            /// 
            /// </summary>
            返容 = 3,
        }

    }
}
